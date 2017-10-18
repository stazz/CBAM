using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using CBAM.Abstractions.Implementation;
using UtilPack.AsyncEnumeration;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLConnectionAcquireInfo : ConnectionAcquireInfoImpl<PgSQLConnectionImpl, PostgreSQLProtocol, System.IO.Stream>
   {
      public PgSQLConnectionAcquireInfo( PgSQLConnectionImpl connection, Stream associatedStream )
         : base( connection, associatedStream )
      {
      }

      protected override async Task DisposeBeforeClosingStream( CancellationToken token, PostgreSQLProtocol connectionFunctionality )
      {
         await connectionFunctionality.PerformClose( token );
      }
   }
}
