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
extern alias CBAMA;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using CBAM.SQL;
using CBAM.SQL.Implementation;
using CBAMA::CBAM.Abstractions;
using CBAM.Abstractions.Implementation;
using UtilPack.AsyncEnumeration;
using UtilPack.TabularData;

namespace CBAM.SQL.Implementation
{
   /// <summary>
   /// This class extends <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/> and implements <see cref="SQLConnection"/> so that the code that should be common for all SQL vendors is located in this class.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The type of object actually implementing the functionality for this facade.</typeparam>
   /// <typeparam name="TVendor">The actual type of vendor.</typeparam>
   public abstract class SQLConnectionImpl<TConnectionFunctionality, TVendor> : ConnectionImpl<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, TVendor, TConnectionFunctionality>, SQLConnection
      where TConnectionFunctionality : class, Connection<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, TVendor>
      where TVendor : SQLConnectionVendorFunctionality
   {
      private Object _isReadOnly;
      private Object _isolationLevel;

      /// <summary>
      /// Creates a new instance of <see cref="SQLConnectionImpl{TConnectionFunctionality, TVendor}"/> with given parameters.
      /// </summary>
      /// <param name="connectionFunctionality">The object containing the actual <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> implementation.</param>
      /// <param name="metaData">The <see cref="SQL.DatabaseMetadata"/> object containing metadata functionality.</param>
      /// <exception cref="ArgumentNullException">If either of <paramref name="connectionFunctionality"/> or <paramref name="metaData"/> is <c>null</c>.</exception>
      public SQLConnectionImpl(
         TConnectionFunctionality connectionFunctionality,
         DatabaseMetadata metaData
         ) : base( connectionFunctionality )
      {
         this.DatabaseMetadata = ArgumentValidator.ValidateNotNull( nameof( metaData ), metaData );
      }

      /// <summary>
      /// Implements <see cref="SQLConnection.DatabaseMetadata"/> and gets the <see cref="SQL.DatabaseMetadata"/> object of this connection.
      /// </summary>
      /// <value>The <see cref="SQL.DatabaseMetadata"/> object of this connection.</value>
      public DatabaseMetadata DatabaseMetadata { get; }

      /// <summary>
      /// Implements <see cref="SQLConnection.GetReadOnlyAsync"/> and asynchronously gets value indicating whether this connection is in read-only mode.
      /// </summary>
      /// <returns>Asynchronously returns value indicating whether this connection is in read-only mode.</returns>
      public async ValueTask<Boolean> GetReadOnlyAsync()
      {
         if ( !this.IsReadOnlyProperty.HasValue )
         {
            this.IsReadOnlyProperty = await this.GetFirstOrDefaultAsync( this.GetSQLForGettingReadOnly(), extractor: this.InterpretReadOnly );
         }
         return this.IsReadOnlyProperty.Value;
      }

      /// <summary>
      /// Implements <see cref="SQLConnection.GetDefaultTransactionIsolationLevelAsync"/> and asynchronously gets value indicating current default transaction isolation level of this connection.
      /// </summary>
      /// <returns>Asynchronously returns value indicating current default transaction isolation level of this connection.</returns>
      /// <seealso cref="TransactionIsolationLevel"/>
      public async ValueTask<TransactionIsolationLevel> GetDefaultTransactionIsolationLevelAsync()
      {
         if ( !this.TransactionIsolationLevelProperty.HasValue )
         {
            this.TransactionIsolationLevelProperty = await this.GetFirstOrDefaultAsync( this.GetSQLForGettingTransactionIsolationLevel(), extractor: this.InterpretTransactionIsolationLevel );
         }
         return this.TransactionIsolationLevelProperty.Value;
      }

      /// <summary>
      /// Implements <see cref="SQLConnection.SetDefaultTransactionIsolationLevelAsync(TransactionIsolationLevel)"/> and asynchronously sets the current default transaction isolation level of this connection.
      /// </summary>
      /// <param name="level">The new <see cref="TransactionIsolationLevel"/> to set.</param>
      /// <returns>Asynchronously returns either <c>-1</c>, if current transaction isolation level is already same as given <paramref name="level"/>, or other number if SQL for setting default transaction isolation level was executed.</returns>
      public ValueTask<Int64> SetDefaultTransactionIsolationLevelAsync( TransactionIsolationLevel level )
      {
         var propValue = this.TransactionIsolationLevelProperty;
         ValueTask<Int64> retVal;
         if ( !propValue.HasValue || propValue.Value != level )
         {
            // This class implements Connection interface twice (TVendor vs SQLConnectionVendorFunctionality), that's why we need to call extension method manually.
            retVal = CBAMA::E_CBAM.ExecuteAndIgnoreResults<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>(
               this,
               this.GetSQLForSettingTransactionIsolationLevel( level ),
               () => this.TransactionIsolationLevelProperty = level
               );
         }
         else
         {
            retVal = new ValueTask<Int64>( -1 );
         }

         return retVal;
      }

      /// <summary>
      /// Implements <see cref="SQLConnection.SetReadOnlyAsync(bool)"/> and asynchronously sets connection read-only mode.
      /// </summary>
      /// <param name="isReadOnly">Whether connection should be in read-only mode.</param>
      /// <returns>Asynchronously returns either <c>-1</c>, if current read-only mode is same as given <paramref name="isReadOnly"/>, or other number if SQL for setting read-only mode was executed.</returns>
      public ValueTask<Int64> SetReadOnlyAsync( Boolean isReadOnly )
      {
         var propValue = this.IsReadOnlyProperty;
         ValueTask<Int64> retVal;
         if ( !propValue.HasValue || propValue.Value != isReadOnly )
         {
            // This class implements Connection interface twice (TVendor vs SQLConnectionVendorFunctionality), that's why we need to call extension method manually.
            retVal = CBAMA::E_CBAM.ExecuteAndIgnoreResults<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>(
            this,
            this.GetSQLForSettingReadOnly( isReadOnly ),
            () => this.IsReadOnlyProperty = isReadOnly
            );
         }
         else
         {
            retVal = new ValueTask<Int64>( -1 );
         }

         return retVal;
      }

      /// <inheritdoc />
      public abstract ValueTask<Boolean> ProcessStatementResultPassively( MemorizingPotentiallyAsyncReader<Char?, Char> reader, SQLStatementBuilderInformation statementInformation, SQLStatementExecutionResult executionResult );

      /// <summary>
      /// Gets the last seen value of read-only mode.
      /// </summary>
      /// <value>The last seen value of read-only mode.</value>
      protected Boolean? IsReadOnlyProperty
      {
         get
         {
            return (Boolean?)this._isReadOnly;
         }
         set
         {
            Interlocked.Exchange( ref this._isReadOnly, value );
         }
      }

      /// <summary>
      /// Gets the last seen value of default transaction isolation level.
      /// </summary>
      /// <value>The last seen value of default transaction isolation level.</value>
      protected TransactionIsolationLevel? TransactionIsolationLevelProperty
      {
         get
         {
            return (TransactionIsolationLevel?)this._isolationLevel;
         }
         set
         {
            Interlocked.Exchange( ref this._isolationLevel, value );
         }
      }

      SQLConnectionVendorFunctionality Connection<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>.VendorFunctionality => this.VendorFunctionality;

      /// <summary>
      /// This method should return SQL statement that is executed in order to get current default transaction isolation level.
      /// </summary>
      /// <returns>SQL statement to get current default transaction isolation level.</returns>
      protected abstract String GetSQLForGettingTransactionIsolationLevel();

      /// <summary>
      /// This method should return SQL statement that is executed in order to set current default transaction isolation level. 
      /// </summary>
      /// <param name="level">The isolation level to set.</param>
      /// <returns>SQL statement to set current default transaction isolation level.</returns>
      /// <remarks>
      /// The returned SQL statement should not be prepared statement - i.e., it should not have parameters.
      /// </remarks>
      protected abstract String GetSQLForSettingTransactionIsolationLevel( TransactionIsolationLevel level );

      /// <summary>
      /// This method should return SQL statement that is executed in order to get connection read-only mode.
      /// </summary>
      /// <returns>SQL statement to get current read-only mode.</returns>
      protected abstract String GetSQLForGettingReadOnly();

      /// <summary>
      /// This method should return SQL statement that is executed in order to set current read-only mode.
      /// </summary>
      /// <param name="isReadOnly">The read-only mode to set.</param>
      /// <returns>SQL statement to set current connection read-only mode.</returns>
      /// <remarks>
      /// The returned SLQ statement should not be prepared statement - i.e., it should not have parameters.
      /// </remarks>
      protected abstract String GetSQLForSettingReadOnly( Boolean isReadOnly );

      /// <summary>
      /// This method should interpret the value returned by executing SQL of <see cref="GetSQLForGettingReadOnly"/>.
      /// </summary>
      /// <param name="row">The row returned by executing SQL of <see cref="GetSQLForGettingReadOnly"/>.</param>
      /// <returns>The value indicating whether connection is in read-only mode.</returns>
      protected abstract ValueTask<Boolean> InterpretReadOnly( AsyncDataColumn row );

      /// <summary>
      /// This method should interpret the value returned by executing SQL of <see cref="GetSQLForGettingTransactionIsolationLevel"/>.
      /// </summary>
      /// <param name="row">The row returned by executing SQL of <see cref="GetSQLForGettingTransactionIsolationLevel"/>.</param>
      /// <returns>The <see cref="TransactionIsolationLevel"/> enumeration value.</returns>
      protected abstract ValueTask<TransactionIsolationLevel> InterpretTransactionIsolationLevel( AsyncDataColumn row );
   }

