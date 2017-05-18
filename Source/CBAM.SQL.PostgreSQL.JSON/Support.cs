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
using CBAM.Abstractions;
using CBAM.SQL;
using CBAM.SQL.PostgreSQL;
using CBAM.SQL.PostgreSQL.JSON;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

public static partial class E_CBAM
{
   public static void EnableJSONSupport( this ConnectionPoolObservable<PgSQLConnection> pool )
   {
      pool.AfterConnectionCreationEvent += Pool_AfterConnectionCreationEvent;
   }

   public static void DisableJSONSupport( this ConnectionPoolObservable<PgSQLConnection> pool )
   {
      pool.AfterConnectionCreationEvent -= Pool_AfterConnectionCreationEvent;
   }

   private static void Pool_AfterConnectionCreationEvent( Object sender, CBAM.Abstractions.AfterConnectionCreationEventArgs<PgSQLConnection> e )
   {
      e.Awaitables.Add( e.Connection.AddJSONSupportAsync() );
   }

   public static async Task AddJSONSupportAsync( this PgSQLConnection connection )
   {
      // TODO detect if we already added support...
      await connection.TypeRegistry.AddTypeFunctionalitiesAsync(
         ("json", CreateJSONSupport),
         ("jsonb", CreateJSONBSupport)
         );

   }

   private static (PgSQLTypeFunctionality UnboundFunctionality, Boolean IsDefaultForCLRType) CreateJSONSupport( (TypeRegistry TypeRegistry, PgSQLTypeDatabaseData DBTypeInfo) param )
   {
      return (DefaultPgSQLJSONTypeFunctionality.Instance, false);
   }

   private static (PgSQLTypeFunctionality UnboundFunctionality, Boolean IsDefaultForCLRType) CreateJSONBSupport( (TypeRegistry TypeRegistry, PgSQLTypeDatabaseData DBTypeInfo) param )
   {
      return (DefaultPgSQLJSONTypeFunctionality.Instance, true);
   }
}
