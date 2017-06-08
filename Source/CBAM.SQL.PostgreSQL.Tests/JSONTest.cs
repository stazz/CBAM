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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CBAM.SQL.PostgreSQL.Tests
{
   [TestClass]
   public class JSONTest : AbstractPostgreSQLTest
   {
      protected interface SimpleJSONDataGenerator
      {
         IEnumerable<(String JSONSpec, JToken Object)> GenerateJSONValues();
      }

      protected class DefaultJSONDataGenerator : SimpleJSONDataGenerator
      {
         public IEnumerable<(String JSONSpec, JToken Object)> GenerateJSONValues()
         {
            const String SPECIAL_CHAR_STRING = "Special character: \uD83D\uDCA9";

            yield return ("\"\"", new JValue( "" ));
            yield return ("{}", new JObject());
            yield return ("{\"a\" : \"b\"  }", new JObject( new JProperty( "a", new JValue( "b" ) ) ));
            yield return ("{\"a \\\" quote\" : \"b\"  }", new JObject( new JProperty( "a \" quote", new JValue( "b" ) ) ));
            yield return ("\"" + SPECIAL_CHAR_STRING + "\"", new JValue( SPECIAL_CHAR_STRING ));
            yield return ("\"Control character\\n string\"", new JValue( "Control character\n string" ));
            yield return ("\"Unicode escape \\uD83D\\uDCA9\"", new JValue( "Unicode escape \uD83D\uDCA9" ));
            // The problem is that if we have oprhaned surrogates, we must build the string using character array, otherwise strings will differ
            // However, if UTF-8 byte array is made from those two different strings, that array will be the same for both strings!
            //yield return ("\"Orphaned surrogate \\uD83D and the rest\"", new JValue( "Orphaned surrogate \uD83D and the rest" ));
            // Some samples from PostgreSQL JSON type page: https://www.postgresql.org/docs/current/static/datatype-json.html
            yield return ("5", new JValue( 5 ));
            yield return ("[1, 2, \"foo\", null]", new JArray( new JValue( 1 ), new JValue( 2 ), new JValue( "foo" ), JValue.CreateNull() ));
            yield return ("{\"bar\": \"baz\", \"balance\": 7.77, \"active\": false}", new JObject( new JProperty( "bar", new JValue( "baz" ) ), new JProperty( "balance", new JValue( 7.77 ) ), new JProperty( "active", new JValue( false ) ) ));
            yield return ("{\"foo\": [true, \"bar\"], \"tags\": {\"a\": 123, \"b\": null}}", new JObject( new JProperty( "foo", new JArray( new JValue( true ), new JValue( "bar" ) ) ), new JProperty( "tags", new JObject( new JProperty( "a", new JValue( 123 ) ), new JProperty( "b", JValue.CreateNull() ) ) ) ));
         }
      }

      [DataTestMethod,
         DataRow(
         DEFAULT_CONFIG_FILE_LOCATION,
         typeof( DefaultJSONDataGenerator )
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestJSONReading(
         String connectionConfigFileLocation,
         Type jsonGenerator
         )
      {
         var generator = (SimpleJSONDataGenerator) Activator.CreateInstance( jsonGenerator );
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseConnectionPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         pool.EnableJSONSupport();
         await pool.UseConnectionAsync( async conn =>
         {
            foreach ( var jsonInfo in generator.GenerateJSONValues() )
            {
               ValidateJSON( jsonInfo.Object, await conn.GetFirstOrDefaultAsync<JToken>( "SELECT '" + jsonInfo.JSONSpec + "'::json" ) );
               ValidateJSON( jsonInfo.Object, await conn.GetFirstOrDefaultAsync<JToken>( "SELECT '" + jsonInfo.JSONSpec + "'::jsonb" ) );
            }
         } );

      }

      [DataTestMethod,
         DataRow(
         DEFAULT_CONFIG_FILE_LOCATION,
         typeof( DefaultJSONDataGenerator )
         ),
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestJSONWriting(
         String connectionConfigFileLocation,
         Type jsonGenerator
         )
      {
         var generator = (SimpleJSONDataGenerator) Activator.CreateInstance( jsonGenerator );
         var pool = PgSQLConnectionPoolProvider.Instance.CreateOneTimeUseConnectionPool( GetConnectionCreationInfo( connectionConfigFileLocation ) );
         pool.EnableJSONSupport();
         await pool.UseConnectionAsync( async conn =>
         {
            var stmt = conn.VendorFunctionality.CreateStatementBuilder( "SELECT ?" );
            foreach ( var jsonInfo in generator.GenerateJSONValues() )
            {
               var json = jsonInfo.Object;
               stmt.SetParameterObjectWithType( 0, json, typeof( JToken ) );
               ValidateJSON( json, await conn.GetFirstOrDefaultAsync<JToken>( stmt ) );
            }
         } );
      }

      private static void ValidateJSON( JToken value, JToken valueFromDB )
      {
         Assert.IsFalse( ReferenceEquals( value, valueFromDB ) );
         Assert.AreEqual( value.GetType(), valueFromDB.GetType() );
         Assert.IsTrue( JToken.DeepEquals( value, valueFromDB ) );
      }
   }
}
