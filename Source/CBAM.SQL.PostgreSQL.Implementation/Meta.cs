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
using System.Threading.Tasks;
using CBAM.SQL.Implementation;
using CBAM.Tabular;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLDatabaseMetaData : SQLCachingDatabaseMetadataImpl
   {
      public PgSQLDatabaseMetaData(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         PostgreSQLProtocol connectionFunctionality
         ) : base(
            vendorFunctionality,
            connectionFunctionality,
            SchemaSearchSQLFactory,
            TableSearchSQLFactory,
            ColumnSearchSQLFactory,
            PrimaryKeySearchSQLFactory,
            ForeignKeySearchSQLFactory
            )
      {
      }

      public override Task<SchemaMetadata> ExtractSchemaAsync( DataRow row )
      {
         throw new NotImplementedException();
      }

      public override Task<TableMetadata> ExtractTableAsync( DataRow row )
      {
         throw new NotImplementedException();
      }

      public override Task<ColumnMetadata> ExtractColumnAsync( DataRow row )
      {
         throw new NotImplementedException();
      }

      public override Task<PrimaryKeyMetadata> ExtractPrimaryKeyAsync( DataRow row )
      {
         throw new NotImplementedException();
      }

      public override Task<ForeignKeyMetadata> ExtractForeignKeyAsync( DataRow row )
      {
         throw new NotImplementedException();
      }

      protected override String GetStringForTableType( TableType tableType )
      {
         throw new NotImplementedException();
      }

      private static String SchemaSearchSQLFactory( Int32 permutationOrderNumber )
      {
         throw new NotImplementedException();
      }

      private static String TableSearchSQLFactory( Int32 permutationOrderNumber, TableType[] tableTypes )
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
