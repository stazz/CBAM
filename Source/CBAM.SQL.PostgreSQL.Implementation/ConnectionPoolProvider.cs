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
using CBAM.SQL.PostgreSQL;
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
   /// Use <see cref="Instance"/> to access the API of this class: this class has not state, and constructor is public *only* to enable generic load scenarios required by <see cref="AsyncResourcePoolProvider{TResource}"/> interface.
   /// </summary>
   /// <remarks>
   /// The <see cref="CreateTimeoutingResourcePool(PgSQLConnectionCreationInfo)"/> and <see cref="CreateOneTimeUseResourcePool(PgSQLConnectionCreationInfo)"/> are the most commonly used methods.
   /// This class also (explicitly) implements <see cref="AsyncResourcePoolProvider{TResource}"/> interface in order to provide dynamic creation of <see cref="AsyncResourcePool{TResource}"/>s, but this is used in generic scenarios (e.g. MSBuild task, where this class can be given as parameter, and the task dynamically loads this type).
   /// </remarks>
   public sealed class PgSQLConnectionPoolProvider : AbstractAsyncResourceFactoryProvider<PgSQLConnection, PgSQLConnectionCreationInfo>
   {
      //private static readonly IEncodingInfo Encoding;

      static PgSQLConnectionPoolProvider()
      {
         var encoding = new UTF8EncodingInfo();
         //Instance = new PgSQLConnectionPoolProvider();
         Factory = new DefaultAsyncResourceFactory<PgSQLConnection, PgSQLConnectionCreationInfo>( config => new PgSQLConnectionFactory( config, encoding ) );
      }

      ///// <summary>
      ///// Gets the default instance of this class.
      ///// This is preferred way of getting this class, since it has no state on its own.
      ///// The constructor is public only to enable dynamic load scenarios related to <see cref="AsyncResourcePoolProvider{TResource}"/> interface.
      ///// </summary>
      ///// <value>The default instance of this class.</value>
      //public static PgSQLConnectionPoolProvider Instance { get; }

      public static AsyncResourceFactory<PgSQLConnection, PgSQLConnectionCreationInfo> Factory { get; }

      public PgSQLConnectionPoolProvider()
         : base( typeof( PgSQLConnectionCreationInfoData ) )
      {
      }

      /// <summary>
      /// This method implements <see cref="AbstractAsyncResourceFactoryProvider{TCreationParameters}.TransformFactoryParameters"/> method, 
      /// </summary>
      /// <param name="untyped"></param>
      /// <returns></returns>
      protected override PgSQLConnectionCreationInfo TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         PgSQLConnectionCreationInfo retVal;
         if ( creationParameters is PgSQLConnectionCreationInfoData creationData )
         {
            retVal = new PgSQLConnectionCreationInfo( creationData );

         }
         else if ( creationParameters is PgSQLConnectionCreationInfo creationInfo )
         {
            retVal = creationInfo;
         }
         else
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( PgSQLConnectionCreationInfoData ).FullName}." );
         }

         return retVal;
      }

      protected override AsyncResourceFactory<PgSQLConnection, PgSQLConnectionCreationInfo> CreateFactory()
         => Factory;



      ///// <summary>
      ///// This method will create a one-time-usage resource pool (which will close connection after each call to <see cref="AsyncResourcePool{TResource}.UseResourceAsync"/>) with given <see cref="PgSQLConnectionCreationInfo"/>.
      ///// </summary>
      ///// <param name="creationInfo">The <see cref="PgSQLConnectionCreationInfo"/> that the returned pool will use to create new connections.</param>
      ///// <returns>A new <see cref="AsyncResourcePoolObservable{TResource}"/> which will use given <see cref="PgSQLConnectionCreationInfo"/> to create new connections.</returns>
      ///// <exception cref="ArgumentNullException">If <paramref name="creationInfo"/> is <c>null</c>.</exception>
      //public AsyncResourcePoolObservable<PgSQLConnection> CreateOneTimeUseResourcePool(
      //   PgSQLConnectionCreationInfo creationInfo
      //   )
      //{
      //   return new PgSQLConnectionFactory( Encoding, creationInfo )
      //      .CreateOneTimeUseResourcePool( creationInfo )
      //      .WithoutExplicitAPI();
      //}

      ///// <summary>
      ///// This method will create timeouting resource pool (which can clear old connections using <see cref="AsyncResourcePoolCleanUp{TCleanUpParameter}.CleanUpAsync"/> method) with given <see cref="PgSQLConnectionCreationInfo"/>.
      ///// </summary>
      ///// <param name="creationInfo">The <see cref="PgSQLConnectionCreationInfo"/> that the returned pool will use to create new connections.</param>
      ///// <returns>A new <see cref="AsyncResourcePoolObservable{TResource}"/> which will use given <see cref="PgSQLConnectionCreationInfo"/> to create new connections.</returns>
      ///// <exception cref="ArgumentNullException">If <paramref name="creationInfo"/> is <c>null</c>.</exception>
      //public AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> CreateTimeoutingResourcePool(
      //   PgSQLConnectionCreationInfo creationInfo
      //   )
      //{
      //   return new PgSQLConnectionFactory( Encoding, creationInfo )
      //      .CreateTimeoutingResourcePool( creationInfo )
      //      .WithoutExplicitAPI();
      //}

      //public AsyncResourcePoolObservable<PgSQLConnection, TimeSpan> CreateTimeoutingAndLimitedResourcePool(
      //   PgSQLConnectionCreationInfo creationInfo,
      //   Func<Int32> maxConcurrentConnections
      //   )
      //{
      //   return new PgSQLConnectionFactory( Encoding, creationInfo )
      //      .CreateTimeoutingAndLimitedResourcePool( creationInfo, maxConcurrentConnections )
      //      .WithoutExplicitAPI();
      //}

      private static PgSQLConnectionCreationInfo CheckCreationParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         PgSQLConnectionCreationInfo retVal;
         if ( creationParameters is PgSQLConnectionCreationInfoData creationData )
         {
            retVal = new PgSQLConnectionCreationInfo( creationData );

         }
         else if ( creationParameters is PgSQLConnectionCreationInfo creationInfo )
         {
            retVal = creationInfo;
         }
         else
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( PgSQLConnectionCreationInfoData ).FullName}." );
         }

         return retVal;
      }


   }
}