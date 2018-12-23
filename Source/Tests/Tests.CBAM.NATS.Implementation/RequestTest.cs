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
using CBAM.NATS;
using CBAM.NATS.Implementation;
using IOUtils.Network.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace Tests.CBAM.NATS.Implementation
{
   [TestClass]
   public class RequestTest : AbstractNATSTest
   {

      [TestMethod, Timeout( TIMEOUT )]
      public async Task PerformTest()
      {
         const String SUBJECT = "MyTestSubject";
         var sentData = new Byte[] { 1, 2, 3 };
         var receivedData = new Byte[] { 4, 5, 6 };

         var pool = NATSConnectionPoolProvider
            .Factory
            .BindCreationParameters( new NATSConnectionCreationInfo( GetNATSConfiguration() ) )
            .CreateOneTimeUseResourcePool();

         var subscribeTask = pool.UseResourceAsync( async natsConn =>
         {
            await Task.Delay( 500 );
            return await natsConn.RequestAsync( SUBJECT, sentData );
         }, default );
         var publishTask = pool.UseResourceAsync( async publishConnection =>
         {
            var msg = await publishConnection.SubscribeAsync( SUBJECT ).FirstOrDefaultAsync();
            await publishConnection.PublishWithStaticDataProducerForWholeArray( msg.ReplyTo, receivedData, repeatCount: 1 )
               .EnumerateAsync();
            return (NATSMessage) null;
         }, default );

         var receivedMessage = ( await Task.WhenAll( subscribeTask, publishTask ) )[0];

         Assert.IsNotNull( receivedMessage );
         //Assert.AreEqual( SUBJECT, receivedMessage.Subject );
         Assert.IsTrue( ArrayEqualityComparer<Byte>.ArrayEquality( receivedData, receivedMessage.CreateDataArray() ) );
      }
   }
}

