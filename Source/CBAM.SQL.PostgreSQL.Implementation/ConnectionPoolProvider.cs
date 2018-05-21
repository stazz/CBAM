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
using CBAM.Abstractions.Implementation.NetworkStream;
using CBAM.SQL.Implementation;
using CBAM.SQL.PostgreSQL;
using CBAM.SQL.PostgreSQL.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.Configuration.NetworkStream;
using UtilPack.ResourcePooling;

namespace CBAM.SQL.PostgreSQL
{
   using TIntermediateState = ValueTuple<BackendABIHelper, ResizableArray<Byte>, CancellationToken, Stream>;
   /// <summary>
   /// This is the entrypoint-class for using PostgreSQL connections.
   /// Use <see cref="Factory"/> static property to acquire <see cref="AsyncResourceFactory{TResource, TParams}"/> for <see cref="PgSQLConnection"/>, and then use <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/> to obtain <see cref="AsyncResourceFactory{TResource}"/>.
   /// This <see cref="AsyncResourceFactory{TResource}"/> with one generic type parameter then has a number of extension methods which create various <see cref="AsyncResourcePool{TResource}"/>s, those pools then provide effective mechanism to actually use <see cref="PgSQLConnection"/>s.
   /// </summary>
   /// <remarks>
   /// This class also (explicitly) implements <see cref="AsyncResourceFactoryProvider"/> interface in order to provide dynamic creation of <see cref="AsyncResourcePool{TResource}"/>s, but this is used in generic scenarios (e.g. MSBuild task, where this class can be given as parameter, and the task dynamically loads this type).
   /// </remarks>
   public sealed class PgSQLConnectionPoolProvider : AbstractAsyncResourceFactoryProvider<PgSQLConnection, PgSQLConnectionCreationInfo>
   {

      /// <summary>
      /// Gets the <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="PgSQLConnection"/>s.
      /// </summary>
      /// <value>The <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="PgSQLConnection"/>s.</value>
      /// <remarks>
      /// By invoking <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/>, one gets the bound version <see cref="AsyncResourceFactory{TResource}"/>, with only one generic parameter.
      /// Instead of directly using <see cref="AsyncResourceFactory{TResource}.AcquireResourceAsync"/>, typical scenario would involve creating an instance <see cref="AsyncResourcePool{TResource}"/> by invoking one of various extension methods for <see cref="AsyncResourceFactory{TResource}"/>.
      /// </remarks>
      public static AsyncResourceFactory<PgSQLConnection, PgSQLConnectionCreationInfo> Factory { get; } = new DefaultAsyncResourceFactory<PgSQLConnection, PgSQLConnectionCreationInfo>( config =>
         config.NewFactoryParametrizer<PgSQLConnectionCreationInfo, PgSQLConnectionCreationInfoData, PgSQLConnectionConfiguration, PgSQLInitializationConfiguration, PgSQLProtocolConfiguration, PgSQLPoolingConfiguration>()
            .BindPublicConnectionType<PgSQLConnection>()
            .CreateStatefulDelegatingConnectionFactory(
               new UTF8EncodingInfo(),
               ( parameters, encodingInfo, stringPool, socketOrNull, stream, token ) => new TIntermediateState( new BackendABIHelper( encodingInfo, stringPool ), new ResizableArray<Byte>( initialSize: 8, exponentialResize: true ), token, stream ),
               async ( parameters, encodingInfo, stringPool, state ) =>
               {
                  var sslMode = parameters.CreationData?.Connection?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired;
                  var retVal = sslMode == ConnectionSSLMode.Required || sslMode == ConnectionSSLMode.Preferred;
                  if ( retVal )
                  {
                     await SSLRequestMessage.INSTANCE.SendMessageAsync( (state.Item1, state.Item4, state.Item3, state.Item2) );

                     await state.Item4.ReadSpecificAmountAsync( state.Item2.Array, 0, 1, state.Item3 );
                     retVal = state.Item2.Array[0] == (Byte) 'S';
                  }

                  return retVal;
               },
               () => new PgSQLException( "Server accepted SSL request, but the creation parameters did not have callback to create SSL stream" ),
               () => new PgSQLException( "Server does not support SSL." ),
               () => new PgSQLException( "SSL stream creation callback returned null." ),
               () => new PgSQLException( "Authentication callback given by SSL stream creation callback was null." ),
               inner => new PgSQLException( "Unable to start SSL client.", inner ),
               async ( parameters, encodingInfo, stringPool, stream, socketOrNull, token, state ) =>
               {
                  (var proto, var warnings) = await PostgreSQLProtocol.PerformStartup(
                  new PgSQLConnectionVendorFunctionalityImpl(),
                     parameters,
                     token,
                     stream,
                     state.Item1,
                     state.Item2
#if !NETSTANDARD1_0
                     , (System.Net.Sockets.Socket) socketOrNull
#endif
                  );

                  return proto;
               },
               protocol => new ValueTask<PgSQLConnectionImpl>( new PgSQLConnectionImpl( protocol, new PgSQLDatabaseMetaData( protocol ) ) ),
               ( protocol, connection ) => new PgSQLConnectionAcquireInfo( connection, protocol ),
               ( functionality, connection, token, error ) => functionality?.Stream
         ) );

      //new PgSQLConnectionFactory( config, new UTF8EncodingInfo() ) );

      /// <summary>
      /// Creates a new instance of <see cref="PgSQLConnectionPoolProvider"/>.
      /// </summary>
      /// <remarks>
      /// This constructor is not intended to be used directly, but a generic scenarios like MSBuild task dynamically loading this type.
      /// </remarks>
      public PgSQLConnectionPoolProvider()
         : base( typeof( PgSQLConnectionCreationInfoData ) )
      {
      }

      /// <summary>
      /// This method implements <see cref="AbstractAsyncResourceFactoryProvider{TFactoryResource, TCreationParameters}.TransformFactoryParameters"/> method, by checking that given object is <see cref="PgSQLConnectionCreationInfo"/>  or <see cref="PgSQLConnectionCreationInfoData"/>.
      /// </summary>
      /// <param name="creationParameters">The untyped creation parameters.</param>
      /// <returns>The <see cref="PgSQLConnectionCreationInfo"/>.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="creationParameters"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">If <paramref name="creationParameters"/> is not <see cref="PgSQLConnectionCreationInfo"/> or <see cref="PgSQLConnectionCreationInfoData"/>.</exception>
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

      /// <summary>
      /// This method implements <see cref="AbstractAsyncResourceFactoryProvider{TFactoryResource, TCreationParameters}.CreateFactory"/> by returning static property <see cref="Factory"/>.
      /// </summary>
      /// <returns>The value of <see cref="Factory"/> static property.</returns>
      protected override AsyncResourceFactory<PgSQLConnection, PgSQLConnectionCreationInfo> CreateFactory()
         => Factory;
   }
}