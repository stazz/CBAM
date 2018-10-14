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
using CBAM.Abstractions;
using CBAM.Abstractions.Implementation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.AsyncEnumeration;
using UtilPack.ResourcePooling;

namespace CBAM.Abstractions.Implementation
{
   /// <summary>
   /// This class implements <see cref="AsyncResourceFactory{TResource, TParams}"/> in order to create instances of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality,  TActualVendorFunctionality, TConnectionFunctionality}"/> as resource instances.
   /// </summary>
   /// <typeparam name="TConnection">The public type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TPrivateConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="PooledConnectionFunctionality"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality,  TActualVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   public abstract class DefaultConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters> : DefaultBoundAsyncResourceFactory<TConnection, TConnectionCreationParameters> // AsyncResourceFactory<TConnection>
      where TConnection : class
      where TPrivateConnection : class, TConnection
      where TConnectionFunctionality : class, PooledConnectionFunctionality
   {
      /// <summary>
      /// Initializes a new instance of <see cref="PooledConnectionFunctionality"/> with given connection creation parameters.
      /// </summary>
      /// <param name="creationParameters">The connection creation parameters.</param>
      public DefaultConnectionFactory(
         TConnectionCreationParameters creationParameters
         ) : base( creationParameters )
      {

      }


      /// <summary>
      /// This method implements <see cref="DefaultBoundAsyncResourceFactory{TConnection, TConnectionCreationParameters}.AcquireResourceAsync"/> by calling a number of abstract methods in this class.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>Potentially asynchronously returns <see cref="AsyncResourceAcquireInfo{TResource}"/> for given resource.</returns>
      /// <remarks>
      /// The methods called are, in this order:
      /// <list type="number">
      /// <item><description>the <see cref="CreateConnectionFunctionality(CancellationToken)"/>,</description></item>
      /// <item><description>the <see cref="CreateConnection(TConnectionFunctionality)"/>, and</description></item>
      /// <item><description>the <see cref="CreateConnectionAcquireInfo(TConnectionFunctionality, TPrivateConnection)"/>.</description></item>
      /// </list>
      /// 
      /// In case of an error, the <see cref="OnConnectionAcquirementError(TConnectionFunctionality, TPrivateConnection, CancellationToken, Exception)"/> will be called.
      /// </remarks>
      protected override async ValueTask<AsyncResourceAcquireInfo<TConnection>> AcquireResourceAsync( CancellationToken token )
      {
         TConnectionFunctionality functionality = null;
         TPrivateConnection connection = null;
         try
         {
            functionality = await this.CreateConnectionFunctionality( token );
            functionality.CurrentCancellationToken = token;
            connection = await this.CreateConnection( functionality );
            return this.CreateConnectionAcquireInfo( functionality, connection );
         }
         catch ( Exception exc )
         {
            try
            {
               await this.OnConnectionAcquirementError( functionality, connection, token, exc );
            }
            catch
            {
               // Ignore this one
            }
            throw;
         }
         finally
         {
            functionality?.ResetCancellationToken();
         }
      }


      /// <summary>
      /// This method is called by <see cref="AcquireResourceAsync(CancellationToken)"/> initially, to create <see cref="PooledConnectionFunctionality"/> for the <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/>.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>A task which will result in <see cref="PooledConnectionFunctionality"/> when completed.</returns>
      protected abstract ValueTask<TConnectionFunctionality> CreateConnectionFunctionality( CancellationToken token );

      /// <summary>
      /// This method is called after <see cref="CreateConnectionFunctionality(CancellationToken)"/>, in order to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> after <see cref="PooledConnectionFunctionality"/> has been created.
      /// </summary>
      /// <param name="functionality">The <see cref="PooledConnectionFunctionality"/> created by <see cref="CreateConnectionFunctionality(CancellationToken)"/> method.</param>
      /// <returns>A task which will result in <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> when completed.</returns>
      protected abstract ValueTask<TPrivateConnection> CreateConnection( TConnectionFunctionality functionality );

      /// <summary>
      /// This method is called after <see cref="CreateConnection(TConnectionFunctionality)"/> in order to create instance of <see cref="AsyncResourceAcquireInfo{TResource}"/> to be returned from <see cref="AcquireResourceAsync(CancellationToken)"/> method.
      /// </summary>
      /// <param name="functionality">The <see cref="PooledConnectionFunctionality"/> created by <see cref="CreateConnectionFunctionality(CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> created by <see cref="CreateConnection(TConnectionFunctionality)"/> method.</param>
      /// <returns>A new instance of <see cref="AsyncResourceAcquireInfo{TResource}"/>.</returns>
      protected abstract AsyncResourceAcquireInfo<TPrivateConnection> CreateConnectionAcquireInfo( TConnectionFunctionality functionality, TPrivateConnection connection );

      /// <summary>
      /// This method is called whenever an error occurs within <see cref="AcquireResourceAsync(CancellationToken)"/> method.
      /// </summary>
      /// <param name="functionality">The <see cref="PooledConnectionFunctionality"/> or <c>null</c> if error occurred during <see cref="CreateConnectionFunctionality(CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="AcquireResourceAsync(CancellationToken)"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>A task which completes after error handling is done.</returns>
      protected abstract Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TPrivateConnection connection, CancellationToken token, Exception error );
   }

   /// <summary>
   /// This class provides CBAM-related implementation for <see cref="AsyncResourceAcquireInfoImpl{TConnection, TChannel}"/> using <see cref="PooledConnectionFunctionality"/>.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="ConnectionImpl{TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="PooledConnectionFunctionality"/>.</typeparam>
   /// <typeparam name="TStream">The actual type of underlying stream or other disposable resource.</typeparam>
   public abstract class ConnectionAcquireInfoImpl<TConnection, TConnectionFunctionality, TStream> : AsyncResourceAcquireInfoImpl<TConnection, TStream>
      where TConnection : class
      where TConnectionFunctionality : class, PooledConnectionFunctionality
      where TStream : IDisposable
   {
      /// <summary>
      /// Creates a new instance of <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStream}"/> with given parameters.
      /// </summary>
      /// <param name="connection">The <typeparamref name="TConnection"/>.</param>
      /// <param name="functionality">The <typeparamref name="TConnectionFunctionality"/>.</param>
      /// <param name="associatedStream">The underlying stream or other disposable resource.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="connection"/> is <c>null</c>.</exception>
      public ConnectionAcquireInfoImpl(
         TConnection connection,
         TConnectionFunctionality functionality,
         TStream associatedStream
         ) : base( ArgumentValidator.ValidateNotNull( nameof( connection ), connection ), associatedStream, ( c, t ) => functionality.CurrentCancellationToken = t, c => functionality.ResetCancellationToken() )
      {
         this.Functionality = ArgumentValidator.ValidateNotNull( nameof( functionality ), functionality );
      }

      /// <summary>
      /// Gets the <typeparamref name="TConnectionFunctionality"/> of this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStream}"/>.
      /// </summary>
      /// <value>The <typeparamref name="TConnectionFunctionality"/> of this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStream}"/>.</value>
      protected TConnectionFunctionality Functionality { get; }

      /// <summary>
      /// This method overrides <see cref="AbstractDisposable.Dispose(Boolean)"/> and will dispose this <see cref="AsyncResourceAcquireInfoImpl{TPublicResource, TPrivateResource}.Channel"/> if <paramref name="disposing"/> is <c>true</c>.
      /// </summary>
      /// <param name="disposing">Whether this method is callsed from <see cref="IDisposable.Dispose"/> method.</param>
      protected override void Dispose( Boolean disposing )
      {
         if ( disposing )
         {
            this.Channel?.DisposeSafely();
         }
      }

      /// <summary>
      /// Overrides the abstract <see cref="AsyncResourceAcquireInfoImpl{TConnection, TStream}.PublicResourceCanBeReturnedToPool"/> method and forwards the call to <see cref="PooledConnectionFunctionality.CanBeReturnedToPool"/>.
      /// </summary>
      /// <returns>The value indicating whether this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStream}"/> can be returned to pool, as indicated by <see cref="PooledConnectionFunctionality.CanBeReturnedToPool"/> property.</returns>
      protected override Boolean PublicResourceCanBeReturnedToPool()
      {
         return this.Functionality.CanBeReturnedToPool;
      }

   }

   /// <summary>
   /// This class extends <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStream}"/> for situations when there is no need to send anything to remote when disconnect occurs (usually stateless protocols, e.g. HTTP).
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="ConnectionImpl{TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="PooledConnectionFunctionality"/>.</typeparam>
   /// <typeparam name="TStream">The actual type of underlying stream or other disposable resource.</typeparam>
   public sealed class StatelessConnectionAcquireInfo<TConnection, TConnectionFunctionality, TStream> : ConnectionAcquireInfoImpl<TConnection, TConnectionFunctionality, TStream>
      where TConnection : class
      where TConnectionFunctionality : class, PooledConnectionFunctionality
      where TStream : IDisposable
   {
      /// <summary>
      /// Creates a new instance of <see cref="StatelessConnectionAcquireInfo{TConnection, TConnectionFunctionality, TStream}"/> with given parameters.
      /// </summary>
      /// <param name="connection">The <typeparamref name="TConnection"/>.</param>
      /// <param name="functionality">The <typeparamref name="TConnectionFunctionality"/>.</param>
      /// <param name="associatedStream">The underlying stream or other disposable resource.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="connection"/> is <c>null</c>.</exception>
      public StatelessConnectionAcquireInfo(
         TConnection connection,
         TConnectionFunctionality functionality,
         TStream associatedStream
         ) : base( connection, functionality, associatedStream )
      {
      }

      /// <summary>
      /// This method does nothing and returns completed task, since there should be nothing to do.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>Always returns completed task.</returns>
      protected override Task DisposeBeforeClosingChannel( CancellationToken token )
      {
         return TaskUtils.CompletedTask;
      }
   }


   /// <summary>
   /// This class extends <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}"/> to provide functionality which is common for connections operating on a <see cref="System.IO.Stream"/> or other <see cref="IDisposable"/> object.
   /// </summary>
   /// <typeparam name="TConnection">The public type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TPrivateConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   public abstract class ConnectionFactoryStream<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters> : DefaultConnectionFactory<TConnection, TPrivateConnection, TConnectionFunctionality, TConnectionCreationParameters>
      where TConnection : class
      where TPrivateConnection : class, TConnection
      where TConnectionFunctionality : class, PooledConnectionFunctionality
   {
      /// <summary>
      /// Initializes a new instance of <see cref="ConnectionFactoryStream{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}"/> with given connection creation parameters.
      /// </summary>
      /// <param name="creationParameters">The connection creation parameters.</param>
      public ConnectionFactoryStream(
         TConnectionCreationParameters creationParameters
         ) : base( creationParameters )
      {
      }

      /// <summary>
      /// This task overrides <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.OnConnectionAcquirementError(TConnectionCreationParameters, TPrivateConnection, CancellationToken, Exception)"/> and calls <see cref="ExtractStreamOnConnectionAcquirementError(TConnectionFunctionality, TPrivateConnection, CancellationToken, Exception)"/> in order to then safely synchronously dispose it.
      /// </summary>
      /// <param name="functionality">The <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnectionFunctionality"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnection"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.AcquireResourceAsync"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>A completed task.</returns>
      protected override Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TPrivateConnection connection, CancellationToken token, Exception error )
      {
         this.ExtractStreamOnConnectionAcquirementError( functionality, connection, token, error ).DisposeSafely();
         return TaskUtils.CompletedTask;
      }

      /// <summary>
      /// This method should be implemented by derived class in order to extract underlying stream or other <see cref="IDisposable"/> object from <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
      /// </summary>
      /// <param name="functionality">The <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnectionFunctionality"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnection"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="DefaultConnectionFactory{TConnection, TPrivateConnection, TConnectionCreationParameters, TConnectionFunctionality}.AcquireResourceAsync"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>The underlying stream or other <see cref="IDisposable"/> object.</returns>
      protected abstract IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TPrivateConnection connection, CancellationToken token, Exception error );
   }

}