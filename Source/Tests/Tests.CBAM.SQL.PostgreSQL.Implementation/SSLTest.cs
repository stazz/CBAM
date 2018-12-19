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
using IOUtils.Network.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CBAM.SQL.PostgreSQL.Tests
{
   [TestClass]
   public class SSLTest : AbstractPostgreSQLTest
   {
      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION_SSL ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestSimpleSSL( String connectionConfigFileLocation )
      {
         // No other proper way to ensure SSL actually works
         var creationInfo = GetConnectionCreationInfo( connectionConfigFileLocation );
         // Since server needs to be configured for SSL mode as well, a separate config file is most generic option (in case SSL-enabled server is in different end-point than normal server used in tests)
         creationInfo.CreationData.Connection.ConnectionSSLMode = ConnectionSSLMode.Required;
         var pool = GetPool( creationInfo );
         var selectResult = await pool.UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
      }
   }
}
