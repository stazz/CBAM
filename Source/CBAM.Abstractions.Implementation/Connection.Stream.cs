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
using UtilPack.AsyncEnumeration;

namespace CBAM.Abstractions.Implementation
{

   /// <summary>
   /// This interface provides a way to execute some code with checks that the connection is still useable, using <see cref="ReservedForStatement"/> as token.
   /// This is typically desireable when connection uses unseekable stream (SU) as its communication channel to remote endpoint.
   /// </summary>
   public interface ConnectionFunctionalitySU
   {
      /// <summary>
      /// This method will make sure that connection is reserved for given statement, and if so, execute the given asynchronous callback.
      /// </summary>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement the connection should be reserved to.</param>
      /// <param name="action">The asynchronous callback to execute, if the connection is reserved to statement represented by <see cref="ReservedForStatement"/>.</param>
      /// <returns>A task which will complete when <paramref name="action"/> is completed and connection reservation state has been restored to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If this connection is not reserved to statement represented by given <see cref="ReservedForStatement"/> object.</exception>
      Task UseStreamWithinStatementAsync( ReservedForStatement reservedState, Func<Task> action, Boolean throwIfBusy = true );

      /// <summary>
      /// This method will make sure that connection is reserved for given statement, and if so, execute the given asynchronous callback.
      /// </summary>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement the connection should be reserved to.</param>
      /// <param name="func">The asynchronous callback to execute, if the connection is reserved to statement represented by <see cref="ReservedForStatement"/>.</param>
      /// <returns>A task which will return result of <paramref name="func"/> and complete when <paramref name="func"/> is completed and connection reservation state has been restored to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If this connection is not reserved to statement represented by given <see cref="ReservedForStatement"/> object.</exception>
      ValueTask<T> UseStreamWithinStatementAsync<T>( ReservedForStatement reservedState, Func<ValueTask<T>> func );

      Task UseStreamOutsideStatementAsync( Func<Task> action, Boolean throwIfBusy = true );

      ValueTask<T> UseStreamOutsideStatementAsync<T>( Func<ValueTask<T>> func );


      Task UseStreamOutsideStatementAsync( Func<Task> action, ReservedForStatement reservedState, Boolean oneTimeOnly, Boolean throwIfBusy );

