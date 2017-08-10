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
using System.Threading.Tasks;
using UtilPack;
using System.Reflection;

namespace CBAM.SQL.PostgreSQL.Tests
{
   [TestClass]
   public class SimpleStatementTest : AbstractPostgreSQLTest
   {

      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestSelect1( String connectionConfigFileLocation )
      {
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseResourcePool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         var selectResult = await pool.UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
      }

      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestSelectMultipleValues( String connectionConfigFileLocation )
      {
         var first = 1;
         var second = 2;
         var third = 3;
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseResourcePool( GetConnectionCreationInfo( connectionConfigFileLocation ) );

         var tuple = await pool.UseResourceAsync( async conn =>
         {
            var iArgs = conn.PrepareStatementForExecution( $"SELECT * FROM( VALUES( {first} ), ( {second} ), ( {third} ) ) AS tmp" );
            Assert.IsTrue( await iArgs.MoveNextAsync() );
            var seenFirst = await iArgs.GetDataRow().GetValueAsync<Int32>( 0 );

            Assert.IsTrue( await iArgs.MoveNextAsync() );
            var seenSecond = await iArgs.GetDataRow().GetValueAsync<Int32>( 0 );

            Assert.IsTrue( await iArgs.MoveNextAsync() );
            var seenThird = await iArgs.GetDataRow().GetValueAsync<Int32>( 0 );

            Assert.IsFalse( await iArgs.MoveNextAsync() );
            return (seenFirst, seenSecond, seenThird);
         } );

         Assert.AreEqual( first, tuple.Item1 );
         Assert.AreEqual( second, tuple.Item2 );
         Assert.AreEqual( third, tuple.Item3 );
      }

      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestNotReadingAllColumns( String connectionConfigFileLocation )
      {
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseResourcePool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         await pool.UseResourceAsync( async conn =>
         {
            var iArgs = conn.PrepareStatementForExecution( "SELECT * FROM( VALUES( 1, 2 ), (3, 4), (5, 6) ) AS tmp" );
            // First read is partial read
            Assert.IsTrue( await iArgs.MoveNextAsync() );
            Assert.AreEqual( 1, await iArgs.GetDataRow().GetValueAsync<Int32>( 0 ) );

            // Second read just ignores columns
            Assert.IsTrue( await iArgs.MoveNextAsync() );

            // Third read reads in opposite order
            Assert.IsTrue( await iArgs.MoveNextAsync() );
            Assert.AreEqual( 6, await iArgs.GetDataRow().GetValueAsync<Int32>( 1 ) );
            Assert.AreEqual( 5, await iArgs.GetDataRow().GetValueAsync<Int32>( 0 ) );

            Assert.IsFalse( await iArgs.MoveNextAsync() );
         } );
      }

      [DataTestMethod,
         DataRow(
         DEFAULT_CONFIG_FILE_LOCATION,
         typeof( TextArrayGenerator )
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestArrays(
         String connectionConfigFileLocation,
         Type arrayGenerator
         )
      {
         var generator = (SimpleArrayDataGenerator) Activator.CreateInstance( arrayGenerator );
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseResourcePool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         await pool.UseResourceAsync( async conn =>
         {
            foreach ( var arrayInfo in generator.GenerateArrays() )
            {
               ValidateArrays( arrayInfo.Array, await conn.GetFirstOrDefaultAsync<Array>( "SELECT " + arrayInfo.ArraySpec + " AS test_column" ) );
            }
         } );
      }

      [DataTestMethod,
         DataRow(
         DEFAULT_CONFIG_FILE_LOCATION
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestMultipleSimpleStatements(
         String connectionConfigFileLocation
         )
      {
         await PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseResourcePool( GetConnectionCreationInfo( connectionConfigFileLocation ) ).UseResourceAsync( async conn =>
         {
            var enumerator = conn.PrepareStatementForExecution( "SELECT 1; SELECT 2;" );
            Assert.IsTrue( await enumerator.MoveNextAsync() );
            Assert.AreEqual( await enumerator.GetDataRow().GetValueAsync<Int32>( 0 ), 1 );
            Assert.IsTrue( await enumerator.MoveNextAsync() );
            Assert.AreEqual( await enumerator.GetDataRow().GetValueAsync<Int32>( 0 ), 2 );
         } );
      }


   }
}