   /// <summary>
   /// This class provides implementation of <see cref="SQLConnectionVendorFunctionality"/> which should be the same for all SQL vendors.
   /// </summary>
   public abstract class DefaultConnectionVendorFunctionality : SQLConnectionVendorFunctionality
   {

      /// <inheritdoc />
      public abstract String EscapeLiteral( String str );

      /// <summary>
      /// Implements <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}.CreateStatementBuilder(TStatementCreationArgs)"/> and will check that <paramref name="sql"/> is not <c>null</c> and not empty, and then parse it using <see cref="TryParseStatementSQL(string, out int[])"/>, and finally return result of <see cref="CreateStatementBuilder(string, int[])"/>.
      /// </summary>
      /// <param name="sql">The textual SQL statement.</param>
      /// <returns>Will return <c>null</c> if <paramref name="sql"/> is <c>null</c> or empty, or can not be parsed into SQL. Otherwise will returns result of <see cref="CreateStatementBuilder(string, int[])"/>.</returns>
      public SQLStatementBuilder CreateStatementBuilder( String sql )
      {
         SQLStatementBuilder retVal;
         if ( !String.IsNullOrEmpty( sql ) )
         {
            var start = 0;
            var count = sql.Length;
            // Trim begin
            while ( count > 0 && this.CanTrimBegin( sql[start] ) )
            {
               ++start;
               --count;
            }
            // Trim end
            while ( count > 0 && this.CanTrimEnd( sql[start + count - 1] ) )
            {
               --count;
            }

            if ( start > 0 || count < sql.Length )
            {
               sql = new String( sql.ToCharArray(), start, count );
            }
            retVal = this.TryParseStatementSQL( sql, out var parameterIndices ) ?
               this.CreateStatementBuilder( sql, parameterIndices ) :
               null;
         }
         else
         {
            retVal = null;
         }
         return retVal;
      }

