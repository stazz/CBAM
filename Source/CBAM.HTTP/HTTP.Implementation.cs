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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

using TRequestHeaderDictionary =
#if NET40
   CBAM.HTTP.DictionaryWithReadOnlyAPI<System.String, CBAM.HTTP.ListWithReadOnlyAPI<System.String>>
#else
   System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<string>>
#endif
   ;


namespace CBAM.HTTP
{
   internal sealed class HTTPRequestImpl : HTTPRequest
   {
      public HTTPRequestImpl()
      {
         this.Headers = new TRequestHeaderDictionary();
      }

      public TRequestHeaderDictionary Headers { get; }

      public String Method { get; set; }
      public String Path { get; set; }

      public String Version { get; set; }

      public HTTPRequestContent Content { get; set; }


   }

   internal sealed class HTTPResponseImpl : HTTPResponse
   {
      public HTTPResponseImpl(
         Int32 statusCode,
         String statusCodeMessage,
         String version,
         IDictionary<String, List<String>> headers,
         HTTPResponseContent content
         )
      {
         this.Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<String, IReadOnlyList<String>>( headers.ToDictionary<KeyValuePair<String, List<String>>, String, IReadOnlyList<String>>(
            kvp => kvp.Key,
            kvp =>
#if NET40
            new ListWithReadOnlyAPI<String>
#else
            new System.Collections.ObjectModel.ReadOnlyCollection<String>
#endif
            ( kvp.Value )
            ) );
         this.StatusCode = statusCode;
         this.StatusCodeMessage = statusCodeMessage;
         this.Version = ArgumentValidator.ValidateNotEmpty( nameof( version ), version );
         this.Content = ArgumentValidator.ValidateNotNull( nameof( content ), content );
      }

      public IReadOnlyDictionary<String, IReadOnlyList<String>> Headers { get; }

      public Int32 StatusCode { get; }
      public String StatusCodeMessage { get; }

      public String Version { get; }

      public HTTPResponseContent Content { get; }


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
      private readonly BufferAdvanceState _bufferAdvanceState;
      private readonly CancellationToken _token;
      private Int64 _bytesRemaining;
      private Int32 _state;


      public HTTPResponseContentFromStream(
         Stream stream,
         Byte[] buffer,
         BufferAdvanceState bufferAdvanceState,
         Int64? byteCount,
         CancellationToken token
         )
      {
         this._stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this._preReadData = ArgumentValidator.ValidateNotNull( nameof( buffer ), buffer );
         this._bufferAdvanceState = ArgumentValidator.ValidateNotNull( nameof( BufferAdvanceState ), bufferAdvanceState );
         this.ByteCount = byteCount;
         this._bytesRemaining = byteCount ?? -1;
         this._token = token;
      }

      public Boolean ContentEndIsKnown => this.ByteCount.HasValue;

      public Int64? ByteCount { get; }

      public Int64? BytesRemaining => this._bytesRemaining < 0 ? default : Interlocked.Read( ref this._bytesRemaining );

