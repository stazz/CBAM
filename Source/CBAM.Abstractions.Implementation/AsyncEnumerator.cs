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

   }

   public class AsyncEnumeratorObservableForClasses<T, TMetadata> : AsyncEnumeratorForClasses<T>, AsyncEnumeratorObservable<T, TMetadata>
      where T : class
   {
      private readonly TMetadata _metadata;
      private readonly Func<GenericEventHandler<EnumerationStartedEventArgs<TMetadata>>> _getGlobalBeforeEnumerationExecutionStart;
      private readonly Func<GenericEventHandler<EnumerationStartedEventArgs<TMetadata>>> _getGlobalAfterEnumerationExecutionStart;
      private readonly Func<GenericEventHandler<EnumerationEndedEventArgs<TMetadata>>> _getGlobalBeforeEnumerationExecutionEnd;
      private readonly Func<GenericEventHandler<EnumerationEndedEventArgs<TMetadata>>> _getGlobalAfterEnumerationExecutionEnd;
      private readonly Func<GenericEventHandler<EnumerationItemEventArgs<T, TMetadata>>> _getGlobalAfterEnumerationExecutionItemEncountered;

      public AsyncEnumeratorObservableForClasses(
         InitialMoveNextAsyncDelegate<T> initialMoveNext,
         TMetadata metadata,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TMetadata>>> getGlobalBeforeEnumerationExecutionStart,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TMetadata>>> getGlobalAfterEnumerationExecutionStart,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TMetadata>>> getGlobalBeforeEnumerationExecutionEnd,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TMetadata>>> getGlobalAfterEnumerationExecutionEnd,
         Func<GenericEventHandler<EnumerationItemEventArgs<T, TMetadata>>> getGlobalAfterEnumerationExecutionItemEncountered
         ) : base( initialMoveNext )
      {
         this._metadata = metadata;
         this._getGlobalBeforeEnumerationExecutionStart = getGlobalBeforeEnumerationExecutionStart;
         this._getGlobalAfterEnumerationExecutionStart = getGlobalAfterEnumerationExecutionStart;
         this._getGlobalBeforeEnumerationExecutionEnd = getGlobalBeforeEnumerationExecutionEnd;
         this._getGlobalAfterEnumerationExecutionEnd = getGlobalAfterEnumerationExecutionEnd;
         this._getGlobalAfterEnumerationExecutionItemEncountered = getGlobalAfterEnumerationExecutionItemEncountered;
      }



      event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<T>.BeforeEnumerationStart
      {
         add
         {
            this.BeforeEnumerationStart += value;
         }

         remove
         {
            this.BeforeEnumerationStart -= value;
         }
      }

      event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<T>.AfterEnumerationStart
      {
         add
         {
            this.AfterEnumerationStart += value;
         }

         remove
         {
            this.AfterEnumerationStart -= value;
         }
      }

      event GenericEventHandler<EnumerationItemEventArgs<T>> AsyncEnumerationObservation<T>.AfterEnumerationItemEncountered
      {
         add
         {
            this.AfterEnumerationItemEncountered += value;
         }
         remove
         {
            this.AfterEnumerationItemEncountered -= value;
         }
      }

      event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<T>.BeforeEnumerationEnd
      {
         add
         {
            this.BeforeEnumerationEnd += value;
         }

         remove
         {
            this.BeforeEnumerationEnd -= value;
         }
      }

      event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<T>.AfterEnumerationEnd
      {
         add
         {
            this.AfterEnumerationEnd += value;
         }

         remove
         {
            this.AfterEnumerationEnd -= value;
         }
      }

      protected override async Task<(bool, T, MoveNextAsyncDelegate<T>, DisposeAsyncDelegate)> CallInitialMoveNext( InitialMoveNextAsyncDelegate<T> initialMoveNext )
      {
         EnumerationStartedEventArgsImpl<TMetadata> args = null;
         this.BeforeEnumerationStart?.InvokeAllEventHandlers( evt => evt( ( args = new EnumerationStartedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
         this._getGlobalBeforeEnumerationExecutionStart?.Invoke()?.InvokeAllEventHandlers( evt => evt( args ?? ( args = new EnumerationStartedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
         try
         {
            return await base.CallInitialMoveNext( initialMoveNext );
         }
         finally
         {
            this.AfterEnumerationStart?.InvokeAllEventHandlers( evt => evt( args ?? ( args = new EnumerationStartedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
            this._getGlobalAfterEnumerationExecutionStart?.Invoke()?.InvokeAllEventHandlers( evt => evt( args ?? ( args = new EnumerationStartedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
         }
      }

      protected override Task AfterMoveNextSucessful()
      {
         EnumerationItemResultEventArgsImpl<T, TMetadata> args = null;
         this.AfterEnumerationItemEncountered?.InvokeAllEventHandlers( evt => evt( ( args = new EnumerationItemResultEventArgsImpl<T, TMetadata>( this.Current, this._metadata ) ) ), throwExceptions: false );
         this._getGlobalAfterEnumerationExecutionItemEncountered?.Invoke()?.InvokeAllEventHandlers( evt => evt( args ?? new EnumerationItemResultEventArgsImpl<T, TMetadata>( this.Current, this._metadata ) ), throwExceptions: false );
         return base.AfterMoveNextSucessful();
      }

      protected override async Task PerformDispose( DisposeAsyncDelegate disposeDelegate = null )
      {
         EnumerationEndedEventArgsImpl<TMetadata> args = null;
         this.BeforeEnumerationEnd?.InvokeAllEventHandlers( evt => evt( ( args = new EnumerationEndedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
         this._getGlobalBeforeEnumerationExecutionEnd?.Invoke()?.InvokeAllEventHandlers( evt => evt( args ?? ( args = new EnumerationEndedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
         try
         {
            await base.PerformDispose( disposeDelegate );
         }
         finally
         {
            this.AfterEnumerationEnd?.InvokeAllEventHandlers( evt => evt( args ?? ( args = new EnumerationEndedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
            this._getGlobalAfterEnumerationExecutionEnd?.Invoke()?.InvokeAllEventHandlers( evt => evt( args ?? ( args = new EnumerationEndedEventArgsImpl<TMetadata>( this._metadata ) ) ), throwExceptions: false );
         }

      }

      public event GenericEventHandler<EnumerationStartedEventArgs<TMetadata>> BeforeEnumerationStart;
      public event GenericEventHandler<EnumerationStartedEventArgs<TMetadata>> AfterEnumerationStart;

      public event GenericEventHandler<EnumerationEndedEventArgs<TMetadata>> BeforeEnumerationEnd;
      public event GenericEventHandler<EnumerationEndedEventArgs<TMetadata>> AfterEnumerationEnd;

      public event GenericEventHandler<EnumerationItemEventArgs<T, TMetadata>> AfterEnumerationItemEncountered;
   }

   public class EnumerationStartedEventArgsImpl<TStatement> : EnumerationStartedEventArgs<TStatement>
   {
      public EnumerationStartedEventArgsImpl( TStatement statement )
      {
         this.Metadata = statement;
      }

      public TStatement Metadata { get; }
   }

   public class EnumerationItemResultEventArgsImpl<TEnumerableItem> : EnumerationItemEventArgs<TEnumerableItem>
   {
      public EnumerationItemResultEventArgsImpl( TEnumerableItem item )
      {
         this.Item = item;
      }

      public TEnumerableItem Item { get; }
   }

   public class EnumerationEndedEventArgsImpl<TStatement> : EnumerationStartedEventArgsImpl<TStatement>, EnumerationEndedEventArgs<TStatement>
   {
      public EnumerationEndedEventArgsImpl(
         TStatement statement
         ) : base( statement )
      {
      }
   }

   public class EnumerationItemResultEventArgsImpl<TEnumerableItem, TMetadata> : EnumerationItemResultEventArgsImpl<TEnumerableItem>, EnumerationItemEventArgs<TEnumerableItem, TMetadata>
   {
      public EnumerationItemResultEventArgsImpl(
         TEnumerableItem item,
         TMetadata metadata
         )
         : base( item )
      {
         this.Metadata = metadata;
      }

      public TMetadata Metadata { get; }
   }
}
