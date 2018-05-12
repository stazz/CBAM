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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CBAM.SQL.PostgreSQL.Implementation;
using UtilPack;
using CBAM.Abstractions.Implementation;
using CBAM.SQL.PostgreSQL;
using UtilPack.Cryptography.SASL.SCRAM;
using UtilPack.Cryptography.SASL;
using UtilPack.Configuration.NetworkStream;

#if !NETSTANDARD1_0
using UtilPack.ResourcePooling.NetworkStream;
#endif

namespace CBAM.SQL.PostgreSQL
{

   /// <summary>
   /// This class represents the passive data related to creation of new <see cref="PgSQLConnection"/>.
   /// Typically, this class holds the values written in some configuration file on a disk.
   /// Use <c>Microsoft.Extensions.Configuration.Binder</c> NuGet package to automatize the creation of this class and population of its properties.
   /// </summary>
   /// <remarks>
   /// See <see href="https://github.com/CometaSolutions/CBAM/tree/develop/Source/CBAM.SQL.PostgreSQL.Implementation"/> for small example on typical usecase of this class.
   /// </remarks>
   /// <seealso cref="PgSQLConnectionCreationInfo"/>
   public class PgSQLConnectionCreationInfoData : NetworkConnectionCreationInfoData<PgSQLConnectionConfiguration, PgSQLInitializationConfiguration, PgSQLProtocolConfiguration, PgSQLAuthenticationConfiguration, PgSQLPoolingConfiguration>
   {


   }

   /// <summary>
   /// This class contains all passive configuration data related to opening a socket connection when initializing new <see cref="PgSQLConnection"/>.
   /// </summary>
   public class PgSQLConnectionConfiguration : NetworkConnectionConfiguration
   {
   }

   /// <summary>
   /// This class contains all passive configuration data related to initialization routine of new <see cref="PgSQLConnection"/> once the <see cref="Stream"/> used to communicate with backend has been initialized.
   /// </summary>
   public class PgSQLInitializationConfiguration : NetworkInitializationConfiguration<PgSQLProtocolConfiguration, PgSQLAuthenticationConfiguration, PgSQLPoolingConfiguration>
   {
      /// <summary>
      /// Gets or sets the type containing passive configuration data about the database to connect to.
      /// </summary>
      /// <value>The type containing passive configuration data about the database to connect to.</value>
      /// <seealso cref="PgSQLDatabaseConfiguration"/>
      public PgSQLDatabaseConfiguration Database { get; set; }

   }

   /// <summary>
   /// This class contains all passive configuration data related to behaviour of PgSQL connections when they are used within the connection pool.
   /// </summary>
   public class PgSQLPoolingConfiguration : NetworkPoolingConfiguration
   {
   }

   /// <summary>
   /// This class contains all passive configuration data related to authentication of new <see cref="PgSQLConnection"/>.
   /// </summary>
   public class PgSQLAuthenticationConfiguration
   {

      internal static readonly Encoding PasswordByteEncoding = new UTF8Encoding( false, true );

      /// <summary>
      /// Gets or sets the username to use when connecting to the database.
      /// </summary>
      /// <value>The username to use when connecting to the database.</value>
      public String Username { get; set; }

      /// <summary>
      /// Gets the textual password as byte array, to use along with <see cref="Username"/> when connecting to the database.
      /// </summary>
      /// <value>The textual password as byte array, to use along with <see cref="Username"/> when connecting to the database.</value>
      /// <seealso cref="PasswordDigest"/>
      public Byte[] PasswordBytes { get; private set; }

      /// <summary>
      /// Gets or sets the digest of the cleartext password.
      /// </summary>
      /// <value>The digest of the cleartext password.</value>
      /// <remarks>
      /// This property will *only* be used in SCRAM authentication, if server chooses to perform it.
      /// The SCRAM authentication allows to use the digest of the password instead of cleartext password.
      /// </remarks>
      public Byte[] PasswordDigest { get; set; }

