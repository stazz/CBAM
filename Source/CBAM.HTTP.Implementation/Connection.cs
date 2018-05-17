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
using CBAM.HTTP;
using System.Text;
using CBAM.HTTP.Implementation;
using System.Collections.Generic;

namespace CBAM.HTTP.Implementation
{
   internal sealed class HTTPConnectionImpl<TRequestMetaData> : ConnectionImpl<HTTPStatement<TRequestMetaData>, HTTPStatementInformation<TRequestMetaData>, HTTPRequestInfo<TRequestMetaData>, HTTPResponseInfo<TRequestMetaData>, HTTPConnectionVendorFunctionality<TRequestMetaData>, IAsyncEnumerable<HTTPResponseInfo<TRequestMetaData>>, IAsyncEnumerableObservable<HTTPResponseInfo<TRequestMetaData>, HTTPStatementInformation<TRequestMetaData>>, HTTPConnectionVendorImpl<TRequestMetaData>, HTTPConnectionFunctionalityImpl<TRequestMetaData>>, HTTPConnection<TRequestMetaData>
   {
      public HTTPConnectionImpl(
         HTTPConnectionFunctionalityImpl<TRequestMetaData> functionality
         ) : base( functionality )
      {
      }

      protected override IAsyncEnumerableObservable<HTTPResponseInfo<TRequestMetaData>, HTTPStatementInformation<TRequestMetaData>> CreateObservable(
         IAsyncEnumerable<HTTPResponseInfo<TRequestMetaData>> enumerable,
         HTTPStatementInformation<TRequestMetaData> info
         )
      {
         return enumerable.AsObservable( info );
      }

      public String ProtocolVersion => HTTPMessageFactory.HTTP1_1;
   }

   internal sealed class HTTPConnectionFunctionalityImpl<TRequestMetaData> : ConnectionFunctionalitySU<HTTPStatement<TRequestMetaData>, HTTPStatementInformation<TRequestMetaData>, HTTPRequestInfo<TRequestMetaData>, HTTPResponseInfo<TRequestMetaData>, HTTPConnectionVendorImpl<TRequestMetaData>> // DefaultConnectionFunctionality<HTTPStatement, HTTPStatementInformation, TCreationParameter, HTTPConnectionVendorImpl, IAsyncConcurrentEnumerable<HTTPResponse>>
   {
      private readonly ClientProtocolIOState _state;

      public HTTPConnectionFunctionalityImpl(
         HTTPConnectionVendorImpl<TRequestMetaData> vendor,
         ClientProtocolIOState state
         ) : base( vendor )
      {
         this._state = ArgumentValidator.ValidateNotNull( nameof( state ), state );
      }


      public Stream Stream => this._state.Stream;

      protected override ReservedForStatement CreateReservationObject( HTTPStatementInformation<TRequestMetaData> stmt )
      {
         return new ReservedForStatement(
#if DEBUG
            stmt
#endif
            );
      }

      protected override HTTPStatementInformation<TRequestMetaData> GetInformationFromStatement( HTTPStatement<TRequestMetaData> statement )
      {
         return statement?.Information;
      }

      protected override Task PerformDisposeStatementAsync( ReservedForStatement reservationObject )
      {
         // Nothing to do as HTTP is stateless protocol
         return TaskUtils.CompletedTask;
      }

      protected override void ValidateStatementOrThrow( HTTPStatementInformation<TRequestMetaData> statement )
      {
         ArgumentValidator.ValidateNotNull( nameof( statement ), statement );
      }

      protected override async ValueTask<(HTTPResponseInfo<TRequestMetaData>, Boolean, Func<ValueTask<(Boolean, HTTPResponseInfo<TRequestMetaData>)>>)> ExecuteStatement(
         HTTPStatementInformation<TRequestMetaData> stmt,
         ReservedForStatement reservationObject
         )
      {
         var stmtImpl = (HTTPStatementInformationImpl<TRequestMetaData>) stmt;
         var generator = stmtImpl.NextRequestGenerator;
         var currentMD = stmtImpl.InitialRequestMetaData;
         HTTPResponse currentResponse = default;
         async ValueTask<(Boolean, HTTPResponseInfo<TRequestMetaData>)> ReadNextResponse()
         {
            var requestInfo = await ( generator?.Invoke( new HTTPResponseInfo<TRequestMetaData>( currentResponse, currentMD ) ) ?? default );
            currentMD = requestInfo.RequestMetaData;
            // Call this always, as it will take care of reading the previous response content till the end.
            currentResponse = await this.SendAndReceive( currentResponse, requestInfo.Request );
            return (currentResponse != default, currentResponse == default ? default : new HTTPResponseInfo<TRequestMetaData>( currentResponse, currentMD ));
         }

         // Send request
         return (
            new HTTPResponseInfo<TRequestMetaData>( currentResponse = await this.SendAndReceive( default, stmtImpl.InitialRequest ), currentMD ),
            true,
            ReadNextResponse
            );
      }

