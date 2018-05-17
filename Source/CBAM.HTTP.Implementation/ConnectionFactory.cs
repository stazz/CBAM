using CBAM.Abstractions.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.ResourcePooling;
using UtilPack.ResourcePooling.NetworkStream;

namespace CBAM.HTTP.Implementation
{
   public sealed class HTTPNetworkConnectionPoolProvider<TRequestMetaData> : AbstractAsyncResourceFactoryProvider<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo>
   {

      /// <summary>
      /// Gets the <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="PgSQLConnection"/>s.
      /// </summary>
      /// <value>The <see cref="AsyncResourceFactory{TResource, TParams}"/> which can create <see cref="PgSQLConnection"/>s.</value>
      /// <remarks>
      /// By invoking <see cref="AsyncResourceFactory{TResource, TParams}.BindCreationParameters"/>, one gets the bound version <see cref="AsyncResourceFactory{TResource}"/>, with only one generic parameter.
      /// Instead of directly using <see cref="AsyncResourceFactory{TResource}.AcquireResourceAsync"/>, typical scenario would involve creating an instance <see cref="AsyncResourcePool{TResource}"/> by invoking one of various extension methods for <see cref="AsyncResourceFactory{TResource}"/>.
      /// </remarks>
      public static AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> Factory { get; } = new HTTPConnectionFactoryFactory<TRequestMetaData>(); // DefaultAsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo>( config => new PgSQLConnectionFactory( config, new UTF8EncodingInfo() ) );

      /// <summary>
      /// Creates a new instance of <see cref="HTTPNetworkConnectionPoolProvider{TRequestMetaData}"/>.
      /// </summary>
      /// <remarks>
      /// This constructor is not intended to be used directly, but a generic scenarios like MSBuild task dynamically loading this type.
      /// </remarks>
      public HTTPNetworkConnectionPoolProvider()
         : base( typeof( HTTPNetworkCreationInfoData ) )
      {
      }


