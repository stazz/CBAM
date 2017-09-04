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

namespace CBAM.SQL.PostgreSQL
{
   /// <summary>
   /// This is the entrypoint-class for using PostgreSQL connections.
   /// This class implements has methods to create <see cref="AsyncResourcePool{TResource}"/> that exposes API to use <see cref="PgSQLConnection"/>s.
   /// Use <see cref="Instance"/> to access the API of this class: this class has not state, and constructor is public *only* to enable generic load scenarios required by <see cref="ResourcePoolProvider{TResource}"/> interface.
   /// </summary>
   /// <remarks>
   /// The <see cref="CreateTimeoutingResourcePool(PgSQLConnectionCreationInfo)"/> and <see cref="CreateOneTimeUseResourcePool(PgSQLConnectionCreationInfo)"/> are the most commonly used methods.
   /// This class also (explicitly) implements <see cref="ResourcePoolProvider{TResource}"/> interface in order to provide dynamic creation of <see cref="AsyncResourcePool{TResource}"/>s, but this is used in generic scenarios (e.g. MSBuild task, where this class can be given as parameter, and the task dynamically loads this type).
   /// </remarks>
   public sealed class PgSQLConnectionPoolProvider : AsyncResourcePoolProvider<PgSQLConnection>
   {
      private static readonly IEncodingInfo Encoding;

      static PgSQLConnectionPoolProvider()
      {
         Encoding = new UTF8EncodingInfo();
         Instance = new PgSQLConnectionPoolProvider();
      }

      /// <summary>
      /// Gets the default instance of this class.
      /// This is preferred way of getting this class, since it has no state on its own.
      /// The constructor is public only to enable dynamic load scenarios related to <see cref="ResourcePoolProvider{TResource}"/> interface.
      /// </summary>
      /// <value>The default instance of this class.</value>
      public static PgSQLConnectionPoolProvider Instance { get; }

      /// <summary>
      /// This property implements <see cref="ResourcePoolProvider{TResource}.DefaultTypeForCreationParameter"/> and returns the type of the default connection creation parameter.
      /// </summary>
      /// <value>The type of the default connection creation parameter.</value>
      /// <remarks>
      /// Note that this returns <see cref="PgSQLConnectionCreationInfoData"/>, and not <see cref="PgSQLConnectionCreationInfo"/>.
      /// </remarks>
      public Type DefaultTypeForCreationParameter => typeof( PgSQLConnectionCreationInfoData );

      /// <summary>
      /// Explicitly implements <see cref="ResourcePoolProvider{TResource}.CreateOneTimeUseResourcePool(object)"/> to call <see cref="CreateOneTimeUseResourcePool(PgSQLConnectionCreationInfo)"/>.
      /// </summary>
      /// <param name="creationParameters">The creation parameters, which should be of type <see cref="PgSQLConnectionCreationInfoData"/>.</param>
      /// <returns>A new instance of <see cref="AsyncResourcePoolObservable{TResource}"/>.</returns>
      /// <seealso cref="ResourcePoolProvider{TResource}.CreateOneTimeUseResourcePool(object)"/>
      /// <exception cref="ArgumentNullException">If <paramref name="creationParameters"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">If <paramref name="creationParameters"/> is not of type <see cref="PgSQLConnectionCreationInfoData"/>.</exception>
      /// <exception cref="NotSupportedException">On .NET Standard pre-1.3 platforms, *always*.</exception>
      AsyncResourcePoolObservable<PgSQLConnection> AsyncResourcePoolProvider<PgSQLConnection>.CreateOneTimeUseResourcePool( Object creationParameters )
      {
#if NETSTANDARD1_0
         throw new NotSupportedException( "This method is not supported for this platform." );
#else
         return this.CreateOneTimeUseResourcePool( CheckCreationParameters( creationParameters ) );
#endif
      }

      /// <summary>
      /// Explicitly implements <see cref="ResourcePoolProvider{TResource}.CreateTimeoutingResourcePool(object)"/> to call <see cref="CreateTimeoutingResourcePool(PgSQLConnectionCreationInfo)"/>.
      /// </summary>
      /// <param name="creationParameters">The creation parameters, which should be of type <see cref="PgSQLConnectionCreationInfoData"/>.</param>
      /// <returns>A new instance of <see cref="AsyncResourcePool{TResource, TCleanUpParameters}"/>.</returns>
      /// <seealso cref="ResourcePoolProvider{TResource}.CreateTimeoutingResourcePool(object)"/>
      /// <exception cref="ArgumentNullException">If <paramref name="creationParameters"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">If <paramref name="creationParameters"/> is not of type <see cref="PgSQLConnectionCreationInfoData"/>.</exception>
      /// <exception cref="NotSupportedException">On .NET Standard pre-1.3 platforms, *always*.</exception>
      AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> AsyncResourcePoolProvider<PgSQLConnection>.CreateTimeoutingResourcePool( Object creationParameters )
      {
#if NETSTANDARD1_0
         throw new NotSupportedException( "This method is not supported for this platform." );
#else
         return this.CreateTimeoutingResourcePool( CheckCreationParameters( creationParameters ) );
#endif
      }

      /// <summary>
      /// This method will create a one-time-usage resource pool (which will close connection after each call to <see cref="AsyncResourcePool{TResource}.UseResourceAsync"/>) with given <see cref="PgSQLConnectionCreationInfo"/>.
      /// </summary>
      /// <param name="creationInfo">The <see cref="PgSQLConnectionCreationInfo"/> that the returned pool will use to create new connections.</param>
      /// <returns>A new <see cref="AsyncResourcePoolObservable{TResource}"/> which will use given <see cref="PgSQLConnectionCreationInfo"/> to create new connections.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="creationInfo"/> is <c>null</c>.</exception>
      public AsyncResourcePoolObservable<PgSQLConnection> CreateOneTimeUseResourcePool(
         PgSQLConnectionCreationInfo creationInfo
         )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationInfo ), creationInfo );

         return new OneTimeUseAsyncResourcePool<PgSQLConnectionImpl, PgSQLConnectionAcquireInfo, PgSQLConnectionCreationInfo>(
            new PgSQLConnectionFactory( Encoding, creationInfo ),
            creationInfo,
            acquire => acquire,
            acquire => (PgSQLConnectionAcquireInfo) acquire
            );
      }

      /// <summary>
      /// This method will create timeouting resource pool (which can clear old connections using <see cref="AsyncResourcePoolCleanUp{TCleanUpParameter}.CleanUpAsync"/> method) with given <see cref="PgSQLConnectionCreationInfo"/>.
      /// </summary>
      /// <param name="creationInfo">The <see cref="PgSQLConnectionCreationInfo"/> that the returned pool will use to create new connections.</param>
      /// <returns>A new <see cref="AsyncResourcePoolObservable{TResource}"/> which will use given <see cref="PgSQLConnectionCreationInfo"/> to create new connections.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="creationInfo"/> is <c>null</c>.</exception>
      public AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> CreateTimeoutingResourcePool(
         PgSQLConnectionCreationInfo creationInfo
         )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationInfo ), creationInfo );

         return new CachingAsyncResourcePoolWithTimeout<PgSQLConnectionImpl, PgSQLConnectionCreationInfo>(
            new PgSQLConnectionFactory( Encoding, creationInfo ),
            creationInfo
            );
      }

      private static PgSQLConnectionCreationInfo CheckCreationParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         if ( !( creationParameters is PgSQLConnectionCreationInfoData creationData ) )
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( PgSQLConnectionCreationInfoData ).FullName}." );
         }

         return new PgSQLConnectionCreationInfo( creationData );
      }
   }
}
