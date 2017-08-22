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
   /// <summary>
   /// This interface contains all API related to metadata of the database that <see cref="SQLConnection"/> is currently connected to.
   /// </summary>
   /// <remarks>
   /// While the API exposed directly by this interface can be used, in most scenarios, the actual usage happens through extension methods:
   /// <list type="bullet">
   /// <item><description><see cref="E_CBAM.GetSchemaMetadataAsync(SQLConnection, string)"/>,</description></item>
   /// <item><description><see cref="E_CBAM.GetTableMetadataAsync(SQLConnection, string, string, TableType[])"/>,</description></item>
   /// <item><description><see cref="E_CBAM.GetColumnMetadataAsync(SQLConnection, string, string, string)"/>,</description></item>
   /// <item><description><see cref="E_CBAM.GetPrimaryKeyMetadataAsync(SQLConnection, string, string)"/>,</description></item>
   /// <item><description><see cref="E_CBAM.GetExportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>, and</description></item>
   /// <item><description><see cref="E_CBAM.GetImportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>.</description></item>
   /// </list>
   /// </remarks>
   /// <seealso cref="SQLConnection.DatabaseMetadata"/>
   public interface DatabaseMetadata
   {
      /// <summary>
      /// Gets the name of the database that the <see cref="SQLConnection"/> is connected to.
      /// </summary>
      /// <value>The name of the database that the <see cref="SQLConnection"/> is connected to.</value>
      String Name { get; }

      /// <summary>
      /// Creates a new <see cref="SQLStatementBuilder"/> which contains information for executing schema search with given schema name pattern.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the schema information from the database.</returns>
      /// <seealso cref="E_CBAM.GetSchemaMetadataAsync(SQLConnection, string)"/>
      /// <seealso cref="ExtractSchemaMetadataAsync(AsyncDataRow)"/>
      SQLStatementBuilder CreateSchemaSearch( String schemaNamePattern = null );

      /// <summary>
      /// Creates a new <see cref="SQLStatementBuilder"/> which contains information for executing table search with given search parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
      /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
      /// <param name="tableTypes">The table types. If not <c>null</c> and not empty, can be used to further narrow down search results based on table type.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table information from the database.</returns>
      /// <seealso cref="E_CBAM.GetTableMetadataAsync(SQLConnection, string, string, TableType[])"/>
      /// <seealso cref="ExtractTableMetadataAsync(AsyncDataRow)"/>
      /// <seealso cref="TableType"/>
      SQLStatementBuilder CreateTableSearch( String schemaNamePattern, String tableNamePattern, params TableType[] tableTypes );

      /// <summary>
      /// Creates a new <see cref="SQLStatementBuilder"/> which contains information for executing table column search with given search parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
      /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
      /// <param name="columnNamePattern">The column name pattern. If not <c>null</c>, will narrow down search results based on table column name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table column information from the database.</returns>
      /// <seealso cref="E_CBAM.GetColumnMetadataAsync(SQLConnection, string, string, string)"/>
      /// <seealso cref="ExtractColumnMetadataAsync(AsyncDataRow)"/>
      SQLStatementBuilder CreateColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern = null );

      /// <summary>
      /// Creates a new <see cref="SQLStatementBuilder"/> which contains information for executing table primary key search with given search parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
      /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table primary key information from the database.</returns>
      /// <seealso cref="E_CBAM.GetPrimaryKeyMetadataAsync(SQLConnection, string, string)"/>
      /// <seealso cref="ExtractPrimaryKeyMetadataAsync(AsyncDataRow)"/>
      SQLStatementBuilder CreatePrimaryKeySearch( String schemaNamePattern, String tableNamePattern );

      /// <summary>
      /// Creates a new <see cref="SQLStatementBuilder"/> which contains information for executing table foreign key search with given search parameters.
      /// </summary>
      /// <param name="primarySchemaName">The schema name of the table containing primary key. If not <c>null</c>, will narrow down search results based on primary key table schema name.</param>
      /// <param name="primaryTableName">The name of the table containing primary key. If not <c>null</c>, will narrow down search results based on primary key table name.</param>
      /// <param name="foreignSchemaName">The schema name of the table containing foreign key. If not <c>null</c>, will narrow down search results based on foreign key table schema name.</param>
      /// <param name="foreignTableName">The name of the table containing foreign key. If not <c>null</c>, will narrow down search results based on foreign key table name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table foreign key information from the database.</returns>
      /// <seealso cref="E_CBAM.GetImportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
      /// <seealso cref="E_CBAM.GetExportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
      /// <seealso cref="E_CBAM.GetCrossReferenceMetadataAsync(SQLConnection, string, string, string, string)"/>
      /// <seealso cref="ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>
      SQLStatementBuilder CreateForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName );

      /// <summary>
      /// This method will create a new <see cref="SchemaMetadata"/> based on <see cref="AsyncDataRow"/> resulted from executing query produced by <see cref="CreateSchemaSearch(string)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateSchemaSearch(string)"/>.</param>
      /// <returns>A task which will on completion result in <see cref="SchemaMetadata"/> object.</returns>
      /// <remarks>
      /// Using this method on a <see cref="AsyncDataRow"/> which originates from other <see cref="AsyncEnumerator{T}"/> then the one returned by <see cref="CreateSchemaSearch(string)"/> will most likely result in errors.
      /// </remarks>
      /// <exception cref="ArgumentNullException">If <paramref name="row"/> is <c>null</c>.</exception>
      /// <seealso cref="E_CBAM.GetSchemaMetadataAsync(SQLConnection, string)"/>
      /// <seealso cref="CreateSchemaSearch(string)"/>
      ValueTask<SchemaMetadata> ExtractSchemaMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// This method will create a new <see cref="TableMetadata"/> based on <see cref="AsyncDataRow"/> resulted from executing query produced by <see cref="CreateTableSearch(string, string, TableType[])"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateTableSearch(string, string, TableType[])"/>.</param>
      /// <returns>A task which will on completion result in <see cref="TableMetadata"/> object.</returns>
      /// <remarks>
      /// Using this method on a <see cref="AsyncDataRow"/> which originates from other <see cref="AsyncEnumerator{T}"/> then the one returned by <see cref="CreateTableSearch(string, string, TableType[])"/> will most likely result in errors.
      /// </remarks>
      /// <seealso cref="E_CBAM.GetTableMetadataAsync(SQLConnection, string, string, TableType[])"/>
      /// <seealso cref="CreateTableSearch(string, string, TableType[])"/>
      ValueTask<TableMetadata> ExtractTableMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// This method will create a new <see cref="ColumnMetadata"/> based on <see cref="AsyncDataRow"/> resulted from executing query produced by <see cref="CreateColumnSearch(string, string, string)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateColumnSearch(string, string, string)"/>.</param>
      /// <returns>A task which will on completion result in <see cref="ColumnMetadata"/> object.</returns>
      /// <remarks>
      /// Using this method on a <see cref="AsyncDataRow"/> which originates from other <see cref="AsyncEnumerator{T}"/> then the one returned by <see cref="CreateColumnSearch(string, string, string)"/> will most likely result in errors.
      /// </remarks>
      /// <seealso cref="E_CBAM.GetColumnMetadataAsync(SQLConnection, string, string, string)"/>
      /// <seealso cref="CreateColumnSearch(string, string, string)"/>
      ValueTask<ColumnMetadata> ExtractColumnMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// This method will create a new <see cref="PrimaryKeyMetadata"/> based on <see cref="AsyncDataRow"/> resulted from executing query produced by <see cref="CreatePrimaryKeySearch(string, string)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreatePrimaryKeySearch(string, string)"/>.</param>
      /// <returns>A task which will on completion result in <see cref="PrimaryKeyMetadata"/> object.</returns>
      /// <remarks>
      /// Using this method on a <see cref="AsyncDataRow"/> which originates from other <see cref="AsyncEnumerator{T}"/> then the one returned by <see cref="CreatePrimaryKeySearch(string, string)"/> will most likely result in errors.
      /// </remarks>
      /// <seealso cref="E_CBAM.GetPrimaryKeyMetadataAsync(SQLConnection, string, string)"/>
      /// <seealso cref="CreatePrimaryKeySearch(string, string)"/>
      ValueTask<PrimaryKeyMetadata> ExtractPrimaryKeyMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// This method will create a new <see cref="ForeignKeyMetadata"/> based on <see cref="AsyncDataRow"/> resulted from executing query produced by <see cref="CreateForeignKeySearch(string, string, string, string)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateForeignKeySearch(string, string, string, string)"/>.</param>
      /// <returns>A task which will on completion result in <see cref="ForeignKeyMetadata"/> object.</returns>
      /// <remarks>
      /// Using this method on a <see cref="AsyncDataRow"/> which originates from other <see cref="AsyncEnumerator{T}"/> then the one returned by <see cref="CreateForeignKeySearch(string, string, string, string)"/> will most likely result in errors.
      /// </remarks>
      /// <seealso cref="E_CBAM.GetImportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
      /// <seealso cref="E_CBAM.GetExportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
      /// <seealso cref="E_CBAM.GetCrossReferenceMetadataAsync(SQLConnection, string, string, string, string)"/>
      /// <seealso cref="CreateForeignKeySearch(string, string, string, string)"/>
      ValueTask<ForeignKeyMetadata> ExtractForeignKeyMetadataAsync( AsyncDataRow row );
   }

   /// <summary>
   /// This is common interface for database metadata objects which have a schema name.
   /// </summary>
   /// <seealso cref="SchemaMetadata"/>
   /// <seealso cref="TableMetadata"/>
   /// <seealso cref="ColumnMetadata"/>
   /// <seealso cref="PrimaryKeyMetadata"/>
   /// <seealso cref="ForeignKeyMetadata"/>
   public interface DatabaseElementWithSchemaName
   {
      /// <summary>
      /// Gets the name of the schema this database metadata object belongs to.
      /// </summary>
      /// <value>The name of the schema this database metadata object belongs to.</value>
      String SchemaName { get; }
   }

   /// <summary>
   /// This interface represents information about a single schema in the database.
   /// </summary>
   /// <seealso cref="E_CBAM.GetSchemaMetadataAsync(SQLConnection, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractSchemaMetadataAsync(AsyncDataRow)"/>
   public interface SchemaMetadata : DatabaseElementWithSchemaName, DatabaseElementWithComment
   {
   }

   /// <summary>
   /// This is common interface for database metadata objects which have a table name.
   /// </summary>
   /// <seealso cref="TableMetadata"/>
   /// <seealso cref="ColumnMetadata"/>
   /// <seealso cref="PrimaryKeyMetadata"/>
   /// <seealso cref="ForeignKeyMetadata"/>
   public interface DatabaseElementWithTableName : DatabaseElementWithSchemaName
   {
      /// <summary>
      /// Gets the name of the table this database metadata object belongs to.
      /// </summary>
      /// <value>The name of the table this database metadata object belongs to.</value>
      String TableName { get; }
   }

   /// <summary>
   /// This is common interface for database metadata objects which have a comment.
   /// </summary>
   /// <seealso cref="SchemaMetadata"/>
   /// <seealso cref="TableMetadata"/>
   /// <seealso cref="ColumnMetadata"/>
   public interface DatabaseElementWithComment
   {
      /// <summary>
      /// Gets the textual comment associated with this database metadata object.
      /// </summary>
      /// <value>The textual comment associated with this database metadata object.</value>
      String Comment { get; }
   }

   /// <summary>
   /// This is common interface for database metadata objects which have some sort of type name.
   /// </summary>
   /// <seealso cref="TableMetadata"/>
   /// <seealso cref="ColumnMetadata"/>
   public interface DatabaseElementWithTypeName
   {
      /// <summary>
      /// Gets the textual type name of this database metadata object.
      /// </summary>
      /// <value>The textual type name of this database metadata object.</value>
      String TypeName { get; }
   }

   /// <summary>
   /// This interface contains information about a single table in the database.
   /// </summary>
   /// <seealso cref="E_CBAM.GetTableMetadataAsync(SQLConnection, string, string, TableType[])"/>
   /// <seealso cref="DatabaseMetadata.ExtractTableMetadataAsync(AsyncDataRow)"/>
   public interface TableMetadata : DatabaseElementWithTableName, DatabaseElementWithComment, DatabaseElementWithTypeName
   {
      /// <summary>
      /// Gets the type of the table as <see cref="SQL.TableType"/> enumeration.
      /// </summary>
      /// <value>The type of the table as <see cref="SQL.TableType"/> enumeration.</value>
      /// <seealso cref="SQL.TableType"/>
      /// <seealso cref="DatabaseElementWithTypeName.TypeName"/>
      TableType? TableType { get; }
   }

   /// <summary>
   /// This is common interface for database metadata objects which have column name.
   /// </summary>
   public interface DatabaseElementWithColumnName : DatabaseElementWithTableName
   {
      /// <summary>
      /// Gets the name of the column this database metadata object belongs to.
      /// </summary>
      /// <value>The name of the column this database metadata object belongs to.</value>
      String ColumnName { get; }
   }

   /// <summary>
   /// This is common interface for database metadata objects which are contained within some other object (e.g. column in a table) and have ordinal position.
   /// </summary>
   public interface DatabaseElementWithOrdinalPosition
   {
      /// <summary>
      /// Gets the zero-based ordinal position of this database metadata object within parent object.
      /// </summary>
      /// <value>The zero-based ordinal position of this database metadata object within parent object.</value>
      Int32 OrdinalPosition { get; }
   }

   /// <summary>
   /// This interface contains information about single column of a single table in the database.
   /// </summary>
   /// <seealso cref="E_CBAM.GetColumnMetadataAsync(SQLConnection, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractColumnMetadataAsync(AsyncDataRow)"/>
   public interface ColumnMetadata : DatabaseElementWithColumnName, DatabaseElementWithComment, DatabaseElementWithTypeName, DatabaseElementWithOrdinalPosition
   {
      //Int32 ColumnSize { get; }

      /// <summary>
      /// Gets the amount of decimal digits, if applicable.
      /// </summary>
      /// <value>The amount of decimal digits, if applicable.</value>
      Int32? DecimalDigits { get; }

      /// <summary>
      /// Gets the value indicating whether this column accepts <c>NULL</c>s as valid values.
      /// </summary>
      /// <value>The value indicating whether this column accepts <c>NULL</c>s as valid values.</value>
      Boolean Nullable { get; }

      /// <summary>
      /// Gets the column default value, if any.
      /// </summary>
      /// <value>The column default value, if any.</value>
      String ColumnDefaultValue { get; }

      /// <summary>
      /// Gets the column CLR type, if it can be deducted at runtime.
      /// </summary>
      /// <value>The column CLR type, if it can be deducted at runtime.</value>
      Type ColumnCLRType { get; }

      /// <summary>
      /// Gets the value indicating whether this column is primary key column.
      /// </summary>
      /// <value>The value indicating whether this column is primary key column.</value>
      Boolean IsPrimaryKeyColumn { get; }
   }

   /// <summary>
   /// This is common interface for direct and indirect primary and foreign key information in the database.
   /// </summary>
   /// <seealso cref="PrimaryKeyMetadata"/>
   /// <seealso cref="ForeignKeyMetadata"/>
   /// <seealso cref="ForeignKeyMetadata.PrimaryKey"/>
   public interface KeyMetadataInfo : DatabaseElementWithColumnName
   {
      /// <summary>
      /// Gets the name of this primary or foreign key information.
      /// </summary>
      String KeyName { get; }
   }

   /// <summary>
   /// This is common interface for direct primary and foreign key information in the database.
   /// </summary>
   /// <seealso cref="PrimaryKeyMetadata"/>
   /// <seealso cref="ForeignKeyMetadata"/>
   public interface KeyMetadata : KeyMetadataInfo, DatabaseElementWithOrdinalPosition
   {
      /// <summary>
      /// Gets the <see cref="ConstraintCharacteristics"/> of this primary or foreign key information, if it exists.
      /// </summary>
      /// <value>The <see cref="ConstraintCharacteristics"/> of this primary or foreign key information, if it exists.</value>
      ConstraintCharacteristics? Deferrability { get; }
   }

   /// <summary>
   /// This interface contains information about single primary key column of a single table in the database.
   /// </summary>
   /// <seealso cref="E_CBAM.GetPrimaryKeyMetadataAsync(SQLConnection, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractPrimaryKeyMetadataAsync(AsyncDataRow)"/>
   public interface PrimaryKeyMetadata : KeyMetadata
   {
   }

   /// <summary>
   /// This interface contains information about single foreign key column of a single table in the database.
   /// </summary>
   /// <seealso cref="E_CBAM.GetImportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
   /// <seealso cref="E_CBAM.GetExportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
   /// <seealso cref="E_CBAM.GetCrossReferenceMetadataAsync(SQLConnection, string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>
   public interface ForeignKeyMetadata : KeyMetadata
   {
      /// <summary>
      /// Gets the information about the primary key column that this this foreign key column references.
      /// </summary>
      /// <value>The information about the primary key column that this this foreign key column references.</value>
      KeyMetadataInfo PrimaryKey { get; }

      /// <summary>
      /// Gets the <see cref="ReferentialAction"/> of this foreign key constraint for update action, if any.
      /// </summary>
      /// <value>The <see cref="ReferentialAction"/> of this foreign key constraint for update action, if any.</value>
      ReferentialAction? OnUpdate { get; }

      /// <summary>
      /// Gets the <see cref="ReferentialAction"/> of this foreign key constraint for delete action, if any.
      /// </summary>
      /// <value>The <see cref="ReferentialAction"/> of this foreign key constraint for delete action, if any.</value>
      ReferentialAction? OnDelete { get; }
   }

   /// <summary>
   /// This enumeration contains possible values for SQL actions when the target of the foreign key changes (<c>ON UPDATE</c>) or gets deleted (<c>ON DELETE</c>).
   /// </summary>
   public enum ReferentialAction
   {
      /// <summary>
      /// The foreign key will be updated to new value in case of change, and the row will be deleted in case of deletion.
      /// </summary>
      Cascade,

      /// <summary>
      /// The foreign key will be set to <c>NULL</c> value if the target changes or gets deleted.
      /// </summary>
      SetNull,

      /// <summary>
      /// The foreign key will be set to default values if the target changes or gets deleted.
      /// </summary>
      SetDefault,

      /// <summary>
      /// The change or deletion of the target will cause an error in that statement.
      /// </summary>
      Restrict,

      /// <summary>
      /// A lot like <see cref="Restrict"/>, this will cause an error if the target gets changed or deleted, but the check is done only only at the very end of the statement, after triggers and other mechanisms have been processed.
      /// This means that even with deletion or change, the end-state might be acceptable, valid foreign key references.
      /// </summary>
      NoAction
   }

   /// <summary>
   /// This enumeraton contains possible values for SQL constraint (e.g. <c>PRIMARY KEY</c>, <c>FOREIGN KEY</c>, etc) characteristics.
   /// </summary>
   public enum ConstraintCharacteristics
   {
      /// <summary>
      /// The constraint is immediate by default, but may be deferred on demand when needed.
      /// </summary>
      InitiallyImmediate_Deferrable,

      /// <summary>
      /// The constraint is always deferred.
      /// </summary>
      InitiallyDeferred_Deferrable,

      /// <summary>
      /// The constraint is always immediate.
      /// </summary>
      NotDeferrable
   }

   /// <summary>
   /// This enumeration specifies for possible table types in table search of <see cref="E_CBAM.GetTableMetadataAsync(SQLConnection, string, string, TableType[])"/> and <see cref="DatabaseMetadata.CreateTableSearch(string, string, TableType[])"/> methods.
   /// </summary>
   public enum TableType
   {
      /// <summary>
      /// This value represents normal SQL table.
      /// </summary>
      Table,

      /// <summary>
      /// This value represents a SQL view.
      /// </summary>
      View,

      /// <summary>
      /// This value represents a system table, aka catalog.
      /// </summary>
      SystemTable,

      /// <summary>
      /// This value represents global temporary table.
      /// </summary>
      GlobalTemporary,

      /// <summary>
      /// This value represents local temporary table.
      /// </summary>
      LocalTemporary,

      /// <summary>
      /// This value represents a synonym table.
      /// </summary>
      Synonym,

      /// <summary>
      /// This value represents the maximum value of this enumeration, and can be used in other enumerations which 'extend' this one.
      /// </summary>
      MaxValue
   }
}

