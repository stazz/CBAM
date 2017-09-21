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
   public static partial class HTTPExtensions
   {
      public static HTTPConnection CreateNewHTTPConnection( this ExplicitAsyncResourcePool<Stream> streamPool, HTTPConnectionConfiguration configuration = null )
      {
         return new HTTPConnectionImpl(
            new HTTPConnectionFunctionalityImpl(
               HTTPConnectionVendorImpl.Instance,
               streamPool,
               configuration?.Data?.MaxReadBufferSize,
               configuration?.Data?.MaxWriteBufferSize
               )
            );
      }

   }

}

public static partial class E_HTTP
{
   public static NetworkStreamFactoryConfiguration CreateNetworkStreamFactoryConfiguration( this HTTPConnectionEndPointConfigurationData httpEndPointConfigurationData )
   {
      return new HTTPConnectionEndPointConfiguration( ArgumentValidator.ValidateNotNullReference( httpEndPointConfigurationData ) )
         .CreateNetworkStreamFactoryConfiguration();
   }

   public static NetworkStreamFactoryConfiguration CreateNetworkStreamFactoryConfiguration( this HTTPConnectionEndPointConfiguration httpEndPointConfiguration )
   {
      var host = httpEndPointConfiguration.Data.Host;
      var remoteAddress = host.CreateAddressOrHostNameResolvingLazy( null );
      var data = httpEndPointConfiguration.Data;
      return new NetworkStreamFactoryConfiguration()
      {
         ConnectionSSLMode = () => data.IsSecure ? ConnectionSSLMode.Required : ConnectionSSLMode.NotRequired,
         IsSSLPossible = () => true,
         ProvideSSLHost = () => host,
         RemoteAddress = async ( token ) => await remoteAddress,
         RemotePort = addr =>
         {
            var port = data.Port;
            if ( port <= 0 )
            {
               port = data.IsSecure ? 443 : 80;
            }
            return port;
         }
      };
   }
}