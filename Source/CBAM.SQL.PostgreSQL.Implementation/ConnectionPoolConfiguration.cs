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
   public class PgSQLConnectionCreationInfoData
   {

#if !NETSTANDARD1_0

      /// <summary>
      /// Gets or sets the <see cref="PgSQLConnectionConfiguration"/>, holding data related to socket-based connections.
      /// </summary>
      /// <value>The <see cref="PgSQLConnectionConfiguration"/>, holding data related to socket-based connections.</value>
      public PgSQLConnectionConfiguration Connection { get; set; }

#endif

      /// <summary>
      /// Gets or sets the <see cref="PgSQLInitializationConfiguration"/>, holding data related to initialization process of the <see cref="PgSQLConnection"/>.
      /// </summary>
      /// <value>The <see cref="PgSQLInitializationConfiguration"/>, holding data related to initialization process of the <see cref="PgSQLConnection"/>.</value>
      public PgSQLInitializationConfiguration Initialization { get; set; }


   }

#if !NETSTANDARD1_0
   /// <summary>
   /// This class contains all passive configuration data related to opening a socket connection when initializing new <see cref="PgSQLConnection"/>.
   /// </summary>
   public class PgSQLConnectionConfiguration
   {
      /// <summary>
      /// This constant defines default SSL protocol, if SSL is enabled.
      /// </summary>
      /// <remarks>
      /// In .NET 4.0 environment, this is Tls. In other environments, it is Tls1.2.
      /// </remarks>
      public const System.Security.Authentication.SslProtocols DEFAULT_SSL_PROTOCOL = System.Security.Authentication.SslProtocols
#if NET40
            .Tls
#else
            .Tls12
#endif
         ;

      /// <summary>
      /// Creates a new instance of <see cref="PgSQLConnectionConfiguration"/> with default values.
      /// </summary>
      /// <remarks>
      /// This constructor sets <see cref="SSLProtocols"/> to <see cref="DEFAULT_SSL_PROTOCOL"/> value.
      /// </remarks>
      public PgSQLConnectionConfiguration()
      {
         this.SSLProtocols = DEFAULT_SSL_PROTOCOL;
      }

      /// <summary>
      /// Gets or sets the host name of the PostgreSQL backend process.
      /// </summary>
      /// <value>The host name of the PostgreSQL backend process.</value>
      /// <remarks>
      /// This should be either textual IP address, or host name.
      /// </remarks>
      public String Host { get; set; }

      /// <summary>
      /// Gets or sets the port of the PostgreSQL backend process.
      /// </summary>
      /// <value>The port of the PostgreSQL backend process.</value>
      public Int32 Port { get; set; }

      ///// <summary>
      ///// Gets or sets the host name to use for local endpoint of the socket connection.
      ///// May be <c>null</c>, in which case default is used.
      ///// </summary>
      ///// <value>The host name to use for local endpoint of the socket connection.</value>
      //public String LocalHost { get; set; }

      ///// <summary>
      ///// Gets or sets the port to use for local endpoint of the socket connection.
      ///// </summary>
      ///// <value>The port to use for local endpoint of the socket connection.</value>
      //public Int32 LocalPort { get; set; }

      /// <summary>
      /// Gets or sets the <see cref="PostgreSQL.ConnectionSSLMode"/> to control the SSL encryption for the socket connection.
      /// </summary>
      /// <value>The <see cref="PostgreSQL.ConnectionSSLMode"/> to control the SSL encryption for the socket connection.</value>
      public ConnectionSSLMode ConnectionSSLMode { get; set; }

      /// <summary>
      /// Gets or sets the <see cref="System.Security.Authentication.SslProtocols"/> controlling what kind of SSL encryption will be used for the socket connection.
      /// </summary>
      /// <value>The <see cref="System.Security.Authentication.SslProtocols"/> controlling what kind of SSL encryption will be used for the socket connection.</value>
      /// <remarks>
      /// This field will only be used of <see cref="ConnectionSSLMode"/> property will be something else than <see cref="PostgreSQL.ConnectionSSLMode.NotRequired"/>
      /// </remarks>
      public System.Security.Authentication.SslProtocols SSLProtocols { get; set; }
   }
