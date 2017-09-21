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
         var pool = GetPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         var selectResult = await pool.UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
      }

      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestSelectMultipleValues( String connectionConfigFileLocation )
      {
         var first = 1;
         var second = 2;
         var third = 3;
         var pool = GetPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );

         var tuple = await pool.UseResourceAsync( async conn =>
         {
            var iArgs = conn.PrepareStatementForExecution( $"SELECT * FROM( VALUES( {first} ), ( {second} ), ( {third} ) ) AS tmp" );
            Int64? tkn;
            Assert.IsTrue( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            var seenFirst = await iArgs.GetDataRow( tkn ).GetValueAsync<Int32>( 0 );

            Assert.IsTrue( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            var seenSecond = await iArgs.GetDataRow( tkn ).GetValueAsync<Int32>( 0 );

            Assert.IsTrue( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            var seenThird = await iArgs.GetDataRow( tkn ).GetValueAsync<Int32>( 0 );

            Assert.IsFalse( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            await iArgs.EnumerationEnded();
            return (seenFirst, seenSecond, seenThird);
         } );

         Assert.AreEqual( first, tuple.Item1 );
         Assert.AreEqual( second, tuple.Item2 );
         Assert.AreEqual( third, tuple.Item3 );
      }

      [DataTestMethod, DataRow( DEFAULT_CONFIG_FILE_LOCATION ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestNotReadingAllColumns( String connectionConfigFileLocation )
      {
         var pool = GetPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         await pool.UseResourceAsync( async conn =>
         {
            var iArgs = conn.PrepareStatementForExecution( "SELECT * FROM( VALUES( 1, 2 ), (3, 4), (5, 6) ) AS tmp" );
            Int64? tkn;
            // First read is partial read
            Assert.IsTrue( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            Assert.AreEqual( 1, await iArgs.GetDataRow( tkn ).GetValueAsync<Int32>( 0 ) );

            // Second read just ignores columns
            Assert.IsTrue( ( tkn = await iArgs.MoveNextAsync() ).HasValue );

            // Third read reads in opposite order
            Assert.IsTrue( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            Assert.AreEqual( 6, await iArgs.GetDataRow( tkn ).GetValueAsync<Int32>( 1 ) );
            Assert.AreEqual( 5, await iArgs.GetDataRow( tkn ).GetValueAsync<Int32>( 0 ) );

            Assert.IsFalse( ( tkn = await iArgs.MoveNextAsync() ).HasValue );
            await iArgs.EnumerationEnded();
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
         var pool = GetPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
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
         const Int32 FIRST = 1;
         const Int32 SECOND = 2;
         await GetPool( GetConnectionCreationInfo( connectionConfigFileLocation ) ).UseResourceAsync( async conn =>
         {
            var enumerator = conn.PrepareStatementForExecution( "SELECT " + FIRST + "; SELECT " + SECOND + ";" );
            Int64? tkn;
            Assert.IsTrue( ( tkn = await enumerator.MoveNextAsync() ).HasValue );
            Assert.AreEqual( FIRST, await enumerator.GetDataRow( tkn ).GetValueAsync<Int32>( 0 ) );
            Assert.IsTrue( ( tkn = await enumerator.MoveNextAsync() ).HasValue );
            Assert.AreEqual( SECOND, await enumerator.GetDataRow( tkn ).GetValueAsync<Int32>( 0 ) );
         } );
      }

      [DataTestMethod,
         DataRow(
         DEFAULT_CONFIG_FILE_LOCATION
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestMultipleHeterogenousSimpleStatements(
         String connectionConfigFileLocation
         )
      {
         const Int32 TEST_INT = 1;
         const String TEST_STRING = "testString";
         await GetPool( GetConnectionCreationInfo( connectionConfigFileLocation ) ).UseResourceAsync( async conn =>
         {
            var enumerator = conn.PrepareStatementForExecution( "SELECT " + TEST_INT + "; SELECT '" + TEST_STRING + "';" );
            Int64? tkn;
            Assert.IsTrue( ( tkn = await enumerator.MoveNextAsync() ).HasValue );
            Assert.AreEqual( 1, await enumerator.GetDataRow( tkn ).GetValueAsync<Int32>( 0 ) );
            Assert.IsTrue( ( tkn = await enumerator.MoveNextAsync() ).HasValue );
            Assert.AreEqual( TEST_STRING, await enumerator.GetDataRow( tkn ).GetValueAsync<String>( 0 ) );
         } );
      }
   }
}
