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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.HTTP
{
   internal abstract class HTTPMessageImpl<TContent> : HTTPMessage<TContent>
      where TContent : HTTPMessageContent
   {
      protected HTTPMessageImpl(
         IDictionary<String, List<String>> headers
         )
      {
         this.Headers = headers ?? HTTPMessageFactory.CreateHeadersDictionary();
      }

      public IDictionary<String, List<String>> Headers { get; }

      public abstract String Version { get; }
      public abstract TContent Content { get; }
   }

   internal sealed class HTTPRequestImpl : HTTPMessageImpl<HTTPRequestContent>, HTTPRequest
   {
      public HTTPRequestImpl()
         : base( null )
      {

      }
      public String Method { get; set; }
      public String Path { get; set; }

      public override String Version => ( (HTTPRequest) this ).Version;

      public override HTTPRequestContent Content => ( (HTTPRequest) this ).Content;

      String HTTPRequest.Version { get; set; }
      HTTPRequestContent HTTPRequest.Content { get; set; }
   }

   internal sealed class HTTPResponseImpl : HTTPMessageImpl<HTTPResponseContent>, HTTPResponse
   {
      public HTTPResponseImpl(
         Int32 statusCode,
         String statusCodeMessage,
         String version,
         IDictionary<String, List<String>> headers,
         HTTPResponseContent content
         ) : base( headers )
      {
         this.StatusCode = statusCode;
         this.StatusCodeMessage = statusCodeMessage;
         this.Version = ArgumentValidator.ValidateNotEmpty( nameof( version ), version );
         this.Content = ArgumentValidator.ValidateNotNull( nameof( content ), content );
      }

      public Int32 StatusCode { get; }
      public String StatusCodeMessage { get; }

      public override String Version { get; }

      public override HTTPResponseContent Content { get; }
   }

   internal sealed class HTTPRequestContentFromString : HTTPRequestContent
   {
      public HTTPRequestContentFromString(
         String str,
         Encoding encoding
         )
      {
         this.StringContent = str ?? String.Empty;
         this.Encoding = encoding ?? System.Text.Encoding.UTF8;
      }

      public Int64? ByteCount => this.Encoding.GetByteCount( this.StringContent );

      public ValueTask<Int64> WriteToStream( HTTPWriter writer, Int64? seenByteCount )
      {
         return writer.WriteToStreamAsync( this.Encoding, this.StringContent, seenByteCount.HasValue ? (Int32?) seenByteCount.Value : null );
      }

      public Encoding Encoding { get; set; }

      public String StringContent { get; }

      public Boolean ContentEndIsKnown => true;
   }

   public sealed class HTTPResponseContentFromStream : HTTPResponseContent
   {
      private const Int32 INITIAL = 0;
      private const Int32 READING = 1;

      private readonly Stream _stream;
      private readonly Byte[] _preReadData;
      private Int32 _bufferOffset;
      private Int32 _bufferTotal;
      private Int64 _bytesRemaining;
      private Int32 _state;
      private readonly Func<ValueTask<Boolean>> _onEnd;
      private Int32 _onEndCalled;


      public HTTPResponseContentFromStream(
         Stream stream,
         Byte[] buffer,
         Int32 bufferOffset,
         Int32 bufferTotal,
         Int64? byteCount,
         Func<ValueTask<Boolean>> onEnd
         )
      {
         this._stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this._preReadData = ArgumentValidator.ValidateNotNull( nameof( buffer ), buffer );
         this._bufferOffset = bufferOffset;
         this._bufferTotal = bufferTotal;
         this.ByteCount = byteCount;
         this._bytesRemaining = byteCount ?? -1;
         this._onEnd = onEnd;
      }

      public Boolean ContentEndIsKnown => this.ByteCount.HasValue;

      public Int64? ByteCount { get; }

      public Int64? BytesRemaining => this._bytesRemaining < 0 ? default : Interlocked.Read( ref this._bytesRemaining );

      public async ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count )
      {
         array.CheckArrayArguments( offset, count, true );
         if ( Interlocked.CompareExchange( ref this._state, READING, INITIAL ) == INITIAL )
         {
            // TODO support for multi-part form stuff
            try
            {
               var remaining = this._bytesRemaining;

               Int32 bytesRead;

               if ( remaining == 0 )
               {
                  bytesRead = 0;
               }
               else
               {
                  if ( remaining > 0 )
                  {
                     count = (Int32) Math.Min( count, remaining );
                  }

                  var bufferRemaining = this._bufferTotal - this._bufferOffset;
                  bytesRead = 0;
                  if ( bufferRemaining > 0 )
                  {
                     // We have some data in buffer
                     var bufferReadCount = Math.Min( bufferRemaining, count );
                     this._preReadData.CopyTo( array, ref this._bufferOffset, offset, bufferReadCount );
                     if ( remaining > 0 )
                     {
                        this._bytesRemaining -= bufferReadCount;
                     }
                     this._bufferTotal -= bufferReadCount;
                     count -= bufferReadCount;
                     offset += bufferReadCount;
                     bytesRead = bufferReadCount;
                  }

                  if ( count > 0 )
                  {
                     var streamRead = await this._stream.ReadAsync( array, offset, count, default );
                     bytesRead += streamRead;
                     if ( remaining > 0 )
                     {
                        this._bytesRemaining -= streamRead;
                     }
                  }
               }


               // No need to use CEX since we are inside CEX-mutex
               if ( ( bytesRead <= 0 || this._bytesRemaining == 0 ) && this._onEndCalled == 0 )
               {
                  Interlocked.Exchange( ref this._onEndCalled, 1 );
                  await CallOnEnd( this._onEnd );
               }

               return bytesRead;
            }
            finally
            {
               Interlocked.Exchange( ref this._state, INITIAL );
            }
         }
         else
         {
            throw new InvalidOperationException( "Concurrent access" );
         }
      }

      public static async Task CallOnEnd( Func<ValueTask<Boolean>> onEnd )
      {
         try
         {
            await ( onEnd?.Invoke() ?? default );
         }
         catch
         {
            // Ignore
         }
      }
   }

   public sealed class HTTPResponseContentFromStream_Chunked : HTTPResponseContent
   {
      private static readonly Byte[] CRLF = new[] { (Byte) '\r', (Byte) '\n' };
      private static readonly Byte[] TerminatingChunk = new[] { (Byte) '0', (Byte) '\r', (Byte) '\n' }; //, (Byte) '\r', (Byte) '\n' };

      private const Int32 INITIAL = 0;
      private const Int32 READING = 1;

      private readonly Stream _stream;
      private readonly ResizableArray<Byte> _headerBuffer;
      private readonly Int32 _streamReadCount;
      private readonly Func<ValueTask<Boolean>> _onEnd;

      private Int32 _state;
      private Int32 _chunkRemaining;
      private Int32 _bufferOffset;
      private Int32 _bufferTotal;

      public HTTPResponseContentFromStream_Chunked(
         Stream stream,
         ResizableArray<Byte> buffer,
         Int32 firstChunkLength,
         Int32 bufferOffset,
         Int32 bufferTotal,
         Int32 streamReadCount,
         Func<ValueTask<Boolean>> onEnd
         )
      {
         this._state = INITIAL;
         this._stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this._headerBuffer = ArgumentValidator.ValidateNotNull( nameof( buffer ), buffer );
         this._chunkRemaining = firstChunkLength == 0 ? -1 : firstChunkLength;
         this._onEnd = onEnd;
         this._bufferOffset = bufferOffset;
         this._bufferTotal = bufferTotal;
         this._streamReadCount = streamReadCount;
      }


      public Int64? BytesRemaining => Math.Max( this._chunkRemaining, 0 );

      public Int64? ByteCount => null;

      public Boolean ContentEndIsKnown => true;

      public async ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count )
      {
         array.CheckArrayArguments( offset, count, true );
         Int32 retVal;
         if ( Interlocked.CompareExchange( ref this._state, READING, INITIAL ) == INITIAL )
         {
            try
            {
               if ( this._chunkRemaining < 0 )
               {
                  // Ended
                  retVal = 0;
               }
               else
               {
                  // Only read maximally the remaining amount of current chunk
                  retVal = Math.Min( this._chunkRemaining, count );
                  var bufferRemaining = this._bufferTotal - this._bufferOffset;
                  if ( bufferRemaining > 0 )
                  {
                     // We have some data in buffer
                     var bufferReadCount = Math.Min( bufferRemaining, count );
                     this._headerBuffer.Array.CopyTo( array, ref this._bufferOffset, offset, bufferReadCount );
                     offset += bufferReadCount;
                     count -= bufferReadCount;
                  }

                  if ( count > 0 )
                  {
                     await this._stream.ReadSpecificAmountAsync( array, offset, count, default );
                  }
                  this._chunkRemaining -= retVal;
                  if ( this._chunkRemaining <= 0 )
                  {
                     // Read next chunk header
                     (this._chunkRemaining, this._bufferOffset, this._bufferTotal) = await ReadChunkHeader(
                        this._stream,
                        this._headerBuffer,
                        0,
                        EraseReadData( this._headerBuffer.Array, this._bufferOffset - 2, this._bufferTotal ), // 
                        this._streamReadCount,
                        false
                        );
                     if ( this._chunkRemaining == 0 )
                     {
                        this._chunkRemaining = -1;
                        await HTTPResponseContentFromStream.CallOnEnd( this._onEnd );
                     }
                  }
               }
            }
            finally
            {
               Interlocked.Exchange( ref this._state, INITIAL );
            }
         }
         else
         {
            throw new InvalidOperationException( "Concurrent access" );
         }

         return retVal;
      }

      public static Int32 EraseReadData(
         Byte[] array,
         Int32 bufferOffset,
         Int32 bufferTotal
         )
      {
         // Messages end with CRLF
         bufferOffset += 2;
         var remainingData = bufferTotal - bufferOffset;
         if ( remainingData > 0 )
         {
            Array.Copy( array, bufferOffset, array, 0, remainingData );
         }
         return remainingData;
      }

      public static async Task<(Int32, Int32, Int32)> ReadChunkHeader(
         Stream stream,
         ResizableArray<Byte> headerBuffer,
         Int32 alreadyRead,
         Int32 totalExisting,
         Int32 streamReadCount,
         Boolean isFirst
         )
      {
         (var end, var preReadLength) = await stream.ReadUntilMaybeAsync( headerBuffer, alreadyRead, totalExisting, CRLF, streamReadCount );
         if ( !isFirst )
         {
            alreadyRead = end + 2;
            (end, preReadLength) = await stream.ReadUntilMaybeAsync( headerBuffer, end + 2, preReadLength, CRLF, streamReadCount );
         }
         var array = headerBuffer.Array;
         const Int32 LAST_CHUNK_SIZE = 3;
         var isLastChunk = ArrayEqualityComparer<Byte>.RangeEquality( TerminatingChunk, 0, LAST_CHUNK_SIZE, array, alreadyRead, end - alreadyRead );
         var chunkLength = 0;
         if ( isLastChunk )
         {
            // TODO trailers!
            (end, preReadLength) = await stream.ReadUntilMaybeAsync( headerBuffer, end + 2, preReadLength, CRLF, streamReadCount );
         }
         else
         {
            var idx = alreadyRead;
            Int32 curHex;
            while ( ( curHex = ExtractASCIIHexValue( array, idx ) ) >= 0 )
            {
               chunkLength = 0x10 * chunkLength + curHex;
               ++idx;
            }
            // TODO extensions!

         }

         return (chunkLength, end + 2, preReadLength);
      }

      private static Int32 ExtractASCIIHexValue( Byte[] array, Int32 idx )
      {
         var cur = array[idx];
         Int32 retVal;
         if ( cur >= '0' && cur <= '9' )
         {
            retVal = cur - 0x30;
         }
         else if ( ( cur >= 'A' && cur <= 'F' ) )
         {
            retVal = cur - 0x37;
         }
         else if ( ( cur >= 'a' && cur <= 'f' ) )
         {
            retVal = cur - 0x57;
         }
         else
         {
            retVal = -1;
         }
         return retVal;
      }
   }
}
