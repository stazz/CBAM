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
using CBAM.SQL;
using CBAM.SQL.Implementation;
using CBAM.Abstractions;
using CBAM.Abstractions.Implementation;
using CBAM.Tabular;
using UtilPack.AsyncEnumeration;

namespace CBAM.SQL.Implementation
{
   public abstract class SQLConnectionImpl<TConnectionFunctionality, TVendor> : ConnectionImpl<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor, TConnectionFunctionality>, SQLConnection
      where TConnectionFunctionality : DefaultConnectionFunctionality<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, TVendor>, SQLConnectionFunctionality
      where TVendor : SQLConnectionVendorFunctionality
   {
      private Object _isReadOnly;
      private Object _isolationLevel;

      public SQLConnectionImpl(
         TConnectionFunctionality connectionFunctionality,
         DatabaseMetadata metaData
         ) : base( connectionFunctionality )
      {
         this.DatabaseMetadata = ArgumentValidator.ValidateNotNull( nameof( metaData ), metaData );
      }

      public DatabaseMetadata DatabaseMetadata { get; }

      public async Task<Boolean> GetReadOnlyAsync()
      {
         if ( !this.IsReadOnlyProperty.HasValue )
         {
            this.IsReadOnlyProperty = await this.GetFirstOrDefaultAsync( this.GetSQLForGettingReadOnly(), extractor: this.InterpretReadOnly );
         }
         return this.IsReadOnlyProperty.Value;
      }

      public async Task<TransactionIsolationLevel> GetDefaultTransactionIsolationLevelAsync()
      {
         if ( !this.TransactionIsolationLevelProperty.HasValue )
         {
            this.TransactionIsolationLevelProperty = await this.GetFirstOrDefaultAsync( this.GetSQLForGettingTransactionIsolationLevel(), extractor: this.InterpretTransactionIsolationLevel );
         }
         return this.TransactionIsolationLevelProperty.Value;
      }


      public async Task SetDefaultTransactionIsolationLevelAsync( TransactionIsolationLevel level )
      {
         await this.ExecuteNonQueryAsync( this.GetSQLForSettingTransactionIsolationLevel( level ), () => this.TransactionIsolationLevelProperty = level );
      }

      public async Task SetReadOnlyAsync( Boolean isReadOnly )
      {
         await this.ExecuteNonQueryAsync( this.GetSQLForSettingReadOnly( isReadOnly ), () => this.IsReadOnlyProperty = isReadOnly );
      }

      public abstract ValueTask<Boolean> ProcessStatementResultPassively( MemorizingPotentiallyAsyncReader<Char?, Char> reader, String sql, SQLStatementExecutionResult executionResult );

      protected Boolean? IsReadOnlyProperty
      {
         get
         {
            return (Boolean?) this._isReadOnly;
         }
         set
         {
            Interlocked.Exchange( ref this._isReadOnly, value );
         }
      }

      protected TransactionIsolationLevel? TransactionIsolationLevelProperty
      {
         get
         {
            return (TransactionIsolationLevel?) this._isolationLevel;
         }
         set
         {
            Interlocked.Exchange( ref this._isolationLevel, value );
         }
      }

      SQLConnectionVendorFunctionality Connection<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>.VendorFunctionality => this.VendorFunctionality;

      protected abstract String GetSQLForGettingTransactionIsolationLevel();

      protected abstract String GetSQLForSettingTransactionIsolationLevel( TransactionIsolationLevel level );

      protected abstract String GetSQLForGettingReadOnly();

      protected abstract String GetSQLForSettingReadOnly( Boolean isReadOnly );

      protected abstract ValueTask<Boolean> InterpretReadOnly( DataColumn row );

      protected abstract ValueTask<TransactionIsolationLevel> InterpretTransactionIsolationLevel( DataColumn row );
   }

   public abstract class DefaultConnectionVendorFunctionality : SQLConnectionVendorFunctionality
   {

      public abstract void AppendEscapedLiteral( StringBuilder builder, String literal );

      public StatementBuilder CreateStatementBuilder( String sql )
      {
         sql = sql?.Trim();
         return !String.IsNullOrEmpty( sql ) && this.TryParseStatementSQL( sql, out var parameterIndices ) ?
            this.CreateStatementBuilder( sql, parameterIndices ) :
            null;
      }

      public virtual Boolean CanTrimBegin( Char c )
      {
         return Char.IsWhiteSpace( c );
      }

      public virtual Boolean CanTrimEnd( Char c )
      {
         return this.CanTrimBegin( c ) || c == ';';
      }

      protected abstract Boolean TryParseStatementSQL( String sql, out Int32[] parameterIndices );

      protected abstract StatementBuilder CreateStatementBuilder( String sql, Int32[] parameterIndices );

      public abstract ValueTask<Boolean> TryAdvanceReaderOverSingleStatement( PeekablePotentiallyAsyncReader<Char?> reader );

   }



   public interface SQLConnectionFunctionality : Connection<StatementBuilder, StatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>
   {

   }

   public abstract class AbstractCommandExecutionResult
   {
      private readonly Lazy<SQLException[]> _warnings;

      public AbstractCommandExecutionResult(
         String commandTag,
         Lazy<SQLException[]> warnings
         )
      {
         this.CommandTag = commandTag;
         this._warnings = warnings;
      }

      public String CommandTag { get; }

      public SQLException[] Warnings
      {
         get
         {
            return this._warnings?.Value ?? Empty<SQLException>.Array;
         }
      }

   }

   public sealed class SingleCommandExecutionResultImpl : AbstractCommandExecutionResult, SingleCommandExecutionResult
   {
      public SingleCommandExecutionResultImpl(
         String commandTag,
         Lazy<SQLException[]> warnings,
         Int32 affectedRows
         ) : base( commandTag, warnings )
      {
         this.AffectedRows = affectedRows;
      }

      public Int32 AffectedRows { get; }
   }

   public sealed class BatchCommandExecutionResultImpl : AbstractCommandExecutionResult, BatchCommandExecutionResult
   {
      public BatchCommandExecutionResultImpl(
         String commandTag,
         Lazy<SQLException[]> warnings,
         Int32[] affectedRows
         ) : base( commandTag, warnings )
      {
         this.AffectedRows = affectedRows;
      }

      public Int32[] AffectedRows { get; }
   }

}

public static partial class E_CBAM
{
   public static async Task ExecuteStatementAsync( this SQLConnectionFunctionality connection, StatementBuilder statementBuilder, Func<AsyncEnumerator<SQLStatementExecutionResult>, Task> executer )
   {
      ArgumentValidator.ValidateNotNullReference( connection );
      ArgumentValidator.ValidateNotNull( nameof( statementBuilder ), statementBuilder );
      ArgumentValidator.ValidateNotNull( nameof( executer ), executer );

      await executer( connection.PrepareStatementForExecution( statementBuilder ) );
   }

   public static async Task<TResult> ExecuteStatementAsync<TResult>( this SQLConnectionFunctionality connection, StatementBuilder statementBuilder, Func<AsyncEnumerator<SQLStatementExecutionResult>, Task<TResult>> executer )
   {
      ArgumentValidator.ValidateNotNullReference( connection );
      ArgumentValidator.ValidateNotNull( nameof( statementBuilder ), statementBuilder );
      ArgumentValidator.ValidateNotNull( nameof( executer ), executer );

      return await executer( connection.PrepareStatementForExecution( statementBuilder ) );
   }


}
