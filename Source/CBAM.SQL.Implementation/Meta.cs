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

using CBAM.Abstractions;
using CBAMA::CBAM.Abstractions;
using CBAM.SQL;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.AsyncEnumeration;
using UtilPack.TabularData;

namespace CBAM.SQL.Implementation
{
   /// <summary>
   /// This class provides default implementation for <see cref="DatabaseMetadata"/> interface, using the same facaded actual connection implementation as <see cref="Abstractions.Implementation.ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality}"/>.
   /// </summary>
   public abstract class DatabaseMetadataImpl : DatabaseMetadata
   {

      /// <summary>
      /// Initializes a new instance of <see cref="DatabaseMetadataImpl"/> with given <see cref="SQLConnectionVendorFunctionality"/> and database name.
      /// </summary>
      /// <param name="vendorFunctionality">The <see cref="SQLConnectionVendorFunctionality"/> to use when creating <see cref="SQLStatementBuilder"/>s.</param>
      /// <param name="name">The name of the database.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="vendorFunctionality"/> is <c>null</c>.</exception>
      public DatabaseMetadataImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         String name
         )
      {
         this.VendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
         this.Name = name;
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.Name"/> and gets the name of the database this connection is connected to.
      /// </summary>
      /// <value>The name of the database this connection is connected to.</value>
      public String Name { get; }

      /// <summary>
      /// Helper property to get the <see cref="SQLConnectionVendorFunctionality"/> of this <see cref="DatabaseMetadataImpl"/>.
      /// </summary>
      /// <value>The <see cref="SQLConnectionVendorFunctionality"/> of this <see cref="DatabaseMetadataImpl"/>.</value>
      protected SQLConnectionVendorFunctionality VendorFunctionality { get; }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.CreateSchemaSearch(string)"/> method by calling <see cref="CreateSQLForSchemaSearch(string)"/> and populating the parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c> or empty, will narrow down search results based on schema name.</param>
      /// <returns>An <see cref="AsyncEnumerator{T}"/> which can be executed to search the schema information from the database.</returns>
      public SQLStatementBuilder CreateSchemaSearch( String schemaNamePattern )
      {
         return this.SetSubsequentNonNullPatterns(
            this.VendorFunctionality.CreateStatementBuilder( this.CreateSQLForSchemaSearch( schemaNamePattern ) ),
            schemaNamePattern
            );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.CreateTableSearch(string, string, TableType[])"/> method by calling <see cref="CreateSQLForTableSearch(string, string, TableType[])"/> and populating the parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c> or empty, will narrow down search results based on schema name.</param>
      /// <param name="tableNamePattern">The table name pattern. If not <c>null</c> or empty, will narrow down search results based on table name.</param>
      /// <param name="tableTypes">The table types. If not <c>null</c> and not empty, can be used to further narrow down search results based on table type.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table information from the database.</returns>
      public SQLStatementBuilder CreateTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes )
      {
         var retVal = this.VendorFunctionality.CreateStatementBuilder( this.CreateSQLForTableSearch( schemaNamePattern, tableNamePattern, tableTypes ) );
         var idx = 0;
         this.SetSubsequentNonNullPatterns( retVal, ref idx, schemaNamePattern, tableNamePattern );
         foreach ( var tType in tableTypes )
         {
            (var obj, var type) = this.GetParameterInfoForTableType( tType );
            retVal.SetParameterObjectWithType( idx++, obj, type );
         }
         return retVal;
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.CreateColumnSearch(string, string, string)"/> method by calling <see cref="CreateSQLForColumnSearch(string, string, string)"/> and populating the parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c> or empty, will narrow down search results based on schema name.</param>
      /// <param name="tableNamePattern">The table name pattern. If not <c>null</c> or empty, will narrow down search results based on table name.</param>
      /// <param name="columnNamePattern">The column name pattern. If not <c>null</c> or empty, will narrow down search results based on table column name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table column information from the database.</returns>
      public SQLStatementBuilder CreateColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern )
      {
         return this.SetSubsequentNonNullPatterns(
            this.VendorFunctionality.CreateStatementBuilder( this.CreateSQLForColumnSearch( schemaNamePattern, tableNamePattern, columnNamePattern ) ),
            schemaNamePattern,
            tableNamePattern,
            columnNamePattern
            );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.CreatePrimaryKeySearch(string, string)"/> method by calling <see cref="CreateSQLForPrimaryKeySearch(string, string)"/> and populating the parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern. If not <c>null</c> or empty, will narrow down search results based on schema name.</param>
      /// <param name="tableNamePattern">The table name pattern. If not <c>null</c> or empty, will narrow down search results based on table name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table primary key information from the database.</returns>
      public SQLStatementBuilder CreatePrimaryKeySearch( String schemaNamePattern, String tableNamePattern )
      {
         return this.SetSubsequentNonNullPatterns(
            this.VendorFunctionality.CreateStatementBuilder( this.CreateSQLForPrimaryKeySearch( schemaNamePattern, tableNamePattern ) ),
            schemaNamePattern,
            tableNamePattern
            );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.CreateForeignKeySearch(string, string, string, string)"/> method by calling <see cref="CreateSQLForForeignKeySearch(string, string, string, string)"/> and populating the parameters.
      /// </summary>
      /// <param name="primarySchemaName">The schema name of the table containing primary key. If not <c>null</c> or empty, will narrow down search results based on primary key table schema name.</param>
      /// <param name="primaryTableName">The name of the table containing primary key. If not <c>null</c> or empty, will narrow down search results based on primary key table name.</param>
      /// <param name="foreignSchemaName">The schema name of the table containing foreign key. If not <c>null</c> or empty, will narrow down search results based on foreign key table schema name.</param>
      /// <param name="foreignTableName">The name of the table containing foreign key. If not <c>null</c> or empty, will narrow down search results based on foreign key table name.</param>
      /// <returns>An <see cref="SQLStatementBuilder"/> which can be used to search the table foreign key information from the database.</returns>
      public SQLStatementBuilder CreateForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
      {
         return this.SetSubsequentNonNullPatterns(
            this.VendorFunctionality.CreateStatementBuilder( this.CreateSQLForForeignKeySearch( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ) ),
            primarySchemaName,
            primaryTableName,
            foreignSchemaName,
            foreignTableName
            );
      }

      /// <summary>
      /// Helper method to set all non-<c>null</c> strings from given array of strings as parameters to given <see cref="SQLStatementBuilder"/>.
      /// </summary>
      /// <param name="stmt">The <see cref="SQLStatementBuilder"/>.</param>
      /// <param name="values">The string array to set parameters from.</param>
      /// <returns>The <paramref name="stmt"/>.</returns>
      /// <exception cref="NullReferenceException">If either of <paramref name="stmt"/> or <paramref name="values"/> is <c>null</c>.</exception>
      protected SQLStatementBuilder SetSubsequentNonNullPatterns( SQLStatementBuilder stmt, params String[] values )
      {
         var idx = 0;
         this.SetSubsequentNonNullPatterns( stmt, ref idx, values );
         return stmt;
      }

      /// <summary>
      /// Helper method to set all non-<c>null</c> strings from given array of strings as parameters to given <see cref="SQLStatementBuilder"/>, and to keep track of the parameter index.
      /// </summary>
      /// <param name="stmt">The <see cref="SQLStatementBuilder"/>.</param>
      /// <param name="idx">The index where to start setting parameters in given <paramref name="stmt"/>.</param>
      /// <param name="values">The string array to set parameters from.</param>
      /// <exception cref="NullReferenceException">If either of <paramref name="stmt"/> or <paramref name="values"/> is <c>null</c>.</exception>
      protected void SetSubsequentNonNullPatterns( SQLStatementBuilder stmt, ref Int32 idx, params String[] values )
      {
         foreach ( var str in values )
         {
            if ( str != null )
            {
               (var obj, var type) = this.GetParameterInfoForPattern( str );
               stmt.SetParameterObjectWithType( idx++, obj, type );
            }
         }
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.ExtractSchemaMetadataAsync"/> by checking that <paramref name="row"/> is not <c>null</c> and then delegating creation to <see cref="DoExtractSchemaMetadataAsync(AsyncDataRow)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateSchemaSearch"/>.</param>
      /// <returns>Result of <see cref="DoExtractSchemaMetadataAsync"/> method.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="row"/> is <c>null</c>.</exception>
      public ValueTask<SchemaMetadata> ExtractSchemaMetadataAsync( AsyncDataRow row )
      {
         return this.DoExtractSchemaMetadataAsync( ArgumentValidator.ValidateNotNull( nameof( row ), row ) );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.ExtractTableMetadataAsync"/> by checking that <paramref name="row"/> is not <c>null</c> and then delegating creation to <see cref="DoExtractSchemaMetadataAsync(AsyncDataRow)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateTableSearch"/>.</param>
      /// <returns>Result of <see cref="DoExtractTableMetadataAsync"/> method.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="row"/> is <c>null</c>.</exception>
      public ValueTask<TableMetadata> ExtractTableMetadataAsync( AsyncDataRow row )
      {
         return this.DoExtractTableMetadataAsync( ArgumentValidator.ValidateNotNull( nameof( row ), row ) );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.ExtractColumnMetadataAsync"/> by checking that <paramref name="row"/> is not <c>null</c> and then delegating creation to <see cref="DoExtractSchemaMetadataAsync(AsyncDataRow)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateColumnSearch"/>.</param>
      /// <returns>Result of <see cref="DoExtractColumnMetadataAsync"/> method.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="row"/> is <c>null</c>.</exception>
      public ValueTask<ColumnMetadata> ExtractColumnMetadataAsync( AsyncDataRow row )
      {
         return this.DoExtractColumnMetadataAsync( ArgumentValidator.ValidateNotNull( nameof( row ), row ) );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.ExtractPrimaryKeyMetadataAsync"/> by checking that <paramref name="row"/> is not <c>null</c> and then delegating creation to <see cref="DoExtractSchemaMetadataAsync(AsyncDataRow)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreatePrimaryKeySearch"/>.</param>
      /// <returns>Result of <see cref="DoExtractPrimaryKeyMetadataAsync"/> method.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="row"/> is <c>null</c>.</exception>
      public ValueTask<PrimaryKeyMetadata> ExtractPrimaryKeyMetadataAsync( AsyncDataRow row )
      {
         return this.DoExtractPrimaryKeyMetadataAsync( ArgumentValidator.ValidateNotNull( nameof( row ), row ) );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadata.ExtractForeignKeyMetadataAsync"/> by checking that <paramref name="row"/> is not <c>null</c> and then delegating creation to <see cref="DoExtractSchemaMetadataAsync(AsyncDataRow)"/>.
      /// </summary>
      /// <param name="row">The data row encountered during processing query produced by <see cref="CreateForeignKeySearch"/>.</param>
      /// <returns>Result of <see cref="DoExtractForeignKeyMetadataAsync"/> method.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="row"/> is <c>null</c>.</exception>
      public ValueTask<ForeignKeyMetadata> ExtractForeignKeyMetadataAsync( AsyncDataRow row )
      {
         return this.DoExtractForeignKeyMetadataAsync( ArgumentValidator.ValidateNotNull( nameof( row ), row ) );
      }

      /// <summary>
      /// Derived classes should implement this method to create new instance of <see cref="SchemaMetadata"/> from <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateSchemaSearch(string)"/>.
      /// When called by <see cref="ExtractSchemaMetadataAsync(AsyncDataRow)"/>, the <paramref name="row"/> is never <c>null</c>.
      /// </summary>
      /// <param name="row">The <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateSchemaSearch(string)"/>.</param>
      /// <returns>Possibly asynchronously returns a new instance of <see cref="SchemaMetadata"/> with information based on <paramref name="row"/>.</returns>
      protected abstract ValueTask<SchemaMetadata> DoExtractSchemaMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// Derived classes should implement this method to create new instance of <see cref="TableMetadata"/> from <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateTableSearch"/>.
      /// When called by <see cref="ExtractTableMetadataAsync(AsyncDataRow)"/>, the <paramref name="row"/> is never <c>null</c>.
      /// </summary>
      /// <param name="row">The <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateTableSearch"/>.</param>
      /// <returns>Possibly asynchronously returns a new instance of <see cref="TableMetadata"/> with information based on <paramref name="row"/>.</returns>
      protected abstract ValueTask<TableMetadata> DoExtractTableMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// Derived classes should implement this method to create new instance of <see cref="ColumnMetadata"/> from <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateColumnSearch"/>.
      /// When called by <see cref="ExtractColumnMetadataAsync(AsyncDataRow)"/>, the <paramref name="row"/> is never <c>null</c>.
      /// </summary>
      /// <param name="row">The <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateColumnSearch"/>.</param>
      /// <returns>Possibly asynchronously returns a new instance of <see cref="ColumnMetadata"/> with information based on <paramref name="row"/>.</returns>
      protected abstract ValueTask<ColumnMetadata> DoExtractColumnMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// Derived classes should implement this method to create new instance of <see cref="PrimaryKeyMetadata"/> from <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreatePrimaryKeySearch"/>.
      /// When called by <see cref="ExtractPrimaryKeyMetadataAsync(AsyncDataRow)"/>, the <paramref name="row"/> is never <c>null</c>.
      /// </summary>
      /// <param name="row">The <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreatePrimaryKeySearch"/>.</param>
      /// <returns>Possibly asynchronously returns a new instance of <see cref="PrimaryKeyMetadata"/> with information based on <paramref name="row"/>.</returns>
      protected abstract ValueTask<PrimaryKeyMetadata> DoExtractPrimaryKeyMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// Derived classes should implement this method to create new instance of <see cref="ForeignKeyMetadata"/> from <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateForeignKeySearch"/>.
      /// When called by <see cref="ExtractForeignKeyMetadataAsync(AsyncDataRow)"/>, the <paramref name="row"/> is never <c>null</c>.
      /// </summary>
      /// <param name="row">The <see cref="AsyncDataRow"/> originating from query produced by <see cref="CreateForeignKeySearch"/>.</param>
      /// <returns>Possibly asynchronously returns a new instance of <see cref="ForeignKeyMetadata"/> with information based on <paramref name="row"/>.</returns>
      protected abstract ValueTask<ForeignKeyMetadata> DoExtractForeignKeyMetadataAsync( AsyncDataRow row );

      /// <summary>
      /// Derived classes should implement this method to create textual SQL statement string for schema search with given parameters.
      /// Returned SQL statement should have legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order the strings appear in this method signature.
      /// </summary>
      /// <param name="schemaNamePattern">The pattern for schema name.</param>
      /// <returns>SQL statement with legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order strings appear in this method signature.</returns>
      protected abstract String CreateSQLForSchemaSearch( String schemaNamePattern );

      /// <summary>
      /// Derived classes should implement this method to create textual SQL statement string for table search with given parameters.
      /// Returned SQL statement should have legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order the strings appear in this method signature.
      /// Additionally, the parameter characters for each value in <paramref name="tableTypes"/> should be present after string parameters.
      /// </summary>
      /// <param name="schemaNamePattern">The pattern for schema name.</param>
      /// <param name="tableNamePattern">The pattern for table name.</param>
      /// <param name="tableTypes">The array of <see cref="TableType"/> enumerations describing the type of the table.</param>
      /// <returns>SQL statement with legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order strings appear in this method signature, and for every value in <paramref name="tableTypes"/> array.</returns>
      protected abstract String CreateSQLForTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes );

      /// <summary>
      /// Derived classes should implement this method to create textual SQL statement string for column search with given parameters.
      /// Returned SQL statement should have legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order the strings appear in this method signature.
      /// </summary>
      /// <param name="schemaNamePattern">The pattern for schema name.</param>
      /// <param name="tableNamePattern">The pattern for table name.</param>
      /// <param name="columnNamePattern">The pattern for column name.</param>
      /// <returns>SQL statement with legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order strings appear in this method signature.</returns>
      protected abstract String CreateSQLForColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern );

      /// <summary>
      /// Derived classes should implement this method to create textual SQL statement string for column search with given parameters.
      /// Returned SQL statement should have legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order the strings appear in this method signature.
      /// </summary>
      /// <param name="schemaNamePattern">The pattern for schema name.</param>
      /// <param name="tableNamePattern">The pattern for table name.</param>
      /// <returns>SQL statement with legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order strings appear in this method signature.</returns>
      protected abstract String CreateSQLForPrimaryKeySearch( String schemaNamePattern, String tableNamePattern );

      /// <summary>
      /// Derived classes should implement this method to create textual SQL statement string for column search with given parameters.
      /// Returned SQL statement should have legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order the strings appear in this method signature.
      /// </summary>
      /// <param name="primarySchemaName">The pattern for schema name of the table holding primary key.</param>
      /// <param name="primaryTableName">The pattern for table name of the table holding primary key.</param>
      /// <param name="foreignSchemaName">The pattern for schema name of the table holding foreign key.</param>
      /// <param name="foreignTableName">The pattern for table name of the table holding foreign key.</param>
      /// <returns>SQL statement with legal parameter character (<c>?</c>) for every non-<c>null</c> string given to this method, in the order strings appear in this method signature.</returns>
      protected abstract String CreateSQLForForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName );

      /// <summary>
      /// Derived classes should implement this method to get parameter information for <see cref="SQLStatementBuilder"/> from a single <see cref="TableType"/>.
      /// </summary>
      /// <param name="tableType">The <see cref="TableType"/>.</param>
      /// <returns>A tuple of value and type of the value for <paramref name="tableType"/>. If value is not <c>null</c>, the type may be <c>null</c>.</returns>
      protected abstract (Object, Type) GetParameterInfoForTableType( TableType tableType );

      /// <summary>
      /// Provides default overridable implementation for getting parameter information for <see cref="SQLStatementBuilder"/> from name pattern.
      /// </summary>
      /// <param name="pattern">The name pattern.</param>
      /// <returns>A tuple of value and type of the value for <paramref name="pattern"/>. If value is not <c>null</c>, the type may be <c>null</c>.</returns>
      protected virtual (Object, Type) GetParameterInfoForPattern( String pattern )
      {
         return (pattern, null);
      }
   }

   /// <summary>
   /// This interface provides API for getting <see cref="OrdinalSQLCache"/>s for SQL statements used by <see cref="DatabaseMetadataImpl"/>.
   /// </summary>
   /// <seealso cref="OrdinalSQLCache"/>
   /// <seealso cref="OrdinalSQLCache{T}"/>
   /// <seealso cref="SQLCachingDatabaseMetadataImpl"/>
   public interface DatabaseMetadataSQLCache
   {
      /// <summary>
      /// Gets the <see cref="OrdinalSQLCache"/> used for schema searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for schema searches.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForSchemaSearch(string)"/>
      OrdinalSQLCache SchemaSearchCache { get; }

      /// <summary>
      /// Gets the <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array is missing, is empty, or contains only one <see cref="TableType"/>.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array is missing, is empty, or contains only one <see cref="TableType"/>.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForTableSearch"/>
      OrdinalSQLCache<TableType?> TableSearchCache { get; }

      /// <summary>
      /// Gets the <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array has at least two elements.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array has at least two elements.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForTableSearch"/>
      OrdinalSQLCache<TableType[]> TableSearchCacheForMultipleTableTypes { get; }

      /// <summary>
      /// Gets the <see cref="OrdinalSQLCache"/> used for column searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for column searches.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForColumnSearch"/>
      OrdinalSQLCache ColumnSearchCache { get; }

      /// <summary>
      /// Gets the <see cref="OrdinalSQLCache"/> used for primary key searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for primary key searches.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForPrimaryKeySearch"/>
      OrdinalSQLCache PrimaryKeySearchCache { get; }

      /// <summary>
      /// Gets the <see cref="OrdinalSQLCache"/> used for foreign key searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for foreign key searches.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForForeignKeySearch"/>
      OrdinalSQLCache ForeignKeySearchCache { get; }
   }

   /// <summary>
   /// This class provides default implementation for <see cref="DatabaseMetadataSQLCache"/> by using <see cref="DefaultOrdinalSQLCache"/> classes as actual caches.
   /// </summary>
   public class DefaultDatabaseMetadataSQLCache : DatabaseMetadataSQLCache
   {
      /// <summary>
      /// Creates new instance of <see cref="DefaultDatabaseMetadataSQLCache"/> with given parameters.
      /// </summary>
      /// <param name="schemaSearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="SchemaSearchCache"/>.</param>
      /// <param name="tableSearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache{T}"/> acting as <see cref="TableSearchCache"/>.</param>
      /// <param name="tableSearchForMultipleTableTypesSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache{T}"/> acting as <see cref="TableSearchCacheForMultipleTableTypes"/>.</param>
      /// <param name="columnSearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="ColumnSearchCache"/>.</param>
      /// <param name="primaryKeySearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="PrimaryKeySearchCache"/>.</param>
      /// <param name="foreignKeySearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="ForeignKeySearchCache"/>.</param>
      /// <exception cref="ArgumentNullException">If any of <paramref name="schemaSearchSQLFactory"/>, <paramref name="tableSearchSQLFactory"/>, <paramref name="tableSearchForMultipleTableTypesSQLFactory"/>, <paramref name="columnSearchSQLFactory"/>, <paramref name="primaryKeySearchSQLFactory"/>, or <paramref name="foreignKeySearchSQLFactory"/> is <c>null</c>.</exception>
      /// <remarks>
      /// This constructor will create the <see cref="DefaultOrdinalSQLCache"/> instances with enough room to cover needed permutation counts for each method.
      /// </remarks>
      public DefaultDatabaseMetadataSQLCache(
         Func<Int32, String> schemaSearchSQLFactory, // 0 - (schemaNamePattern Missing), 1 - (schemaNamePattern Present)
         Func<Int32, TableType?, String> tableSearchSQLFactory, // 0 - (schemaNamePattern M, tableNamePattern M, tableTypes M), 1 - (schemaNamePattern M, tableNamePattern M, tableTypes P), 2 - (schemaNamePattern M, tableNamePattern P, tableTypes M), 3 - (schemaNamePattern M, tableNamePattern P, tableTypes P)
         Func<Int32, TableType[], (String, Boolean)> tableSearchForMultipleTableTypesSQLFactory,
         Func<Int32, String> columnSearchSQLFactory,
         Func<Int32, String> primaryKeySearchSQLFactory,
         Func<Int32, String> foreignKeySearchSQLFactory
         )
      {
         this.SchemaSearchCache = new DefaultOrdinalSQLCache( 1 << 1, schemaSearchSQLFactory );
         this.TableSearchCache = new DefaultOrdinalSQLCache<TableType?>( 1 << 3, tableSearchSQLFactory );
         this.TableSearchCacheForMultipleTableTypes = new DefaultOrdinalSQLCache<TableType[]>( 1 << 2, tableSearchForMultipleTableTypesSQLFactory );
         this.ColumnSearchCache = new DefaultOrdinalSQLCache( 1 << 3, columnSearchSQLFactory );
         this.PrimaryKeySearchCache = new DefaultOrdinalSQLCache( 1 << 2, primaryKeySearchSQLFactory );
         this.ForeignKeySearchCache = new DefaultOrdinalSQLCache( 1 << 4, foreignKeySearchSQLFactory );
      }

      /// <summary>
      /// Implements <see cref="DatabaseMetadataSQLCache.SchemaSearchCache"/> and gets the <see cref="OrdinalSQLCache"/> used for schema searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for schema searches.</value>
      public OrdinalSQLCache SchemaSearchCache { get; }

      /// <summary>
      /// Implements <see cref="DatabaseMetadataSQLCache.TableSearchCache"/> and gets the <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array is missing, is empty, or contains only one <see cref="TableType"/>.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array is missing, is empty, or contains only one <see cref="TableType"/>.</value>
      public OrdinalSQLCache<TableType?> TableSearchCache { get; }

      /// <summary>
      /// Implements <see cref="DatabaseMetadataSQLCache.TableSearchCacheForMultipleTableTypes"/> and gets the <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array has at least two elements.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache{T}"/> used for table searches, when the <see cref="TableType"/> array has at least two elements.</value>
      public OrdinalSQLCache<TableType[]> TableSearchCacheForMultipleTableTypes { get; }

      /// <summary>
      /// Implements <see cref="DatabaseMetadataSQLCache.ColumnSearchCache"/> and gets the <see cref="OrdinalSQLCache"/> used for column searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for column searches.</value>
      public OrdinalSQLCache ColumnSearchCache { get; }

      /// <summary>
      /// Implements <see cref="DatabaseMetadataSQLCache.PrimaryKeySearchCache"/> and gets the <see cref="OrdinalSQLCache"/> used for primary key searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for primary key searches.</value>
      public OrdinalSQLCache PrimaryKeySearchCache { get; }

      /// <summary>
      /// Implements <see cref="DatabaseMetadataSQLCache.ForeignKeySearchCache"/> and gets the <see cref="OrdinalSQLCache"/> used for foreign key searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for foreign key searches.</value>
      public OrdinalSQLCache ForeignKeySearchCache { get; }
   }

   /// <summary>
   /// Implements caching SQL statements based on ordinal number, which is binary sequence of null and non-null parameters of the original method.
   /// </summary>
   /// <remarks>
   /// <para>The "original method" here is one of the following:
   /// <list type="bullet">
   /// <item><description><see cref="DatabaseMetadata.CreateSchemaSearch"/>,</description></item>
   /// <item><description><see cref="DatabaseMetadata.CreateTableSearch"/>,</description></item>
   /// <item><description><see cref="DatabaseMetadata.CreateColumnSearch"/>,</description></item>
   /// <item><description><see cref="DatabaseMetadata.CreatePrimaryKeySearch"/>, or</description></item>
   /// <item><description><see cref="DatabaseMetadata.CreateForeignKeySearch"/>.</description></item>
   /// </list>
   /// </para>
   /// <para>
   /// The ordinal number is generated so that it covers all permutations of method parameters being <c>null</c> or not being <c>null</c>.
   /// For example, the <see cref="DatabaseMetadata.CreateSchemaSearch"/> method has one parameter, so nullability of it can be represented using 1 bit.
   /// This bit is <c>0</c> when the parameter is <c>null</c>, and <c>1</c> when parameter is not <c>null</c>.
   /// Therefore, the possible permutation order numbers are <c>0</c> and <c>1</c>.
   /// </para>
   /// <para>
   /// A little more complex example: <see cref="DatabaseMetadata.CreateColumnSearch"/>, which has three parameters, so nullability of them can be represented using 3 bits.
   /// <list type="table">
   /// 
   /// <listheader>
   /// <term>First parameter</term>
   /// <term>Second parameter</term>
   /// <term>Third parameter</term>
   /// <term>Bits</term>
   /// <term>Bits as binary number (argument for <see cref="GetSQL(int)"/> method)</term>
   /// </listheader>
   /// 
   /// <item>
   /// <term><c>null</c></term>
   /// <term><c>null</c></term>
   /// <term><c>null</c></term>
   /// <term>000</term>
   /// <term>0</term>
   /// </item>
   /// 
   /// <item>
   /// <term><c>null</c></term>
   /// <term><c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term>001</term>
   /// <term>1</term>
   /// </item>
   /// 
   /// <item>
   /// <term><c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term><c>null</c></term>
   /// <term>010</term>
   /// <term>2</term>
   /// </item>
   /// 
   /// <item>
   /// <term><c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term>011</term>
   /// <term>3</term>
   /// </item>
   /// 
   /// <item>
   /// <term>not <c>null</c></term>
   /// <term><c>null</c></term>
   /// <term><c>null</c></term>
   /// <term>100</term>
   /// <term>4</term>
   /// </item>
   /// 
   /// <item>
   /// <term>not <c>null</c></term>
   /// <term><c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term>101</term>
   /// <term>5</term>
   /// </item>
   /// 
   /// <item>
   /// <term>not <c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term><c>null</c></term>
   /// <term>110</term>
   /// <term>6</term>
   /// </item>
   /// 
   /// <item>
   /// <term>not <c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term>not <c>null</c></term>
   /// <term>111</term>
   /// <term>7</term>
   /// </item>
   /// 
   /// </list>
   /// A number with 3 bits can have 8 different values, so the permut
   /// </para>
   /// </remarks>
   public interface OrdinalSQLCache
   {
      /// <summary>
      /// Gets the SQL statement string based on permutation ordinal number calculated from which parameters to original method are <c>null</c>s.
      /// See the remarks of this interface to learn more.
      /// </summary>
      /// <param name="permutationOrdinalNumber">The ordinal number of null parameter permutation sequence. See the remarks of this interface to learn more.</param>
      /// <returns>Cached or created SQL string.</returns>
      String GetSQL( Int32 permutationOrdinalNumber );
   }

   /// <summary>
   /// This interface is like <see cref="OrdinalSQLCache"/>, except it requires extra parameter of type <typeparamref name="T"/> when getting SQL statement string.
   /// </summary>
   /// <typeparam name="T">The type of parameter for <see cref="GetSQL"/> method.</typeparam>
   /// <remarks>
   /// See remarks of <see cref="OrdinalSQLCache"/> to learn more about how permutation ordinal number is calculated.
   /// </remarks>
   public interface OrdinalSQLCache<in T>
   {
      /// <summary>
      /// Gets the SQL statement string based on permutation ordinal number calculated from which parameters to original method are <c>null</c>s.
      /// See the remarks of <see cref="OrdinalSQLCache"/> to learn more.
      /// </summary>
      /// <param name="permutationOrdinalNumber">The ordinal number of null parameter permutation sequence. See the remarks of <see cref="OrdinalSQLCache"/> to learn more.</param>
      /// <param name="parameter">The custom parameter to be possibly used by factory.</param>
      /// <returns>Cached or created SQL string.</returns>
      String GetSQL( Int32 permutationOrdinalNumber, T parameter );
   }

   /// <summary>
   /// Provides default implementation for <see cref="OrdinalSQLCache"/>, using array to cache SQL statements and custom callback to create new instances of SQL statement strings for uncached values.
   /// </summary>
   public class DefaultOrdinalSQLCache : OrdinalSQLCache
   {
      private readonly String[] _cache;
      private readonly Func<Int32, String> _factory;

      /// <summary>
      /// Creates a new instance of <see cref="DefaultOrdinalSQLCache"/> with given parameters.
      /// </summary>
      /// <param name="maxPermutationCount">The maximum amount of permutations.</param>
      /// <param name="factory">The callback to create new SQL statement from permutation ordinal number.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="factory"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">If <paramref name="maxPermutationCount"/> is less than <c>0</c>.</exception>
      public DefaultOrdinalSQLCache(
         Int32 maxPermutationCount,
         Func<Int32, String> factory
         )
      {
         this._cache = maxPermutationCount.NewArrayOfLength<String>( nameof( maxPermutationCount ) );
         this._factory = ArgumentValidator.ValidateNotNull( nameof( factory ), factory );
      }

      /// <summary>
      /// Implements <see cref="OrdinalSQLCache.GetSQL"/> and returns possibly cached SQL statement string for given permutation ordinal number.
      /// If new SQL statement string is created, it is then cached.
      /// </summary>
      /// <param name="permutationOrdinalNumber">The permutation ordinal number.</param>
      /// <returns>Cached or created SQL statement string.</returns>
      public String GetSQL( Int32 permutationOrdinalNumber )
      {
         var retVal = this._cache[permutationOrdinalNumber];
         if ( retVal == null )
         {
            retVal = this._factory( permutationOrdinalNumber );
            if ( retVal != null )
            {
               Interlocked.Exchange( ref this._cache[permutationOrdinalNumber], retVal );
            }
         }

         return retVal;
      }
   }

   /// <summary>
   /// Provides default implementation for <see cref="OrdinalSQLCache{T}"/>, using array to cache SQL statements and custom callback to create new instances of SQL statement strings for uncached values.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   public class DefaultOrdinalSQLCache<T> : OrdinalSQLCache<T>
   {
      private readonly String[] _cache;
      private readonly Func<Int32, T, (String, Boolean)> _factory;

      /// <summary>
      /// Creates a new instance of <see cref="DefaultOrdinalSQLCache{T}"/> with given parameters.
      /// </summary>
      /// <param name="maxPermutationCount">The maximum amount of permutations.</param>
      /// <param name="factory">The callback to create new SQL statement from permutation ordinal number and extra parameter. The return value will always be cached.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="factory"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">If <paramref name="maxPermutationCount"/> is less than <c>0</c>.</exception>
      public DefaultOrdinalSQLCache(
         Int32 maxPermutationCount,
         Func<Int32, T, String> factory
         ) : this( maxPermutationCount, factory == null ? (Func<Int32, T, (String, Boolean)>)null : ( n, p ) => (factory( n, p ), true) )
      {
      }

      /// <summary>
      /// Creates a new instance of <see cref="DefaultOrdinalSQLCache{T}"/> with given parameters.
      /// </summary>
      /// <param name="maxPermutationCount">The maximum amount of permutations.</param>
      /// <param name="factory">The callback to create new SQL statement from permutation ordinal number and extra parameter. The return value will be cached if second item of the returned tuple is <c>true</c>.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="factory"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">If <paramref name="maxPermutationCount"/> is less than <c>0</c>.</exception>
      public DefaultOrdinalSQLCache(
         Int32 maxPermutationCount,
         Func<Int32, T, (String, Boolean)> factory
         )
      {
         this._cache = maxPermutationCount.NewArrayOfLength<String>( nameof( maxPermutationCount ) );
         this._factory = factory;
      }

      /// <summary>
      /// Implements <see cref="OrdinalSQLCache.GetSQL"/> and returns possibly cached SQL statement string for given permutation ordinal number.
      /// If new SQL statement string is created, and if factory method used to create it returns <c>true</c> in the result tuple second item, the SQL string is then cached.
      /// </summary>
      /// <param name="permutationOrderNumber">The permutation ordinal number.</param>
      /// <param name="param">The extra parameter to pass to factory method.</param>
      /// <returns>Cached or created SQL statement string.</returns>
      public String GetSQL( Int32 permutationOrderNumber, T param )
      {
         var retVal = this._cache[permutationOrderNumber];
         if ( retVal == null )
         {
            Boolean shouldCache;
            (retVal, shouldCache) = this._factory( permutationOrderNumber, param );
            if ( retVal != null && shouldCache )
            {
               Interlocked.Exchange( ref this._cache[permutationOrderNumber], retVal );
            }
         }

         return retVal;
      }
   }

   /// <summary>
   /// This class extends <see cref="DatabaseMetadataImpl"/> to implement lazy caching of SQL statement strings used to create <see cref="SQLStatementBuilder"/>s for various metadata queries.
   /// </summary>
   public abstract class SQLCachingDatabaseMetadataImpl : DatabaseMetadataImpl
   {

      private readonly DatabaseMetadataSQLCache _cache;

      /// <summary>
      /// Inititalizes a new instance of <see cref="SQLCachingDatabaseMetadataImpl"/> with given parameters.
      /// </summary>
      /// <param name="vendorFunctionality">The <see cref="SQLConnectionVendorFunctionality"/> to use when creating <see cref="SQLStatementBuilder"/>s.</param>
      /// <param name="name">The name of the database.</param>
      /// <param name="cache">The <see cref="DatabaseMetadataSQLCache"/> to use to retrieve SQL statement strings.</param>
      /// <exception cref="ArgumentNullException">If either of <paramref name="vendorFunctionality"/> or <paramref name="cache"/> is <c>null</c>.</exception>
      public SQLCachingDatabaseMetadataImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         String name,
         DatabaseMetadataSQLCache cache
         ) : base( vendorFunctionality, name )
      {
         this._cache = ArgumentValidator.ValidateNotNull( nameof( cache ), cache );
      }

      /// <summary>
      /// This method implements <see cref="DatabaseMetadataImpl.CreateSQLForSchemaSearch"/> by using <see cref="DatabaseMetadataSQLCache.SchemaSearchCache"/> and passing values <c>0..1</c> as permutation ordinal numbers.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern.</param>
      /// <returns>Created or cached SQL string for schema search.</returns>
      /// <remarks>
      /// The permutational ordinal number is computed using following logic:
      /// <list type="table">
      /// 
      /// <listheader>
      /// <term>Value of <paramref name="schemaNamePattern"/></term>
      /// <term>Permutation ordinal number</term>
      /// </listheader>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>0</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>1</term>
      /// </item>
      /// 
      /// </list>
      /// </remarks>
      protected override String CreateSQLForSchemaSearch( String schemaNamePattern )
      {
         return this._cache.SchemaSearchCache.GetSQL( schemaNamePattern == null ? 0 : 1 );
      }

      /// <summary>
      /// This method implements <see cref="DatabaseMetadataImpl.CreateSQLForTableSearch"/> by either using <see cref="DatabaseMetadataSQLCache.TableSearchCache"/> and passing values <c>0..7</c> as permutation ordinal numbers, or using <see cref="DatabaseMetadataSQLCache.TableSearchCacheForMultipleTableTypes"/> and passing values <c>0..3</c> as permutation ordinal numbers.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern.</param>
      /// <param name="tableNamePattern">The table name pattern.</param>
      /// <param name="tableTypes">The table types.</param>
      /// <returns>Created or cached SQL string for table search.</returns>
      /// <remarks>
      /// The cache and permutational ordinal numbers are computed using following logic.
      /// <list type="table">
      /// 
      /// <listheader>
      /// <term>Value of <paramref name="schemaNamePattern"/></term>
      /// <term>Value of <paramref name="tableNamePattern"/></term>
      /// <term>Value of <paramref name="tableTypes"/></term>
      /// <term>Permutation ordinal number</term>
      /// <term>Cache used</term>
      /// </listheader>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c> or empty</term>
      /// <term>0</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>has exactly one element</term>
      /// <term>1</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c> or empty</term>
      /// <term>2</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>has exactly one element</term>
      /// <term>3</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c> or empty</term>
      /// <term>4</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>has exactly one element</term>
      /// <term>5</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c> or empty</term>
      /// <term>6</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>has exactly one element</term>
      /// <term>7</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCache"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>has at least two elements</term>
      /// <term>0</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCacheForMultipleTableTypes"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>has at least two elements</term>
      /// <term>1</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCacheForMultipleTableTypes"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>has at least two elements</term>
      /// <term>2</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCacheForMultipleTableTypes"/></term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>has at least two elements</term>
      /// <term>3</term>
      /// <term><see cref="DatabaseMetadataSQLCache.TableSearchCacheForMultipleTableTypes"/></term>
      /// </item>
      /// 
      /// </list>
      /// </remarks>
      protected override String CreateSQLForTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes )
      {
         var noTypes = tableTypes.IsNullOrEmpty();
         String retVal;
         if ( noTypes || tableTypes.Length == 1 )
         {
            // 0..1 table types
            retVal = this._cache.TableSearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern, noTypes ? null : "" ), noTypes ? default : tableTypes[0] );
         }
         else
         {
            // 2..* table types
            retVal = this._cache.TableSearchCacheForMultipleTableTypes.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern ), tableTypes );
         }
         return retVal;
      }

      /// <summary>
      /// This method implements <see cref="DatabaseMetadataImpl.CreateSQLForColumnSearch"/> by using <see cref="DatabaseMetadataSQLCache.ColumnSearchCache"/> and passing values <c>0..7</c> as permutation ordinal numbers.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern.</param>
      /// <param name="tableNamePattern">The table name pattern.</param>
      /// <param name="columnNamePattern">The column name pattern.</param>
      /// <returns>Created or cached SQL string for column search.</returns>
      /// <remarks>
      /// The permutational ordinal number is computed using following logic:
      /// <list type="table">
      /// 
      /// <listheader>
      /// <term>Value of <paramref name="schemaNamePattern"/></term>
      /// <term>Value of <paramref name="tableNamePattern"/></term>
      /// <term>Value of <paramref name="columnNamePattern"/></term>
      /// <term>Permutation ordinal number</term>
      /// </listheader>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>0</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>1</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>2</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>3</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>4</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>5</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>6</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>7</term>
      /// </item>
      /// 
      /// </list>
      /// </remarks>
      protected override String CreateSQLForColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern )
      {
         return this._cache.ColumnSearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern, columnNamePattern ) );
      }

      /// <summary>
      /// This method implements <see cref="DatabaseMetadataImpl.CreateSQLForPrimaryKeySearch"/> by using <see cref="DatabaseMetadataSQLCache.PrimaryKeySearchCache"/> and passing values <c>0..3</c> as permutation ordinal numbers.
      /// </summary>
      /// <param name="schemaNamePattern">The schema name pattern.</param>
      /// <param name="tableNamePattern">The table name pattern.</param>
      /// <returns>Created or cached SQL string for primary key search.</returns>
      /// <remarks>
      /// The permutational ordinal number is computed using following logic:
      /// <list type="table">
      /// 
      /// <listheader>
      /// <term>Value of <paramref name="schemaNamePattern"/></term>
      /// <term>Value of <paramref name="tableNamePattern"/></term>
      /// <term>Permutation ordinal number</term>
      /// </listheader>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>0</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>1</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>2</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>3</term>
      /// </item>
      /// 
      /// </list>
      /// </remarks>
      protected override String CreateSQLForPrimaryKeySearch( String schemaNamePattern, String tableNamePattern )
      {
         return this._cache.PrimaryKeySearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern ) );
      }

      /// <summary>
      /// This method implements <see cref="DatabaseMetadataImpl.CreateSQLForForeignKeySearch"/> by using <see cref="DatabaseMetadataSQLCache.ForeignKeySearchCache"/> and passing values <c>0..15</c> as permutation ordinal numbers.
      /// </summary>
      /// <param name="primarySchemaName">The name pattern for schema name of the table with primary key.</param>
      /// <param name="primaryTableName">The name pattern for table name of the table with primary key.</param>
      /// <param name="foreignSchemaName">The name pattern for schema name of the table with foreeign key.</param>
      /// <param name="foreignTableName">The name pattern for table name of the table with foreeign key.</param>
      /// <returns>Created or cached SQL string for primary key search.</returns>
      /// <remarks>
      /// The permutational ordinal number is computed using following logic:
      /// <list type="table">
      /// 
      /// <listheader>
      /// <term>Value of <paramref name="primarySchemaName"/></term>
      /// <term>Value of <paramref name="primaryTableName"/></term>
      /// <term>Value of <paramref name="foreignSchemaName"/></term>
      /// <term>Value of <paramref name="foreignTableName"/></term>
      /// <term>Permutation ordinal number</term>
      /// </listheader>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>0</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>1</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>2</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>3</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>4</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>5</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>6</term>
      /// </item>
      /// 
      /// <item>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>7</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>8</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>9</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>10</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>11</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>12</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>13</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>14</term>
      /// </item>
      /// 
      /// <item>
      /// <term>not <c>null</c></term>
      /// <term><c>null</c></term>
      /// <term><c>null</c></term>
      /// <term>not <c>null</c></term>
      /// <term>15</term>
      /// </item>
      /// 
      /// </list>
      /// </remarks>
      protected override String CreateSQLForForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
      {
         return this._cache.ForeignKeySearchCache.GetSQL( GetLexicographicalOrderNumber( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ) );
      }

      private static Int32 GetLexicographicalOrderNumber( String p1, String p2 )
      {
         var retVal = p2 == null ? 0 : 1;
         if ( p1 != null )
         {
            retVal += 2;
         }
         return retVal;
      }

      private static Int32 GetLexicographicalOrderNumber( String p1, String p2, String p3 )
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
         // Unwrap this generic method for variations taking 2,3, and 4 strings, to avoid creating array for "params" parameter.
         //var retVal = 0;
         //var curPow = 1;
         //for ( var i = 0; i < paramz.Length; ++i )
         //{
         //   if ( paramz[i] != null )
         //   {
         //      retVal += curPow;
         //   }
         //   curPow *= 2;
         //}

         //return retVal;

         var retVal = p3 == null ? 0 : 1;
         if ( p2 != null )
         {
            retVal += 2;
         }
         if ( p1 != null )
         {
            retVal += 4;
         }

         return retVal;
      }


      private static Int32 GetLexicographicalOrderNumber( String p1, String p2, String p3, String p4 )
      {
         var retVal = p4 == null ? 0 : 1;
         if ( p3 != null )
         {
            retVal += 2;
         }
         if ( p2 != null )
         {
            retVal += 4;
         }
         if ( p1 != null )
         {
            retVal += 8;
         }

         return retVal;
      }

   }
}
