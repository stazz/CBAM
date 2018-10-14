/*
 * Copyright 2018 Stanislav Muhametsin. All rights Reserved.
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
using CBAM.NATS.Implementation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.Configuration.NetworkStream;

namespace CBAM.NATS.Tests
{
   [TestClass]
   public class SimplePublishSubscribeTest
   {
      // , Timeout( 10000 )
      [TestMethod]
      public async Task PerformTest()
      {
         const String SUBJECT = "MyTestSubject";
         var expectedData = new Byte[] { 1, 2, 3 };
         NATSMessage receivedMessage = null;

         var pool = NATSConnectionPoolProvider.Factory.BindCreationParameters( new NATSConnectionCreationInfo( new NATSConnectionCreationInfoData()
         {
            Connection = new NATSConnectionConfiguration()
            {
               Host = "localhost",
               Port = 4222,
               ConnectionSSLMode = ConnectionSSLMode.NotRequired
            }
         } ) ).CreateOneTimeUseResourcePool().WithoutExplicitAPI();

         var subscribeTask = pool.UseResourceAsync( async natsConn =>
         {
            await natsConn.SubscribeAsync( SUBJECT, autoUnsubscribeAfter: 1 ).EnumerateAsync( msg =>
            {
               receivedMessage = msg;
            } );
         } );
         var publishTask = pool.UseResourceAsync( async publishConnection =>
         {
            await Task.Delay( 500 );
            await publishConnection.PublishWithStaticDataProducerForWholeArray( SUBJECT, expectedData, repeatCount: 1 ).EnumerateAsync();
         } );

         await Task.WhenAll( subscribeTask, publishTask );

         Assert.IsNotNull( receivedMessage );
         Assert.AreEqual( SUBJECT, receivedMessage.Subject );
         Assert.IsTrue( ArrayEqualityComparer<Byte>.ArrayEquality( expectedData, receivedMessage.CreateDataArray() ) );
      }
   }
}
