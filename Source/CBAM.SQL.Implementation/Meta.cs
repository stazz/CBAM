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
using CBAM.SQL;
using CBAM.Tabular;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.AsyncEnumeration;

namespace CBAM.SQL.Implementation
{
   public abstract class DatabaseMetadataImpl : DatabaseMetadata
   {
      public DatabaseMetadataImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         SQLConnectionFunctionality connectionFunctionality
         )
      {
         this.VendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );
      }

      protected SQLConnectionVendorFunctionality VendorFunctionality { get; }

      protected SQLConnectionFunctionality ConnectionFunctionality { get; }

      public AsyncEnumerator<SQLStatementExecutionResult> PrepareSchemaSearch( String schemaNamePattern )
      {
         return this.UseSQLSearch(
            this.CreateSQLForSchemaSearch( schemaNamePattern ),
            stmt => SetSubsequentNonNullStrings( stmt, schemaNamePattern )
            );
      }

      public AsyncEnumerator<SQLStatementExecutionResult> PrepareTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes )
      {
         return this.UseSQLSearch(
            this.CreateSQLForTableSearch( schemaNamePattern, tableNamePattern, tableTypes ),
            stmt =>
            {
               var idx = 0;
               SetSubsequentNonNullStrings( stmt, ref idx, schemaNamePattern, tableNamePattern );
               foreach ( var tType in tableTypes )
               {
                  stmt.SetParameterString( idx++, this.GetStringForTableType( tType ) );
               }
            }
            );
      }

      public AsyncEnumerator<SQLStatementExecutionResult> PrepareColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern )
      {
         return this.UseSQLSearch(
            this.CreateSQLForColumnSearch( schemaNamePattern, tableNamePattern, columnNamePattern ),
            stmt => SetSubsequentNonNullStrings( stmt, schemaNamePattern, tableNamePattern, columnNamePattern )
            );
      }

      public AsyncEnumerator<SQLStatementExecutionResult> PreparePrimaryKeySearch( String schemaNamePattern, String tableNamePattern )
      {
         return this.UseSQLSearch(
            this.CreateSQLForPrimaryKeySearch( schemaNamePattern, tableNamePattern ),
            stmt => SetSubsequentNonNullStrings( stmt, schemaNamePattern, tableNamePattern )
            );
      }

      public AsyncEnumerator<SQLStatementExecutionResult> PrepareForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
      {
         return this.UseSQLSearch(
            this.CreateSQLForForeignKeySearch( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ),
            stmt => SetSubsequentNonNullStrings( stmt, primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName )
            );
      }

      protected AsyncEnumerator<SQLStatementExecutionResult> UseSQLSearch(
         String sql,
         Action<StatementBuilder> prepareStatement
         )
      {
         var builder = this.VendorFunctionality.CreateStatementBuilder( sql );
         prepareStatement?.Invoke( builder );
         return this.ConnectionFunctionality.CreateIterationArguments( builder );
      }

      protected static void SetSubsequentNonNullStrings( StatementBuilder stmt, params String[] values )
      {
         var idx = 0;
         SetSubsequentNonNullStrings( stmt, ref idx, values );
      }

      protected static void SetSubsequentNonNullStrings( StatementBuilder stmt, ref Int32 idx, params String[] values )
      {
         foreach ( var str in values )
         {
            if ( str != null )
            {
               stmt.SetParameterString( idx++, str );
            }
         }
      }

      public abstract Task<SchemaMetadata> ExtractSchemaAsync( DataRow row );
      public abstract Task<TableMetadata> ExtractTableAsync( DataRow row );
      public abstract Task<ColumnMetadata> ExtractColumnAsync( DataRow row );
      public abstract Task<PrimaryKeyMetadata> ExtractPrimaryKeyAsync( DataRow row );
      public abstract Task<ForeignKeyMetadata> ExtractForeignKeyAsync( DataRow row );

      protected abstract String CreateSQLForSchemaSearch( String schemaNamePattern );
      protected abstract String CreateSQLForTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes );
      protected abstract String GetStringForTableType( TableType tableType );
      protected abstract String CreateSQLForColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern );
      protected abstract String CreateSQLForPrimaryKeySearch( String schemaNamePattern, String tableNamePattern );
      protected abstract String CreateSQLForForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName );
   }

   public abstract class SQLCachingDatabaseMetadataImpl : DatabaseMetadataImpl
   {
      private sealed class SQLCacheByParameterCount
      {
         private readonly String[] _cache;
         private readonly Func<Int32, String> _factory;

         public SQLCacheByParameterCount(
            Int32 maxPermutationCount,
            Func<Int32, String> factory
            )
         {
            this._cache = new String[maxPermutationCount];
            this._factory = factory;
         }

         public String GetSQL( Int32 permutationOrderNumber )
         {
            var retVal = this._cache[permutationOrderNumber];
            if ( retVal == null )
            {
               retVal = this._factory( permutationOrderNumber );
               if ( retVal != null )
               {
                  Interlocked.Exchange( ref this._cache[permutationOrderNumber], retVal );
               }
            }

            return retVal;
         }
      }

      private sealed class SQLCacheByParameterCount<T>
         where T : class
      {
         private readonly String[] _cache;
         private readonly Func<Int32, T, String> _factory;

         public SQLCacheByParameterCount(
            Int32 maxPermutationCount,
            Func<Int32, T, String> factory
            )
         {
            this._cache = new String[maxPermutationCount];
            this._factory = factory;
         }

         public String GetSQL( Int32 permutationOrderNumber, T param )
         {
            var retVal = this._cache[permutationOrderNumber];
            if ( retVal == null )
            {
               retVal = this._factory( permutationOrderNumber, param );
               if ( retVal != null && param == null )
               {
                  Interlocked.Exchange( ref this._cache[permutationOrderNumber], retVal );
               }
            }

            return retVal;
         }
      }


      private readonly SQLCacheByParameterCount _schemaSearchCache;
      private readonly SQLCacheByParameterCount<TableType[]> _tableSearchCache;
      private readonly SQLCacheByParameterCount _columnSearchCache;
      private readonly SQLCacheByParameterCount _primaryKeySearchCache;
      private readonly SQLCacheByParameterCount _foreignKeySearchCache;

      public SQLCachingDatabaseMetadataImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         SQLConnectionFunctionality connectionFunctionality,
         Func<Int32, String> schemaSearchSQLFactory, // 0 - (schemaNamePattern Missing), 1 - (schemaNamePattern Present)
         Func<Int32, TableType[], String> tableSearchSQLFactory, // 0 - (schemaNamePattern M, tableNamePattern M, tableTypes M), 1 - (schemaNamePattern M, tableNamePattern M, tableTypes P), 2 - (schemaNamePattern M, tableNamePattern P, tableTypes M), 3 - (schemaNamePattern M, tableNamePattern P, tableTypes P)
         Func<Int32, String> columnSearchSQLFactory,
         Func<Int32, String> primaryKeySearchSQLFactory,
         Func<Int32, String> foreignKeySearchSQLFactory
         ) : base( vendorFunctionality, connectionFunctionality )
      {
         this._schemaSearchCache = new SQLCacheByParameterCount( 1 << 1, schemaSearchSQLFactory );
         this._tableSearchCache = new SQLCacheByParameterCount<TableType[]>( 1 << 3, tableSearchSQLFactory );
         this._columnSearchCache = new SQLCacheByParameterCount( 1 << 3, columnSearchSQLFactory );
         this._primaryKeySearchCache = new SQLCacheByParameterCount( 1 << 2, primaryKeySearchSQLFactory );
         this._foreignKeySearchCache = new SQLCacheByParameterCount( 1 << 4, foreignKeySearchSQLFactory );
      }

      protected override String CreateSQLForSchemaSearch( String schemaNamePattern )
      {
         return this._schemaSearchCache.GetSQL( schemaNamePattern == null ? 0 : 1 );
      }

      protected override String CreateSQLForTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes )
      {
         return this._tableSearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern, tableTypes == null ? null : "" ), tableTypes );
      }

      protected override String CreateSQLForColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern )
      {
         return this._columnSearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern, columnNamePattern ) );
      }

      protected override String CreateSQLForPrimaryKeySearch( String schemaNamePattern, String tableNamePattern )
      {
         return this._primaryKeySearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern ) );
      }

      protected override String CreateSQLForForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
      {
         return this._foreignKeySearchCache.GetSQL( GetLexicographicalOrderNumber( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ) );
      }


      private static Int32 GetLexicographicalOrderNumber( params String[] paramz )
      {
         // Consider boolean array as array of characters '0' or '1'.
         // So with the array of 3 booleans, we would get
         // 000 -> 0
         // 001 -> 1
         // 010 -> 2
         // 011 -> 3
         // 100 -> 4
         // 101 -> 5
         // 110 -> 6
         // 111 -> 7

         // So basically, we need to perform number base conversion from 2 to 10 using boolean array
         // Our boolean array is paramz.Select(p => p != null) which we can inline
         var retVal = 0;
         var curPow = 1;
         for ( var i = 0; i < paramz.Length; ++i )
         {
            if ( paramz[i] != null )
            {
               retVal += curPow;
            }
            curPow *= 2;
         }

         return retVal;
      }

   }
}
