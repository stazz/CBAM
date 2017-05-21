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
   public interface ConnectionPoolObservable<TConnection>
      where TConnection : class
   {
      event EventHandler<AfterConnectionCreationEventArgs<TConnection>> AfterConnectionCreationEvent;
      event EventHandler<AfterConnectionAcquiringEventArgs<TConnection>> AfterConnectionAcquiringEvent;
      event EventHandler<BeforeConnectionReturningEventArgs<TConnection>> BeforeConnectionReturningEvent;
      event EventHandler<BeforeConnectionCloseEventArgs<TConnection>> BeforeConnectionCloseEvent;
   }
   public interface ConnectionPoolUsage<out TConnection>
   {
      Task UseConnectionAsync( Func<TConnection, Task> user, CancellationToken token = default( CancellationToken ) );
   }

   public interface ConnectionPoolCleanUp<in TCleanUpParameters>
   {
      Task CleanUpAsync( TCleanUpParameters cleanupParameters, CancellationToken token );
   }

   public interface ConnectionPoolUnsafe<TConnection>
   {
      Task<TConnection> CreateUnmanagedConnectionAsync( CancellationToken token = default( CancellationToken ) );
   }

   public interface ConnectionPool<TConnection, in TCleanUpParameters>
      : ConnectionPoolUsage<TConnection>,
        ConnectionPoolCleanUp<TCleanUpParameters>,
        ConnectionPoolObservable<TConnection>,
        ConnectionPoolUnsafe<TConnection>
      where TConnection : class
   {

   }

   public class AbstractConnectionPoolEventArgs<TConnection> : EventArgsWithAsyncContext
      where TConnection : class
   {
      public AbstractConnectionPoolEventArgs(
         TConnection connection
         )
      {
         this.Connection = ArgumentValidator.ValidateNotNull( nameof( connection ), connection );
      }

      public TConnection Connection { get; }
   }

   public class AfterConnectionCreationEventArgs<TConnection> : AbstractConnectionPoolEventArgs<TConnection>
      where TConnection : class
   {
      public AfterConnectionCreationEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public class AfterConnectionAcquiringEventArgs<TConnection> : AbstractConnectionPoolEventArgs<TConnection>
      where TConnection : class
   {
      public AfterConnectionAcquiringEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public class BeforeConnectionReturningEventArgs<TConnection> : AbstractConnectionPoolEventArgs<TConnection>
      where TConnection : class
   {
      public BeforeConnectionReturningEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }

   public class BeforeConnectionCloseEventArgs<TConnection> : AbstractConnectionPoolEventArgs<TConnection>
      where TConnection : class
   {
      public BeforeConnectionCloseEventArgs(
         TConnection connection
         )
         : base( connection )
      {
      }
   }
}

public static partial class E_CBAM
{
   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPoolUsage<TConnection> pool, Func<TConnection, Task<T>> user, CancellationToken token = default( CancellationToken ) )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( async connection => retVal = await user( connection ), token );
      return retVal;
   }

   public static async Task<T> UseConnectionAsync<TConnection, T>( this ConnectionPoolUsage<TConnection> pool, Func<TConnection, T> user, CancellationToken token = default( CancellationToken ) )
   {
      var retVal = default( T );
      await pool.UseConnectionAsync( connection =>
      {
         retVal = user( connection );
         return TaskUtils.CompletedTask;
      }, token );

      return retVal;
   }

   public static async Task UseConnectionAsync<TConnection>( this ConnectionPoolUsage<TConnection> pool, Action<TConnection> executer, CancellationToken token = default( CancellationToken ) )
   {
      await pool.UseConnectionAsync( connection =>
      {
         executer( connection );
         return TaskUtils.CompletedTask;
      }, token );
   }
}