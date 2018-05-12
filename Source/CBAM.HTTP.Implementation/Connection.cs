/*
 * Copyright 2017 Stanislav Muhametsin. All rights Reserved.
 *
 * Licensed  under the  Apache License,  Version 2.0  (the "License");
 * you may not use  this file  except in  compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed  under the  License is distributed on an "AS IS" BASIS,
 * WITHOUT  WARRANTIES OR CONDITIONS  OF ANY KIND, either  express  or
 * implied.
 *
 * See the License for the specific language governing permissions and
 * limitations under the License. 
 */
using System;

using CBAM.Abstractions.Implementation;
using UtilPack;
using UtilPack.AsyncEnumeration;
using UtilPack.ResourcePooling;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

using TCreationParameter = UtilPack.EitherOr<CBAM.HTTP.HTTPRequest, System.Func<CBAM.HTTP.HTTPRequest>>;
using CBAM.HTTP;
using System.Text;
using CBAM.HTTP.Implementation;
using System.Collections.Generic;

namespace CBAM.HTTP.Implementation
{
   internal sealed class HTTPConnectionImpl : ConnectionImpl<HTTPStatement, HTTPStatementInformation, TCreationParameter, HTTPResponse, HTTPConnectionVendorFunctionality, IAsyncConcurrentEnumerable<HTTPResponse>, IAsyncConcurrentEnumerableObservable<HTTPResponse, HTTPStatementInformation>, HTTPConnectionVendorImpl, HTTPConnectionFunctionalityImpl>, HTTPConnection
   {
      public HTTPConnectionImpl(
         HTTPConnectionFunctionalityImpl functionality
         ) : base( functionality )
      {
      }

      protected override IAsyncConcurrentEnumerableObservable<HTTPResponse, HTTPStatementInformation> CreateObservable( IAsyncConcurrentEnumerable<HTTPResponse> enumerable, HTTPStatementInformation info )
      {
         return enumerable.AsObservable( info );
      }
   }

   internal sealed class HTTPConnectionFunctionalityImpl : DefaultConnectionFunctionality<HTTPStatement, HTTPStatementInformation, TCreationParameter, HTTPConnectionVendorImpl, IAsyncConcurrentEnumerable<HTTPResponse>>
   {
      private const Int32 MIN_BUFFER_SIZE = 1024;

      private readonly ExplicitAsyncResourcePool<Stream> _streamPool;

      // TODO Use AsyncResourcePool<ResizableArray<Byte>, TimeSpan> except that it should be Sync
      private readonly LocklessInstancePoolForClasses<ResizableArray<Byte>> _writeBuffers;
      private readonly LocklessInstancePoolForClasses<ResizableArray<Byte>> _readBuffers;
      private readonly Int32 _writerBufferLimit;
      private readonly Int32 _readerBufferLimit;
      //private readonly Encoding _encoding;

      public HTTPConnectionFunctionalityImpl(
         HTTPConnectionVendorImpl vendorFunctionality,
         ExplicitAsyncResourcePool<Stream> streamPool,
         Int32? readerBufferLimit,
         Int32? writerBufferLimit
         //Encoding encoding
         ) : base( vendorFunctionality )
      {
         this._streamPool = ArgumentValidator.ValidateNotNull( nameof( streamPool ), streamPool );
         this._writeBuffers = new DefaultLocklessInstancePoolForClasses<ResizableArray<Byte>>();
         this._readBuffers = new DefaultLocklessInstancePoolForClasses<ResizableArray<Byte>>();
         this._readerBufferLimit = readerBufferLimit.HasValue && readerBufferLimit.Value > 0 ? Math.Max( MIN_BUFFER_SIZE, readerBufferLimit.Value ) : -1;
         this._writerBufferLimit = writerBufferLimit.HasValue && writerBufferLimit.Value > 0 ? Math.Max( MIN_BUFFER_SIZE, writerBufferLimit.Value ) : -1;
         //this._encoding = encoding;
      }

