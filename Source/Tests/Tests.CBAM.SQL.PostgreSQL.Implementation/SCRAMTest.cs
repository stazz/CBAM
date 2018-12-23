using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tests.CBAM.SQL.PostgreSQL.Implementation
{
   [TestClass]
   public class SCRAMTest : AbstractPostgreSQLTest
   {
      [
         DataTestMethod,
         DataRow( PgSQLConfigurationKind.SCRAM ), // File containing cleartext password
         DataRow( PgSQLConfigurationKind.SCRAM_Digest ), // File containing password_s, after the PBKDF2 applied to it
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestSCRAM( PgSQLConfigurationKind configurationKind )
      {
         var creationInfo = GetConnectionCreationInfo( configurationKind );
         var pool = GetPool( creationInfo );
         var selectResult = await pool.UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
      }


   }
}
