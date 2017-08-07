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
using System;
using System.Collections.Generic;
using System.Text;
using CBAM.Abstractions.Implementation;
using UtilPack;
using UtilPack.ResourcePooling;

namespace CBAM.SQL.Implementation
{
   public class OneTimeUseSQLConnectionPool<TConnection, TConnectionInstance, TConnectionCreationParams> : OneTimeUseAsyncResourcePool<TConnection, TConnectionInstance, TConnectionCreationParams>, SQLConnectionPool<TConnection>
      where TConnection : class, SQLConnection
      where TConnectionCreationParams : class
   {
      public OneTimeUseSQLConnectionPool(
         ResourceFactory<TConnection, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters,
         Func<TConnectionInstance, ResourceAcquireInfo<TConnection>> connectionExtractor,
         Func<ResourceAcquireInfo<TConnection>, TConnectionInstance> instanceCreator
         ) : base( factory, factoryParameters, connectionExtractor, instanceCreator )
      {
      }
   }

   public class CachingSQLConnectionPool<TConnection, TConnectionInstance, TConnectionCreationParams> : CachingAsyncResourcePool<TConnection, TConnectionInstance, TConnectionCreationParams>, SQLConnectionPool<TConnection>
      where TConnection : class, SQLConnection
      where TConnectionInstance : class, InstanceWithNextInfo<TConnectionInstance>
      where TConnectionCreationParams : class
   {

      public CachingSQLConnectionPool(
         ResourceFactory<TConnection, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters,
         Func<TConnectionInstance, ResourceAcquireInfo<TConnection>> connectionExtractor,
         Func<ResourceAcquireInfo<TConnection>, TConnectionInstance> instanceCreator
         ) : base( factory, factoryParameters, connectionExtractor, instanceCreator )
      {

      }

   }

   public class CachingSQLConnectionPoolWithTimeout<TConnection, TConnectionCreationParams> : CachingAsyncResourcePoolWithTimeout<TConnection, TConnectionCreationParams>, SQLConnectionPool<TConnection, TimeSpan>
      where TConnection : class, SQLConnection
      where TConnectionCreationParams : class
   {

      public CachingSQLConnectionPoolWithTimeout(
         ResourceFactory<TConnection, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters
         )
         : base( factory, factoryParameters )
      {
      }
   }

   public abstract class SQLConnectionFactory<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality> : DefaultConnectionFactory<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, String, StatementBuilder, StatementBuilderInformation, SQLStatementExecutionResult>
      where TConnection : ConnectionImpl<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor, TConnectionFunctionality>, SQLConnection
      where TVendor : SQLConnectionVendorFunctionality
      where TConnectionFunctionality : DefaultConnectionFunctionality<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor>
   {

   }
}
