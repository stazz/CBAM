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

namespace CBAM.Abstractions.Implementation
{
   public interface ConnectionFactory<TConnection, in TParams>
      where TConnection : class
   {
      Task<ConnectionAcquireInfo<TConnection>> AcquireConnection( TParams parameters, CancellationToken token );
   }

   public interface ConnectionAcquireInfo<out TConnection> : IAsyncDisposable
      where TConnection : class
   {
      ConnectionUsageInfo<TConnection> GetConnectionUsageForToken( CancellationToken token );

      // Return false if e.g. cancellation caused connection disposing.
      Boolean IsConnectionReturnableToPool { get; }
   }

   public abstract class ConnectionAcquireInfoImpl<TConnection, TConnectionFunctionality, TStatement, TEnumerableItem, TStream> : ConnectionAcquireInfo<TConnection>
      where TConnection : class
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TEnumerableItem>
      where TStream : IDisposable
   {
      private const Int32 NOT_CANCELED = 0;
      private const Int32 CANCELED = 1;

      private Int32 _cancellationState;

      public ConnectionAcquireInfoImpl(
         TConnection connection,
         TConnectionFunctionality connectionFunctionality,
         TStream associatedStream
         )
      {
         this.Connection = ArgumentValidator.ValidateNotNull( nameof( connection ), connection );
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );
         ArgumentValidator.ValidateNotNull<Object>( nameof( associatedStream ), associatedStream );
         this.Stream = associatedStream;
      }

      public Boolean IsConnectionReturnableToPool => this._cancellationState == NOT_CANCELED && this.ConnectionFunctionality.CanBeReturnedToPool;

      public async Task DisposeAsync( CancellationToken token )
      {
         try
         {
            await ( this.DisposeBeforeClosingStream( token ) ?? TaskUtils.CompletedTask );
         }
         finally
         {
            this.Stream.DisposeSafely();
         }
      }

      public ConnectionUsageInfo<TConnection> GetConnectionUsageForToken( CancellationToken token )
      {
         return new CancelableConnectionUsageInfo<TConnection, TConnectionFunctionality, TStatement, TEnumerableItem>(
            this.Connection,
            this.ConnectionFunctionality,
            token,
            token.Register( () =>
            {
               try
               {
                  Interlocked.Exchange( ref this._cancellationState, CANCELED );
               }
               finally
               {
                  this.Stream.DisposeSafely();
               }
            } )
            );
      }

      protected TConnection Connection { get; }
      protected TConnectionFunctionality ConnectionFunctionality { get; }
      protected TStream Stream { get; }

