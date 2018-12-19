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
using CBAM.Abstractions;
using CBAM.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.TabularData;

namespace CBAM.SQL
{
   /// <summary>
   /// This interfaces extends the generic CBAM <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> interface to provide SQL-specific functionality in addition to generic functionality.
   /// Furthermore, all generic type arguments of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> are bound to those that also provide SQL-specialization, and enables to use this interface for any SQL processing, regardless of vendor.
   /// </summary>
   public interface SQLConnection : Connection<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>
   {
      /// <summary>
      /// Gets the <see cref="SQL.DatabaseMetadata"/> object describing the database this <see cref="SQLConnection"/> is connected to.
      /// </summary>
      /// <value>The <see cref="SQL.DatabaseMetadata"/> object describing the database this <see cref="SQLConnection"/> is connected to.</value>
      /// <seealso cref="SQL.DatabaseMetadata"/>
      DatabaseMetadata DatabaseMetadata { get; }

      /// <summary>
      /// Potentially asynchronously gets the value indicating whether this <see cref="SQLConnection"/> is read-only, .
      /// </summary>
      /// <returns>A task which on completion will have value indicating whether this <see cref="SQLConnection"/> is read-only.</returns>
      ValueTask<Boolean> GetReadOnlyAsync();

      /// <summary>
      /// Asynchronously sets the value indicating whether this <see cref="SQLConnection"/> is read-only.
      /// </summary>
      /// <param name="isReadOnly">Whether this connection should be read-only.</param>
      /// <returns>A task which on completion has set the value indicating whether this <see cref="SQLConnection"/> is read-only. The returned value of the task should always be <c>1</c>.</returns>
      ValueTask<Int64> SetReadOnlyAsync( Boolean isReadOnly );

      /// <summary>
      /// Potentially asynchronously gets the value indicating current transaction isolation level.
      /// </summary>
      /// <returns>A task which on completion will have value indicating current transaction isolation level.</returns>
      /// <seealso cref="TransactionIsolationLevel"/>
      ValueTask<TransactionIsolationLevel> GetDefaultTransactionIsolationLevelAsync();

      /// <summary>
      /// Asynchronously sets the value indicating current transaction isolation level.
      /// </summary>
      /// <param name="level">The new transaction isolation level.</param>
      /// <returns>A task which on completion has set the value indicating current transaction isolation level. The returned value of the task should always be <c>1</c>.</returns>
      /// <seealso cref="TransactionIsolationLevel"/>
      ValueTask<Int64> SetDefaultTransactionIsolationLevelAsync( TransactionIsolationLevel level );

      /// <summary>
      /// Given current <see cref="SQLStatementExecutionResult"/> and context of reading SQL from some source (<see cref="MemorizingPotentiallyAsyncReader{TValue, TBufferItem}"/>), processes the result passively (no user input).
      /// This is used by <see cref="E_CBAM.ExecuteStatementsFromStreamAsync(SQLConnection, MemorizingPotentiallyAsyncReader{Char?, Char}, Func{SQLException, WhenExceptionInMultipleStatements})"/> method which is equivalent to running SQL dump from file to database.
      /// </summary>
      /// <param name="reader">The source where SQL statement originated.</param>
      /// <param name="statementInformation">The <see cref="SQLStatementBuilderInformation"/> about current statement.</param>
      /// <param name="executionResult">The <see cref="SQLStatementExecutionResult"/> encountered when enumerating <see cref="IAsyncEnumerable{T}"/> returned by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/>.</param>
      /// <returns>A task which should return <c>true</c> on completion if anything was done to <paramref name="reader"/>.</returns>
      ValueTask<Boolean> ProcessStatementResultPassively( MemorizingPotentiallyAsyncReader<Char?, Char> reader, SQLStatementBuilderInformation statementInformation, SQLStatementExecutionResult executionResult );

   }

   /// <summary>
   /// This interface extends generic CBAM interface <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> to provide SQL-specific functionality common for all SQL vendors.
   /// This functionality has mostly to do with SQL syntax.
   /// </summary>
   public interface SQLConnectionVendorFunctionality : ConnectionVendorFunctionality<SQLStatementBuilder, String>
   {

