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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using UtilPack;
using CBAM.Tabular;

namespace CBAM.Tabular.Implementation
{
   public class DataRowImpl : DataRow
   {

      public DataRowImpl(
         DataRowMetaData rowMetadata,
         DataColumn[] valueStreams
         )
      {
         this.Metadata = ArgumentValidator.ValidateNotNull( nameof( rowMetadata ), rowMetadata );
         this.ValueStreams = ArgumentValidator.ValidateNotNull( nameof( valueStreams ), valueStreams );
      }

      public virtual DataColumn GetColumn( Int32 index )
      {
         return this.ValueStreams[index];
      }

      public DataRowMetaData Metadata { get; }

      protected DataColumn[] ValueStreams { get; }
   }

   public abstract class AbstractDataColumn : DataColumn
   {
      private const Int32 INITIAL = 0;
      private const Int32 COMPLETE = 1;
      private const Int32 READING_WHOLE_VALUE = 2;
      private const Int32 READING_BYTES = 3;
      private const Int32 READING_BYTES_MORE_LEFT = 4;
      private const Int32 FAULTED = 5;

      private Int32 _state;
      private Object _value;

      public AbstractDataColumn(
         DataColumnMetaData metadata,
         Int32 columnIndex
         )
      {
         this.MetaData = ArgumentValidator.ValidateNotNull( nameof( metadata ), metadata );
         this.ColumnIndex = columnIndex;

         this._state = INITIAL;
         this._value = null;
      }

      public async Task<ResultOrNone<Object>> TryGetValueAsync()
      {
         Int32 oldState;
         ResultOrNone<Object> retVal;
         if ( ( oldState = Interlocked.CompareExchange( ref this._state, READING_WHOLE_VALUE, INITIAL ) ) > COMPLETE )
         {
            // Either concurrent/re-entrant attempt, or reading by bytes has started, or faulted
            retVal = new ResultOrNone<Object>();
         }
         else
         {
            if ( oldState == INITIAL )
            {
               // First-time acquisition
               var faulted = true;
               try
               {
                  Interlocked.Exchange( ref this._value, await this.PerformReadAsValueAsync() );
                  faulted = false;
               }
               catch
               {
                  Interlocked.Exchange( ref this._state, FAULTED );
                  throw;
               }
               finally
               {
                  if ( !faulted )
                  {
                     Interlocked.Exchange( ref this._state, COMPLETE );
                  }
               }
            }
            retVal = this._state == FAULTED ? default( ResultOrNone<Object> ) : new ResultOrNone<Object>( this._value );
         }

         return retVal;
      }

      public async Task<Int32?> TryReadBytesAsync( Byte[] array, Int32 offset, Int32 count )
      {
         if ( count <= 0 )
         {
            throw new ArgumentException( nameof( count ) );
         }

         Int32? retVal;
         Int32 oldState;
         if ( ( oldState = Interlocked.CompareExchange( ref this._state, READING_BYTES, INITIAL ) ) == INITIAL
            || ( oldState = Interlocked.CompareExchange( ref this._state, READING_BYTES, READING_BYTES_MORE_LEFT ) ) == READING_BYTES_MORE_LEFT
            )
         {
            var isComplete = false;
            try
            {
               var tuple = await this.PerformReadToBytes( array, offset, count, oldState == INITIAL );
               isComplete = tuple.IsComplete;

               retVal = tuple.BytesRead;
            }
            finally
            {
               Interlocked.Exchange( ref this._state, isComplete ? COMPLETE : READING_BYTES_MORE_LEFT );
            }
         }
         else if ( oldState == COMPLETE )
         {
            retVal = 0;
         }
         else if ( oldState == READING_BYTES )
         {
            retVal = null;
         }
         else
         {
            retVal = -1;
         }

         return retVal;
      }

      public abstract Task<Object> ConvertFromBytesAsync( System.IO.Stream stream, Int32 count );

      public DataColumnMetaData MetaData { get; }

      public Int32 ColumnIndex { get; }

      protected abstract Task<Object> PerformReadAsValueAsync();

      protected abstract Task<(Int32 BytesRead, Boolean IsComplete)> PerformReadToBytes( Byte[] array, Int32 offset, Int32 count, Boolean isInitialRead );

      protected virtual void Reset()
      {
         Interlocked.Exchange( ref this._state, INITIAL );
      }
   }

