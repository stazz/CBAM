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
using CBAM.HTTP.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace Tests.CBAM.HTTP.Implementation
{
   [TestClass]
   public partial class TestHTTP
   {
      public const Int32 DEFAULT_TIMEOUT = 10000;

      [
      DataTestMethod,
      DataRow( "CBAM_TEST_HTTP_CONFIG" ),
      DataRow( "CBAM_TEST_HTTP_CONFIG_ENCRYPTED" ),
      Timeout( DEFAULT_TIMEOUT )
      ]
      public async Task TestHTTPRequestSending(
         String configFileLocationEnvName
         )
      {
         var configuration = new ConfigurationBuilder()
            .AddJsonFile( System.IO.Path.GetFullPath( Environment.GetEnvironmentVariable( configFileLocationEnvName ) ) )
            .Build()
            .Get<HTTPTestConfiguration>();
         var response = await configuration
            .ConnectionConfiguration
            .CreatePoolAndReceiveTextualResponseAsync(
               HTTPFactory.CreateGETRequest( configuration.Path )
               .WithHeader( "Host", configuration.ConnectionConfiguration.Host ) // The Host is required by HTTP spec ( http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.23 ). Nginx returns 400 otherwise.
            );

         AssertResponse( response, configuration );
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

      //private static void AssertResponses( IEnumerable<HTTPTextualResponseInfo> bag )
      //{
      //   Assert.IsTrue( bag.All( AssertResponse ) );
      //}

      private static Boolean AssertResponse( HTTPTextualResponseInfo info, HTTPTestConfiguration config )
      {
         Assert.AreEqual(
            File.ReadAllText( config.ExpectedContentPath ),
            info.TextualContent
            );

         return true;
      }
   }

   public sealed class HTTPTestConfiguration
   {
      public SimpleHTTPConfiguration ConnectionConfiguration { get; set; }

      public String Path { get; set; }

      public String ExpectedContentPath { get; set; }
   }
}
