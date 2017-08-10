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
using System.Threading.Tasks;
using System.Threading;
using CBAM.Abstractions;
using UtilPack.AsyncEnumeration;
using UtilPack.TabularData;

namespace CBAM.SQL
{
   public interface DatabaseMetadata
   {
      AsyncEnumerator<SQLStatementExecutionResult> PrepareSchemaSearch( String schemaNamePattern = null );
      AsyncEnumerator<SQLStatementExecutionResult> PrepareTableSearch( String schemaNamePattern, String tableNamePattern, params TableType[] tableTypes );
      AsyncEnumerator<SQLStatementExecutionResult> PrepareColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern = null );
      AsyncEnumerator<SQLStatementExecutionResult> PreparePrimaryKeySearch( String schemaNamePattern, String tableNamePattern );
      AsyncEnumerator<SQLStatementExecutionResult> PrepareForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName );

      Task<SchemaMetadata> ExtractSchemaAsync( AsyncDataRow row );
      Task<TableMetadata> ExtractTableAsync( AsyncDataRow row );
      Task<ColumnMetadata> ExtractColumnAsync( AsyncDataRow row );
      Task<PrimaryKeyMetadata> ExtractPrimaryKeyAsync( AsyncDataRow row );
      Task<ForeignKeyMetadata> ExtractForeignKeyAsync( AsyncDataRow row );
   }

   public interface DatabaseElementWithSchemaName
   {
      String SchemaName { get; }
   }

   public interface SchemaMetadata : DatabaseElementWithSchemaName
   {
   }

   public interface DatabaseElementWithTableName : DatabaseElementWithSchemaName
   {
      String TableName { get; }
   }

   public interface DatabaseElementWithComment
   {
      String Comment { get; }
   }

   public interface DatabaseElementWithTypeName
   {
      String TypeName { get; }
   }

   public interface TableMetadata : DatabaseElementWithTableName, DatabaseElementWithComment, DatabaseElementWithTypeName
   {
      TableType? TableType { get; }
   }

   public interface DatabaseElementWithColumnName : DatabaseElementWithTableName
   {
      String ColumnName { get; }
   }

   public interface DatabaseElementWithOrdinalPosition
   {
      Int32 OrdinalPosition { get; }
   }

   public interface ColumnMetadata : DatabaseElementWithColumnName, DatabaseElementWithComment, DatabaseElementWithTypeName, DatabaseElementWithOrdinalPosition
   {
      Int32 ColumnSize { get; }
      Int32 DecimalDigits { get; }
      Boolean? Nullable { get; }
      String ColumnDefaultValue { get; }
      Int32? ColumnSQLType { get; }
      Type ColumnCLRType { get; }
      Boolean IsPrimaryKeyColumn { get; }
   }

   public interface KeyMetadataInfo : DatabaseElementWithColumnName
   {
      String KeyName { get; }
   }

   public interface KeyMetadata : KeyMetadataInfo, DatabaseElementWithOrdinalPosition
   {
      ConstraintCharacteristics? Deferrability { get; }
   }

   public interface PrimaryKeyMetadata : KeyMetadata
   {
   }

   public interface ForeignKeyMetadata : KeyMetadata
   {
      KeyMetadataInfo PrimaryKey { get; }
      ReferentialAction? OnUpdate { get; }
      ReferentialAction? OnDelete { get; }
   }

   // Copy-pasta from SQL-generator so casting is easy
   public enum ReferentialAction { Cascade, SetNull, SetDefault, Restrict, NoAction }
   public enum ConstraintCharacteristics { InitiallyImmediate_Deferrable, InitiallyDeferred_Deferrable, NotDeferrable }

   // Table types
   public enum TableType
   {
      Table,
      View,
      SystemTable,
      GlobalTemporary,
      LocalTemporary,
      Alias,
      Synonym,
      MaxValue
   }
}

public static partial class E_CBAM
{
   public static async Task<List<ForeignKeyMetadata>> GetExportedForeignKeyMetadataAsync( this DatabaseMetadata md, String schemaName, String tableName )
   {
      return await md.GetCrossReferenceMetadataAsync( schemaName, tableName, null, null );
   }

   public static async Task<List<ForeignKeyMetadata>> GetImportedForeignKeyMetadataAsync( this DatabaseMetadata md, String schemaName, String tableName )
   {
      return await md.GetCrossReferenceMetadataAsync( null, null, schemaName, tableName );
   }

   public static async Task<List<SchemaMetadata>> GetSchemaMetadataAsync( this DatabaseMetadata md, String schemaNamePattern = null )
   {
      var list = new List<SchemaMetadata>();
      await md.PrepareSchemaSearch( schemaNamePattern ).EnumerateAsync( async schema => list.Add( await md.ExtractSchemaAsync( (SQLDataRow) schema ) ) );
      return list;
   }

   public static async Task<List<TableMetadata>> GetTableMetadataAsync( this DatabaseMetadata md, String schemaNamePattern, String tableNamePattern, params TableType[] tableTypes )
   {
      var list = new List<TableMetadata>();
      await md.PrepareTableSearch( schemaNamePattern, tableNamePattern ).EnumerateAsync( async table => list.Add( await md.ExtractTableAsync( (SQLDataRow) table ) ) );
      return list;
   }

   public static async Task<List<ColumnMetadata>> GetColumnMetadataAsync( this DatabaseMetadata md, String schemaNamePattern, String tableNamePattern, String columnNamePattern = null )
   {
      var list = new List<ColumnMetadata>();
      await md.PrepareColumnSearch( schemaNamePattern, tableNamePattern, columnNamePattern ).EnumerateAsync( async column => list.Add( await md.ExtractColumnAsync( (SQLDataRow) column ) ) );
      return list;
   }

   public static async Task<List<PrimaryKeyMetadata>> GetPrimaryKeyMetadataAsync( this DatabaseMetadata md, String schemaNamePattern, String tableNamePattern )
   {
      var list = new List<PrimaryKeyMetadata>();
      await md.PreparePrimaryKeySearch( schemaNamePattern, tableNamePattern ).EnumerateAsync( async pk => list.Add( await md.ExtractPrimaryKeyAsync( (SQLDataRow) pk ) ) );
      return list;
   }

   public static async Task<List<ForeignKeyMetadata>> GetCrossReferenceMetadataAsync( this DatabaseMetadata md, String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
   {
      var list = new List<ForeignKeyMetadata>();
      await md.PrepareForeignKeySearch( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ).EnumerateAsync( async fk => list.Add( await md.ExtractForeignKeyAsync( (SQLDataRow) fk ) ) );
      return list;
   }


}