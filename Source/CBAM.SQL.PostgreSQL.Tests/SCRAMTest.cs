using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UtilPack.ResourcePooling.NetworkStream;

namespace CBAM.SQL.PostgreSQL.Tests
{
   [TestClass]
   public class SCRAMTest : AbstractPostgreSQLTest
   {
      [
         DataTestMethod,
         DataRow( SCRAM_CONFIG_FILE_LOCATION ), // File containing cleartext password
         DataRow( SCRAM_DIGEST_CONFIG_FILE_LOCATION ), // File containing password_s, after the PBKDF2 applied to it
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestSCRAM( String connectionConfigFileLocation )
      {
         var creationInfo = GetConnectionCreationInfo( connectionConfigFileLocation );
         var pool = GetPool( creationInfo );
         var selectResult = await pool.UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
      }


   }
}
