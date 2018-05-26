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

   /// <summary>
   /// The primary purpose of this class is to bind instance of <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> and provide binding public connection type via <see cref="BindPublicConnectionType"/> method by using just one type parameter.
   /// </summary>
   /// <typeparam name="TConnectionCreationParameters">The type of creation parameters, must be subtype of <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
   /// <typeparam name="TCreationData">The type of creation parameters passive data, must be subtype of <see cref="NetworkConnectionCreationInfoData{TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
   /// <typeparam name="TConnectionConfiguration">The type of connection configuration passive data, must be subtype of <see cref="NetworkConnectionConfiguration"/>.</typeparam>
   /// <typeparam name="TInitializationConfiguration">The type of initialization configuration passive data, must be subtype of <see cref="NetworkInitializationConfiguration{TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
   /// <typeparam name="TProtocolConfiguration">The type of protocol configuration passive data.</typeparam>
   /// <typeparam name="TPoolingConfiguration">The type of connection pool configuration passive data, must be subtype of <see cref="NetworkPoolingConfiguration"/>.</typeparam>
   /// <seealso cref="BindPublicConnectionType"/>
   /// <seealso cref="ConnectionCreationParametersAndPublicTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection}"/>
   public sealed class ConnectionCreationParametersTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
   {
      private readonly TConnectionCreationParameters _creationParameters;

      /// <summary>
      /// Creates a new instance of of <see cref="ConnectionCreationParametersTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> with given connection creation parameters.
      /// </summary>
      /// <param name="creationParameters">The connection creation parameters.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="creationParameters"/> is <c>null</c>.</exception>
      public ConnectionCreationParametersTypeBinder(
         TConnectionCreationParameters creationParameters
         )
      {
         this._creationParameters = ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );
      }

      /// <summary>
      /// Given one generic type argument, creates an object which can be used to create <see cref="AsyncResourceFactory{TResource}"/>.
      /// </summary>
      /// <typeparam name="TConnection">The public type of connection.</typeparam>
      /// <returns>An object which can be used to create <see cref="AsyncResourceFactory{TResource}"/>.</returns>
      /// <seealso cref="ConnectionCreationParametersAndPublicTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection}"/>
      public ConnectionCreationParametersAndPublicTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection> BindPublicConnectionType<TConnection>()
         where TConnection : class
      {
         return new ConnectionCreationParametersAndPublicTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection>(
            this._creationParameters
            );
      }

   }

   /// <summary>
   /// This class is typically acquired from <see cref="ConnectionCreationParametersTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>, and it provides a way to create <see cref="AsyncResourceFactory{TResource}"/> by specifying only two type parameters to creation methods.
   /// </summary>
   /// <typeparam name="TConnectionCreationParameters">The type of creation parameters, must be subtype of <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
   /// <typeparam name="TCreationData">The type of creation parameters passive data, must be subtype of <see cref="NetworkConnectionCreationInfoData{TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
   /// <typeparam name="TConnectionConfiguration">The type of connection configuration passive data, must be subtype of <see cref="NetworkConnectionConfiguration"/>.</typeparam>
   /// <typeparam name="TInitializationConfiguration">The type of initialization configuration passive data, must be subtype of <see cref="NetworkInitializationConfiguration{TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
   /// <typeparam name="TProtocolConfiguration">The type of protocol configuration passive data.</typeparam>
   /// <typeparam name="TPoolingConfiguration">The type of connection pool configuration passive data, must be subtype of <see cref="NetworkPoolingConfiguration"/>.</typeparam>
   /// <typeparam name="TConnection">The public type of connection.</typeparam>
   public sealed class ConnectionCreationParametersAndPublicTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection>
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
      where TConnection : class
   {
      private readonly TConnectionCreationParameters _creationParameters;

      /// <summary>
      /// Creates a new instance of <see cref="ConnectionCreationParametersAndPublicTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration, TConnection}"/> with given creation parameters.
      /// </summary>
      /// <param name="creationParameters">The connection creation parameters.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="creationParameters"/> is <c>null</c>.</exception>
      public ConnectionCreationParametersAndPublicTypeBinder(
         TConnectionCreationParameters creationParameters
         )
      {
         this._creationParameters = ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );
      }

      /// <summary>
      /// Creates a new instance of <see cref="AsyncResourceFactory{TResource}"/> with behaviour customizable by given callbacks.
      /// </summary>
      /// <typeparam name="TPrivateConnection">The actual, usually internal, type of connection.</typeparam>
      /// <typeparam name="TConnectionFunctionality">The type of connection functionality, which usually contains the actual protocol implementation.</typeparam>
      /// <param name="encodingInfo">The <see cref="IEncodingInfo"/> to use when (de)serializing strings.</param>
      /// <param name="isSSLPossible">The callback to check if remote end supports SSL.</param>
      /// <param name="noSSLStreamProvider">The callback to create an exception when there was not possible to create SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="remoteNoSSLSupport">The callback to create an exception when the remote does not support SSL. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoStream">The callback to create an exception when the SSL stream creation callback did not return SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoAuthenticationCallback">The callback to create an exception when the SSL stream creation callback did not return authentication validation callback. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamOtherError">The callback to create an exception when other error occurs during SSL stream initialization. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="createFunctionality">The callback to create the connection functionality of type <typeparamref name="TConnectionFunctionality"/>.</param>
      /// <param name="createConnection">The callback to create connection after connection functionality has been created.</param>
      /// <param name="createConnectionAcquireInfo">The callback to create connection acquire info after connection functionality and connection have been created.</param>
      /// <param name="extractStreamOnConnectionAcquirementError">The callback to extract stream from connection acquire info when something goes wrong.</param>
      /// <param name="dedicatedStringPoolNeedsToBeConcurrent">Optional boolean indicating whether per-connection dedicated string pools, if used, need to be concurrent.</param>
      /// <returns>A <see cref="AsyncResourceFactory{TResource}"/> which uses sockets and network stream underneath, and has customized behaviour based on given callbacks.</returns>
      /// <remarks>
      /// The returned <see cref="AsyncResourceFactory{TResource}"/> is <see cref="DelegatingStatelessProtocolConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.
      /// </remarks>
      public AsyncResourceFactory<TConnection> CreateStatelessDelegatingConnectionFactory<TPrivateConnection, TConnectionFunctionality>(
         IEncodingInfo encodingInfo,
         IsSSLPossibleDelegate<TConnectionCreationParameters> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters> createFunctionality,
         CreatePrivateConnectionDelegate<TConnectionFunctionality, TPrivateConnection> createConnection,
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

      /// <summary>
      /// Creates a new instance of <see cref="AsyncResourceFactory{TResource}"/> with behaviour customizable by given callbacks, which have access to the intermediate state existing between invocations.
      /// </summary>
      /// <typeparam name="TPrivateConnection">The actual, usually internal, type of connection.</typeparam>
      /// <typeparam name="TConnectionFunctionality">The type of connection functionality, which usually contains the actual protocol implementation.</typeparam>
      /// <typeparam name="TIntermediateState">The type of intermediate state.</typeparam>
      /// <param name="encodingInfo">The <see cref="IEncodingInfo"/> to use when (de)serializing strings.</param>
      /// <param name="createIntermediateState">The callback to create intermediate state.</param>
      /// <param name="isSSLPossible">The callback to check if remote end supports SSL.</param>
      /// <param name="noSSLStreamProvider">The callback to create an exception when there was not possible to create SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="remoteNoSSLSupport">The callback to create an exception when the remote does not support SSL. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoStream">The callback to create an exception when the SSL stream creation callback did not return SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoAuthenticationCallback">The callback to create an exception when the SSL stream creation callback did not return authentication validation callback. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamOtherError">The callback to create an exception when other error occurs during SSL stream initialization. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="createFunctionality">The callback to create the connection functionality of type <typeparamref name="TConnectionFunctionality"/>.</param>
      /// <param name="createConnection">The callback to create connection after connection functionality has been created.</param>
      /// <param name="createConnectionAcquireInfo">The callback to create connection acquire info after connection functionality and connection have been created.</param>
      /// <param name="extractStreamOnConnectionAcquirementError">The callback to extract stream from connection acquire info when something goes wrong.</param>
      /// <param name="dedicatedStringPoolNeedsToBeConcurrent">Optional boolean indicating whether per-connection dedicated string pools, if used, need to be concurrent.</param>
      /// <returns>A <see cref="AsyncResourceFactory{TResource}"/> which uses sockets and network stream underneath, and has customized behaviour based on given callbacks.</returns>
      /// <remarks>
      /// The returned <see cref="AsyncResourceFactory{TResource}"/> is <see cref="DelegatingStatefulProtocolConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.
      /// </remarks>
      public AsyncResourceFactory<TConnection> CreateStatefulDelegatingConnectionFactory<TPrivateConnection, TConnectionFunctionality, TIntermediateState>(
         IEncodingInfo encodingInfo,
         CreateIntermediateStateDelegate<TConnectionCreationParameters, TIntermediateState> createIntermediateState,
         IsSSLPossibleDelegate<TConnectionCreationParameters, TIntermediateState> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         CreateConnectionFunctionality<TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters> createFunctionality,
         CreatePrivateConnectionDelegate<TConnectionFunctionality, TPrivateConnection> createConnection,
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

   /// <summary>
   /// This delegate is used to potentially asynchronously create a CBAM connection from close-to-protocol connection functionality.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The type of close-to-protocol connection functionality.</typeparam>
   /// <typeparam name="TPrivateConnection">The type of connection.</typeparam>
   /// <param name="connectionFunctionality">The close-to-protocol connection functionality.</param>
   /// <returns>Potentially asynchronously creates a CBAM connection.</returns>
   public delegate ValueTask<TPrivateConnection> CreatePrivateConnectionDelegate<TConnectionFunctionality, TPrivateConnection>( TConnectionFunctionality connectionFunctionality );

   /// <summary>
   /// This delegate is used to create a new instance of <see cref="AsyncResourceAcquireInfo{TResource}"/> once the close-to-protocol connection functionality and CBAM connection have been created.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The type of close-to-protocol connection functionality.</typeparam>
   /// <typeparam name="TPrivateConnection">The type of connection.</typeparam>
   /// <param name="connectionFunctionality">The close-to-protocol connection functionality.</param>
   /// <param name="privateConnection">The CBAM connection.</param>
   /// <returns>A new instance of <see cref="AsyncResourceAcquireInfo{TResource}"/>.</returns>
   /// <seealso cref="StatelessConnectionAcquireInfo{TConnection, TConnectionFunctionality, TStream}"/>
   /// <seealso cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStream}"/>
   public delegate AsyncResourceAcquireInfo<TPrivateConnection> CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection>( TConnectionFunctionality connectionFunctionality, TPrivateConnection privateConnection );

   /// <summary>
   /// This delegate is used to extract stream or other <see cref="IDisposable"/> after an error occurs during new connection creation or protocol initialization.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The type of close-to-protocol connection functionality.</typeparam>
   /// <typeparam name="TPrivateConnection">The type of connection.</typeparam>
   /// <param name="connectionFunctionality">The close-to-protocol connection functionality.</param>
   /// <param name="privateConnection">The CBAM connection.</param>
   /// <param name="token">The <see cref="CancellationToken"/>.</param>
   /// <param name="error">The error that occurred.</param>
   /// <returns></returns>
   public delegate IDisposable ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection>( TConnectionFunctionality connectionFunctionality, TPrivateConnection privateConnection, CancellationToken token, Exception error );


   /// <summary>
   /// This class contains extension methods for types defined in other assemblies.
   /// </summary>
   public static partial class CBAMExtensions
   {
      /// <summary>
      /// Helper method to easily create <see cref="ConnectionCreationParametersTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> without using generic parameters.
      /// Note: Currently, the generic parameters are for some reason required. Need to investigate whether the problem is in the code or the compiler.
      /// </summary>
      /// <typeparam name="TConnectionCreationParameters">The type of creation parameters, must be subtype of <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
      /// <typeparam name="TCreationData">The type of creation parameters passive data, must be subtype of <see cref="NetworkConnectionCreationInfoData{TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
      /// <typeparam name="TConnectionConfiguration">The type of connection configuration passive data, must be subtype of <see cref="NetworkConnectionConfiguration"/>.</typeparam>
      /// <typeparam name="TInitializationConfiguration">The type of initialization configuration passive data, must be subtype of <see cref="NetworkInitializationConfiguration{TProtocolConfiguration, TPoolingConfiguration}"/>.</typeparam>
      /// <typeparam name="TProtocolConfiguration">The type of protocol configuration passive data.</typeparam>
      /// <typeparam name="TPoolingConfiguration">The type of connection pool configuration passive data, must be subtype of <see cref="NetworkPoolingConfiguration"/>.</typeparam>
      /// <param name="creationParameters">This <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</param>
      /// <returns>A new instance of <see cref="ConnectionCreationParametersTypeBinder{TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.</returns>
      /// <exception cref="NullReferenceException">If this <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> is <c>null</c>.</exception>
      public static ConnectionCreationParametersTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> NewFactoryParametrizer<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
         this TConnectionCreationParameters creationParameters
         )
         where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
         where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
         where TConnectionConfiguration : NetworkConnectionConfiguration
         where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
         where TPoolingConfiguration : NetworkPoolingConfiguration
      {
         return new ConnectionCreationParametersTypeBinder<TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>( ArgumentValidator.ValidateNotNullReference( creationParameters ) );
      }
   }
}
