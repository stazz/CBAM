/*
 * Copyright 2018 Stanislav Muhametsin. All rights Reserved.
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
using CBAM.Abstractions.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.ResourcePooling;
using UtilPack.ResourcePooling.NetworkStream;
using CBAM.Abstractions.Implementation.NetworkStream;
using UtilPack.Configuration.NetworkStream;

namespace CBAM.HTTP.Implementation
{
   public sealed class HTTPNetworkConnectionPoolProvider<TRequestMetaData> : AbstractAsyncResourceFactoryProvider<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo>
   {

      /// <summary>
      /// Gets the <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="PgSQLConnection"/>s.
      /// </summary>
      /// <value>The <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="PgSQLConnection"/>s.</value>
      /// <remarks>
      /// By invoking <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/>, one gets the bound version <see cref="AsyncResourceFactory{TResource}"/>, with only one generic parameter.
      /// Instead of directly using <see cref="AsyncResourceFactory{TResource}.AcquireResourceAsync"/>, typical scenario would involve creating an instance <see cref="AsyncResourcePool{TResource}"/> by invoking one of various extension methods for <see cref="AsyncResourceFactory{TResource}"/>.
      /// </remarks>
      public static AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> Factory { get; } = new HTTPConnectionFactoryFactory<TRequestMetaData>(); // DefaultAsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo>( config => new PgSQLConnectionFactory( config, new UTF8EncodingInfo() ) );

      /// <summary>
      /// Creates a new instance of <see cref="HTTPNetworkConnectionPoolProvider{TRequestMetaData}"/>.
      /// </summary>
      /// <remarks>
      /// This constructor is not intended to be used directly, but a generic scenarios like MSBuild task dynamically loading this type.
      /// </remarks>
      public HTTPNetworkConnectionPoolProvider()
         : base( typeof( HTTPNetworkCreationInfoData ) )
      {
      }


      protected override HTTPNetworkCreationInfo TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         HTTPNetworkCreationInfo retVal;
         if ( creationParameters is HTTPNetworkCreationInfoData creationData )
         {
            retVal = new HTTPNetworkCreationInfo( creationData );

         }
         else if ( creationParameters is HTTPNetworkCreationInfo creationInfo )
         {
            retVal = creationInfo;
         }
         else
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( HTTPNetworkCreationInfoData ).FullName}." );
         }

         return retVal;
      }

      /// <summary>
      /// This method implements <see cref="AbstractAsyncResourceFactoryProvider{TFactoryResource, TCreationParameters}.CreateFactory"/> by returning static property <see cref="Factory"/>.
      /// </summary>
      /// <returns>The value of <see cref="Factory"/> static property.</returns>
      protected override AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> CreateFactory()
         => Factory;
   }

   internal sealed class HTTPConnectionFactoryFactory<TRequestMetaData> : AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> //, AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPSimpleCreationInfo>
   {
      public AsyncResourceFactory<HTTPConnection<TRequestMetaData>> BindCreationParameters( HTTPNetworkCreationInfo creationInfo )
      {
         return creationInfo
            .NewFactoryParametrizer<HTTPNetworkCreationInfo, HTTPNetworkCreationInfoData, HTTPConnectionConfiguration, HTTPInitializationConfiguration, HTTPProtocolConfiguration, HTTPPoolingConfiguration>()
            .BindPublicConnectionType<HTTPConnection<TRequestMetaData>>()
            .CreateStatelessDelegatingConnectionFactory(
               Encoding.ASCII.CreateDefaultEncodingInfo(),
               ( parameters, encodingInfo, stringPool ) =>
               {
                  return TaskUtils.TaskFromBoolean( ( parameters.CreationData?.Connection?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired ) != ConnectionSSLMode.NotRequired );
               },
               null,
               null,
               null,
               null,
               null,
               ( parameters, encodingInfo, stringPool, stream, socketOrNull, token ) =>
               {
                  return new ValueTask<HTTPConnectionFunctionalityImpl<TRequestMetaData>>(
                     new HTTPConnectionFunctionalityImpl<TRequestMetaData>(
                        HTTPConnectionVendorImpl<TRequestMetaData>.Instance,
                        new ClientProtocolIOState(
                           stream,
                           stringPool,
                           Encoding.ASCII.CreateDefaultEncodingInfo(),
                           new WriteState(),
                           new ReadState()
                           ) )
                        );
               },
               functionality => new ValueTask<HTTPConnectionImpl<TRequestMetaData>>( new HTTPConnectionImpl<TRequestMetaData>( functionality ) ),
               ( functionality, connection ) => new StatelessConnectionAcquireInfol<HTTPConnectionImpl<TRequestMetaData>, HTTPConnectionFunctionalityImpl<TRequestMetaData>, Stream>( connection, functionality, functionality.Stream ),
               ( functionality, connection, token, error ) => functionality?.Stream
               );
      }
   }

}