      protected override IAsyncConcurrentEnumerable<HTTPResponse> CreateEnumerable(
         HTTPStatementInformation metadata
         )
      {
         var list = new List<ExplicitResourceAcquireInfo<Stream>>();
         var generator = ( (HTTPStatementInformationImpl) metadata ).MessageGenerator;
         return AsyncEnumerationFactory.CreateConcurrentEnumerable( () => AsyncEnumerationFactory.CreateConcurrentStartInfo(
            () =>
            {
               var request = generator();
               return (request != null, request);
            },
            async ( request ) =>
            {
               var token = default( CancellationToken ); // this.CurrentCancellationToken; TODO get token from HTTPStatementInformation (since we are not pooled)

               var streamInstance = await this._streamPool.TakeResourceAsync( token );
               try
               {
                  var stream = streamInstance.Resource;

                  // Send request
                  var requestMethod = await stream.SendRequest(
                     request,
                     this._writeBuffers,
                     MIN_BUFFER_SIZE,
                     this._writerBufferLimit,
                     Encoding.ASCII, // this._encoding,
                     token
                     );

                  // Then, wait for response to arrive, but don't deserialize body, if the request has one
                  var response = await stream.ReceiveResponse(
                     requestMethod,
                     this._readBuffers,
                     MIN_BUFFER_SIZE,
                     this._readerBufferLimit,
                     Encoding.ASCII, // this._encoding,
                     list,
                     streamInstance,
                     this._streamPool,
                     token
                     );

                  return response;
               }
               catch
               {
                  streamInstance.Resource.DisposeSafely();
                  throw;
               }
            },
            () =>
            {
               var streamPool = this._streamPool;
               ExplicitResourceAcquireInfo<Stream>[] array;
               lock ( list )
               {
                  array = list.ToArray();
                  list.Clear();
               }

               foreach ( var stream in array )
               {
                  // If any stream is opened at this point, the content was not read till the end -> we are in inconsistent state (mid-transport of content).
                  // Just close the stream (TODO in future, see if we know the content size of the stream, and read it till the end instead of closing (if feasible))
                  // TODO for streams with unknown size, and which are left opened after all data sent, we need some mechanism to explicitly signal that the user of the HTTPResponseContent has reached the end of stream.
                  stream.Resource.DisposeSafely();
               }

               return TaskUtils.CompletedTask;
            } ) );
      }

      protected override HTTPStatementInformation GetInformationFromStatement( HTTPStatement statement )
      {
         return statement.Information;
      }

      protected override void ValidateStatementOrThrow( HTTPStatementInformation statement )
      {
         ArgumentValidator.ValidateNotNull( nameof( statement ), statement );
      }
   }

   internal sealed class HTTPConnectionVendorImpl : HTTPConnectionVendorFunctionality
   {
      internal static HTTPConnectionVendorImpl Instance { get; } = new HTTPConnectionVendorImpl();

      private HTTPConnectionVendorImpl()
      {

      }

      public HTTPStatement CreateStatementBuilder( TCreationParameter creationArgs )
      {
         var retVal = new HTTPStatementImpl();
         if ( creationArgs.IsFirst )
         {
            retVal.StaticMessage = creationArgs.First;
         }
         else if ( creationArgs.IsSecond )
         {
            retVal.MessageGenerator = creationArgs.Second;
         }

         return retVal;
      }
   }

   internal sealed class HTTPWriterImpl : HTTPWriter
   {
      private readonly ResizableArray<Byte> _buffer;
      private readonly Stream _stream;
      private readonly CancellationToken _token;

      public HTTPWriterImpl( ResizableArray<Byte> buffer, Stream stream, CancellationToken token )
      {
         this._buffer = ArgumentValidator.ValidateNotNull( nameof( buffer ), buffer );
         this._stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this._token = token;
      }

      public Byte[] Buffer => this._buffer.Array;

      public async ValueTask<Int64> FlushBufferContents( Int32 offset, Int32 count )
      {
         await this._stream.WriteAsync( this.Buffer, offset, count, this._token );
         return count - offset;
      }
   }

}

public static partial class E_HTTP
{
   private const Byte CR = 0x0D; // \r
   private const Byte LF = 0x0A; // \n
   private const Byte SPACE = 0x20;
   private const Byte COLON = 0x3A;

