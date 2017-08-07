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

namespace UtilPack.ResourcePooling
{
   /// <summary>
   /// This interface is used by <see cref="OneTimeUseAsyncResourcePool{TResource, TConnectionInstance, TConnectionCreationParams}"/> to create a new instance of connection.
   /// </summary>
   /// <typeparam name="TResource">The type of resource.</typeparam>
   /// <typeparam name="TParams">The type of parameters used to create a resource.</typeparam>
   public interface ResourceFactory<TResource, in TParams>
   {
      /// <summary>
      /// Asynchronously creates a new instance of resource with given parameters.
      /// </summary>
      /// <param name="parameters">Parameters that are required for resource creation.</param>
      /// <param name="token">Cancellation token to use during resource creation.</param>
      /// <returns>A task which returns a <see cref="ResourceAcquireInfo{TResource}"/> upon completion.</returns>
      ValueTask<ResourceAcquireInfo<TResource>> AcquireResourceAsync( TParams parameters, CancellationToken token );
   }

   /// <summary>
   /// This interface provides information about resource which has been created by <see cref="ResourceFactory{TResource, TParams}"/>.
   /// Use <see cref="IAsyncDisposable.DisposeAsync(CancellationToken)"/> when it is ok to wait for proper resource disposal.
   /// Use <see cref="IDisposable.Dispose"/> method only when it is necessary to immediately close underlying resources.
   /// </summary>
   /// <typeparam name="TResource">The type of resource.</typeparam>
   public interface ResourceAcquireInfo<out TResource> : IAsyncDisposable, IDisposable
   {
      /// <summary>
      /// Gets the <see cref="ResourceUsageInfo{TResource}"/> to use a resource in association with given <see cref="CancellationToken"/>.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use when reacting to cancellation.</param>
      /// <returns>The <see cref="ResourceUsageInfo{TResource}"/>.</returns>
      /// <seealso cref="ResourceAcquireInfoImpl{TPublicResource, TPrivateResource}"/>
      /// <seealso cref="CancelableResourceUsageInfo{TResource}"/>
      ResourceUsageInfo<TResource> GetConnectionUsageForToken( CancellationToken token );

      // Return false if e.g. cancellation caused connection disposing.
      /// <summary>
      /// Gets the value indicating whether it is ok to return the resource of this <see cref="ResourceAcquireInfo{TResource}"/> to the pool.
      /// </summary>
      /// <value>The value indicating whether it is ok to return the resource of this <see cref="ResourceAcquireInfo{TResource}"/> to the pool.</value>
      Boolean IsResourceReturnableToPool { get; }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="ResourceAcquireInfo{TConnection}"/> using <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
   /// This class assumes that there is some kind of disposable resource object that is used to communicate with remote resource, represented by <typeparamref name="TPrivateResource"/>.
   /// </summary>
   /// <typeparam name="TPublicResource">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TPrivateResource">The actual type of underlying stream or other disposable resource.</typeparam>
   /// <remarks>
   /// Because most (all?) IO async methods are not cancelable via <see cref="CancellationToken"/> once the underlying native IO calls are invoked, this class simply disposes the underlying object that is used to communicate with remote resource.
   /// Only that way the any pending async IO calls get completed, since the exception will be thrown for them after disposing.
   /// It is not the most optimal solution, but it is how currently things are to be done, if we desire truly cancelable async IO calls.
   /// The alternative (lack of cancelability via <see cref="CancellationToken"/>) is worse option.
   /// </remarks>
   public abstract class ResourceAcquireInfoImpl<TPublicResource, TPrivateResource> : AbstractDisposable, ResourceAcquireInfo<TPublicResource>
   {
      private const Int32 NOT_CANCELED = 0;
      private const Int32 CANCELED = 1;

      private Int32 _cancellationState;

      private readonly Action<TPublicResource, CancellationToken> _setCancellationToken;
      private readonly Action<TPublicResource> _resetCancellationToken;

      /// <summary>
      /// Creates a new instance of <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> with given connection and disposable object that is used for communication with remote resource.
      /// </summary>
      /// <param name="publicResource">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> CBAM connection.</param>
      /// <param name="privateResource">The disposable object that is used for communication with remote resource.</param>
      /// <param name="setCancellationToken">The callback to set cancellation token to some external resource. Will be invoked in this constructor.</param>
      /// <param name="resetCancellationToken">The callback to reset cancellation token to some external resource. Will be invoked in <see cref="AbstractDisposable.Dispose(bool)"/> method.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="publicResource"/> or <paramref name="privateResource"/> is <c>null</c>.</exception>
      public ResourceAcquireInfoImpl(
         TPublicResource publicResource,
         TPrivateResource privateResource,
         Action<TPublicResource, CancellationToken> setCancellationToken,
         Action<TPublicResource> resetCancellationToken
         )
      {
         this.PublicResource = publicResource;
         this.Channel = privateResource;
         this._setCancellationToken = setCancellationToken;
         this._resetCancellationToken = resetCancellationToken;
      }

      /// <summary>
      /// Gets the value indicating whether this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> can be returned back to connection pool.
      /// </summary>
      /// <value>The value indicating whether this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> can be returned back to connection pool.</value>
      /// <remarks>
      /// On cancellation via <see cref="CancellationToken"/>, this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> will dispose the object used to communicate with remote resource.
      /// Because of this, this property will return <c>false</c> when the <see cref="CancellationToken"/> receives cancellation signal, or when <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.CanBeReturnedToPool"/> returns <c>false</c>.
      /// </remarks>
      public Boolean IsResourceReturnableToPool => this._cancellationState == NOT_CANCELED && this.PublicResourceCanBeReturnedToPool();

      /// <summary>
      /// This method implements <see cref="IAsyncDisposable.DisposeAsync(CancellationToken)"/> and will invoke <see cref="DisposeBeforeClosingChannel(CancellationToken)"/> before disposing this <see cref="Channel"/>.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use when disposing.</param>
      /// <returns>A task which will complete once asynchronous diposing routine is done and this <see cref="Channel"/> is closed.</returns>
      public async Task DisposeAsync( CancellationToken token )
      {
         try
         {
            await ( this.DisposeBeforeClosingChannel( token ) ?? TaskUtils.CompletedTask );
         }
         finally
         {
            this.Dispose( true );
         }
      }

