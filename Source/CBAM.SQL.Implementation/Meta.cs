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
      /// <returns>A tuple of value and type of the value for <paramref name="tableType"/>. If value is not <c>null</c>, the type may be <c>null</c>.</returns>
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
      /// Gets the <see cref="OrdinalSQLCache{T}"/> used for table searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache{T}"/> used for table searches.</value>
      /// <seealso cref="SQLCachingDatabaseMetadataImpl.CreateSQLForTableSearch"/>
      OrdinalSQLCache<TableType?> TableSearchCache { get; }

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
      /// <param name="columnSearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="ColumnSearchCache"/>.</param>
      /// <param name="primaryKeySearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="PrimaryKeySearchCache"/>.</param>
      /// <param name="foreignKeySearchSQLFactory">The factory callback for <see cref="DefaultOrdinalSQLCache"/> acting as <see cref="ForeignKeySearchCache"/>.</param>
      public DefaultDatabaseMetadataSQLCache(
         Func<Int32, String> schemaSearchSQLFactory, // 0 - (schemaNamePattern Missing), 1 - (schemaNamePattern Present)
         Func<Int32, TableType?, String> tableSearchSQLFactory, // 0 - (schemaNamePattern M, tableNamePattern M, tableTypes M), 1 - (schemaNamePattern M, tableNamePattern M, tableTypes P), 2 - (schemaNamePattern M, tableNamePattern P, tableTypes M), 3 - (schemaNamePattern M, tableNamePattern P, tableTypes P)
         Func<Int32, String> columnSearchSQLFactory,
         Func<Int32, String> primaryKeySearchSQLFactory,
         Func<Int32, String> foreignKeySearchSQLFactory
         )
      {
         this.SchemaSearchCache = new DefaultOrdinalSQLCache( 1 << 1, schemaSearchSQLFactory );
         this.TableSearchCache = new DefaultOrdinalSQLCache<TableType>( 1 << 3, tableSearchSQLFactory );
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
      /// Implements <see cref="DatabaseMetadataSQLCache.TableSearchCache"/> and gets the <see cref="OrdinalSQLCache{T}"/> used for schema searches.
      /// </summary>
      /// <value>The <see cref="OrdinalSQLCache"/> used for schema searches.</value>
      public OrdinalSQLCache<TableType?> TableSearchCache { get; }
      public OrdinalSQLCache ColumnSearchCache { get; }
      public OrdinalSQLCache PrimaryKeySearchCache { get; }
      public OrdinalSQLCache ForeignKeySearchCache { get; }
   }

   public interface OrdinalSQLCache
   {
      String GetSQL( Int32 permutationOrderNumber );
   }

   public interface OrdinalSQLCache<in T>
   {
      String GetSQL( Int32 permutationOrderNumber, T parameter );
   }

   public class DefaultOrdinalSQLCache : OrdinalSQLCache
   {
      private readonly String[] _cache;
      private readonly Func<Int32, String> _factory;

      public DefaultOrdinalSQLCache(
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

   public class DefaultOrdinalSQLCache<T> : OrdinalSQLCache<T?>
      where T : struct
   {
      private readonly String[] _cache;
      private readonly Func<Int32, T?, String> _factory;

      public DefaultOrdinalSQLCache(
         Int32 maxPermutationCount,
         Func<Int32, T?, String> factory
         )
      {
         this._cache = new String[maxPermutationCount];
         this._factory = factory;
      }

      public String GetSQL( Int32 permutationOrderNumber, T? param )
      {
         var retVal = this._cache[permutationOrderNumber];
         if ( retVal == null )
         {
            retVal = this._factory( permutationOrderNumber, param );
            if ( retVal != null )
            {
               Interlocked.Exchange( ref this._cache[permutationOrderNumber], retVal );
            }
         }

         return retVal;
      }
   }

   public abstract class SQLCachingDatabaseMetadataImpl : DatabaseMetadataImpl
   {

      private readonly DatabaseMetadataSQLCache _cache;

      public SQLCachingDatabaseMetadataImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         String name,
         DatabaseMetadataSQLCache cache
         ) : base( vendorFunctionality, name )
      {
         this._cache = ArgumentValidator.ValidateNotNull( nameof( cache ), cache );
      }

      protected override String CreateSQLForSchemaSearch( String schemaNamePattern )
      {
         return this._cache.SchemaSearchCache.GetSQL( schemaNamePattern == null ? 0 : 1 );
      }

      protected override String CreateSQLForTableSearch( String schemaNamePattern, String tableNamePattern, TableType[] tableTypes )
      {
         var noTypes = tableTypes.IsNullOrEmpty();
         return this._cache.TableSearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern, noTypes ? null : "" ), noTypes || tableTypes.Length > 1 ? default : tableTypes[0] );
      }

      protected override String CreateSQLForColumnSearch( String schemaNamePattern, String tableNamePattern, String columnNamePattern )
      {
         return this._cache.ColumnSearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern, columnNamePattern ) );
      }

      protected override String CreateSQLForPrimaryKeySearch( String schemaNamePattern, String tableNamePattern )
      {
         return this._cache.PrimaryKeySearchCache.GetSQL( GetLexicographicalOrderNumber( schemaNamePattern, tableNamePattern ) );
      }

      protected override String CreateSQLForForeignKeySearch( String primarySchemaName, String primaryTableName, String foreignSchemaName, String foreignTableName )
      {
         return this._cache.ForeignKeySearchCache.GetSQL( GetLexicographicalOrderNumber( primarySchemaName, primaryTableName, foreignSchemaName, foreignTableName ) );
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