   private const String CRLF = "\r\n";
   private static readonly Byte[] CRLF_BYTES = new[] { (Byte) '\r', (Byte) '\n' };
   private const String SPACE_STR = " ";
   private const String COLON_STR = ":";

   // TODO get rid of this method - it is not exactly optimal.
   internal static ValueTask<Int64> WriteString( this HTTPWriter writer, ResizableArray<Byte> buffer, Encoding encoding, String str, Int32 bufferIndex = 0 )
   {
      ValueTask<Int64> retVal;
      if ( !String.IsNullOrEmpty( str ) )
      {
         //if ( !crlfAllowed && str.IndexOf( "\r\n" ) >= 0 )
         //{
         //   // TODO Extremely ineffective
         //   // But probably won't be entering here very often...
         //   str = str.Replace( "\r\n", "\\r\\n" );
         //}

         var strByteCount = encoding.GetByteCount( str );
         if ( buffer != null )
         {
            buffer.CurrentMaxCapacity = Math.Min( buffer.MaximumSize, strByteCount );
         }

         retVal = writer.WriteToStreamAsync( encoding, str, strByteCount, bufferIndex );
      }
      else
      {
         retVal = new ValueTask<Int64>( 0 );
      }

      return retVal;
   }

   internal static async ValueTask<String> SendRequest(
      this Stream stream,
      HTTPRequest request,
      LocklessInstancePoolForClasses<ResizableArray<Byte>> bufferPool,
      Int32 bufferInitialSize,
      Int32 bufferLimit,
      Encoding encoding,
      CancellationToken token
      )
   {
      // null as "separator" treats "str" as URI path and query
      // Empty string as "separator" prevents all escaping
      String EscapeHTTPComponentString( String str, String separator )
      {
         if ( !String.IsNullOrEmpty( str ) )
         {
            if ( separator == null && str != "*" && !Uri.IsWellFormedUriString( str, UriKind.RelativeOrAbsolute ) )
            {
               str = new Uri( "dummy://dummy:1" + ( str[0] == '/' ? "" : "/" ) + str ).PathAndQuery;
            }
            else if ( !String.IsNullOrEmpty( separator ) && str.IndexOf( separator ) >= 0 )
            {
               // TODO extremely ineffective, but hopefully we won't be going here very often
               str = str.Replace( separator, new String( separator.ToCharArray().SelectMany( s => new[] { '\\', s } ).ToArray() ) );
            }
         }
         return str;
      }

      var buffer = bufferPool.TakeInstance() ?? new ResizableArray<Byte>( initialSize: bufferInitialSize, maxLimit: bufferLimit );
      var retVal = 0L;
      String method = null;
      try
      {
         var writer = new HTTPWriterImpl( buffer, stream, token );
         // First line - method, path, version
         retVal += await writer.WriteString( buffer, encoding, method = EscapeHTTPComponentString( request.Method, SPACE_STR ), 0 );
         buffer.Array[0] = SPACE;
         var path = request.Path;
         if ( String.IsNullOrEmpty( path ) )
         {
            path = "/";
         }
         retVal += await writer.WriteString( buffer, encoding, EscapeHTTPComponentString( path, null ), 1 );
         retVal += await writer.WriteString( buffer, encoding, EscapeHTTPComponentString( request.Version, CRLF ), 1 );
         // CRLF will be sent as part of sending headers

         // Headers
         foreach ( var hdr in request.Headers )
         {
            foreach ( var hdrValue in hdr.Value )
            {
               buffer.Array[0] = CR;
               buffer.Array[1] = LF;
               retVal += await writer.WriteString( buffer, encoding, EscapeHTTPComponentString( hdr.Key.Trim(), CRLF ), 2 ); // Replace CRLF instead of COLON_STR, since first colon will be used anyways
               buffer.Array[0] = COLON;
               retVal += await writer.WriteString( buffer, encoding, EscapeHTTPComponentString( hdrValue, CRLF ), 1 );
            }
         }
         retVal += await writer.WriteString( buffer, encoding, CRLF + CRLF );
         await stream.FlushAsync( default );

         // Body
         var body = request.Content;
         if ( body != null )
         {
            var bodySize = body.ByteCount;
            if ( ( bodySize ?? -1 ) != 0 )
            {
               if ( bodySize.HasValue )
               {
                  buffer.CurrentMaxCapacity = (Int32) Math.Min( bodySize.Value, buffer.MaximumSize );
               }

               retVal += await body.WriteToStream( writer, bodySize );

               await stream.FlushAsync( default );
            }
         }
      }
      finally
      {
         // Clear any possibly sensitive data left over
         Array.Clear( buffer.Array, 0, buffer.Array.Length );
         bufferPool.ReturnInstance( buffer );
      }

      return method;
   }

