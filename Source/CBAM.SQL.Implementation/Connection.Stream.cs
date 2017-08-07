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

namespace CBAM.SQL.Implementation
{
   using UtilPack.AsyncEnumeration;
   using TStatementExecutionSimpleTaskParameter = System.ValueTuple<SQLStatementExecutionResult, UtilPack.AsyncEnumeration.MoveNextAsyncDelegate<SQLStatementExecutionResult>>;

   public abstract class SQLConnectionFunctionalitySU<TVendor> : ConnectionFunctionalitySU<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor>, SQLConnectionFunctionality
      where TVendor : SQLConnectionVendorFunctionality
   {

      public SQLConnectionFunctionalitySU( TVendor vendor )
         : base( vendor )
      {
      }

      protected override async Task<TStatementExecutionSimpleTaskParameter> ExecuteStatement( CancellationToken token, StatementBuilderInformation stmt, ReservedForStatement reservationObject )
      {
         Task<TStatementExecutionSimpleTaskParameter> retValTask;
         if ( stmt.HasBatchParameters() )
         {
            retValTask = this.ExecuteStatementAsBatch( token, stmt, reservationObject );
         }
         else if ( stmt.SQLParameterCount > 0 )
         {
            retValTask = this.ExecuteStatementAsPrepared( token, stmt, reservationObject );
         }
         else
         {
            retValTask = this.ExecuteStatementAsSimple( token, stmt, reservationObject );
         }
         return await retValTask;
      }

      protected override StatementBuilderInformation GetInformationFromStatement( StatementBuilder statement )
      {
         return statement?.StatementBuilderInformation;
      }

      protected abstract Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsSimple( CancellationToken token, StatementBuilderInformation stmt, ReservedForStatement reservedState );

      protected abstract Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsPrepared( CancellationToken token, StatementBuilderInformation stmt, ReservedForStatement reservedState );

      protected abstract Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsBatch( CancellationToken token, StatementBuilderInformation stmt, ReservedForStatement reservedState );

      SQLConnectionVendorFunctionality Connection<StatementBuilder, StatementBuilderInformation, string, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>.VendorFunctionality => this.VendorFunctionality;

   }

   public abstract class SQLConnectionFactorySU<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality> : ConnectionFactorySU<TConnection, TVendor, TConnectionCreationParameters, TConnectionFunctionality, String, StatementBuilder, StatementBuilderInformation, SQLStatementExecutionResult>
      where TConnection : ConnectionImpl<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor, TConnectionFunctionality>, SQLConnection
      where TVendor : SQLConnectionVendorFunctionality
      where TConnectionFunctionality : DefaultConnectionFunctionality<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor>
   {

   }

}
