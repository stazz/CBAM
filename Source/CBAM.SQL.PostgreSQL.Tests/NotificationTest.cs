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
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CBAM.SQL.PostgreSQL.Tests
{
   [TestClass]
   public class NotificationTest : AbstractPostgreSQLTest
   {
      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestNotificationCheck( String connectionConfigFileLocation )
      {
         const String NOTIFICATION_NAME = "testing";
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseConnectionPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         await pool.UseConnectionAsync( async conn =>
         {
            NotificationEventArgs notificationArgs = null;
            conn.NotificationEvent += ( sender, nArgs ) => notificationArgs = nArgs;

            // Check that notification check will not stuck
            await conn.CheckNotificationsAsync();
            Assert.IsNull( notificationArgs );

            // Start listening
            await conn.ExecuteNonQueryAsync( "LISTEN " + NOTIFICATION_NAME );

            // Use another connection pool to issue notify
            await pool.UseConnectionAsync( async conn2 => await conn2.ExecuteNonQueryAsync( "NOTIFY " + NOTIFICATION_NAME ) );

            // Make sure that we have received it
            await conn.CheckNotificationsAsync();
            Assert.IsNotNull( notificationArgs );
            Assert.AreEqual( notificationArgs.Name, NOTIFICATION_NAME );
            Assert.AreNotEqual( notificationArgs.ProcessID, conn.BackendProcessID );
            Assert.AreEqual( notificationArgs.Payload.Length, 0 );
         } );
      }
   }
}