#endif

   /// <summary>
   /// This class contains all passive configuration data related to initialization routine of new <see cref="PgSQLConnection"/> once the <see cref="Stream"/> used to communicate with backend has been initialized.
   /// </summary>
   public class PgSQLInitializationConfiguration
   {
      /// <summary>
      /// Gets or sets the type containing passive configuration data about the database to connect to.
      /// </summary>
      /// <value>The type containing passive configuration data about the database to connect to.</value>
      /// <seealso cref="PgSQLDatabaseConfiguration"/>
      public PgSQLDatabaseConfiguration Database { get; set; }

      /// <summary>
      /// Gets or sets the type containing passive configuration data about the communication protocol -specific settings.
      /// </summary>
      /// <value>The type containing passive configuration data about the communication protocol -specific settings.</value>
      /// <seealso cref="PgSQLProtocolConfiguration"/>
      public PgSQLProtocolConfiguration Protocol { get; set; }

      /// <summary>
      /// Gets or sets the type containing passive configuration data about the behaviour of the connections when they are used within the connection pool.
      /// </summary>
      /// <value>The type containing passive configuration data about the behaviour of the connections when they are used within the connection pool.</value>
      public PgSQLPoolingConfiguration ConnectionPool { get; set; }
   }

   /// <summary>
   /// This class contains all passive configuration data related to behaviour of connections when they are used within the connection pool.
   /// </summary>
   public class PgSQLPoolingConfiguration
   {
      /// <summary>
      /// Gets or sets the value indicating whether each connection should have its own <see cref="BinaryStringPool"/>.
      /// </summary>
      /// <value>The value indicating whether each connection should have its own <see cref="BinaryStringPool"/>.</value>
      /// <remarks>
      /// Typically this should be true if the same connection pool is used to access secure data of multiple roles or conceptional users.
      /// </remarks>
      public Boolean ConnectionsOwnStringPool { get; set; }
   }

   /// <summary>
   /// This class contains all passive configuration data related to selecting which database the <see cref="PgSQLConnection"/> will be connected to, as well as authentication to that database.
   /// </summary>
   public class PgSQLDatabaseConfiguration
   {
      internal static readonly Encoding PasswordByteEncoding = new UTF8Encoding( false, true );

      /// <summary>
      /// Gets or sets the name of the database that the <see cref="PgSQLConnection"/> should be connected to.
      /// </summary>
      /// <value>The name of the database that the <see cref="PgSQLConnection"/> should be connected to.</value>
      public String Database { get; set; }

      /// <summary>
      /// Gets or sets the username to use when connecting to the database.
      /// </summary>
      /// <value>The username to use when connecting to the database.</value>
      public String Username { get; set; }

      /// <summary>
      /// Gets the password, as bytes, to use along with <see cref="Username"/> when connecting to the database.
      /// </summary>
      /// <value>The password, as bytes, to use along with <see cref="Username"/> when connecting to the database.</value>
      public Byte[] PasswordBytes { get; private set; }

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
   public sealed class PgSQLConnectionCreationInfo
   {

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
         )
      {
         this.CreationData = ArgumentValidator.ValidateNotNull( nameof( data ), data );

#if NETSTANDARD2_0 || NETCOREAPP1_1 || NET45 || NET40
         this.ProvideSSLStream = (
            Stream innerStream,
            Boolean leaveInnerStreamOpen,
            RemoteCertificateValidationCallback userCertificateValidationCallback,
            LocalCertificateSelectionCallback userCertificateSelectionCallback,
            out AuthenticateAsClientAsync authenticateAsClientAsync
            ) =>
         {
            authenticateAsClientAsync = (
               Stream stream,
               String targetHost,
               System.Security.Cryptography.X509Certificates.X509CertificateCollection clientCertificates,
               System.Security.Authentication.SslProtocols enabledSslProtocols,
               Boolean checkCertificateRevocation
            ) =>
            {
               return ( (System.Net.Security.SslStream) stream ).AuthenticateAsClientAsync( targetHost, clientCertificates, enabledSslProtocols, checkCertificateRevocation );
            };

            return new System.Net.Security.SslStream(
               innerStream,
               leaveInnerStreamOpen,
                  (
                     Object sender,
                     System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                     System.Security.Cryptography.X509Certificates.X509Chain chain,
                     System.Net.Security.SslPolicyErrors sslPolicyErrors
                     ) => userCertificateValidationCallback?.Invoke( sender, certificate, chain, sslPolicyErrors ) ?? true,
               userCertificateSelectionCallback == null ?
                  (System.Net.Security.LocalCertificateSelectionCallback) null :
                  (
                     Object sender,
                     String targetHost,
                     System.Security.Cryptography.X509Certificates.X509CertificateCollection localCertificates,
                     System.Security.Cryptography.X509Certificates.X509Certificate remoteCertificate,
                     String[] acceptableIssuers
                  ) => userCertificateSelectionCallback( sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers ),
               System.Net.Security.EncryptionPolicy.RequireEncryption
               );
         };
#endif
      }

      /// <summary>
      /// Gets the <see cref="PgSQLConnectionCreationInfoData"/> that this <see cref="PgSQLConnectionCreationInfo"/> will use when creating and initializing new <see cref="PgSQLConnection"/>s.
      /// </summary>
      /// <value>The <see cref="PgSQLConnectionCreationInfoData"/> that this <see cref="PgSQLConnectionCreationInfo"/> will use when creating and initializing new <see cref="PgSQLConnection"/>s.</value>
      public PgSQLConnectionCreationInfoData CreationData { get; }

#if !NETSTANDARD1_0

      /// <summary>
      /// Gets or sets the callback which should select single <see cref="IPAddress"/> from an array of <see cref="IPAddress"/> that were resolved from the hostname.
      /// </summary>
      /// <value>The callback which should select single <see cref="IPAddress"/> from an array of <see cref="IPAddress"/> that were resolved from the hostname.</value>
      /// <remarks>
      /// This will be invoked only when the amount of resolved <see cref="IPAddress"/>es is more than <c>1</c>.
      /// If this returns <c>null</c>, the first <see cref="IPAddress"/> will be used.
      /// </remarks>
      public Func<IPAddress[], IPAddress> SelectRemoteIPAddress { get; set; }

      /// <summary>
      /// Gets or sets the callback which should provide the local <see cref="IPEndPoint"/> given remote <see cref="IPEndPoint"/>.
      /// </summary>
      /// <value>The callback which should provide the local <see cref="IPEndPoint"/> given remote <see cref="IPEndPoint"/>.</value>
      /// <remarks>
      /// If this is <c>null</c> or returns <c>null</c>, a first free local endpoint will be used.
      /// </remarks>
      public Func<IPEndPoint, IPEndPoint> SelectLocalIPEndPoint { get; set; }

#if NETSTANDARD1_3

      /// <summary>
      /// Gets or sets the callback which should perform DNS resolve from host name.
      /// </summary>
      /// <value>The callback which should perform DNS resolve from host name.</value>
      /// <remarks>
      /// This property is available only on platforms .NET Standard 1.3-1.6.
      /// </remarks>
      public Func<String, ValueTask<IPAddress[]>> DNSResolve { get; set; }

#endif

      /// <summary>
      /// This event is used to add client certificates to <see cref="System.Security.Cryptography.X509Certificates.X509CertificateCollection"/> when using SSL to connect to the backend.
      /// </summary>
      public event Action<System.Security.Cryptography.X509Certificates.X509CertificateCollection> ProvideClientCertificatesEvent;
      internal Action<System.Security.Cryptography.X509Certificates.X509CertificateCollection> ProvideClientCertificates => this.ProvideClientCertificatesEvent;

      /// <summary>
      /// This callback is used to create SSL stream when using SSL to connect to backend.
      /// </summary>
      /// <value>The callback to create SSL stream when using SSL to connect to backend.</value>
      /// <remarks>
      /// In .NET Core App 1.1+ and .NET Desktop 4.0+ environments this will be set to default by the constructor.
      /// </remarks>
      public ProvideSSLStream ProvideSSLStream { get; set; }

      /// <summary>
      /// This callback will be used to validate server certificate when using SSL to connect to the backend.
      /// </summary>
      /// <value>The callback will to validate server certificate when using SSL to connect to the backend.</value>
      /// <remarks>
      /// When not specified (i.e. left to <c>null</c>), server certificate (if provided) will always be accepted.
      /// </remarks>
      public RemoteCertificateValidationCallback ValidateServerCertificate { get; set; }

      /// <summary>
      /// This callback will be used to select local certificate when using SSL to connect to the backend.
      /// </summary>
      /// <value>The callback to select local certificate when using SSL to connect to the backend.</value>
      /// <remarks>
      /// When not specified (i.e. left to <c>null</c>), the first of the certificates is selected, if any.
      /// </remarks>
      public LocalCertificateSelectionCallback SelectLocalCertificate { get; set; }

#endif

      /// <summary>
      /// This property is special in a sense that it is only one visible in .NET Standard 1.0-1.2 environments, and thus is mandatory on those platforms.
      /// On .NET Standard 1.3, .NET Core App 1.1+, and .NET Desktop 4.0+, this property is optional, and may be used to override socket initialization routine and create <see cref="Stream"/> right away.
      /// </summary>
      /// <value>Callback to override socket intialization and create <see cref="Stream"/> right away.</value>
      /// <remarks>
      /// <para>
      /// Using this property has consequence on <see cref="PgSQLConnection.CheckNotificationsAsync"/> method so that using it will cause SQL query to be issued to backend.
      /// This property should be used only in special circumstances (.NET Standard 1.0-1.2 environments, or tests), and in 99.99% of the other cases it should not be used.
      /// </para>
      /// <para>
      /// The <see cref="Stream"/> returned by this callback will be used to start normal PostgreSQL backend initialization routine (<see href="https://www.postgresql.org/docs/current/static/protocol-flow.html#AEN112843"/>).
      /// </para>
      /// </remarks>
      public Func<Stream> StreamFactory { get; set; }

   }