   internal static async ValueTask<HTTPResponse> ReceiveResponse(
      this Stream stream,
      String requestMethod,
      LocklessInstancePoolForClasses<ResizableArray<Byte>> bufferPool,
      Int32 bufferInitialSize,
      Int32 bufferLimit,
      Encoding encoding,
      List<ExplicitResourceAcquireInfo<Stream>> allStreams,
      ExplicitResourceAcquireInfo<Stream> streamAcquireInfo,
      ExplicitAsyncResourcePool<Stream> streamPool,
      CancellationToken token
      )
   {
      String UnescapeHTTPComponentString( String str ) //, String separator )
      {
         //if ( !String.IsNullOrEmpty( str ) )
         //{
         //   if ( separator == null && str != "*" )
         //   {
         //      //str = new Uri( str, UriKind.RelativeOrAbsolute ).PathAndQuery;
         //   }
         //   else if ( !String.IsNullOrEmpty( separator ) &&  )
         //      str = new Uri( "dummy://dummy:1" + ( str[0] == '/' ? "" : "/" ) + str ).PathAndQuery;
         //}
         return str;
      }

      //var buffer = bufferPool.TakeInstance() ?? new ResizableArray<Byte>( initialSize: bufferInitialSize, maxLimit: bufferLimit );
      try
      {
         var buffer = new ResizableArray<Byte>();
         var streamReadCount = 0x1000;
         // Read first line
         (var bufferOffset, var bufferTotal) = await stream.ReadUntilMaybeAsync2( buffer, 0, 0, CRLF_BYTES, streamReadCount );
         var array = buffer.Array;
         var idx = Array.IndexOf( array, SPACE );
         var version = UnescapeHTTPComponentString( encoding.GetString( array, 0, idx ) );

         var start = idx + 1;
         idx = Array.IndexOf( array, SPACE, start );
         var statusCode = UnescapeHTTPComponentString( encoding.GetString( array, start, idx - start ) );
         Int32.TryParse( statusCode, out var statusCodeInt );

         // The rest is message
         var statusMessage = UnescapeHTTPComponentString( encoding.GetString( array, idx + 1, bufferOffset - idx - 1 ) );
         // Read headers - one line at a time
         // TODO max header count limit (how many fieldname:fieldvalue lines)
         var headers = HTTPMessageFactory.CreateHeadersDictionary();
         bufferTotal = HTTPResponseContentFromStream_Chunked.EraseReadData( array, bufferOffset, bufferTotal );
         do
         {
            (bufferOffset, bufferTotal) = await stream.ReadUntilMaybeAsync( buffer, 0, bufferTotal, CRLF_BYTES, streamReadCount );
            if ( bufferOffset > 0 )
            {
               array = buffer.Array;
               idx = Array.IndexOf( array, COLON );
               if ( idx > 0 )
               {
                  start = 0;
                  // In this block, "idx" = count
                  TrimBeginAndEnd( array, ref start, ref idx, false );
                  if ( start < idx )
                  {
                     var headerName = UnescapeHTTPComponentString( encoding.GetString( array, start, idx ) );
                     start = idx + 1;
                     idx = bufferOffset - start;
                     TrimBeginAndEnd( array, ref start, ref idx, true );
                     String headerValue;
                     if ( idx > 0 )
                     {
                        headerValue = UnescapeHTTPComponentString( encoding.GetString( array, start, idx ) );
                     }
                     else
                     {
                        headerValue = String.Empty;
                     }
                     headers
                        .GetOrAdd_NotThreadSafe( headerName, hn => new List<String>( 1 ) )
                        .Add( headerValue );
                  }
               }
            }

            bufferTotal = HTTPResponseContentFromStream_Chunked.EraseReadData( array, bufferOffset, bufferTotal );
         } while ( bufferOffset > 0 );

         // Now we can set the content, if it is present
         // https://tools.ietf.org/html/rfc7230#section-3.3
         var hasContent = CanHaveMessageContent( requestMethod, statusCodeInt );
         HTTPResponseContent responseContent;
         if ( hasContent )
         {
            var hasXferEncoding = headers.TryGetValue( "Transfer-Encoding", out var xferEncodingStrings );
            var contentLengthStrings = xferEncodingStrings;
            Int64? contentLength;
            if (
               ( hasXferEncoding || headers.TryGetValue( "Content-Length", out contentLengthStrings ) )
               && contentLengthStrings.Count > 0
               && Int64.TryParse( contentLengthStrings[0], out var contentLengthInt )
               )
            {
               contentLength = contentLengthInt;
            }
            else
            {
               contentLength = null;
            }

            hasContent = contentLength.HasValue && contentLength.Value > 0;

            ValueTask<Boolean> OnEnd()
            {
               ValueTask<Boolean> awaitable = default;
               lock ( allStreams )
               {
                  var streamIdx = allStreams.IndexOf( streamAcquireInfo );
                  if ( streamIdx >= 0 )
                  {
                     awaitable = streamPool.ReturnResource( allStreams[streamIdx] );
                     allStreams.RemoveAt( streamIdx );
                  }
               }

               return awaitable;
            }


            if ( contentLength.HasValue
               || ( xferEncodingStrings?.All( xferEncoding => !String.Equals( xferEncoding, "chunked", StringComparison.OrdinalIgnoreCase ) ) ?? true )
               )
            {
               if ( contentLength.HasValue && contentLength.Value == 0 )
               {
                  responseContent = EmptyHTTPResponseContent.Instance;
               }
               else
               {
                  lock ( allStreams )
                  {
                     allStreams.Add( streamAcquireInfo );
                  }

                  responseContent = new HTTPResponseContentFromStream( stream, buffer.Array, 0, bufferTotal, contentLength, OnEnd );
               }
            }
            else
            {
               // Chunked encoding
               hasContent = true;
               lock ( allStreams )
               {
                  allStreams.Add( streamAcquireInfo );
               }

               responseContent = await CreateChunkedContentAsync( stream, buffer, 0, bufferTotal, streamReadCount, OnEnd );
            }
         }
         else
         {
            responseContent = EmptyHTTPResponseContent.Instance;
         }

         if ( !hasContent )
         {
            // No content, or empty content -> return stream right away
            await streamPool.ReturnResource( streamAcquireInfo );
         }

         return HTTPMessageFactory.CreateResponse( version, statusCodeInt, statusMessage, headers, responseContent );
      }
      catch
      {
         stream.DisposeSafely();
         throw;
      }
      finally
      {
         //Array.Clear( buffer.Array, 0, buffer.Array.Length );
         //bufferPool.ReturnInstance( buffer );
      }
   }

