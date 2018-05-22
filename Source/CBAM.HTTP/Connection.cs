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
   /// This interface extends <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/> to provide a way to create statements which enable sending <see cref="HTTPRequest"/>s to client.
   /// </summary>
   public interface HTTPConnection<TRequestMetaData> : Connection<HTTPStatement<TRequestMetaData>, HTTPStatementInformation<TRequestMetaData>, HTTPRequestInfo<TRequestMetaData>, HTTPResponseInfo<TRequestMetaData>, HTTPConnectionVendorFunctionality<TRequestMetaData>, IAsyncEnumerable<HTTPResponseInfo<TRequestMetaData>>>
   {
      String ProtocolVersion { get; }
   }

   /// <summary>
   /// This interface extends <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> to provide any HTTP-specific functionality which does not need a connection.
   /// </summary>
   public interface HTTPConnectionVendorFunctionality<TRequestMetaData> : ConnectionVendorFunctionality<HTTPStatement<TRequestMetaData>, HTTPRequestInfo<TRequestMetaData>>
   {

   }

   public struct HTTPResponseInfo<TRequestMetaData>
   {
      public HTTPResponseInfo(
         HTTPResponse response,
         TRequestMetaData md
         )
      {
         this.Response = ArgumentValidator.ValidateNotNull( nameof( response ), response );
         this.RequestMetaData = md;
      }

      public HTTPResponse Response { get; }

      public TRequestMetaData RequestMetaData { get; }
   }

   public struct HTTPRequestInfo<TRequestMetaData>
   {
      public HTTPRequestInfo(
         HTTPRequest request,
         TRequestMetaData md
         )
      {
         this.Request = request;
         this.RequestMetaData = md;
      }

      public HTTPRequest Request { get; }

      public TRequestMetaData RequestMetaData { get; }
   }

   public sealed class HTTPTextualResponseInfo
   {
      private static readonly Encoding TextEncoding = new UTF8Encoding( false, false );

      private HTTPTextualResponseInfo(
         HTTPResponse response,
         Byte[] content
         )
      {
         this.Version = response.Version;
         this.StatusCode = response.StatusCode;
         this.Message = response.StatusCodeMessage;
         this.Headers = response.Headers;
         if ( content != null )
         {
            String cType; Int32 charsetIndex; Int32 charsetEndIdx;
            var encoding = response.Headers.TryGetValue( "Content-Type", out var cTypes ) && cTypes.Count > 0 && ( charsetIndex = ( cType = cTypes[0] ).IndexOf( "charset=" ) ) >= 0 ?
               Encoding.GetEncoding( cType.Substring( charsetIndex + 8, ( ( charsetEndIdx = cType.IndexOf( ';', charsetIndex + 9 ) ) > 0 ? charsetEndIdx : cType.Length ) - charsetIndex - 8 ) ) :
               TextEncoding;
            this.TextualContent = encoding.GetString( content, 0, content.Length );
         }
      }

      public String Version { get; }
      public Int32 StatusCode { get; }
      public String Message { get; }

      public IReadOnlyDictionary<String, IReadOnlyList<String>> Headers { get; }

      public String TextualContent { get; }

      public static async ValueTask<HTTPTextualResponseInfo> CreateInfoAsync( HTTPResponse response )
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

         return new HTTPTextualResponseInfo( response, bytes );
      }
   }

}

public static partial class E_CBAM
{
   public static ValueTask<HTTPTextualResponseInfo> ReceiveOneResponse<TRequestMetaData>(
      this HTTPConnection<TRequestMetaData> connection,
      HTTPRequest request,
      TRequestMetaData metaData = default
      )
   {
      return connection
         .PrepareStatementForExecution( new HTTPRequestInfo<TRequestMetaData>( request, metaData ) )
         .Select( async responseInfo => await HTTPTextualResponseInfo.CreateInfoAsync( responseInfo.Response ) )
         .FirstAsync();
   }
}