      /// <summary>
      /// Returns a new <see cref="ResourceUsageInfo{TConnection}"/> in order to start logical scope of using connection, typically at the start of <see cref="AsyncResourcePool{TConnection}.UseResourceAsync(Func{TConnection, Task}, CancellationToken)"/> method.
      /// </summary>
      /// <param name="token">The cancellation token for this connection usage scenario.</param>
      /// <returns>A new instance of <see cref="ResourceUsageInfo{TConnection}"/> which should have its <see cref="IDisposable.Dispose"/> method called when the usage scenario ends.</returns>
      public ResourceUsageInfo<TPublicResource> GetConnectionUsageForToken( CancellationToken token )
      {
         return new CancelableResourceUsageInfo<TPublicResource>(
            this.PublicResource,
            token,
            token.Register( () =>
            {
               try
               {
                  Interlocked.Exchange( ref this._cancellationState, CANCELED );
               }
               finally
               {
                  // Since the Read/WriteAsync methods for e.g. NetworkStream are not truly async, we must close the whole connection on cancellation token cancel.
                  // This will cause exception to be thrown from Read/WriteAsync methods, and thus allow for execution to proceed, instead of remaining stuck in Read/WriteAsync methods.
                  this.Dispose( true );
               }
            } ),
            this._setCancellationToken,
            this._resetCancellationToken
            );
      }

      /// <summary>
      /// Gets the <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> instance of this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/>.
      /// </summary>
      /// <value>The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> instance of this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/>.</value>
      protected TPublicResource PublicResource { get; }

      /// <summary>
      /// Gets the object that is used to communicate with remote resource that this <see cref="PublicResource"/> represents.
      /// </summary>
      /// <value>The object that is used to communicate with remote resource that this <see cref="PublicResource"/> represents.</value>
      protected TPrivateResource Channel { get; }

      /// <summary>
      /// Derived classes should implement the custom logic when closing the connection.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use during disposing.</param>
      /// <returns>A task which performs disposing asynchronously, or <c>null</c> if disposing has been done synchronously.</returns>
      /// <remarks>
      /// Typically disposing process involves sending some data via this <see cref="Channel"/> to the remote resource indicating that this end is closing the connection.
      /// </remarks>
      protected abstract Task DisposeBeforeClosingChannel( CancellationToken token );

      /// <summary>
      /// This method should be overridden by derived classes to perform additional check whether the <see cref="PublicResource"/> can be returned back to resource pool.
      /// </summary>
      /// <returns><c>true</c> if <see cref="PublicResource"/> can be returned to pool; <c>false</c> otherwise.</returns>
      protected abstract Boolean PublicResourceCanBeReturnedToPool();

   }

   /// <summary>
   /// This interface represents a single scope of using one instance of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.
   /// </summary>
   /// <typeparam name="TResource"></typeparam>
   /// <remarks>The instances of this interface are obtained by <see cref="ResourceAcquireInfo{TConnection}.GetConnectionUsageForToken(CancellationToken)"/> method, and <see cref="IDisposable.Dispose"/> method for this <see cref="ResourceUsageInfo{TConnection}"/> should be called when usage is over.</remarks>
   public interface ResourceUsageInfo<out TResource> : IDisposable
   {
      /// <summary>
      /// Gets the <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> to be used.
      /// </summary>
      /// <value>The <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> to be used.</value>
      TResource Resource { get; }
   }

   // TODO move CancelableConnectionUsageInfo along other stuff to UtilPack (prolly new project, UtilPack.ResourcePooling).

   /// <summary>
   /// This class provides implementation for <see cref="ResourceUsageInfo{TConnection}"/> where the constructor invokes callback to set cancellation token, and <see cref="Dispose(bool)"/> method invokes callback to reset cancellation token.
   /// </summary>
   /// <typeparam name="TResource">The type of useable resource.</typeparam>
   public class CancelableResourceUsageInfo<TResource> : AbstractDisposable, ResourceUsageInfo<TResource>
   {
      // TODO consider extending UtilPack.UsingHelper, since that is essentially what this class does.

      private readonly Action<TResource> _resetCancellationToken;
      private readonly CancellationTokenRegistration _registration;

      /// <summary>
      /// Creates a new instance of <see cref="CancelableResourceUsageInfo{TConnection}"/> with given parameters.
      /// </summary>
      /// <param name="resource">The useable resource.</param>
      /// <param name="token">The <see cref="CancellationToken"/>.</param>
      /// <param name="registration">The <see cref="CancellationTokenRegistration"/> associated with this <see cref="CancelableResourceUsageInfo{TConnection}"/>.</param>
      /// <param name="setCancellationToken">The callback to set cancellation token to some external resource. Will be invoked in this constructor.</param>
      /// <param name="resetCancellationToken">The callback to reset cancellation token to some external resource. Will be invoked in <see cref="Dispose(bool)"/> method.</param>
      public CancelableResourceUsageInfo(
         TResource resource,
         CancellationToken token,
         CancellationTokenRegistration registration,
         Action<TResource, CancellationToken> setCancellationToken,
         Action<TResource> resetCancellationToken

         )
      {
         this.Resource = resource;
         setCancellationToken?.Invoke( resource, token );
         this._resetCancellationToken = resetCancellationToken;
         this._registration = registration;
      }

      /// <summary>
      /// Gets the resource associated with this <see cref="CancelableResourceUsageInfo{TConnection}"/>.
      /// </summary>
      /// <value>The resource associated with this <see cref="CancelableResourceUsageInfo{TConnection}"/>.</value>
      public TResource Resource { get; }

      /// <summary>
      /// This method will call the cancellation token reset callback given to constructor, and then dispose the <see cref="CancellationTokenRegistration"/> given to constructor.
      /// </summary>
      /// <param name="disposing">Whether this was called from <see cref="IDisposable.Dispose"/> method.</param>
      protected override void Dispose( Boolean disposing )
      {
         if ( disposing )
         {
            try
            {
               this._resetCancellationToken?.Invoke( this.Resource );
            }
            finally
            {
               this._registration.DisposeSafely();
            }
         }
      }
   }

