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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.ResourcePooling.NetworkStream;

namespace CBAM.HTTP.Tests
{
   [TestClass]
   public partial class TestHTTP
   {
      private sealed class HTTPResponseInfo
      {

         private HTTPResponseInfo(
            HTTPResponse response,
            Byte[] content
            )
         {
            this.Version = response.Version;
            this.StatusCode = response.StatusCode;
            this.Message = response.Message;
            this.Headers = response.Headers;
            if ( content != null )
            {
               this.TextualContent = Encoding.UTF8.GetString( content );
            }
         }

         public String Version { get; }
         public Int32 StatusCode { get; }
         public String Message { get; }

         public IDictionary<String, List<String>> Headers { get; }

         public String TextualContent { get; }

         public static async ValueTask<HTTPResponseInfo> CreateInfoAsync( HTTPResponse response, CancellationToken token )
         {
            var content = response.Content;
            Byte[] bytes;
            if ( content != null )
            {
               bytes = await content.ReadAllContentIfKnownSizeAsync( token );
            }
            else
            {
               bytes = null;
            }

            return new HTTPResponseInfo( response, bytes );
         }
      }

      public const Int32 DEFAULT_TIMEOUT = 10000;

      [
      DataTestMethod,
      DataRow( UNENCRYPTED_HOST, UNENCRYPTED_PORT, false, "" ),
      DataRow( ENCRYPTED_HOST, ENCRYPTED_PORT, true, "" ),
      Timeout( DEFAULT_TIMEOUT )
      ]
      public async Task TestHTTPRequestSending( String host, Int32 port, Boolean isSecure, String path )
      {
         var httpConnection = new NetworkStreamFactory()
            .CreateOneTimeUseResourcePool( new HTTPConnectionEndPointConfigurationData()
            {
               Host = host,
               Port = port,
               IsSecure = isSecure
            }.CreateNetworkStreamFactoryConfiguration() )
            .CreateNewHTTPConnection();

         var responses = new ConcurrentBag<HTTPResponseInfo>();
         await httpConnection
            .PrepareStatementForExecution( HTTPMessageFactory.CreateGETRequest( path ) )
            .EnumerateInParallelAsync( async response =>
            {
               responses.Add( await HTTPResponseInfo.CreateInfoAsync( response, default ) );
            } );

         Assert.AreEqual( 1, responses.Count );
      }

      [
      DataTestMethod,
      DataRow( UNENCRYPTED_HOST, UNENCRYPTED_PORT, false, "", 10 ),
      Timeout( DEFAULT_TIMEOUT )
      ]
      public async Task TestHTTPRequestSendingInParallel( String host, Int32 port, Boolean isSecure, String path, Int32 requestCount )
      {
         var httpConnection = new NetworkStreamFactory()
            .CreateOneTimeUseResourcePool( new HTTPConnectionEndPointConfigurationData()
            {
               Host = host,
               Port = port,
               IsSecure = isSecure
            }.CreateNetworkStreamFactoryConfiguration() )
            .CreateNewHTTPConnection();

         var responses = new ConcurrentBag<HTTPResponseInfo>();
         await httpConnection
            .PrepareStatementForExecution( HTTPMessageFactory.CreateGETRequest( path ).CreateRepeater( requestCount ) )
            .EnumerateInParallelAsync( async response =>
            {
               responses.Add( await HTTPResponseInfo.CreateInfoAsync( response, default ) );
            } );

         Assert.AreEqual( requestCount, responses.Count );
      }

   }
}
