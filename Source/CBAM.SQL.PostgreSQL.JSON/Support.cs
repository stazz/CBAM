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

namespace CBAM.SQL.PostgreSQL.JSON
{
   /// <summary>
   /// This class contains extension methods for types defined in other assemblies.
   /// </summary>
   public static class CBAMExtensions
   {
      /// <summary>
      /// This method will add support for <c>json</c> and <c>jsonb</c> PostgreSQL types for all connections instantiated by this <see cref="AsyncResourcePoolObservable{TResource}"/>.
      /// </summary>
      /// <param name="pool">This <see cref="AsyncResourcePoolObservable{TResource}"/>.</param>
      /// <exception cref="NullReferenceException">If this <see cref="AsyncResourcePoolObservable{TResource}"/> is <c>null</c>.</exception>
      /// <remarks>
      /// The "support" here means that the value returned by <see cref="UtilPack.TabularData.AsyncDataColumn.TryGetValueAsync"/> method is directly of type <see cref="JObject"/>, <see cref="JArray"/>, and <see cref="JValue"/>, without any further need to parse etc process the value.
      /// </remarks>
      public static void EnableJSONSupport( this AsyncResourcePoolObservable<PgSQLConnection> pool )
      {
         pool.AfterResourceCreationEvent += Pool_AfterConnectionCreationEvent;
      }

      /// <summary>
      /// This method will remove support for <c>json</c> and <c>jsonb</c> PostgreSQL types for all connections instantied by this <see cref="AsyncResourcePoolObservable{TResource}"/>.
      /// </summary>
      /// <param name="pool">This <see cref="AsyncResourcePoolObservable{TResource}"/>.</param>
      /// <exception cref="NullReferenceException">If this <see cref="AsyncResourcePoolObservable{TResource}"/> is <c>null</c>.</exception>
      /// <remarks>
      /// The "support" here means that the value returned by <see cref="UtilPack.TabularData.AsyncDataColumn.TryGetValueAsync"/> method is directly of type <see cref="JObject"/>, <see cref="JArray"/>, and <see cref="JValue"/>, without any further need to parse etc process the value.
      /// If this <paramref name="pool"/> has cached connections, the support will not be removed from them.
      /// </remarks>
      public static void DisableJSONSupport( this AsyncResourcePoolObservable<PgSQLConnection> pool )
      {
         pool.AfterResourceCreationEvent -= Pool_AfterConnectionCreationEvent;
      }

      private static void Pool_AfterConnectionCreationEvent( AfterAsyncResourceCreationEventArgs<PgSQLConnection> e )
      {
         e.AddAwaitable( e.Resource.AddJSONSupportAsync() );
      }

      /// <summary>
      /// This method will add support for <c>json</c> and <c>jsonb</c> PostgreSQL types for this specific <see cref="PgSQLConnection"/>.
      /// </summary>
      /// <param name="connection">This <see cref="PgSQLConnection"/>.</param>
      /// <returns>A task which on completion has added support for <c>json</c> and <c>jsonb</c> PostgreSQL types for this <see cref="PgSQLConnection"/>.</returns>
      /// <remarks>
      /// The "support" here means that the value returned by <see cref="UtilPack.TabularData.AsyncDataColumn.TryGetValueAsync"/> method is directly of type <see cref="JObject"/>, <see cref="JArray"/>, and <see cref="JValue"/>, without any further need to parse etc process the value.
      /// </remarks>
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
}