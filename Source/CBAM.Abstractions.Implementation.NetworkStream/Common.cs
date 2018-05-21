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
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.Configuration.NetworkStream;
using UtilPack.ResourcePooling;

#if !NETSTANDARD1_0
using UtilPack.ResourcePooling.NetworkStream;
#endif

namespace CBAM.Abstractions.Implementation.NetworkStream
{
   public sealed class ConnectionCreationParametersTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
   {
      private readonly TConnectionCreationParameters _creationParameters;

      public ConnectionCreationParametersTypeBinder(
         TConnectionCreationParameters creationParameters
         )
      {
         this._creationParameters = ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );
      }

      public ConnectionCreationParametersAndPublicTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection> BindPublicConnectionType<TConnection>()
         where TConnection : class
      {
         return new ConnectionCreationParametersAndPublicTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection>(
            this._creationParameters
            );
      }

   }

   public sealed class ConnectionCreationParametersAndPublicTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection>
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
      where TConnection : class
   {
      private readonly TConnectionCreationParameters _creationParameters;

      public ConnectionCreationParametersAndPublicTypeBinder(
         TConnectionCreationParameters creationParameters
         )
      {
         this._creationParameters = ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );
      }

      public DelegatingStatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> CreateStatelessDelegatingConnectionFactory<TPrivateConnection, TConnectionFunctionality>(
         IEncodingInfo encodingInfo,
         IsSSLPossibleDelegate<TConnectionCreationParameters> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters> createFunctionality,
         CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> createConnection,
         CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
         ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError,
         Boolean dedicatedStringPoolNeedsToBeConcurrent = false
         )
         where TPrivateConnection : class, TConnection
         where TConnectionFunctionality : class, PooledConnectionFunctionality
      {
         return new DelegatingStatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
            this._creationParameters,
            encodingInfo,
            isSSLPossible,
            noSSLStreamProvider,
            remoteNoSSLSupport,
            sslStreamProviderNoStream,
            sslStreamProviderNoAuthenticationCallback,
            sslStreamOtherError,
            dedicatedStringPoolNeedsToBeConcurrent,
            createFunctionality,
            createConnection,
            createConnectionAcquireInfo,
            extractStreamOnConnectionAcquirementError
            );
      }


      public DelegatingStatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> CreateStatefulDelegatingConnectionFactory<TPrivateConnection, TConnectionFunctionality, TIntermediateState>(
         IEncodingInfo encodingInfo,
         CreateIntermediateStateDelegate<TConnectionCreationParameters, TIntermediateState> createIntermediateState,
         IsSSLPossibleDelegate<TConnectionCreationParameters, TIntermediateState> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         CreateConnectionFunctionality<TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters> createFunctionality,
         CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> createConnection,
         CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
         ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError,
         Boolean dedicatedStringPoolNeedsToBeConcurrent = false
      )
         where TPrivateConnection : class, TConnection
         where TConnectionFunctionality : class, PooledConnectionFunctionality
      {
         return new DelegatingStatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
            this._creationParameters,
            encodingInfo,
            createIntermediateState,
            isSSLPossible,
            noSSLStreamProvider,
            remoteNoSSLSupport,
            sslStreamProviderNoStream,
            sslStreamProviderNoAuthenticationCallback,
            sslStreamOtherError,
            dedicatedStringPoolNeedsToBeConcurrent,
            createFunctionality,
            createConnection,
            createConnectionAcquireInfo,
            extractStreamOnConnectionAcquirementError
            );
      }
   }

   public static partial class CBAMExtensions
   {
      public static ConnectionCreationParametersTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> NewFactoryParametrizer<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
         this TConnectionCreationParameters creationParameters
         )
         where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
         where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
         where TConnectionConfiguration : NetworkConnectionConfiguration
         where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
         where TPoolingConfiguration : NetworkPoolingConfiguration
      {
         return new ConnectionCreationParametersTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>( creationParameters );
      }
   }
}