      ValueTask<T> UseStreamOutsideStatementAsync<T>( Func<ValueTask<T>> func, ReservedForStatement reservedState, Boolean oneTimeOnly );
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
   public abstract class ConnectionFunctionalitySU<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor> : PooledConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TVendor, IAsyncEnumerable<TEnumerableItem>>, ConnectionFunctionalitySU
      where TStatement : TStatementInformation
      where TEnumerableItem : class
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   {
      private sealed class NotInUse : ConnectionStreamUsageState
      {
         private NotInUse()
         {

         }

         public static readonly NotInUse Instance = new NotInUse();
      }

      private static readonly ReservedForStatement _NoStatement = new ReservedForStatement(
#if DEBUG
         null
#endif
         );

      private ConnectionStreamUsageState _currentlyExecutingStatement;

      /// <summary>
      /// Creates a new instance of <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
      /// </summary>
      /// <param name="vendor">The <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.</param>
      public ConnectionFunctionalitySU( TVendor vendor )
         : base( vendor )
      {
         this._currentlyExecutingStatement = NotInUse.Instance;
      }

      /// <summary>
      /// Implements <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.CreateEnumerable"/> by creating sequential exclusive enumerator (<see cref="AsyncEnumerationFactory.CreateExclusiveSequentialEnumerable{T}(SequentialEnumerationStartInfo{T})"/>).
      /// The <see cref="ExecuteStatement(TStatementInformation, ReservedForStatement)"/> method will be used to perform initial execution, after which the callback returned by the method will be used.
      /// </summary>
      /// <param name="metadata">The statement information.</param>
      /// <returns>The <see cref="IAsyncEnumerable{T}"/> that can sequentially enumerate items from underlying stream.</returns>
      /// <seealso cref="AsyncEnumerationFactory.CreateExclusiveSequentialEnumerable{T}(SequentialEnumerationStartInfo{T})"/>
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

                  if ( reservation == null && Interlocked.CompareExchange( ref reservation, this.CreateReservationObject( metadata ), null ) == null )
                  {
                     (item, moveNext) = await this.UseStreamOutsideStatementAsync(
                        async () => await this.ExecuteStatement( metadata, reservation ),
                        reservation,
                        false
                        );
                  }
                  else
                  {
                     if ( moveNext == null )
                     {
                        item = null;
                     }
                     else
                     {
                        Boolean success;
                        (success, item) = await this.UseStreamWithinStatementAsync( reservation, moveNext );
                        if ( !success )
                        {
                           item = null;
                        }
                     }
                  }

                  return (item != null, item);
               },
               () =>
               {
                  var seenReservation = reservation;

                  Interlocked.Exchange( ref reservation, null );
                  Interlocked.Exchange( ref moveNext, null );

                  return this.DisposeStatementAsync( seenReservation );
               } ) );

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
      protected abstract ValueTask<(TEnumerableItem, Func<ValueTask<(Boolean, TEnumerableItem)>>)> ExecuteStatement( TStatementInformation stmt, ReservedForStatement reservationObject );

      /// <summary>
      /// Derived classes should override this abstract method to create custom <see cref="ReservedForStatement"/> objects.
      /// </summary>
      /// <param name="stmt">The read-only information about statement being executed.</param>
      /// <returns>A new instance of <see cref="ReservedForStatement"/> which will be used to mark this connection as being in use to execute the <paramref name="stmt"/>.</returns>
      protected abstract ReservedForStatement CreateReservationObject( TStatementInformation stmt );

      /// <summary>
      /// This is helper method to mark this connection as reserved for anonymous statement, execute custom callback, and then free the connection.
      /// </summary>
      /// <param name="action">The asynchronous callback to execute after reserving and before freeing the connection.</param>
      /// <returns>A task which will complete after connection is freed up.</returns>
      /// <exception cref="InvalidOperationException">If this connection is already reserved for another statement.</exception>
      public Task UseStreamOutsideStatementAsync( Func<Task> action, Boolean throwIfBusy )
      {
         return this.UseStreamOutsideStatementAsync( action, _NoStatement, true, throwIfBusy );
      }

      /// <summary>
      /// This is helper method to mark this connection as reserved for anonymous statement, execute custom callback, and then free the connection.
      /// </summary>
      /// <param name="func">The asynchronous callback to execute after reserving and before freeing the connection.</param>
      /// <returns>A task which will complete after connection is freed up, returning result of the <paramref name="func"/>.</returns>
      /// <exception cref="InvalidOperationException">If this connection is already reserved for another statement.</exception>
      public ValueTask<T> UseStreamOutsideStatementAsync<T>( Func<ValueTask<T>> func )
      {
         return this.UseStreamOutsideStatementAsync( func, _NoStatement, true );
      }

      public async Task UseStreamOutsideStatementAsync( Func<Task> action, ReservedForStatement reservedState, Boolean oneTimeOnly, Boolean throwIfBusy )
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

      public async ValueTask<T> UseStreamOutsideStatementAsync<T>( Func<ValueTask<T>> func, ReservedForStatement reservedState, Boolean oneTimeOnly )
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

      /// <summary>
      /// This method will make sure that connection is reserved for given statement, and if so, execute the given asynchronous callback.
      /// </summary>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement the connection should be reserved to.</param>
      /// <param name="action">The asynchronous callback to execute, if the connection is reserved to statement represented by <see cref="ReservedForStatement"/>.</param>
      /// <returns>A task which will complete when <paramref name="action"/> is completed and connection reservation state has been restored to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If this connection is not reserved to statement represented by given <see cref="ReservedForStatement"/> object.</exception>
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

      /// <summary>
      /// This method will make sure that connection is reserved for given statement, and if so, execute the given asynchronous callback.
      /// </summary>
      /// <param name="reservedState">The <see cref="ReservedForStatement"/> object identifying the statement the connection should be reserved to.</param>
      /// <param name="func">The asynchronous callback to execute, if the connection is reserved to statement represented by <see cref="ReservedForStatement"/>.</param>
      /// <returns>A task which will return result of <paramref name="func"/> and complete when <paramref name="func"/> is completed and connection reservation state has been restored to which it was at start.</returns>
      /// <exception cref="InvalidOperationException">If this connection is not reserved to statement represented by given <see cref="ReservedForStatement"/> object.</exception>
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

      protected async Task DisposeStatementAsync( ReservedForStatement reservationObject )
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
