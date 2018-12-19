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
using CBAM.Abstractions.Implementation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL.Implementation
{
   using TStatementExecutionTaskParameter = System.ValueTuple<SQLStatementExecutionResult, Func<ValueTask<(Boolean, SQLStatementExecutionResult)>>>;

   /// <summary>
   /// This class extends <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> to provide SQL-related connection functionality which operates on non-seekable streams.
   /// </summary>
   /// <typeparam name="TVendor">The actual type of <see cref="SQLConnectionVendorFunctionality"/>.</typeparam>
   public abstract class SQLConnectionFunctionalitySU<TVendor> : ConnectionFunctionalitySU<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, TVendor>
      where TVendor : SQLConnectionVendorFunctionality
   {

      /// <summary>
      /// Initializes new instance of <see cref="SQLConnectionFunctionalitySU{TVendor}"/> with given vendor.
      /// </summary>
      /// <param name="vendor">The vendor functionality.</param>
      /// <param name="asyncProvider">The <see cref="IAsyncProvider"/> to use.</param>
      public SQLConnectionFunctionalitySU( TVendor vendor, IAsyncProvider asyncProvider )
         : base( vendor, asyncProvider )
      {
      }

      /// <summary>
      /// This method implements <see cref="ConnectionFunctionalitySU{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.ExecuteStatement(TStatementInformation, ReservedForStatement)"/> to further delegate execution to <see cref="ExecuteStatementAsBatch(SQLStatementBuilderInformation, ReservedForStatement)"/>, <see cref="ExecuteStatementAsPrepared(SQLStatementBuilderInformation, ReservedForStatement)"/>, or <see cref="ExecuteStatementAsSimple(SQLStatementBuilderInformation, ReservedForStatement)"/> methods.
      /// </summary>
      /// <param name="stmt">The <see cref="SQLStatementBuilderInformation"/> to use.</param>
      /// <param name="reservationObject">The <see cref="ReservedForStatement"/> object of this execution.</param>
      /// <returns>The result of <see cref="ExecuteStatementAsBatch(SQLStatementBuilderInformation, ReservedForStatement)"/>, if <paramref name="stmt"/> is a batched statement. Otherwise, if <paramref name="stmt"/> is prepared statement, returns result of <see cref="ExecuteStatementAsPrepared(SQLStatementBuilderInformation, ReservedForStatement)"/>. Otherwise, returns result of <see cref="ExecuteStatementAsSimple(SQLStatementBuilderInformation, ReservedForStatement)"/>.</returns>
      protected override async ValueTask<(SQLStatementExecutionResult, Boolean, Func<ValueTask<(Boolean, SQLStatementExecutionResult)>>)> ExecuteStatement( SQLStatementBuilderInformation stmt, ReservedForStatement reservationObject )
      {
         var retVal = await ( stmt.HasBatchParameters() ?
            this.ExecuteStatementAsBatch( stmt, reservationObject ) : (
               stmt.SQLParameterCount > 0 ?
                  this.ExecuteStatementAsPrepared( stmt, reservationObject ) :
                  this.ExecuteStatementAsSimple( stmt, reservationObject )
            ) );

         return (retVal.Item1, retVal.Item1 != null, retVal.Item2);
      }

      /// <summary>
      /// Implements <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}.GetInformationFromStatement(TStatement)"/> by returns value of <see cref="SQLStatementBuilder.StatementBuilderInformation"/> of given <see cref="SQLStatementBuilder"/>.
      /// </summary>
      /// <param name="statement">The <see cref="SQLStatementBuilder"/>.</param>
      /// <returns>The result of <see cref="SQLStatementBuilder.StatementBuilderInformation"/>.</returns>
      protected override SQLStatementBuilderInformation GetInformationFromStatement( SQLStatementBuilder statement )
      {
         return statement?.StatementBuilderInformation;
      }

      /// <summary>
      /// Derived classes should implement this method in order to execute <see cref="SQLStatementBuilderInformation"/> as simple, non-prepared and non-batched statement.
      /// </summary>
      /// <param name="stmt">The <see cref="SQLStatementBuilderInformation"/> to use.</param>
      /// <param name="reservationObject">The <see cref="ReservedForStatement"/> object of this execution.</param>
      /// <returns>Asynchronously returns the tuple of <see cref="SQLStatementExecutionResult"/> resulted from executing statement, and optional <see cref="MoveNextAsyncDelegate{T}"/> to enumerate more results.</returns>
      protected abstract ValueTask<TStatementExecutionTaskParameter> ExecuteStatementAsSimple( SQLStatementBuilderInformation stmt, ReservedForStatement reservationObject );

      /// <summary>
      /// Derived classes should implement this method in order to execute <see cref="SQLStatementBuilderInformation"/> as prepared and non-batched statement.
      /// </summary>
      /// <param name="stmt">The <see cref="SQLStatementBuilderInformation"/> to use.</param>
      /// <param name="reservationObject">The <see cref="ReservedForStatement"/> object of this execution.</param>
      /// <returns>Asynchronously returns the tuple of <see cref="SQLStatementExecutionResult"/> resulted from executing statement, and optional <see cref="MoveNextAsyncDelegate{T}"/> to enumerate more results.</returns>
      protected abstract ValueTask<TStatementExecutionTaskParameter> ExecuteStatementAsPrepared( SQLStatementBuilderInformation stmt, ReservedForStatement reservationObject );

      /// <summary>
      /// Derived classes should implement this method in order to execute <see cref="SQLStatementBuilderInformation"/> as batched statement, which is either simple or prepared.
      /// </summary>
      /// <param name="stmt">The <see cref="SQLStatementBuilderInformation"/> to use.</param>
      /// <param name="reservationObject">The <see cref="ReservedForStatement"/> object of this execution.</param>
      /// <returns>Asynchronously returns the tuple of <see cref="SQLStatementExecutionResult"/> resulted from executing statement, and optional <see cref="MoveNextAsyncDelegate{T}"/> to enumerate more results.</returns>
      protected abstract ValueTask<TStatementExecutionTaskParameter> ExecuteStatementAsBatch( SQLStatementBuilderInformation stmt, ReservedForStatement reservationObject );

   }

}