   /// <summary>
   /// This class implements the <see cref="AsyncResourcePoolObservable{TResource}"/> interface in such way that the resource is disposed of after each use in <see cref="UseResourceAsync(Func{TResource, Task}, CancellationToken)"/> method.
   /// </summary>
   /// <typeparam name="TResource">The type of resource.</typeparam>
   /// <typeparam name="TConnectionInstance">The type of instance holding the resource.</typeparam>
   /// <typeparam name="TConnectionCreationParams">The type of parameters used to create a new instance of <typeparamref name="TConnectionInstance"/>.</typeparam>
   /// <remarks>
   /// While this class is useful in simple scenarios, e.g. testing, the actual production environments most likely will want to use <see cref="CachingAsyncResourcePoolWithTimeout{TConnection, TConnectionCreationParams}"/> which is inherited from this class.
   /// </remarks>
   /// <seealso cref="CachingAsyncResourcePoolWithTimeout{TResource, TConnectionCreationParams}"/>
   /// <seealso cref="CachingAsyncResourcePool{TResource, TConnectionInstance, TConnectionCreationParams}"/>
   public class OneTimeUseAsyncResourcePool<TResource, TConnectionInstance, TConnectionCreationParams> : AsyncResourcePoolObservable<TResource>
   {

      /// <summary>
      /// Creates a new instance of <see cref="OneTimeUseAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/>
      /// </summary>
      /// <param name="factory">The <see cref="ResourceFactory{TConnection, TParams}"/> to use for creation of new resources.</param>
      /// <param name="factoryParameters">The parameters to passe to <see cref="ResourceFactory{TConnection, TParams}"/> when creating new instances of resources.</param>
      /// <param name="connectionExtractor">The callback to extract <see cref="ResourceAcquireInfo{TConnection}"/> from instances of <typeparamref name="TConnectionInstance"/>.</param>
      /// <param name="instanceCreator">The callback to create a new instance of <typeparamref name="TConnectionInstance"/> from existing <see cref="ResourceAcquireInfo{TConnection}"/>.</param>
      /// <exception cref="ArgumentNullException">If any of <paramref name="factory"/>, <paramref name="connectionExtractor"/>, or <paramref name="instanceCreator"/> is <c>null</c>.</exception>
      public OneTimeUseAsyncResourcePool(
         ResourceFactory<TResource, TConnectionCreationParams> factory,
         TConnectionCreationParams factoryParameters,
         Func<TConnectionInstance, ResourceAcquireInfo<TResource>> connectionExtractor,
         Func<ResourceAcquireInfo<TResource>, TConnectionInstance> instanceCreator
      )
      {
         this.FactoryParameters = factoryParameters;
         this.Factory = ArgumentValidator.ValidateNotNull( nameof( factory ), factory );
         this.ConnectionExtractor = ArgumentValidator.ValidateNotNull( nameof( connectionExtractor ), connectionExtractor );
         this.InstanceCreator = ArgumentValidator.ValidateNotNull( nameof( instanceCreator ), instanceCreator );
      }

      /// <summary>
      /// Gets the <see cref="ResourceFactory{TConnection, TParams}"/> used to create new instances of resources.
      /// </summary>
      /// <value>The <see cref="ResourceFactory{TConnection, TParams}"/> used to create new instances of resources.</value>
      protected ResourceFactory<TResource, TConnectionCreationParams> Factory { get; }

      /// <summary>
      /// Gets the parameters passed to <see cref="ResourceFactory{TConnection, TParams}.AcquireResourceAsync(TParams, CancellationToken)"/> to create new instances of resources.
      /// </summary>
      /// <value>The parameters passed to <see cref="ResourceFactory{TConnection, TParams}.AcquireResourceAsync(TParams, CancellationToken)"/> to create new instances of resources.</value>
      protected TConnectionCreationParams FactoryParameters { get; }

      /// <summary>
      /// Gets the callback to extract <see cref="ResourceAcquireInfo{TConnection}"/> from instances of <typeparamref name="TConnectionInstance"/>.
      /// </summary>
      /// <value>The callback to extract <see cref="ResourceAcquireInfo{TConnection}"/> from instances of <typeparamref name="TConnectionInstance"/>.</value>
      protected Func<TConnectionInstance, ResourceAcquireInfo<TResource>> ConnectionExtractor { get; }

      /// <summary>
      /// Gets the callback to create a new instance of <typeparamref name="TConnectionInstance"/> from existing <see cref="ResourceAcquireInfo{TConnection}"/>.
      /// </summary>
      /// <value>The callback to create a new instance of <typeparamref name="TConnectionInstance"/> from existing <see cref="ResourceAcquireInfo{TConnection}"/>.</value>
      protected Func<ResourceAcquireInfo<TResource>, TConnectionInstance> InstanceCreator { get; }

      /// <summary>
      /// Implements <see cref="AsyncResourcePool{TConnection}.UseResourceAsync(Func{TConnection, Task}, CancellationToken)"/> method by validating that the given callback is not <c>null</c> and delegating implementation to private method.
      /// </summary>
      /// <param name="user">The callback to asynchronously use the resource.</param>
      /// <param name="token">The optional <see cref="CancellationToken"/>.</param>
      /// <returns>Task which completes after the <paramref name="user"/> callback completes and this resource pool has cleaned up any of its own internal resources.</returns>
      /// <remarks>
      /// Will return completed task if <paramref name="user"/> is <c>null</c> or cancellation is pequested for given <paramref name="token"/>.
      /// </remarks>
      public Task UseResourceAsync( Func<TResource, Task> user, CancellationToken token = default( CancellationToken ) )
      {
         // Avoid potentially allocating new instance of Task on every call to this method, and check for user and token cancellation first.
         return user == null ? TaskUtils.CompletedTask : ( token.IsCancellationRequested ?
            TaskUtils2.FromCanceled( token ) :
            this.DoUseConnectionAsync( user, token )
            );

      }

      private async Task DoUseConnectionAsync( Func<TResource, Task> executer, CancellationToken token )
      {
         var instance = await this.AcquireResourceAsync( token );

         try
         {
            using ( var usageInfo = this.ConnectionExtractor( instance ).GetConnectionUsageForToken( token ) )
            {
               await executer( usageInfo.Resource );
            }
         }
         finally
         {
            await ( this.DisposeResourceAsync( instance, token ) ?? TaskUtils.CompletedTask );
         }
      }

      /// <summary>
      /// Implements <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.AfterConnectionCreationEvent"/>.
      /// </summary>
      public event GenericEventHandler<AfterAsyncResourceCreationEventArgs<TResource>> AfterConnectionCreationEvent;

      /// <summary>
      /// Implements <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.AfterConnectionAcquiringEvent"/>.
      /// </summary>
      public event GenericEventHandler<AfterAsyncResourceAcquiringEventArgs<TResource>> AfterConnectionAcquiringEvent;

      /// <summary>
      /// Implements <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.BeforeConnectionReturningEvent"/>.
      /// </summary>
      public event GenericEventHandler<BeforeAsyncResourceReturningEventArgs<TResource>> BeforeConnectionReturningEvent;

