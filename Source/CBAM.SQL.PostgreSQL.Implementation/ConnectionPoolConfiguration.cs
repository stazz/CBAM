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

namespace CBAM.SQL.PostgreSQL
{

#if !NETSTANDARD1_0
   public class PgSQLConnectionCreationInfoData
   {

      public PgSQLConnectionConfiguration Connection { get; set; }
      public PgSQLInitializationConfiguration Initialization { get; set; }

      public PgSQLConnectionCreationInfoData CreateCopy()
      {
         var conn = this.Connection;
         var init = this.Initialization;
         var db = init?.Database;
         var protocol = init?.Protocol;

         return new PgSQLConnectionCreationInfoData()
         {
            Connection = new PgSQLConnectionConfiguration()
            {
               Host = conn?.Host,
               Port = conn?.Port ?? 0,
               LocalHost = conn?.LocalHost,
               LocalPort = conn?.LocalPort ?? 0,
               ConnectionSSLMode = conn?.ConnectionSSLMode ?? ConnectionSSLMode.NotRequired,
               SSLProtocols = conn?.SSLProtocols ?? PgSQLConnectionConfiguration.DEFAULT
            },
            Initialization = new PgSQLInitializationConfiguration()
            {
               Database = new PgSQLDatabaseConfiguration()
               {
                  Database = db?.Database,
                  Username = db?.Username,
                  PasswordBytes = db?.PasswordBytes?.CreateBlockCopy(),
               },
               Protocol = new PgSQLProtocolConfiguration()
               {
                  ForceTypeIDLoad = protocol?.ForceTypeIDLoad ?? false,
                  DisableBinaryProtocolSend = protocol?.DisableBinaryProtocolSend ?? false,
                  DisableBinaryProtocolReceive = protocol?.DisableBinaryProtocolReceive ?? false
               }
            }
         };
      }
   }

   public class PgSQLConnectionConfiguration
   {
      public const System.Security.Authentication.SslProtocols DEFAULT = System.Security.Authentication.SslProtocols
#if NET40
            .Tls
#else
            .Tls12
#endif
         ;
      public PgSQLConnectionConfiguration()
      {
         this.SSLProtocols = DEFAULT;
      }

      public String Host { get; set; }
      public Int32 Port { get; set; }
      public String LocalHost { get; set; }
      public Int32 LocalPort { get; set; }
      public ConnectionSSLMode ConnectionSSLMode { get; set; }
      public System.Security.Authentication.SslProtocols SSLProtocols { get; set; }
   }
#endif



   public class PgSQLInitializationConfiguration
   {
      public PgSQLDatabaseConfiguration Database { get; set; }
      public PgSQLProtocolConfiguration Protocol { get; set; }
   }

   public class PgSQLDatabaseConfiguration
   {
      internal static readonly Encoding PasswordByteEncoding = new UTF8Encoding( false, true );

      public String Database { get; set; }
      public String Username { get; set; }
      public Byte[] PasswordBytes { get; set; }
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

   public class PgSQLProtocolConfiguration
   {
      public Boolean ForceTypeIDLoad { get; set; }
      public Boolean DisableBinaryProtocolSend { get; set; }
      public Boolean DisableBinaryProtocolReceive { get; set; }
   }

#if !NETSTANDARD1_0
   public sealed class PgSQLConnectionCreationInfo
   {
      public PgSQLConnectionCreationInfo(
         PgSQLConnectionCreationInfoData data
         )
      {
         this.CreationData = ArgumentValidator.ValidateNotNull( nameof( data ), data );

#if NETCOREAPP1_1 || NET45 || NET40
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
               return async () => await ( (System.Net.Security.SslStream) stream ).AuthenticateAsClientAsync( targetHost, clientCertificates, enabledSslProtocols, checkCertificateRevocation );
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

      public PgSQLConnectionCreationInfoData CreationData { get; }
      public event Func<String, IPAddress> DNSEvent;
      internal Func<String, IPAddress> DNS => this.DNSEvent;
      public event Action<System.Security.Cryptography.X509Certificates.X509CertificateCollection> ProvideClientCertificatesEvent;
      internal Action<System.Security.Cryptography.X509Certificates.X509CertificateCollection> ProvideClientCertificates => this.ProvideClientCertificatesEvent;
      internal ProvideSSLStream ProvideSSLStream { get; set; }
      public event RemoteCertificateValidationCallback ValidateServerCertificateEvent;
      internal RemoteCertificateValidationCallback ValidateServerCertificate => this.ValidateServerCertificateEvent;
      public event LocalCertificateSelectionCallback SelectLocalCertificateEvent;
      internal LocalCertificateSelectionCallback SelectLocalCertificate => this.SelectLocalCertificateEvent;

   }

   public enum ConnectionSSLMode
   {
      NotRequired,
      Preferred,
      Required
   }

   public delegate Stream ProvideSSLStream(
      Stream innerStream,
      Boolean leaveInnerStreamOpen,
      RemoteCertificateValidationCallback userCertificateValidationCallback,
      LocalCertificateSelectionCallback userCertificateSelectionCallback,
      out AuthenticateAsClientAsync autenticateAsClientAsync
      );

   public delegate Task SSLStreamAuthenticationAsync(
         String targetHost,
         System.Security.Cryptography.X509Certificates.X509CertificateCollection clientCertificates,
         System.Security.Authentication.SslProtocols enabledSslProtocols,
         Boolean checkCertificateRevocation
      );

   public delegate Boolean RemoteCertificateValidationCallback(
      Object sender,
      System.Security.Cryptography.X509Certificates.X509Certificate certificate,
      System.Security.Cryptography.X509Certificates.X509Chain chain,
      System.Net.Security.SslPolicyErrors sslPolicyErrors
      );

   public delegate System.Security.Cryptography.X509Certificates.X509Certificate LocalCertificateSelectionCallback(
      Object sender,
      String targetHost,
      System.Security.Cryptography.X509Certificates.X509CertificateCollection localCertificates,
      System.Security.Cryptography.X509Certificates.X509Certificate remoteCertificate,
      String[] acceptableIssuers
      );

   public delegate Func<Task> AuthenticateAsClientAsync(
      Stream stream,
      String targetHost,
      System.Security.Cryptography.X509Certificates.X509CertificateCollection clientCertificates,
      System.Security.Authentication.SslProtocols enabledSslProtocols,
      Boolean checkCertificateRevocation
      );

#if NET40
   internal static class SSLStreamExtensions
   {
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

   public class PgSQLConnectionCreationInfoForReadyMadeStreams
   {
      public PgSQLInitializationConfiguration Initialization { get; set; }
      public Stream Stream { get; set; }
   }
}