public static partial class E_CBAM
{
   /// <summary>
   /// Creates a new <see cref="AsyncEnumerator{T}"/> which can be used to execute schema search with given schema name pattern.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <returns>An <see cref="AsyncEnumerator{T}"/> which can be executed to search the schema information from the database.</returns>
   /// <seealso cref="E_CBAM.GetSchemaMetadataAsync(SQLConnection, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractSchemaMetadataAsync(AsyncDataRow)"/>
   public static AsyncEnumerator<SQLStatementExecutionResult, SQLStatementBuilderInformation> PrepareSchemaSearch( this SQLConnection connection, String schemaNamePattern = null )
   {
      return connection.PrepareStatementForExecution( connection.DatabaseMetadata.CreateSchemaSearch( schemaNamePattern ) );
   }

   /// <summary>
   /// Creates a new <see cref="AsyncEnumerator{T}"/> which can be used to execute table search with given search parameters.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
   /// <param name="tableTypes">The table types. If not <c>null</c> and not empty, can be used to further narrow down search results based on table type.</param>
   /// <returns>An <see cref="AsyncEnumerator{T}"/> which can be executed to search the table information from the database.</returns>
   /// <seealso cref="E_CBAM.GetTableMetadataAsync(SQLConnection, string, string, TableType[])"/>
   /// <seealso cref="DatabaseMetadata.ExtractTableMetadataAsync(AsyncDataRow)"/>
   /// <seealso cref="TableType"/>
   public static AsyncEnumerator<SQLStatementExecutionResult, SQLStatementBuilderInformation> PrepareTableSearch( this SQLConnection connection, String schemaNamePattern, String tableNamePattern, params TableType[] tableTypes )
   {
      return connection.PrepareStatementForExecution( connection.DatabaseMetadata.CreateTableSearch( schemaNamePattern, tableNamePattern, tableTypes ) );
   }