      /// <summary>
      /// Implements <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.BeforeConnectionCloseEvent"/>.
      /// </summary>
      public event GenericEventHandler<BeforeAsyncResourceCloseEventArgs<TResource>> BeforeConnectionCloseEvent;

      /// <summary>
      /// Helper property to get instance of <see cref="AfterConnectionAcquiringEvent"/> for derived classes.
      /// </summary>
      /// <value>The instance of <see cref="AfterConnectionAcquiringEvent"/> for derived classes.</value>
      protected GenericEventHandler<AfterAsyncResourceAcquiringEventArgs<TResource>> AfterConnectionAcquiringEventInstance => this.AfterConnectionAcquiringEvent;

      /// <summary>
      /// This method is called by <see cref="UseResourceAsync(Func{TResource, Task}, CancellationToken)"/> before invoking the given asynchronous callback.
      /// The implementation in this class always uses <see cref="ResourceFactory{TConnection, TParams}.AcquireResourceAsync(TParams, CancellationToken)"/> method of this <see cref="Factory"/>, but derived classes may override this method to cache previously used resources into a pool.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>A task which will have instance of <typeparamref name="TConnectionInstance"/> upon completion.</returns>
      protected virtual async ValueTask<TConnectionInstance> AcquireResourceAsync( CancellationToken token )
      {
         var connAcquireInfo = await this.Factory.AcquireResourceAsync( this.FactoryParameters, token );
         var creationEvent = this.AfterConnectionCreationEvent;
         var acquireEvent = this.AfterConnectionAcquiringEvent;
         if ( creationEvent != null || acquireEvent != null )
         {
            using ( var usageInfo = connAcquireInfo.GetConnectionUsageForToken( token ) )
            {
               await creationEvent.InvokeAndWaitForAwaitables( new DefaultAfterAsyncResourceCreationEventArgs<TResource>( usageInfo.Resource ) );
               await acquireEvent.InvokeAndWaitForAwaitables( new DefaultAfterAsyncResourceAcquiringEventArgs<TResource>( usageInfo.Resource ) );
            }
         }

         return this.InstanceCreator( connAcquireInfo );
      }

      /// <summary>
      /// This method is called by <see cref="UseResourceAsync(Func{TResource, Task}, CancellationToken)"/> after the asynchronous callback has finished using the resource.
      /// </summary>
      /// <param name="connection">The resource that was used by the asynchronous callback of <see cref="UseResourceAsync(Func{TResource, Task}, CancellationToken)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/>.</param>
      /// <returns>Task which is completed when disposing the connection is completed.</returns>
      /// <remarks>
      /// This method simply calls <see cref="PerformDisposeConnectionAsync(TConnectionInstance, CancellationToken, ResourceDisposeKind)"/> and gives <c>true</c> to both boolean arguments.
      /// </remarks>
      protected virtual async Task DisposeResourceAsync( TConnectionInstance connection, CancellationToken token )
      {
         await this.PerformDisposeConnectionAsync( connection, token, ResourceDisposeKind.ReturnAndDispose );
      }

      /// <summary>
      /// This method will take care of invoking the <see cref="BeforeConnectionReturningEvent"/> and <see cref="BeforeConnectionCloseEvent"/> before actually disposing the given resource.
      /// </summary>
      /// <param name="resource">The resource instance.</param>
      /// <param name="token">The <see cref="CancellationToken"/>.</param>
      /// <param name="disposeKind">How to dispose the <paramref name="resource"/>.</param>
      /// <returns>A task which completes after all necessary event invocation and resource dispose code is done.</returns>
      /// <seealso cref="ResourceDisposeKind"/>
      protected async Task PerformDisposeConnectionAsync(
         TConnectionInstance resource,
         CancellationToken token,
         ResourceDisposeKind disposeKind
         )
      {
         var returningEvent = this.BeforeConnectionReturningEvent;
         var closingEvent = this.BeforeConnectionCloseEvent;
         var isResourceClosed = disposeKind.IsResourceClosed();
         var isResourceReturned = disposeKind.IsResourceReturned();
         if ( ( returningEvent != null && isResourceReturned ) || ( closingEvent != null && isResourceClosed ) )
         {
            using ( var usageInfo = this.ConnectionExtractor( resource ).GetConnectionUsageForToken( token ) )
            {
               if ( isResourceReturned )
               {
                  await returningEvent.InvokeAndWaitForAwaitables( new DefaultBeforeAsyncResourceReturningEventArgs<TResource>( usageInfo.Resource ) );
               }

               if ( isResourceClosed )
               {
                  await closingEvent.InvokeAndWaitForAwaitables( new DefaultBeforeAsyncResourceCloseEventArgs<TResource>( usageInfo.Resource ) );
               }
            }
         }

         if ( isResourceClosed )
         {
            await this.ConnectionExtractor( resource ).DisposeAsyncSafely( token );
         }
      }


   }

   /// <summary>
   /// This enumeration controls how <see cref="OneTimeUseAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}.PerformDisposeConnectionAsync(TConnectionInstance, CancellationToken, ResourceDisposeKind)"/> method will behave.
   /// </summary>
   public enum ResourceDisposeKind
   {
      /// <summary>
      /// The resource is closed, but closing happens right after returning the resource to the pool.
      /// Both the <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.BeforeConnectionReturningEvent"/> and <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.BeforeConnectionCloseEvent"/> will be invoked, in that order.
      /// Finally, the resource will be asynchronously disposed of.
      /// </summary>
      ReturnAndDispose,

      /// <summary>
      /// The resource is returned to the pool, but not closed.
      /// Only <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.BeforeConnectionReturningEvent"/> will be invoked, and the resource will not be disposed.
      /// </summary>
      OnlyReturn,

      /// <summary>
      /// The resource is closed, and not returned to the pool.
      /// Only <see cref="ResourcePoolObservation{TResource, TCreationArgs, TAcquiringArgs, TReturningArgs, TCloseArgs}.BeforeConnectionCloseEvent"/> will be invoked, and resource will be then disposed.
      /// </summary>
      OnlyDispose
   }