      /// <summary>
      /// Gets the password, as <see cref="String"/>, to use along with <see cref="Username"/> when connecting to the database.
      /// </summary>
      /// <value>The password, as <see cref="String"/>, to use along with <see cref="Username"/> when connecting to the database.</value>
      public String Password
      {
         get
         {
            var arr = this.PasswordBytes;
            return arr == null ? null : PasswordByteEncoding.GetString( arr, 0, arr.Length );
         }
         set
         {
            this.PasswordBytes = value == null ? null : PasswordByteEncoding.GetBytes( value );
         }
      }
   }

   /// <summary>
   /// This class contains all passive configuration data related to selecting which database the <see cref="PgSQLConnection"/> will be connected to, as well as authentication to that database.
   /// </summary>
   public class PgSQLDatabaseConfiguration
   {

      /// <summary>
      /// Gets or sets the name of the database that the <see cref="PgSQLConnection"/> should be connected to.
      /// </summary>
      /// <value>The name of the database that the <see cref="PgSQLConnection"/> should be connected to.</value>
      public String Database { get; set; }

      /// <summary>
      /// Gets the search path (<see href="https://www.postgresql.org/docs/current/static/runtime-config-client.html"/>) to use for the database.
      /// </summary>
      /// <value>The search path (<see href="https://www.postgresql.org/docs/current/static/runtime-config-client.html"/>) to use for the database.</value>
      public String SearchPath { get; set; }
   }

   /// <summary>
   /// This class contains all passive data configuration related to protocol used to communicate with backend process.
   /// </summary>
   public class PgSQLProtocolConfiguration
   {
      /// <summary>
      /// Gets or sets the value indicating whether SQL type IDs should be re-read from database, even though they have been previously cached for the version of PostgreSQL backend connected to.
      /// </summary>
      /// <value>The value indicating whether SQL type IDs should be re-read from database</value>
      public Boolean ForceTypeIDLoad { get; set; }

      /// <summary>
      /// Gets or sets the value indicating whether <see cref="DataFormat.Binary"/> should be disabled when sending data to the backend.
      /// </summary>
      /// <value>The value indicating whether <see cref="DataFormat.Binary"/> should be disabled when sending data to the backend.</value>
      public Boolean DisableBinaryProtocolSend { get; set; }

      /// <summary>
      /// Gets or sets the value indicating whether <see cref="DataFormat.Binary"/> should be disabled when receiving data from the backend.
      /// </summary>
      /// <value>The value indicating whether <see cref="DataFormat.Binary"/> should be disabled when receiving data from the backend.</value>
      public Boolean DisableBinaryProtocolReceive { get; set; }
   }

   /// <summary>
   /// This class binds together the passive configuration data of the <see cref="PgSQLConnectionCreationInfoData"/> and behaviour (callbacks) needed when creating and initializing <see cref="PgSQLConnection"/>s.
   /// </summary>
   public sealed class PgSQLConnectionCreationInfo : NetworkConnectionCreationInfo<PgSQLConnectionCreationInfoData, PgSQLConnectionConfiguration, PgSQLInitializationConfiguration, PgSQLProtocolConfiguration, PgSQLAuthenticationConfiguration, PgSQLPoolingConfiguration>
   {
      private const String SCRAM_SHA_256 = "SCRAM-SHA-256";
      private const String SCRAM_SHA_512 = "SCRAM-SHA-512";