      /// <summary>
      /// Provides default implementation for <see cref="SQLConnectionVendorFunctionality.CanTrimBegin(char)"/> and returns <c>true</c> if <see cref="Char.IsWhiteSpace(char)"/> returns <c>true</c>.
      /// </summary>
      /// <param name="c">The character to check.</param>
      /// <returns><c>true</c> if <see cref="Char.IsWhiteSpace(char)"/> returns <c>true</c>.</returns>
      /// <remarks>
      /// Subclasses may override this method.
      /// </remarks>
      public virtual Boolean CanTrimBegin( Char c )
      {
         return Char.IsWhiteSpace( c );
      }

      /// <summary>
      /// Provides default implementation for <see cref="SQLConnectionVendorFunctionality.CanTrimEnd(char)"/> and returns <c>true</c> if <see cref="Char.IsWhiteSpace(char)"/> returns <c>true</c>, or if <paramref name="c"/> is <c>;</c> character.
      /// </summary>
      /// <param name="c">The character to check.</param>
      /// <returns><c>true</c> if <see cref="Char.IsWhiteSpace(char)"/> returns <c>true</c>, or if <paramref name="c"/> is <c>;</c> character-</returns>
      /// <remarks>
      /// Subclasses may override this method.
      /// </remarks>
      public virtual Boolean CanTrimEnd( Char c )
      {
         return this.CanTrimBegin( c ) || c == ';';
      }

      /// <summary>
      /// This method is called by <see cref="CreateStatementBuilder(string)"/> and should try to parse textual SQL string so that indices of parameter characters (<c>?</c>) are known.
      /// </summary>
      /// <param name="sql">The textual SQL to parse.</param>
      /// <param name="parameterIndices">This parameter should have indices of legal parameter characters (<c>?</c>) in <paramref name="sql"/>.</param>
      /// <returns><c>true</c> if <paramref name="sql"/> at least looks like valid SQL; <c>false</c> otherwise.</returns>
      protected abstract Boolean TryParseStatementSQL( String sql, out Int32[] parameterIndices );

