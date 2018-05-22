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
using CBAM.HTTP;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UtilPack;
using UtilPack.Configuration.NetworkStream;

namespace CBAM.HTTP
{
   public sealed class HTTPNetworkCreationInfo : NetworkConnectionCreationInfo<HTTPNetworkCreationInfoData, HTTPConnectionConfiguration, HTTPInitializationConfiguration, HTTPProtocolConfiguration, HTTPPoolingConfiguration>
   {
      public HTTPNetworkCreationInfo(
         HTTPNetworkCreationInfoData data
         ) : base( data )
      {
      }

   }

   public sealed class HTTPNetworkCreationInfoData : NetworkConnectionCreationInfoData<HTTPConnectionConfiguration, HTTPInitializationConfiguration, HTTPProtocolConfiguration, HTTPPoolingConfiguration>
   {

   }

   public sealed class HTTPConnectionConfiguration : NetworkConnectionConfiguration
   {

   }

   public sealed class HTTPInitializationConfiguration : NetworkInitializationConfiguration<HTTPProtocolConfiguration, HTTPPoolingConfiguration>
   {

   }

   public sealed class HTTPProtocolConfiguration
   {
   }

   public sealed class HTTPPoolingConfiguration : NetworkPoolingConfiguration
   {

   }

   /// <summary>
   /// This class contains properties for simplistic HTTP configuration.
   /// This class may also be used when (de)serializing configuration.
   /// </summary>
   /// <seealso cref="HTTPSimpleConfigurationPoolProvider{TRequestMetaData}"/>
   public class SimpleHTTPConfiguration
   {
      /// <summary>
      /// Gets or sets the host name for the remote endpoint.
      /// </summary>
      /// <value>The host name for the remote endpoint.</value>
      /// <remarks>
      /// This may be either stringified <see cref="IPAddress"/> or actual hostname (which will result in DNS resolve).
      /// </remarks>
      public String Host { get; set; }

      /// <summary>
      /// Gets or sets the port number for the remote endpoint.
      /// </summary>
      /// <value>The port number for the remote endpoint.</value>
      public Int32 Port { get; set; }

      /// <summary>
      /// Gets or sets the value indicating whether the connection is secured by SSL.
      /// </summary>
      /// <value>The value indicating whether the connection is secured by SSL.</value>
      public Boolean IsSecure { get; set; }
   }


}

public static partial class E_CBAM
{
   public static HTTPNetworkCreationInfo CreateNetworkCreationInfo( this SimpleHTTPConfiguration simpleConfig )
   {
      var isSecure = simpleConfig.IsSecure;
      var port = simpleConfig.Port;
      return new HTTPNetworkCreationInfo( new HTTPNetworkCreationInfoData()
      {
         Connection = new HTTPConnectionConfiguration()
         {
            ConnectionSSLMode = isSecure ? ConnectionSSLMode.Required : ConnectionSSLMode.NotRequired,
            Host = simpleConfig.Host,
            Port = port <= 0 ? ( isSecure ? 443 : 80 ) : port
         },

      } );
   }
}