using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.CBAM.SQL.PostgreSQL.Implementation
{
   [TestClass]
   public class SCRAMTest : AbstractPostgreSQLTest
   {
      [
         TestMethod,
         Timeout( DEFAULT_TIMEOUT )
         ]
      public async Task TestSCRAM()
      {
         var creationInfo = GetConnectionCreationInfo( PgSQLConfigurationKind.SCRAM );
         var defaultCreateSASLMechanism = creationInfo.CreateSASLMechanism;
         var saslCalled = false;
         Byte[] saslDigest = null;
         creationInfo.CreateSASLMechanism = mechanism =>
         {
            saslCalled = true;
            return defaultCreateSASLMechanism( mechanism );
         };
         creationInfo.OnSASLSCRAMSuccess = digestBytes =>
         {
            saslDigest = digestBytes.ToArray();
         };

         var selectResult = await GetPool( creationInfo ).UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );
         Assert.IsNotNull( saslDigest, "SASL byte digest must've been seen." );
         Assert.IsTrue( saslCalled, "SASL callback must've been called" );

         Console.WriteLine( "First SCRAM Authentication successful." );

         // Now do test with just sasl digest
         creationInfo.CreationData.Initialization.Authentication.Password = null;
         creationInfo.CreationData.Initialization.Authentication.PasswordDigest = saslDigest;
         selectResult = await GetPool( creationInfo ).UseResourceAsync( async conn => { return await conn.GetFirstOrDefaultAsync<Int32>( "SELECT 1" ); } );
         Assert.AreEqual( 1, selectResult );

         Console.WriteLine( "Second SCRAM Authentication successful." );
      }


   }
}
