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
   public delegate TIntermediateState CreateIntermediateStateDelegate<TConnectionCreationParameters, TIntermediateState>( TConnectionCreationParameters creationParameters, IEncodingInfo encodingInfo, BinaryStringPool stringPool, Object socketOrNull, Stream stream, CancellationToken token );

   public delegate Task<Boolean> IsSSLPossibleDelegate<TConnectionCreationParameters, TIntermediateState>( TConnectionCreationParameters creationParameters, IEncodingInfo encoding, BinaryStringPool stringPool, TIntermediateState intermediateState );

   public abstract class StatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> : ConnectionFactoryStream<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters>
      where TConnection : class
      where TPrivateConnection : class, TConnection
      where TConnectionFunctionality : class, PooledConnectionFunctionality
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
   {
      private readonly CreateIntermediateStateDelegate<TConnectionCreationParameters, TIntermediateState> _createIntermediateState;
      private readonly Boolean _dedicatedStringPoolNeedsToBeConcurrent;

      public StatefulProtocolConnectionFactory(
         TConnectionCreationParameters creationInfo,
         IEncodingInfo encodingInfo,
         CreateIntermediateStateDelegate<TConnectionCreationParameters, TIntermediateState> createIntermediateState,
         IsSSLPossibleDelegate<TConnectionCreationParameters, TIntermediateState> isSSLPossible,
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
         this._createIntermediateState = ArgumentValidator.ValidateNotNull( nameof( createIntermediateState ), createIntermediateState );
         this._dedicatedStringPoolNeedsToBeConcurrent = dedicatedStringPoolNeedsToBeConcurrent;

         var encoding = encodingInfo.Encoding;
#if NETSTANDARD1_0
         if ( !( creationInfo.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? default ) )
         {
            this.GlobalStringPool = BinaryStringPoolFactory.NewConcurrentBinaryStringPool( encoding );
         }
#else
         (this.NetworkStreamConfiguration, this.RemoteAddress, this.GlobalStringPool) = creationInfo.CreateStatefulNetworkStreamFactoryConfiguration().Create(
            ( socket, stream, token ) =>
            {
               var stringPool = this.GetStringPoolForNewConnection();

               return (stringPool, this._createIntermediateState( this.CreationParameters, this.Encoding, stringPool, socket, stream, token ));
            },
            encoding,
            state => isSSLPossible?.Invoke( this.CreationParameters, this.Encoding, state.Item1, state.Item2 ),
            noSSLStreamProvider,
            remoteNoSSLSupport,
            sslStreamProviderNoStream,
            sslStreamProviderNoAuthenticationCallback,
            sslStreamOtherError
            );
         this.NetworkStreamConfiguration.TransformStreamAfterCreation = stream => new DuplexBufferedAsyncStream( stream );
#endif
      }

      protected IEncodingInfo Encoding { get; }

      private BinaryStringPool GlobalStringPool { get; }

#if !NETSTANDARD1_0
      protected ReadOnlyResettableAsyncLazy<IPAddress> RemoteAddress;
      protected NetworkStreamFactoryConfiguration<(BinaryStringPool, TIntermediateState)> NetworkStreamConfiguration;
#endif


      public override void ResetFactoryState()
      {
         this.GlobalStringPool?.ClearPool();
#if !NETSTANDARD1_0
         this.RemoteAddress.Reset();
#endif
      }

      private BinaryStringPool GetStringPoolForNewConnection()
      {
         return this.GlobalStringPool ?? ( this._dedicatedStringPoolNeedsToBeConcurrent ? BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool() : BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool() );
      }

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
         (BinaryStringPool, TIntermediateState) state;
         if ( streamFactory == null )
         {
#if NETSTANDARD1_0
            throw new ArgumentNullException( nameof( streamFactory ) );
#else
            (socket, stream, state) = await NetworkStreamFactory<(BinaryStringPool, TIntermediateState)>.AcquireNetworkStreamFromConfiguration(
                  this.NetworkStreamConfiguration,
                  token );
#endif
         }
         else
         {
            socket = null;
            stream = await streamFactory();
            state = (this.GetStringPoolForNewConnection(), this._createIntermediateState( this.CreationParameters, this.Encoding, this.GetStringPoolForNewConnection(), null, stream, token ));
         }

         return await this.CreateFunctionality(
            state.Item1,
            stream,
            socket,
            token,
            state.Item2
            );
      }

      protected abstract ValueTask<TConnectionFunctionality> CreateFunctionality(
         BinaryStringPool stringPool,
         Stream acquiredStream,
         Object socketOrNull,
         CancellationToken token,
         TIntermediateState intermediateState
         );
   }

   public delegate ValueTask<TConnectionFunctionality> CreateConnectionFunctionality<TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters>( TConnectionCreationParameters creationParameters, IEncodingInfo encoding, BinaryStringPool stringPool, Stream stream, Object socketOrNull, CancellationToken token, TIntermediateState intermediateState );

   public sealed class DelegatingStatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> : StatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnection : class
      where TPrivateConnection : class, TConnection
      where TConnectionFunctionality : class, PooledConnectionFunctionality
      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
      where TConnectionConfiguration : NetworkConnectionConfiguration
      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
      where TPoolingConfiguration : NetworkPoolingConfiguration
   {
      private readonly CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> _createConnection;
      private readonly CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> _createConnectionAcquireInfo;
      private readonly CreateConnectionFunctionality<TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters> _createFunctionality;
      private readonly ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> _extractStreamOnConnectionAcquirementError;

      public DelegatingStatefulProtocolConnectionFactory(
         TConnectionCreationParameters creationInfo,
         IEncodingInfo encodingInfo,
         CreateIntermediateStateDelegate<TConnectionCreationParameters, TIntermediateState> createIntermediateState,
         IsSSLPossibleDelegate<TConnectionCreationParameters, TIntermediateState> isSSLPossible,
         Func<Exception> noSSLStreamProvider,
         Func<Exception> remoteNoSSLSupport,
         Func<Exception> sslStreamProviderNoStream,
         Func<Exception> sslStreamProviderNoAuthenticationCallback,
         Func<Exception, Exception> sslStreamOtherError,
         Boolean dedicatedStringPoolNeedsToBeConcurrent,
         CreateConnectionFunctionality<TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters> createFunctionality,
         CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> createConnection,
         CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
         ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError
         ) : base( creationInfo, encodingInfo, createIntermediateState, isSSLPossible, noSSLStreamProvider, remoteNoSSLSupport, sslStreamProviderNoStream, sslStreamProviderNoAuthenticationCallback, sslStreamOtherError, dedicatedStringPoolNeedsToBeConcurrent )
      {
         this._createConnection = ArgumentValidator.ValidateNotNull( nameof( createConnection ), createConnection );
         this._createConnectionAcquireInfo = ArgumentValidator.ValidateNotNull( nameof( createConnectionAcquireInfo ), createConnectionAcquireInfo );
         this._createFunctionality = ArgumentValidator.ValidateNotNull( nameof( createFunctionality ), createFunctionality );
         this._extractStreamOnConnectionAcquirementError = ArgumentValidator.ValidateNotNull( nameof( extractStreamOnConnectionAcquirementError ), extractStreamOnConnectionAcquirementError );
      }

      protected override ValueTask<TPrivateConnection> CreateConnection( TConnectionFunctionality functionality )
         => this._createConnection( functionality );

      protected override AsyncResourceAcquireInfo<TPrivateConnection> CreateConnectionAcquireInfo( TConnectionFunctionality functionality, TPrivateConnection connection )
         => this._createConnectionAcquireInfo( functionality, connection );

      protected override ValueTask<TConnectionFunctionality> CreateFunctionality( BinaryStringPool stringPool, Stream acquiredStream, Object socketOrNull, CancellationToken token, TIntermediateState intermediateState )
         => this._createFunctionality( this.CreationParameters, this.Encoding, stringPool, acquiredStream, socketOrNull, token, intermediateState );

      protected override IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TPrivateConnection connection, CancellationToken token, Exception error )
         => this._extractStreamOnConnectionAcquirementError( functionality, connection, token, error );
   }



}

