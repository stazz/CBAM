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
   internal class HTTPMessageImpl<TContent> : HTTPMessage<TContent>
      where TContent : HTTPMessageContent
   {
      protected HTTPMessageImpl()
      {
         this.Headers = new Dictionary<String, List<String>>( StringComparer.OrdinalIgnoreCase );
      }

      public IDictionary<String, List<String>> Headers { get; }

      public String Version { get; set; }
      public TContent Content { get; set; }
   }

   internal sealed class HTTPRequestImpl : HTTPMessageImpl<HTTPRequestContent>, HTTPRequest
   {
      public String Method { get; set; }
      public String Path { get; set; }
   }

   internal sealed class HTTPResponseImpl : HTTPMessageImpl<HTTPResponseContent>, HTTPResponse
   {
      public Int32 StatusCode { get; set; }
      public String StatusCodeMessage { get; set; }
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
   }

   internal sealed class HTTPResponseContentFromStream : HTTPResponseContent
   {
      private const Int32 INITIAL = 0;
      private const Int32 READING = 1;

      private readonly Stream _stream;
      private Int64 _bytesRemaining;
      private Int32 _state;
      private readonly Func<ValueTask<Boolean>> _onEnd;
      private Int32 _onEndCalled;


      public HTTPResponseContentFromStream(
         Stream stream,
         Int64? byteCount,
         Func<ValueTask<Boolean>> onEnd
         )
      {
         this._stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
         this.ByteCount = byteCount;
         this._bytesRemaining = byteCount ?? -1;
         this._onEnd = onEnd;
      }

      public Int64? ByteCount { get; }

      public Int64? BytesRemaining => this._bytesRemaining < 0 ? default : Interlocked.Read( ref this._bytesRemaining );

      public async ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count, CancellationToken token )
      {
         if ( Interlocked.CompareExchange( ref this._state, READING, INITIAL ) == INITIAL )
         {
            // TODO support for multi-part form stuff
            try
            {
               var remaining = this._bytesRemaining;

               Int32 bytesRead;
               if ( remaining < 0 )
               {
                  // Unknown byte size
                  bytesRead = await this._stream.ReadAsync( array, offset, count, token );
               }
               else if ( remaining > 0 )
               {
                  // Known byte size, read only what can be read
                  count = (Int32) Math.Min( count, remaining );
                  bytesRead = await this._stream.ReadAsync( array, offset, count, token );
                  Interlocked.Exchange( ref this._bytesRemaining, remaining - count );
               }
               else
               {
                  // No more bytes left
                  bytesRead = 0;
               }

               // No need to use CEX since we are inside CEX-mutex
               if ( ( bytesRead <= 0 || this._bytesRemaining == 0 ) && this._onEndCalled == 0 )
               {
                  Interlocked.Exchange( ref this._onEndCalled, 1 );
                  try
                  {
                     await ( this._onEnd?.Invoke() ?? default );
                  }
                  catch
                  {
                     // Ignore
                  }
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
   }
}
