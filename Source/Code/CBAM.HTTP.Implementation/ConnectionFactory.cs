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
using CBAM.Abstractions.Implementation.NetworkStream;
using CBAM.HTTP;
using CBAM.HTTP.Implementation;
using IOUtils.Network.Configuration;
using ResourcePooling.Async.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.HTTP.Implementation
{
   /// <summary>
   /// This class provides static <see cref="Factory"/> property to create connection pools that provide instances of <see cref="HTTPConnection{TRequestMetaData}"/> to use.
   /// This class also can be used dynamically by some other code, and then use <see cref="AsyncResourceFactoryProvider.BindCreationParameters"/> to obtain same <see cref="AsyncResourceFactory{TResource}"/> as the one by calling <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/> for the static property <see cref="Factory"/> of this class.
   /// The configuration type for this factory class is <see cref="HTTPNetworkCreationInfo"/>.
   /// For simpler configuration support, see <see cref="HTTPSimpleConfigurationPoolProvider{TRequestMetaData}"/>
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with each request, used in identifying the request that response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   /// <seealso cref="HTTPSimpleConfigurationPoolProvider{TRequestMetaData}"/>
   public sealed class HTTPNetworkConnectionPoolProvider<TRequestMetaData> : AbstractAsyncResourceFactoryProvider<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo>
   {

      /// <summary>
      /// Gets the <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create pools that provide instances of <see cref="HTTPConnection{TRequestMetaData}"/> to use.
      /// </summary>
      /// <value>The <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="HTTPConnection{TRequestMetaData}"/> to use.</value>
      /// <remarks>
      /// By invoking <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/>, one gets the bound version <see cref="AsyncResourceFactory{TResource}"/>, with only one generic parameter.
      /// Instead of directly using <see cref="AsyncResourceFactory{TResource}.CreateAcquireResourceContext"/>, typical scenario would involve creating an instance <see cref="AsyncResourcePool{TResource}"/> by invoking one of various extension methods for <see cref="AsyncResourceFactory{TResource}"/>.
      /// </remarks>
      public static AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> Factory { get; } = new DefaultAsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo>( config => config
            .NewFactoryParametrizer<HTTPNetworkCreationInfo, HTTPNetworkCreationInfoData, HTTPConnectionConfiguration, HTTPInitializationConfiguration, HTTPProtocolConfiguration, HTTPPoolingConfiguration>()
            .BindPublicConnectionType<HTTPConnection<TRequestMetaData>>()
            .CreateStatelessDelegatingConnectionFactory(
               Encoding.ASCII.CreateDefaultEncodingInfo(),
               ( parameters, encodingInfo, stringPool, stringPoolIsDedicated ) =>
               {
                  return TaskUtils.TaskFromBoolean( ( parameters.CreationData?.Connection?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired ) != ConnectionSSLMode.NotRequired );
               },
               null,
               null,
               null,
               null,
               null,
               ( parameters, encodingInfo, stringPool, stringPoolIsDedicated, stream, socketOrNull, token ) =>
               {
                  return new ValueTask<HTTPConnectionFunctionalityImpl<TRequestMetaData>>(
                     new HTTPConnectionFunctionalityImpl<TRequestMetaData>(
                        HTTPConnectionVendorImpl<TRequestMetaData>.Instance,
                        new ClientProtocolIOState(
                           stream,
                           stringPool,
                           encodingInfo,
                           new WriteState(),
                           new ReadState()
                           ) )
                        );
               },
               functionality => new ValueTask<HTTPConnectionImpl<TRequestMetaData>>( new HTTPConnectionImpl<TRequestMetaData>( functionality ) ),
               ( functionality, connection ) => new StatelessConnectionAcquireInfo<HTTPConnectionImpl<TRequestMetaData>, HTTPConnectionFunctionalityImpl<TRequestMetaData>, Stream>( connection, functionality, functionality.Stream ),
               ( functionality, connection, token, error ) => functionality?.Stream
               ) );

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

      /// <inheritdoc />
      protected override HTTPNetworkCreationInfo TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         HTTPNetworkCreationInfo retVal;
         switch ( creationParameters )
         {
            case HTTPNetworkCreationInfoData creationData:
               retVal = new HTTPNetworkCreationInfo( creationData );
               break;
            case HTTPNetworkCreationInfo creationInfo:
               retVal = creationInfo;
               break;
            case SimpleHTTPConfiguration simpleConfig:
               retVal = simpleConfig.CreateNetworkCreationInfo();
               break;
            default:
               throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( HTTPNetworkCreationInfoData ).FullName} or { typeof( SimpleHTTPConfiguration ).FullName }." );
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

   /// <summary>
   /// This class provides static <see cref="Factory"/> property to create connection pools that provide instances of <see cref="HTTPConnection{TRequestMetaData}"/> to use.
   /// This class also can be used dynamically by some other code, and then use <see cref="AsyncResourceFactoryProvider.BindCreationParameters"/> to obtain same <see cref="AsyncResourceFactory{TResource}"/> as the one by calling <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/> for the static property <see cref="Factory"/> of this class.
   /// The configuration type for this factory class is <see cref="SimpleHTTPConfiguration"/>.
   /// For more advanced and customizable configuration support, see <see cref="HTTPNetworkConnectionPoolProvider{TRequestMetaData}"/>
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with each request, used in identifying the request that response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   /// <seealso cref="HTTPNetworkConnectionPoolProvider{TRequestMetaData}"/>
   public sealed class HTTPSimpleConfigurationPoolProvider<TRequestMetaData> : AbstractAsyncResourceFactoryProvider<HTTPConnection<TRequestMetaData>, SimpleHTTPConfiguration>
   {
      /// <summary>
      /// Gets the <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create pools that provide instances of <see cref="HTTPConnection{TRequestMetaData}"/> to use.
      /// </summary>
      /// <value>The <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="HTTPConnection{TRequestMetaData}"/> to use.</value>
      /// <remarks>
      /// By invoking <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/>, one gets the bound version <see cref="AsyncResourceFactory{TResource}"/>, with only one generic parameter.
      /// Instead of directly using <see cref="AsyncResourceFactory{TResource}.CreateAcquireResourceContext"/>, typical scenario would involve creating an instance <see cref="AsyncResourcePool{TResource}"/> by invoking one of various extension methods for <see cref="AsyncResourceFactory{TResource}"/>.
      /// </remarks>
      public static AsyncResourceFactory<HTTPConnection<TRequestMetaData>, SimpleHTTPConfiguration> Factory { get; } = new DefaultAsyncResourceFactory<HTTPConnection<TRequestMetaData>, SimpleHTTPConfiguration>( config => HTTPNetworkConnectionPoolProvider<TRequestMetaData>.Factory.BindCreationParameters( config.CreateNetworkCreationInfo() ) );

      /// <summary>
      /// Creates a new instance of <see cref="HTTPSimpleConfigurationPoolProvider{TRequestMetaData}"/>.
      /// </summary>
      /// <remarks>
      /// This constructor is not intended to be used directly, but a generic scenarios like MSBuild task dynamically loading this type.
      /// </remarks>
      public HTTPSimpleConfigurationPoolProvider()
         : base( typeof( SimpleHTTPConfiguration ) )
      {
      }

      /// <inheritdoc />
      protected override SimpleHTTPConfiguration TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         SimpleHTTPConfiguration retVal;
         switch ( creationParameters )
         {
            case SimpleHTTPConfiguration simpleConfig:
               retVal = simpleConfig;
               break;
            default:
               throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of { typeof( SimpleHTTPConfiguration ).FullName }." );
         }
         return retVal;
      }

      /// <summary>
      /// This method implements <see cref="AbstractAsyncResourceFactoryProvider{TFactoryResource, TCreationParameters}.CreateFactory"/> by returning static property <see cref="Factory"/>.
      /// </summary>
      /// <returns>The value of <see cref="Factory"/> static property.</returns>
      protected override AsyncResourceFactory<HTTPConnection<TRequestMetaData>, SimpleHTTPConfiguration> CreateFactory()
         => Factory;
   }


}