      private async Task<HTTPResponse> SendAndReceive(
         HTTPResponse prevResponse,
         HTTPRequest request
         )
      {
         var state = this._state;
         var buffer = state.ReadState.Buffer;

         HTTPResponseContent prevResponseContent;
         if ( ( prevResponseContent = prevResponse?.Content ) != null )
         {
            while ( ( await prevResponseContent.ReadToBuffer( buffer.Array, 0, buffer.CurrentMaxCapacity ) ) > 0 ) ;
         }

         HTTPResponse retVal;
         if ( request != null )
         {
            var requestMethod = await this._state.SendRequest(
               request,
               this.CurrentCancellationToken
               );
            retVal = await this._state.ReceiveResponse(
               requestMethod,
               this.CurrentCancellationToken
               );
         }
         else
         {
            retVal = null;
         }

         return retVal;
      }



      //private const Int32 MIN_BUFFER_SIZE = 1024;

      //private readonly ExplicitAsyncResourcePool<Stream> _streamPool;

      //// TODO Use AsyncResourcePool<ResizableArray<Byte>, TimeSpan> except that it should be Sync
      //private readonly LocklessInstancePoolForClasses<ResizableArray<Byte>> _writeBuffers;
      //private readonly LocklessInstancePoolForClasses<ResizableArray<Byte>> _readBuffers;
      //private readonly Int32 _writerBufferLimit;
      //private readonly Int32 _readerBufferLimit;
      ////private readonly Encoding _encoding;

      //public HTTPConnectionFunctionalityImpl(
      //   HTTPConnectionVendorImpl vendorFunctionality,
      //   ExplicitAsyncResourcePool<Stream> streamPool,
      //   Int32? readerBufferLimit,
      //   Int32? writerBufferLimit
      //   //Encoding encoding
      //   ) : base( vendorFunctionality )
      //{
      //   this._streamPool = ArgumentValidator.ValidateNotNull( nameof( streamPool ), streamPool );
      //   this._writeBuffers = new DefaultLocklessInstancePoolForClasses<ResizableArray<Byte>>();
      //   this._readBuffers = new DefaultLocklessInstancePoolForClasses<ResizableArray<Byte>>();
      //   this._readerBufferLimit = readerBufferLimit.HasValue && readerBufferLimit.Value > 0 ? Math.Max( MIN_BUFFER_SIZE, readerBufferLimit.Value ) : -1;
      //   this._writerBufferLimit = writerBufferLimit.HasValue && writerBufferLimit.Value > 0 ? Math.Max( MIN_BUFFER_SIZE, writerBufferLimit.Value ) : -1;
      //   //this._encoding = encoding;
      //}

      //protected override IAsyncConcurrentEnumerable<HTTPResponse> CreateEnumerable(
      //   HTTPStatementInformation metadata
      //   )
      //{
      //   var list = new List<ExplicitResourceAcquireInfo<Stream>>();
      //   var generator = ( (HTTPStatementInformationImpl) metadata ).MessageGenerator;
      //   return AsyncEnumerationFactory.CreateConcurrentEnumerable( () => AsyncEnumerationFactory.CreateConcurrentStartInfo(
      //      () =>
      //      {
      //         var request = generator();
      //         return (request != null, request);
      //      },
      //      async ( request ) =>
      //      {
      //         var token = default( CancellationToken ); // this.CurrentCancellationToken; TODO get token from HTTPStatementInformation (since we are not pooled)

      //         var streamInstance = await this._streamPool.TakeResourceAsync( token );
      //         try
      //         {
      //            var stream = streamInstance.Resource;

      //            // Send request
      //            var requestMethod = await stream.SendRequest(
      //               request,
      //               this._writeBuffers,
      //               MIN_BUFFER_SIZE,
      //               this._writerBufferLimit,
      //               Encoding.ASCII, // this._encoding,
      //               token
      //               );

      //            // Then, wait for response to arrive, but don't deserialize body, if the request has one
      //            var response = await stream.ReceiveResponse(
      //               requestMethod,
      //               this._readBuffers,
      //               MIN_BUFFER_SIZE,
      //               this._readerBufferLimit,
      //               Encoding.ASCII, // this._encoding,
      //               list,
      //               streamInstance,
      //               this._streamPool,
      //               token
      //               );

