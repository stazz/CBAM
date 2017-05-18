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
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using UtilPack;
using CBAM.Abstractions.Implementation;
using CBAM.Tabular;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLConnectionImpl : SQLConnectionImpl<PostgreSQLProtocol>, PgSQLConnection
   {
      private const String TRANSACTION_ISOLATION_PREFIX = "SET SESSION CHARACTERISTICS AS TRANSACTION ISOLATION LEVEL ";

      private const String READ_UNCOMMITTED = TRANSACTION_ISOLATION_PREFIX + "READ UNCOMMITTED";
      private const String READ_COMMITTED = TRANSACTION_ISOLATION_PREFIX + "READ COMMITTED";
      private const String REPEATABLE_READ = TRANSACTION_ISOLATION_PREFIX + "REPEATABLE READ";
      private const String SERIALIZABLE = TRANSACTION_ISOLATION_PREFIX + "SERIALIZABLE";

      public PgSQLConnectionImpl(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         PostgreSQLProtocol functionality,
         DatabaseMetadata metaData
         )
         : base( vendorFunctionality, functionality, metaData )
      {
      }

      public event EventHandler<NotificationEventArgs> NotificationEvent
      {
         add
         {
            this.Protocol.NotifyEvent += value;
         }
         remove
         {
            this.Protocol.NotifyEvent -= value;
         }
      }

      public Int32 BackendProcessID => this.Protocol.BackendProcessID;

      public async Task CheckNotificationsAsync()
      {
         await this.Protocol.CheckNotificationsAsync();
      }

      public TypeRegistry TypeRegistry => this.Protocol.TypeRegistry;

      internal PostgreSQLProtocol Protocol
      {
         get
         {
            return (PostgreSQLProtocol) this.ConnectionFunctionality;
         }
      }

      protected override String GetSQLForGettingReadOnly()
      {
         return "SHOW default_transaction_read_only";
      }

      protected override String GetSQLForGettingTransactionIsolationLevel()
      {
         return "SHOW TRANSACTION ISOLATION LEVEL";
      }

      protected override String GetSQLForSettingReadOnly( Boolean isReadOnly )
      {
         this.ThrowIfInTransaction( "read-only property" );
         return "SET SESSION CHARACTERISTICS AS TRANSACTION READ " + ( isReadOnly ? "ONLY" : "WRITE" );
      }

      protected override String GetSQLForSettingTransactionIsolationLevel( TransactionIsolationLevel level )
      {
         this.ThrowIfInTransaction( "default transaction isolation level" );
         String levelString;
         switch ( level )
         {
            case TransactionIsolationLevel.ReadUncommitted:
               levelString = READ_UNCOMMITTED;
               break;
            case TransactionIsolationLevel.ReadCommitted:
               levelString = READ_COMMITTED;
               break;
            case TransactionIsolationLevel.RepeatableRead:
               levelString = REPEATABLE_READ;
               break;
            case TransactionIsolationLevel.Serializable:
               levelString = SERIALIZABLE;
               break;
            default:
               throw new ArgumentException( "Unsupported isolation level: " + level + "." );
         }
         return levelString;
      }

      protected override async Task<Boolean> InterpretReadOnly( DataColumn row )
      {
         return String.Equals( (String) ( await row.TryGetValueAsync() ).Result, "on", StringComparison.OrdinalIgnoreCase );
      }

      protected override async Task<TransactionIsolationLevel> InterpretTransactionIsolationLevel( DataColumn row )
      {
         TransactionIsolationLevel retVal;
         String levelString;
         switch ( ( levelString = (String) ( await row.TryGetValueAsync() ).Result ) )
         {
            case READ_UNCOMMITTED:
               retVal = TransactionIsolationLevel.ReadUncommitted;
               break;
            case READ_COMMITTED:
               retVal = TransactionIsolationLevel.ReadCommitted;
               break;
            case REPEATABLE_READ:
               retVal = TransactionIsolationLevel.RepeatableRead;
               break;
            case SERIALIZABLE:
               retVal = TransactionIsolationLevel.Serializable;
               break;
            default:
               throw new ArgumentException( $"Unrecognied transaction isolation level from backend: \"{levelString}\"." );
         }
         return retVal;
      }

      private void ThrowIfInTransaction( String what )
      {
         if ( ( (PostgreSQLProtocol) this.ConnectionFunctionality ).LastSeenTransactionStatus != TransactionStatus.Idle )
         {
            throw new NotSupportedException( "Can not change " + what + " while in middle of transaction." );
         }
      }


   }

   internal sealed class PgSQLConnectionVendorFunctionality : SQLConnectionVendorFunctionalitySU<PgSQLConnection, PgSQLConnectionCreationInfo, PostgreSQLProtocol>
   {
      public PgSQLConnectionVendorFunctionality()
      {
         this.StandardConformingStrings = true;
      }

      public override void AppendEscapedLiteral( StringBuilder builder, String literal )
      {
         ArgumentValidator.ValidateNotNull( "Builder", builder );

         if ( this.StandardConformingStrings )
         {
            // Escape only single-quotes by doubling
            foreach ( var ch in literal )
            {
               builder.Append( ch );
               if ( ch == '\'' )
               {
                  builder.Append( ch );
               }
            }
         }
         else
         {
            // Escape both backslashes and single-quotes by doubling
            foreach ( var ch in literal )
            {
               builder.Append( ch );
               if ( ch == '\'' || ch == '\\' )
               {
                  builder.Append( ch );
               }
            }
         }
      }

      protected override StatementBuilder CreateStatementBuilder( String sql, Int32[] parameterIndices )
      {
         return new PgSQLStatementBuilder( sql, parameterIndices );
      }

      protected override Boolean TryParseStatementSQL( String sql, out Int32[] parameterIndices )
      {
         Int32 charsRead;
         using ( var reader = new StringReader( sql ) )
         {
            charsRead = Parser.ParseStringForNextSQLStatement( reader, this.StandardConformingStrings, out parameterIndices );
         }

         return AllSpaces( sql, charsRead );

      }

      public Boolean StandardConformingStrings { get; set; }

      private static Boolean AllSpaces( String sql, Int32 startIdxInclusive )
      {
         var retVal = true;
         for ( var i = startIdxInclusive; i < sql.Length && retVal; ++i )
         {
            if ( !Parser.IsSpace( sql[i] ) )
            {
               retVal = false;
            }
         }

         return retVal;
      }

      protected override async Task<PostgreSQLProtocol> CreateConnectionFunctionality(
         PgSQLConnectionCreationInfo parameters,
         CancellationToken token
         )
      {
         var tuple = await PostgreSQLProtocol.PerformStartup(
            this,
            parameters,
            token
            );
         // TODO event: StartupNoticeOccurred
         return tuple.Protocol;
      }

      protected override Task<PgSQLConnection> CreateConnection( PostgreSQLProtocol functionality )
      {
         return Task.FromResult<PgSQLConnection>( new PgSQLConnectionImpl( this, functionality, new PgSQLDatabaseMetaData( this, functionality ) ) );
      }

      protected override ConnectionAcquireInfo<PgSQLConnection> CreateConnectionAcquireInfo( PostgreSQLProtocol functionality, PgSQLConnection connection )
      {
         return new PgSQLConnectionAcquireInfo(
            connection,
            functionality,
            functionality.Stream
            );
      }

      protected override IDisposable ExtractStreamOnConnectionAcquirementError( PostgreSQLProtocol functionality, PgSQLConnection connection, CancellationToken token, Exception error )
      {
         return functionality?.Stream;
      }
   }

   //internal sealed class PgSQLIterationArguments : IterationArgumentsSU
   //{
   //   // TODO create a copy of statement builder, to prevent modifications..
   //   public PgSQLIterationArguments( StatementBuilder statement )
   //      : base( statement )
   //   {
   //      //this.IsSimple = statement.IsSimple();
   //   }

   //   //public PgSQLIterationArguments( PgSQLDataRow row, PgSQLException[] warnings, Boolean isSimple )
   //   //   : base( row, warnings )
   //   //{
   //   //   this.IsSimple = isSimple;
   //   //}

   //   //public PgSQLIterationArguments( Int32 affectedRows, PgSQLException[] warnings, Boolean isSimple )
   //   //   : base( affectedRows, warnings )
   //   //{
   //   //   this.IsSimple = isSimple;
   //   //}

   //   //public PgSQLIterationArguments( Int32[] affectedRows, PgSQLException[] warnings, Boolean isSimple )
   //   //   : base( affectedRows, warnings )
   //   //{
   //   //   this.IsSimple = isSimple;
   //   //}

   //   //public Boolean IsSimple { get; }
   //}
}
