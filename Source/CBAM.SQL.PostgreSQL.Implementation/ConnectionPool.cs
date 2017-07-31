using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using CBAM.Abstractions.Implementation;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLConnectionAcquireInfo : ConnectionAcquireInfoImpl<PgSQLConnection, PostgreSQLProtocol, StatementBuilder, StatementBuilderInformation, SQLStatementExecutionResult, System.IO.Stream>
   {
      public PgSQLConnectionAcquireInfo( PgSQLConnection connection, PostgreSQLProtocol connectionFunctionality, Stream associatedStream )
         : base( connection, connectionFunctionality, associatedStream )
      {
      }

      protected override async Task DisposeBeforeClosingStream( CancellationToken token )
      {
         await this.ConnectionFunctionality.PerformClose( token );
      }
   }
}