      /// <summary>
      /// Creates a new instance of <see cref="PgSQLConnectionCreationInfo"/> with given <see cref="PgSQLConnectionCreationInfoData"/>.
      /// </summary>
      /// <param name="data">The <see cref="PgSQLConnectionCreationInfoData"/> to use.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="data"/> is <c>null</c>.</exception>
      /// <remarks>
      /// In .NET Core App 1.1+ and .NET Desktop 4.0+ environments this will also set up default values for <see cref="P:CBAM.SQL.PostgreSQL.PgSQLConnectionCreationInfo.ProvideSSLStream"/>.
      /// </remarks>
      public PgSQLConnectionCreationInfo(
         PgSQLConnectionCreationInfoData data
         ) : base( data )
      {
         this.CreateSASLMechanism = ( names ) =>
         {
            UtilPack.Cryptography.Digest.BlockDigestAlgorithm algorithm;
            String mechanismName;

            if ( String.IsNullOrEmpty( names ) )
            {
               algorithm = null;
               mechanismName = null;
            }
            else
            {
               if ( names.IndexOf( SCRAM_SHA_512 ) >= 0 )
               {
                  algorithm = new UtilPack.Cryptography.Digest.SHA512();
                  mechanismName = SCRAM_SHA_512;
               }
               else if ( names.IndexOf( SCRAM_SHA_256 ) >= 0 )
               {
                  algorithm = new UtilPack.Cryptography.Digest.SHA256();
                  mechanismName = SCRAM_SHA_256;
               }
               else
               {
                  algorithm = null;
                  mechanismName = null;
               }
            }

            return (algorithm?.CreateSASLClientSCRAM(), mechanismName);
         };
      }
      /// <summary>
      /// This callback will be used during SCRAM authentication to select the <see cref="SASLMechanism"/> based on the advertised mechanisms sent by server as a string.
      /// The constructor will set this to default value which supports SCRAM-SHA-256 and SCRAM-SHA-512, but this may be overridden for custom <see cref="SASLMechanism"/>s.
      /// </summary>
      /// <value>The callback to select <see cref="SASLMechanism"/> from a list of SASL mechanisms advertised by backend.</value>
      /// <remarks>
      /// This callback should return a tuple of <see cref="SASLMechanism"/> and the name of it.
      /// </remarks>
      public Func<String, (SASLMechanism, String)> CreateSASLMechanism { get; set; }

      /// <summary>
      /// This callback will be used during SCRAM authentication, when the user is successfully authenticated.
      /// It will receive a digest of the cleartext password, so that it can be e.g. saved for later purpose.
      /// </summary>
      /// <value>The callback to call on successful SASL SCRAM authentication, receiving password digest as argument.</value>
      public Action<Byte[]> OnSASLSCRAMSuccess { get; set; }

   }
}

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// This is helper method to create a new deep copy of this <see cref="PgSQLConnectionCreationInfoData"/> instance.
   /// </summary>
   /// <param name="data">This <see cref="PgSQLConnectionCreationInfoData"/>.</param>
   /// <returns>A new instance of <see cref="PgSQLConnectionCreationInfoData"/>, with all values deeply copied from this <see cref="PgSQLConnectionCreationInfoData"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="PgSQLConnectionCreationInfoData"/> is <c>null</c>.</exception>
   public static PgSQLConnectionCreationInfoData CreateCopy( this PgSQLConnectionCreationInfoData data )
   {

#if !NETSTANDARD1_0
      var conn = data.Connection;
#endif

      var init = data.Initialization;
      var db = init?.Database;
      var protocol = init?.Protocol;
      var pool = init?.ConnectionPool;
      var auth = init?.Authentication;

      return new PgSQLConnectionCreationInfoData()
      {
#if !NETSTANDARD1_0
         Connection = new PgSQLConnectionConfiguration()
         {
            Host = conn?.Host,
            Port = conn?.Port ?? 0,
            ConnectionSSLMode = conn?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired,
            SSLProtocols = conn?.SSLProtocols ?? PgSQLConnectionConfiguration.DEFAULT_SSL_PROTOCOL
         },
#endif
         Initialization = new PgSQLInitializationConfiguration()
         {
            Database = new PgSQLDatabaseConfiguration()
            {
               Database = db?.Database,
               SearchPath = db?.SearchPath
            },
            Authentication = new PgSQLAuthenticationConfiguration()
            {
               Username = auth?.Username,
               Password = auth?.Password,
               PasswordDigest = auth?.PasswordDigest
            },
            Protocol = new PgSQLProtocolConfiguration()
            {
               ForceTypeIDLoad = protocol?.ForceTypeIDLoad ?? false,
               DisableBinaryProtocolSend = protocol?.DisableBinaryProtocolSend ?? false,
               DisableBinaryProtocolReceive = protocol?.DisableBinaryProtocolReceive ?? false
            },
            ConnectionPool = new PgSQLPoolingConfiguration()
            {
               ConnectionsOwnStringPool = pool?.ConnectionsOwnStringPool ?? false
            }
         }
      };
   }
}