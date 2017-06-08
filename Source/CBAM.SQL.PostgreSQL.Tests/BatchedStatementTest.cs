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
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CBAM.SQL.PostgreSQL.Tests
{
   [TestClass]
   public class BatchedStatementTest : AbstractPostgreSQLTest
   {
      [
         DataTestMethod,
         DataRow( DEFAULT_CONFIG_FILE_LOCATION ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestBatchedStatement( String connectionConfigFileLocation )
      {
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseConnectionPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         const String FIRST = "first";
         const String SECOND = "second";
         const String THIRD = "third";

         //const Int32 FIRST = 1;
         //const Int32 SECOND = 2;
         //const Int32 THIRD = 3;

         await pool.UseConnectionAsync( async conn =>
         {
            await conn.ExecuteNonQueryAsync( "CREATE TEMPORARY TABLE batch_test( id SERIAL, value TEXT, PRIMARY KEY (id) )" );

            var batchStmt = conn.CreateStatementBuilder( "INSERT INTO batch_test(value) VALUES ( ? )" );

            batchStmt.SetParameterString( 0, FIRST );
            //batchStmt.SetParameterInt32( 0, FIRST );
            //batchStmt.SetParameterString( 1, FIRST );
            batchStmt.AddBatch();

            batchStmt.SetParameterString( 0, SECOND );
            //batchStmt.SetParameterInt32( 0, SECOND );
            //batchStmt.SetParameterString( 1, SECOND );
            batchStmt.AddBatch();

            batchStmt.SetParameterString( 0, THIRD );
            //batchStmt.SetParameterInt32( 0, THIRD );
            //batchStmt.SetParameterString( 1, THIRD );
            batchStmt.AddBatch();

            await conn.ExecuteNonQueryAsync( batchStmt );

            await AssertThatQueryProducesSameResults_IgnoreOrder( conn, "SELECT value FROM batch_test", FIRST, SECOND, THIRD );
         } );
      }

      [
         DataTestMethod,
         DataRow( DEFAULT_CONFIG_FILE_LOCATION ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestBatchedStatementWithoutParameters( String connectionConfigFileLocation )
      {
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseConnectionPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );

         await pool.UseConnectionAsync( async conn =>
         {
            await conn.ExecuteNonQueryAsync( "CREATE TEMPORARY TABLE batch_test( id SERIAL, PRIMARY KEY (id) )" );

            var batchStmt = conn.CreateStatementBuilder( "INSERT INTO batch_test DEFAULT VALUES" );
            batchStmt.AddBatch();
            batchStmt.AddBatch();
            batchStmt.AddBatch();

            await conn.ExecuteNonQueryAsync( batchStmt );

            await AssertThatQueryProducesSameResults_IgnoreOrder( conn, "SELECT id FROM batch_test", 1, 2, 3 );
         } );
      }
   }
}
