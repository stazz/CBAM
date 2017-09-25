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
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UtilPack;

namespace CBAM.HTTP
{
   /// <summary>
   /// This is configuration class when creating <see cref="UtilPack.ResourcePooling.NetworkStream.NetworkStreamFactoryConfiguration"/> for <see cref="UtilPack.ResourcePooling.NetworkStream.NetworkStreamFactory"/> from HTTP-specific simple configuration.
   /// </summary>
   /// <remarks>
   /// This class contains all properties which are not considered as raw data, e.g. callbacks.
   /// Currently there are none, but in the future there may be some.
   /// The configuration raw data is in <see cref="HTTPConnectionEndPointConfigurationData"/> class.
   /// </remarks>
   /// <seealso cref="HTTPConnectionEndPointConfigurationData"/>
   /// <seealso cref="UtilPack.ResourcePooling.NetworkStream.NetworkStreamFactory"/>
   /// <seealso cref="UtilPack.ResourcePooling.AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/>
   /// <seealso cref="E_HTTP.CreateNetworkStreamFactoryConfiguration(HTTPConnectionEndPointConfiguration)"/>
   public class HTTPConnectionEndPointConfiguration
   {
      /// <summary>
      /// Creates new instance of <see cref="HTTPConnectionEndPointConfiguration"/> with given <see cref="HTTPConnectionEndPointConfigurationData"/>.
      /// </summary>
      /// <param name="data">The <see cref="HTTPConnectionEndPointConfigurationData"/> to use.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="data"/> is <c>null</c>.</exception>
      /// <seealso cref="HTTPConnectionEndPointConfigurationData"/>
      public HTTPConnectionEndPointConfiguration(
         HTTPConnectionEndPointConfigurationData data
         )
      {
         this.Data = ArgumentValidator.ValidateNotNull( nameof( data ), data );
      }

      /// <summary>
      /// Gets the <see cref="HTTPConnectionEndPointConfigurationData"/> of this <see cref="HTTPConnectionEndPointConfiguration"/>.
      /// </summary>
      /// <value>The <see cref="HTTPConnectionEndPointConfigurationData"/> of this <see cref="HTTPConnectionEndPointConfiguration"/>.</value>
      public HTTPConnectionEndPointConfigurationData Data { get; }

   }

   /// <summary>
   /// This class contains all properties of the <see cref="HTTPConnectionEndPointConfiguration"/> which are considered as raw data.
   /// Thus this class may be used when (de)serializing configuration.
   /// </summary>
   /// <seealso cref="E_HTTP.CreateNetworkStreamFactoryConfiguration(HTTPConnectionEndPointConfigurationData)"/>
   public class HTTPConnectionEndPointConfigurationData
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

   /// <summary>
   /// This is configuration class used to create new <see cref="HTTPConnection"/>s by <see cref="HTTPExtensions.CreateNewHTTPConnection"/>.
   /// </summary>
   /// <remarks>
   /// This class contains all properties which are not considered as raw data, e.g. callbacks.
   /// Currently there are none, but in the future there may be some.
   /// The configuration raw data is in <see cref="HTTPConnectionConfigurationData"/> class.
   /// </remarks>
   /// <seealso cref="HTTPConnectionConfigurationData"/>
   public class HTTPConnectionConfiguration
   {
      /// <summary>
      /// Creates a new instance of <see cref="HTTPConnectionConfiguration"/> with given <see cref="HTTPConnectionConfigurationData"/>.
      /// </summary>
      /// <param name="data">The <see cref="HTTPConnectionConfigurationData"/> to use.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="data"/> is <c>null</c>.</exception>
      /// <seealso cref="HTTPConnectionConfigurationData"/>
      public HTTPConnectionConfiguration(
         HTTPConnectionConfigurationData data
         )
      {
         this.Data = ArgumentValidator.ValidateNotNull( nameof( data ), data );
      }

      /// <summary>
      /// Gets the <see cref="HTTPConnectionConfigurationData"/> of this <see cref="HTTPConnectionConfiguration"/>.
      /// </summary>
      /// <value>The <see cref="HTTPConnectionConfigurationData"/> of this <see cref="HTTPConnectionConfiguration"/>.</value>
      public HTTPConnectionConfigurationData Data { get; }

   }

   /// <summary>
   /// This class contains all properties of the <see cref="HTTPConnectionConfiguration"/> which are considered as raw data.
   /// Thus this class may be used when (de)serializing configuration.
   /// </summary>
   public class HTTPConnectionConfigurationData
   {
      /// <summary>
      /// Gets or sets the maximum buffer size when reading <see cref="HTTPResponse"/> (does not apply for <see cref="HTTPResponseContent.ReadToBuffer"/>).
      /// </summary>
      /// <value>The maximum buffer size when reading <see cref="HTTPResponse"/>.</value>
      public Int32 MaxReadBufferSize { get; set; }

      /// <summary>
      /// Gets or sets the maximum buffer size when writing <see cref="HTTPRequest"/> (does also apply for <see cref="HTTPRequestContent.WriteToStream"/> via size of <see cref="HTTPWriter.Buffer"/> of <see cref="HTTPWriter"/>).
      /// </summary>
      /// <value>The maximum buffer size when writing <see cref="HTTPRequest"/>.</value>
      public Int32 MaxWriteBufferSize { get; set; }
   }
}