   public class DataRowMetaDataImpl : DataRowMetaData
   {
      private readonly Lazy<IDictionary<String, Int32>> _labels;
      private readonly DataColumnMetaData[] _columnMetaDatas;

      public DataRowMetaDataImpl(
         DataColumnMetaData[] columnMetaDatas
         )
      {
         this._columnMetaDatas = ArgumentValidator.ValidateNotNull( nameof( columnMetaDatas ), columnMetaDatas );

         var columnCount = this._columnMetaDatas.Length;
         this.ColumnCount = columnCount;
         this._labels = new Lazy<IDictionary<String, Int32>>( () =>
         {
            var dic = new Dictionary<String, Int32>();
            for ( var i = 0; i < columnCount; ++i )
            {
               dic[this._columnMetaDatas[i].Label] = i;
            }
            return dic;
         }, LazyThreadSafetyMode.ExecutionAndPublication );
      }

      public Int32 ColumnCount { get; }

      public Int32 GetIndexFor( String columnName )
      {
         return this._labels.Value[columnName];
      }


      public DataColumnMetaData GetColumnMetaData( Int32 columnIndex )
      {
         return this._columnMetaDatas[columnIndex];
      }
   }

   public abstract class AbstractDataColumnMetaData : DataColumnMetaData
   {
      public AbstractDataColumnMetaData(
         Type type,
         String label
         )
      {
         this.ColumnCLRType = type;
         this.Label = label;
      }

      public Type ColumnCLRType { get; }

      public String Label { get; }

      public abstract Object ChangeType( Object value, Type targetType );
   }

   //public class AsyncReadOnlyLazy<T>
   //{
   //   private sealed class LazyValueHolder
   //   {
   //      public LazyValueHolder( T value )
   //         : this( value, null )
   //      {

   //      }

   //      public LazyValueHolder( Exception error )
   //         : this( default( T ), error )
   //      {

   //      }

   //      private LazyValueHolder( T value, Exception error )
   //      {
   //         this.Value = value;
   //         this.Error = error;
   //      }

   //      public T Value { get; }
   //      public Exception Error { get; }

   //   }

   //   private const Int32 NOT_CREATED = 0;
   //   private const Int32 IN_PROGRESS = 1;
   //   private const Int32 COMPLETED = 2;

   //   private readonly Func<CancellationToken, Task<T>> _factory;
   //   private LazyValueHolder _value;
   //   private Int32 _state;

   //   public AsyncReadOnlyLazy( Func<CancellationToken, Task<T>> factory )
   //   {
   //      this._factory = factory;
   //   }

   //   public async Task<T> GetValue( CancellationToken token )
   //   {
   //      T retVal;
   //      Int32 prevVal;
   //      if ( ( prevVal = Interlocked.CompareExchange( ref this._state, IN_PROGRESS, NOT_CREATED ) ) == NOT_CREATED )
   //      {
   //         // This thread captured transition -> await here
   //         try
   //         {
   //            retVal = await this._factory( token );
   //            Interlocked.Exchange( ref this._value, new LazyValueHolder( retVal ) );
   //         }
   //         catch ( Exception exc )
   //         {
   //            retVal = default( T );
   //            Interlocked.Exchange( ref this._value, new LazyValueHolder( exc ) );
   //         }
   //         finally
   //         {
   //            Interlocked.Exchange( ref this._state, COMPLETED );
   //         }
   //      }
   //      else
   //      {
   //         if ( prevVal == IN_PROGRESS )
   //         {
   //            // TODO check for re-entrancy here
   //            // Wait in this thread
   //            while ( ( prevVal = this._state ) == IN_PROGRESS )
   //            {
   //               await Task.Delay( 100 );
   //            }
   //         }
   //         var createdValue = this._value;

   //         if ( createdValue.Error != null )
   //         {
   //            throw new AggregateException( createdValue.Error );
   //         }
   //         else
   //         {
   //            retVal = createdValue.Value;
   //         }

   //      }

   //      return retVal;
   //   }

   //   public Boolean HasInitializationStarted
   //   {
   //      get
   //      {
   //         return !( this._state == NOT_CREATED );
   //      }
   //   }

   //   public Boolean IsInitializationInProgress
   //   {
   //      get
   //      {
   //         return this._state == IN_PROGRESS;
   //      }
   //   }

   //   public Boolean IsInitializationCompleted
   //   {
   //      get
   //      {
   //         return this._state == COMPLETED;
   //      }
   //   }
   //}
}
