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
using System;
using System.Collections.Generic;
using System.Text;

namespace CBAM.SQL.PostgreSQL
{
   public sealed class PgSQLConnectionPoolProvider : ConnectionPoolProvider<PgSQLConnection>
   {
      public ConnectionPool<PgSQLConnection> CreateOneTimeUseConnectionPool( Object creationParameters )
      {
         return creationParameters is PgSQLConnectionCreationInfoData creationData ?
            PgSQLConnectionPool.CreateOneTimeUseConnectionPool( new PgSQLConnectionCreationInfo( creationData ) ) :
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( PgSQLConnectionCreationInfoData ).FullName}." );
      }

      public Type DefaultTypeForCreationParameter => typeof( PgSQLConnectionCreationInfoData );
   }
}
