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
using AsyncEnumeration.Abstractions;
using AsyncEnumeration.Implementation.Enumerable;
using CBAM.Abstractions;
using CBAM.Abstractions.Implementation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.Abstractions.Implementation
{

   /// <summary>
   /// This interface provides a way to execute some code with checks that the connection is still useable, using <see cref="ReservedForStatement"/> as token.
   /// This is typically desireable when connection uses unseekable stream (SU) as its communication channel to remote endpoint.
   /// </summary>
   /// <remarks>
   /// These methods should really be protected methods of <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>, but since there is no multi-inheritance, this is an interface.
   /// </remarks>
   public interface ConnectionFunctionalitySU
   {
      /// <summary>
      /// This method will make sure that connection is reserved for given statement, and if so, execute the given asynchronous callback. The connection is then left as reserved for <paramref name="reservedState"/>.
      /// </summary>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement the connection should be reserved to.</param>
      /// <param name="action">The asynchronous callback to execute, if the connection is reserved to statement represented by <see cref="ReservedForStatement"/>.</param>
      /// <param name="throwIfBusy">If set to <c>true</c>, will throw an exception if currently underlying stream is reserved for other activity than the one identified by <paramref name="reservedState"/>.</param>
      /// <returns>A task which will complete when <paramref name="action"/> is completed and connection reservation state has been restored to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If <paramref name="throwIfBusy"/> is <c>true</c>, and if this connection is not reserved to statement represented by given <see cref="ReservedForStatement"/> object.</exception>
      Task UseStreamWithinStatementAsync( ReservedForStatement reservedState, Func<Task> action, Boolean throwIfBusy );

      /// <summary>
      /// This method will make sure that connection is reserved for given statement, and if so, execute the given asynchronous callback. The connection is then left as reserved for <paramref name="reservedState"/>.
      /// </summary>
      /// <typeparam name="T">The asynchronous return type of <paramref name="func"/>.</typeparam>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement the connection should be reserved to.</param>
      /// <param name="func">The asynchronous callback to execute, if the connection is reserved to statement represented by <see cref="ReservedForStatement"/>.</param>
      /// <returns>A task which will return result of <paramref name="func"/> and complete when <paramref name="func"/> is completed and connection reservation state has been restored to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If this connection is not reserved to statement represented by given <paramref name="reservedState"/> object.</exception>
      ValueTask<T> UseStreamWithinStatementAsync<T>( ReservedForStatement reservedState, Func<ValueTask<T>> func );

      /// <summary>
      /// This method will make sure that connection is not reserved for any statement, and if so, execute the given asynchronous callback.
      /// </summary>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement reservation.</param>
      /// <param name="action">The asynchronous callback to execute, if the connection is not reserved for any statement.</param>
      /// <param name="oneTimeOnly">If <c>true</c>, the connection reservation state will be returned to original after <paramref name="action"/> has executed. Otherwise, the connection will be left in reserved state to <paramref name="reservedState"/>.</param>
      /// <param name="throwIfBusy">If set to <c>true</c>, will throw an exception if currently underlying stream is reserved for other activity than the one identified by <paramref name="reservedState"/>.</param>
      /// <returns>A task which will complete when <paramref name="action"/> is completed and connection reservation state has been restored, if <paramref name="oneTimeOnly"/> is <c>true</c>, to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If <paramref name="throwIfBusy"/> is <c>true</c>, and if this connection is already reserved to another statement.</exception>
      Task UseStreamOutsideStatementAsync( ReservedForStatement reservedState, Func<Task> action, Boolean oneTimeOnly, Boolean throwIfBusy );

      /// <summary>
      /// This method will make sure that connection is not reserved for any statement, and if so, execute the given asynchronous callback.
      /// </summary>
      /// <typeparam name="T">The asynchronous return type of <paramref name="func"/>.</typeparam>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement reservation.</param>
      /// <param name="func">The asynchronous callback to execute, if the connection is not reserved for any statement.</param>
      /// <param name="oneTimeOnly">If <c>true</c>, the connection reservation state will be returned to original after <paramref name="func"/> has executed. Otherwise, the connection will be left in reserved state to <paramref name="reservedState"/>.</param>
      /// <returns>A task which will return result of <paramref name="func"/> and complete when <paramref name="func"/> is completed and connection reservation state has been restored, if <paramref name="oneTimeOnly"/> is <c>true</c>, to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If this connection is already reserved to another statement.</exception>
      ValueTask<T> UseStreamOutsideStatementAsync<T>( ReservedForStatement reservedState, Func<ValueTask<T>> func, Boolean oneTimeOnly );

      /// <summary>
      /// This method will make sure that connection is reserved to given statement, and then perform potentially asynchronous dispose operations.
      /// </summary>
      /// <param name="reservationObject">The <see cref="ReservedForStatement"/> object identifying the statement reservation.</param>
      /// <returns>A task which will complete when dipose operations are done, and the connection reservation state has been cleared.</returns>
      /// <exception cref="InvalidOperationException">If this connection is not reserved to statement represented by given <paramref name="reservationObject"/> object.</exception>
      Task DisposeStatementAsync( ReservedForStatement reservationObject );
   }

   /// <summary>
   /// This class extends <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> in order to provide specialized implementation for situations when unseekable stream (SU) (e.g. <see cref="T:System.Net.Sockets.NetworkStream"/>) is used to communicate with remote resource.
   /// In such scenarios, the whole stream is typically reserved for execution of single statement at a time.
   /// </summary>
   /// <typeparam name="TStatement">The type of statement to modify/query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The read-only information about the statement.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of arguments to create a new statement.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of items enumerated by statement.</typeparam>
   /// <typeparam name="TVendor">The type of <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</typeparam>
   public abstract class ConnectionFunctionalitySU<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor> : PooledConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem>, ConnectionFunctionalitySU
      where TStatement : TStatementInformation
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   {
      private sealed class NotInUse : ConnectionStreamUsageState
      {
         private NotInUse()
         {

         }

         public static readonly NotInUse Instance = new NotInUse();
      }



      private ConnectionStreamUsageState _currentlyExecutingStatement;

      /// <summary>
      /// Creates a new instance of <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
      /// </summary>
      /// <param name="vendor">The <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</param>
      /// <param name="asyncProvider">The optional custom <see cref="IAsyncProvider"/>.</param>
      public ConnectionFunctionalitySU(
         TVendor vendor,
         IAsyncProvider asyncProvider
         )
         : base( vendor )
      {
         this._currentlyExecutingStatement = NotInUse.Instance;
         this.AsyncProvider = ArgumentValidator.ValidateNotNull( nameof( asyncProvider ), asyncProvider );
      }

      /// <summary>
      /// Gets the <see cref="IAsyncProvider"/> that is used for creating <see cref="IAsyncEnumerable{T}"/>s
      /// </summary>
      protected IAsyncProvider AsyncProvider { get; }

      /// <summary>
      /// Implements <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.CreateEnumerable"/> by creating sequential exclusive enumerator (<see cref="AsyncEnumerationFactory.CreateExclusiveSequentialEnumerable{T}(SequentialEnumerationStartInfo{T}, IAsyncProvider)"/>).
      /// The <see cref="ExecuteStatement(TStatementInformation, ReservedForStatement)"/> method will be used to perform initial execution, after which the callback returned by the method will be used.
      /// </summary>
      /// <param name="metadata">The statement information.</param>
      /// <returns>The <see cref="IAsyncEnumerable{T}"/> that can sequentially enumerate items from underlying stream.</returns>
      /// <seealso cref="AsyncEnumerationFactory.CreateExclusiveSequentialEnumerable{T}(SequentialEnumerationStartInfo{T}, IAsyncProvider)"/>
      protected override IAsyncEnumerable<TEnumerableItem> CreateEnumerable(
         TStatementInformation metadata
         )
      {
         ReservedForStatement reservation = null;
         Func<ValueTask<(Boolean, TEnumerableItem)>> moveNext = null;

         return AsyncEnumerationFactory.CreateExclusiveSequentialEnumerable( AsyncEnumerationFactory.CreateSequentialStartInfo(
               async () =>
               {
                  TEnumerableItem item;
                  Boolean success;
                  if ( reservation == null && Interlocked.CompareExchange( ref reservation, this.CreateReservationObject( metadata ), null ) == null )
                  {
                     (item, success, moveNext) = await this.UseStreamOutsideStatementAsync(
                        reservation,
                        async () => await this.ExecuteStatement( metadata, reservation ),
                        false
                        );
                  }
                  else
                  {
                     if ( moveNext == null )
                     {
                        success = false;
                        item = default;
                     }
                     else
                     {
                        (success, item) = await this.UseStreamWithinStatementAsync( reservation, moveNext );
                     }
                  }

                  return (success, item);
               },
               () =>
               {
                  var seenReservation = reservation;

                  Interlocked.Exchange( ref reservation, null );
                  Interlocked.Exchange( ref moveNext, null );

                  return this.DisposeStatementAsync( seenReservation );
               } ),
               this.AsyncProvider
               );

      }

      /// <summary>
      /// Derived classes should override this abstract method to provide custom execution logic for statement.
      /// </summary>
      /// <param name="stmt">The read-only information about statement being executed.</param>
      /// <param name="reservationObject">The reservation object created by <see cref="CreateReservationObject(TStatementInformation)"/>.</param>
      /// <returns>Information about the backend result and how to enumerate more results.</returns>
      /// <remarks>
      /// The connection will be marked as reserved to the statement before this method is called.
      /// </remarks>
      protected abstract ValueTask<(TEnumerableItem, Boolean, Func<ValueTask<(Boolean, TEnumerableItem)>>)> ExecuteStatement( TStatementInformation stmt, ReservedForStatement reservationObject );

      /// <summary>
      /// Derived classes should override this abstract method to create custom <see cref="ReservedForStatement"/> objects.
      /// </summary>
      /// <param name="stmt">The read-only information about statement being executed.</param>
      /// <returns>A new instance of <see cref="ReservedForStatement"/> which will be used to mark this connection as being in use to execute the <paramref name="stmt"/>.</returns>
      protected abstract ReservedForStatement CreateReservationObject( TStatementInformation stmt );


      /// <inheritdoc />
      public async Task UseStreamOutsideStatementAsync( ReservedForStatement reservedState, Func<Task> action, Boolean oneTimeOnly, Boolean throwIfBusy )
      {
         if ( ReferenceEquals( Interlocked.CompareExchange( ref this._currentlyExecutingStatement, reservedState, NotInUse.Instance ), NotInUse.Instance ) )
         {
            try
            {
               await this.UseStreamWithinStatementAsync( reservedState, action );
            }
            finally
            {
               if ( oneTimeOnly )
               {
                  Interlocked.Exchange( ref this._currentlyExecutingStatement, NotInUse.Instance );
               }
            }
         }
         else if ( throwIfBusy )
         {
            throw new InvalidOperationException( "The connection is currently being used by another statement." );
         }
      }

      /// <inheritdoc />
      public async ValueTask<T> UseStreamOutsideStatementAsync<T>( ReservedForStatement reservedState, Func<ValueTask<T>> func, Boolean oneTimeOnly )
      {
         if ( ReferenceEquals( Interlocked.CompareExchange( ref this._currentlyExecutingStatement, reservedState, NotInUse.Instance ), NotInUse.Instance ) )
         {
            try
            {
               return await this.UseStreamWithinStatementAsync( reservedState, func );
            }
            finally
            {
               if ( oneTimeOnly )
               {
                  Interlocked.Exchange( ref this._currentlyExecutingStatement, NotInUse.Instance );
               }
            }
         }
         else
         {
            throw new InvalidOperationException( "The connection is currently being used by another statement." );
         }
      }

      /// <inheritdoc />
      public async Task UseStreamWithinStatementAsync( ReservedForStatement reservedState, Func<Task> action, Boolean throwIfBusy = true )
      {
         ArgumentValidator.ValidateNotNull( nameof( reservedState ), reservedState );
         ConnectionStreamUsageState prevState;
         if ( ReferenceEquals( ( prevState = Interlocked.CompareExchange( ref this._currentlyExecutingStatement, reservedState.UsageState, reservedState ) ), reservedState ) // Transition
            || ReferenceEquals( prevState, reservedState.UsageState ) // Re-entrance
            )
         {
            try
            {
               await action();
            }
            finally
            {
               Interlocked.Exchange( ref this._currentlyExecutingStatement, prevState );
            }
         }
         else if ( throwIfBusy )
         {
            throw new InvalidOperationException( "The stream is not reserved for this statement." );
         }
      }

      /// <inheritdoc />
      public async ValueTask<T> UseStreamWithinStatementAsync<T>( ReservedForStatement reservedState, Func<ValueTask<T>> func )
      {
         ArgumentValidator.ValidateNotNull( nameof( reservedState ), reservedState );
         ConnectionStreamUsageState prevState;
         if ( ReferenceEquals( ( prevState = Interlocked.CompareExchange( ref this._currentlyExecutingStatement, reservedState.UsageState, reservedState ) ), reservedState ) // Transition
            || ReferenceEquals( prevState, reservedState.UsageState ) // Re-entrance
            )
         {
            try
            {
               return await func();
            }
            finally
            {
               Interlocked.Exchange( ref this._currentlyExecutingStatement, prevState );
            }
         }
         else
         {
            throw new InvalidOperationException( "The stream is not reserved for this statement." );
         }
      }

      /// <inheritdoc />
      public async Task DisposeStatementAsync( ReservedForStatement reservationObject )
      {
         try
         {
            await this.UseStreamWithinStatementAsync( reservationObject, () => this.PerformDisposeStatementAsync( reservationObject ) );
         }
         finally
         {
            Interlocked.Exchange( ref this._currentlyExecutingStatement, NotInUse.Instance );
         }
      }

      /// <summary>
      /// Derived classes should override this abstract method to implement custom dispose functionality when the statement represented by given <see cref="ReservedForStatement"/> is being disposed.
      /// </summary>
      /// <param name="reservationObject">The <see cref="ReservedForStatement"/> identifying the statement.</param>
      /// <returns>The task which will complete when the dispose procedure has been completed.</returns>
      protected abstract Task PerformDisposeStatementAsync( ReservedForStatement reservationObject );

      /// <summary>
      /// This property implements <see cref="PooledConnectionFunctionality.CanBeReturnedToPool"/> by checking that this connection is not reserved to any statement.
      /// </summary>
      /// <value>Will return <c>true</c> if this connection is not reserved for any statement.</value>
      public override Boolean CanBeReturnedToPool => ReferenceEquals( this._currentlyExecutingStatement, NotInUse.Instance );

   }


   /// <summary>
   /// This is common class which is used to mark <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> as either being reserved for idle statement, or statement processing being in progress.
   /// </summary>
   public abstract class ConnectionStreamUsageState
   {

   }

   /// <summary>
   /// This class is used to mark <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> as being reserved to a statement.
   /// Whenever any actual communication with remote resource is being done, the value of <see cref="UsageState"/> property is used.
   /// </summary>
   public class ReservedForStatement : ConnectionStreamUsageState
   {
      /// <summary>
      /// Creates a new instance of <see cref="ReservedForStatement"/> and also new instance of <see cref="CurrentlyInUse"/> for <see cref="UsageState"/>.
      /// </summary>
      public ReservedForStatement(
#if DEBUG
         Object statement
#endif
         )
      {
#if DEBUG
         this.Statement = statement;
#endif
         this.UsageState = new CurrentlyInUse(
#if DEBUG
            statement
#endif
            );
      }
#if DEBUG
      public Object Statement { get; }
#endif
      /// <summary>
      /// Gets the <see cref="CurrentlyInUse"/> object used to mark <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> as using underlying connection for communicating with remote resource.
      /// </summary>
      /// <value>The <see cref="CurrentlyInUse"/> object used to mark <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> as using underlying connection for communicating with remote resource.</value>
      public CurrentlyInUse UsageState { get; }
   }

   /// <summary>
   /// This class is used to mark <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> as being in use for a statement represented by <see cref="ReservedForStatement"/>, which has this object in its <see cref="ReservedForStatement.UsageState"/> property.
   /// </summary>
   /// <remarks>
   /// The instances of this class can only be created by constructor of <see cref="ReservedForStatement"/> class.
   /// </remarks>
   public sealed class CurrentlyInUse : ConnectionStreamUsageState
   {
      internal CurrentlyInUse(
#if DEBUG
         Object statement
#endif
         )
      {
#if DEBUG
         this.Statement = statement;
#endif
      }

#if DEBUG
      public Object Statement { get; }
#endif
   }
}

