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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.ResourcePooling;
using UtilPack.ResourcePooling.NetworkStream;
using UtilPack.Configuration.NetworkStream;

namespace CBAM.NATS.Implementation
{



   using TNetworkStreamInitState = ValueTuple<ClientProtocol.ReadState, Reference<ServerInformation>, CancellationToken, Stream>;

   internal sealed class ClientProtocolPoolInfo : PooledConnectionFunctionality
   {

      private Object _cancellationToken;

      public ClientProtocolPoolInfo( ClientProtocol protocol )
      {
         //this.Socket = ArgumentValidator.ValidateNotNull( nameof( socket ), socket );
         this.Protocol = ArgumentValidator.ValidateNotNull( nameof( protocol ), protocol );
      }

      public ClientProtocol Protocol { get; }

      //public Socket Socket { get; }

      public CancellationToken CurrentCancellationToken
      {
         get => (CancellationToken) this._cancellationToken;
         set => Interlocked.Exchange( ref this._cancellationToken, value );
      }

      public Boolean CanBeReturnedToPool => this.Protocol.CanBeReturnedToPool;

      public void ResetCancellationToken()
      {
         this._cancellationToken = null;
      }
   }

   internal sealed class NATSConnectionFactory : ConnectionFactorySU<NATSConnection, NATSConnectionImpl, NATSConnectionCreationInfo, ClientProtocolPoolInfo>
   {
      private readonly BinaryStringPool _stringPool;
      private readonly ReadOnlyResettableAsyncLazy<IPAddress> _remoteAddress;
      private readonly NetworkStreamFactoryConfiguration<TNetworkStreamInitState> _networkStreamConfig;

      public NATSConnectionFactory(
         NATSConnectionCreationInfo creationInfo,
         IEncodingInfo encoding
         ) : base( creationInfo )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationInfo ), creationInfo );
         this.EncodingInfo = ArgumentValidator.ValidateNotNull( nameof( encoding ), encoding );

#if NETSTANDARD1_0
         if ( !( creationInfo.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? default ) )
         {
            this._stringPool = BinaryStringPoolFactory.NewConcurrentBinaryStringPool( encoding.Encoding );
         }
#else
#if !NETSTANDARD1_0
         (this._networkStreamConfig, this._remoteAddress, this._stringPool) = creationInfo.CreateStatefulNetworkStreamFactoryConfiguration().Create(
            ( socket, stream, token ) => this.CreateNetworkStreamInitState( token, stream ),
            encoding.Encoding,
            async ( state ) =>
            {
               // First, read the INFO message from server
               var rState = state.Item1;
               var buffer = rState.Buffer;
               var aState = rState.BufferAdvanceState;
               await state.Item4.ReadUntilMaybeAsync( buffer, aState, ClientProtocolConsts.CRLF, ClientProtocolConsts.READ_COUNT );
               var array = buffer.Array;
               var idx = 0;
               var end = aState.BufferOffset;
               if ( end < 7
               || ( array.ReadInt32BEFromBytes( ref idx ) & ClientProtocolConsts.UPPERCASE_MASK_FULL ) != ClientProtocolConsts.INFO_INT
               || ( array[idx] != ClientProtocolConsts.SPACE
                  && array[idx] != ClientProtocolConsts.TAB )
               )
               {
                  throw new NATSException( "Invalid INFO message at startup." );
               }
               ++idx;

               var serverInfo = ClientProtocol.DeserializeInfoMessage( array, idx, end - idx, NATSAuthenticationConfiguration.PasswordByteEncoding );
               state.Item2.Value = serverInfo;
               var sslMode = creationInfo.CreationData.Connection?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired;

               if ( serverInfo.SSLRequired && sslMode == ConnectionSSLMode.NotRequired )
               {
                  throw new NATSException( "Server requires SSL, but client does not." );
               }
               else if ( !serverInfo.SSLRequired && sslMode == ConnectionSSLMode.Required )
               {
                  throw new NATSException( "Client requires SSL, but server does not." );
               }
               else if ( serverInfo.AuthenticationRequired )
               {
                  throw new NotImplementedException();
               }

               // We should not receive anything else except info message at start, but let's just make sure we leave anything extra still to be visible to client protocol
               ClientProtocol.SetPreReadLength( rState );

               return serverInfo.AuthenticationRequired; // sslMode != ConnectionSSLMode.NotRequired && creationInfo.ProvideSSLStream != null;
            },
            () => new NATSException( "Server accepted SSL request, but the creation parameters did not have callback to create SSL stream" ),
            () => new NATSException( "Server does not support SSL." ),
            () => new NATSException( "SSL stream creation callback returned null." ),
            () => new NATSException( "Authentication callback given by SSL stream creation callback was null." ),
            inner => new NATSException( "Unable to start SSL client.", inner )
            );
         // Make NetworkStreamFactory<TNetworkStreamInitState>.AcquireNetworkStreamFromConfiguration always call our isSSLPossible callback
         this._networkStreamConfig.ConnectionSSLMode = _ => ConnectionSSLMode.Preferred;