      public async ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count )
      {
         array.CheckArrayArguments( offset, count, true );
         Int32 bytesRead;
         if ( Interlocked.CompareExchange( ref this._state, READING, INITIAL ) == INITIAL )
         {
            // TODO support for multi-part form stuff
            try
            {
               var remaining = this._bytesRemaining;

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

                  var aState = this._bufferAdvanceState;
                  var bufferRemaining = aState.BufferTotal - aState.BufferOffset;
                  bytesRead = 0;
                  if ( bufferRemaining > 0 )
                  {
                     // We have some data in buffer
                     var bufferReadCount = Math.Min( bufferRemaining, count );
                     Array.Copy( this._preReadData, aState.BufferOffset, array, offset, bufferReadCount );
                     aState.Advance( bufferReadCount );
                     if ( remaining > 0 )
                     {
                        this._bytesRemaining -= bufferReadCount;
                     }
                     count -= bufferReadCount;
                     offset += bufferReadCount;
                     bytesRead = bufferReadCount;

                  }

                  if ( count > 0 )
                  {
                     var streamRead = await this._stream.ReadAsync( array, offset, count, this._token );
                     bytesRead += streamRead;
                     if ( remaining > 0 )
                     {
                        this._bytesRemaining -= streamRead;
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

         return bytesRead;
      }
   }

   public sealed class HTTPResponseContentFromStream_Chunked : HTTPResponseContent
   {
      private static readonly Byte[] CRLF = new[] { (Byte) '\r', (Byte) '\n' };
      private static readonly Byte[] TerminatingChunk = new[] { (Byte) '0', (Byte) '\r', (Byte) '\n' }; //, (Byte) '\r', (Byte) '\n' };

      private const Int32 INITIAL = 0;
      private const Int32 READING = 1;

      private readonly Stream _stream;
      private readonly ResizableArray<Byte> _buffer;
      private readonly BufferAdvanceState _advanceState;
      private readonly Int32 _streamReadCount;
      private readonly CancellationToken _token;


      private Int32 _state;
      private Int32 _chunkRemaining;

      public HTTPResponseContentFromStream_Chunked(
         Stream stream,
         ResizableArray<Byte> buffer,
         BufferAdvanceState advanceState,
         Int32 firstChunkCount,
         Int32 streamReadCount,
         CancellationToken token
         )
      {
         this._state = INITIAL;
         this._stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this._buffer = ArgumentValidator.ValidateNotNull( nameof( buffer ), buffer );
         this._advanceState = ArgumentValidator.ValidateNotNull( nameof( advanceState ), advanceState );
         this._token = token;
         this._streamReadCount = streamReadCount;
         this._chunkRemaining = Math.Max( 0, firstChunkCount );
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
                  // Our whole chunk data has already been read to buffer.
                  retVal = Math.Min( this._chunkRemaining, count );
                  Array.Copy( this._buffer.Array, this._advanceState.BufferOffset, array, offset, retVal );
                  this._advanceState.Advance( retVal );
                  this._chunkRemaining -= retVal;

                  if ( this._chunkRemaining == 0 )
                  {
                     // We must read next chunk
                     EraseReadData( this._advanceState, this._buffer );
                     if ( ( this._chunkRemaining = await ReadNextChunk( this._stream, this._buffer, this._advanceState, this._streamReadCount, this._token ) ) < 0 )
                     {
                        // Clear data
                        EraseReadData( this._advanceState, this._buffer );
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


      public static void EraseReadData(
         BufferAdvanceState aState,
         ResizableArray<Byte> buffer
         )
      {
         var end = aState.BufferOffset;
         var preReadLength = aState.BufferTotal;
         // Messages end with CRLF
         end += 2;
         var remainingData = preReadLength - end;
         if ( remainingData > 0 )
         {
            var array = buffer.Array;
            Array.Copy( array, end, array, 0, remainingData );
         }
         aState.Reset();
         aState.ReadMore( remainingData );
      }

      // When this method is done, the buffer will have header + chunk (including terminating CRLF) in its contents
      // It assumes that chunk headers starts at advanceState.BufferOffset
      // Returns the chunk length (>0) or -1 if last chunk. Will be -1 if last chunk. The advanceState.BufferOffset will always be set after first LF.
      public static async Task<Int32> ReadNextChunk(
         Stream stream,
         ResizableArray<Byte> buffer,
         BufferAdvanceState advanceState,
         Int32 streamReadCount,
         CancellationToken token
         )
      {
         var start = advanceState.BufferOffset;
         await stream.ReadUntilMaybeAsync( buffer, advanceState, CRLF, streamReadCount );
         var array = buffer.Array;
         const Int32 LAST_CHUNK_SIZE = 3;
         var isLastChunk = ArrayEqualityComparer<Byte>.RangeEquality( TerminatingChunk, 0, LAST_CHUNK_SIZE, array, start, advanceState.BufferOffset - start );
         advanceState.Advance( 2 );
         Int32 retVal;
         if ( isLastChunk )
         {
            // TODO trailers!
            await stream.ReadUntilMaybeAsync( buffer, advanceState, CRLF, streamReadCount );
            retVal = -1;
         }
         else
         {
            var idx = start;
            retVal = 0;
            Int32 curHex;
            while ( ( curHex = ExtractASCIIHexValue( array, idx ) ) >= 0 )
            {
               retVal = 0x10 * retVal + curHex;
               ++idx;
            }
            // TODO extensions!
            // Now read content.
            streamReadCount = retVal + 2 - ( advanceState.BufferTotal - advanceState.BufferOffset );
            if ( streamReadCount > 0 )
            {
               await stream.ReadSpecificAmountAsync( buffer, advanceState.BufferTotal, streamReadCount, token );
               advanceState.ReadMore( streamReadCount );
            }
         }

         return retVal;

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
