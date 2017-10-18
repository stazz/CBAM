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
using CBAM.HTTP.Implementation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UtilPack.ResourcePooling;
using UtilPack.ResourcePooling.NetworkStream;
using System.IO;
using CBAM.HTTP;
using UtilPack;

namespace CBAM.HTTP
{
   /// <summary>
   /// This class contains extension methods for types defined in other assemblies than this assembly.
   /// </summary>
   public static partial class HTTPExtensions
   {
      /// <summary>
      /// This method will create a new instance of <see cref="HTTPConnection"/> which will use this <see cref="ExplicitAsyncResourcePool{TResource}"/> when sending and receiving <see cref="HTTPMessage{TContent}"/>s.
      /// </summary>
      /// <param name="streamPool">This <see cref="ExplicitAsyncResourcePool{TResource}"/>.</param>
      /// <param name="configuration">The optional <see cref="HTTPConnectionConfiguration"/> to use.</param>
      /// <returns>A new instance of <see cref="HTTPConnection"/> that represents connection to remote endpoint and can be used to send and receive <see cref="HTTPMessage{TContent}"/>s via <see cref="CBAM.Abstractions.Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}.PrepareStatementForExecution"/>.</returns>
      /// <exception cref="NullReferenceException">If this <see cref="ExplicitAsyncResourcePool{TResource}"/> is <c>null</c>.</exception>
      public static HTTPConnection CreateNewHTTPConnection( this ExplicitAsyncResourcePool<Stream> streamPool, HTTPConnectionConfiguration configuration = null )
      {
         return new HTTPConnectionImpl(
            new HTTPConnectionFunctionalityImpl(
               HTTPConnectionVendorImpl.Instance,
               ArgumentValidator.ValidateNotNullReference( streamPool ),
               configuration?.Data?.MaxReadBufferSize,
               configuration?.Data?.MaxWriteBufferSize
               )
            );
      }

   }

}

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_HTTP
{
   /// <summary>
   /// Creates a new <see cref="NetworkStreamFactoryConfiguration"/> from this <see cref="HTTPConnectionEndPointConfigurationData"/>.
   /// The returned <see cref="NetworkStreamFactoryConfiguration"/> can be used to e.g. invoke <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/> method of <see cref="NetworkStreamFactory"/>, or directly call static <see cref="NetworkStreamFactory.AcquireNetworkStreamFromConfiguration"/>.
   /// </summary>
   /// <param name="httpEndPointConfigurationData">This <see cref="HTTPConnectionEndPointConfigurationData"/>.</param>
   /// <returns>A instance of <see cref="NetworkStreamFactoryConfiguration"/> which will use this <see cref="HTTPConnectionEndPointConfigurationData"/> in its callbacks.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPConnectionEndPointConfigurationData"/> is <c>null</c>.</exception>
   public static NetworkStreamFactoryConfiguration CreateNetworkStreamFactoryConfiguration( this HTTPConnectionEndPointConfigurationData httpEndPointConfigurationData )
   {
      var host = httpEndPointConfigurationData.Host;
      var remoteAddress = host.CreateAddressOrHostNameResolvingLazy( null );
      return new NetworkStreamFactoryConfiguration()
      {
         ConnectionSSLMode = () => httpEndPointConfigurationData.IsSecure ? ConnectionSSLMode.Required : ConnectionSSLMode.NotRequired,
         IsSSLPossible = () => true,
         ProvideSSLHost = () => host,
         RemoteAddress = async ( token ) => await remoteAddress,
         RemotePort = addr =>
         {
            var port = httpEndPointConfigurationData.Port;
            if ( port <= 0 )
            {
               port = httpEndPointConfigurationData.IsSecure ? 443 : 80;
            }
            return port;
         }
      };
   }

   /// <summary>
   /// Creates a new <see cref="NetworkStreamFactoryConfiguration"/> from this <see cref="HTTPConnectionEndPointConfiguration"/>.
   /// The returned <see cref="NetworkStreamFactoryConfiguration"/> can be used to e.g. invoke <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/> method of <see cref="NetworkStreamFactory"/>, or directly call static <see cref="NetworkStreamFactory.AcquireNetworkStreamFromConfiguration"/>.
   /// </summary>
   /// <param name="httpEndPointConfiguration">This <see cref="HTTPConnectionEndPointConfiguration"/>.</param>
   /// <returns>A instance of <see cref="NetworkStreamFactoryConfiguration"/> which will use this <see cref="HTTPConnectionEndPointConfiguration"/> in its callbacks.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="HTTPConnectionEndPointConfiguration"/> is <c>null</c>.</exception>
   public static NetworkStreamFactoryConfiguration CreateNetworkStreamFactoryConfiguration( this HTTPConnectionEndPointConfiguration httpEndPointConfiguration )
   {
      return httpEndPointConfiguration.Data.CreateNetworkStreamFactoryConfiguration();
   }
}