   private static async ValueTask<HTTPResponseContentFromStream_Chunked> CreateChunkedContentAsync(
      Stream stream,
      ResizableArray<Byte> buffer,
      Int32 bufferOffset,
      Int32 bufferTotal,
      Int32 streamReadCount,
      Func<ValueTask<Boolean>> onEnd
      )
   {
      Int32 chunkLength;
      (chunkLength, bufferOffset, bufferTotal) = await HTTPResponseContentFromStream_Chunked.ReadChunkHeader( stream, buffer, bufferOffset, bufferTotal, streamReadCount, true );
      if ( chunkLength == 0 )
      {
         await HTTPResponseContentFromStream.CallOnEnd( onEnd );
         onEnd = null;
      }

      return new HTTPResponseContentFromStream_Chunked( stream, buffer, chunkLength, bufferOffset, bufferTotal, streamReadCount, onEnd );
   }

   private static Boolean CanHaveMessageContent( String requestMethod, Int32 statusCode )
   {
      // Request: HEAD method never has content
      // Response: 1XX, 204, and 304 never have content
      return !( String.Equals( requestMethod, "HEAD" ) || ( statusCode >= 100 && statusCode < 200 ) || statusCode == 204 || statusCode == 304 );
   }

   //private static async ValueTask<Int32> ReadUntilCRLF( this BufferedStream stream, ResizableArray<Byte> buffer, CancellationToken token )
   //{
   //   const Int32 INITIAL = 0;
   //   const Int32 CRLF_SEEN = 1;
   //   const Int32 END_SEEN = 2;
   //   var cur = 0;
   //   var endEncountered = INITIAL;
   //   do
   //   {
   //      buffer.CurrentMaxCapacity = cur + 1;
   //      // The implementation of BufferedStream is optimized to return cached task when reading from buffer using same count
   //      if ( await stream.ReadAsync( buffer.Array, cur, 1, token ) == 1 )
   //      {
   //         ++cur;
   //         if ( buffer.Array[cur - 1] == CR )
   //         {
   //            buffer.CurrentMaxCapacity = cur + 1;
   //            if ( await stream.ReadAsync( buffer.Array, cur, 1, token ) == 1 )
   //            {
   //               ++cur;
   //               if ( buffer.Array[cur - 1] == LF )
   //               {
   //                  endEncountered = CRLF_SEEN;
   //               }
   //            }
   //            else
   //            {
   //               endEncountered = END_SEEN;
   //            }
   //         }
   //      }
   //      else
   //      {
   //         endEncountered = END_SEEN;
   //      }
   //   } while ( endEncountered == INITIAL );

