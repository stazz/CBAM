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
using CBAM.Abstractions;
using UtilPack.AsyncEnumeration;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UtilPack;
using CBAM.HTTP;
using System.Text;

namespace CBAM.HTTP
{
   /// <summary>
   /// This interface extends <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/> to provide a way to create statements which enable sending <see cref="HTTPRequest"/>s to server.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with each request, used in identifying the request that response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   public interface HTTPConnection<TRequestMetaData> : Connection<HTTPStatement<TRequestMetaData>, HTTPStatementInformation<TRequestMetaData>, HTTPRequestInfo<TRequestMetaData>, HTTPResponseInfo<TRequestMetaData>, HTTPConnectionVendorFunctionality<TRequestMetaData>, IAsyncEnumerable<HTTPResponseInfo<TRequestMetaData>>>
   {
      /// <summary>
      /// Gets the HTTP protocol version used by this <see cref="HTTPConnection{TRequestMetaData}"/>.
      /// </summary>
      String ProtocolVersion { get; }
   }

   /// <summary>
   /// This interface extends <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> to provide any HTTP-specific functionality which does not need a connection.
   /// Currently, there are none, but there might be some in the future.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with each request, used in identifying the request that response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   public interface HTTPConnectionVendorFunctionality<TRequestMetaData> : ConnectionVendorFunctionality<HTTPStatement<TRequestMetaData>, HTTPRequestInfo<TRequestMetaData>>
   {

   }

   /// <summary>
   /// This is information associated with a single response.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of the metadata of the request that this response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   public struct HTTPResponseInfo<TRequestMetaData>
   {
      /// <summary>
      /// Creates a new instance of <see cref="HTTPResponseInfo{TRequestMetaData}"/> with given parameters.
      /// </summary>
      /// <param name="response">The <see cref="HTTPResponse"/>.</param>
      /// <param name="metadata">The metadata of the request that <paramref name="response"/> is associated with.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="response"/> is <c>null</c>.</exception>
      public HTTPResponseInfo(
         HTTPResponse response,
         TRequestMetaData metadata
         )
      {
         this.Response = ArgumentValidator.ValidateNotNull( nameof( response ), response );
         this.RequestMetaData = metadata;
      }

      /// <summary>
      /// Gets the <see cref="HTTPResponse"/> of this <see cref="HTTPResponseInfo{TRequestMetaData}"/>.
      /// </summary>
      /// <value>The <see cref="HTTPResponse"/> of this <see cref="HTTPResponseInfo{TRequestMetaData}"/>.</value>
      public HTTPResponse Response { get; }

      /// <summary>
      /// Gets the metadata of the request that <see cref="Response"/> is associated with.
      /// </summary>
      /// <value>The metadata of the request that <see cref="Response"/> is associated with.</value>
      public TRequestMetaData RequestMetaData { get; }
   }

   /// <summary>
   /// This struct binds together the <see cref="HTTPRequest"/> and metadata.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata of the request. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   public struct HTTPRequestInfo<TRequestMetaData>
   {
      /// <summary>
      /// Creates a new instance of <see cref="HTTPRequestInfo{TRequestMetaData}"/> with given parameters.
      /// </summary>
      /// <param name="request">The <see cref="HTTPRequest"/>. May be <c>null</c>.</param>
      /// <param name="metadata">The metadata of the <paramref name="request"/>.</param>
      public HTTPRequestInfo(
         HTTPRequest request,
         TRequestMetaData metadata
         )
      {
         this.Request = request;
         this.RequestMetaData = metadata;
      }

      /// <summary>
      /// Gets the <see cref="HTTPRequest"/> of this <see cref="HTTPRequestInfo{TRequestMetaData}"/>.
      /// </summary>
      /// <value></value>
      public HTTPRequest Request { get; }

      /// <summary>
      /// Gets the metadata of the <see cref="Request"/>.
      /// </summary>
      /// <value>The metadata of the <see cref="Request"/>.</value>
      public TRequestMetaData RequestMetaData { get; }
   }

   /// <summary>
   /// This class is meant to be used in simple situations, when the textual content of the HTTP response is meant to be created, without handling memory and performance or stream copying manually.
   /// </summary>
   /// <seealso cref="E_CBAM.CreateTextualResponseInfo"/>
   public sealed class HTTPTextualResponseInfo
   {
      private static readonly Encoding DefaultTextEncoding = new UTF8Encoding( false, false );

