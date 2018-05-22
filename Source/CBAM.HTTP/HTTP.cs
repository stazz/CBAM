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
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.HTTP
{

#if NET40
   public sealed class DictionaryWithReadOnlyAPI<TKey, TValue> : Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
   {
      IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.Keys;

      IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.Values;
   }

   public sealed class ListWithReadOnlyAPI<TValue> : List<TValue>, IReadOnlyList<TValue>
   {
      public ListWithReadOnlyAPI()
         : base()
      {
      }

      public ListWithReadOnlyAPI(
         IEnumerable<TValue> collection
         )
         : base( collection )
      {
      }
   }
#endif

   /// <summary>
   /// This interface describes a HTTP request, which client sends to server, from client's point of view.
   /// </summary>
   /// <seealso cref="HTTPMessageFactory"/>
   public interface HTTPRequest : HTTPMessage<HTTPRequestContent,
#if NET40
      DictionaryWithReadOnlyAPI<String, ListWithReadOnlyAPI<String>>
#else
      Dictionary<String, List<String>>
#endif
      ,
#if NET40
      ListWithReadOnlyAPI<String>
#else
      List<String>
#endif
      >
   {
      /// <summary>
      /// Gets or sets the HTTP request method, as string.
      /// </summary>
      /// <value>The HTTP request method, as string.</value>
      String Method { get; set; }

      /// <summary>
      /// Gets or sets the HTTP request path, as string.
      /// </summary>
      /// <value>The HTTP request path, as string.</value>
      String Path { get; set; }

      /// <summary>
      /// Gets or sets the HTTP version of this HTTP message (request or response).
      /// </summary>
      /// <value>The HTTP version of this HTTP message (request or response).</value>
      new String Version { get; set; }

      /// <summary>
      /// Gets or sets the content of this HTTP message (request or response).
      /// </summary>
      /// <value>The content of this HTTP message (request or response).</value>
      new HTTPRequestContent Content { get; set; }
   }

   /// <summary>
   /// This interface describes a HTTP response, which server sends to the client, from client's point of view.
   /// </summary>
   /// <seealso cref="HTTPMessageFactory"/>
   public interface HTTPResponse : HTTPMessage<HTTPResponseContent, IReadOnlyDictionary<String, IReadOnlyList<String>>, IReadOnlyList<String>>
   {
      /// <summary>
      /// Gets the status code returned by the server.
      /// </summary>
      /// <value>The status code returned by the server.</value>
      Int32 StatusCode { get; }

      /// <summary>
      /// Gets the status code message returned by the server.
      /// </summary>
      /// <value>The status code message returned by the server.</value>
      String StatusCodeMessage { get; }
   }

   /// <summary>
   /// This is common interface for <see cref="HTTPRequest"/> and <see cref="HTTPResponse"/>.
   /// </summary>
   /// <typeparam name="TContent"></typeparam>
   public interface HTTPMessage<TContent, TDictionary, TList>
      where TContent : HTTPMessageContent
      where TDictionary : IReadOnlyDictionary<String, TList>
      where TList : IReadOnlyList<String>
   {
      /// <summary>
      /// Gets the HTTP headers of this HTTP message (request or response).
      /// </summary>
      /// <value>The HTTP headers of this HTTP message (request or response).</value>
      TDictionary Headers { get; }

      /// <summary>
      /// Gets the HTTP version of this HTTP message (request or response).
      /// </summary>
      /// <value>The HTTP version of this HTTP message (request or response).</value>
      String Version { get; }

      /// <summary>
      /// Gets the content of this HTTP message (request or response).
      /// </summary>
      /// <value>The content of this HTTP message (request or response).</value>
      TContent Content { get; }
   }

   /// <summary>
   /// This is common interface for <see cref="HTTPRequestContent"/> and <see cref="HTTPResponseContent"/>.
   /// </summary>
   public interface HTTPMessageContent
   {
      /// <summary>
      /// Gets the amount of bytes this content takes, if the amount is known.
      /// </summary>
      /// <value>The amount of bytes this content takes, if the amount is known.</value>
      Int64? ByteCount { get; }

      Boolean ContentEndIsKnown { get; } // TODO not sure if we should even allow situation when this is true??
   }

   /// <summary>
   /// This is the content object for <see cref="HTTPRequest"/>.
   /// </summary>
   /// <seealso cref="HTTPMessageFactory"/>
   public interface HTTPRequestContent : HTTPMessageContent
   {
      /// <summary>
      /// Writes the content bytes of this <see cref="HTTPRequestContent"/> to given <see cref="HTTPWriter"/>.
      /// </summary>
      /// <param name="writer">The <see cref="HTTPWriter"/>.</param>
      /// <param name="seenByteCount">The byte count as returned by <see cref="HTTPMessageContent.ByteCount"/> property.</param>
      /// <returns>Potentially asynchronously returns the amount of bytes written to <see cref="HTTPWriter"/>.</returns>
      ValueTask<Int64> WriteToStream( HTTPWriter writer, Int64? seenByteCount );
   }

   /// <summary>
   /// This is content object for <see cref="HTTPResponse"/>.
   /// </summary>
   /// <seealso cref="HTTPMessageFactory"/>
   public interface HTTPResponseContent : HTTPMessageContent
   {
      /// <summary>
      /// Gets the amount of bytes remaining in this content, if the content byte count is known.
      /// </summary>
      /// <value>The amount of bytes remaining in this content, if the content byte count is known.</value>
      Int64? BytesRemaining { get; }

      /// <summary>
      /// Potentially asynchronously reads the given amount of bytes to given array.
      /// </summary>
      /// <param name="array">The byte array to read to.</param>
      /// <param name="offset">The offset in <paramref name="array"/> where to start writing bytes.</param>
      /// <param name="count">The maximum amount of bytes to write.</param>
      /// <returns>Potentially asynchronously returns amount of bytes read. The return value of <c>0</c> means that end of content has been reached.</returns>
      ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count );
   }

   /// <summary>
   /// This interface is used by <see cref="HTTPRequestContent.WriteToStream"/> method to write the content to HTTP server.
   /// </summary>
   public interface HTTPWriter
   {
      /// <summary>
      /// The buffer to use.
      /// It may be large enough to fit the whole contents.
      /// </summary>
      /// <value>The buffer to write content to.</value>
      Byte[] Buffer { get; }

      /// <summary>
      /// This method will flush whatever is written to <see cref="Buffer"/> of this <see cref="HTTPWriter"/> to underlying stream.
      /// </summary>
      /// <param name="offset">The offset in <see cref="Buffer"/> where to start reading data.</param>
      /// <param name="count">The amount of bytes in <see cref="Buffer"/> to read.</param>
      /// <returns>Potentially asynchronously returns amount of bytes flushed.</returns>
      ValueTask<Int64> FlushBufferContents( Int32 offset, Int32 count );
   }



   /// <summary>
   /// This class implements <see cref="HTTPResponseContent"/> when the content is of size <c>0</c>.
   /// </summary>
   /// <seealso cref="Instance"/>
   public class EmptyHTTPResponseContent : HTTPResponseContent
   {
      /// <summary>
      /// Gets the instance of <see cref="EmptyHTTPResponseContent"/>.
      /// </summary>
      /// <value>The instance of <see cref="EmptyHTTPResponseContent"/>.</value>
      public static EmptyHTTPResponseContent Instance { get; } = new EmptyHTTPResponseContent();

      private EmptyHTTPResponseContent()
      {

      }

      public Boolean ContentEndIsKnown => true;

      /// <summary>
      /// Implements <see cref="HTTPMessageContent.ByteCount"/> and always returns <c>0</c>.
      /// </summary>
      /// <value>Always returns <c>0</c>.</value>
      public Int64? ByteCount => 0;

      /// <summary>
      /// Implements <see cref="HTTPResponseContent.BytesRemaining"/> and always returns <c>0</c>.
      /// </summary>
      /// <value>Always returns <c>0</c>.</value>
      public Int64? BytesRemaining => 0;

      /// <summary>
      /// Implements <see cref="HTTPResponseContent.ReadToBuffer"/> and always returns synchronously <c>0</c>.
      /// </summary>
      /// <param name="array">The byte array.</param>
      /// <param name="offset">The offset in byte array, ignored.</param>
      /// <param name="count">The maximum amount of bytes to read, ignored.</param>
      /// <param name="token">The <see cref="CancellationToken"/>, ignored.</param>
      /// <returns>Always returns <c>0</c> synchronously.</returns>
      public ValueTask<Int32> ReadToBuffer( Byte[] array, Int32 offset, Int32 count )
      {
         array.CheckArrayArguments( offset, count, false );
         return new ValueTask<Int32>( 0 );
      }
   }


   /// <summary>
   /// This static class provides methods to create instances of <see cref="HTTPRequest"/>, <see cref="HTTPResponse"/>, <see cref="HTTPRequestContent"/>, and <see cref="HTTPResponseContent"/> types.
   /// </summary>
   public static class HTTPMessageFactory
   {
      public const String HTTP1_1 = "HTTP/1.1";

      public const String METHOD_GET = "GET";
      public const String METHOD_POST = "POST";

      private static String DefaultIfNullOrEmpty( this String str, String defaultString )
      {
         return String.IsNullOrEmpty( str ) ? defaultString : str;
      }

      /// <summary>
      /// Creates a <see cref="HTTPRequest"/> with <c>"GET"</c> method and given path and version.
      /// </summary>
      /// <param name="path">The value for <see cref="HTTPRequest.Path"/>.</param>
      /// <param name="version">The optional value for <see cref="HTTPMessage{TContent}.Version"/>, is <c>"HTTP/1.1"</c> by default.</param>
      /// <returns>A new instance of <see cref="HTTPRequest"/> with no headers and properties set to given values.</returns>
      public static HTTPRequest CreateGETRequest(
         String path,
         String version = HTTP1_1
         ) => CreateRequest( path, method: METHOD_GET, version: version, content: null );

      /// <summary>
      /// Creates a <see cref="HTTPRequest"/> with <c>"POST"</c> method and given path, content, and version.
      /// </summary>
      /// <param name="path">The value for <see cref="HTTPRequest.Path"/>.</param>
      /// <param name="content">The value for <see cref="HTTPMessage{TContent}.Content"/>.</param>
      /// <param name="version">The optional value for <see cref="HTTPMessage{TContent}.Version"/>, is <c>"HTTP/1.1"</c> by default.</param>
      /// <returns>A new instance of <see cref="HTTPRequest"/> with no headers and properties set to given values.</returns>
      public static HTTPRequest CreatePOSTRequest(
         String path,
         HTTPRequestContent content,
         String version = HTTP1_1
         ) => CreateRequest( path, method: METHOD_POST, version: version, content: content );

      /// <summary>
      /// Creates a <see cref="HTTPRequest"/> with <c>"POST"</c> method and given path, textual content, and version.
      /// </summary>
      ///<param name="path">The value for <see cref="HTTPRequest.Path"/>.</param>
      /// <param name="textualContent">The content for <see cref="HTTPMessage{TContent}.Content"/> as string.</param>
      /// <param name="version">The optional value for <see cref="HTTPMessage{TContent}.Version"/>, is <c>"HTTP/1.1"</c> by default.</param>
      /// <param name="encoding">The optional <see cref="Encoding"/> to use when sending <paramref name="textualContent"/>, is <see cref="Encoding.UTF8"/> by default.</param>
      /// <returns>A new instance of <see cref="HTTPRequest"/> with no headers and properties set to given values.</returns>
      public static HTTPRequest CreatePOSTRequest(
         String path,
         String textualContent,
         String version = HTTP1_1,
         Encoding encoding = null
         ) => CreateRequest( path, METHOD_POST, CreateRequestContentFromString( textualContent, encoding ), version: version );

      /// <summary>
      /// Generic method to create <see cref="HTTPRequest"/> with given properties.
      /// </summary>
      /// <param name="path">The value for <see cref="HTTPRequest.Path"/>.</param>
      /// <param name="method">The value for <see cref="HTTPRequest.Method"/>.</param>
      /// <param name="content">The value for <see cref="HTTPMessage{TContent}.Content"/>.</param>
      /// <param name="version">The optional value for <see cref="HTTPMessage{TContent}.Version"/>, is <c>"HTTP/1.1"</c> by default.</param>
      /// <returns>A new instance of <see cref="HTTPRequest"/> with no headers and properties set to given values.</returns>
      public static HTTPRequest CreateRequest(
         String path,
         String method,
         HTTPRequestContent content,
         String version = HTTP1_1
         )
      {
         HTTPRequest retVal = new HTTPRequestImpl()
         {
            Method = METHOD_GET,
            Path = path
         };

         retVal.Version = DefaultIfNullOrEmpty( version, HTTP1_1 );
         retVal.Content = content;
         return retVal;
      }

      /// <summary>
      /// Creates a new instance of <see cref="HTTPRequestContent"/> which has given <see cref="String"/> as content.
      /// </summary>
      /// <param name="textualContent">The string for the content.</param>
      /// <param name="encoding">The optional <see cref="Encoding"/> to use when sending <paramref name="textualContent"/>, is <see cref="Encoding.UTF8"/> by default.</param>
      /// <returns>A new instance of <see cref="HTTPRequestContent"/> which will use <paramref name="textualContent"/> as contents to send to server.</returns>
      public static HTTPRequestContent CreateRequestContentFromString(
         String textualContent,
         Encoding encoding = null
         ) => new HTTPRequestContentFromString( textualContent, encoding );


      /// <summary>
      /// Creates a new instance of <see cref="HTTPResponse"/> with given parameters.
      /// </summary>
      /// <param name="version">The value for <see cref="HTTPMessage{TContent}.Version"/> property.</param>
      /// <param name="statusCode">The value for <see cref="HTTPResponse.StatusCode"/> property.</param>
      /// <param name="statusMessage">The value for <see cref="HTTPResponse.StatusCodeMessage"/> property.</param>
      /// <param name="content">The value for <see cref="HTTPMessage{TContent}.Content"/> property.</param>
      /// <returns>A new instance of <see cref="HTTPResponse"/> with no headers and properties set to given values.</returns>
      public static HTTPResponse CreateResponse(
         String version,
         Int32 statusCode,
         String statusMessage,
         IDictionary<String, List<String>> headers,
         HTTPResponseContent content
         )
      {
         return new HTTPResponseImpl(
            statusCode,
            statusMessage,
            version,
            headers,
            content
            );
      }

      public static IDictionary<String, List<String>> CreateHeadersDictionary()
      {
         return new Dictionary<String, List<String>>( StringComparer.OrdinalIgnoreCase );
      }

      ///// <summary>
      ///// Creates a new instance of <see cref="HTTPResponseContent"/> which operates on <see cref="Stream"/> to read data from. It assumes that data begins at stream's current position.
      ///// </summary>
      ///// <param name="stream">The stream to read data from.</param>
      ///// <param name="byteCount">The amount of data, if known.</param>
      ///// <param name="onEnd">The callback to run when end of data is encountered.</param>
      ///// <returns>A new instance of<see cref="HTTPResponseContent"/> which redirects read actions to underlying <see cref="Stream"/>.</returns>
      ///// <exception cref="ArgumentNullException">If <paramref name="stream"/> is <c>null</c>.</exception>
      //public static HTTPResponseContent CreateResponseContentFromStream(
      //   Stream stream,
      //   Int64? byteCount,
      //   Func<ValueTask<Boolean>> onEnd
      //   ) => new HTTPResponseContentFromStream( stream, byteCount, onEnd );

      //public static HTTPResponseContent CreateResponseContentFromStreamChunked(
      //   Stream stream,
      //   Int64? byteCount,
      //   Func<ValueTask<Boolean>> onEnd
      //   ) => new HTTPResponseContentFromStream_Chunked(  new HTTPResponseContentFromStream( stream, byteCount, onEnd );
   }
}