public static partial class E_CBAM
{
   //public static DelegatingStatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> CreateStatefulDelegatingConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
   //      this TConnectionCreationParameters creationInfo,
   //      IEncodingInfo encodingInfo,
   //      CreateIntermediateStateDelegate<TIntermediateState> createIntermediateState,
   //      Func<TIntermediateState, Task<Boolean>> isSSLPossible,
   //      Func<Exception> noSSLStreamProvider,
   //      Func<Exception> remoteNoSSLSupport,
   //      Func<Exception> sslStreamProviderNoStream,
   //      Func<Exception> sslStreamProviderNoAuthenticationCallback,
   //      Func<Exception, Exception> sslStreamOtherError,
   //      CreateConnectionFunctionality<TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters> createFunctionality,
   //      CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> createConnection,
   //      CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
   //      ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError,
   //      TypeMarker<TConnection> marker
   //   )
   //   where TConnection : class
   //   where TPrivateConnection : class, TConnection
   //   where TConnectionFunctionality : class, PooledConnectionFunctionality
   //   where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
   //   where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
   //   where TConnectionConfiguration : NetworkConnectionConfiguration
   //   where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
   //   where TPoolingConfiguration : NetworkPoolingConfiguration
   //{
   //   return new DelegatingStatefulProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TIntermediateState, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
   //      creationInfo,
   //      encodingInfo,
   //      createIntermediateState,
   //      isSSLPossible,
   //      noSSLStreamProvider,
   //      remoteNoSSLSupport,
   //      sslStreamProviderNoStream,
   //      sslStreamProviderNoAuthenticationCallback,
   //      sslStreamOtherError,
   //      createFunctionality,
   //      createConnection,
   //      createConnectionAcquireInfo,
   //      extractStreamOnConnectionAcquirementError
   //      );
   //}
}