      //            return response;
      //         }
      //         catch
      //         {
      //            streamInstance.Resource.DisposeSafely();
      //            throw;
      //         }
      //      },
      //      () =>
      //      {
      //         var streamPool = this._streamPool;
      //         ExplicitResourceAcquireInfo<Stream>[] array;
      //         lock ( list )
      //         {
      //            array = list.ToArray();
      //            list.Clear();
      //         }

      //         foreach ( var stream in array )
      //         {
      //            // If any stream is opened at this point, the content was not read till the end -> we are in inconsistent state (mid-transport of content).
      //            // Just close the stream (TODO in future, see if we know the content size of the stream, and read it till the end instead of closing (if feasible))
      //            // TODO for streams with unknown size, and which are left opened after all data sent, we need some mechanism to explicitly signal that the user of the HTTPResponseContent has reached the end of stream.
      //            stream.Resource.DisposeSafely();
      //         }

      //         return TaskUtils.CompletedTask;
      //      } ) );
      //}

      //protected override HTTPStatementInformation GetInformationFromStatement( HTTPStatement statement )
      //{
      //   return statement.Information;
      //}

      //protected override void ValidateStatementOrThrow( HTTPStatementInformation statement )
      //{
      //   ArgumentValidator.ValidateNotNull( nameof( statement ), statement );
      //}

   }

   public abstract class AbstractIOState
   {

      public AbstractIOState()
      {
         //this.Lock = new AsyncLock();
         this.Buffer = new ResizableArray<Byte>( 0x100 );
      }

      public ResizableArray<Byte> Buffer { get; }

      //public AsyncLock Lock { get; }
   }

   public sealed class WriteState : AbstractIOState
   {
      public WriteState(
         ) : base()
      {
      }
   }

   public sealed class ReadState : AbstractIOState
   {
      public ReadState(
         ) : base()
      {
         //this.PreReadLength = 0;
         //this.MessageSpaceIndices = new Int32[3];
         this.BufferAdvanceState = new BufferAdvanceState();
      }

      public BufferAdvanceState BufferAdvanceState { get; }

      //public Int32 BufferOffset { get; set; }

      //public Int32 PreReadLength { get; set; }

      //public Int32[] MessageSpaceIndices { get; }
   }

   public sealed class ClientProtocolIOState
   {

      public ClientProtocolIOState(
         Stream stream,
         BinaryStringPool stringPool,
         IEncodingInfo encoding,
         WriteState writeState,
         ReadState readState
         )
      {
         this.Stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this.StringPool = stringPool ?? BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool( encoding.Encoding );
         this.Encoding = ArgumentValidator.ValidateNotNull( nameof( encoding ), encoding );
         this.WriteState = writeState ?? new WriteState();
         this.ReadState = readState ?? new ReadState();
      }

      public WriteState WriteState { get; }

      public ReadState ReadState { get; }

      public Stream Stream { get; }

      public BinaryStringPool StringPool { get; }

      public IEncodingInfo Encoding { get; }
   }

   internal sealed class HTTPConnectionVendorImpl<TRequestMetaData> : HTTPConnectionVendorFunctionality<TRequestMetaData>
   {
      internal static HTTPConnectionVendorImpl<TRequestMetaData> Instance { get; } = new HTTPConnectionVendorImpl<TRequestMetaData>();

      private HTTPConnectionVendorImpl()
      {

      }

