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
            state => isSSLPossible?.Invoke( this.CreationParameters, this.Encoding, state ),
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

      protected BinaryStringPool GlobalStringPool { get; }

#if !NETSTANDARD1_0
      protected ReadOnlyResettableAsyncLazy<IPAddress> RemoteAddress;
      protected NetworkStreamFactoryConfiguration<BinaryStringPool> NetworkStreamConfiguration;
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

      protected abstract ValueTask<TConnectionFunctionality> CreateFunctionality(
         BinaryStringPool stringPool,
         Stream acquiredStream,
         Object socketOrNull,
         CancellationToken token
         );
   }

   public delegate ValueTask<TConnectionFunctionality> CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters>( TConnectionCreationParameters creationParameters, IEncodingInfo encoding, BinaryStringPool stringPool, Stream stream, Object socketOrNull, CancellationToken token );
   public delegate ValueTask<TPrivateConnection> CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality>( TConnectionFunctionality connectionFunctionality );
   public delegate AsyncResourceAcquireInfo<TPrivateConnection> CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection>( TConnectionFunctionality connectionFunctionality, TPrivateConnection privateConnection );
   public delegate IDisposable ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection>( TConnectionFunctionality connectionFunctionality, TPrivateConnection privateConnection, CancellationToken token, Exception error );
   public delegate Task<Boolean> IsSSLPossibleDelegate<TConnectionCreationParameters>( TConnectionCreationParameters creationParameters, IEncodingInfo encoding, BinaryStringPool stringPool );

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
      private readonly CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> _createConnection;
      private readonly CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> _createConnectionAcquireInfo;
      private readonly CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters> _createFunctionality;
      private readonly ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> _extractStreamOnConnectionAcquirementError;

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
         CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> createConnection,
         CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
         ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError
         ) : base( creationInfo, encodingInfo, isSSLPossible, noSSLStreamProvider, remoteNoSSLSupport, sslStreamProviderNoStream, sslStreamProviderNoAuthenticationCallback, sslStreamOtherError, dedicatedStringPoolNeedsToBeConcurrent )
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

      protected override ValueTask<TConnectionFunctionality> CreateFunctionality( BinaryStringPool stringPool, Stream acquiredStream, Object socketOrNull, CancellationToken token )
         => this._createFunctionality( this.CreationParameters, this.Encoding, stringPool, acquiredStream, socketOrNull, token );

      protected override IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TPrivateConnection connection, CancellationToken token, Exception error )
         => this._extractStreamOnConnectionAcquirementError( functionality, connection, token, error );
   }
}

//public static partial class E_CBAM
//{
//   public static DelegatingStatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration> CreateStatelessDelegatingConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
//      this TConnectionCreationParameters creationInfo,
//      IEncodingInfo encodingInfo,
//      IsSSLPossibleDelegate<TConnectionCreationParameters> isSSLPossible,
//      Func<Exception> noSSLStreamProvider,
//      Func<Exception> remoteNoSSLSupport,
//      Func<Exception> sslStreamProviderNoStream,
//      Func<Exception> sslStreamProviderNoAuthenticationCallback,
//      Func<Exception, Exception> sslStreamOtherError,
//      CreateConnectionFunctionality<TConnectionFunctionality, TConnectionCreationParameters> createFunctionality,
//      CreatePrivateConnectionDelegate<TPrivateConnection, TConnectionFunctionality> createConnection,
//      CreateConnectionAcquireInfo<TConnectionFunctionality, TPrivateConnection> createConnectionAcquireInfo,
//      ExtractStreamOnConnectionAcquirementErrorDelegate<TConnectionFunctionality, TPrivateConnection> extractStreamOnConnectionAcquirementError,
//      Boolean dedicatedStringPoolNeedsToBeConcurrent = false
//      )
//      where TConnection : class
//      where TPrivateConnection : class, TConnection
//      where TConnectionFunctionality : class, PooledConnectionFunctionality
//      where TConnectionCreationParameters : NetworkConnectionCreationInfo<TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
//      where TCreationData : NetworkConnectionCreationInfoData<TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>
//      where TConnectionConfiguration : NetworkConnectionConfiguration
//      where TInitializationConfiguration : NetworkInitializationConfiguration<TProtocolConfiguration, TPoolingConfiguration>
//      where TPoolingConfiguration : NetworkPoolingConfiguration
//   {
//      return new DelegatingStatelessProtocolConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters, TCreationData, TConnectionConfiguration, TInitializationConfiguration, TProtocolConfiguration, TPoolingConfiguration>(
//         creationInfo,
//         encodingInfo,
//         isSSLPossible,
//         noSSLStreamProvider,
//         remoteNoSSLSupport,
//         sslStreamProviderNoStream,
//         sslStreamProviderNoAuthenticationCallback,
//         sslStreamOtherError,
//         dedicatedStringPoolNeedsToBeConcurrent,
//         createFunctionality,
//         createConnection,
//         createConnectionAcquireInfo,
//         extractStreamOnConnectionAcquirementError
//         );
//   }
//}