      /// <summary>
      /// This method should actually create instance of <see cref="SQLStatementBuilder"/> once SQL has been parsed and indices of parameter characters (<c>?</c>) are known.
      /// </summary>
      /// <param name="sql">The textual SQL statement.</param>
      /// <param name="parameterIndices">The indices of legal parameter characters (<c>?</c>) in <paramref name="sql"/>.</param>
      /// <returns>A new instance of <see cref="SQLStatementBuilder"/>.</returns>
      protected abstract SQLStatementBuilder CreateStatementBuilder( String sql, Int32[] parameterIndices );

      /// <inheritdoc/>
      public abstract ValueTask<Boolean> TryAdvanceReaderOverSingleStatement( PeekablePotentiallyAsyncReader<Char?> reader );

   }

   /// <summary>
   /// This class provides default implementation for <see cref="SQLStatementExecutionResult"/>.
   /// </summary>
   public abstract class AbstractCommandExecutionResult : SQLStatementExecutionResult
   {
      private readonly Lazy<SQLException[]> _warnings;

      /// <summary>
      /// Initializes a new instance of <see cref="AbstractCommandExecutionResult"/> with given parameters.
      /// </summary>
      /// <param name="commandTag">Textual SQL command tag, if any. May be <c>null</c>.</param>
      /// <param name="warnings">The lazily initialized <see cref="Lazy{T}"/> to get occurred warnings. May be <c>null</c>.</param>
      public AbstractCommandExecutionResult(
         String commandTag,
         Lazy<SQLException[]> warnings
         )
      {
         this.CommandTag = commandTag;
         this._warnings = warnings;
      }

      /// <summary>
      /// Gets the SQL command tag, if such was supplied.
      /// </summary>
      /// <value>The SQL command tag or <c>null</c>.</value>
      public String CommandTag { get; }

      /// <summary>
      /// Implements <see cref="SQLStatementExecutionResult.Warnings"/> and gets the warnings related to previous SQL command execution, or empty array if none occurred.
      /// </summary>
      /// <value>The warnings related to previous SQL command execution, or empty array if none occurred.</value>
      public SQLException[] Warnings
      {
         get
         {
            return this._warnings?.Value ?? Empty<SQLException>.Array;
         }
      }

   }

   /// <summary>
   /// This class provides default implementation for <see cref="SingleCommandExecutionResult"/> by extending <see cref="AbstractCommandExecutionResult"/>.
   /// </summary>
   public sealed class SingleCommandExecutionResultImpl : AbstractCommandExecutionResult, SingleCommandExecutionResult
   {
      /// <summary>
      /// Creates a new instance of <see cref="SingleCommandExecutionResultImpl"/> with given parameters.
      /// </summary>
      /// <param name="commandTag">Textual SQL command tag, if any. May be <c>null</c>.</param>
      /// <param name="warnings">The lazily initialized <see cref="Lazy{T}"/> to get occurred warnings. May be <c>null</c>.</param>
      /// <param name="affectedRows">How many rows were affected by this single command.</param>
      public SingleCommandExecutionResultImpl(
         String commandTag,
         Lazy<SQLException[]> warnings,
         Int32 affectedRows
         ) : base( commandTag, warnings )
      {
         this.AffectedRows = affectedRows;
      }

      /// <summary>
      /// Implements <see cref="SingleCommandExecutionResult.AffectedRows"/> and gets the amount of rows affected by single command.
      /// </summary>
      /// <value>The amount of rows affected by single command.</value>
      public Int32 AffectedRows { get; }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="BatchCommandExecutionResult"/> by extending <see cref="AbstractCommandExecutionResult"/>.
   /// </summary>
   public sealed class BatchCommandExecutionResultImpl : AbstractCommandExecutionResult, BatchCommandExecutionResult
   {
      /// <summary>
      /// Creates a new instane of <see cref="BatchCommandExecutionResultImpl"/> with given parameters.
      /// </summary>
      /// <param name="commandTag">Textual SQL command tag, if any. May be <c>null</c>.</param>
      /// <param name="warnings">The lazily initialized <see cref="Lazy{T}"/> to get occurred warnings. May be <c>null</c>.</param>
      /// <param name="affectedRows">The array indicating amount of affected rows for each item in the batch.</param>
      public BatchCommandExecutionResultImpl(
         String commandTag,
         Lazy<SQLException[]> warnings,
         Int32[] affectedRows
         ) : base( commandTag, warnings )
      {
         this.AffectedRows = affectedRows;
      }

      /// <summary>
      /// Implements <see cref="BatchCommandExecutionResult.AffectedRows"/> and gets the amount of rows affected by each executed SQL statement.
      /// </summary>
      /// <value>The amount of rows affected by each executed SQL statement.</value>
      public Int32[] AffectedRows { get; }
   }

}