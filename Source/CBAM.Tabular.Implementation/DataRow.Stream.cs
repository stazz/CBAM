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
using CBAM.Abstractions.Implementation;
using CBAM.Tabular.Implementation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.Tabular.Implementation
{
   // SUKS = Stream Unseekable and Known Size
   public abstract class DataRowColumnSUKS : AbstractDataColumn
   {
      private Int32 _totalBytesRead;
      private readonly ReadOnlyResettableAsyncLazy<Int32> _byteCount;
      private readonly DataRowColumnSUKS[] _allStreams;
      private readonly Func<Int32?, Task<Int32>> _transitionFunc;

      public DataRowColumnSUKS(
         DataColumnMetaData metadata,
         Int32 thisStreamIndex,
         ResizableArray<Byte> byteArray,
         DataRowColumnSUKS[] allDataRowStreams
         ) : base( metadata, thisStreamIndex )
      {
         this.ByteArray = ArgumentValidator.ValidateNotNull( nameof( byteArray ), byteArray );
         this._totalBytesRead = 0;
         this._byteCount = new ReadOnlyResettableAsyncLazy<Int32>( async () => await this.ReadByteCountAsync() );
         this._allStreams = ArgumentValidator.ValidateNotEmpty( nameof( allDataRowStreams ), allDataRowStreams );
         this._transitionFunc = async unused => await this.ReadByteCountAsync();
      }

      internal protected ResizableArray<Byte> ByteArray { get; }

      protected override async Task<Object> PerformReadAsValueAsync()
      {
         if ( this.ColumnIndex > 0 )
         {
            await this.ForceAllPreviousColumnsToBeRead( true );
         }

         var byteCount = await this._byteCount;
         Object retVal;
         if ( byteCount >= 0 )
         {
            retVal = await this.ReadValueAsync( byteCount );
         }
         else
         {
            retVal = null;
         }

         return retVal;
      }

      protected override async Task<(Int32 BytesRead, Boolean IsComplete)> PerformReadToBytes( Byte[] array, Int32 offset, Int32 count, Boolean isInitial )
      {
         var bc = this._byteCount;

         if ( isInitial )
         {
            // First read.
            if ( this.ColumnIndex > 0 )
            {
               await this.ForceAllPreviousColumnsToBeRead( true );
            }
         }
         var byteCount = await this._byteCount;
         Int32 retVal;
         if ( byteCount == this._totalBytesRead || byteCount <= 0 )
         {
            // we have encountered EOS
            retVal = 0;
         }
         else
         {
            retVal = await this.DoReadFromStreamAsync( array, offset, Math.Min( count, byteCount - this._totalBytesRead ) );
            Interlocked.Exchange( ref this._totalBytesRead, this._totalBytesRead + retVal );
         }

         return (retVal, this._totalBytesRead >= byteCount);
      }

      private async Task ForceAllPreviousColumnsToBeRead( Boolean useValue )
      {
         // Don't use Task.WhenAll - we want to read them in this specific order!
         // Since the stream is unseekable.
         // TODO wouldn't the same effect be achieved by calling SkipBytesAsync on last stream?
         for ( var i = 0; i < this.ColumnIndex; ++i )
         {
            await this._allStreams[i].SkipBytesAsync( useValue );
         }
      }

      protected abstract Task<Int32> ReadByteCountAsync();

      protected abstract Task<Object> ReadValueAsync( Int32 byteCount );

      protected abstract Task<Int32> DoReadFromStreamAsync( Byte[] array, Int32 offset, Int32 count );

      protected override void Reset()
      {
         base.Reset();
         this._byteCount.Reset();
         this._totalBytesRead = 0;
      }

   }

   public abstract class DataRowColumnSUKSWithConnectionFunctionality<TConnectionFunctionality, TStatement, TEnumerationItem> : DataRowColumnSUKS
      where TConnectionFunctionality : ConnectionFunctionalitySU<TStatement, TEnumerationItem>
      where TEnumerationItem : class
   {

      public DataRowColumnSUKSWithConnectionFunctionality(
         DataColumnMetaData metadata,
         Int32 thisStreamIndex,
         ResizableArray<Byte> byteArray,
         DataRowColumnSUKS[] allDataRowStreams,
         TConnectionFunctionality connectionFunctionality,
         ReservedForStatement reservedForStatement
         ) : base( metadata, thisStreamIndex, byteArray, allDataRowStreams )
      {
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );
         this.ReservedForStatement = ArgumentValidator.ValidateNotNull( nameof( reservedForStatement ), reservedForStatement );
      }

      protected TConnectionFunctionality ConnectionFunctionality { get; }

      protected ReservedForStatement ReservedForStatement { get; }

      protected override async Task<Int32> DoReadFromStreamAsync( Byte[] array, Int32 offset, Int32 count )
      {
         return await this.ConnectionFunctionality.UseStreamWithinStatementAsync( this.ReservedForStatement, async () => await this.PerformReadFromStreamAsync( array, offset, count ) );
      }

      protected abstract Task<Int32> PerformReadFromStreamAsync( Byte[] array, Int32 offset, Int32 count );

   }
}

public static partial class E_CBAM
{
   public static async Task SkipBytesAsync( this DataRowColumnSUKS stream, Boolean useValue )
   {
      if ( useValue )
      {
         await stream.TryGetValueAsync();
      }
      else
      {
         var array = stream.ByteArray.Array;
         while ( ( ( await stream.TryReadBytesAsync( array, 0, array.Length ) ) ?? 0 ) != 0 ) ;
      }
   }
}