   /// <summary>
   /// This class extends <see cref="OneTimeUseAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/> and implements rudimentary pooling for resource instances.
   /// The pool is never cleared by this class though, since this class does not define any logic for how to perform clean-up operation for the pool.
   /// The <see cref="CachingAsyncResourcePoolWithTimeout{TConnection, TConnectionCreationParams}"/> extends this class and defines exactly that.
   /// </summary>
   /// <typeparam name="TResource">The type of resource.</typeparam>
   /// <typeparam name="TCachedResource">The type of instance holding the resource. This will be the type for <see cref="LocklessInstancePoolForClassesNoHeapAllocations{TInstance}"/> object used to pool resource instances.</typeparam>
   /// <typeparam name="TResourceCreationParams">The type of parameters used to create a new instance of <typeparamref name="TCachedResource"/>.</typeparam>
   /// <remarks>
   /// This class is not very useful by itself - instead it provides some common implementation for pooling resource instances.
   /// End-users will find <see cref="CachingAsyncResourcePoolWithTimeout{TConnection, TConnectionCreationParams}"/> much more useful.
   /// </remarks>
   /// <seealso cref="CachingAsyncResourcePoolWithTimeout{TConnection, TConnectionCreationParams}"/>
   public class CachingAsyncResourcePool<TResource, TCachedResource, TResourceCreationParams> : OneTimeUseAsyncResourcePool<TResource, TCachedResource, TResourceCreationParams>, IAsyncDisposable, IDisposable
      where TCachedResource : class, InstanceWithNextInfo<TCachedResource>
   {
      private const Int32 NOT_DISPOSED = 0;
      private const Int32 DISPOSED = 1;

      private Int32 _disposed;

      /// <summary>
      /// Creates a new instance of <see cref="CachingAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/> with given parameters.
      /// </summary>
      /// <param name="factory">The <see cref="ResourceFactory{TConnection, TParams}"/> to use for creation of new resources.</param>
      /// <param name="factoryParameters">The parameters to passe to <see cref="ResourceFactory{TConnection, TParams}"/> when creating new instances of resources.</param>
      /// <param name="connectionExtractor">The callback to extract <see cref="ResourceAcquireInfo{TConnection}"/> from instances of <typeparamref name="TCachedResource"/>.</param>
      /// <param name="instanceCreator">The callback to create a new instance of <typeparamref name="TCachedResource"/> from existing <see cref="ResourceAcquireInfo{TConnection}"/>.</param>
      /// <exception cref="ArgumentNullException">If any of <paramref name="factory"/>, <paramref name="connectionExtractor"/>, or <paramref name="instanceCreator"/> is <c>null</c>.</exception>
      public CachingAsyncResourcePool(
         ResourceFactory<TResource, TResourceCreationParams> factory,
         TResourceCreationParams factoryParameters,
         Func<TCachedResource, ResourceAcquireInfo<TResource>> connectionExtractor,
         Func<ResourceAcquireInfo<TResource>, TCachedResource> instanceCreator
         ) : base( factory, factoryParameters, connectionExtractor, instanceCreator )
      {
         this.Pool = new LocklessInstancePoolForClassesNoHeapAllocations<TCachedResource>();
      }

      /// <summary>
      /// Gets the actual instance-caching pool used by this <see cref="CachingAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/>.
      /// </summary>
      /// <value>The actual instance-caching pool used by this <see cref="CachingAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/>.</value>
      /// <seealso cref="LocklessInstancePoolForClassesNoHeapAllocations{TInstance}"/>
      protected LocklessInstancePoolForClassesNoHeapAllocations<TCachedResource> Pool { get; }

      /// <summary>
      /// Implements <see cref="IAsyncDisposable.DisposeAsync(CancellationToken)"/> method to empty the <see cref="Pool"/> from all connections and dispose them asynchronously.
      /// </summary>
      /// <param name="token">The cancellation token to use.</param>
      /// <returns>A task which will be completed when all resource instances in <see cref="Pool"/> are asynchronously disposed.</returns>
      public virtual async Task DisposeAsync( CancellationToken token )
      {
         if ( Interlocked.CompareExchange( ref this._disposed, DISPOSED, NOT_DISPOSED ) == NOT_DISPOSED )
         {
            TCachedResource conn;
            while ( ( conn = this.Pool.TakeInstance() ) != null )
            {
               try
               {
                  await base.PerformDisposeConnectionAsync( conn, token, ResourceDisposeKind.OnlyDispose );
               }
               catch
               {
                  // Most likely we will not enter here, but better be safe than sorry.
               }
            }
         }
      }

      /// <summary>
      /// Implements <see cref="IDisposable.Dispose"/> to empty the <see cref="Pool"/> from all connections and dispose them synchronously.
      /// </summary>
      /// <remarks>
      /// The asynchronous disposing via <see cref="DisposeAsync(CancellationToken)"/> method is preferable, but this method can be used when there is no time to wait potentially long time for all connections to become disposed asynchronously.
      /// </remarks>
      public void Dispose()
      {
         if ( Interlocked.CompareExchange( ref this._disposed, DISPOSED, NOT_DISPOSED ) == NOT_DISPOSED )
         {
            this.DisposeAllInPool();
         }
      }

      private void DisposeAllInPool()
      {
         TCachedResource conn;
         while ( ( conn = this.Pool.TakeInstance() ) != null )
         {
            this.ConnectionExtractor( conn ).DisposeSafely();
         }
      }

      /// <summary>
      /// This property checks whether this <see cref="CachingAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/> is disposed, or being in process of disposing.
      /// </summary>
      /// <value><c>true</c> if this <see cref="CachingAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}"/> is disposed, or being in process of disposing; <c>false</c> otherwise.</value>
      public Boolean Disposed => this._disposed == NOT_DISPOSED;

      /// <summary>
      /// This method overrides <see cref="OneTimeUseAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}.AcquireResourceAsync(CancellationToken)"/> to implement logic where instead of always using <see cref="ResourceFactory{TConnection, TParams}"/>, the existing resource instance may be acquired from this <see cref="Pool"/>.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use when acquiring new resource from <see cref="ResourceFactory{TConnection, TParams}"/>.</param>
      /// <returns>A task which completes when the resource has been acquired.</returns>
      protected override async ValueTask<TCachedResource> AcquireResourceAsync( CancellationToken token )
      {
         if ( this.Disposed )
         {
            throw new ObjectDisposedException( "This pool is already disposed or being disposed of." );
         }

         var retVal = this.Pool.TakeInstance();
         if ( retVal == null )
         {
            // Create connection and await for events
            retVal = await base.AcquireResourceAsync( token );
         }
         else
         {
            // Just await for acquire event
            var evt = this.AfterConnectionAcquiringEventInstance;
            if ( evt != null )
            {
               using ( var usageInfo = this.ConnectionExtractor( retVal ).GetConnectionUsageForToken( token ) )
               {
                  await evt.InvokeAndWaitForAwaitables( new DefaultAfterAsyncResourceAcquiringEventArgs<TResource>( usageInfo.Resource ) );
               }
            }
         }

         return retVal;
      }

      /// <summary>
      /// This method overrides <see cref="OneTimeUseAsyncResourcePool{TConnection, TConnectionInstance, TConnectionCreationParams}.DisposeResourceAsync(TConnectionInstance, CancellationToken)"/> in order to return the resource to this <see cref="Pool"/>, if it is returnable back to the pool as specified by <see cref="ResourceAcquireInfo{TConnection}.IsResourceReturnableToPool"/>.
      /// </summary>
      /// <param name="instance">The resource instance to dispose or to return to the <see cref="Pool"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use when disposing asynchronously.</param>
      /// <returns>A task which completes once all events have been invoked and resource either disposed of or returned to this <see cref="Pool"/>.</returns>
      protected override async Task DisposeResourceAsync( TCachedResource instance, CancellationToken token )
      {
         await this.PerformDisposeConnectionAsync( instance, token, ResourceDisposeKind.OnlyReturn );
         var info = this.ConnectionExtractor( instance );

         if ( !this.Disposed && info.IsResourceReturnableToPool )
         {
            this.Pool.ReturnInstance( instance );
            // We might've disposed or started to dispose asynchronously after returning to pool in such way that racing condition may occur.
            if ( this.Disposed )
            {
               this.DisposeAllInPool();
            }
         }
         else
         {
            await info.DisposeAsyncSafely( token );
         }
      }
   }

