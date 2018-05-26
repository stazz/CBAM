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
   /// This class extends <see cref="ConnectionFactoryStream{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters}"/> in order to implement socket and stream creation using <see cref="T:UtilPack.ResourcePooling.NetworkStream.NetworkStreamFactory"/>, such that there is no intermediate state used during connection initialization.
   /// Notice that this need for state does not always correlate whether the underlying protocol itself is stateless or stateful.
   /// </summary>
   /// <typeparam name="TConnection">The public type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/>.</typeparam>
   /// <typeparam name="TPrivateConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable, TEnumerableObservable, TActualVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TCreationData">The type holding passive data about the remote endpoint and protocol configuration.</typeparam>
   /// <typeparam name="TConnectionConfiguration">The type holding passive data about the socket connection.</typeparam>
   /// <typeparam name="TInitializationConfiguration">The type holding passive data about protocol initialization.</typeparam>
   /// <typeparam name="TProtocolConfiguration">The type holding passive data about protocol.</typeparam>
   /// <typeparam name="TPoolingConfiguration">The type holding passive data about pooling behaviour.</typeparam>
   public abstract class StatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> : ConnectionFactoryStream<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters>
      where TConnection : class
      where TPrivateConnection : class, TConnection
      where TConnectionFunctionality : class, PooledConnectionFunctionality
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
   {

      private readonly Boolean _dedicatedStringPoolNeedsToBeConcurrent;

      /// <summary>
      /// Creates a new instance of <see cref="StatelessProtocolConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/>.
      /// </summary>
      /// <param name="creationInfo">The connection creation parameters.</param>
      /// <param name="encodingInfo">The <see cref="IEncodingInfo"/> to use when (de)serializing strings.</param>
      /// <param name="isSSLPossible">The callback to check if remote end supports SSL.</param>
      /// <param name="noSSLStreamProvider">The callback to create an exception when there was not possible to create SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="remoteNoSSLSupport">The callback to create an exception when the remote does not support SSL. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoStream">The callback to create an exception when the SSL stream creation callback did not return SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoAuthenticationCallback">The callback to create an exception when the SSL stream creation callback did not return authentication validation callback. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamOtherError">The callback to create an exception when other error occurs during SSL stream initialization. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="dedicatedStringPoolNeedsToBeConcurrent">A boolean indicating whether per-connection dedicated string pools, if used, need to be concurrent.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="creationInfo"/> or <paramref name="encodingInfo"/> is <c>null</c>.</exception>
      public StatelessProtocolConnectionFactory(
         TConnectionCreationParameters creationInfo,
         IEncodingInfo encodingInfo,
         IsSSLPossibleDelegate<TConnectionCreationParameters> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         Boolean dedicatedStringPoolNeedsToBeConcurrent
         ) : base( creationInfo )
      {
         this.Encoding = ArgumentValidator.ValidateNotNull( nameof( encodingInfo ), encodingInfo );
         ArgumentValidator.ValidateNotNull( nameof( creationInfo ), creationInfo );
         this._dedicatedStringPoolNeedsToBeConcurrent = dedicatedStringPoolNeedsToBeConcurrent;

         var encoding = encodingInfo.Encoding;
#if NETSTANDARD1_0
         if ( !( creationInfo.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? default ) )
         {
            this.GlobalStringPool = BinaryStringPoolFactory.NewConcurrentBinaryStringPool( encoding );
         }
#else
         (this.NetworkStreamConfiguration, this.RemoteAddress, this.GlobalStringPool) = creationInfo.CreateStatefulNetworkStreamFactoryConfiguration().Create(
            ( socket, stream, token ) => this.GetStringPoolForNewConnection(),
            encoding,
            state => isSSLPossible?.Invoke( this.CreationParameters, this.Encoding, state, this.IsDedicatedStringPool( state ) ),
            noSSLStreamProvider,
            remoteNoSSLSupport,
            sslStreamProviderNoStream,
            sslStreamProviderNoAuthenticationCallback,
            sslStreamOtherError
            );
         this.NetworkStreamConfiguration.TransformStreamAfterCreation = stream => new DuplexBufferedAsyncStream( stream );
#endif
      }

      /// <summary>
      /// Gets the <see cref="IEncodingInfo"/> used to (de)serialize strings.
      /// </summary>
      /// <value>The <see cref="IEncodingInfo"/> used to (de)serialize strings.</value>
      protected IEncodingInfo Encoding { get; }

      /// <summary>
      /// Gets the <see cref="BinaryStringPool"/> shared between all connections created by this factory. May be <c>null</c>.
      /// </summary>
      /// <value>the <see cref="BinaryStringPool"/> shared between all connections created by this factory.</value>
      protected BinaryStringPool GlobalStringPool { get; }

      /// <summary>
      /// Helper method to check whether the <see cref="BinaryStringPool"/> is not <c>null</c> and is dedicated to the connection.
      /// </summary>
      /// <param name="stringPool">The <see cref="BinaryStringPool"/> to check.</param>
      /// <returns><c>true</c> if given <paramref name="stringPool"/> is not <c>null</c> and is dedicated to the connection, <c>false</c> otherwise.</returns>
      protected Boolean IsDedicatedStringPool( BinaryStringPool stringPool )
      {
         return stringPool != null && !ReferenceEquals( this.GlobalStringPool, stringPool );
      }

#if !NETSTANDARD1_0

      /// <summary>
      /// Gets the lazily asynchronous <see cref="IPAddress"/> of the remote endpoint. May be <c>null</c> (if e.g. connection to Unix domain socket).
      /// </summary>
      /// <value>The lazily asynchronous <see cref="IPAddress"/> of the remote endpoint.</value>
      protected ReadOnlyResettableAsyncLazy<IPAddress> RemoteAddress { get; }

      /// <summary>
      /// Gets the <see cref="NetworkStreamFactoryConfiguration{TState}"/> that is used by the factory when creating new connections.
      /// </summary>
      /// <value>The <see cref="NetworkStreamFactoryConfiguration{TState}"/> that is used by the factory when creating new connections.</value>
      /// <remarks>
      /// This configuration is stateful, as the factory needs to hold on possibly connection-specific <see cref="BinaryStringPool"/> during initialization.
      /// </remarks>
      protected NetworkStreamFactoryConfiguration<BinaryStringPool> NetworkStreamConfiguration { get; }

#endif

      /// <summary>
      /// Implements <see cref="ResourceFactoryInformation.ResetFactoryState"/> and resets this string pool, and remote address lazy, if these are in use.
      /// </summary>
      public override void ResetFactoryState()
      {
         this.GlobalStringPool?.ClearPool();
#if !NETSTANDARD1_0
         this.RemoteAddress?.Reset();
#endif
      }

      private BinaryStringPool GetStringPoolForNewConnection()
      {
         return this.GlobalStringPool ?? ( this._dedicatedStringPoolNeedsToBeConcurrent ? BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool() : BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool() );
      }

      /// <summary>
      /// Implements <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters}.CreateConnectionFunctionality(CancellationToken)"/> by utilizing <see cref="M:UtilPack.ResourcePooling.NetworkStream.NetworkStreamFactory`1.AcquireNetworkStreamFromConfiguration(UtilPack.ResourcePooling.NetworkStream.NetworkStreamFactoryConfiguration{`0},System.Threading.CancellationToken)"/> method, or using <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}.StreamFactory"/> callback.
      /// </summary>
      /// <param name="token">The cancellation token to use during initialization process.</param>
      /// <returns>Potentially asynchronously returns an instance of <typeparamref name="TConnectionFunctionality"/>.</returns>
      protected override async ValueTask<TConnectionFunctionality> CreateConnectionFunctionality( CancellationToken token )
      {

         var parameters = this.CreationParameters;
         var streamFactory = parameters.StreamFactory;

#if NETSTANDARD1_0
         Object
#else
         System.Net.Sockets.Socket
#endif
             socket;
         Stream stream;
         BinaryStringPool stringPool;
         if ( streamFactory == null )
         {
#if NETSTANDARD1_0
            throw new ArgumentNullException( nameof( streamFactory ) );
#else
            (socket, stream, stringPool) = await NetworkStreamFactory<BinaryStringPool>.AcquireNetworkStreamFromConfiguration(
                  this.NetworkStreamConfiguration,
                  token );
#endif
         }
         else
         {
            (socket, stream, stringPool) = (null, await streamFactory(), this.GetStringPoolForNewConnection());
         }

         return await this.CreateFunctionality(
            stringPool,
            stream,
            socket,
            token
            );
      }

      /// <summary>
      /// Derived classes should implement this in order to create <typeparamref name="TConnectionFunctionality"/> from given parameters.
      /// </summary>
      /// <param name="stringPool">The possibly dedicated string pool to use for the connection being created.</param>
      /// <param name="acquiredStream">The <see cref="Stream"/> that has been acquired and is ready to use.</param>
      /// <param name="socketOrNull">A socket that has been acquired, or <c>null</c> if <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}.StreamFactory"/> was used to create stream.</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>Potentially asynchronously creates an instance of <typeparamref name="TConnectionFunctionality"/>.</returns>
      protected abstract ValueTask<TConnectionFunctionality> CreateFunctionality(
         BinaryStringPool stringPool,
         Stream acquiredStream,
         Object socketOrNull,
         CancellationToken token
         );
   }

   /// <summary>
   /// This class extends and implements all missing functionality from <see cref="StatelessProtocolConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> by using callbacks that are passed to constructor.
   /// </summary>
   /// <typeparam name="TConnection">The public type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/>.</typeparam>
   /// <typeparam name="TPrivateConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable, TEnumerableObservable, TActualVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TCreationData">The type holding passive data about the remote endpoint and protocol configuration.</typeparam>
   /// <typeparam name="TConnectionConfiguration">The type holding passive data about the socket connection.</typeparam>
   /// <typeparam name="TInitializationConfiguration">The type holding passive data about protocol initialization.</typeparam>
   /// <typeparam name="TProtocolConfiguration">The type holding passive data about protocol.</typeparam>
   /// <typeparam name="TPoolingConfiguration">The type holding passive data about pooling behaviour.</typeparam>
   public sealed class DelegatingStatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> : StatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
     where TConnection : class
     where TPrivateConnection : class, TConnection
     where TConnectionFunctionality : class, PooledConnectionFunctionality
     where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
     where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
     where TConnectionConfiguration : NetworkConnectionConfiguration
     where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
     where TPoolingConfiguration : NetworkPoolingConfiguration
   {
      private readonly CreatePrivateConnectionDelegate<TConnectionFunctionality, TPrivateConnection> _createConnection;
      private readonly CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> _createConnectionAcquireInfo;
      private readonly CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters> _createFunctionality;
      private readonly ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> _extractStreamOnConnectionAcquirementError;

      /// <summary>
      /// Creates a new instance of <see cref="DelegatingStatelessProtocolConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}"/> with given parameters.
      /// </summary>
      /// <param name="creationInfo">The connection creation parameters.</param>
      /// <param name="encodingInfo">The <see cref="IEncodingInfo"/> to use when (de)serializing strings.</param>
      /// <param name="isSSLPossible">The callback to check if remote end supports SSL.</param>
      /// <param name="noSSLStreamProvider">The callback to create an exception when there was not possible to create SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="remoteNoSSLSupport">The callback to create an exception when the remote does not support SSL. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoStream">The callback to create an exception when the SSL stream creation callback did not return SSL stream. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamProviderNoAuthenticationCallback">The callback to create an exception when the SSL stream creation callback did not return authentication validation callback. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="sslStreamOtherError">The callback to create an exception when other error occurs during SSL stream initialization. May be <c>null</c>, then <see cref="InvalidOperationException"/> is used with default message.</param>
      /// <param name="dedicatedStringPoolNeedsToBeConcurrent">A boolean indicating whether per-connection dedicated string pools, if used, need to be concurrent.</param>
      /// <param name="createFunctionality">The callback for <see cref="StatelessProtocolConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}.CreateFunctionality"/> implementation.</param>
      /// <param name="createConnection">The callback for <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters}.CreateConnection"/> implementation.</param>
      /// <param name="createConnectionAcquireInfo">The callback for <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters}.CreateConnectionAcquireInfo"/> implementation.</param>
      /// <param name="extractStreamOnConnectionAcquirementError">The callback for <see cref="ConnectionFactoryStream{TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters}.ExtractStreamOnConnectionAcquirementError"/> implementation.</param>
      /// <exception cref="ArgumentNullException">If any of the <paramref name="creationInfo"/>, <paramref name="encodingInfo"/>, <paramref name="createFunctionality"/>, <paramref name="createConnection"/>, <paramref name="createConnectionAcquireInfo"/>, or <paramref name="extractStreamOnConnectionAcquirementError"/> is <c>null</c>.</exception>
      public DelegatingStatelessProtocolConnectionFactory(
         TConnectionCreationParameters creationInfo,
         IEncodingInfo encodingInfo,
         IsSSLPossibleDelegate<TConnectionCreationParameters> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         Boolean dedicatedStringPoolNeedsToBeConcurrent,
         CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters> createFunctionality,
         CreatePrivateConnectionDelegate<TConnectionFunctionality, TPrivateConnection> createConnection,
         CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
         ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError
         ) : base( creationInfo, encodingInfo, isSSLPossible, noSSLStreamProvider, remoteNoSSLSupport, sslStreamProviderNoStream, sslStreamProviderNoAuthenticationCallback, sslStreamOtherError, dedicatedStringPoolNeedsToBeConcurrent )
      {
         this._createConnection = ArgumentValidator.ValidateNotNull( nameof( createConnection ), createConnection );
         this._createConnectionAcquireInfo = ArgumentValidator.ValidateNotNull( nameof( createConnectionAcquireInfo ), createConnectionAcquireInfo );
         this._createFunctionality = ArgumentValidator.ValidateNotNull( nameof( createFunctionality ), createFunctionality );
         this._extractStreamOnConnectionAcquirementError = ArgumentValidator.ValidateNotNull( nameof( extractStreamOnConnectionAcquirementError ), extractStreamOnConnectionAcquirementError );
      }

      /// <inheritdoc />
      protected override ValueTask<TPrivateConnection> CreateConnection( TConnectionFunctionality functionality )
         => this._createConnection( functionality );

      /// <inheritdoc />
      protected override AsyncResourceAcquireInfo<TPrivateConnection> CreateConnectionAcquireInfo( TConnectionFunctionality functionality, TPrivateConnection connection )
         => this._createConnectionAcquireInfo( functionality, connection );

      /// <inheritdoc />
      protected override ValueTask<TConnectionFunctionality> CreateFunctionality( BinaryStringPool stringPool, Stream acquiredStream, Object socketOrNull, CancellationToken token )
         => this._createFunctionality( this.CreationParameters, this.Encoding, stringPool, this.IsDedicatedStringPool( stringPool ), acquiredStream, socketOrNull, token );

      /// <inheritdoc />
      protected override IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TPrivateConnection connection, CancellationToken token, Exception error )
         => this._extractStreamOnConnectionAcquirementError( functionality, connection, token, error );
   }

   /// <summary>
   /// This delegate is used to create close-to-protocol connection functionality object.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The type of close-to-protocol connection functionality.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of connection cration parameters.</typeparam>
   /// <param name="creationParameters">The connection creation parameters.</param>
   /// <param name="encoding">The <see cref="IEncodingInfo"/> used to (de)serialize strings.</param>
   /// <param name="stringPool">The <see cref="BinaryStringPool"/> to use when deserializing strings.</param>
   /// <param name="stringPoolIsDedicated"><c>true</c> if the <paramref name="stringPool"/> is dedicated to this connection; <c>false</c> if the string pool is shared by all connections created by the same factory.</param>
   /// <param name="stream">The <see cref="Stream"/> to the remote endpoint.</param>
   /// <param name="socketOrNull">The socket to the remote endpoint, or <c>null</c>, if stream was created via <see cref="NetworkConnectionCreationInfo{TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration}.StreamFactory"/> callback.</param>
   /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
   /// <returns>Potentially asynchronously creates the close-to-protocol connection functionality object.</returns>
   public delegate ValueTask<TConnectionFunctionality> CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters>(
      TConnectionCreationParameters creationParameters,
      IEncodingInfo encoding,
      BinaryStringPool stringPool,
      Boolean stringPoolIsDedicated,
      Stream stream,
      Object socketOrNull,
      CancellationToken token
      );

   /// <summary>
   /// This delegate is used to check whether remote end supports SSL, when there is no intermediate state used during connection initialization.
   /// </summary>
   /// <typeparam name="TConnectionCreationParameters">The type of connection creation parameters.</typeparam>
   /// <param name="creationParameters">The connection creation parameters.</param>
   /// <param name="encoding">The <see cref="IEncodingInfo"/> used to (de)serialize strings.</param>
   /// <param name="stringPool">The <see cref="BinaryStringPool"/> to use when deserializing strings.</param>
   /// <param name="stringPoolIsDedicated"><c>true</c> if the <paramref name="stringPool"/> is dedicated to this connection; <c>false</c> if the string pool is shared by all connections created by the same factory.</param>
   /// <returns>Asynchronously checks whether remote endpoint supports using SSL.</returns>
   /// <seealso cref="TaskUtils.False"/>
   /// <seealso cref="TaskUtils.True"/>
   public delegate Task<Boolean> IsSSLPossibleDelegate<TConnectionCreationParameters>(
      TConnectionCreationParameters creationParameters,
      IEncodingInfo encoding,
      BinaryStringPool stringPool,
      Boolean stringPoolIsDedicated
      );


}