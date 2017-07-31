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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.Abstractions
{
   /// <summary>
   /// This interface is typically entrypoint for scenarios using CBAM.
   /// It provides a way to use connections via <see cref="UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   public interface ConnectionPool<out TConnection>
   {
      /// <summary>
      /// Takes an existing connection or creates a new one, runs the given asynchronous callback for it, and returns it back into the pool.
      /// </summary>
      /// <param name="user">The asynchronous callback to use the connection.</param>
      /// <param name="token">The optional <see cref="CancellationToken"/> to use during asynchronous operations inside <paramref name="user"/> callback.</param>
      /// <returns>A task which completes when <paramref name="user"/> callback completes and connection is returned back to the pool.</returns>
      Task UseConnectionAsync( Func<TConnection, Task> user, CancellationToken token = default( CancellationToken ) );
   }

   /// <summary>
   /// This interface exposes events related to observing a <see cref="ConnectionPool{TConnection}"/>.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   public interface ConnectionPoolObservation<out TConnection>
   {
      /// <summary>
      /// This event is triggered after a new instance of <typeparamref name="TConnection"/> is created (i.e. when there was no previously used pooled connection available).
      /// </summary>
      event GenericEventHandler<AfterConnectionCreationEventArgs<TConnection>> AfterConnectionCreationEvent;

      /// <summary>
      /// This event is triggered just before an instance of <typeparamref name="TConnection"/> is given to callback of <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.
      /// </summary>
      event GenericEventHandler<AfterConnectionAcquiringEventArgs<TConnection>> AfterConnectionAcquiringEvent;

      /// <summary>
      /// This event is triggered right after an instance of <typeparamref name="TConnection"/> is re-acquired by connection pool from callback in <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.
      /// </summary>
      event GenericEventHandler<BeforeConnectionReturningEventArgs<TConnection>> BeforeConnectionReturningEvent;

      /// <summary>
      /// This event is triggered just before the connection is closed (and thus becomes unusuable) by <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> or <see cref="ConnectionPoolCleanUp{TCleanUpParameter}.CleanUpAsync(TCleanUpParameter, CancellationToken)"/> methods.
      /// </summary>
      event GenericEventHandler<BeforeConnectionCloseEventArgs<TConnection>> BeforeConnectionCloseEvent;
   }

   /// <summary>
   /// This interface augments <see cref="ConnectionPool{TConnection}"/> with observability aspect from <see cref="ConnectionPoolObservation{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <seealso cref="ConnectionPoolObservation{TConnection}"/>
   public interface ConnectionPoolObservable<out TConnection> : ConnectionPool<TConnection>, ConnectionPoolObservation<TConnection>
   {

   }

   /// <summary>
   /// This interface can be used to augment <see cref="ConnectionPool{TConnection}"/> with clean-up routine.
   /// </summary>
   /// <typeparam name="TCleanUpParameter">The type of parameter for clean-up routine.</typeparam>
   /// <remarks>
   /// Typically <typeparamref name="TCleanUpParameter"/> is <see cref="TimeSpan"/> in order to clean up all connection that have been idle for at least given time period.
   /// </remarks>
   public interface ConnectionPoolCleanUp<in TCleanUpParameter> : IDisposable
   {
      /// <summary>
      /// Asynchronously cleans up connections that do not fulfille the given requirements specified by <paramref name="cleanupParameter"/>.
      /// Typically this means cleaning up all connections that have been idle longer than given <see cref="TimeSpan"/>, when <typeparamref name="TCleanUpParameter"/> is <see cref="TimeSpan"/>.
      /// </summary>
      /// <param name="cleanupParameter">The clean up parameter.</param>
      /// <param name="token">The optional <see cref="CancellationToken"/> to use.</param>
      /// <returns>A <see cref="Task"/> which will be completed asynchronously.</returns>
      Task CleanUpAsync( TCleanUpParameter cleanupParameter, CancellationToken token = default( CancellationToken ) );
   }

   /// <summary>
   /// This interface augments <see cref="ConnectionPool{TConnection}"/> with clean-up aspect from <see cref="ConnectionPoolCleanUp{TCleanUpParameter}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <typeparam name="TCleanUpParameters">The type of parameter for <see cref="ConnectionPoolCleanUp{TCleanUpParameter}.CleanUpAsync(TCleanUpParameter, CancellationToken)"/> method.</typeparam>
   /// <seealso cref="ConnectionPoolCleanUp{TCleanUpParameter}"/>
   public interface ConnectionPool<out TConnection, in TCleanUpParameters>
      : ConnectionPool<TConnection>,
        ConnectionPoolCleanUp<TCleanUpParameters>
   {

   }

   /// <summary>
   /// This interface further augments <see cref="ConnectionPool{TConnection, TCleanUpParameters}"/> with observability aspect from <see cref="ConnectionPoolObservation{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <typeparam name="TCleanUpParameter">The type of parameter for <see cref="ConnectionPoolCleanUp{TCleanUpParameter}.CleanUpAsync(TCleanUpParameter, CancellationToken)"/> method.</typeparam>
   public interface ConnectionPoolObservable<out TConnection, in TCleanUpParameter> : ConnectionPool<TConnection, TCleanUpParameter>, ConnectionPoolObservation<TConnection>
   {

   }

   /// <summary>
   /// This is common interface for event arguments for events in <see cref="ConnectionPoolObservation{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   /// <remarks>
   /// This interface extends <see cref="EventArgsWithAsyncContext"/>, so that event handlers could add asynchronous callbacks to be waited by the invoker of the event.
   /// </remarks>
   public interface AbstractConnectionPoolEventArgs<out TConnection> : EventArgsWithAsyncContext
   {
      /// <summary>
      /// Gets the connection related to this event argument interface.
      /// </summary>
      /// <value>The connection related to this event argument interface.</value>
      TConnection Connection { get; }
   }

   /// <summary>
   /// This is event argument interface for <see cref="ConnectionPoolObservation{TConnection}.AfterConnectionCreationEvent"/> event.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   /// <remarks>
   /// This interface extends <see cref="EventArgsWithAsyncContext"/>, so that event handlers could add asynchronous callbacks to be waited by the invoker of the event.
   /// </remarks>
   public interface AfterConnectionCreationEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   /// <summary>
   /// This is event argument interface for <see cref="ConnectionPoolObservation{TConnection}.AfterConnectionAcquiringEvent"/> event.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   /// <remarks>
   /// This interface extends <see cref="EventArgsWithAsyncContext"/>, so that event handlers could add asynchronous callbacks to be waited by the invoker of the event.
   /// </remarks>
   public interface AfterConnectionAcquiringEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   /// <summary>
   /// This is event argument interface for <see cref="ConnectionPoolObservation{TConnection}.BeforeConnectionReturningEvent"/> event.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   /// <remarks>
   /// This interface extends <see cref="EventArgsWithAsyncContext"/>, so that event handlers could add asynchronous callbacks to be waited by the invoker of the event.
   /// </remarks>
   public interface BeforeConnectionReturningEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   /// <summary>
   /// This is event argument interface for <see cref="ConnectionPoolObservation{TConnection}.BeforeConnectionCloseEvent"/> event.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   /// <remarks>
   /// This interface extends <see cref="EventArgsWithAsyncContext"/>, so that event handlers could add asynchronous callbacks to be waited by the invoker of the event.
   /// </remarks>
   public interface BeforeConnectionCloseEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   /// <summary>
   /// This class provides default implementation for <see cref="AbstractConnectionPoolEventArgs{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   public class DefaultAbstractConnectionPoolEventArgs<TConnection> : EventArgsWithAsyncContextImpl, AbstractConnectionPoolEventArgs<TConnection>
   {
      /// <summary>
      /// Creates a new instance of <see cref="DefaultAbstractConnectionPoolEventArgs{TConnection}"/> with given connection.
      /// </summary>
      /// <param name="connection">The connection related to the event argument.</param>
      public DefaultAbstractConnectionPoolEventArgs(
         TConnection connection
         )
      {
         this.Connection = connection;
      }

      /// <summary>
      /// Gets the connection related to the event argument.
      /// </summary>
      /// <value>The connection related to the event argument.</value>
      public TConnection Connection { get; }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="AfterConnectionCreationEventArgs{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   public class DefaultAfterConnectionCreationEventArgs<TConnection> : DefaultAbstractConnectionPoolEventArgs<TConnection>, AfterConnectionCreationEventArgs<TConnection>
   {
      /// <summary>
      /// Creates new instance of <see cref="DefaultAfterConnectionCreationEventArgs{TConnection}"/> with given connection.
      /// </summary>
      /// <param name="connection">The connection related to the event argument.</param>
      public DefaultAfterConnectionCreationEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="AfterConnectionAcquiringEventArgs{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   public class DefaultAfterConnectionAcquiringEventArgs<TConnection> : DefaultAbstractConnectionPoolEventArgs<TConnection>, AfterConnectionAcquiringEventArgs<TConnection>
   {
      /// <summary>
      /// Creates new instance of <see cref="DefaultAfterConnectionAcquiringEventArgs{TConnection}"/> with given connection.
      /// </summary>
      /// <param name="connection">The connection related to the event argument.</param>
      public DefaultAfterConnectionAcquiringEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="BeforeConnectionReturningEventArgs{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   public class DefaultBeforeConnectionReturningEventArgs<TConnection> : DefaultAbstractConnectionPoolEventArgs<TConnection>, BeforeConnectionReturningEventArgs<TConnection>
   {
      /// <summary>
      /// Creates new instance of <see cref="DefaultBeforeConnectionReturningEventArgs{TConnection}"/> with given connection.
      /// </summary>
      /// <param name="connection">The connection related to the event argument.</param>
      public DefaultBeforeConnectionReturningEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="BeforeConnectionCloseEventArgs{TConnection}"/> interface.
   /// </summary>
   /// <typeparam name="TConnection">The type of connection exposed by this event argument interface.</typeparam>
   public class DefaultBeforeConnectionCloseEventArgs<TConnection> : DefaultAbstractConnectionPoolEventArgs<TConnection>, BeforeConnectionCloseEventArgs<TConnection>
   {
      /// <summary>
      /// Creates new instance of <see cref="DefaultBeforeConnectionCloseEventArgs{TConnection}"/> with given connection.
      /// </summary>
      /// <param name="connection">The connection related to the event argument.</param>
      public DefaultBeforeConnectionCloseEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   /// <summary>
   /// This interface represents a <see cref="ConnectionPool{TConnection}"/> bound to specific <see cref="CancellationToken"/>.
   /// It is useful when one wants to bind the cancellation token, and let custom callbacks only customize the callback for <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by the originating pool.</typeparam>
   public interface ConnectionPoolUser<out TConnection>
   {
      /// <summary>
      /// Gets the <see cref="CancellationToken"/> that will be passed to <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.
      /// </summary>
      /// <value>The <see cref="CancellationToken"/> that will be passed to <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.</value>
      CancellationToken Token { get; }

      /// <summary>
      /// Invokes the <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> with given asynchronous callback, and the value of <see cref="Token"/> property as <see cref="CancellationToken"/>.
      /// </summary>
      /// <param name="user">The asynchronous callback to use the connection.</param>
      /// <returns>A task which completes when <paramref name="user"/> callback completes and connection is returned back to the pool.</returns>
      Task UseConnectionAsync( Func<TConnection, Task> user );
   }

   /// <summary>
   /// This class provides default implementation for <see cref="ConnectionPoolUser{TConnection}"/>.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by the originating pool.</typeparam>
   public class DefaultConnectionPoolUser<TConnection> : ConnectionPoolUser<TConnection>
   {
      private readonly ConnectionPool<TConnection> _pool;

      /// <summary>
      /// Creates a new instance of <see cref="DefaultConnectionPoolUser{TConnection}"/> with given parameters.
      /// </summary>
      /// <param name="pool">The connection pool.</param>
      /// <param name="token">The cancellation token.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="pool"/> is <c>null</c>.</exception>
      public DefaultConnectionPoolUser( ConnectionPool<TConnection> pool, CancellationToken token )
      {
         this._pool = ArgumentValidator.ValidateNotNull( nameof( pool ), pool );
         this.Token = token;
      }

      /// <inheritdoc />
      public CancellationToken Token { get; }

      /// <inheritdoc />
      public Task UseConnectionAsync( Func<TConnection, Task> user )
      {
         return this._pool.UseConnectionAsync( user, this.Token );
      }
   }
}

/// <summary>
/// This class contains extension method for CBAM types.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// Helper method to invoke <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> with callback which asynchronously returns value of type <typeparamref name="T"/>.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <typeparam name="T">The type of return value of asynchronous callback.</typeparam>
   /// <param name="pool">This <see cref="ConnectionPool{TConnection}"/>.</param>
   /// <param name="user">The callback which asynchronously returns some value of type <typeparamref name="T"/>.</param>
   /// <param name="token">The optional <see cref="CancellationToken"/> to use during asynchronous operations inside <paramref name="user"/> callback.</param>
   /// <returns>A task which returns the result of <paramref name="user"/> on its completion.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionPool{TConnection}"/> is <c>null</c>.</exception>
   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPool<TConnection> pool, Func<TConnection, Task<T>> user, CancellationToken token = default( CancellationToken ) )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( async connection => retVal = await user( connection ), token );
      return retVal;
   }

   /// <summary>
   /// Helper method to invoke <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> with callback which synchronously returns value of type <typeparamref name="T"/>.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <typeparam name="T">The type of return value of synchronous callback.</typeparam>
   /// <param name="pool">This <see cref="ConnectionPool{TConnection}"/>.</param>
   /// <param name="user">The callback which synchronously returns some value of type <typeparamref name="T"/>.</param>
   /// <param name="token">The optional <see cref="CancellationToken"/> to use during connection acquirement.</param>
   /// <returns>A task which returns the result of <paramref name="user"/> on its completion.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionPool{TConnection}"/> is <c>null</c>.</exception>
   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPool<TConnection> pool, Func<TConnection, T> user, CancellationToken token = default( CancellationToken ) )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( connection =>
      {
         retVal = user( connection );
         return TaskUtils.CompletedTask;
      }, token );

      return retVal;
   }

   /// <summary>
   /// Helper method to invoke <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> with callback which synchronously uses the connection.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <param name="pool">This <see cref="ConnectionPool{TConnection}"/>.</param>
   /// <param name="user">The callback which synchronously uses connection.</param>
   /// <param name="token">The optional <see cref="CancellationToken"/> to use during connection acquirement.</param>
   /// <returns>A task which completes after connection has been returned to the pool.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionPool{TConnection}"/> is <c>null</c>.</exception>
   public static async Task UseConnectionAsync<TConnection>( this ConnectionPool<TConnection> pool, Action<TConnection> user, CancellationToken token = default( CancellationToken ) )
   {
      await pool.UseConnectionAsync( connection =>
      {
         user( connection );
         return TaskUtils.CompletedTask;
      }, token );
   }

   /// <summary>
   /// Helper method to invoke <see cref="ConnectionPoolUser{TConnection}.UseConnectionAsync(Func{TConnection, Task})"/> with callback which asynchronously returns value of type <typeparamref name="T"/>.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <typeparam name="T">The type of return value of asynchronous callback.</typeparam>
   /// <param name="pool">This <see cref="ConnectionPoolUser{TConnection}"/>.</param>
   /// <param name="user">The callback which asynchronously returns some value of type <typeparamref name="T"/>.</param>
   /// <returns>A task which returns the result of <paramref name="user"/> on its completion.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionPoolUser{TConnection}"/> is <c>null</c>.</exception>
   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPoolUser<TConnection> pool, Func<TConnection, Task<T>> user )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( async connection => retVal = await user( connection ) );
      return retVal;
   }

   /// <summary>
   /// Helper method to invoke <see cref="ConnectionPoolUser{TConnection}.UseConnectionAsync(Func{TConnection, Task})"/> with callback which synchronously returns value of type <typeparamref name="T"/>.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <typeparam name="T">The type of return value of synchronous callback.</typeparam>
   /// <param name="pool">This <see cref="ConnectionPoolUser{TConnection}"/>.</param>
   /// <param name="user">The callback which synchronously returns some value of type <typeparamref name="T"/>.</param>
   /// <returns>A task which returns the result of <paramref name="user"/> on its completion.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionPoolUser{TConnection}"/> is <c>null</c>.</exception>
   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPoolUser<TConnection> pool, Func<TConnection, T> user )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( connection =>
      {
         retVal = user( connection );
         return TaskUtils.CompletedTask;
      } );

      return retVal;
   }

   /// <summary>
   /// Helper method to invoke <see cref="ConnectionPoolUser{TConnection}.UseConnectionAsync(Func{TConnection, Task})"/> with callback which synchronously uses the connection.
   /// </summary>
   /// <typeparam name="TConnection">The type of connections handled by this pool.</typeparam>
   /// <param name="pool">This <see cref="ConnectionPoolUser{TConnection}"/>.</param>
   /// <param name="user">The callback which synchronously uses connection.</param>
   /// <returns>A task which completes after connection has been returned to the pool.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionPoolUser{TConnection}"/> is <c>null</c>.</exception>
   public static async Task UseConnectionAsync<TConnection>( this ConnectionPoolUser<TConnection> pool, Action<TConnection> user )
   {
      await pool.UseConnectionAsync( connection =>
      {
         user( connection );
         return TaskUtils.CompletedTask;
      } );
   }
}