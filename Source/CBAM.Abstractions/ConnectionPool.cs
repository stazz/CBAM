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

   public interface ConnectionPoolCleanUp<in TCleanUpParameters> : IDisposable
   {
      Task CleanUpAsync( TCleanUpParameters cleanupParameters, CancellationToken token );
   }

   //public interface ConnectionPoolUnsafe<TConnection>
   //{
   //   Task<TConnection> CreateUnmanagedConnectionAsync( CancellationToken token = default( CancellationToken ) );
   //}

   public interface ConnectionPool<out TConnection>
   {
      Task UseConnectionAsync( Func<TConnection, Task> user, CancellationToken token = default( CancellationToken ) );

      event GenericEventHandler<AfterConnectionCreationEventArgs<TConnection>> AfterConnectionCreationEvent;
      event GenericEventHandler<AfterConnectionAcquiringEventArgs<TConnection>> AfterConnectionAcquiringEvent;
      event GenericEventHandler<BeforeConnectionReturningEventArgs<TConnection>> BeforeConnectionReturningEvent;
      event GenericEventHandler<BeforeConnectionCloseEventArgs<TConnection>> BeforeConnectionCloseEvent;
   }

   public interface ConnectionPool<out TConnection, in TCleanUpParameters>
      : ConnectionPool<TConnection>,
        ConnectionPoolCleanUp<TCleanUpParameters>
   //ConnectionPoolUnsafe<TConnection>
   {

   }

   public interface AbstractConnectionPoolEventArgs<out TConnection> : EventArgsWithAsyncContext
   {
      TConnection Connection { get; }
   }

   public interface AfterConnectionCreationEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   public interface AfterConnectionAcquiringEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   public interface BeforeConnectionReturningEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   public interface BeforeConnectionCloseEventArgs<out TConnection> : AbstractConnectionPoolEventArgs<TConnection>
   {
   }

   public class AbstractConnectionPoolEventArgsImpl<TConnection> : EventArgsWithAsyncContextImpl, AbstractConnectionPoolEventArgs<TConnection>
      where TConnection : class
   {
      public AbstractConnectionPoolEventArgsImpl(
         TConnection connection
         )
      {
         this.Connection = ArgumentValidator.ValidateNotNull( nameof( connection ), connection );
      }

      public TConnection Connection { get; }
   }

   public class AfterConnectionCreationEventArgsImpl<TConnection> : AbstractConnectionPoolEventArgsImpl<TConnection>, AfterConnectionCreationEventArgs<TConnection>
      where TConnection : class
   {
      public AfterConnectionCreationEventArgsImpl(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public class AfterConnectionAcquiringEventArgsImpl<TConnection> : AbstractConnectionPoolEventArgsImpl<TConnection>, AfterConnectionAcquiringEventArgs<TConnection>
      where TConnection : class
   {
      public AfterConnectionAcquiringEventArgsImpl(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public class BeforeConnectionReturningEventArgsImpl<TConnection> : AbstractConnectionPoolEventArgsImpl<TConnection>, BeforeConnectionReturningEventArgs<TConnection>
      where TConnection : class
   {
      public BeforeConnectionReturningEventArgsImpl(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public class BeforeConnectionCloseEventArgsImpl<TConnection> : AbstractConnectionPoolEventArgsImpl<TConnection>, BeforeConnectionCloseEventArgs<TConnection>
      where TConnection : class
   {
      public BeforeConnectionCloseEventArgsImpl(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public interface ConnectionPoolUser<out TConnection>
   {
      CancellationToken Token { get; }

      Task UseConnectionAsync( Func<TConnection, Task> user );
   }

   public class DefaultConnectionPoolUser<TConnection> : ConnectionPoolUser<TConnection>
   {
      private readonly ConnectionPool<TConnection> _pool;

      public DefaultConnectionPoolUser( ConnectionPool<TConnection> pool, CancellationToken token )
      {
         this._pool = ArgumentValidator.ValidateNotNull( nameof( pool ), pool );
         this.Token = token;
      }

      public CancellationToken Token { get; }

      public Task UseConnectionAsync( Func<TConnection, Task> user )
      {
         return this._pool.UseConnectionAsync( user, this.Token );
      }
   }
}

public static partial class E_CBAM
{
   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPool<TConnection> pool, Func<TConnection, Task<T>> user, CancellationToken token = default( CancellationToken ) )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( async connection => retVal = await user( connection ), token );
      return retVal;
   }

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

   public static async Task UseConnectionAsync<TConnection>( this ConnectionPool<TConnection> pool, Action<TConnection> executer, CancellationToken token = default( CancellationToken ) )
   {
      await pool.UseConnectionAsync( connection =>
      {
         executer( connection );
         return TaskUtils.CompletedTask;
      }, token );
   }

   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPoolUser<TConnection> pool, Func<TConnection, Task<T>> user )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( async connection => retVal = await user( connection ) );
      return retVal;
   }

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

   public static async Task UseConnectionAsync<TConnection>( this ConnectionPoolUser<TConnection> pool, Action<TConnection> executer )
   {
      await pool.UseConnectionAsync( connection =>
      {
         executer( connection );
         return TaskUtils.CompletedTask;
      } );
   }
}