   //   if ( endEncountered == END_SEEN )
   //   {
   //      throw new EndOfStreamException();
   //   }

   //   return cur;
   //}

   private static void TrimBeginAndEnd( Byte[] array, ref Int32 start, ref Int32 count, Boolean trimEnd )
   {
      // Trim begin
      while ( count > 0 && Char.IsWhiteSpace( (Char) array[start] ) )
      {
         ++start;
         --count;
      }
      if ( trimEnd )
      {
         // Trim end
         while ( count > 0 && Char.IsWhiteSpace( (Char) array[start + count - 1] ) )
         {
            --count;
         }
      }
   }
}

internal static partial class E_TEMP
{
   public static async ValueTask<(Int32 Offset, Int32 TotalReadBytes)> ReadUntilMaybeAsync2(
   this Stream stream,
   ResizableArray<Byte> buffer,
   Int32 alreadyRead,
   Int32 totalExisting,
   Byte[] endMark,
   Int32 streamReadCount
   )
   {
      ArgumentValidator.ValidateNotNullReference( stream );
      // Scan to see if we have endMark already
      var end = ArgumentValidator.ValidateNotNull( nameof( buffer ), buffer ).Array.IndexOfArray( alreadyRead, totalExisting - alreadyRead, endMark );
      if ( end == -1 )
      {
         //beforeAsyncRead?.Invoke();
         (end, totalExisting) = await stream.ReadUntilAsync( buffer, totalExisting, endMark, streamReadCount );
      }
      return (end, totalExisting);
   }

   private static async Task<(Int32 Offset, Int32 TotalReadBytes)> ReadUntilAsync( this Stream stream, ResizableArray<Byte> buffer, Int32 offset, Byte[] endMark, Int32 streamReadCount )
   {
      Int32 originalBufferOffset, bytesRead, retVal;

      do
      {
         bytesRead = await stream.ReadAsync( buffer.SetCapacityAndReturnArray( offset + streamReadCount ), offset, streamReadCount, default );
         if ( bytesRead <= 0 )
         {
            throw new EndOfStreamException();
         }
         originalBufferOffset = Math.Max( 0, offset + 1 - endMark.Length );
         offset += bytesRead;
      } while ( ( retVal = buffer.Array.IndexOfArray( originalBufferOffset, bytesRead, endMark, EqualityComparer<Byte>.Default ) ) < 0 );

      return (retVal, offset);
   }
}