   /// <summary>
   /// Creates a new <see cref="AsyncEnumerator{T}"/> which can be used to execute table column search with given search parameters.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
   /// <param name="columnNamePattern">The column name pattern. If not <c>null</c>, will narrow down search results based on table column name.</param>
   /// <returns>An <see cref="AsyncEnumerator{T}"/> which can be executed to search the table column information from the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="E_CBAM.GetColumnMetadataAsync(SQLConnection, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractColumnMetadataAsync(AsyncDataRow)"/>
   public static AsyncEnumerator<SQLStatementExecutionResult, SQLStatementBuilderInformation> PrepareColumnSearch( this SQLConnection connection, String schemaNamePattern, String tableNamePattern, String columnNamePattern = null )
   {
      return connection.PrepareStatementForExecution( connection.DatabaseMetadata.CreateColumnSearch( schemaNamePattern, tableNamePattern, columnNamePattern ) );
   }

   /// <summary>
   /// Creates a new <see cref="AsyncEnumerator{T}"/> which can be used to execute table primary key search with given search parameters.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
   /// <returns>An <see cref="AsyncEnumerator{T}"/> which can be executed to search the table primary key information from the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="E_CBAM.GetPrimaryKeyMetadataAsync(SQLConnection, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractPrimaryKeyMetadataAsync(AsyncDataRow)"/>
   public static AsyncEnumerator<SQLStatementExecutionResult, SQLStatementBuilderInformation> PreparePrimaryKeySearch( this SQLConnection connection, String schemaNamePattern, String tableNamePattern )
   {
      return connection.PrepareStatementForExecution( connection.DatabaseMetadata.CreatePrimaryKeySearch( schemaNamePattern, tableNamePattern ) );
   }