      protected override HTTPNetworkCreationInfo TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         HTTPNetworkCreationInfo retVal;
         if ( creationParameters is HTTPNetworkCreationInfoData creationData )
         {
            retVal = new HTTPNetworkCreationInfo( creationData );

         }
         else if ( creationParameters is HTTPNetworkCreationInfo creationInfo )
         {
            retVal = creationInfo;
         }
         else
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( HTTPNetworkCreationInfoData ).FullName}." );
         }

         return retVal;
      }

      /// <summary>
      /// This method implements <see cref="AbstractAsyncResourceFactoryProvider{TFactoryResource, TCreationParameters}.CreateFactory"/> by returning static property <see cref="Factory"/>.
      /// </summary>
      /// <returns>The value of <see cref="Factory"/> static property.</returns>
      protected override AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> CreateFactory()
         => Factory;
   }

   internal sealed class HTTPConnectionFactoryFactory<TRequestMetaData> : AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPNetworkCreationInfo> //, AsyncResourceFactory<HTTPConnection<TRequestMetaData>, HTTPSimpleCreationInfo>
   {
      public AsyncResourceFactory<HTTPConnection<TRequestMetaData>> BindCreationParameters( HTTPNetworkCreationInfo parameters )
      {
         return new HTTPConnectionFactory<TRequestMetaData>( parameters );
      }
   }

   internal sealed class HTTPConnectionFactory<TRequestMetaData> : ConnectionFactorySU<HTTPConnection<TRequestMetaData>, HTTPConnectionImpl<TRequestMetaData>, HTTPNetworkCreationInfo, HTTPConnectionFunctionalityImpl<TRequestMetaData>>
   {
      private readonly BinaryStringPool _stringPool;

#if !NETSTANDARD1_0
      private readonly ReadOnlyResettableAsyncLazy<IPAddress> _remoteAddress;
      private readonly NetworkStreamFactoryConfiguration _networkStreamConfig;
#endif

      public HTTPConnectionFactory(
         HTTPNetworkCreationInfo creationInfo
         ) : base( creationInfo )
      {
         //this.Encoding = ArgumentValidator.ValidateNotNull( nameof( encoding ), encoding );
         ArgumentValidator.ValidateNotNull( nameof( creationInfo ), creationInfo );
         var encoding = Encoding.ASCII;

#if NETSTANDARD1_0
         if ( !( creationInfo.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? default ) )
         {
            this._stringPool = BinaryStringPoolFactory.NewConcurrentBinaryStringPool( encoding );
         }
#else
#if !NETSTANDARD1_0
         (this._networkStreamConfig, this._remoteAddress, this._stringPool) = creationInfo.CreateNetworkStreamFactoryConfiguration(
            //( socket, stream, token ) => this.CreateNetworkStreamInitState( token, stream ),
            encoding,
            () => TaskUtils.True,
            null,
            null,
            null,
            null,
            null
            );
         this._networkStreamConfig.TransformStreamAfterCreation = stream => new DuplexBufferedAsyncStream( stream );
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

      protected override ValueTask<HTTPConnectionImpl<TRequestMetaData>> CreateConnection( HTTPConnectionFunctionalityImpl<TRequestMetaData> functionality )
      {
         return new ValueTask<HTTPConnectionImpl<TRequestMetaData>>( new HTTPConnectionImpl<TRequestMetaData>( functionality ) );
      }

      protected override AsyncResourceAcquireInfo<HTTPConnectionImpl<TRequestMetaData>> CreateConnectionAcquireInfo(
         HTTPConnectionFunctionalityImpl<TRequestMetaData> functionality,
         HTTPConnectionImpl<TRequestMetaData> connection
         )
      {
         return new HTTPConnectionAcquireInfo<TRequestMetaData>( connection, functionality );
      }

      protected override async ValueTask<HTTPConnectionFunctionalityImpl<TRequestMetaData>> CreateConnectionFunctionality( CancellationToken token )
      {

         var parameters = this.CreationParameters;
         var streamFactory = parameters.StreamFactory;

#if !NETSTANDARD1_0
         System.Net.Sockets.Socket socket;
#endif
         Stream stream;
         //TNetworkStreamInitState state;
         if ( streamFactory == null )
         {
#if NETSTANDARD1_0
            throw new ArgumentNullException( nameof( streamFactory ) );
#else
            (socket, stream) = await NetworkStreamFactory.AcquireNetworkStreamFromConfiguration(
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
               stream) = (
#if !NETSTANDARD1_0
               null,
#endif
               await streamFactory());
         }

         // TODO send http request to determine http 2.0 capability, and use 2.0 if possible.
         // TODO set max size for buffers
         return new HTTPConnectionFunctionalityImpl<TRequestMetaData>(
            HTTPConnectionVendorImpl<TRequestMetaData>.Instance,
            new ClientProtocolIOState(
               stream,
               this._stringPool,
               Encoding.ASCII.CreateDefaultEncodingInfo(),
               new WriteState(),
               new ReadState()
               ) );
      }

      protected override IDisposable ExtractStreamOnConnectionAcquirementError(
         HTTPConnectionFunctionalityImpl<TRequestMetaData> functionality,
         HTTPConnectionImpl<TRequestMetaData> connection,
         CancellationToken token,
         Exception error
         )
      {
         return functionality.Stream;
      }
   }

   internal sealed class HTTPConnectionAcquireInfo<TRequestMetaData> : ConnectionAcquireInfoImpl<HTTPConnectionImpl<TRequestMetaData>, HTTPConnectionFunctionalityImpl<TRequestMetaData>, System.IO.Stream>
   {
      public HTTPConnectionAcquireInfo( HTTPConnectionImpl<TRequestMetaData> connection, HTTPConnectionFunctionalityImpl<TRequestMetaData> functionality )
         : base( connection, functionality, functionality.Stream )
      {

      }

      protected override Task DisposeBeforeClosingChannel( CancellationToken token )
      {
         // Nothing to do as HTTP is not stateful protocol
         return TaskUtils.CompletedTask;
      }
   }
}
