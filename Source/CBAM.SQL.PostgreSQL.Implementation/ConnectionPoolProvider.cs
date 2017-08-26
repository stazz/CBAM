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
using CBAM.SQL.Implementation;
using CBAM.SQL.PostgreSQL.Implementation;
using System;
using System.Collections.Generic;
using System.Text;
using UtilPack;
using UtilPack.ResourcePooling;

using TDefaultConfiguration = CBAM.SQL.PostgreSQL.
#if NETSTANDARD1_0
PgSQLConnectionCreationInfoForReadyMadeStreams
#else
PgSQLConnectionCreationInfo
#endif
   ;
using TDefaultConfigurationData = CBAM.SQL.PostgreSQL.
#if NETSTANDARD1_0
PgSQLConnectionCreationInfoForReadyMadeStreams
#else
PgSQLConnectionCreationInfoData
#endif
   ;

namespace CBAM.SQL.PostgreSQL
{
   public sealed class PgSQLConnectionPoolProvider : ResourcePoolProvider<PgSQLConnection>
   {
      private static readonly IEncodingInfo Encoding;

      static PgSQLConnectionPoolProvider()
      {
         Encoding = new UTF8EncodingInfo();
         Instance = new PgSQLConnectionPoolProvider();
      }

      public static PgSQLConnectionPoolProvider Instance { get; }


      public Type DefaultTypeForCreationParameter => typeof( TDefaultConfigurationData );

      AsyncResourcePoolObservable<PgSQLConnection> ResourcePoolProvider<PgSQLConnection>.CreateOneTimeUseResourcePool( Object creationParameters )
      {
         return this.CreateOneTimeUseResourcePool( CheckCreationParameters( creationParameters ) );
      }

      AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> ResourcePoolProvider<PgSQLConnection>.CreateTimeoutingResourcePool( Object creationParameters )
      {
         return this.CreateTimeoutingResourcePool( CheckCreationParameters( creationParameters ) );
      }
#if !NETSTANDARD1_0
      public AsyncResourcePoolObservable<PgSQLConnection> CreateOneTimeUseResourcePool(
         PgSQLConnectionCreationInfo connectionConfig
         )
      {
         return new OneTimeUseAsyncResourcePool<PgSQLConnectionImpl, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfo>(
            new PgSQLConnectionFactory( Encoding, connectionConfig?.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? false ),
            connectionConfig,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      public AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> CreateTimeoutingResourcePool(
         PgSQLConnectionCreationInfo connectionConfig
         )
      {
         return new CachingAsyncResourcePoolWithTimeout<PgSQLConnectionImpl, PgSQLConnectionCreationInfo>(
            new PgSQLConnectionFactory( Encoding, connectionConfig?.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? false ),
            connectionConfig
            );
      }
#endif

      public AsyncResourcePoolObservable<PgSQLConnection> CreateOneTimeUseResourcePool(
         PgSQLConnectionCreationInfoForReadyMadeStreams connectionConfig
         )
      {
         return new OneTimeUseAsyncResourcePool<PgSQLConnectionImpl, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfoForReadyMadeStreams>(
            new PgSQLConnectionFactoryForReadyMadeStreams( Encoding, connectionConfig?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? false ),
            connectionConfig,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      public AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> CreateTimeoutingResourcePool(
         PgSQLConnectionCreationInfoForReadyMadeStreams connectionConfig
         )
      {
         return new CachingAsyncResourcePoolWithTimeout<PgSQLConnectionImpl, PgSQLConnectionCreationInfoForReadyMadeStreams>(
            new PgSQLConnectionFactoryForReadyMadeStreams( Encoding, connectionConfig?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? false ),
            connectionConfig
            );
      }

      private static TDefaultConfiguration CheckCreationParameters( Object creationParameters )
      {
         if ( !( creationParameters is TDefaultConfigurationData creationData ) )
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( TDefaultConfigurationData ).FullName}." );
         }

         return
#if NETSTANDARD1_0
            creationData
#else
            new TDefaultConfiguration( creationData )
#endif
            ;
      }


   }
}