   /// <summary>
   /// This class extends <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}"/> to add information when the resource was returned to the pool, in order to be able to clean it up later using <see cref="CleanUpAsync(TimeSpan, CancellationToken)"/> method.
   /// </summary>
   /// <typeparam name="TResource">The type of resource exposed to public.</typeparam>
   /// <typeparam name="TResourceCreationParams">The type of parameters used to create a new instance of <see cref="InstanceHolderWithTimestamp{TResource}"/>.</typeparam>
   public class CachingAsyncResourcePoolWithTimeout<TResource, TResourceCreationParams> : CachingAsyncResourcePool<TResource, InstanceHolderWithTimestamp<ResourceAcquireInfo<TResource>>, TResourceCreationParams>, AsyncResourcePoolObservable<TResource, TimeSpan>
   {
      /// <summary>
      /// Creates a new instance of <see cref="CachingAsyncResourcePoolWithTimeout{TResource, TResourceCreationParams}"/> with given parameters.
      /// </summary>
      /// <param name="factory">The <see cref="ResourceFactory{TResource, TParams}"/> to use when needed to create new instances of resource.</param>
      /// <param name="factoryParameters">The parameters to pass when using <see cref="ResourceFactory{TResource, TParams}.AcquireResourceAsync(TParams, CancellationToken)"/> of <paramref name="factory"/>.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="factory"/> is <c>null</c>.</exception>
      public CachingAsyncResourcePoolWithTimeout(
         ResourceFactory<TResource, TResourceCreationParams> factory,
         TResourceCreationParams factoryParameters
         )
         : base( factory, factoryParameters, instance => instance.Instance, connection => new InstanceHolderWithTimestamp<ResourceAcquireInfo<TResource>>( connection ) )
      {
      }

      /// <summary>
      /// This method overrides <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.DisposeResourceAsync(TCachedResource, CancellationToken)"/> to register return time of the resource.
      /// </summary>
      /// <param name="instance">The resource instance to dispose or to return to the <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.Pool"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use when disposing asynchronously.</param>
      /// <returns>A task which completes once all events have been invoked and resource either disposed of or returned to this <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.Pool"/>.</returns>
      protected override async Task DisposeResourceAsync( InstanceHolderWithTimestamp<ResourceAcquireInfo<TResource>> instance, CancellationToken token )
      {
         instance.JustBeforePuttingBackToPool();
         await base.DisposeResourceAsync( instance, token );
      }

      /// <summary>
      /// This method implements <see cref="AsyncResourcePoolCleanUp{TCleanUpParameter}.CleanUpAsync(TCleanUpParameter, CancellationToken)"/> with <see cref="TimeSpan"/> as clean-up parameter.
      /// The <paramref name="maxConnectionIdleTime"/> will serve as limit: this method will dispose all resource instances which have been idle in the <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.Pool"/> for longer than the limit.
      /// During this method execution, this <see cref="CachingAsyncResourcePoolWithTimeout{TResource, TResourceCreationParams}"/> will continue to be usable as normally.
      /// </summary>
      /// <param name="maxConnectionIdleTime">The maximum idle time for resource in the <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.Pool"/> for it to be returned back to the pool. If idle time is longer than (operator <c>&gt;</c>) this parameter, then the resource will be disposed.</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use when disposing.</param>
      /// <returns>A task which will be complete when all of the resource instances of <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.Pool"/> have been checked and resource eligible for clean-up will be disposed of.</returns>
      public async Task CleanUpAsync( TimeSpan maxConnectionIdleTime, CancellationToken token )
      {
         InstanceHolderWithTimestamp<ResourceAcquireInfo<TResource>> instance;
         var tasks = new List<Task>();
         while ( ( instance = this.Pool.TakeInstance() ) != null )
         {
            if ( DateTime.UtcNow - instance.TimeOfReturningBackToPool > maxConnectionIdleTime )
            {
               tasks.Add( this.PerformDisposeConnectionAsync( instance, token, ResourceDisposeKind.OnlyDispose ) );
            }
            else
            {
               this.Pool.ReturnInstance( instance );
            }
         }

         await
#if NET40
            TaskEx
#else
            Task
#endif
            .WhenAll( tasks );
      }
   }

   /// <summary>
   /// This class is used by <see cref="CachingAsyncResourcePoolWithTimeout{TResource, TResourceCreationParams}"/> to capture the time when resource instance was returned to the <see cref="CachingAsyncResourcePool{TResource, TCachedResource, TResourceCreationParams}.Pool"/>.
   /// </summary>
   /// <typeparam name="TResource">The type of resource.</typeparam>
   public sealed class InstanceHolderWithTimestamp<TResource> : InstanceWithNextInfo<InstanceHolderWithTimestamp<TResource>>
   {
      private InstanceHolderWithTimestamp<TResource> _next;
      private Object _lastChanged;

