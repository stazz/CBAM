﻿/*
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
using System.Threading.Tasks;
using CBAM.SQL.Implementation;
using UtilPack.TabularData;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLDatabaseMetaData : SQLCachingDatabaseMetadataImpl
   {
      private static readonly DatabaseMetadataSQLCache SQLCache = new DefaultDatabaseMetadataSQLCache(
         SchemaSearchSQLFactory,
         TableSearchSQLFactory,
         TableSearchForMultipleTableTypesSQLFactory,
         ColumnSearchSQLFactory,
         PrimaryKeySearchSQLFactory,
         ForeignKeySearchSQLFactory
         );

      public PgSQLDatabaseMetaData(
         PostgreSQLProtocol connectionFunctionality
         ) : base(
            connectionFunctionality.VendorFunctionality,
            connectionFunctionality.ServerParameters[PostgreSQLProtocol.SERVER_PARAMETER_DATABASE],
            SQLCache
            )
      {
      }

      protected override ValueTask<SchemaMetadata> DoExtractSchemaMetadataAsync( AsyncDataRow row )
      {
         throw new NotImplementedException();
      }

      protected override ValueTask<TableMetadata> DoExtractTableMetadataAsync( AsyncDataRow row )
      {
         throw new NotImplementedException();
      }

      protected override ValueTask<ColumnMetadata> DoExtractColumnMetadataAsync( AsyncDataRow row )
      {
         throw new NotImplementedException();
      }

      protected override ValueTask<PrimaryKeyMetadata> DoExtractPrimaryKeyMetadataAsync( AsyncDataRow row )
      {
         throw new NotImplementedException();
      }

      protected override ValueTask<ForeignKeyMetadata> DoExtractForeignKeyMetadataAsync( AsyncDataRow row )
      {
         throw new NotImplementedException();
      }

      protected override (Object, Type) GetParameterInfoForTableType( TableType tableType )
      {
         throw new NotImplementedException();
      }

      private static String SchemaSearchSQLFactory( Int32 permutationOrderNumber )
      {
         throw new NotImplementedException();
      }

      private static String TableSearchSQLFactory( Int32 permutationOrderNumber, TableType? tableType )
      {
         throw new NotImplementedException();
      }

      private static (String, Boolean) TableSearchForMultipleTableTypesSQLFactory( Int32 permutationOrderNumber, TableType[] tableTypes )
      {
         throw new NotImplementedException();
      }

      private static String ColumnSearchSQLFactory( Int32 permutationOrderNumber )
      {
         throw new NotImplementedException();
      }

      private static String PrimaryKeySearchSQLFactory( Int32 permutationOrderNumber )
      {
         throw new NotImplementedException();
      }

      private static String ForeignKeySearchSQLFactory( Int32 permutationOrderNumber )
      {
         throw new NotImplementedException();
      }


   }
}
