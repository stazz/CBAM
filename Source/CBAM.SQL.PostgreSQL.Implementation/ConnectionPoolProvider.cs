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
      private static readonly PgSQLConnectionVendorFunctionalityImpl _VendorFunctionality;
#endif
      private static readonly PgSQLConnectionVendorFunctionalityForReadyMadeStreams _VendorFunctionalityForReadyMadeStreams;

      static PgSQLConnectionPoolProvider()
      {
#if !NETSTANDARD1_0
         _VendorFunctionality = new PgSQLConnectionVendorFunctionalityImpl();
#endif
         _VendorFunctionalityForReadyMadeStreams = new PgSQLConnectionVendorFunctionalityForReadyMadeStreams();

         Instance = new PgSQLConnectionPoolProvider();
      }

      public static PgSQLConnectionPoolProvider Instance { get; }
      public static PgSQLConnectionVendorFunctionality VendorFunctionality =>
#if NETSTANDARD1_0
         _VendorFunctionalityForReadyMadeStreams
#else
         _VendorFunctionality
#endif
         ;

      // We leave constructor as public so that this class could be instantiated by dynamic loading (e.g. CBAM.Abstractions.MSBuild project)


      public Type DefaultTypeForCreationParameter => typeof( TDefaultConfigurationData );

      ConnectionPool<PgSQLConnection> ConnectionPoolProvider<PgSQLConnection>.CreateOneTimeUseConnectionPool( Object creationParameters )
      {
         return this.CreateOneTimeUseConnectionPool( CheckCreationParameters( creationParameters ) );
      }

      ConnectionPool<PgSQLConnection, TimeSpan> ConnectionPoolProvider<PgSQLConnection>.CreateTimeoutingConnectionPool( Object creationParameters )
      {
         return this.CreateTimeoutingConnectionPool( CheckCreationParameters( creationParameters ) );
      }
#if !NETSTANDARD1_0
      public SQLConnectionPool<PgSQLConnection> CreateOneTimeUseConnectionPool(
         PgSQLConnectionCreationInfo connectionConfig
         )
      {
         return new OneTimeUseSQLConnectionPool<PgSQLConnection, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfo>(
            _VendorFunctionality,
            connectionConfig,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      public SQLConnectionPool<PgSQLConnection, TimeSpan> CreateTimeoutingConnectionPool(
         PgSQLConnectionCreationInfo connectionConfig
         )
      {
         return new CachingSQLConnectionPoolWithTimeout<PgSQLConnection, PgSQLConnectionCreationInfo>(
            _VendorFunctionality,
            connectionConfig
            );
      }
#endif

      public SQLConnectionPool<PgSQLConnection> CreateOneTimeUseConnectionPool(
         PgSQLConnectionCreationInfoForReadyMadeStreams connectionConfig
         )
      {
         return new OneTimeUseSQLConnectionPool<PgSQLConnection, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfoForReadyMadeStreams>(
            _VendorFunctionalityForReadyMadeStreams,
            connectionConfig,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      public SQLConnectionPool<PgSQLConnection, TimeSpan> CreateTimeoutingConnectionPool(
         PgSQLConnectionCreationInfoForReadyMadeStreams connectionConfig
         )
      {
         return new CachingSQLConnectionPoolWithTimeout<PgSQLConnection, PgSQLConnectionCreationInfoForReadyMadeStreams>(
            _VendorFunctionalityForReadyMadeStreams,
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
