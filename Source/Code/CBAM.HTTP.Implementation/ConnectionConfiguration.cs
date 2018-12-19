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
using IOUtils.Network.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UtilPack;

namespace CBAM.HTTP
{
   /// <summary>
   /// This class extends <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> to provide detailed and highly customizable configuration for HTTP connection.
   /// </summary>
   public sealed class HTTPNetworkCreationInfo : NetworkConnectionCreationInfo<HTTPNetworkCreationInfoData, HTTPConnectionConfiguration, HTTPInitializationConfiguration, HTTPProtocolConfiguration, HTTPPoolingConfiguration>
   {
      /// <summary>
      /// Creates new instance of <see cref="HTTPNetworkCreationInfo"/> with given <see cref="HTTPNetworkCreationInfoData"/>.
      /// </summary>
      /// <param name="data">The <see cref="HTTPNetworkCreationInfoData"/>.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="data"/> is <c>null</c>.</exception>
      public HTTPNetworkCreationInfo(
         HTTPNetworkCreationInfoData data
         ) : base( data )
      {
      }

   }

   /// <summary>
   /// This class extends <see cref="NetworkConnectionCreationInfoData{TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> to provide detailed and highly customizable configuration for HTTP connection.
   /// </summary>
   public sealed class HTTPNetworkCreationInfoData : NetworkConnectionCreationInfoData<HTTPConnectionConfiguration, HTTPInitializationConfiguration, HTTPProtocolConfiguration, HTTPPoolingConfiguration>
   {

   }

   /// <summary>
   /// This class extends <see cref="NetworkConnectionConfiguration"/> to provide detailed and highly customizable configuration for HTTP connection.
   /// </summary>
   public sealed class HTTPConnectionConfiguration : NetworkConnectionConfiguration
   {

   }

   /// <summary>
   /// This class extends <see cref="NetworkInitializationConfiguration{TProtocolConfiguration, TPoolingConfiguration}"/> to provide detailed and highly customizable configuration for HTTP protocol and pooling initialization.
   /// </summary>
   public sealed class HTTPInitializationConfiguration : NetworkInitializationConfiguration<HTTPProtocolConfiguration, HTTPPoolingConfiguration>
   {

   }

   /// <summary>
   /// This class contains information about HTTP protocol initialization.
   /// </summary>
   public sealed class HTTPProtocolConfiguration
   {
   }

   /// <summary>
   /// This class extends <see cref="NetworkPoolingConfiguration"/>.
   /// </summary>
   public sealed class HTTPPoolingConfiguration : NetworkPoolingConfiguration
   {

   }

   /// <summary>
   /// This class contains properties for simplistic HTTP configuration.
   /// This class may also be used when (de)serializing configuration.
   /// </summary>
   /// <seealso cref="Implementation.HTTPSimpleConfigurationPoolProvider{TRequestMetaData}"/>
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
   /// <summary>
   /// This is helper method to create a new <see cref="HTTPNetworkCreationInfo"/> from this <see cref="SimpleHTTPConfiguration"/>.
   /// </summary>
   /// <param name="simpleConfig">This <see cref="SimpleHTTPConfiguration"/>.</param>
   /// <returns>A new instance of <see cref="HTTPNetworkCreationInfo"/> which is configured as this <see cref="SimpleHTTPConfiguration"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SimpleHTTPConfiguration"/> is <c>null</c>.</exception>
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