#if !NETSTANDARD1_0

   /// <summary>
   /// This enumeration tells the behaviour of SSL stream establishment when creating connection.
   /// </summary>
   public enum ConnectionSSLMode
   {
      /// <summary>
      /// This is the default value, that will cause SSL stream establishment never to occur - all data will be passed as-is to the underlying <see cref="Stream"/>.
      /// </summary>
      NotRequired,
      /// <summary>
      /// This mode will cause initiation of SSL stream establishment, but in case of error will silently fallback to non-SSL stream.
      /// </summary>
      Preferred,

      /// <summary>
      /// This mode will cause initiation of SSL stream establishment, and if any error occurs, an exception will be thrown.
      /// </summary>
      Required
   }

   /// <summary>
   /// This delegate is used in signature of <see cref="PgSQLConnectionCreationInfo.ProvideSSLStream"/> in order to customize providing of SSL stream.
   /// It is rarely needed to use, since the constructor of <see cref="PgSQLConnectionCreationInfo"/> sets the default value for it in all other platforms except .NET Standard 1.6. or earlier.
   /// </summary>
   /// <param name="innerStream">The inner, unencrypted stream.</param>
   /// <param name="leaveInnerStreamOpen">Whether to leave inner stream opened.</param>
   /// <param name="userCertificateValidationCallback">The callback to validate user certificates.</param>
   /// <param name="userCertificateSelectionCallback">The callback to select user certificates.</param>
   /// <param name="authenticateAsClientAsync">This parameter should contain the <see cref="AuthenticateAsClientAsync"/> callback that will be used by by this library to authenticate the connection.</param>
   /// <returns>The constructed SSL stream.</returns>
   /// <remarks>
   /// The <paramref name="authenticateAsClientAsync"/> parameter typically is a call to <see cref="M:System.Net.Security.SslStream.AuthenticateAsClientAsync(System.String,System.Security.Cryptography.X509Certificates.X509CertificateCollection,System.Security.Authentication.SslProtocols,System.Boolean)"/> method.
   /// </remarks>
   public delegate Stream ProvideSSLStream(
      Stream innerStream,
      Boolean leaveInnerStreamOpen,
      RemoteCertificateValidationCallback userCertificateValidationCallback,
      LocalCertificateSelectionCallback userCertificateSelectionCallback,
      out AuthenticateAsClientAsync authenticateAsClientAsync
      );


   /// <summary>
   /// This delegate is used by <see cref="PgSQLConnectionCreationInfo.ValidateServerCertificate"/> in order to validate the certificate of the PostgreSQL backend.
   /// The signature is just a copy of <see cref="T:System.Net.Security.RemoteCertificateValidationCallback"/> which is not available on all platforms.
   /// </summary>
   /// <param name="sender">The sender.</param>
   /// <param name="certificate">The certificate of the PostgreSQL backend.</param>
   /// <param name="chain">The chain of certificate authorities associated with the <paramref name="certificate"/>.</param>
   /// <param name="sslPolicyErrors">One or more errors associated with the remote certificate.</param>
   /// <returns><c>true</c> if PostgreSQL backend certificate is OK; <c>false</c> otherwise.</returns>
   public delegate Boolean RemoteCertificateValidationCallback(
      Object sender,
      System.Security.Cryptography.X509Certificates.X509Certificate certificate,
      System.Security.Cryptography.X509Certificates.X509Chain chain,
      System.Net.Security.SslPolicyErrors sslPolicyErrors
      );

   /// <summary>
   /// This delegate is used by <see cref="PgSQLConnectionCreationInfo.SelectLocalCertificate"/> in order to select one local certificate to use from possibly many local certificates.
   /// The signature is just a copy of <see cref="T:System.Net.Security.LocalCertificateSelectionCallback"/> which is not available on all platforms.
   /// </summary>
   /// <param name="sender">The sender.</param>
   /// <param name="targetHost">The host server specified by client.</param>
   /// <param name="localCertificates">Local certificates used.</param>
   /// <param name="remoteCertificate">Remote certificate of PostgreSQL backend.</param>
   /// <param name="acceptableIssuers">Certificate issuers acceptable to PostgreSQL backend.</param>
   /// <returns>A certificate from <paramref name="localCertificates"/> collection that should be used in SSL connection.</returns>
   public delegate System.Security.Cryptography.X509Certificates.X509Certificate LocalCertificateSelectionCallback(
      Object sender,
      String targetHost,
      System.Security.Cryptography.X509Certificates.X509CertificateCollection localCertificates,
      System.Security.Cryptography.X509Certificates.X509Certificate remoteCertificate,
      String[] acceptableIssuers
      );

   /// <summary>
   /// This delegate is used by <see cref="ProvideSSLStream"/> delegate in its signature.
   /// The signature captures the one of <see cref="M:System.Net.Security.SslStream.AuthenticateAsClientAsync(System.String,System.Security.Cryptography.X509Certificates.X509CertificateCollection,System.Security.Authentication.SslProtocols,System.Boolean)"/> method.
   /// </summary>
   /// <param name="stream">The stream provided by <see cref="ProvideSSLStream"/> callback.</param>
   /// <param name="targetHost">The host server sepcified by client.</param>
   /// <param name="clientCertificates">The client certificates.</param>
   /// <param name="enabledSslProtocols">The <see cref="System.Security.Authentication.SslProtocols"/> to use in SSL connection.</param>
   /// <param name="checkCertificateRevocation">Whether to check that certificate has been revoked.</param>
   /// <returns>A task which is completed once authentication is completed.</returns>
   public delegate Task AuthenticateAsClientAsync(
      Stream stream,
      String targetHost,
      System.Security.Cryptography.X509Certificates.X509CertificateCollection clientCertificates,
      System.Security.Authentication.SslProtocols enabledSslProtocols,
      Boolean checkCertificateRevocation
      );

