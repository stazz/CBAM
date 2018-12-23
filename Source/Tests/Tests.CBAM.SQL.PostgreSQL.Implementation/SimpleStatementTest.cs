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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace Tests.CBAM.SQL.PostgreSQL.Implementation
{
   [TestClass]
   public class SimpleStatementTest : AbstractPostgreSQLTest
   {

      [DataTestMethod, DataRow( PgSQLConfigurationKind.Normal ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestSelect1( PgSQLConfigurationKind configurationKind )
      {
         var pool = GetPool( GetConnectionCreationInfo( configurationKind ) );
         var selectResult = await pool.UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
      }

      [DataTestMethod, DataRow( PgSQLConfigurationKind.Normal ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestSelectMultipleValues( PgSQLConfigurationKind configurationKind )
      {
         var first = 1;
         var second = 2;
         var third = 3;
         var pool = GetPool( GetConnectionCreationInfo( configurationKind ) );
         var integers = await pool.UseResourceAsync( async conn =>
            {
               return await conn.PrepareStatementForExecution( $"SELECT * FROM( VALUES( {first} ), ( {second} ), ( {third} ) ) AS tmp" )
                  .IncludeDataRowsOnly()
                  .Select( async row => await row.GetValueAsync<Int32>( 0 ) )
                  .ToArrayAsync();
            }
         );
         Assert.IsTrue( ArrayEqualityComparer<Int32>.ArrayEquality( new[] { first, second, third }, integers ) );
      }

      [DataTestMethod, DataRow( PgSQLConfigurationKind.Normal ), Timeout( DEFAULT_TIMEOUT )]
      public async Task TestNotReadingAllColumns( PgSQLConfigurationKind configurationKind )
      {
         var pool = GetPool( GetConnectionCreationInfo( configurationKind ) );
         var rowsSeen = 0;
         await pool.UseResourceAsync( async conn =>
         {
            await conn.PrepareStatementForExecution( "SELECT * FROM( VALUES( 1, 2 ), (3, 4), (5, 6) ) AS tmp" )
            .IncludeDataRowsOnly()
            .EnumerateAsync( async row =>
               {
                  switch ( Interlocked.Increment( ref rowsSeen ) )
                  {
                     case 1:
                        // First read is partial read
                        Assert.AreEqual( 1, await row.GetValueAsync<Int32>( 0 ) );
                        break;
                     case 2:
                        // Second read just ignores columns
                        break;
                     case 3:
                        // Third read reads in opposite order
                        Assert.AreEqual( 6, await row.GetValueAsync<Int32>( 1 ) );
                        Assert.AreEqual( 5, await row.GetValueAsync<Int32>( 0 ) );
                        break;
                  }
               } );
         } );

         Assert.AreEqual( 3, rowsSeen );
      }

      [DataTestMethod,
         DataRow(
         PgSQLConfigurationKind.Normal,
         typeof( TextArrayGenerator )
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestArrays(
         PgSQLConfigurationKind configurationKind,
         Type arrayGenerator
         )
      {
         var generator = (SimpleArrayDataGenerator) Activator.CreateInstance( arrayGenerator );
         var pool = GetPool( GetConnectionCreationInfo( configurationKind ) );
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
         PgSQLConfigurationKind.Normal
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestMultipleSimpleStatements(
         PgSQLConfigurationKind configurationKind
         )
      {
         const Int32 FIRST = 1;
         const Int32 SECOND = 2;
         var integers = await GetPool( GetConnectionCreationInfo( configurationKind ) )
            .UseResourceAsync( async conn =>
         {
            return await conn.PrepareStatementForExecution( "SELECT " + FIRST + "; SELECT " + SECOND + ";" )
               .IncludeDataRowsOnly()
               .Select( async row => await row.GetValueAsync<Int32>( 0 ) )
               .ToArrayAsync();
         } );
         Assert.IsTrue( ArrayEqualityComparer<Int32>.ArrayEquality( new[] { FIRST, SECOND }, integers ) );
      }

      [DataTestMethod,
         DataRow(
         PgSQLConfigurationKind.Normal
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestMultipleHeterogenousSimpleStatements(
         PgSQLConfigurationKind configurationKind
         )
      {
         const Int32 TEST_INT = 1;
         const String TEST_STRING = "testString";
         var objects = await GetPool( GetConnectionCreationInfo( configurationKind ) )
            .UseResourceAsync( async conn =>
         {
            return await conn.PrepareStatementForExecution( "SELECT " + TEST_INT + "; SELECT '" + TEST_STRING + "';" )
               .IncludeDataRowsOnly()
               .Select( async row => await row.GetValueAsObjectAsync( 0 ) )
               .ToArrayAsync();
         } );
         Assert.IsTrue( ArrayEqualityComparer<Object>.ArrayEquality( new Object[] { TEST_INT, TEST_STRING }, objects ) );
      }
   }
}