      /// <summary>
      /// Creates a new instance of <see cref="InstanceHolderWithTimestamp{TResource}"/>
      /// </summary>
      /// <param name="instance"></param>
      public InstanceHolderWithTimestamp( TResource instance )
      {
         this.Instance = instance;
      }

      /// <summary>
      /// Gets the resource instance.
      /// </summary>
      /// <value>The resoruce instance.</value>
      public TResource Instance { get; }

      /// <summary>
      /// Gets or sets the next <see cref="InstanceHolderWithTimestamp{TResource}"/> for this pool.
      /// </summary>
      /// <value>The next <see cref="InstanceHolderWithTimestamp{TResource}"/> for this pool.</value>
      public InstanceHolderWithTimestamp<TResource> Next
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

      /// <summary>
      /// Gets the <see cref="DateTime"/> when this instance was returned to pool.
      /// </summary>
      /// <value>The <see cref="DateTime"/> when this instance was returned to pool.</value>
      /// <exception cref="NullReferenceException">If this <see cref="InstanceHolderWithTimestamp{TResource}"/> has never been put back to the pool before.</exception>
      public DateTime TimeOfReturningBackToPool
      {
         get
         {
            return (DateTime) this._lastChanged;
         }
      }

      /// <summary>
      /// Marks this <see cref="InstanceHolderWithTimestamp{TResource}"/> as being returned to pool, thus updating the value of <see cref="TimeOfReturningBackToPool"/>.
      /// </summary>
      public void JustBeforePuttingBackToPool()
      {
         Interlocked.Exchange( ref this._lastChanged, DateTime.UtcNow );
      }
   }
}

namespace CBAM.Abstractions.Implementation
{
   /// <summary>
   /// This class implements <see cref="ResourceFactory{TResource, TParams}"/> in order to create instances of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> as resource instances.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TVendor">The actual type of <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of parameters to create instances of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TStatement">The actual type of statement which modifies/queries remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of read-only information about <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of enumerable items returned by statement execution.</typeparam>
   public abstract class DefaultConnectionFactory<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem> : ResourceFactory<TConnection, TConnectionCreationParameters>
   where TConnection : ConnectionImpl<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TConnectionFunctionality>
   where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor>
   where TStatement : TStatementInformation
   {
      /// <summary>
      /// This method implements <see cref="ResourceFactory{TResource, TParams}.AcquireResourceAsync(TParams, CancellationToken)"/> by calling a number of abstract methods in this class.
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
      public async ValueTask<ResourceAcquireInfo<TConnection>> AcquireResourceAsync( TConnectionCreationParameters parameters, CancellationToken token )
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

      /// <summary>
      /// This method is called by <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/> initially, to create <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> for the <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/>.
      /// </summary>
      /// <param name="parameters">The parameters needed to create a new instance of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>A task which will result in <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> when completed.</returns>
      protected abstract ValueTask<TConnectionFunctionality> CreateConnectionFunctionality( TConnectionCreationParameters parameters, CancellationToken token );

      /// <summary>
      /// This method is called after <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/>, in order to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> after <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> has been created.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> created by <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <returns>A task which will result in <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> when completed.</returns>
      protected abstract ValueTask<TConnection> CreateConnection( TConnectionFunctionality functionality );

      /// <summary>
      /// This method is called after <see cref="CreateConnection(TConnectionFunctionality)"/> in order to create instance of <see cref="ResourceAcquireInfo{TResource}"/> to be returned from <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/> method.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> created by <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> created by <see cref="CreateConnection(TConnectionFunctionality)"/> method.</param>
      /// <returns>A new instance of <see cref="ResourceAcquireInfo{TResource}"/>.</returns>
      protected abstract ResourceAcquireInfo<TConnection> CreateConnectionAcquireInfo( TConnectionFunctionality functionality, TConnection connection );

      /// <summary>
      /// This method is called whenever an error occurs within <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/> method.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>A task which completes after error handling is done.</returns>
      protected abstract Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }

   /// <summary>
   /// This class provides CBAM-related implementation for <see cref="ResourceAcquireInfoImpl{TConnection, TChannel}"/> using <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TStatement">The actual type of statement which modifies/queries remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of read-only information about <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of parameters to create instances of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of enumerable items returned by statement execution.</typeparam>
   /// <typeparam name="TVendor">The actual type of <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</typeparam>
   /// <typeparam name="TStream">The actual type of underlying stream or other disposable resource.</typeparam>
   public abstract class ConnectionAcquireInfoImpl<TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream> : ResourceAcquireInfoImpl<TConnection, TStream>, ResourceAcquireInfo<TConnection>
      where TStatement : TStatementInformation
      where TConnection : ConnectionImpl<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TConnectionFunctionality>
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor>
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TStream : IDisposable
   {
      /// <summary>
      /// Creates a new instance of <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> with given parameters.
      /// </summary>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/>.</param>
      /// <param name="associatedStream">The underlying stream or other disposable resource.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="connection"/> is <c>null</c>.</exception>
      public ConnectionAcquireInfoImpl(
         TConnection connection,
         TStream associatedStream
         ) : base( ArgumentValidator.ValidateNotNull( nameof( connection ), connection ), associatedStream, ( c, t ) => c.ConnectionFunctionality.CurrentCancellationToken = t, c => c.ConnectionFunctionality.ResetCancellationToken() )
      {

      }

      /// <summary>
      /// This method overrides <see cref="AbstractDisposable.Dispose(bool)"/> and will dispose this <see cref="ResourceAcquireInfoImpl{TPublicResource, TPrivateResource}.Channel"/> if <paramref name="disposing"/> is <c>true</c>.
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
      /// This method forwards the disposing for <see cref="DisposeBeforeClosingStream(CancellationToken, TConnectionFunctionality)"/> and giving <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}.ConnectionFunctionality"/> as second parameter to the method.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
      /// <returns>A task which performs disposing asynchronously, or <c>null</c> if disposing has been done synchronously.</returns>
      protected override Task DisposeBeforeClosingChannel( CancellationToken token )
      {
         return this.DisposeBeforeClosingStream( token, this.PublicResource.ConnectionFunctionality );
      }

      /// <summary>
      /// Overrides the abstract <see cref="ResourceAcquireInfoImpl{TConnection, TStream}.PublicResourceCanBeReturnedToPool"/> method and forwards the call to <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.CanBeReturnedToPool"/>.
      /// </summary>
      /// <returns>The value indicating whether this <see cref="ConnectionAcquireInfoImpl{TConnection, TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TStream}"/> can be returned to pool, as indicated by <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.CanBeReturnedToPool"/> property.</returns>
      protected override Boolean PublicResourceCanBeReturnedToPool()
      {
         return this.PublicResource.ConnectionFunctionality.CanBeReturnedToPool;
      }

      /// <summary>
      /// Derived classes should implement the disposing functionality, see <see cref="ResourceAcquireInfoImpl{TConnection, TStream}.DisposeBeforeClosingChannel(CancellationToken)"/>.
      /// </summary>
      /// <param name="token">The cancellation token to use.</param>
      /// <param name="connectionFunctionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</param>
      /// <returns>A task which performs disposing asynchronously, or <c>null</c> if disposing has been done synchronously.</returns>
      protected abstract Task DisposeBeforeClosingStream( CancellationToken token, TConnectionFunctionality connectionFunctionality );


   }