      protected abstract Task DisposeBeforeClosingStream( CancellationToken token );
   }

   public interface ConnectionUsageInfo<out TConnection> : IDisposable
      where TConnection : class
   {
      TConnection Connection { get; }
   }

   public class CancelableConnectionUsageInfo<TConnection, TConnectionFunctionality, TStatement, TEnumerableItem> : ConnectionUsageInfo<TConnection>
      where TConnection : class
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TEnumerableItem>
   {
      private readonly CancellationTokenRegistration _registration;
      private readonly TConnectionFunctionality _connectionFunctionality;

      public CancelableConnectionUsageInfo(
         TConnection connection,
         TConnectionFunctionality connectionFunctionality,
         CancellationToken token,
         CancellationTokenRegistration registration
         )
      {
         this.Connection = connection ?? throw new ArgumentNullException( nameof( connection ) );
         this._connectionFunctionality = connectionFunctionality ?? throw new ArgumentNullException( nameof( connectionFunctionality ) );
         this._connectionFunctionality.CurrentCancellationToken = token;
         this._registration = registration;
      }

      public TConnection Connection { get; }

      public void Dispose()
      {
         this._connectionFunctionality.ResetCancellationToken();
         this._registration.DisposeSafely();
      }
   }

   public class OneTimeUseConnectionPool<TConnection, TConnectionInstance, TConnectionCreationParams, TCleanUpParameters> : ConnectionPool<TConnection, TCleanUpParameters>
      where TConnection : class
      where TConnectionCreationParams : class
   {
      public OneTimeUseConnectionPool(
         ConnectionFactory<TConnection, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters,
         Func<TConnectionInstance, ConnectionAcquireInfo<TConnection>> connectionExtractor,
         Func<ConnectionAcquireInfo<TConnection>, TConnectionInstance> instanceCreator
      )
      {
         this.FactoryParameters = ArgumentValidator.ValidateNotNull( nameof( factoryParameters ), factoryParameters );
         this.Factory = ArgumentValidator.ValidateNotNull( nameof( factory ), factory );
         this.ConnectionExtractor = ArgumentValidator.ValidateNotNull( nameof( connectionExtractor ), connectionExtractor );
         this.InstanceCreator = ArgumentValidator.ValidateNotNull( nameof( instanceCreator ), instanceCreator );
      }

      protected ConnectionFactory<TConnection, TConnectionCreationParams> Factory { get; }

      protected TConnectionCreationParams FactoryParameters { get; }

      protected Func<TConnectionInstance, ConnectionAcquireInfo<TConnection>> ConnectionExtractor { get; }

      protected Func<ConnectionAcquireInfo<TConnection>, TConnectionInstance> InstanceCreator { get; }

      public async Task UseConnectionAsync( Func<TConnection, Task> executer, CancellationToken token = default( CancellationToken ) )
      {
         var instance = await this.AcquireConnectionAsync( token );

         try
         {
            using ( var usageInfo = this.ConnectionExtractor( instance ).GetConnectionUsageForToken( token ) )
            {
               await executer( usageInfo.Connection );
            }
         }
         finally
         {
            await ( this.DisposeConnectionAsync( instance, token ) ?? TaskUtils.CompletedTask );
         }

      }

      public event EventHandler<AfterConnectionCreationEventArgs<TConnection>> AfterConnectionCreationEvent;
      public event EventHandler<AfterConnectionAcquiringEventArgs<TConnection>> AfterConnectionAcquiringEvent;
      public event EventHandler<BeforeConnectionReturningEventArgs<TConnection>> BeforeConnectionReturningEvent;
      public event EventHandler<BeforeConnectionCloseEventArgs<TConnection>> BeforeConnectionCloseEvent;

      public virtual Task CleanUpAsync( TCleanUpParameters parameters, CancellationToken token )
      {
         return TaskUtils.CompletedTask;
      }

      protected EventHandler<AfterConnectionAcquiringEventArgs<TConnection>> AfterConnectionAcquiringEventInstance => this.AfterConnectionAcquiringEvent;

      protected virtual async Task<TConnectionInstance> AcquireConnectionAsync( CancellationToken token )
      {
         var connAcquireInfo = await this.Factory.AcquireConnection( this.FactoryParameters, token );
         var creationEvent = this.AfterConnectionCreationEvent;
         var acquireEvent = this.AfterConnectionAcquiringEvent;
         if ( creationEvent != null || acquireEvent != null )
         {
            using ( var usageInfo = connAcquireInfo.GetConnectionUsageForToken( token ) )
            {
               await creationEvent.InvokeAndWaitForAwaitables( new AfterConnectionCreationEventArgs<TConnection>( usageInfo.Connection ) );
               await acquireEvent.InvokeAndWaitForAwaitables( new AfterConnectionAcquiringEventArgs<TConnection>( usageInfo.Connection ) );
            }
         }

         return this.InstanceCreator( connAcquireInfo );
      }

      protected virtual async Task DisposeConnectionAsync( TConnectionInstance connection, CancellationToken token )
      {
         await this.PerformDisposeConnectionAsync( connection, token, true, true );
      }

      protected async Task PerformDisposeConnectionAsync(
         TConnectionInstance connection,
         CancellationToken token,
         Boolean isConnectionReturned,
         Boolean closeConnection
         )
      {
         var returningEvent = this.BeforeConnectionReturningEvent;
         var closingEvent = this.BeforeConnectionCloseEvent;
         if ( ( returningEvent != null && isConnectionReturned ) || ( closingEvent != null && closeConnection ) )
         {
            using ( var usageInfo = this.ConnectionExtractor( connection ).GetConnectionUsageForToken( token ) )
            {
               if ( isConnectionReturned )
               {
                  await returningEvent.InvokeAndWaitForAwaitables( new BeforeConnectionReturningEventArgs<TConnection>( usageInfo.Connection ) );
               }

               if ( closeConnection )
               {
                  await closingEvent.InvokeAndWaitForAwaitables( new BeforeConnectionCloseEventArgs<TConnection>( usageInfo.Connection ) );
               }
            }
         }

         if ( closeConnection )
         {
            await this.ConnectionExtractor( connection ).DisposeAsyncSafely( token );
         }
      }
   }

   public class CachingConnectionPool<TConnection, TConnectionInstance, TConnectionCreationParams, TCleanUpParameters> : OneTimeUseConnectionPool<TConnection, TConnectionInstance, TConnectionCreationParams, TCleanUpParameters>, IAsyncDisposable
      where TConnection : class
      where TConnectionInstance : class, InstanceWithNextInfo<TConnectionInstance>
      where TConnectionCreationParams : class
   {

      public CachingConnectionPool(
         ConnectionFactory<TConnection, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters,
         Func<TConnectionInstance, ConnectionAcquireInfo<TConnection>> connectionExtractor,
         Func<ConnectionAcquireInfo<TConnection>, TConnectionInstance> instanceCreator
         ) : base( factory, factoryParameters, connectionExtractor, instanceCreator )
      {
         this.Pool = new LocklessInstancePoolForClassesNoHeapAllocations<TConnectionInstance>();
      }

      protected LocklessInstancePoolForClassesNoHeapAllocations<TConnectionInstance> Pool { get; }

      public virtual async Task DisposeAsync( CancellationToken token )
      {
         TConnectionInstance conn;
         while ( ( conn = this.Pool.TakeInstance() ) != null )
         {
            await base.PerformDisposeConnectionAsync( conn, token, false, true );
         }
      }

      protected override async Task<TConnectionInstance> AcquireConnectionAsync( CancellationToken token )
      {
         var retVal = this.Pool.TakeInstance();
         if ( retVal == null )
         {
            // Create connection and await for events
            retVal = await this.AcquireConnectionAsync( token );
         }
         else
         {
            // Just await for acquire event
            var evt = this.AfterConnectionAcquiringEventInstance;
            if ( evt != null )
            {
               using ( var usageInfo = this.ConnectionExtractor( retVal ).GetConnectionUsageForToken( token ) )
               {
                  await evt.InvokeAndWaitForAwaitables( new AfterConnectionAcquiringEventArgs<TConnection>( usageInfo.Connection ) );
               }
            }
         }

         return retVal;
      }

      protected override async Task DisposeConnectionAsync( TConnectionInstance connection, CancellationToken token )
      {
         await this.PerformDisposeConnectionAsync( connection, token, true, false );

         if ( this.ConnectionExtractor( connection ).IsConnectionReturnableToPool )
         {
            this.Pool.ReturnInstance( connection );
         }
         else
         {
            await this.ConnectionExtractor( connection ).DisposeAsyncSafely( token );
         }
      }
   }

   public class CachingConnectionPoolWithTimeout<TConnection, TConnectionCreationParams> : CachingConnectionPool<TConnection, InstanceHolderWithTimestamp<ConnectionAcquireInfo<TConnection>>, TConnectionCreationParams, TimeSpan>
      where TConnection : class
      where TConnectionCreationParams : class
   {

      public CachingConnectionPoolWithTimeout(
         ConnectionFactory<TConnection, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters
         )
         : base( factory, factoryParameters, instance => instance.Instance, connection => new InstanceHolderWithTimestamp<ConnectionAcquireInfo<TConnection>>( connection ) )
      {
      }

      protected override async Task DisposeConnectionAsync( InstanceHolderWithTimestamp<ConnectionAcquireInfo<TConnection>> connection, CancellationToken token )
      {
         connection.JustBeforePuttingBackToPool();
         await base.DisposeConnectionAsync( connection, token );
      }

      public override async Task CleanUpAsync( TimeSpan maxConnectionIdleTime, CancellationToken token )
      {
         InstanceHolderWithTimestamp<ConnectionAcquireInfo<TConnection>> instance;
         var tasks = new List<Task>();
         while ( ( instance = this.Pool.TakeInstance() ) != null )
         {
            if ( DateTime.UtcNow - instance.WhenPutBackToPool > maxConnectionIdleTime )
            {
               tasks.Add( this.PerformDisposeConnectionAsync( instance, token, false, true ) );
            }
            else
            {
               this.Pool.ReturnInstance( instance );
            }
         }

         await Task.WhenAll( tasks );
      }
   }

   public sealed class InstanceHolderWithTimestamp<TInstance> : InstanceWithNextInfo<InstanceHolderWithTimestamp<TInstance>>
   {
      private InstanceHolderWithTimestamp<TInstance> _next;
      private Object _lastChanged;

      public InstanceHolderWithTimestamp( TInstance instance )
      {
         this.Instance = instance;
      }

      public TInstance Instance { get; }

      public InstanceHolderWithTimestamp<TInstance> Next
      {
         get
         {
            return this._next;
         }
         set
         {
            Interlocked.Exchange( ref this._next, value );
         }
      }

      public DateTime WhenPutBackToPool
      {
         get
         {
            return (DateTime) this._lastChanged;
         }
      }

      public void JustBeforePuttingBackToPool()
      {
         Interlocked.Exchange( ref this._lastChanged, DateTime.UtcNow );
      }
   }
}
