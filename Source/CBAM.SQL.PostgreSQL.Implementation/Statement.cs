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
using CBAM.SQL.Implementation;
using CBAM.SQL.PostgreSQL;
using System;
using System.Collections.Generic;
using System.Text;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLStatementBuilder : StatementBuilderImpl<StatementParameter>
   {

      public PgSQLStatementBuilder(
         PgSQLStatementBuilderInformation information,
         StatementParameter[] currentParameters,
         List<StatementParameter[]> batchParams
         )
         : base( information, currentParameters, batchParams )
      {
      }

      protected override StatementParameter CreateStatementParameter( Int32 parameterIndex, Object value, Type clrType )
      {
         return new StatementParameterImpl(
            clrType,
            value
            );
      }

      //protected override SQLException VerifyBatchParameters( StatementParameter previous, StatementParameter toBeAdded )
      //{
      //   return null;
      //}
   }

   internal sealed class PgSQLStatementBuilderInformation : StatementBuilderInformationImpl<StatementParameter, List<StatementParameter[]>>
   {
      public PgSQLStatementBuilderInformation(
         String sql,
         StatementParameter[] currentParameters,
         List<StatementParameter[]> batchParams,
         Int32[] parameterIndices
         ) : base( sql, currentParameters, batchParams )
      {
         this.ParameterIndices = parameterIndices;
      }

      internal Int32[] ParameterIndices { get; }
   }


   //internal sealed class PgSQLStatementParameterImpl : StatementParameterImpl, PgStatementParameter
   //{
   //   public PgSQLStatementParameterImpl(
   //      Type cilType,
   //      Object value,
   //      TypeRegistry typeRegistry
   //      ) : base( cilType, value )
   //   {
   //      this.DBTypeID = typeRegistry.GetTypeInfo( cilType ).BoundData.TypeID;
   //   }

   //   public Int32 DBTypeID { get; }
   //}
}