   /// <summary>
   /// This class extends <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}"/> to provide functionality which is common for connections operating on a stream or other <see cref="IDisposable"/> object.
   /// </summary>
   /// <typeparam name="TConnection">The actual type of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.</typeparam>
   /// <typeparam name="TVendor">The actual type of <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</typeparam>
   /// <typeparam name="TConnectionCreationParameters">The type of parameter containing enough information to create an instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/>.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The actual type of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of parameters to create instances of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TStatement">The actual type of statement which modifies/queries remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of read-only information about <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of enumerable items returned by statement execution.</typeparam>
   public abstract class ConnectionFactorySU<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem> : DefaultConnectionFactory<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem>
      where TStatement : TStatementInformation
      where TConnection : ConnectionImpl<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TConnectionFunctionality>
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor>
   {

      /// <summary>
      /// This task overrides <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.OnConnectionAcquirementError(TConnectionFunctionality, TConnection, CancellationToken, Exception)"/> and calls <see cref="ExtractStreamOnConnectionAcquirementError(TConnectionFunctionality, TConnection, CancellationToken, Exception)"/> in order to then safely synchronously dispose it.
      /// </summary>
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/>.</param>
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
      /// <param name="functionality">The <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.CreateConnectionFunctionality(TConnectionCreationParameters, CancellationToken)"/> method.</param>
      /// <param name="connection">The <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> or <c>null</c> if error occurred during <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.CreateConnection(TConnectionFunctionality)"/>.</param>
      /// <param name="token">The <see cref="CancellationToken"/> passed to <see cref="DefaultConnectionFactory{TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, TStatementCreationArgs, TStatement, TStatementInformation, TEnumerableItem}.AcquireResourceAsync(TConnectionCreationParameters, CancellationToken)"/>.</param>
      /// <param name="error">The error which occurred.</param>
      /// <returns>The underlying stream or other <see cref="IDisposable"/> object.</returns>
      protected abstract IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }

   /// <summary>
   /// This class will be moved to UtilPack project. It contains async utility methods lacking from current <see cref="UtilPack.TaskUtils"/> class.
   /// </summary>
   public static class TaskUtils2
   {
      /// <summary>
      /// Creates a new instance of <see cref="Task"/> which has already been canceled.
      /// </summary>
      /// <param name="token">The <see cref="CancellationToken"/>.</param>
      /// <returns>A new instance of <see cref="Task"/> which has already been canceled.</returns>
      /// <exception cref="ArgumentException">If <see cref="CancellationToken.IsCancellationRequested"/> of given <paramref name="token"/> returns <c>false</c>.</exception>
      /// <remarks>
      /// Due to limitations of public async API, the private state bits of returned task will be slightly different than the ones returned by framework's own corresponding method.
      /// </remarks>
      public static Task FromCanceled( CancellationToken token )
      {
         if ( !token.IsCancellationRequested )
         {
            throw new ArgumentException( nameof( token ) );
         }
         return new Task( () => { }, token, TaskCreationOptions.None );
      }

      /// <summary>
      /// Creates a new instance of <see cref="Task{T}"/> which has already been canceled.
      /// </summary>
      /// <typeparam name="T">The type of task result.</typeparam>
      /// <param name="token">The <see cref="CancellationToken"/>.</param>
      /// <returns>A new instance of <see cref="Task{T}"/> which has already been canceled.</returns>
      /// <exception cref="ArgumentException">If <see cref="CancellationToken.IsCancellationRequested"/> of given <paramref name="token"/> returns <c>false</c>.</exception>
      /// <remarks>
      /// Due to limitations of public async API, the private state bits of returned task will be slightly different than the ones returned by framework's own corresponding method.
      /// </remarks>
      public static Task<T> FromCanceled<T>( CancellationToken token )
      {
         if ( !token.IsCancellationRequested )
         {
            throw new ArgumentException( nameof( token ) );
         }
         return new Task<T>( () => default( T ), token, TaskCreationOptions.None );
      }
   }
}

/// <summary>
/// This class will be moved to UtilPack project. It contains extension methods for types defined there.
/// </summary>
public static class E_UtilPack
{
   /// <summary>
   /// Returns <c>true</c> if this <see cref="ResourceDisposeKind"/> is the kind which indicates returning resource to pool.
   /// </summary>
   /// <param name="kind">This <see cref="ResourceDisposeKind"/>.</param>
   /// <returns><c>true</c> if this <see cref="ResourceDisposeKind"/> is either <see cref="ResourceDisposeKind.ReturnAndDispose"/> or <see cref="ResourceDisposeKind.OnlyReturn"/>; <c>false</c> otherwise.</returns>
   public static Boolean IsResourceReturned( this ResourceDisposeKind kind )
   {
      return kind == ResourceDisposeKind.ReturnAndDispose || kind == ResourceDisposeKind.OnlyReturn;
   }

   /// <summary>
   /// Returns <c>true</c> if this <see cref="ResourceDisposeKind"/> is the kind which indicates disposal of resource.
   /// </summary>
   /// <param name="kind">This <see cref="ResourceDisposeKind"/>.</param>
   /// <returns><c>true</c> if this <see cref="ResourceDisposeKind"/> is either <see cref="ResourceDisposeKind.ReturnAndDispose"/> or <see cref="ResourceDisposeKind.OnlyDispose"/>; <c>false</c> otherwise.</returns>
   public static Boolean IsResourceClosed( this ResourceDisposeKind kind )
   {
      return kind == ResourceDisposeKind.ReturnAndDispose || kind == ResourceDisposeKind.OnlyDispose;
   }
}