#if NET40

   internal static class DnsEx
   {
      // TODO To UtilPack or Theraot.Core
      public static Task<IPAddress[]> GetHostAddressesAsync( String hostName ) //, CancellationToken token )
      {
         return Task.Factory.FromAsync(
           ( hName, cb, state ) => Dns.BeginGetHostAddresses( hName, cb, state ),
           ( result ) => Dns.EndGetHostAddresses( result ),
           hostName,
           null
           );
      }
   }

   internal static class SSLStreamExtensions
   {


      // TODO To UtilPack or Theraot.Core
      public static Task AuthenticateAsClientAsync(
         this System.Net.Security.SslStream stream,
         String targetHost,
         System.Security.Cryptography.X509Certificates.X509CertificateCollection clientCertificates,
         System.Security.Authentication.SslProtocols enabledSslProtocols,
         Boolean checkCertificateRevocation
         )
      {
         var authArgs = (stream, targetHost, clientCertificates, enabledSslProtocols, checkCertificateRevocation);
         return Task.Factory.FromAsync(
            ( aArgs, cb, state ) => aArgs.Item1.BeginAuthenticateAsClient( aArgs.Item2, aArgs.Item3, aArgs.Item4, aArgs.Item5, cb, state ),
            result => ( (System.Net.Security.SslStream) result.AsyncState ).EndAuthenticateAsClient( result ),
            authArgs,
            stream
            );
      }
   }
#endif

#endif
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
               Username = db?.Username,
               Password = db?.Password,
               SearchPath = db?.SearchPath
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