/// <summary>
/// Contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   private static readonly ReservedForStatement _NoStatement = new ReservedForStatement(
#if DEBUG
         null
#endif
         );

   /// <summary>
   /// This method will make sure that connection is not reserved for any statement, and if so, execute the given asynchronous callback. An anonymous <see cref="ReservedForStatement"/> reservation will be used.
   /// </summary>
   /// <param name="functionality">This <see cref="ConnectionFunctionalitySU"/>.</param>
   /// <param name="action">The asynchronous callback to execute, if connection is not reserved for any statement.</param>
   /// <param name="throwIfBusy">If set to <c>true</c>, will throw an exception if currently underlying stream is reserved for any other activity. By default, is <c>true</c>.</param>
   /// <returns>A task which will complete when <paramref name="action"/> is completed and connection reservation state has been restored to which it was at start.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionFunctionalitySU"/> is <c>null</c>.</exception>
   /// <exception cref="InvalidOperationException">If <paramref name="throwIfBusy"/> is <c>true</c>, and if this connection is reserved to any statement.</exception>
   public static Task UseStreamOutsideStatementAsync( this ConnectionFunctionalitySU functionality, Func<Task> action, Boolean throwIfBusy = true )
   {
      return functionality.UseStreamOutsideStatementAsync( _NoStatement, action, true, throwIfBusy );
   }

   /// <summary>
   /// This method will make sure that connection is not reserved for any statement, and if so, execute the given asynchronous callback. An anonymous <see cref="ReservedForStatement"/> reservation will be used.
   /// </summary>
   /// <param name="functionality">This <see cref="ConnectionFunctionalitySU"/>.</param>
   /// <param name="func">The asynchronous callback to execute, if connection is not reserved for any statement.</param>
   /// <returns>A task which will complete when <paramref name="func"/> is completed and connection reservation state has been restored to which it was at start.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="ConnectionFunctionalitySU"/> is <c>null</c>.</exception>
   /// <exception cref="InvalidOperationException">If this connection is reserved to any statement.</exception>
   public static ValueTask<T> UseStreamOutsideStatementAsync<T>( this ConnectionFunctionalitySU functionality, Func<ValueTask<T>> func )
   {
      return functionality.UseStreamOutsideStatementAsync( _NoStatement, func, true );
   }
}