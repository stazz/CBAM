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
   // SU = Stream Unseekable
   public abstract class ConnectionFunctionalitySU<TStatement, TEnumerableItem> : DefaultConnectionFunctionality<TStatement, TEnumerableItem>, ConnectionFunctionality<TStatement, TEnumerableItem>
      where TEnumerableItem : class
   {
      private sealed class NotInUse : ConnectionStreamUsageState
      {
         private NotInUse()
         {

         }

         public static readonly NotInUse Instance = new NotInUse();
      }

      private static readonly ReservedForStatement _NoStatement = new ReservedForStatement();

      private ConnectionStreamUsageState _currentlyExecutingStatement;

      public ConnectionFunctionalitySU()
      {
         this._currentlyExecutingStatement = NotInUse.Instance;
      }

      protected override AsyncEnumeratorObservable<TEnumerableItem, TStatement> PerformCreateIterationArguments(
         TStatement stmt,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionStart,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TStatement>>> getGlobalAfterStatementExecutionStart,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionEnd,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TStatement>>> getGlobalAfterStatementExecutionEnd,
         Func<GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem>>> getGlobalAfterStatementExecutionItemEncountered
         )
      {
         return new AsyncEnumeratorObservableForClasses<TEnumerableItem, TStatement>( async () =>
         {
            var reserved = this.CreateReservationObject( stmt );
            var simpleTuple = await this.UseStreamOutsideStatementAsync(
               async () => await this.ExecuteStatement( stmt, reserved ),
               reserved,
               false
               );
            return (simpleTuple.Item1 != null, simpleTuple.Item1, simpleTuple.Item2, async () => await this.DisposeStatementAsync( reserved ));
         }, stmt, getGlobalBeforeStatementExecutionStart, getGlobalAfterStatementExecutionStart, getGlobalBeforeStatementExecutionEnd, getGlobalAfterStatementExecutionEnd, getGlobalAfterStatementExecutionItemEncountered );
      }

      protected abstract Task<(TEnumerableItem, MoveNextAsyncDelegate<TEnumerableItem>)> ExecuteStatement( TStatement stmt, ReservedForStatement reservationObject );

      protected abstract ReservedForStatement CreateReservationObject( TStatement stmt );

      protected async Task UseStreamOutsideStatementAsync( Func<Task> action )
      {
         await this.UseStreamOutsideStatementAsync( action, _NoStatement, true );
      }

      protected async Task<T> UseStreamOutsideStatementAsync<T>( Func<Task<T>> action )
      {
         return await this.UseStreamOutsideStatementAsync( action, _NoStatement, true );
      }

      private async Task UseStreamOutsideStatementAsync( Func<Task> action, ReservedForStatement reservedState, Boolean oneTimeOnly )
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
         else
         {
            throw new InvalidOperationException( "The connection is currently being used by another statement." );
         }
      }

      private async Task<T> UseStreamOutsideStatementAsync<T>( Func<Task<T>> func, ReservedForStatement reservedState, Boolean oneTimeOnly )
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

      public async Task UseStreamWithinStatementAsync( ReservedForStatement reservedState, Func<Task> action ) //, Boolean throwIfNotReserved = true )
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
         else // if ( throwIfNotReserved )
         {
            throw new InvalidOperationException( "The stream is not reserved for this statement." );
         }
      }

      public async Task<T> UseStreamWithinStatementAsync<T>( ReservedForStatement reservedState, Func<Task<T>> action )
      {
         ArgumentValidator.ValidateNotNull( nameof( reservedState ), reservedState );
         ConnectionStreamUsageState prevState;
         if ( ReferenceEquals( ( prevState = Interlocked.CompareExchange( ref this._currentlyExecutingStatement, reservedState.UsageState, reservedState ) ), reservedState ) // Transition
            || ReferenceEquals( prevState, reservedState.UsageState ) // Re-entrance
            )
         {
            try
            {
               return await action();
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

      private async Task DisposeStatementAsync( ReservedForStatement reservationObject )
      {
         try
         {
            await this.UseStreamWithinStatementAsync( reservationObject, async () => await this.PerformDisposeStatementAsync( reservationObject ) );
         }
         finally
         {
            Interlocked.Exchange( ref this._currentlyExecutingStatement, NotInUse.Instance );
         }
      }

      protected abstract Task PerformDisposeStatementAsync( ReservedForStatement reservationObject );

      public override Boolean CanBeReturnedToPool => ReferenceEquals( this._currentlyExecutingStatement, NotInUse.Instance );

   }

   public abstract class ConnectionStreamUsageState
   {

   }

   public class ReservedForStatement : ConnectionStreamUsageState
   {
      public ReservedForStatement()
      {
         this.UsageState = new CurrentlyInUse();
      }

      public CurrentlyInUse UsageState { get; }
   }

   public sealed class CurrentlyInUse : ConnectionStreamUsageState
   {

   }

   public abstract class ConnectionVendorFunctionalitySU<TConnection, TStatement, TStatementCreationArgs, TEnumerableItem, TConnectionCreationParameters, TConnectionFunctionality> : DefaultConnectionVendorFunctionality<TConnection, TStatement, TStatementCreationArgs, TEnumerableItem, TConnectionCreationParameters, TConnectionFunctionality>
      where TConnection : class
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TEnumerableItem>
   {

      protected override Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error )
      {
         this.ExtractStreamOnConnectionAcquirementError( functionality, connection, token, error ).DisposeSafely();
         return TaskUtils.CompletedTask;
      }

      protected abstract IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }




}
