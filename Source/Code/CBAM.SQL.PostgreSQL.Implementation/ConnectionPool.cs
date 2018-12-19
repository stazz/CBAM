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
using CBAM.Abstractions.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLConnectionAcquireInfo : ConnectionAcquireInfoImpl<PgSQLConnectionImpl, PostgreSQLProtocol, Stream>
   {
      public PgSQLConnectionAcquireInfo( PgSQLConnectionImpl connection, PostgreSQLProtocol functionality )
         : base( connection, functionality, functionality.Stream )
      {

      }

      protected override Task DisposeBeforeClosingChannel( CancellationToken token )
      {
         return this.Functionality.PerformClose( token );
      }
   }
}
