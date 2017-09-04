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
using UtilPack.ResourcePooling;

namespace CBAM.Abstractions.Implementation
{
   /// <summary>
   /// This class implements <see cref="AsyncResourceFactory{TResource, TParams}"/> in order to create instances of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> as resource instances.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   public abstract class DefaultConnectionFactory<TConnection, TConnectionCreationParameters, TConnectionFunctionality> : AsyncResourceFactory<TConnection, TConnectionCreationParameters>
      where TConnection : class
      where TConnectionFunctionality : DefaultConnectionFunctionality
   {
      /// <summary>
      /// This method implements <see cref="AsyncResourceFactory{TResource, TParams}.AcquireResourceAsync(TParams, CancellationToken)"/> by calling a number of abstract methods in this class.
      /// </summary>
      /// <param name="parameters"></param>
      /// <param name="token"></param>
      /// <returns></returns>
      /// <remarks>
      /// The methods called are, in this order:
      /// <list type="number">
      /// <item><description>the <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/>,</description></item>
      /// <item><description>the <see cref="CreateConnection(TConnectionFunctionality)"/>, and</description></item>
      /// <item><description>the <see cref="CreateConnectionAcquireInfo(TConnectionFunctionality, TConnection)"/>.</description></item>
      /// </list>
      /// 
      /// In case of an error, the <see cref="OnConnectionAcquirementError(TConnectionFunctionality, TConnection, CancellationToken, Exception)"/> will be called.
      /// </remarks>
      public async ValueTask<AsyncResourceAcquireInfo<TConnection>> AcquireResourceAsync( TConnectionCreationParameters parameters, CancellationToken token )
      {
         TConnectionFunctionality functionality = null;
         TConnection connection = null;
         try
         {
            functionality = await this.CreateConnectionFunctionality( parameters, token );
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

      /// <inheritdoc/>
      public abstract void ResetFactoryState();

      /// <summary>
      /// This method is called by <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/> initially, to create <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> for the <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/>.
      /// </summary>
      /// <param name="parameters">The parameters needed to create a new instance of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>A task which will result in <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> when completed.</returns>
      protected abstract ValueTask<TConnectionFunctionality> CreateConnectionFunctionality( TConnectionCreationParameters parameters, CancellationToken token );

      /// <summary>
      /// This method is called after <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/>, in order to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> after <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> has been created.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> created by <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <returns>A task which will result in <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> when completed.</returns>
      protected abstract ValueTask<TConnection> CreateConnection( TConnectionFunctionality functionality );

      /// <summary>
      /// This method is called after <see cref="CreateConnection(TConnectionFunctionality)"/> in order to create instance of <see cref="AsyncResourceAcquireInfo{TResource}"/> to be returned from <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/> method.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> created by <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> created by <see cref="CreateConnection(TConnectionFunctionality)"/> method.</param>
      /// <returns>A new instance of <see cref="AsyncResourceAcquireInfo{TResource}"/>.</returns>
      protected abstract AsyncResourceAcquireInfo<TConnection> CreateConnectionAcquireInfo( TConnectionFunctionality functionality, TConnection connection );

      /// <summary>
      /// This method is called whenever an error occurs within <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/> method.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>A task which completes after error handling is done.</returns>
      protected abstract Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }

   /// <summary>
   /// This class provides CBAM-related implementation for <see cref="AsyncResourceAcquireInfoImpl{TConnection, TChannel}"/> using <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TStatement">The actual type of statement which modifies/queries remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of read-only information about <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of parameters to create instances of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of enumerable items returned by statement execution.</typeparam>
   /// <typeparam name="TVendor">The type of <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>, as specified by interface generic parameter.</typeparam>
   /// <typeparam name="TActualVendor">The actual type of <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</typeparam>
   /// <typeparam name="TStream">The actual type of underlying stream or other disposable resource.</typeparam>
   public abstract class ConnectionAcquireInfoImpl<TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TActualVendor, TStream> : AsyncResourceAcquireInfoImpl<TConnection, TStream>
      where TStatement : TStatementInformation
      where TConnection : ConnectionImpl<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TActualVendor, TConnectionFunctionality>
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TActualVendor>
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TActualVendor : TVendor
      where TStream : IDisposable
   {
      /// <summary>
      /// Creates a new instance of <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TActualVendor, TStream}"/> with given parameters.
      /// </summary>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/>.</param>
      /// <param name="associatedStream">The underlying stream or other disposable resource.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="connection"/> is <c>null</c>.</exception>
      public ConnectionAcquireInfoImpl(
         TConnection connection,
         TStream associatedStream
         ) : base( ArgumentValidator.ValidateNotNull( nameof( connection ), connection ), associatedStream, ( c, t ) => c.ConnectionFunctionality.CurrentCancellationToken = t, c => c.ConnectionFunctionality.ResetCancellationToken() )
      {

      }

      /// <summary>
      /// This method overrides <see cref="AbstractDisposable.Dispose(bool)"/> and will dispose this <see cref="AsyncResourceAcquireInfoImpl{TPublicResource, TPrivateResource}.Channel"/> if <paramref name="disposing"/> is <c>true</c>.
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
      /// This method forwards the disposing for <see cref="DisposeBeforeClosingStream(CancellationToken, TConnectionFunctionality)"/> and giving <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}.ConnectionFunctionality"/> as second parameter to the method.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>A task which performs disposing asynchronously, or <c>null</c> if disposing has been done synchronously.</returns>
      protected override Task DisposeBeforeClosingChannel( CancellationToken token )
      {
         return this.DisposeBeforeClosingStream( token, this.PublicResource.ConnectionFunctionality );
      }

      /// <summary>
      /// Overrides the abstract <see cref="AsyncResourceAcquireInfoImpl{TConnection, TStream}.PublicResourceCanBeReturnedToPool"/> method and forwards the call to <see cref="DefaultConnectionFunctionality.CanBeReturnedToPool"/>.
      /// </summary>
      /// <returns>The value indicating whether this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TActualVendorFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> can be returned to pool, as indicated by <see cref="DefaultConnectionFunctionality.CanBeReturnedToPool"/> property.</returns>
      protected override Boolean PublicResourceCanBeReturnedToPool()
      {
         return this.PublicResource.ConnectionFunctionality.CanBeReturnedToPool;
      }

      /// <summary>
      /// Derived classes should implement the disposing functionality, see <see cref="AsyncResourceAcquireInfoImpl{TConnection, TStream}.DisposeBeforeClosingChannel(CancellationToken)"/>.
      /// </summary>
      /// <param name="token">The cancellation token to use.</param>
      /// <param name="connectionFunctionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</param>
      /// <returns>A task which performs disposing asynchronously, or <c>null</c> if disposing has been done synchronously.</returns>
      protected abstract Task DisposeBeforeClosingStream( CancellationToken token, TConnectionFunctionality connectionFunctionality );


   }


   /// <summary>
   /// This class extends <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}"/> to provide functionality which is common for connections operating on a stream or other <see cref="IDisposable"/> object.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   public abstract class ConnectionFactorySU<TConnection, TConnectionCreationParameters, TConnectionFunctionality> : DefaultConnectionFactory<TConnection, TConnectionCreationParameters, TConnectionFunctionality>
      where TConnection : class
      where TConnectionFunctionality : DefaultConnectionFunctionality
   {

      /// <summary>
      /// This task overrides <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}.OnConnectionAcquirementError(TConnectionFunctionality, TConnection, CancellationToken, Exception)"/> and calls <see cref="ExtractStreamOnConnectionAcquirementError(TConnectionFunctionality, TConnection, CancellationToken, Exception)"/> in order to then safely synchronously dispose it.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="DefaultConnectionFactory{TConnection,  TConnectionCreationParameters, TConnectionFunctionality}.AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>A completed task.</returns>
      protected override Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error )
      {
         this.ExtractStreamOnConnectionAcquirementError( functionality, connection, token, error ).DisposeSafely();
         return TaskUtils.CompletedTask;
      }

      /// <summary>
      /// This method should be implemented by derived class in order to extract underlying stream or other <see cref="IDisposable"/> object from <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}.CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="DefaultConnectionFactory{TConnection, TConnectionCreationParameters, TConnectionFunctionality}.AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>The underlying stream or other <see cref="IDisposable"/> object.</returns>
      protected abstract IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }

}