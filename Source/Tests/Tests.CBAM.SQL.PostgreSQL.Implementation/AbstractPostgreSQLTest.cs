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
using CBAM.SQL;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResourcePooling.Async.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL.PostgreSQL.Tests
{
   public class AbstractPostgreSQLTest
   {
      private const String COMMON_PREFIX = "../../../../../TestConfigFiles/";
      public const String DEFAULT_CONFIG_FILE_LOCATION = COMMON_PREFIX + "test_config.json";
      public const String DEFAULT_CONFIG_FILE_LOCATION_SSL = COMMON_PREFIX + "test_config_ssl.json";
      public const String SCRAM_CONFIG_FILE_LOCATION = COMMON_PREFIX + "test_config_scram.json";
      public const String SCRAM_DIGEST_CONFIG_FILE_LOCATION = COMMON_PREFIX + "test_config_scram_digest.json";

      public const Int32 DEFAULT_TIMEOUT = 10000;

      protected static PgSQLConnectionCreationInfoData GetConnectionCreationInfoData(
         String connectionConfigFileLocation
         )
      {
         return new ConfigurationBuilder()
            .AddJsonFile( System.IO.Path.GetFullPath( connectionConfigFileLocation ) )
            .Build()
            .Get<PgSQLConnectionCreationInfoData>();
      }

      protected static PgSQLConnectionCreationInfo GetConnectionCreationInfo(
         String connectionConfigFileLocation
         )
      {
         return new PgSQLConnectionCreationInfo( GetConnectionCreationInfoData( connectionConfigFileLocation ) );
      }


      protected static AsyncResourcePoolObservable<PgSQLConnection> GetPool( PgSQLConnectionCreationInfo info )
      {
         return PgSQLConnectionPoolProvider.Factory.BindCreationParameters( info ).CreateOneTimeUseResourcePool();
      }

      protected interface SimpleArrayDataGenerator
      {
         IEnumerable<(String ArraySpec, Array Array)> GenerateArrays();
      }

      protected static async Task AssertThatConnectionIsStillUseable( PgSQLConnection connection ) //, AsyncEnumerator<SQLStatementExecutionResult> enumerator )
      {
         //if ( enumerator != null )
         //{
         //   await enumerator.EnumerationEnded();
         //}
         var selectResult = await connection.GetFirstOrDefaultAsync<Int32>( "SELECT 1" );
         Assert.AreEqual( 1, selectResult );
      }

      protected static async Task AssertThatQueryProducesSameResults( PgSQLConnection connection, String query, params Object[] values )
      {
         var queryCount = 0;
         await connection.PrepareStatementForExecution( query ).EnumerateAsync( async item =>
         {
            Assert.AreEqual( values[queryCount++], await ( (SQLDataRow) item ).GetValueAsObjectAsync( 0 ) );
         } );

         Assert.AreEqual( values.Length, queryCount );
      }

      protected static async Task AssertThatQueryProducesSameResults_IgnoreOrder( PgSQLConnection connection, String query, params Object[] values )
      {
         var valuesSet = new HashSet<Object>( values );
         var querySet = new HashSet<Object>();
         var queryCount = 0;
         await connection.PrepareStatementForExecution( query ).EnumerateAsync( async item =>
         {
            querySet.Add( await ( (SQLDataRow) item ).GetValueAsObjectAsync( 0 ) );
            ++queryCount;
         } );

         Assert.AreEqual( values.Length, queryCount );
         Assert.IsTrue( valuesSet.SetEquals( querySet ) );
      }

      protected static async Task TestWithAndWithoutBinaryReceive(
         String connectionConfigFileLocation,
         Func<PgSQLConnection, Task> performTest
         )
      {
         var data = GetConnectionCreationInfoData( connectionConfigFileLocation );
         var enabled = data.CreateCopy();
         enabled.Initialization.Protocol.DisableBinaryProtocolReceive = false;
         var disabled = data.CreateCopy();
         disabled.Initialization.Protocol.DisableBinaryProtocolReceive = true;

         await Task.WhenAll(
            GetPool( new PgSQLConnectionCreationInfo( enabled ) ).UseResourceAsync( performTest ),
            GetPool( new PgSQLConnectionCreationInfo( disabled ) ).UseResourceAsync( performTest )
            );
      }

      protected static async Task TestWithAndWithoutBinarySend(
         String connectionConfigFileLocation,
         Func<PgSQLConnection, Task> performTest
         )
      {
         var data = GetConnectionCreationInfoData( connectionConfigFileLocation );
         var enabled = data.CreateCopy();
         enabled.Initialization.Protocol.DisableBinaryProtocolSend = false;
         var disabled = data.CreateCopy();
         disabled.Initialization.Protocol.DisableBinaryProtocolSend = true;

         await Task.WhenAll(
            GetPool( new PgSQLConnectionCreationInfo( enabled ) ).UseResourceAsync( performTest ),
            GetPool( new PgSQLConnectionCreationInfo( disabled ) ).UseResourceAsync( performTest )
            );
      }

      protected static void ValidateArrays( Array array, Array arrayFromDB )
      {
         var length = array.Length;
         Assert.AreEqual( length, arrayFromDB.Length );
         Assert.AreEqual( array.Rank, arrayFromDB.Rank );
         Assert.AreEqual( array.GetType(), arrayFromDB.GetType() );
         var rank = array.Rank;
         Assert.AreEqual( rank, arrayFromDB.Rank );
         for ( var j = 0; j < rank; ++j )
         {
            Assert.AreEqual( array.GetLowerBound( j ), arrayFromDB.GetLowerBound( j ) );
            Assert.AreEqual( array.GetUpperBound( j ), arrayFromDB.GetUpperBound( j ) );
            Assert.AreEqual( array.GetLength( j ), arrayFromDB.GetLength( j ) );
         }
         var arrayEnum = array.GetEnumerator();
         var dbEnum = arrayFromDB.GetEnumerator();

         while ( arrayEnum.MoveNext() )
         {
            Assert.IsTrue( dbEnum.MoveNext() );
            Assert.AreEqual( arrayEnum.Current, dbEnum.Current );
         }

         Assert.IsFalse( dbEnum.MoveNext() );
      }

      protected sealed class TextArrayGenerator : SimpleArrayDataGenerator
      {
         public IEnumerable<(String ArraySpec, Array Array)> GenerateArrays()
         {
            yield return ("'{}'::_text", Empty<String>.Array);
            yield return ("'{a}'::_text", new String[] { "a" });
            yield return ("'{NULL}'::_text", new String[] { null });
            yield return ("'{{{a,b},{c,d},{e,f}},{{g,h},{i,j},{k,l}}}'::_text", new String[,,] { { { "a", "b" }, { "c", "d" }, { "e", "f" } }, { { "g", "h" }, { "i", "j" }, { "k", "l" } } });
            yield return ("'{\"\"}'::_text", new String[] { "" });
            yield return ("'{\"A quote\\\"\"}'::_text", new String[] { "A quote\"" });
            var loboArray = Array.CreateInstance( typeof( String ), new[] { 2 }, new[] { -1 } );
            loboArray.SetValue( "first", new[] { -1 } );
            loboArray.SetValue( "second", new[] { 0 } );
            yield return ("'[0:1]={first,second}'::_text", loboArray);

            loboArray = Array.CreateInstance( typeof( Int32? ), new[] { 1, 2, 3 }, new[] { 0, -3, 2 } );
            loboArray.SetValue( 1, new[] { 0, -3, 2 } );
            loboArray.SetValue( 2, new[] { 0, -3, 3 } );
            loboArray.SetValue( 3, new[] { 0, -3, 4 } );
            loboArray.SetValue( 4, new[] { 0, -2, 2 } );
            loboArray.SetValue( null, new[] { 0, -2, 3 } );
            loboArray.SetValue( 6, new[] { 0, -2, 4 } );
            // A little bit modified array from Pg documentation example about array input and output syntax
            yield return ("'[1:1][-2:-1][3:5]={{{1,2,3},{4,NULL,6}}}'::int[]", loboArray);
         }
      }
   }
}

public static partial class E_CBAM
{
   public static Task UseResourceAsync<TResource>( this AsyncResourcePool<TResource> pool, Func<TResource, Task> user )
      => pool.UseResourceAsync( user, default );

   public static Task<T> UseResourceAsync<TResource, T>( this AsyncResourcePool<TResource> pool, Func<TResource, Task<T>> user )
      => pool.UseResourceAsync( user, default );

   //public static SQLDataRow GetDataRow( this AsyncEnumerator<SQLStatementExecutionResult> args, Int64? token )
   //{
   //   return (SQLDataRow) args.OneTimeRetrieve( token.Value );
   //}
}