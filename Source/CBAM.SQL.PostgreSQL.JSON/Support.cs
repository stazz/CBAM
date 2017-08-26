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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UtilPack.ResourcePooling;

public static partial class E_CBAM
{
   public static void EnableJSONSupport( this AsyncResourcePoolObservable<PgSQLConnection> pool )
   {
      pool.AfterResourceCreationEvent += Pool_AfterConnectionCreationEvent;
   }

   public static void DisableJSONSupport( this AsyncResourcePoolObservable<PgSQLConnection> pool )
   {
      pool.AfterResourceCreationEvent -= Pool_AfterConnectionCreationEvent;
   }

   private static void Pool_AfterConnectionCreationEvent( AfterAsyncResourceCreationEventArgs<PgSQLConnection> e )
   {
      e.AddAwaitable( e.Resource.AddJSONSupportAsync() );
   }

   public static async Task AddJSONSupportAsync( this PgSQLConnection connection )
   {
      // TODO detect if we already added support...
      await connection.TypeRegistry.AddTypeFunctionalitiesAsync(
         ("json", typeof( JToken ), CreateJSONSupport),
         ("jsonb", typeof( JToken ), CreateJSONBSupport)
         );

   }

   private static TypeFunctionalityCreationResult CreateJSONSupport( PgSQLTypeDatabaseData param )
   {
      return new TypeFunctionalityCreationResult( DefaultPgSQLJSONTypeFunctionality.Instance, false );
   }

   private static TypeFunctionalityCreationResult CreateJSONBSupport( PgSQLTypeDatabaseData param )
   {
      return new TypeFunctionalityCreationResult( DefaultPgSQLJSONTypeFunctionality.Instance, true );
   }
}