      /// <summary>
      /// Creates a new instance of <see cref="HTTPTextualResponseInfo"/> with given parameters.
      /// </summary>
      /// <param name="response">The <see cref="HTTPResponse"/>.</param>
      /// <param name="content">The content of the <see cref="HTTPResponse"/>, as byte array.</param>
      /// <param name="defaultEncoding">The default <see cref="Encoding"/> to use if no encoding can be deduced from <see cref="HTTPResponse"/>. If <c>null</c>, then <see cref="UTF8Encoding"/> will be used.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="response"/> is <c>null</c>.</exception>
      public HTTPTextualResponseInfo(
         HTTPResponse response,
         Byte[] content,
         Encoding defaultEncoding
         )
      {
         ArgumentValidator.ValidateNotNull( nameof( response ), response );

         this.Version = response.Version;
         this.StatusCode = response.StatusCode;
         this.Message = response.StatusCodeMessage;
         this.Headers = response.Headers;
         String textualContent;
         if ( !content.IsNullOrEmpty() )
         {
            String cType; Int32 charsetIndex; Int32 charsetEndIdx;
            var encoding = response.Headers.TryGetValue( "Content-Type", out var cTypes ) && cTypes.Count > 0 && ( charsetIndex = ( cType = cTypes[0] ).IndexOf( "charset=" ) ) >= 0 ?
               Encoding.GetEncoding( cType.Substring( charsetIndex + 8, ( ( charsetEndIdx = cType.IndexOf( ';', charsetIndex + 9 ) ) > 0 ? charsetEndIdx : cType.Length ) - charsetIndex - 8 ) ) :
               ( defaultEncoding ?? DefaultTextEncoding );
            textualContent = encoding.GetString( content, 0, content.Length );
         }
         else
         {
            textualContent = String.Empty;
         }
         this.TextualContent = textualContent;
      }

      /// <summary>
      /// Gets the <see cref="HTTPMessage{TContent, TDictionary, TList}.Version"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.
      /// </summary>
      /// <value>The <see cref="HTTPMessage{TContent, TDictionary, TList}.Version"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.</value>
      public String Version { get; }

      /// <summary>
      /// Gets the <see cref="HTTPResponse.StatusCode"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.
      /// </summary>
      /// <value>The <see cref="HTTPResponse.StatusCode"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.</value>
      public Int32 StatusCode { get; }

      /// <summary>
      /// Gets the <see cref="HTTPResponse.StatusCodeMessage"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.
      /// </summary>
      /// <value>The <see cref="HTTPResponse.StatusCodeMessage"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.</value>
      public String Message { get; }

      /// <summary>
      /// Gets the <see cref="HTTPMessage{TContent, TDictionary, TList}.Headers"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.
      /// </summary>
      /// <value>the <see cref="HTTPMessage{TContent, TDictionary, TList}.Headers"/> of the <see cref="HTTPResponse"/> this <see cref="HTTPTextualResponseInfo"/> was created from.</value>
      public IReadOnlyDictionary<String, IReadOnlyList<String>> Headers { get; }

      /// <summary>
      /// Gets the deserialized <see cref="HTTPResponseContent"/> as <see cref="String"/>.
      /// </summary>
      /// <value>The deserialized <see cref="HTTPResponseContent"/> as <see cref="String"/>.</value>
      public String TextualContent { get; }


   }

}

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// This method will asynchronously receive one <see cref="HTTPTextualResponseInfo"/> from this <see cref="HTTPConnection{TRequestMetaData}"/>, after sending a given <see cref="HTTPRequest"/>.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with given <see cref="HTTPRequest"/>. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   /// <param name="connection">This <see cref="HTTPConnection{TRequestMetaData}"/>.</param>
   /// <param name="request">The <see cref="HTTPRequest"/> to send.</param>
   /// <param name="metaData">The metadata of the <paramref name="request"/>.</param>
   /// <param name="defaultEncoding">The default encoding to use when deserializing response contents to string. By default, it is <see cref="UTF8Encoding"/>.</param>
   /// <returns>Potentially asynchronously creates constructed <see cref="HTTPTextualResponseInfo"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPConnection{TRequestMetaData}"/> is <c>null</c>.</exception>
   public static ValueTask<HTTPTextualResponseInfo> ReceiveOneTextualResponse<TRequestMetaData>(
      this HTTPConnection<TRequestMetaData> connection,
      HTTPRequest request,
      TRequestMetaData metaData = default,
      Encoding defaultEncoding = default
      )
   {

      return connection
         .PrepareStatementForExecution( new HTTPRequestInfo<TRequestMetaData>( request, metaData ) )
         .Select( async responseInfo => await responseInfo.Response.CreateTextualResponseInfo( defaultEncoding ) )
         .FirstAsync();
   }

   /// <summary>
   /// Asynchronously creates <see cref="HTTPTextualResponseInfo"/> from this <see cref="HTTPResponse"/>.
   /// </summary>
   /// <param name="response">This <see cref="HTTPResponse"/>.</param>
   /// <param name="defaultEncoding">The <see cref="Encoding"/> to use if <see cref="HTTPResponse"/> does not contain any encoding information. By default, it is <see cref="UTF8Encoding"/>.</param>
   /// <returns>Potentially asynchronously creates constructed <see cref="HTTPTextualResponseInfo"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPResponse"/> is <c>null</c>.</exception>
   public static async ValueTask<HTTPTextualResponseInfo> CreateTextualResponseInfo(
      this HTTPResponse response,
      Encoding defaultEncoding = default
      )
   {
      var content = response.Content;
      Byte[] bytes;
      if ( content != null )
      {
         bytes = await content.ReadAllContentAsync();
      }
      else
      {
         bytes = null;
      }

      return new HTTPTextualResponseInfo( response, bytes, defaultEncoding );
   }
}