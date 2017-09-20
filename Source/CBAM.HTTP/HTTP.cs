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
using CBAM.HTTP;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.HTTP
{
   // It would be nice to use System.Net.Http namespace, but it is old and outdated API, and HttpResponseMessage can be properly build only by HttpClient, and that is not suitable for this library at all.
   // Microsoft.AspNetCore.Http would be also nice, but that is server-oriented API, while this is client-oriented API, and therefore can't be used directly either (also it requires .NET Standard 2.0).

   public interface HTTPRequest : HTTPMessage<HTTPRequestContent>
   {
      String Method { get; set; }
      String Path { get; set; }
   }

   public interface HTTPResponse : HTTPMessage<HTTPResponseContent>
   {
      Int32 StatusCode { get; set; }
      String Message { get; set; }
   }

   public interface HTTPMessage<TContent>
      where TContent : HTTPMessageContent
   {
      IDictionary<String, List<String>> Headers { get; }
      String Version { get; set; }
      TContent Content { get; set; }
   }

   public interface HTTPMessageContent
   {
      Int64? ByteCount { get; }
   }

   public interface HTTPRequestContent : HTTPMessageContent
   {
      // Return amount of bytes written
      ValueTask<Int64> WriteToStream( HTTPWriter writer, Int64? seenByteCount );
   }

   public interface HTTPResponseContent : HTTPMessageContent
   {
      Int64? BytesRemaining { get; }

      ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count, CancellationToken token = default );
   }

   public interface HTTPWriter
   {
      Byte[] Buffer { get; }
      ValueTask<Int64> FlushBufferContents( Int32 offset, Int32 count );
   }

   public class HTTPMessageImpl<TContent> : HTTPMessage<TContent>
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

   public class HTTPRequestImpl : HTTPMessageImpl<HTTPRequestContent>, HTTPRequest
   {
      public String Method { get; set; }
      public String Path { get; set; }
   }

   public class HTTPResponseImpl : HTTPMessageImpl<HTTPResponseContent>, HTTPResponse
   {
      public Int32 StatusCode { get; set; }
      public String Message { get; set; }
   }

   public class HTTPRequestContentFromString : HTTPRequestContent
   {
      public HTTPRequestContentFromString( String str )
      {
         this.StringContent = str ?? String.Empty;
         this.Encoding = System.Text.Encoding.UTF8;
      }

      public Int64? ByteCount => this.Encoding.GetByteCount( this.StringContent );

      public ValueTask<Int64> WriteToStream( HTTPWriter writer, Int64? seenByteCount )
      {
         return WriteToStream( writer, this.Encoding, this.StringContent, seenByteCount.HasValue ? (Int32?) seenByteCount.Value : null );
      }

      public static ValueTask<Int64> WriteToStream(
         HTTPWriter writer,
         Encoding encoding,
         String str,
         Int32? strByteCount,
         Int32 bufferIndex = 0
         )
      {
         var buffer = writer.Buffer;
         var bufferLen = buffer.Length;

         var byteCount = strByteCount ?? encoding.GetByteCount( str );

         ValueTask<Int64> retVal;
         if ( bufferLen >= byteCount )
         {
            // Can just write it directly
            retVal = writer.FlushBufferContents( bufferIndex + encoding.GetBytes( str, 0, str.Length, buffer, bufferIndex ) );
         }
         else
         {
            retVal = WriteToStreamAsync( writer, encoding, str, byteCount, bufferIndex );
         }

         return retVal;
      }

      private static async ValueTask<Int64> WriteToStreamAsync(
         HTTPWriter writer,
         Encoding encoding,
         String text,
         Int32 seenByteCount,
         Int32 bufferIndex
         )
      {
         var buffer = writer.Buffer;
         var bufferLen = buffer.Length;

         // Make sure there is always room for max size (4) char
         if ( bufferLen - bufferIndex <= 4 )
         {
            throw new InvalidOperationException( "Too small buffer" );
         }

         bufferLen -= 4;

         var cur = 0;
         var textLen = text.Length;
         do
         {
            while ( cur < textLen && bufferIndex < bufferLen )
            {
               Int32 count;
               if ( Char.IsLowSurrogate( text[cur] ) && cur < textLen - 1 && Char.IsHighSurrogate( text[cur + 1] ) )
               {
                  count = 2;
               }
               else
               {
                  count = 1;
               }
               bufferIndex += encoding.GetBytes( text, cur, count, buffer, bufferIndex );
               cur += count;
            }
            await writer.FlushBufferContents( bufferIndex );
            bufferIndex = 0;
         } while ( cur < textLen );

         return seenByteCount;
      }

      public Encoding Encoding { get; set; }

      public String StringContent { get; }
   }

   public class HTTPResponseContentFromStream : HTTPResponseContent
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

   public class EmptyHTTPResponseContent : HTTPResponseContent
   {
      public static EmptyHTTPResponseContent Instance { get; } = new EmptyHTTPResponseContent();

      private EmptyHTTPResponseContent()
      {

      }

      public Int64? ByteCount => 0;

      public Int64? BytesRemaining => 0;

      public ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count, CancellationToken token = default )
      {
         array.CheckArrayArguments( offset, count, false );
         return new ValueTask<Int32>( 0 );
      }
   }



   public static class HTTPMessageFactory
   {
      private const String HTTP1_1 = "HTTP/1.1";

      private const String METHOD_GET = "GET";
      private const String METHOD_POST = "POST";

      public static Func<HTTPRequest> RepeatRequest( HTTPRequest request, Int32 count )
      {
         return () =>
         {
            var createRequest = count > 0 && Interlocked.Decrement( ref count ) >= 0;
            return createRequest ?
               request :
               null;
         };
      }

      public static Func<HTTPRequest> GenerateRequest( Func<Int32, HTTPRequest> generator, Int32 count )
      {
         var amount = count;
         return () =>
         {
            HTTPRequest retVal = null;
            Int32 decremented;
            if ( count > 0 && ( decremented = Interlocked.Decrement( ref count ) ) >= 0 )
            {
               retVal = generator( amount - decremented - 1 );
            }

            return retVal;
         };
      }

      public static HTTPRequest CreateGETRequest( String path )
      {
         return new HTTPRequestImpl()
         {
            Version = HTTP1_1,
            Method = METHOD_GET,
            Path = path
         };
      }

      public static HTTPRequest CreatePOSTRequest( String path, HTTPRequestContent content )
      {
         return new HTTPRequestImpl()
         {
            Version = HTTP1_1,
            Method = METHOD_POST,
            Path = path,
            Content = content
         };
      }

      public static HTTPRequest CreatePOSTRequest( String path, String textualContent ) => CreatePOSTRequest( path, new HTTPRequestContentFromString( textualContent ) );

      public static HTTPResponse CreateResponse(
         String version,
         Int32 statusCode,
         String statusMessage,
         HTTPResponseContent content
         )
      {
         return new HTTPResponseImpl()
         {
            Version = version,
            StatusCode = statusCode,
            Message = statusMessage,
            Content = content
         };
      }
   }
}

public static partial class E_HTTP
{
   public static ValueTask<Int64> FlushBufferContents( this HTTPWriter writer, Int32 count )
   {
      return writer.FlushBufferContents( 0, count );
   }

   public static HTTPMessage<TContent> WithHeader<TContent>( this HTTPMessage<TContent> message, String headerName, String headerValue )
      where TContent : HTTPMessageContent
   {
      message.Headers
         .GetOrAdd_NotThreadSafe( headerName, hn => new List<String>() )
         .Add( headerValue );

      return message;
   }

   public static async ValueTask<Byte[]> ReadAllContentIfKnownSizeAsync( this HTTPResponseContent content, CancellationToken token = default )
   {
      ArgumentValidator.ValidateNotNullReference( content );
      var length = (Int32) ( content.BytesRemaining ?? throw new InvalidOperationException( "Content must have known byte count." ) );
      Byte[] retVal;
      if ( length > 0 )
      {
         retVal = new Byte[length];
         var offset = 0;
         do
         {
            offset += await content.ReadToBuffer( retVal, offset, length - offset, token );
         } while ( content.BytesRemaining.Value > 0 );
      }
      else
      {
         retVal = Empty<Byte>.Array;
      }

      return retVal;
   }
}