public static partial class E_CBAM
{
   /// <summary>
   /// This is ease-of-life method to asynchronously create <see cref="HTTPConnection{TRequestMetaData}"/> pool, send one HTTP request, and receive and return first HTTP response, with its contents completely stringified.
   /// </summary>
   /// <param name="config">This <see cref="SimpleHTTPConfiguration"/>.</param>
   /// <param name="request">The request to send.</param>
   /// <param name="defaultEncoding">The default encoding to use when stringifying the response contents. If not specified, a <see cref="UTF8Encoding"/> will be used.</param>
   /// <returns>Asynchronously returns the <see cref="HTTPTextualResponseInfo"/> of the first response that server sends.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SimpleHTTPConfiguration"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="request"/> is <c>null</c>.</exception>
   public static Task<HTTPTextualResponseInfo> CreatePoolAndReceiveTextualResponseAsync( this SimpleHTTPConfiguration config, HTTPRequest request, Encoding defaultEncoding = null )
   {
      ArgumentValidator.ValidateNotNullReference( config );
      ArgumentValidator.ValidateNotNull( nameof( request ), request );
      return HTTPSimpleConfigurationPoolProvider<Int64>
         .Factory
         .BindCreationParameters( config )
         .CreateOneTimeUseResourcePool()
         .UseResourceAsync( async conn => { return await conn.ReceiveOneTextualResponseAsync( request, defaultEncoding: defaultEncoding ); }, default );
   }
}