      public HTTPStatement<TRequestMetaData> CreateStatementBuilder( HTTPRequestInfo<TRequestMetaData> creationArgs )
      {
         return new HTTPStatementImpl<TRequestMetaData>( creationArgs );
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

   internal static Task WriteHTTPString( this Stream stream, ResizableArray<Byte> buffer, Encoding encoding, String str, CancellationToken token, Int32 bufferIndex = 0 )
   {
      Task retVal;
      if ( !String.IsNullOrEmpty( str ) )
      {
         var strByteCount = encoding.GetByteCount( str );
         var count = bufferIndex + strByteCount;
         var array = buffer.SetCapacityAndReturnArray( count );
         encoding.GetBytes( str, 0, str.Length, array, bufferIndex );
         retVal = stream.WriteAsync( array, 0, count, token );
      }
      else
      {
         retVal = TaskUtils.CompletedTask;
      }

      return retVal;
   }

   internal static async Task<String> SendRequest(
      this ClientProtocolIOState state,
      HTTPRequest request,
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

      String method = null;

      var stream = state.Stream;
      var wState = state.WriteState;
      var buffer = wState.Buffer;
      var encoding = state.Encoding.Encoding;

      // First line - method, path, version
      await stream.WriteHTTPString( buffer, encoding, method = EscapeHTTPComponentString( request.Method, SPACE_STR ), token );
      buffer.Array[0] = SPACE;
      var path = request.Path;
      if ( String.IsNullOrEmpty( path ) )
      {
         path = "/";
      }
      await stream.WriteHTTPString( buffer, encoding, EscapeHTTPComponentString( path, null ), token, 1 );
      await stream.WriteHTTPString( buffer, encoding, EscapeHTTPComponentString( request.Version, CRLF ), token, 1 );
      // CRLF will be sent as part of sending headers

      // Headers
      foreach ( var hdr in request.Headers )
      {
         foreach ( var hdrValue in hdr.Value )
         {
            buffer.Array[0] = CR;
            buffer.Array[1] = LF;
            await stream.WriteHTTPString( buffer, encoding, EscapeHTTPComponentString( hdr.Key.Trim(), CRLF ), token, 2 ); // Replace CRLF instead of COLON_STR, since first colon will be used anyways
            buffer.Array[0] = COLON;
            await stream.WriteHTTPString( buffer, encoding, EscapeHTTPComponentString( hdrValue, CRLF ), token, 1 );
         }
      }

      await stream.WriteHTTPString( buffer, encoding, CRLF + CRLF, token );
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

            await body.WriteToStream( new HTTPWriterImpl( buffer, stream, token ), bodySize );

            await stream.FlushAsync( default );
         }
      }

      return method;
   }

   internal static async ValueTask<HTTPResponse> ReceiveResponse(
      this ClientProtocolIOState state,
      String requestMethod,
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

      var streamReadCount = 0x1000;
      var stream = state.Stream;
      var rState = state.ReadState;
      var buffer = rState.Buffer;
      var aState = rState.BufferAdvanceState;
      var strings = state.StringPool;

      // Read first line
      if ( aState.BufferTotal > 0 )
      {
         HTTPResponseContentFromStream_Chunked.EraseReadData( aState, buffer );
      }
      await stream.ReadUntilMaybeAsync( buffer, aState, CRLF_BYTES, streamReadCount );
      var array = buffer.Array;
      var idx = Array.IndexOf( array, SPACE );
      var version = UnescapeHTTPComponentString( strings.GetString( array, 0, idx ) );

      var start = idx + 1;
      idx = Array.IndexOf( array, SPACE, start );
      var statusCode = UnescapeHTTPComponentString( strings.GetString( array, start, idx - start ) );
      Int32.TryParse( statusCode, out var statusCodeInt );

      // The rest is message
      var statusMessage = UnescapeHTTPComponentString( strings.GetString( array, idx + 1, aState.BufferOffset - idx - 1 ) );
      // Read headers - one line at a time
      // TODO max header count limit (how many fieldname:fieldvalue lines)
      var headers = HTTPMessageFactory.CreateHeadersDictionary();
      HTTPResponseContentFromStream_Chunked.EraseReadData( aState, buffer );
      Int32 bufferOffset;
      do
      {
         await stream.ReadUntilMaybeAsync( buffer, aState, CRLF_BYTES, streamReadCount );
         bufferOffset = aState.BufferOffset;
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
                  var headerName = UnescapeHTTPComponentString( strings.GetString( array, start, idx ) );
                  start = idx + 1;
                  idx = bufferOffset - start;
                  TrimBeginAndEnd( array, ref start, ref idx, true );
                  String headerValue;
                  if ( idx > 0 )
                  {
                     headerValue = UnescapeHTTPComponentString( strings.GetString( array, start, idx ) );
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

         HTTPResponseContentFromStream_Chunked.EraseReadData( aState, buffer );
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
               responseContent = new HTTPResponseContentFromStream( stream, buffer.Array, aState, contentLength, token );
            }
         }
         else
         {
            // Chunked encoding
            hasContent = true;
            responseContent = new HTTPResponseContentFromStream_Chunked(
               stream,
               buffer,
               aState,
               await HTTPResponseContentFromStream_Chunked.ReadNextChunk( stream, buffer, aState, streamReadCount, token ),
               streamReadCount,
               token
               );
         }
      }
      else
      {
         responseContent = EmptyHTTPResponseContent.Instance;
      }

      return HTTPMessageFactory.CreateResponse( version, statusCodeInt, statusMessage, headers, responseContent );
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