/// <summary>
/// This class contains extensions methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// Helper method to invoke <see cref="HTTPWriter.FlushBufferContents"/> with <c>0</c> as first argument to offset.
   /// </summary>
   /// <param name="writer">This <see cref="HTTPWriter"/>.</param>
   /// <param name="count">The amount of bytes from beginning of the <see cref="HTTPWriter.Buffer"/> to flush.</param>
   /// <returns>The amount of bytes written.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPWriter"/> is <c>null</c>.</exception>
   public static ValueTask<Int64> FlushBufferContents( this HTTPWriter writer, Int32 count )
   {
      return writer.FlushBufferContents( 0, count );
   }

   /// <summary>
   /// Helper method to add header with given name and value to this <see cref="HTTPMessage{TContent}"/> and return it.
   /// </summary>
   /// <typeparam name="T">The type of this <see cref="HTTPMessage{TContent}"/></typeparam>
   /// <typeparam name="TContent">The type of the <see cref="HTTPMessageContent"/>.</typeparam>
   /// <param name="message">This <see cref="HTTPMessage{TContent}"/>.</param>
   /// <param name="headerName">The name of the header.</param>
   /// <param name="headerValue">The value of the header.</param>
   /// <returns>This <see cref="HTTPMessage{TContent}"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPMessage{TContent}"/> is <c>null</c>.</exception>
   public static HTTPRequest WithHeader( this HTTPRequest message, String headerName, String headerValue )
   {
      message.Headers
         .GetOrAdd_NotThreadSafe( headerName, hn => new
#if NET40
         ListWithReadOnlyAPI
#else
         List
#endif
         <String>() )
         .Add( headerValue );

      return message;
   }

   /// <summary>
   /// Helper method to read all content of this <see cref="HTTPResponseContent"/> into single byte array, if the byte size of this <see cref="HTTPResponseContent"/> is known.
   /// </summary>
   /// <param name="content">This <see cref="HTTPResponseContent"/>.</param>
   /// <param name="token">The <see cref="CancellationToken"/>.</param>
   /// <returns>Potentially asynchronously returns a new byte array with the contents read from this <see cref="HTTPResponseContent"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPResponseContent"/> is <c>null</c>.</exception>
   /// <exception cref="InvalidOperationException">If this <see cref="HTTPResponseContent"/> does not know its byte size, that is, its <see cref="HTTPResponseContent.BytesRemaining"/> is <c>null</c>.</exception>
   public static ValueTask<Byte[]> ReadAllContentAsync( this HTTPResponseContent content )
   {
      ArgumentValidator.ValidateNotNullReference( content );
      if ( !content.ContentEndIsKnown )
      {
         throw new InvalidOperationException( "The response content does not know where the data ends." );
      }

      var length = content.ByteCount;
      return length.HasValue ?
         content.ReadAllContentIfKnownSizeAsync( content.BytesRemaining.Value ) :
         content.ReadAllContentGrowBuffer();
   }

   private static async ValueTask<Byte[]> ReadAllContentIfKnownSizeAsync( this HTTPResponseContent content, Int64 length64 )
   {
      if ( length64 > Int32.MaxValue )
      {
         throw new InvalidOperationException( "The content length is too big: " + length64 );
      }
      else if ( length64 < 0 )
      {
         throw new InvalidOperationException( "The content length is negative: " + length64 );
      }

      var length = (Int32) length64;
      Byte[] retVal;
      if ( length > 0 )
      {
         retVal = new Byte[length];
         var offset = 0;
         do
         {
            var readCount = await content.ReadToBuffer( retVal, offset, length - offset );
            offset += readCount;
            if ( offset < length && readCount <= 0 )
            {
               throw new EndOfStreamException();
            }
         } while ( offset < length );
      }
      else
      {
         retVal = Empty<Byte>.Array;
      }

      return retVal;
   }

   private static async ValueTask<Byte[]> ReadAllContentGrowBuffer( this HTTPResponseContent content )
   {
      var buffer = new ResizableArray<Byte>( exponentialResize: false );
      var bytesRead = 0;
      Int32 bytesRemaining;
      Int32 bytesSeen = 0;
      while ( ( bytesRemaining = (Int32) content.BytesRemaining.Value ) > 0 )
      {
         bytesSeen += bytesRemaining;
         bytesRead += await content.ReadToBuffer( buffer.SetCapacityAndReturnArray( bytesRead + bytesRemaining ), bytesRead, bytesRemaining );
      }
      // Since exponentialResize was set to false, the array will never be too big
      return buffer.Array;
   }

   /// <summary>
   /// This is helper method to write a <see cref="String"/> to this <see cref="HTTPWriter"/> using given <see cref="Encoding"/>.
   /// </summary>
   /// <param name="writer">This <see cref="HTTPWriter"/>.</param>
   /// <param name="encoding">The <see cref="Encoding"/> to use.</param>
   /// <param name="str">The <see cref="String"/> to write.</param>
   /// <param name="strByteCount">The string byte count, as returned by <see cref="Encoding.GetByteCount(string)"/>. May be <c>null</c>, then this method will call <see cref="Encoding.GetByteCount(string)"/>.</param>
   /// <param name="bufferIndex">The index in <see cref="HTTPWriter.Buffer"/> where to start writing.</param>
   /// <returns>Potentially asynchronously returns amount of bytes written.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPWriter"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If either of <paramref name="encoding"/> or <paramref name="str"/> is <c>null</c>.</exception>
   /// <remarks>
   /// This method also takes care of situation when the <paramref name="str"/> does not fit into <see cref="HTTPWriter.Buffer"/> at once.
   /// </remarks>
   public static ValueTask<Int64> WriteToStreamAsync(
      this HTTPWriter writer,
      Encoding encoding,
      String str,
      Int32? strByteCount,
      Int32 bufferIndex = 0
      )
   {
      ArgumentValidator.ValidateNotNull( nameof( encoding ), encoding );
      ArgumentValidator.ValidateNotNull( nameof( str ), str );

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
         retVal = MultiPartWriteToStreamAsync( writer, encoding, str, byteCount, bufferIndex );
      }

      return retVal;
   }

   private static async ValueTask<Int64> MultiPartWriteToStreamAsync(
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
}