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
   using TStatementExecutionSimpleTaskParameter = System.ValueTuple<SQLStatementExecutionResult, CBAM.Abstractions.Implementation.MoveNextAsyncDelegate<SQLStatementExecutionResult>>;

   public abstract class SQLConnectionFunctionalitySU : ConnectionFunctionalitySU<StatementBuilder, SQLStatementExecutionResult>, SQLConnectionFunctionality
   {
      public SQLConnectionFunctionalitySU()
      {
      }

      protected override async Task<TStatementExecutionSimpleTaskParameter> ExecuteStatement( StatementBuilder stmt, ReservedForStatement reservationObject )
      {
         Task<TStatementExecutionSimpleTaskParameter> retValTask;
         if ( stmt.HasBatchParameters() )
         {
            retValTask = this.ExecuteStatementAsBatch( stmt, reservationObject );
         }
         else if ( stmt.SQLParameterCount > 0 )
         {
            retValTask = this.ExecuteStatementAsPrepared( stmt, reservationObject );
         }
         else
         {
            retValTask = this.ExecuteStatementAsSimple( stmt, reservationObject );
         }
         return await retValTask;
      }

      protected abstract Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsSimple( StatementBuilder stmt, ReservedForStatement reservedState );

      protected abstract Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsPrepared( StatementBuilder stmt, ReservedForStatement reservedState );

      protected abstract Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsBatch( StatementBuilder stmt, ReservedForStatement reservedState );

   }

   public abstract class SQLConnectionVendorFunctionalitySU<TConnection, TConnectionCreationParameters, TConnectionFunctionality> : DefaultConnectionVendorFunctionality<TConnection, TConnectionCreationParameters, TConnectionFunctionality>
      where TConnection : class, SQLConnection
      where TConnectionFunctionality : SQLConnectionFunctionalitySU
   {

      protected override Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error )
      {
         this.ExtractStreamOnConnectionAcquirementError( functionality, connection, token, error ).DisposeSafely();
         return TaskUtils.CompletedTask;
      }

      protected abstract IDisposable ExtractStreamOnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }




}