#endif
#endif
      }

      public override void ResetFactoryState()
      {
         this._stringPool.ClearPool();
#if !NETSTANDARD1_0
         this._remoteAddress.Reset();
#endif
      }

      protected override async ValueTask<ClientProtocolPoolInfo> CreateConnectionFunctionality( CancellationToken token )
      {
         var parameters = this.CreationParameters;
         var streamFactory = parameters.StreamFactory;

#if !NETSTANDARD1_0
         System.Net.Sockets.Socket socket;
#endif
         Stream stream;
         TNetworkStreamInitState state;
         if ( streamFactory == null )
         {
#if NETSTANDARD1_0
            throw new ArgumentNullException( nameof( streamFactory ) );
#else
            (socket, stream, state) = await NetworkStreamFactory<TNetworkStreamInitState>.AcquireNetworkStreamFromConfiguration(
                  this._networkStreamConfig,
                  token );
#endif
         }
         else
         {
            (
#if !NETSTANDARD1_0
               socket,
#endif
               stream, state) = (
#if !NETSTANDARD1_0
               null,
#endif
               await streamFactory(), this.CreateNetworkStreamInitState( token, null ));
         }


         // TODO Send CONNECT here
         var paramData = parameters.CreationData;
         var initConfig = paramData.Initialization;
         var protoConfig = initConfig?.Protocol ?? new NATSProtocolConfiguration();
         var authConfig = initConfig?.Authentication;
         var wState = new ClientProtocol.WriteState();
         var serverInfo = state.Item2.Value;
         await ClientProtocol.InitializeNewConnection( new ClientInformation()
         {
            IsVerbose = protoConfig.Verbose,
            IsPedantic = protoConfig.Pedantic,
            SSLRequired = serverInfo.SSLRequired,
            AuthenticationToken = authConfig?.AuthenticationToken,
            Username = authConfig?.Username,
            Password = authConfig?.Password,
            ClientName = protoConfig.ClientName,
            ClientLanguage = protoConfig.ClientLanguage,
            ClientVersion = protoConfig.ClientVersion
         }, NATSAuthenticationConfiguration.PasswordByteEncoding, wState, stream, token );

         return new ClientProtocolPoolInfo( new ClientProtocol( new ClientProtocol.ClientProtocolIOState(
            new DuplexBufferedAsyncStream( stream, Math.Max( NATSProtocolConfiguration.DEFAULT_BUFFER_SIZE, protoConfig.StreamBufferSize ) ),
            this._stringPool,
            this.EncodingInfo,
            wState,
            state.Item1
            ), serverInfo ) );

      }


      protected override ValueTask<NATSConnectionImpl> CreateConnection( ClientProtocolPoolInfo functionality )
      {
         return new ValueTask<NATSConnectionImpl>( new NATSConnectionImpl( NATSConnectionVendorFunctionalityImpl.Instance, functionality.Protocol ) );
      }

      protected override AsyncResourceAcquireInfo<NATSConnectionImpl> CreateConnectionAcquireInfo( ClientProtocolPoolInfo functionality, NATSConnectionImpl connection )
      {
         return new NATSConnectionAcquireInfo( connection, functionality );
      }

      protected override IDisposable ExtractStreamOnConnectionAcquirementError( ClientProtocolPoolInfo functionality, NATSConnectionImpl connection, CancellationToken token, Exception error )
      {
         return functionality?.Protocol?.Stream;
      }


      private TNetworkStreamInitState CreateNetworkStreamInitState( CancellationToken token, Stream stream )
      {
         return (new ClientProtocol.ReadState(), new Reference<ServerInformation>(), token, stream);
      }

      private IEncodingInfo EncodingInfo { get; }

      private BinaryStringPool GetStringPoolForNewConnection()
      {
         return this._stringPool ?? BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool();
      }
   }

   internal sealed class NATSConnectionAcquireInfo : ConnectionAcquireInfoImpl<NATSConnectionImpl, ClientProtocolPoolInfo, System.IO.Stream>
   {
      public NATSConnectionAcquireInfo( NATSConnectionImpl connection, ClientProtocolPoolInfo functionality )
         : base( connection, functionality, functionality.Protocol.Stream )
      {
      }

      protected override Task DisposeBeforeClosingChannel( CancellationToken token )
      {
         return TaskUtils.CompletedTask;
      }

      //protected override ResourceUsageInfo<NATSConnectionImpl> CreateResourceUsageInfoWrapper( ResourceUsageInfo<NATSConnectionImpl> resourceUsageInfo, CancellationToken token )
      //{
      //   var info = resourceUsageInfo.Resource.UsageStarted();
      //   return new ResourceUsageInfoWrapper<NATSConnectionImpl>( resourceUsageInfo, connection => connection.UsageEnded( info ) );
      //}
   }

   public class ResourceUsageInfoWrapper<TResource> : AbstractDisposable, ResourceUsageInfo<TResource>
   {
      private readonly ResourceUsageInfo<TResource> _instance;
      private readonly Action<TResource> _onDispose;

      public ResourceUsageInfoWrapper(
         ResourceUsageInfo<TResource> instance,
         Action<TResource> onDispose
         )
      {
         this._instance = ArgumentValidator.ValidateNotNull( nameof( instance ), instance );
         this._onDispose = onDispose;
      }

      public TResource Resource => this._instance.Resource;

      protected override void Dispose( Boolean disposing )
      {
         if ( disposing )
         {
            this._onDispose?.Invoke( this.Resource );
         }
      }
   }

}
