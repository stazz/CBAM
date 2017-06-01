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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.Abstractions.Implementation
{
   public delegate Task<(Boolean, T, MoveNextAsyncDelegate<T>, DisposeAsyncDelegate)> InitialMoveNextAsyncDelegate<T>();
   public delegate Task<(Boolean, T)> MoveNextAsyncDelegate<T>();
   public delegate Task DisposeAsyncDelegate();

   public class AsyncEnumeratorForClasses<T> : AsyncEnumerator<T>
      where T : class
   {
      private sealed class CurrentInfo
      {
         private T _current;

         public CurrentInfo(
            T current,
            MoveNextAsyncDelegate<T> moveNext,
            DisposeAsyncDelegate disposeDelegate
            )
         {
            this.MoveNext = moveNext;
            this.Current = ArgumentValidator.ValidateNotNull( nameof( current ), current );
            this.Dispose = disposeDelegate;
         }

         public MoveNextAsyncDelegate<T> MoveNext { get; }
         public DisposeAsyncDelegate Dispose { get; }
         public T Current
         {
            get
            {
               return this._current;
            }
            set
            {
               Interlocked.Exchange( ref this._current, value );
            }
         }
      }

      private const Int32 STATE_INITIAL = 0;
      private const Int32 MOVE_NEXT_STARTED = 1;
      private const Int32 MOVE_NEXT_ENDED = 2;
      private const Int32 STATE_ENDED = 3;
      private const Int32 RESETTING = 4;

      private Int32 _state;
      private CurrentInfo _current;
      private readonly InitialMoveNextAsyncDelegate<T> _initialMoveNext;


      public AsyncEnumeratorForClasses(
         InitialMoveNextAsyncDelegate<T> initialMoveNext
         )
      {
         this._state = STATE_INITIAL;
         this._current = null;
         this._initialMoveNext = ArgumentValidator.ValidateNotNull( nameof( initialMoveNext ), initialMoveNext );
      }

      public async Task<Boolean> MoveNextAsync()
      {
         // We can call move next only in initial state, or after we have called it once
         Boolean retVal = false;
         var wasNotInitial = Interlocked.CompareExchange( ref this._state, MOVE_NEXT_STARTED, MOVE_NEXT_ENDED ) == MOVE_NEXT_ENDED;
         if ( wasNotInitial || Interlocked.CompareExchange( ref this._state, MOVE_NEXT_STARTED, STATE_INITIAL ) == STATE_INITIAL )
         {
            DisposeAsyncDelegate disposeDelegate = null;

            try
            {

               if ( wasNotInitial )
               {
                  var moveNext = this._current.MoveNext;
                  if ( moveNext == null )
                  {
                     retVal = false;
                  }
                  else
                  {
                     T current;
                     (retVal, current) = await moveNext();
                     if ( retVal )
                     {
                        this._current.Current = current;
                     }
                  }
               }
               else
               {
                  // First time calling move next
                  var result = await this.CallInitialMoveNext( this._initialMoveNext );
                  retVal = result.Item1;
                  if ( retVal )
                  {
                     Interlocked.Exchange( ref this._current, new CurrentInfo( result.Item2, result.Item3, result.Item4 ) );
                  }
                  else
                  {
                     disposeDelegate = result.Item4;
                  }
               }
            }
            finally
            {
               try
               {
                  if ( retVal )
                  {
                     var t = this.AfterMoveNextSucessful();
                     if ( t != null )
                     {
                        await t;
                     }
                  }
                  else
                  {
                     await this.PerformDispose( disposeDelegate );
                  }
               }
               catch
               {
                  // Ignore.
               }

               if ( !retVal )
               {
                  Interlocked.Exchange( ref this._current, null );
               }
               Interlocked.Exchange( ref this._state, retVal ? MOVE_NEXT_ENDED : STATE_ENDED );
            }
         }
         else if ( this._state != STATE_ENDED )
         {
            // Re-entrancy or concurrent with Reset -> exception
            throw new InvalidOperationException( "Tried to concurrently move to next or reset." );
         }
         return retVal;
      }

      public T Current
      {
         get
         {
            return this._current?.Current;
         }
      }

      public async Task ResetAsync()
      {
         // We can reset from MOVE_NEXT_STARTED and STATE_ENDED states
         if (
            Interlocked.CompareExchange( ref this._state, RESETTING, MOVE_NEXT_STARTED ) == MOVE_NEXT_STARTED
            || Interlocked.CompareExchange( ref this._state, RESETTING, STATE_ENDED ) == STATE_ENDED
            )
         {
            try
            {
               var moveNext = this._current?.MoveNext;
               if ( moveNext != null )
               {
                  while ( ( await moveNext() ).Item1 ) ;
               }
            }
            finally
            {
               try
               {
                  await this.PerformDispose();
               }
               catch
               {
                  // Ignore
               }

               Interlocked.Exchange( ref this._state, STATE_INITIAL );
            }
         }
         else if ( this._state != STATE_INITIAL )
         {
            // Re-entrancy or concurrent with move next -> exception
            throw new InvalidOperationException( "Tried to concurrently reset or move to next." );
         }
      }

      protected virtual async Task PerformDispose( DisposeAsyncDelegate disposeDelegate = null )
      {
         var prev = Interlocked.Exchange( ref this._current, null );
         if ( prev != null || disposeDelegate != null )
         {
            this.IterationEndedEvent.InvokeAllEventHandlers( ( evt ) => evt( null, new IterationEndedEventArgs() ), throwExceptions: false );
            if ( disposeDelegate == null )
            {
               disposeDelegate = prev.Dispose;
            }

            if ( disposeDelegate != null )
            {
               await prev.Dispose();
            }
         }
      }

      protected virtual async Task<(Boolean, T, MoveNextAsyncDelegate<T>, DisposeAsyncDelegate)> CallInitialMoveNext( InitialMoveNextAsyncDelegate<T> initialMoveNext )
      {
         return await initialMoveNext();
      }

      protected virtual Task AfterMoveNextSucessful()
      {
         return null;
      }

      public event EventHandler<IterationEndedEventArgs> IterationEndedEvent;

   }

   public class AsyncEnumeratorObservableForClasses<T, TStatement> : AsyncEnumeratorForClasses<T>, AsyncEnumeratorObservable<T, TStatement>
      where T : class
   {
      private readonly TStatement _statement;
      private readonly Func<GenericEventHandler<StatementExecutionStartedEventArgs<TStatement>>> _getGlobalBeforeStatementExecutionStart;
      private readonly Func<GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>>> _getGlobalBeforeStatementExecutionEnd;
      private readonly Func<GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>>> _getGlobalAfterStatementExecutionEnd;
      private readonly Func<GenericEventHandler<StatementExecutionResultEventArgs<T>>> _getGlobalAfterStatementExecutionItemEncountered;

      public AsyncEnumeratorObservableForClasses(
         InitialMoveNextAsyncDelegate<T> initialMoveNext,
         TStatement statement,
         Func<GenericEventHandler<StatementExecutionStartedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionStart,
         Func<GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionEnd,
         Func<GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>>> getGlobalAfterStatementExecutionEnd,
         Func<GenericEventHandler<StatementExecutionResultEventArgs<T>>> getGlobalAfterStatementExecutionItemEncountered
         ) : base( initialMoveNext )
      {
         this._statement = statement;
         this._getGlobalBeforeStatementExecutionStart = getGlobalBeforeStatementExecutionStart;
         this._getGlobalBeforeStatementExecutionEnd = getGlobalBeforeStatementExecutionEnd;
         this._getGlobalAfterStatementExecutionEnd = getGlobalAfterStatementExecutionEnd;
         this._getGlobalAfterStatementExecutionItemEncountered = getGlobalAfterStatementExecutionItemEncountered;
      }

      protected override async Task<(bool, T, MoveNextAsyncDelegate<T>, DisposeAsyncDelegate)> CallInitialMoveNext( InitialMoveNextAsyncDelegate<T> initialMoveNext )
      {
         this.BeforeStatementExecutionStart?.InvokeAllEventHandlers( evt => evt( new StatementExecutionStartedEventArgsImpl<TStatement>( this._statement ) ), throwExceptions: false );
         this._getGlobalBeforeStatementExecutionStart?.Invoke()?.InvokeAllEventHandlers( evt => evt( new StatementExecutionStartedEventArgsImpl<TStatement>( this._statement ) ), throwExceptions: false );
         return await base.CallInitialMoveNext( initialMoveNext );
      }

      protected override Task AfterMoveNextSucessful()
      {
         this.AfterStatementExecutionItemEncountered?.InvokeAllEventHandlers( evt => evt( new StatementExecutionResultEventArgsImpl<T>( this.Current ) ), throwExceptions: false );
         this._getGlobalAfterStatementExecutionItemEncountered?.Invoke()?.InvokeAllEventHandlers( evt => evt( new StatementExecutionResultEventArgsImpl<T>( this.Current ) ), throwExceptions: false );
         return base.AfterMoveNextSucessful();
      }

      protected override async Task PerformDispose( DisposeAsyncDelegate disposeDelegate = null )
      {
         this.BeforeStatementExecutionEnd?.InvokeAllEventHandlers( evt => evt( new StatementExecutionEndedEventArgsImpl<TStatement>( this._statement ) ), throwExceptions: false );
         this._getGlobalBeforeStatementExecutionEnd?.Invoke()?.InvokeAllEventHandlers( evt => evt( new StatementExecutionEndedEventArgsImpl<TStatement>( this._statement ) ), throwExceptions: false );
         try
         {
            await base.PerformDispose( disposeDelegate );
         }
         finally
         {
            this.AfterStatementExecutionEnd?.InvokeAllEventHandlers( evt => evt( new StatementExecutionEndedEventArgsImpl<TStatement>( this._statement ) ), throwExceptions: false );
            this._getGlobalAfterStatementExecutionEnd?.Invoke()?.InvokeAllEventHandlers( evt => evt( new StatementExecutionEndedEventArgsImpl<TStatement>( this._statement ) ), throwExceptions: false );
         }

      }

      public event GenericEventHandler<StatementExecutionStartedEventArgs<TStatement>> BeforeStatementExecutionStart;
      public event GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>> BeforeStatementExecutionEnd;
      public event GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>> AfterStatementExecutionEnd;
      public event GenericEventHandler<StatementExecutionResultEventArgs<T>> AfterStatementExecutionItemEncountered;
   }
}
