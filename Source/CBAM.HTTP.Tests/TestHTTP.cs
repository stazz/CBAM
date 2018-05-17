﻿/*
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
using CBAM.HTTP.Implementation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
         private static readonly Encoding TextEncoding = new UTF8Encoding( false, false );
         private HTTPResponseInfo(
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
               this.TextualContent = encoding.GetString( content );
            }
         }

         public String Version { get; }
         public Int32 StatusCode { get; }
         public String Message { get; }

         public IReadOnlyDictionary<String, IReadOnlyList<String>> Headers { get; }

         public String TextualContent { get; }

         public static async ValueTask<HTTPResponseInfo> CreateInfoAsync( HTTPResponse response )
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

            return new HTTPResponseInfo( response, bytes );
         }
      }

      public const Int32 DEFAULT_TIMEOUT = 10000;

      [
      DataTestMethod,
      DataRow( UNENCRYPTED_HOST, UNENCRYPTED_PORT, false, "" ),
      //DataRow( ENCRYPTED_HOST, ENCRYPTED_PORT, true, "" ),
      //Timeout( DEFAULT_TIMEOUT )
      ]
      public async Task TestHTTPRequestSending( String host, Int32 port, Boolean isSecure, String path )
      {
         var pool = HTTPNetworkConnectionPoolProvider<Int64>.Factory
            .BindCreationParameters( new HTTPNetworkCreationInfo( new HTTPNetworkCreationInfoData()
            {
               Connection = new HTTPConnectionConfiguration()
               {
                  Host = host,
                  Port = port,
                  ConnectionSSLMode = isSecure ? UtilPack.Configuration.NetworkStream.ConnectionSSLMode.Required : UtilPack.Configuration.NetworkStream.ConnectionSSLMode.NotRequired
               }
            } ) ).CreateOneTimeUseResourcePool();

         var response = await pool.UseResourceAsync( async httpConn =>
         {
            return await httpConn
               .PrepareStatementForExecution( new HTTPRequestInfo<Int64>( HTTPMessageFactory.CreateGETRequest( path ), 1 ) )
               .Select( async responseInfo => (responseInfo.RequestMetaData, await HTTPResponseInfo.CreateInfoAsync( responseInfo.Response )) )
               .FirstAsync();
         } );

         Assert.AreEqual( 1, response.RequestMetaData );
         AssertResponse( response.Item2 );


         //var httpConnection = NetworkStreamFactory.Instance
         //   .BindCreationParameters( new HTTPConnectionEndPointConfigurationData()
         //   {
         //      Host = host,
         //      Port = port,
         //      IsSecure = isSecure
         //   }.CreateNetworkStreamFactoryConfiguration() )
         //   .CreateOneTimeUseResourcePool()
         //   .CreateNewHTTPConnection();

         //var responses = await httpConnection
         //   .PrepareStatementForExecution( HTTPMessageFactory.CreateGETRequest( path ) )
         //   .ToConcurrentBagAsync( async response => await HTTPResponseInfo.CreateInfoAsync( response ) );

         //Assert.AreEqual( 1, responses.Count );
         //AssertResponses( responses );
      }

      //[
      //DataTestMethod,
      //DataRow( UNENCRYPTED_HOST, UNENCRYPTED_PORT, false, "", 10 ),
      ////Timeout( DEFAULT_TIMEOUT )
      //]
      //public async Task TestHTTPRequestSendingInParallel( String host, Int32 port, Boolean isSecure, String path, Int32 requestCount )
      //{
      //   var httpConnection = NetworkStreamFactory.Instance
      //      .BindCreationParameters( new HTTPConnectionEndPointConfigurationData()
      //      {
      //         Host = host,
      //         Port = port,
      //         IsSecure = isSecure
      //      }.CreateNetworkStreamFactoryConfiguration() )
      //      .CreateOneTimeUseResourcePool()
      //      .CreateNewHTTPConnection();

      //   var responses = await httpConnection
      //      .PrepareStatementForExecution( HTTPMessageFactory.CreateGETRequest( path ).CreateRepeater( requestCount ) )
      //      .ToConcurrentBagAsync( async response => await HTTPResponseInfo.CreateInfoAsync( response ) );

      //   Assert.AreEqual( requestCount, responses.Count );
      //   AssertResponses( responses );
      //}

      //[DataTestMethod,
      //   DataRow( UNENCRYPTED_HOST, UNENCRYPTED_PORT, false, "", 20, 10 )
      //   //, Timeout( DEFAULT_TIMEOUT )
      //   ]
      //public async Task TestHTTPWithLimitedPool(
      //   String host, Int32 port, Boolean isSecure, String path, Int32 requestCount, Int32 poolLimit
      //   )
      //{
      //   ConcurrentBag<String> responseTexts;
      //   Int32 streamsAcquired = 0;
      //   using ( var pool = NetworkStreamFactory.Instance
      //      .BindCreationParameters(
      //         new HTTPConnectionEndPointConfigurationData()
      //         {
      //            Host = host,
      //            Port = port,
      //            IsSecure = isSecure
      //         }.CreateNetworkStreamFactoryConfiguration()
      //         )
      //      .CreateTimeoutingAndLimitedResourcePool( poolLimit )
      //      )
      //   {
      //      pool.AfterResourceCreationEvent += ( argz ) => Interlocked.Increment( ref streamsAcquired );
      //      var httpConnection = pool.CreateNewHTTPConnection();

      //      // Send 20 requests in parallel and process each response
      //      responseTexts = await httpConnection
      //         .PrepareStatementForExecution( HTTPMessageFactory.CreateGETRequest( "/" ).CreateRepeater( requestCount ) )
      //         .ToConcurrentBagAsync( async response => Encoding.UTF8.GetString( await response.Content.ReadAllContentAsync() ) );

      //   }

      //   Assert.AreEqual( requestCount, responseTexts.Count );
      //   Assert.AreEqual( poolLimit, streamsAcquired );

      //}

      private static void AssertResponses( IEnumerable<HTTPResponseInfo> bag )
      {
         Assert.IsTrue( bag.All( AssertResponse ) );
      }

      private static Boolean AssertResponse( HTTPResponseInfo info )
      {
         Assert.IsNotNull( info.TextualContent );

         return true;
      }
   }
}
