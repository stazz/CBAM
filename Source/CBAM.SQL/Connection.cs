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
using System.Linq;
using System.Text;
using CBAM.SQL;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using CBAM.Abstractions;
using CBAM.Tabular;

namespace CBAM.SQL
{
   public interface SQLConnection : Connection<StatementBuilder, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>
   {

      DatabaseMetadata DatabaseMetadata { get; }

      Task<Boolean> GetReadOnlyAsync();

      Task SetReadOnlyAsync( Boolean isReadOnly );

      Task<TransactionIsolationLevel> GetDefaultTransactionIsolationLevelAsync();

      Task SetDefaultTransactionIsolationLevelAsync( TransactionIsolationLevel level );
   }

   public interface SQLConnectionVendorFunctionality : ConnectionVendorFunctionality<StatementBuilder, String>
   {
      void AppendEscapedLiteral( StringBuilder builder, String literal );
   }

   public enum TransactionIsolationLevel
   {
      ReadUncommitted,
      ReadCommitted,
      RepeatableRead,
      Serializable
   }


   public interface SQLStatementExecutionResult
   {
      SQLException[] Warnings { get; }
   }

   public interface SingleCommandExecutionResult : SQLStatementExecutionResult
   {
      Int32 AffectedRows { get; }
      String CommandTag { get; }
   }

   public interface BatchCommandExecutionResult : SQLStatementExecutionResult
   {
      Int32[] AffectedRows { get; }
      String CommandTag { get; }
   }

   public interface SQLDataRow : DataRow, SQLStatementExecutionResult
   {

   }
}

public static partial class E_CBAM
{
   public static AsyncEnumerator<SQLStatementExecutionResult> PrepareStatementForExecution( this SQLConnection connection, String sql )
   {
      return connection.PrepareStatementForExecution( connection.CreateStatementBuilder( sql ) );
   }

   public static async Task ExecuteNonQueryAsync( this SQLConnection connection, StatementBuilder stmt )
   {
      var iArgs = connection.PrepareStatementForExecution( stmt );
      while ( await iArgs.MoveNextAsync() ) ;
   }

   public static async Task ExecuteNonQueryAsync( this SQLConnection connection, StatementBuilder stmt, Action action )
   {
      var iArgs = connection.PrepareStatementForExecution( stmt );
      iArgs.IterationEndedEvent += ( sender, args ) => action();
      while ( await iArgs.MoveNextAsync() ) ;
   }

   public static async Task ExecuteQueryAsync( this SQLConnection connection, StatementBuilder stmt, Action<DataRow> action )
   {
      var iArgs = connection.PrepareStatementForExecution( stmt );
      while ( await iArgs.MoveNextAsync() )
      {
         action( iArgs.GetDataRow() );
      }
   }

   public static async Task<T> ExecuteStatementAsync<T>( this SQLConnection connection, StatementBuilder stmt, Func<AsyncEnumerator<SQLStatementExecutionResult>, Task<T>> executer )
   {
      ArgumentValidator.ValidateNotNullReference( connection );
      ArgumentValidator.ValidateNotNull( nameof( executer ), executer );

      var iArgs = connection.PrepareStatementForExecution( stmt );
      return await executer( iArgs );
   }

   public static async Task ExecuteStatementAsync( this SQLConnection connection, String sql, Func<AsyncEnumerator<SQLStatementExecutionResult>, Task> executer )
   {
      var iArgs = connection.PrepareStatementForExecution( connection.CreateStatementBuilder( sql ) );
      while ( await iArgs.MoveNextAsync() )
      {
         await executer( iArgs );
      }
   }

   public static async Task<T> ExecuteStatementAsync<T>( this SQLConnection connection, String sql, Func<AsyncEnumerator<SQLStatementExecutionResult>, Task<T>> executer )
   {
      return await ExecuteStatementAsync( connection, connection.CreateStatementBuilder( sql ), executer );
   }

   public static async Task ExecuteNonQueryAsync( this SQLConnection connection, String sql )
   {
      await connection.ExecuteNonQueryAsync( connection.CreateStatementBuilder( sql ) );
   }

   public static async Task ExecuteNonQueryAsync( this SQLConnection connection, String sql, Action action )
   {
      await connection.ExecuteNonQueryAsync( connection.CreateStatementBuilder( sql ), action );
   }

   public static async Task<T> GetFirstOrDefaultAsync<T>( this SQLConnection connection, StatementBuilder stmt, Int32 parameterIndex = 0, Func<DataColumn, Task<T>> extractor = null )
   {
      return await connection.ExecuteStatementAsync( stmt, async args =>
      {
         await args.MoveNextAsync();
         var row = args.GetDataRow();
         var retVal = row == null ? default( T ) : await ( extractor?.Invoke( row.GetColumn( parameterIndex ) ) ?? row.GetValueAsync<T>( parameterIndex ) );

         // Read until the end
         while ( await args.MoveNextAsync() ) ;

         return retVal;
      } );
   }

   public static async Task<T> GetFirstOrDefaultAsync<T>( this SQLConnection connection, String sql, Int32 parameterIndex = 0, Func<DataColumn, Task<T>> extractor = null )
   {
      return await connection.GetFirstOrDefaultAsync( connection.VendorFunctionality.CreateStatementBuilder( sql ), parameterIndex, extractor );
   }

   public static String EscapeLiteral( this SQLConnectionVendorFunctionality connection, String literal )
   {
      var b = new StringBuilder( literal.Length );
      connection.AppendEscapedLiteral( b, literal );
      return b.ToString();
   }

   public static async Task DoWriteStatements( this SQLConnection connection, Func<SQLConnection, Task> action )
   {
      await connection.DoStatements( action, false );
   }

   public static async Task<T> DoWriteStatementsAndReturn<T>( this SQLConnection connection, Func<SQLConnection, Task<T>> func )
   {
      return await connection.DoStatementsAndReturn( func, false );
   }

   public static async Task DoReadStatements( this SQLConnection connection, Func<SQLConnection, Task> action )
   {
      await connection.DoStatements( action, true );
   }

   public static async Task<T> DoReadStatementsAndReturn<T>( this SQLConnection connection, Func<SQLConnection, Task<T>> func )
   {
      return await connection.DoStatementsAndReturn( func, true );
   }

   public static async Task DoStatements( this SQLConnection connection, Func<SQLConnection, Task> action, Boolean readOnly )
   {
      var needToChange = await connection.GetReadOnlyAsync() != readOnly;
      if ( needToChange )
      {
         await connection.SetReadOnlyAsync( readOnly );
      }
      try
      {
         await action( connection );
      }
      finally
      {
         if ( needToChange )
         {
            await connection.SetReadOnlyAsync( !readOnly );
         }
      }
   }

   public static async Task<T> DoStatementsAndReturn<T>( this SQLConnection connection, Func<SQLConnection, Task<T>> func, Boolean readOnly )
   {
      var needToChange = await connection.GetReadOnlyAsync() != readOnly;
      if ( needToChange )
      {
         await connection.SetReadOnlyAsync( readOnly );
      }
      try
      {
         return await func( connection );
      }
      finally
      {
         if ( needToChange )
         {
            await connection.SetReadOnlyAsync( !readOnly );
         }
      }
   }

   public static StatementBuilder CreateStatementBuilder( this SQLConnection connection, String sql )
   {
      return connection.VendorFunctionality.CreateStatementBuilder( sql );
   }

   public static SQLDataRow GetDataRow( this AsyncEnumerator<SQLStatementExecutionResult> args )
   {
      return args.Current as SQLDataRow;
   }


}