   /// <summary>
   /// Creates a new <see cref="AsyncEnumerator{T}"/> which can be used to execute table foreign key search with given search parameters.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="primarySchemaName">The schema name of the table containing primary key. If not <c>null</c>, will narrow down search results based on primary key table schema name.</param>
   /// <param name="primaryTableName">The name of the table containing primary key. If not <c>null</c>, will narrow down search results based on primary key table name.</param>
   /// <param name="foreignSchemaName">The schema name of the table containing foreign key. If not <c>null</c>, will narrow down search results based on foreign key table schema name.</param>
   /// <param name="foreignTableName">The name of the table containing foreign key. If not <c>null</c>, will narrow down search results based on foreign key table name.</param>
   /// <returns>An <see cref="AsyncEnumerator{T}"/> which can be executed to search the table foreign key information from the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="E_CBAM.GetImportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
   /// <seealso cref="E_CBAM.GetExportedForeignKeyMetadataAsync(SQLConnection, string, string)"/>
   /// <seealso cref="E_CBAM.GetCrossReferenceMetadataAsync(SQLConnection, string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>
   public static AsyncEnumerator<SQLStatementExecutionResult, SQLStatementBuilderInformation> PrepareForeignKeySearch( this SQLConnection connection, String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
   {
      return connection.PrepareStatementForExecution( connection.DatabaseMetadata.CreateForeignKeySearch( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ) );
   }

   /// <summary>
   /// This is shortcut method to enumerate all foreign key columns of given table, and return the column information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaName">The schema name of the table to get foreign keys from.</param>
   /// <param name="tableName">The table name of the table to get foreign keys from.</param>
   /// <returns>Asynchronously returns list of <see cref="ForeignKeyMetadata"/> objects that have information about foreign key columns of the given table.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="GetCrossReferenceMetadataAsync(SQLConnection, string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.CreateForeignKeySearch(string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static ValueTask<List<ForeignKeyMetadata>> GetExportedForeignKeyMetadataAsync( this SQLConnection connection, String schemaName, String tableName )
   {
      return connection.GetCrossReferenceMetadataAsync( schemaName, tableName, null, null );
   }

   /// <summary>
   /// This is shortcut method to enumerate all foreign key columns of other tables that reference primary key of given table, and return the column information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaName">The schema name of the table that other foreign keys reference.</param>
   /// <param name="tableName">The table name of the table that other foreign keys reference.</param>
   /// <returns>Asynchronously returns list of <see cref="ForeignKeyMetadata"/> objects that have information about foreign key columns of other tables that reference given table.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="GetCrossReferenceMetadataAsync(SQLConnection, string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.CreateForeignKeySearch(string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static ValueTask<List<ForeignKeyMetadata>> GetImportedForeignKeyMetadataAsync( this SQLConnection connection, String schemaName, String tableName )
   {
      return connection.GetCrossReferenceMetadataAsync( null, null, schemaName, tableName );
   }

   /// <summary>
   /// This is shortcut method to enumerate all schemas, with given optional schema name filter, in the database, and return the schema information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <returns>Asynchronously returns list of <see cref="SchemaMetadata"/> objects that have information about schema in the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="DatabaseMetadata.CreateSchemaSearch(string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractSchemaMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static async ValueTask<List<SchemaMetadata>> GetSchemaMetadataAsync( this SQLConnection connection, String schemaNamePattern = null )
   {
      var md = connection.DatabaseMetadata;
      var list = new List<SchemaMetadata>();
      await connection.PrepareSchemaSearch( schemaNamePattern ).EnumerateAsync( async schema => list.Add( await md.ExtractSchemaMetadataAsync( (SQLDataRow) schema ) ) );
      return list;
   }

   /// <summary>
   /// This is shortcut method to enumerate all tables, with given optional schema name, table name, and table type filters, in the database, and return the table information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
   /// <param name="tableTypes">The table types. If not <c>null</c> and not empty, can be used to further narrow down search results based on table type.</param>
   /// <returns>Asynchronously returns list of <see cref="TableMetadata"/> objects that have information about table in the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="DatabaseMetadata.CreateTableSearch(string, string, TableType[])"/>
   /// <seealso cref="DatabaseMetadata.ExtractTableMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static async ValueTask<List<TableMetadata>> GetTableMetadataAsync( this SQLConnection connection, String schemaNamePattern, String tableNamePattern, params TableType[] tableTypes )
   {
      var md = connection.DatabaseMetadata;
      var list = new List<TableMetadata>();
      await connection.PrepareTableSearch( schemaNamePattern, tableNamePattern ).EnumerateAsync( async table => list.Add( await md.ExtractTableMetadataAsync( (SQLDataRow) table ) ) );
      return list;
   }

   /// <summary>
   /// This is shortcut method to enumerate all columns, with given optional schema, table, and column name filters, in the database, and return the column information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
   /// <param name="columnNamePattern">The column name pattern. If not <c>null</c>, will narrow down search results based on column name.</param>
   /// <returns>Asynchronously returns list of <see cref="ColumnMetadata"/> objects that have information about column in the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="DatabaseMetadata.CreateColumnSearch(string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractColumnMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static async ValueTask<List<ColumnMetadata>> GetColumnMetadataAsync( this SQLConnection connection, String schemaNamePattern, String tableNamePattern, String columnNamePattern = null )
   {
      var md = connection.DatabaseMetadata;
      var list = new List<ColumnMetadata>();
      await connection.PrepareColumnSearch( schemaNamePattern, tableNamePattern, columnNamePattern ).EnumerateAsync( async column => list.Add( await md.ExtractColumnMetadataAsync( (SQLDataRow) column ) ) );
      return list;
   }

   /// <summary>
   /// This is shortcut method to enumerate all primary key columns, with given optional schema and table name filters, in the database, and return the primary key column information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c>, will narrow down search results based on schema name.</param>
   /// <param name="tableNamePattern">The table name pattern. If not <c>null</c>, will narrow down search results based on table name.</param>
   /// <returns>Asynchronously returns list of <see cref="PrimaryKeyMetadata"/> objects that have information about primary key column in the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="DatabaseMetadata.CreatePrimaryKeySearch(string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractPrimaryKeyMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static async ValueTask<List<PrimaryKeyMetadata>> GetPrimaryKeyMetadataAsync( this SQLConnection connection, String schemaNamePattern, String tableNamePattern )
   {
      var md = connection.DatabaseMetadata;
      var list = new List<PrimaryKeyMetadata>();
      await connection.PreparePrimaryKeySearch( schemaNamePattern, tableNamePattern ).EnumerateAsync( async pk => list.Add( await md.ExtractPrimaryKeyMetadataAsync( (SQLDataRow) pk ) ) );
      return list;
   }

   /// <summary>
   /// This is shortcut method to enumerate all foreign key columns, with given primary and foreign table schema name and table name filters, in the database, and return the foreign key column information in a list.
   /// </summary>
   /// <param name="connection">This <see cref="SQLConnection"/>.</param>
   /// <param name="primarySchemaName">The primary table schema name. If not <c>null</c>, will narrow down search results based on primary table's schema name.</param>
   /// <param name="primaryTableName">The primary table table name. If not <c>null</c>, will narrow down search results based on primary table's table name.</param>
   /// <param name="foreignSchemaName">The foreign table schema name. If not <c>null</c>, will narrow down search results based on foreign table's schema name.</param>
   /// <param name="foreignTableName">The foreign table table name. If not <c>null</c>, will narrow down search results based on foreign table's table name.</param>
   /// <returns>Asynchronously returns list of <see cref="ForeignKeyMetadata"/> objects that have information about foreign key column in the database.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLConnection"/> is <c>null</c>.</exception>
   /// <seealso cref="DatabaseMetadata.CreateForeignKeySearch(string, string, string, string)"/>
   /// <seealso cref="DatabaseMetadata.ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>
   /// <remarks>
   /// Since this method stores all results in a single <see cref="List{T}"/>, use this when it is not expected to return a very large sets of data.
   /// </remarks>
   public static async ValueTask<List<ForeignKeyMetadata>> GetCrossReferenceMetadataAsync( this SQLConnection connection, String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
   {
      var md = connection.DatabaseMetadata;
      var list = new List<ForeignKeyMetadata>();
      await connection.PrepareForeignKeySearch( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ).EnumerateAsync( async fk => list.Add( await md.ExtractForeignKeyMetadataAsync( (SQLDataRow) fk ) ) );
      return list;
   }


}