      /// <summary>
      /// Given a string, escapes any characters in it so that it can be interpreted as literal string in this SQL vendor.
      /// </summary>
      /// <param name="str">The string to escape. May be <c>null</c>.</param>
      /// <returns>Escaped string.</returns>
      String EscapeLiteral( String str );

      /// <summary>
      /// Given a <see cref="PeekablePotentiallyAsyncReader{TValue}"/> (which can wrap a normal <see cref="String"/>), tries to advance it over a single, complete SQL statement.
      /// </summary>
      /// <param name="reader">The reader to advance.</param>
      /// <returns>A task which will complete after advance is over. Return value is currently not used, since <see cref="ValueTask{TResult}"/> does not exist as non-generic version.</returns>
      ValueTask<Boolean> TryAdvanceReaderOverSingleStatement( PeekablePotentiallyAsyncReader<Char?> reader );

      /// <summary>
      /// Returns <c>true</c> if given character is ignored when it appears at start of SQL string.
      /// </summary>
      /// <param name="c">The character to check.</param>
      /// <returns><c>true</c> if <paramref name="c"/> is ignored when it appears at start of SQL string.</returns>
      Boolean CanTrimBegin( Char c );

      /// <summary>
      /// Returns <c>true</c> if given character is ignored when it appears at end of SQL string.
      /// </summary>
      /// <param name="c">The character to check.</param>
      /// <returns><c>true</c> if <paramref name="c"/> is ignored when it appears at end of SQL string.</returns>
      Boolean CanTrimEnd( Char c );
   }

   /// <summary>
   /// This enumeration describes the transaction isolation levels in relational databases.
   /// </summary>
   /// <seealso href="https://en.wikipedia.org/wiki/Isolation_(database_systems)"/>
   public enum TransactionIsolationLevel
   {
      /// <summary>
      /// Indicates the <c>READ UNCOMMITTED</c> isolation level.
      /// </summary>
      ReadUncommitted,
      /// <summary>
      /// Indicates the <c>READ COMMITTED</c> isolation level.
      /// </summary>
      ReadCommitted,

      /// <summary>
      /// Indicates the <c>REPEATABLE READ</c> isolation level.
      /// </summary>
      RepeatableRead,

      /// <summary>
      /// Indicates the <c>SERIALIZABLE</c> isolation level.
      /// </summary>
      Serializable
   }

   /// <summary>
   /// This is common interface for items enumerated by <see cref="IAsyncEnumerable{T}"/> returned by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/> method of <see cref="SQLConnection"/>.
   /// </summary>
   /// <seealso cref="SQLDataRow"/>
   /// <seealso cref="SingleCommandExecutionResult"/>
   /// <seealso cref="BatchCommandExecutionResult"/>
   public interface SQLStatementExecutionResult
   {
      /// <summary>
      /// Gets the warnings issued by backend during last call of <see cref="IAsyncEnumerator{T}.WaitForNextAsync"/>.
      /// </summary>
      /// <value>The warnings issued by backend during last call of <see cref="IAsyncEnumerator{T}.WaitForNextAsync"/>.</value>
      SQLException[] Warnings { get; }
   }

   /// <summary>
   /// This interface extends <see cref="SQLStatementExecutionResult"/> to provide additional information when backend has finished executing one SQL statement (SQL string may contain multiple statements).
   /// </summary>
   public interface SingleCommandExecutionResult : SQLStatementExecutionResult
   {
      /// <summary>
      /// Gets the amount of rows affected by the single SQL statement.
      /// </summary>
      /// <value>The amount of rows affected by the single SQL statement.</value>
      Int32 AffectedRows { get; }

      /// <summary>
      /// Gets the vendor-specific command tag string (e.g. <c>"INSERT"</c>, <c>"UPDATE"</c> etc) of the executed SQL statement.
      /// </summary>
      /// <value>The vendor-specific command tag string (e.g. <c>"INSERT"</c>, <c>"UPDATE"</c> etc) of the executed SQL statement.</value>
      String CommandTag { get; }
   }

