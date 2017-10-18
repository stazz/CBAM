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
using System.Threading.Tasks;
using UtilPack;
using UtilPack.TabularData;

namespace CBAM.Abstractions.Implementation.Tabular
{
   /// <summary>
   /// This class extends <see cref="DataColumnSUKS"/> to use <see cref="ConnectionFunctionalitySU"/> to reserve it when reading values.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The real type of <see cref="ConnectionFunctionalitySU"/>.</typeparam>
   /// <seealso cref="ConnectionFunctionalitySU.UseStreamWithinStatementAsync(ReservedForStatement, Func{Task})"/>
   /// <seealso cref="ConnectionFunctionalitySU.UseStreamWithinStatementAsync{T}(ReservedForStatement, Func{ValueTask{T}})"/>
   public abstract class DataColumnSUKSWithConnectionFunctionality<TConnectionFunctionality> : DataColumnSUKS
      where TConnectionFunctionality : class, ConnectionFunctionalitySU
   {

      /// <summary>
      /// Creates new instance of <see cref="DataColumnSUKSWithConnectionFunctionality{TConnectionFunctionality}"/> with given parameters.
      /// </summary>
      /// <param name="metadata">The <see cref="DataColumnMetaData"/> of this data column.</param>
      /// <param name="columnIndex">The index of this column within the <see cref="AsyncDataRow"/>.</param>
      /// <param name="previousColumn">The column at previous index within the <see cref="AsyncDataRow"/>.</param>
      /// <param name="connectionFunctionality">The <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> to use.</param>
      /// <param name="reservedForStatement">The <see cref="Implementation.ReservedForStatement"/> object that is used identify current reservation state of <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</param>
      /// <exception cref="ArgumentNullException">If any of <paramref name="metadata"/>, <paramref name="connectionFunctionality"/>, or <paramref name="reservedForStatement"/> is <c>null</c>, or if <paramref name="columnIndex"/> is greater than <c>0</c> but <paramref name="previousColumn"/> is <c>null</c>.</exception>
      public DataColumnSUKSWithConnectionFunctionality(
         DataColumnMetaData metadata,
         Int32 columnIndex,
         AsyncDataColumn previousColumn,
         TConnectionFunctionality connectionFunctionality,
         ReservedForStatement reservedForStatement
         ) : base( metadata, columnIndex, previousColumn )
      {
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );
         this.ReservedForStatement = ArgumentValidator.ValidateNotNull( nameof( reservedForStatement ), reservedForStatement );
      }

      /// <summary>
      /// Gets the <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> of this data column.
      /// </summary>
      /// <value>The <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> of this data column.</value>
      protected TConnectionFunctionality ConnectionFunctionality { get; }

      /// <summary>
      /// Gets the <see cref="Implementation.ReservedForStatement"/> object of this data column.
      /// </summary>
      /// <value>The <see cref="Implementation.ReservedForStatement"/> object of this data column.</value>
      protected ReservedForStatement ReservedForStatement { get; }

      /// <summary>
      /// Implements <see cref="DataColumnSUKS.ReadValueAsync(int)"/> and will call <see cref="ReadValueWhileReservedAsync(int)"/> within reservation usage scope.
      /// </summary>
      /// <param name="byteCount">The size of data, in bytes.</param>
      /// <returns>Asynchronously returns deserialized value.</returns>
      /// <seealso cref="DataColumnSUKS.ReadValueAsync(int)"/>
      /// <seealso cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.UseStreamWithinStatementAsync{T}(ReservedForStatement, Func{ValueTask{T}})"/>
      protected override ValueTask<Object> ReadValueAsync( Int32 byteCount )
      {
         return this.ConnectionFunctionality.UseStreamWithinStatementAsync( this.ReservedForStatement, () => this.ReadValueWhileReservedAsync( byteCount ) );
      }

      /// <summary>
      /// Implements <see cref="DataColumnSUKS.DoReadFromStreamAsync(byte[], int, int)"/> and will call <see cref="ReadFromStreamWhileReservedAsync(byte[], int, int)"/> within reservation usage scope.
      /// </summary>
      /// <param name="array">The byte array where to read the data to.</param>
      /// <param name="offset">The offset in <paramref name="array"/> where to start writing bytes.</param>
      /// <param name="count">The maximum amount of bytes to write.</param>
      /// <returns>Asynchronously returns the amount of bytes read.</returns>
      /// <seealso cref="DataColumnSUKS.DoReadFromStreamAsync(byte[], int, int)"/>
      /// <seealso cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.UseStreamWithinStatementAsync{T}(ReservedForStatement, Func{ValueTask{T}})"/>
      protected override ValueTask<Int32> DoReadFromStreamAsync( Byte[] array, Int32 offset, Int32 count )
      {
         return this.ConnectionFunctionality.UseStreamWithinStatementAsync( this.ReservedForStatement, () => this.ReadFromStreamWhileReservedAsync( array, offset, count ) );
      }

      /// <summary>
      /// Derived classes should implement this method to read binary data from underlying stream.
      /// </summary>
      /// <param name="array">The byte array where to read the data to.</param>
      /// <param name="offset">The offset in <paramref name="array"/> where to start writing bytes.</param>
      /// <param name="count">The maximum amount of bytes to write.</param>
      /// <returns>Asynchronously returns the amount of bytes read.</returns>
      protected abstract ValueTask<Int32> ReadFromStreamWhileReservedAsync( Byte[] array, Int32 offset, Int32 count );

      /// <summary>
      /// Derived classes should implement this method to deserialize binary data into CLR object.
      /// </summary>
      /// <param name="byteCount">The size of the data, in bytes.</param>
      /// <returns>Asynchronously returns deserialized value.</returns>
      protected abstract ValueTask<Object> ReadValueWhileReservedAsync( Int32 byteCount );

   }
}
