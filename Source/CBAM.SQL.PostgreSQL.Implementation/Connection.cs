﻿/*
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
using UtilPack.ResourcePooling;
using UtilPack.TabularData;
using System.Net;
using UtilPack.Configuration.NetworkStream;

#if !NETSTANDARD1_0
using UtilPack.ResourcePooling.NetworkStream;
#endif

namespace CBAM.SQL.PostgreSQL.Implementation
{
   using TNetworkStreamInitState = ValueTuple<BackendABIHelper, ResizableArray<Byte>, CancellationToken, Stream>;

   internal sealed class PgSQLConnectionImpl : SQLConnectionImpl<PostgreSQLProtocol, PgSQLConnectionVendorFunctionality>, PgSQLConnection
   {
      private const String TRANSACTION_ISOLATION_PREFIX = "SET SESSION CHARACTERISTICS AS TRANSACTION ISOLATION LEVEL ";

      private const String READ_UNCOMMITTED = TRANSACTION_ISOLATION_PREFIX + "READ UNCOMMITTED";
      private const String READ_COMMITTED = TRANSACTION_ISOLATION_PREFIX + "READ COMMITTED";
      private const String REPEATABLE_READ = TRANSACTION_ISOLATION_PREFIX + "REPEATABLE READ";
      private const String SERIALIZABLE = TRANSACTION_ISOLATION_PREFIX + "SERIALIZABLE";

      public PgSQLConnectionImpl(
         PostgreSQLProtocol functionality,
         DatabaseMetadata metaData
         )
         : base( functionality, metaData )
      {
      }

      //public event GenericEventHandler<NotificationEventArgs> NotificationEvent;

      public Int32 BackendProcessID => this.ConnectionFunctionality.BackendProcessID;

      public TransactionStatus LastSeenTransactionStatus => this.ConnectionFunctionality.LastSeenTransactionStatus;

      public override ValueTask<Boolean> ProcessStatementResultPassively(
         MemorizingPotentiallyAsyncReader<Char?, Char> reader,
         SQLStatementBuilderInformation statementInformation,
         SQLStatementExecutionResult executionResult
         )
      {
         // TODO detect COPY IN result from executionResult, and use reader to read data and send it to backend
         return new ValueTask<Boolean>( false );
      }

      public ValueTask<NotificationEventArgs[]> CheckNotificationsAsync()
      {
         return this.ConnectionFunctionality.CheckNotificationsAsync();

         //         if ( !argsArray.IsNullOrEmpty() )
         //         {
         //            foreach ( var args in argsArray )
         //            {
         //#if DEBUG
         //               this.NotificationEvent?.Invoke( args );
         //#else
         //                     var curArgs = args;
         //                     this.NotificationEvent?.InvokeAllEventHandlers( evt => evt( curArgs ), throwExceptions: false );
         //#endif
         //            }
         //         }

         //         return argsArray?.Length ?? 0;
      }

      public IAsyncEnumerable<NotificationEventArgs> ContinuouslyListenToNotificationsAsync()
      {
         return this.ConnectionFunctionality.ListenToNotificationsAsync();
      }

      public TypeRegistry TypeRegistry => this.ConnectionFunctionality.TypeRegistry;

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

      protected override async ValueTask<Boolean> InterpretReadOnly( AsyncDataColumn row )
      {
         return String.Equals( (String) ( await row.TryGetValueAsync() ).Result, "on", StringComparison.OrdinalIgnoreCase );
      }

      protected override async ValueTask<TransactionIsolationLevel> InterpretTransactionIsolationLevel( AsyncDataColumn row )
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
         if ( this.ConnectionFunctionality.LastSeenTransactionStatus != TransactionStatus.Idle )
         {
            throw new NotSupportedException( "Can not change " + what + " while in middle of transaction." );
         }
      }


   }

   internal sealed class PgSQLConnectionVendorFunctionalityImpl : DefaultConnectionVendorFunctionality, PgSQLConnectionVendorFunctionality
   {
      public PgSQLConnectionVendorFunctionalityImpl()
      {
         this.StandardConformingStrings = true;
      }

      public override String EscapeLiteral( String str )
      {
         if ( !String.IsNullOrEmpty( str ) )
         {
            if ( this.StandardConformingStrings )
            {
               const String STANDARD_ESCAPABLE = "'";
               const String STANDARD_REPLACEABLE = "''";
               if ( str.IndexOf( STANDARD_ESCAPABLE ) >= 0 )
               {
                  str = str.Replace( STANDARD_ESCAPABLE, STANDARD_REPLACEABLE );
               }
            }
            else
            {
               // Escape both backslashes and single-quotes by doubling
               //
               if ( str.IndexOfAny( new[] { '\'', '\\' } ) >= 0 )
               {
                  // Use Regex for now but consider doing manual replacing if performance becomes a problem
                  // C# does not allow replacing any character in string with other as built-in function, so just do this manually
                  var sb = new StringBuilder( str.Length + 5 );
                  foreach ( var ch in str )
                  {
                     sb.Append( ch );
                     if ( ch == '\'' || ch == '\\' )
                     {
                        sb.Append( ch );
                     }
                  }
                  str = sb.ToString();
               }
            }
         }

         return str;
      }

      protected override SQLStatementBuilder CreateStatementBuilder( String sql, Int32[] parameterIndices )
      {
         var paramz = new StatementParameter[parameterIndices?.Length ?? 0];
         var batchParams = new List<StatementParameter[]>();
         var info = new PgSQLStatementBuilderInformation( sql, paramz, batchParams, parameterIndices );
         return new PgSQLStatementBuilder( info, paramz, batchParams );
      }

      protected override Boolean TryParseStatementSQL( String sql, out Int32[] parameterIndices )
      {
         // We accept either:
         // 1. Multiple simple statements, OR
         // 2. Exactly one statement with parameters
         parameterIndices = null;
         var strIdx = new StringIndex( sql );
         var boundReader = ReaderFactory.NewNullablePeekableValueReader(
            StringCharacterReaderLogic.Instance,
            strIdx
            );
         Boolean wasOK;
         do
         {
            // Because how ValueTask works, and since StringCharacterReader never performs any asynchrony, we will always complete synchronously here
            var curParameterIndices = Parser.ParseStringForNextSQLStatement(
               boundReader,
               this.StandardConformingStrings,
               () => strIdx.CurrentIndex - 1
               ).GetAwaiter().GetResult();
            wasOK = curParameterIndices == null || parameterIndices == null;
            parameterIndices = curParameterIndices;
         } while ( wasOK && strIdx.CurrentIndex < sql.Length );

         return wasOK;

      }

      public override async ValueTask<Boolean> TryAdvanceReaderOverSingleStatement( PeekablePotentiallyAsyncReader<Char?> reader )
      {
         await Parser.ParseStringForNextSQLStatement( reader, this.StandardConformingStrings, null );
         return true;
      }

      public ValueTask<Boolean> TryAdvanceReaderOverCopyInStatement( PeekablePotentiallyAsyncReader<Char?> reader )
      {
         // TODO
         return new ValueTask<Boolean>( false );
      }

      // TODO: allow this to reflect the configurable propery of backend.
      public Boolean StandardConformingStrings { get; set; }

      //private static Boolean AllSpaces( String sql, Int32 startIdxInclusive )
      //{
      //   var retVal = true;
      //   for ( var i = startIdxInclusive; i < sql.Length && retVal; ++i )
      //   {
      //      if ( !Parser.IsSpace( sql[i] ) )
      //      {
      //         retVal = false;
      //      }
      //   }

      //   return retVal;
      //}

      //private static void FindCopyInEndFromTextReader( TextReader reader, ref Char[] auxArray )
      //{
      //   const Int32 AUX_ARRAY_LEN = 3;
      //   if ( auxArray == null )
      //   {
      //      // We need 3 characters to detect the end of COPY IN FROM STDIN statement (line-break, \.)
      //      // The following line-break can be validated by using .Peek();
      //      auxArray = new Char[AUX_ARRAY_LEN];
      //   }
      //   Int32 c;
      //   var arrayIndex = 0;
      //   var found = false;
      //   while ( !found && ( c = reader.Read() ) != -1 )
      //   {
      //      // Update auxiliary array
      //      auxArray[arrayIndex] = (Char) c;

      //      if ( CSDBC.Core.PostgreSQL.Implementation.Parser.CheckForCircularlyFilledArray(
      //         COPY_IN_END_CHARS,
      //         auxArray,
      //         arrayIndex
      //         ) )
      //      {
      //         // We've found '\.', now we need to check for line-ends before and after
      //         var peek = reader.Peek();
      //         if ( peek == '\n' || peek == '\r' )
      //         {
      //            // This '\.' is followed by new-line. Now check that it is also preceded by a new-line
      //            var ch = auxArray[( arrayIndex + 1 ) % AUX_ARRAY_LEN];
      //            found = IsNewline( ch );
      //            if ( found )
      //            {
      //               // Consume the linebreak
      //               reader.Read();
      //            }
      //         }
      //      }

      //      if ( arrayIndex == AUX_ARRAY_LEN - 1 )
      //      {
      //         arrayIndex = 0;
      //      }
      //      else
      //      {
      //         ++arrayIndex;
      //      }
      //   }
      //}

      //private static Boolean IsNewline( Char ch )
      //{
      //   return ch == '\n' || ch == '\r';
      //}

   }

   internal sealed class PgSQLConnectionFactory : ConnectionFactorySU<PgSQLConnection, PgSQLConnectionImpl, PgSQLConnectionCreationInfo, PostgreSQLProtocol>
   {
      private readonly BinaryStringPool _stringPool;

#if !NETSTANDARD1_0
      private readonly ReadOnlyResettableAsyncLazy<IPAddress> _remoteAddress;
      private readonly NetworkStreamFactoryConfiguration<TNetworkStreamInitState> _networkStreamConfig;
#endif

      public PgSQLConnectionFactory(
         PgSQLConnectionCreationInfo creationInfo,
         IEncodingInfo encoding
         ) : base( creationInfo )
      {

         this.Encoding = ArgumentValidator.ValidateNotNull( nameof( encoding ), encoding );
         ArgumentValidator.ValidateNotNull( nameof( creationInfo ), creationInfo );

#if NETSTANDARD1_0
         if ( !( creationInfo.CreationData?.Initialization?.ConnectionPool?.ConnectionsOwnStringPool ?? default ) )
         {
            this._stringPool = BinaryStringPoolFactory.NewConcurrentBinaryStringPool( encoding.Encoding );
         }
#else
#if !NETSTANDARD1_0
         (this._networkStreamConfig, this._remoteAddress, this._stringPool) = creationInfo.CreateStatefulNetworkStreamFactoryConfiguration().Create(
            ( socket, stream, token ) => this.CreateNetworkStreamInitState( token, stream ),
            encoding.Encoding,
            async ( state ) =>
            {
               await SSLRequestMessage.INSTANCE.SendMessageAsync( (state.Item1, state.Item4, state.Item3, state.Item2) );

               var response = await state.Item4.ReadByte( state.Item2, state.Item3 );
               return response == (Byte) 'S';
            },
            () => new PgSQLException( "Server accepted SSL request, but the creation parameters did not have callback to create SSL stream" ),
            () => new PgSQLException( "Server does not support SSL." ),
            () => new PgSQLException( "SSL stream creation callback returned null." ),
            () => new PgSQLException( "Authentication callback given by SSL stream creation callback was null." ),
            inner => new PgSQLException( "Unable to start SSL client.", inner )
            );
         this._networkStreamConfig.TransformStreamAfterCreation = stream => new DuplexBufferedAsyncStream( stream );
#endif
#endif
      }

      public override void ResetFactoryState()
      {
         this._stringPool.ClearPool();
#if !NETSTANDARD1_0
         this._remoteAddress.Reset();
#endif
      }

      protected override ValueTask<PgSQLConnectionImpl> CreateConnection( PostgreSQLProtocol functionality )
      {
         return new ValueTask<PgSQLConnectionImpl>( new PgSQLConnectionImpl( functionality, new PgSQLDatabaseMetaData( functionality ) ) );
      }

      protected override AsyncResourceAcquireInfo<PgSQLConnectionImpl> CreateConnectionAcquireInfo( PostgreSQLProtocol functionality, PgSQLConnectionImpl connection )
      {
         return new PgSQLConnectionAcquireInfo(
            connection,
            functionality
            );
      }


      protected override IDisposable ExtractStreamOnConnectionAcquirementError( PostgreSQLProtocol functionality, PgSQLConnectionImpl connection, CancellationToken token, Exception error )
      {
         return functionality?.Stream;
      }

      private IEncodingInfo Encoding { get; }

      private BinaryStringPool GetStringPoolForNewConnection()
      {
         return this._stringPool ?? BinaryStringPoolFactory.NewNotConcurrentBinaryStringPool();
      }

      private TNetworkStreamInitState CreateNetworkStreamInitState( CancellationToken token, Stream stream )
      {
         return (new BackendABIHelper( this.Encoding, this.GetStringPoolForNewConnection() ), new ResizableArray<Byte>( initialSize: 8, exponentialResize: true ), token, stream);
      }

      protected override async ValueTask<PostgreSQLProtocol> CreateConnectionFunctionality(
         CancellationToken token
         )
      {
         var parameters = this.CreationParameters;
         var streamFactory = parameters.StreamFactory;

#if !NETSTANDARD1_0
         System.Net.Sockets.Socket socket;
#endif
         Stream stream;
         TNetworkStreamInitState state;
         if ( streamFactory == null )
         {
#if NETSTANDARD1_0
            throw new ArgumentNullException( nameof( streamFactory ) );
#else
            (socket, stream, state) = await NetworkStreamFactory<TNetworkStreamInitState>.AcquireNetworkStreamFromConfiguration(
                  this._networkStreamConfig,
                  token );
#endif
         }
         else
         {
            (
#if !NETSTANDARD1_0
               socket,
#endif
               stream, state) = (
#if !NETSTANDARD1_0
               null,
#endif
               await streamFactory(), this.CreateNetworkStreamInitState( token, null ));
         }

         (var proto, var warnings) = await PostgreSQLProtocol.PerformStartup(
            new PgSQLConnectionVendorFunctionalityImpl(),
            parameters,
            token,
            stream,
            state.Item1,
            state.Item2
#if !NETSTANDARD1_0
            , socket
#endif
            );

         return proto;
      }
   }

}