   /// <summary>
   /// This interface extends <see cref="SQLStatementExecutionResult"/> to provide additional information when backend has finished executing a batch of SQL statement with various parameters.
   /// </summary>
   /// <seealso cref="SQLStatementBuilder.AddBatch"/>
   public interface BatchCommandExecutionResult : SQLStatementExecutionResult
   {
      /// <summary>
      /// Gets the amount of rows affected by each executed SQL statement.
      /// </summary>
      /// <value>The amount of rows affected by each executed SQL statement.</value>
      Int32[] AffectedRows { get; }

      /// <summary>
      /// Gets the vendor-specific command tag string (e.g. <c>"INSERT"</c>, <c>"UPDATE"</c> etc) of the executed SQL statement.
      /// </summary>
      /// <value>The vendor-specific command tag string (e.g. <c>"INSERT"</c>, <c>"UPDATE"</c> etc) of the executed SQL statement.</value>
      String CommandTag { get; }
   }

   /// <summary>
   /// This interface extends <see cref="SQLStatementExecutionResult"/> to provide additional information about single row sent by backend as a result of <c>"SELECT"</c> statement.
   /// Furthermore, this interface extends <see cref="AsyncDataRow"/> to provide access to the columns of the data row.
   /// </summary>
   public interface SQLDataRow : AsyncDataRow, SQLStatementExecutionResult
   {

   }

