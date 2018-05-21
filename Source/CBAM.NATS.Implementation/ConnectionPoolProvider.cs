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
using System;
using System.Collections.Generic;
using System.Text;
using UtilPack;
using UtilPack.ResourcePooling;
using CBAM.Abstractions.Implementation.NetworkStream;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using UtilPack.Configuration.NetworkStream;

namespace CBAM.NATS.Implementation
{
   using TIntermediateState = ValueTuple<ClientProtocol.ReadState, Reference<ServerInformation>, CancellationToken, Stream>;

   public sealed class NATSConnectionPoolProvider : AbstractAsyncResourceFactoryProvider<NATSConnection, NATSConnectionCreationInfo>
   {
      public static AsyncResourceFactory<NATSConnection, NATSConnectionCreationInfo> Factory { get; } = new DefaultAsyncResourceFactory<NATSConnection, NATSConnectionCreationInfo>( config =>
         config.NewFactoryParametrizer<NATSConnectionCreationInfo, NATSConnectionCreationInfoData, NATSConnectionConfiguration, NATSInitializationConfiguration, NATSProtocolConfiguration, NATSPoolingConfiguration>()
            .BindPublicConnectionType<NATSConnection>()
            .CreateStatefulDelegatingConnectionFactory(
               Encoding.ASCII.CreateDefaultEncodingInfo(),
               ( parameters, encodingInfo, stringPool, socketOrNull, stream, token ) => new TIntermediateState( new ClientProtocol.ReadState(), new Reference<ServerInformation>(), token, stream ),
               async ( parameters, encodingInfo, stringPool, state ) =>
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
                  var sslMode = parameters.CreationData.Connection?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired;
                  var serverNeedsSSL = serverInfo.SSLRequired;

                  if ( serverNeedsSSL && sslMode == ConnectionSSLMode.NotRequired )
                  {
                     throw new NATSException( "Server requires SSL, but client does not." );
                  }
                  else if ( !serverNeedsSSL && sslMode == ConnectionSSLMode.Required )
                  {
                     throw new NATSException( "Client requires SSL, but server does not." );
                  }
                  else if ( serverInfo.AuthenticationRequired )
                  {
                     throw new NotImplementedException();
                  }

                  // We should not receive anything else except info message at start, but let's just make sure we leave anything extra still to be visible to client protocol
                  ClientProtocol.SetPreReadLength( rState );

                  return serverNeedsSSL;
               },
               () => new NATSException( "Server accepted SSL request, but the creation parameters did not have callback to create SSL stream" ),
               () => new NATSException( "Server does not support SSL." ),
               () => new NATSException( "SSL stream creation callback returned null." ),
               () => new NATSException( "Authentication callback given by SSL stream creation callback was null." ),
               inner => new NATSException( "Unable to start SSL client.", inner ),
               async ( parameters, encodingInfo, stringPool, stream, socketOrNull, token, state ) =>
               {
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
                     stringPool,
                     encodingInfo,
                     wState,
                     state.Item1
                     ), serverInfo ) );
               },
               protocol => new ValueTask<NATSConnectionImpl>( new NATSConnectionImpl( NATSConnectionVendorFunctionalityImpl.Instance, protocol.Protocol ) ),
               ( protocol, connection ) => new CBAM.Abstractions.Implementation.StatelessConnectionAcquireInfol<NATSConnectionImpl, ClientProtocolPoolInfo, Stream>( connection, protocol, protocol.Protocol.Stream ),
               ( functionality, connection, token, error ) => functionality.Protocol?.Stream
               ) );


      public NATSConnectionPoolProvider()
         : base( typeof( NATSConnectionCreationInfoData ) )
      {
      }

      protected override AsyncResourceFactory<NATSConnection, NATSConnectionCreationInfo> CreateFactory()
      {
         return Factory;
      }

      protected override NATSConnectionCreationInfo TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         NATSConnectionCreationInfo retVal;
         if ( creationParameters is NATSConnectionCreationInfoData creationData )
         {
            retVal = new NATSConnectionCreationInfo( creationData );

         }
         else if ( creationParameters is NATSConnectionCreationInfo creationInfo )
         {
            retVal = creationInfo;
         }
         else
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( NATSConnectionCreationInfoData ).FullName}." );
         }

         return retVal;
      }
   }
}
