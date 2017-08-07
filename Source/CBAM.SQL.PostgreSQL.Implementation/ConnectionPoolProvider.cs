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
   public sealed class PgSQLConnectionPoolProvider : ConnectionPoolProvider<PgSQLConnection>
   {
#if !NETSTANDARD1_0
      private static readonly PgSQLConnectionFactory _Factory;
#endif
      private static readonly PgSQLConnectionFactoryForReadyMadeStreams _FactoryForReadyMadeStreams;

      static PgSQLConnectionPoolProvider()
      {
         //VendorFunctionality = new PgSQLConnectionVendorFunctionalityImpl();
#if !NETSTANDARD1_0
         _Factory = new PgSQLConnectionFactory();
#endif
         _FactoryForReadyMadeStreams = new PgSQLConnectionFactoryForReadyMadeStreams();

         Instance = new PgSQLConnectionPoolProvider();
      }

      public static PgSQLConnectionPoolProvider Instance { get; }
      //public static PgSQLConnectionVendorFunctionality VendorFunctionality { get; }
      //      public static CBAM.Abstractions.Implementation.ConnectionFactory<PgSQLConnection, PgSQLConnectionVendorFunctionality,  FactoryFunctionality =>
      //#if NETSTANDARD1_0
      //         _FactoryForReadyMadeStreams
      //#else
      //         _FactoryFunctionality
      //#endif
      //         ;

      // We leave constructor as public so that this class could be instantiated by dynamic loading (e.g. CBAM.Abstractions.MSBuild project)


      public Type DefaultTypeForCreationParameter => typeof( TDefaultConfigurationData );

      AsyncResourcePoolObservable<PgSQLConnection> ConnectionPoolProvider<PgSQLConnection>.CreateOneTimeUseConnectionPool( Object creationParameters )
      {
         return this.CreateOneTimeUseConnectionPool( CheckCreationParameters( creationParameters ) );
      }

      AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> ConnectionPoolProvider<PgSQLConnection>.CreateTimeoutingConnectionPool( Object creationParameters )
      {
         return this.CreateTimeoutingConnectionPool( CheckCreationParameters( creationParameters ) );
      }
#if !NETSTANDARD1_0
      public SQLConnectionPool<PgSQLConnection> CreateOneTimeUseConnectionPool(
         PgSQLConnectionCreationInfo connectionConfig
         )
      {
         return new OneTimeUseSQLConnectionPool<PgSQLConnectionImpl, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfo>(
            _Factory,
            connectionConfig,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      public SQLConnectionPool<PgSQLConnection, TimeSpan> CreateTimeoutingConnectionPool(
         PgSQLConnectionCreationInfo connectionConfig
         )
      {
         return new CachingSQLConnectionPoolWithTimeout<PgSQLConnectionImpl, PgSQLConnectionCreationInfo>(
            _Factory,
            connectionConfig
            );
      }
#endif

      public SQLConnectionPool<PgSQLConnection> CreateOneTimeUseConnectionPool(
         PgSQLConnectionCreationInfoForReadyMadeStreams connectionConfig
         )
      {
         return new OneTimeUseSQLConnectionPool<PgSQLConnectionImpl, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfoForReadyMadeStreams>(
            _FactoryForReadyMadeStreams,
            connectionConfig,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      public SQLConnectionPool<PgSQLConnection, TimeSpan> CreateTimeoutingConnectionPool(
         PgSQLConnectionCreationInfoForReadyMadeStreams connectionConfig
         )
      {
         return new CachingSQLConnectionPoolWithTimeout<PgSQLConnectionImpl, PgSQLConnectionCreationInfoForReadyMadeStreams>(
            _FactoryForReadyMadeStreams,
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