   /// <summary>
   /// This enumeration is used by <see cref="E_CBAM.ExecuteStatementsFromStreamAsync(SQLConnection, MemorizingPotentiallyAsyncReader{Char?, Char}, Func{SQLException, WhenExceptionInMultipleStatements})"/> and <see cref="E_CBAM.ExecuteStatementsFromStreamAsync(SQLConnection, System.IO.Stream, Encoding, Int32, Int32, Func{SQLException, WhenExceptionInMultipleStatements}, CancellationToken)"/> extension methods to control how the method behaves when an exception is occurred in statement result processing.
   /// </summary>
   public enum WhenExceptionInMultipleStatements
   {
      /// <summary>
      /// This value indicates that an exception should be simply re-thrown.
      /// </summary>
      Rethrow,
      /// <summary>
      /// This value indicates that exception should be ignored, and current transaction should be rollbacked (no SQL command is executed on error, as the transaction is automatically rollbacked when an error occurs).
      /// The SQL statement processing will continue.
      /// </summary>
      Continue,
      /// <summary>
      /// This value indicates that exception should be ignored, and new transaction should be started (current transaction is automatically rollbacked when an error occurs, and SQL statement to start new transaction (<c>"BEGIN TRANSACTION"</c>) is issued).
      /// The SQL statement processing will continue.
      /// </summary>
      RollbackAndStartNew
   }

}

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// This method is a shortcut of calling <see cref="M:E_UtilPack.OfType{T, U}(IAsyncEnumerable{T}, OfTypeInfo{U})"/> making this <see cref="IAsyncEnumerable{T}"/> of <see cref="SQLStatementExecutionResult"/> only return <see cref="SQLDataRow"/>s.
   /// </summary>
   /// <param name="enumerable">This SQL <see cref="IAsyncEnumerable{T}"/>.</param>
   /// <returns>Asynchronous enumerable which only returns <see cref="SQLDataRow"/>s and filters out all other items.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="IAsyncEnumerable{T}"/> is <c>null</c>.</exception>
   public static IAsyncEnumerable<SQLDataRow> IncludeDataRowsOnly( this IAsyncEnumerable<SQLStatementExecutionResult> enumerable )
   {
      return enumerable.Of().Type<SQLDataRow>();
   }


   //public static Task ExecuteQueryAsync( this SQLConnection connection, SQLStatementBuilder stmt, Action<SQLDataRow> action )
   //{
   //   return connection.PrepareStatementForExecution( stmt ).EnumerateAsync( res => action( res as SQLDataRow ) );
   //}

   /// <summary>
   /// Shortcut method to get some value from first seen <see cref="SQLDataRow"/> of <see cref="IAsyncEnumerator{T}"/> returned by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/> for given <see cref="SQLStatementBuilder"/>.
   /// </summary>
   /// <typeparam name="T">The type of the value to return.</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="statement">The <see cref="SQLStatementBuilder"/> to execute.</param>
   /// <param name="extractor">The asynchronous callback to get value from <see cref="SQLDataRow"/>.</param>
   /// <returns>A task which will return value of <paramref name="extractor"/> if at least one <see cref="SQLDataRow"/> is encountered during execution of <paramref name="statement"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="statement"/> or <paramref name="extractor"/> is <c>null</c>.</exception>
   public static async ValueTask<T> GetFirstOrDefaultAsync<T>( this SQLConnection connection, SQLStatementBuilder statement, Func<AsyncDataRow, ValueTask<T>> extractor )
   {
      ArgumentValidator.ValidateNotNullReference( connection );
      ArgumentValidator.ValidateNotNull( nameof( extractor ), extractor );
      return await connection.PrepareStatementForExecution( statement )
         .IncludeDataRowsOnly()
         .Select( row => extractor( row ) )
         .FirstOrDefaultAsync();
   }

   /// <summary>
   /// Shortcut method to get some value from first seen <see cref="SQLDataRow"/> of <see cref="IAsyncEnumerator{T}"/> returned by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/> for <see cref="SQLStatementBuilder"/> created with given SQL string.
   /// </summary>
   /// <typeparam name="T">The type of the value to return.</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="sql">The SQL string.</param>
   /// <param name="extractor">The asynchronous callback to get value from <see cref="SQLDataRow"/>.</param>
   /// <returns>A task which will return value of <paramref name="extractor"/> if at least one <see cref="SQLDataRow"/> is encountered during execution of <paramref name="sql"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="sql"/> or <paramref name="extractor"/> is <c>null</c>.</exception>
   public static ValueTask<T> GetFirstOrDefaultAsync<T>( this SQLConnection connection, String sql, Func<AsyncDataRow, ValueTask<T>> extractor )
   {
      return connection.GetFirstOrDefaultAsync( connection.VendorFunctionality.CreateStatementBuilder( sql ), extractor );
   }

   /// <summary>
   /// Shortcut method to get some value from first seen <see cref="SQLDataRow"/> of <see cref="IAsyncEnumerator{T}"/> returned by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/> for given <see cref="SQLStatementBuilder"/>.
   /// This method lets optionally specify a callback to extract value from single <see cref="AsyncDataColumn"/>, and also optionally specify a column index which will be used to get the <see cref="AsyncDataColumn"/> to extract value from.
   /// </summary>
   /// <typeparam name="T">The type of the value to return.</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="statement">The <see cref="SQLStatementBuilder"/> to execute.</param>
   /// <param name="parameterIndex">The index of the <see cref="AsyncDataColumn"/> to pass to <paramref name="extractor"/> callback, is <c>0</c> by default.</param>
   /// <param name="extractor">The optional asynchronous callback to use to extract the value from <see cref="AsyncDataColumn"/>.</param>
   /// <returns>A task which will return value of <paramref name="extractor"/> if at least one <see cref="SQLDataRow"/> is encountered during execution of <paramref name="statement"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="statement"/> is <c>null</c>.</exception>
   public static async ValueTask<T> GetFirstOrDefaultAsync<T>( this SQLConnection connection, SQLStatementBuilder statement, Int32 parameterIndex = 0, Func<AsyncDataColumn, ValueTask<T>> extractor = null )
   {
      return await connection
         .PrepareStatementForExecution( statement )
         .IncludeDataRowsOnly()
         .Select( async row => await ( extractor?.Invoke( row.GetColumn( parameterIndex ) ) ?? row.GetValueAsync<T>( parameterIndex ) ) )
         .FirstOrDefaultAsync();
   }

   /// <summary>
   /// Shortcut method to get some value from first seen <see cref="SQLDataRow"/> of <see cref="IAsyncEnumerator{T}"/> returned by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/> for <see cref="SQLStatementBuilder"/> created with given SQL string.
   /// This method lets optionally specify a callback to extract value from single <see cref="AsyncDataColumn"/>, and also optionally specify a column index which will be used to get the <see cref="AsyncDataColumn"/> to extract value from.
   /// </summary>
   /// <typeparam name="T">The type of the value to return.</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="sql">The <see cref="SQLStatementBuilder"/> to execute.</param>
   /// <param name="parameterIndex">The index of the <see cref="AsyncDataColumn"/> to pass to <paramref name="extractor"/> callback, is <c>0</c> by default.</param>
   /// <param name="extractor">The optional asynchronous callback to use to extract the value from <see cref="AsyncDataColumn"/>.</param>
   /// <returns>A task which will return value of <paramref name="extractor"/> if at least one <see cref="SQLDataRow"/> is encountered during execution of <paramref name="sql"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="sql"/> is <c>null</c>.</exception>
   public static ValueTask<T> GetFirstOrDefaultAsync<T>( this SQLConnection connection, String sql, Int32 parameterIndex = 0, Func<AsyncDataColumn, ValueTask<T>> extractor = null )
   {
      return connection.GetFirstOrDefaultAsync( connection.VendorFunctionality.CreateStatementBuilder( sql ), parameterIndex, extractor );
   }

   /// <summary>
   /// This is helper method to perform some action on <see cref="SQLConnection"/> and make sure that the connection is not in readonly mode.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="action">The asynchronous callback to use <see cref="SQLConnection"/></param>
   /// <returns>A task which on completion has executed given <paramref name="action"/> callback.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="action"/> is <c>null</c>.</exception>
   public static Task DoWriteStatements( this SQLConnection connection, Func<SQLConnection, Task> action )
   {
      return connection.DoStatements( action, false );
   }

   /// <summary>
   /// This is helper method to perform some asynchronous action on <see cref="SQLConnection"/> and make sure that the connection is not in readonly mode.
   /// Then, some value whhich is obtained by the asynchronous action, is returned.
   /// </summary>
   /// <typeparam name="T">The type of return value of callback</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="func">The asynchronous callback to use <see cref="SQLConnection"/> and return value of type <typeparamref name="T"/>.</param>
   /// <returns>A task which on completion has executed given <paramref name="func"/> callback.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="func"/> is <c>null</c>.</exception>
   public static ValueTask<T> DoWriteStatements<T>( this SQLConnection connection, Func<SQLConnection, ValueTask<T>> func )
   {
      return connection.DoStatements( func, false );
   }

   /// <summary>
   /// This is helper method to perform some action on <see cref="SQLConnection"/> and make sure that the connection is in readonly mode.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="action">The asynchronous callback to use <see cref="SQLConnection"/></param>
   /// <returns>A task which on completion has executed given <paramref name="action"/> callback.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="action"/> is <c>null</c>.</exception>
   public static Task DoReadStatements( this SQLConnection connection, Func<SQLConnection, Task> action )
   {
      return connection.DoStatements( action, true );
   }

   /// <summary>
   /// This is helper method to perform some asynchronous action on <see cref="SQLConnection"/> and make sure that the connection is in readonly mode.
   /// Then, some value whhich is obtained by the asynchronous action, is returned.
   /// </summary>
   /// <typeparam name="T">The type of return value of callback</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="func">The asynchronous callback to use <see cref="SQLConnection"/> and return value of type <typeparamref name="T"/>.</param>
   /// <returns>A task which on completion has executed given <paramref name="func"/> callback.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="func"/> is <c>null</c>.</exception>
   public static ValueTask<T> DoReadStatements<T>( this SQLConnection connection, Func<SQLConnection, ValueTask<T>> func )
   {
      return connection.DoStatements( func, true );
   }

   /// <summary>
   /// This is generic method to execute some asynchronous callback for this <see cref="SQLConnection"/> and make sure that the connection readonly mode is the one specified as parameter.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="action">The asynchronous callback to use <see cref="SQLConnection"/>.</param>
   /// <param name="readOnly">Whether to set <see cref="SQLConnection"/> in readonly mode before calling <paramref name="action"/> callback.</param>
   /// <returns>A task which on completion has executed given <paramref name="action"/> callback.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="action"/> is <c>null</c>.</exception>
   public static async Task DoStatements( this SQLConnection connection, Func<SQLConnection, Task> action, Boolean readOnly )
   {
      ArgumentValidator.ValidateNotNull( nameof( action ), action );
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

   /// <summary>
   /// This is generic method to execute some asynchronous callback for this <see cref="SQLConnection"/> and make sure that the connection readonly mode is the one specified as parameter.
   /// </summary>
   /// <typeparam name="T">The return type of the task of the asynchronous callback.</typeparam>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="func">The asynchronous callback to use <see cref="SQLConnection"/>.</param>
   /// <param name="readOnly">Whether to set <see cref="SQLConnection"/> in readonly mode before calling <paramref name="func"/> callback.</param>
   /// <returns>A task which on completion has executed given <paramref name="func"/> callback, and returns the result of <paramref name="func"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="func"/> is <c>null</c>.</exception>
   public static async ValueTask<T> DoStatements<T>( this SQLConnection connection, Func<SQLConnection, ValueTask<T>> func, Boolean readOnly )
   {
      ArgumentValidator.ValidateNotNull( nameof( func ), func );
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


   /// <summary>
   /// This method can be used to read SQL statements from stream (e.g. a file) and passively process each statement with this <see cref="SQLConnection"/> until the last statement is done processing.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="stream">The <see cref="System.IO.Stream"/> containing the SQL statements.</param>
   /// <param name="encoding">The <see cref="Encoding"/> to use when reading textual SQL statemetns from <paramref name="stream"/>. If <c>null</c>, a new instance of <see cref="UTF8Encoding"/> which does not emit nor throw will be used.</param>
   /// <param name="streamMaxBufferCount">The amount of characters to read for one statement until the buffer is cleared. This does not mean the maximum size for statement, instead it indicates that if after processing a single SQL statement, if the buffer is higher than this number, then it will be cleared.</param>
   /// <param name="streamReadChunkCount">The amount bytes to read in one chunk from given <paramref name="stream"/>.</param>
   /// <param name="onException">Optional callback to react when <see cref="SQLException"/> occurs during passive processing of single SQL statement. It should return <see cref="WhenExceptionInMultipleStatements"/>, or be left out, in which case <see cref="WhenExceptionInMultipleStatements.Rethrow"/> behaviour pattern will be used.</param>
   /// <param name="token">Optional <see cref="CancellationToken"/> to use when creating <see cref="StreamReaderWithResizableBuffer"/>.</param>
   /// <returns>A task which will on completion return amount of statements read.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="stream"/> is <c>null</c>.</exception>
   /// <seealso cref="ExecuteStatementsFromStreamAsync(SQLConnection, MemorizingPotentiallyAsyncReader{Char?, Char}, Func{SQLException, WhenExceptionInMultipleStatements})"/>
   /// <seealso cref="WhenExceptionInMultipleStatements"/>
   public static async ValueTask<Int64> ExecuteStatementsFromStreamAsync(
      this SQLConnection connection,
      System.IO.Stream stream,
      Encoding encoding,
      Int32 streamMaxBufferCount = 1024,
      Int32 streamReadChunkCount = 1024,
      Func<SQLException, WhenExceptionInMultipleStatements> onException = null,
      CancellationToken token = default
   )
   {
      ArgumentValidator.ValidateNotNullReference( connection );

      var streamReader = StreamFactory.CreateUnlimitedReader(
            stream,
            token: token,
            chunkSize: streamReadChunkCount
         );
      var charReader = ReaderFactory.NewNullableMemorizingValueReader(
         new StreamCharacterReaderLogic( ( encoding ?? new UTF8Encoding( false, false ) ).CreateDefaultEncodingInfo() ),
         streamReader
         );
      using ( charReader.ClearStreamWhenStreamBufferTooBig( streamReader, streamMaxBufferCount ) )
      {
         return await connection.ExecuteStatementsFromStreamAsync( charReader, onException: onException );
      }
   }


   /// <summary>
   /// This method can be used to read SQL statements from <see cref="MemorizingPotentiallyAsyncReader{TValue, TBufferItem}"/> reader, and passively process each statement with this <see cref="SQLConnection"/> until the last statement is done processing.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="reader">The <see cref="MemorizingPotentiallyAsyncReader{TValue, TBufferItem}"/> reader to use to read characters from.</param>
   /// <param name="onException">Optional callback to react when <see cref="SQLException"/> occurs during passive processing of single SQL statement. It should return <see cref="WhenExceptionInMultipleStatements"/>, or be left out, in which case <see cref="WhenExceptionInMultipleStatements.Rethrow"/> behaviour pattern will be used.</param>
   /// <returns>A task which will on completion return amount of statements read.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="reader"/> is <c>null</c>.</exception>
   /// <seealso cref="ExecuteStatementsFromStreamAsync(SQLConnection, System.IO.Stream, Encoding, Int32, Int32, Func{SQLException, WhenExceptionInMultipleStatements}, CancellationToken)"/>
   /// <seealso cref="WhenExceptionInMultipleStatements"/>
   /// <seealso cref="MemorizingPotentiallyAsyncReader{TValue, TBufferItem}"/>
   public static async ValueTask<Int64> ExecuteStatementsFromStreamAsync(
      this SQLConnection connection,
      MemorizingPotentiallyAsyncReader<Char?, Char> reader,
      Func<SQLException, WhenExceptionInMultipleStatements> onException = null
      )
   {
      Int32 charsRead;
      var totalStatements = 0;
      var vendorFunc = connection.VendorFunctionality;
      ArgumentValidator.ValidateNotNull( nameof( reader ), reader );
      do
      {
         reader.ClearBuffer();
         await vendorFunc.TryAdvanceReaderOverSingleStatement( reader );
         charsRead = reader.BufferCount;
         if ( charsRead > 0 )
         {
            var start = 0;
            var count = reader.BufferCount;
            // Trim begin
            while ( count > 0 && vendorFunc.CanTrimBegin( reader.Buffer[start] ) )
            {
               ++start;
               --count;
            }
            // Trim end
            while ( count > 0 && vendorFunc.CanTrimEnd( reader.Buffer[start + count - 1] ) )
            {
               --count;
            }

            if ( count > 0 )
            {
               WhenExceptionInMultipleStatements? whenException = null;
               var stmt = connection.CreateStatementBuilder( new String( reader.Buffer, start, count ) );
               var enumerable = connection.PrepareStatementForExecution( stmt );
               var stmtInfo = stmt.StatementBuilderInformation;
               try
               {
                  await enumerable.EnumerateAsync( res =>
                  {
                     connection.ProcessStatementResultPassively( reader, stmtInfo, res );
                  } );
               }
               catch ( SQLException sqle )
               {
                  try
                  {
                     whenException = onException?.Invoke( sqle );
                  }
                  catch
                  {
                     // Ignore
                  }

                  if ( !whenException.HasValue || whenException == WhenExceptionInMultipleStatements.Rethrow )
                  {
                     throw;
                  }
               }

               if ( whenException.HasValue )
               {
                  // Have to issue ROLLBACK statement in order to continue from errors
                  // Except that transaction is automatically rollbacked when an error occurs.
                  //await connection.ExecuteNonQueryAsync( "ROLLBACK" );

                  if ( whenException.Value == WhenExceptionInMultipleStatements.RollbackAndStartNew )
                  {
                     // TODO additional optional parameter to specify additional parameters to BEGIN TRANSACTION (isolation level etc)
                     // or, create DSL to SQLConnection
                     await connection.ExecuteAndIgnoreResults( "BEGIN TRANSACTION" );
                  }
               }

               ++totalStatements;
            }
         }

      } while ( charsRead > 0 );

      return totalStatements;
   }

}