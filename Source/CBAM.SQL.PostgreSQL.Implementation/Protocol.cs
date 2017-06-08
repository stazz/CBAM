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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UtilPack;
using CBAM.SQL.PostgreSQL.Implementation;
using CBAM.SQL.PostgreSQL;
using System.Net;
using TBoundTypeInfo = System.ValueTuple<CBAM.SQL.PostgreSQL.PgSQLTypeFunctionality, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using CBAM.Abstractions;

using MessageIOArgs = System.ValueTuple<CBAM.SQL.PostgreSQL.BackendABIHelper, System.IO.Stream, System.Threading.CancellationToken, UtilPack.ResizableArray<System.Byte>>;
using CBAM.Abstractions.Implementation;
using CBAM.Tabular.Implementation;

#if !NETSTANDARD1_0
using System.Net.Sockets;
#endif

namespace CBAM.SQL.PostgreSQL.Implementation
{
   using TStatementExecutionSimpleTaskParameter = System.ValueTuple<SQLStatementExecutionResult, CBAM.Abstractions.Implementation.MoveNextAsyncDelegate<SQLStatementExecutionResult>>;

   internal sealed partial class PostgreSQLProtocol : SQLConnectionFunctionalitySU
   {


      private Int32 _lastSeenTransactionStatus;
      private readonly IDictionary<String, String> _serverParameters;
      //private Int32 _standardConformingStrings;
      private readonly Version _serverVersion;
      private readonly PgSQLConnectionVendorFunctionality _vendorFunctionality;

      public PostgreSQLProtocol(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         Boolean disableBinaryProtocolSend,
         Boolean disableBinaryProtocolReceive,
         BackendABIHelper messageIOArgs,
         Stream stream,
         ResizableArray<Byte> buffer,
         IDictionary<String, String> serverParameters,
         TransactionStatus status,
         Int32 backendPID
#if !NETSTANDARD1_0
         , Socket socket
#endif
         )
      {
         this._vendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
         this.DisableBinaryProtocolSend = disableBinaryProtocolSend;
         this.DisableBinaryProtocolReceive = disableBinaryProtocolReceive;
         this.MessageIOArgs = ArgumentValidator.ValidateNotNull( nameof( messageIOArgs ), messageIOArgs );
         this.Stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
#if !NETSTANDARD1_0
         this.Socket = socket;
#endif
         this.Buffer = buffer ?? new ResizableArray<Byte>( 8, exponentialResize: true );
         this.DataRowColumnSizes = new ResizableArray<ResettableTransformable<Int32?, Int32>>( exponentialResize: false );
         this._serverParameters = ArgumentValidator.ValidateNotNull( nameof( serverParameters ), serverParameters );
         this.TypeRegistry = new TypeRegistryImpl( vendorFunctionality, this );

         if ( serverParameters.TryGetValue( "server_version", out var serverVersionString ) )
         {
            // Parse server version
            var i = 0;
            var version = serverVersionString.Trim();
            while ( i < version.Length && ( Char.IsDigit( version[i] ) || version[i] == '.' ) )
            {
               ++i;
            }
            this._serverVersion = new Version( version.Substring( 0, i ) );

         }

         // Min supported version is 8.4.
         var serverVersion = this._serverVersion;
         if ( serverVersion != null && ( serverVersion.Major < 8 || ( serverVersion.Major == 8 && serverVersion.Minor < 4 ) ) )
         {
            throw new PgSQLException( "Unsupported server version: " + serverVersion + "." );
         }
         this.LastSeenTransactionStatus = status;
         this.BackendProcessID = backendPID;
      }

      public TypeRegistryImpl TypeRegistry { get; }

      public Int32 BackendProcessID { get; }

      protected override ReservedForStatement CreateReservationObject( StatementBuilder stmt )
      {
         return new PgReservedForStatement( stmt.IsSimple(), stmt.HasBatchParameters() ? "cbam_statement" : null );
      }

      protected override void ValidateStatementOrThrow( StatementBuilder statement )
      {
         if ( statement.BatchParameterCount > 1 )
         {
            // Verify that all columns have same typeIDs
            var first = statement
               .GetParameterEnumerable( 0 )
               .Select( param => this.TypeRegistry.GetTypeInfo( param.ParameterCILType ).BoundData.TypeID )
               .ToArray();
            var max = statement.BatchParameterCount;
            for ( var i = 1; i < max; ++i )
            {
               var j = 0;
               foreach ( var param in statement.GetParameterEnumerable( i ) )
               {
                  if ( first[j] != this.TypeRegistry.GetTypeInfo( param.ParameterCILType ).BoundData.TypeID )
                  {
                     throw new PgSQLException( "When using batch parameters, columns must have same type IDs for all batch rows." );
                  }
                  ++j;
               }
            }
         }
      }

      private static (Int32[] ParameterIndices, TBoundTypeInfo[] TypeInfos, Int32[] TypeIDs) GetVariablesForExtendedQuerySequence(
         StatementBuilder stmt,
         TypeRegistry typeRegistry,
         Func<StatementBuilder, Int32, StatementParameter> paramExtractor
         )
      {
         var pCount = stmt.SQLParameterCount;
         TBoundTypeInfo[] typeInfos;
         Int32[] typeIDs;
         if ( pCount > 0 )
         {
            typeInfos = new TBoundTypeInfo[pCount];
            typeIDs = new Int32[pCount];
            for ( var i = 0; i < pCount; ++i )
            {
               var param = paramExtractor( stmt, i );
               var typeInfo = typeRegistry.GetTypeInfo( param.ParameterCILType ); ;
               typeInfos[i] = typeInfo;
               typeIDs[i] = typeInfo.BoundData?.TypeID ?? 0;
            }
         }
         else
         {
            typeInfos = Empty<TBoundTypeInfo>.Array;
            typeIDs = Empty<Int32>.Array;
         }

         return (( (PgSQLStatementBuilder) stmt ).ParameterIndices, typeInfos, typeIDs);
      }

      private MessageIOArgs GetIOArgs( ResizableArray<Byte> bufferToUse = null, CancellationToken? tokenToUse = null )
      {
         return (this.MessageIOArgs, this.Stream, tokenToUse ?? this.CurrentCancellationToken, bufferToUse ?? this.Buffer);
      }

      protected override async Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsBatch( StatementBuilder statement, ReservedForStatement reservedState )
      {
         // TODO somehow make statement name and chunk size parametrizable
         (var parameterIndices, var typeInfos, var typeIDs) = GetVariablesForExtendedQuerySequence( statement, this.TypeRegistry, ( stmt, idx ) => stmt.GetBatchParameterInfo( 0, idx ) );
         var ioArgs = this.GetIOArgs();
         var stmtName = ( (PgReservedForStatement) reservedState ).StatementName;
         var chunkSize = 1000;

         // Send a parse message with statement name
         await new ParseMessage( statement.SQL, parameterIndices, typeIDs, stmtName ).SendMessageAsync( ioArgs, true );

         // Now send describe message 
         await new DescribeMessage( true, stmtName ).SendMessageAsync( ioArgs, true );

         // And then Flush message for backend to send responses
         await FrontEndMessageWithNoContent.FLUSH.SendMessageAsync( ioArgs, false );

         // Receive first batch of messages
         BackendMessageObject msg = null;
         SQLStatementExecutionResult current = null;
         List<PgSQLError> notices = new List<PgSQLError>();
         var sendBatch = true;
         while ( msg == null )
         {
            msg = ( await this.ReadMessagesUntilMeaningful( notices ) ).Item1;
            switch ( msg )
            {
               case MessageWithNoContents nc:
                  switch ( nc.Code )
                  {
                     case BackendMessageCode.ParseComplete:
                        // Continue reading messages
                        msg = null;
                        break;
                     case BackendMessageCode.EmptyQueryResponse:
                        // The statement does not produce any data, we are done
                        sendBatch = false;
                        break;
                     case BackendMessageCode.NoData:
                        // Do nothing, thus causing batch messages to be sent
                        break;
                     default:
                        throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
                  }
                  break;
               case RowDescription rd:
                  throw new PgSQLException( "Batch statements may only be used for non-query statements." );
               case ParameterDescription pd:
                  if ( !ArrayEqualityComparer<Int32>.ArrayEquality( pd.ObjectIDs, typeIDs ) )
                  {
                     throw new PgSQLException( "Backend required certain amount of parameters, but either they were not supplied, or were of wrong type." );
                  }
                  // Continue to RowDescription/NoData message
                  msg = null;
                  break;
               default:
                  throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
            }
         }

         if ( sendBatch )
         {
            var batchCount = statement.BatchParameterCount;
            var affectedRowsArray = new Int32[batchCount];
            // Send and receive messages asynchronously
            var commandTag = new String[1];
            await
#if NET40
               TaskEx
#else
               Task
#endif
               .WhenAll(
               this.SendMessagesForBatch( statement, typeInfos, stmtName, ioArgs, chunkSize, batchCount ),
               this.ReceiveMessagesForBatch( notices, affectedRowsArray, commandTag )
               );
            current = new BatchCommandExecutionResultImpl(
               commandTag[0],
               new Lazy<SQLException[]>( () => notices?.Select( n => new PgSQLException( n ) )?.ToArray() ),
               affectedRowsArray
               );
         }

         return (current, null);
      }

      private async Task SendMessagesForBatch(
         StatementBuilder statement,
         TBoundTypeInfo[] typeInfos,
         String statementName,
         MessageIOArgs ioArgs,
         Int32 chunkSize,
         Int32 batchCount
         )
      {
         var singleRowParamCount = statement.SQLParameterCount;
         Int32 max;
         var execMessage = new ExecuteMessage();
         for ( var i = 0; i < batchCount; i = max )
         {
            max = Math.Min( batchCount, i + chunkSize );
            for ( var j = i; j < max; ++j )
            {
               // Send Bind and Execute messages
               // TODO reuse BindMessage -> add Reset method.
               await new BindMessage(
                  statement.GetParameterEnumerable( j ),
                  singleRowParamCount,
                  typeInfos,
                  this.DisableBinaryProtocolSend,
                  this.DisableBinaryProtocolReceive,
                  statementName: statementName
                  ).SendMessageAsync( ioArgs, true );
               await execMessage.SendMessageAsync( ioArgs, true );
            }

            // Now send flush message for backend to start sending results back
            await FrontEndMessageWithNoContent.FLUSH.SendMessageAsync( ioArgs, false );
         }
      }

      private async Task ReceiveMessagesForBatch(
         List<PgSQLError> notices,
         Int32[] affectedRows,
         String[] commandTag // This is fugly, but other option is to make both ReceiveMessagesForBatch and SendMessagesForBatch return Task<String>, and then only use the result of the ReceiveMessagesForBatch (since they are both given to Task.WhenAll)
         )
      {
         // We must allocate new buffer, since the reading will be done concurrently while the writing still performs
         // Furthermore, if some error is occurred during sending task, the backend will send error response right away.
         var buffer = new ResizableArray<Byte>( initialSize: 8 );

         for ( var i = 0; i < affectedRows.Length; ++i )
         {
            var msg = ( await this.ReadMessagesUntilMeaningful( notices, bufferToUse: buffer ) ).Item1;
            if ( msg is MessageWithNoContents nc && msg.Code == BackendMessageCode.BindComplete )
            {
               // Bind was sucessul - now read result of execute message
               msg = ( await this.ReadMessagesUntilMeaningful( notices, bufferToUse: buffer ) ).Item1;
               if ( msg is CommandComplete cc )
               {
                  Interlocked.Exchange( ref affectedRows[i], cc.AffectedRows ?? 0 );
                  if ( commandTag[0] == null )
                  {
                     Interlocked.Exchange( ref commandTag[0], cc.CommandTag );
                  }
               }
               else
               {
                  throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
               }
            }
            else
            {
               throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
            }
         }
      }

      protected override async Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsPrepared( StatementBuilder statement, ReservedForStatement reservedState )
      {
         (var parameterIndices, var typeInfos, var typeIDs) = GetVariablesForExtendedQuerySequence( statement, this.TypeRegistry, ( stmt, idx ) => stmt.GetParameterInfo( idx ) );
         var ioArgs = this.GetIOArgs();

         // First, send the parse message
         await new ParseMessage( statement.SQL, parameterIndices, typeIDs ).SendMessageAsync( ioArgs, true );

         // Then send bind message
         var bindMsg = new BindMessage( statement.GetParameterEnumerable(), parameterIndices.Length, typeInfos, this.DisableBinaryProtocolSend, this.DisableBinaryProtocolReceive );
         await bindMsg.SendMessageAsync( ioArgs, true );

         // Then send describe message
         await new DescribeMessage( false ).SendMessageAsync( ioArgs, true );

         // Then execute message
         await new ExecuteMessage().SendMessageAsync( ioArgs, true );

         // Then flush in order to receive response
         await FrontEndMessageWithNoContent.FLUSH.SendMessageAsync( ioArgs, false );

         // Start receiving messages
         BackendMessageObject msg = null;
         SQLStatementExecutionResult current = null;
         MoveNextAsyncDelegate<SQLStatementExecutionResult> moveNext = null;
         RowDescription seenRD = null;
         List<PgSQLError> notices = new List<PgSQLError>();
         while ( msg == null )
         {
            msg = ( await this.ReadMessagesUntilMeaningful( notices ) ).Item1;
            switch ( msg )
            {
               case MessageWithNoContents nc:
                  switch ( nc.Code )
                  {
                     case BackendMessageCode.ParseComplete:
                     case BackendMessageCode.BindComplete:
                     case BackendMessageCode.NoData:
                        // Continue reading messages
                        msg = null;
                        break;
                     case BackendMessageCode.EmptyQueryResponse:
                        // The statement does not produce any data, we are done
                        break;
                     default:
                        throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
                  }
                  break;
               case RowDescription rd:
                  // 0..* DataRowObjects incoming...
                  seenRD = rd;
                  msg = null;
                  break;
               case DataRowObject dr:
                  var streamArray = new PgSQLDataRowStream[seenRD.Fields.Length];
                  var mdArray = new PgSQLDataColumnMetaDataImpl[streamArray.Length];
                  for ( var i = 0; i < streamArray.Length; ++i )
                  {
                     var curField = seenRD.Fields[i];
                     var curMD = new PgSQLDataColumnMetaDataImpl( curField.dataTypeID, this.TypeRegistry.GetTypeInfo( curField.dataTypeID ), curField.name );
                     var curStream = new PgSQLDataRowStream( curMD, i, streamArray, this, reservedState, seenRD );
                     streamArray[i] = curStream;
                     curStream.Reset( dr );
                     mdArray[i] = curMD;
                  }
                  var warningsLazy = LazyFactory.NewReadOnlyResettableLazy<SQLException[]>( () => notices?.Select( n => new PgSQLException( n ) )?.ToArray(), LazyThreadSafetyMode.ExecutionAndPublication );
                  var dataRowCurrent = new SQLDataRowImpl(
                        new PgSQLDataRowMetaDataImpl( mdArray ),
                        streamArray,
                        warningsLazy
                        );
                  current = dataRowCurrent;
                  moveNext = async () => await this.MoveNextAsync( reservedState, streamArray, notices, dataRowCurrent, warningsLazy );
                  break;
               case CommandComplete cc:
                  if ( seenRD == null )
                  {
                     current = new SingleCommandExecutionResultImpl(
                        cc.CommandTag,
                        new Lazy<SQLException[]>( () => notices?.Select( n => new PgSQLException( n ) )?.ToArray() ),
                        cc.AffectedRows ?? 0
                        );
                  }
                  break;
               default:
                  throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
            }
         }

         return (current, moveNext);
      }

      protected override async Task<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsSimple(
         StatementBuilder stmt,
         ReservedForStatement reservedState
         )
      {
         // Send Query message
         await new QueryMessage( stmt.SQL ).SendMessageAsync( this.GetIOArgs() );

         // Then wait for appropriate response
         List<PgSQLError> notices = new List<PgSQLError>();
         MoveNextAsyncDelegate<SQLStatementExecutionResult> drMoveNext = null;

         // We have to always set moveNext, since we might be executing arbitrary amount of SQL statements in simple StatementBuilder.
         MoveNextAsyncDelegate<SQLStatementExecutionResult> moveNext = async () =>
         {
            SQLStatementExecutionResult current = null;
            if ( drMoveNext != null )
            {
               // We are iterating over some query result, check that first.
               var drNext = await drMoveNext();
               if ( drNext.Item1 )
               {
                  current = drNext.Item2;
               }
               else
               {
                  drMoveNext = null;
               }
            }

            if ( current == null )
            {
               BackendMessageObject msg = null;
               RowDescription seenRD = null;
               while ( msg == null )
               {
                  msg = ( await this.ReadMessagesUntilMeaningful( notices ) ).Item1;

                  switch ( msg )
                  {
                     case CommandComplete cc:
                        if ( seenRD == null )
                        {
                           current = new SingleCommandExecutionResultImpl(
                              cc.CommandTag,
                              new Lazy<SQLException[]>( () => notices?.Select( n => new PgSQLException( n ) )?.ToArray() ),
                              cc.AffectedRows ?? 0
                              );
                        }
                        else
                        {
                           // RowDescription followed immediately by CommandComplete -> treat as empty query
                           // Read more
                           msg = null;
                        }
                        seenRD = null;
                        break;
                     case RowDescription rd:
                        seenRD = rd;
                        // Read more (DataRow or CommandComplete)
                        msg = null;
                        break;
                     case DataRowObject dr:
                        // First DataRowObject
                        var streamArray = new PgSQLDataRowStream[seenRD.Fields.Length];
                        var mdArray = new PgSQLDataColumnMetaDataImpl[streamArray.Length];
                        for ( var i = 0; i < streamArray.Length; ++i )
                        {
                           var curField = seenRD.Fields[i];
                           var curMD = new PgSQLDataColumnMetaDataImpl( curField.dataTypeID, this.TypeRegistry.GetTypeInfo( curField.dataTypeID ), curField.name );
                           var curStream = new PgSQLDataRowStream( curMD, i, streamArray, this, reservedState, seenRD );
                           streamArray[i] = curStream;
                           curStream.Reset( dr );
                           mdArray[i] = curMD;
                        }
                        var warningsLazy = LazyFactory.NewReadOnlyResettableLazy<SQLException[]>( () => notices?.Select( n => new PgSQLException( n ) )?.ToArray(), LazyThreadSafetyMode.ExecutionAndPublication );
                        var dataRowCurrent = new SQLDataRowImpl(
                              new PgSQLDataRowMetaDataImpl( mdArray ),
                              streamArray,
                              warningsLazy
                              );
                        current = dataRowCurrent;
                        drMoveNext = async () => await this.MoveNextAsync( reservedState, streamArray, notices, dataRowCurrent, warningsLazy );
                        break;
                     case ReadyForQuery rfq:
                        ( (PgReservedForStatement) reservedState ).RFQSeen();
                        break;
                     default:
                        if ( !ReferenceEquals( MessageWithNoContents.EMPTY_QUERY, msg ) )
                        {
                           throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
                        }
                        // Read more
                        msg = null;
                        break;
                  }
               }
            }

            return (current != null, current);
         };

         var firstResult = await moveNext();

         return (firstResult.Item1 ? firstResult.Item2 : null, moveNext);

      }

      private async Task<(Boolean Success, SQLStatementExecutionResult)> MoveNextAsync(
         ReservedForStatement reservationObject,
         PgSQLDataRowStream[] streams,
         List<PgSQLError> notices,
         SQLDataRowImpl dataRow,
         ReadOnlyResettableLazy<SQLException[]> warningsLazy
         )
      {
         return await this.UseStreamWithinStatementAsync( reservationObject, async () =>
         {
            // Force read of all columns
            foreach ( var colStream in streams )
            {
               await colStream.SkipBytesAsync( false );
            }

            notices.Clear();
            var msg = ( await this.ReadMessagesUntilMeaningful( notices ) ).Item1;
            var dr = msg as DataRowObject;
            foreach ( var stream in streams )
            {
               stream.Reset( dr );
            }

            var retVal = dr != null;
            warningsLazy.Reset();
            return (retVal, dataRow);
         } );
      }


      public TransactionStatus LastSeenTransactionStatus
      {
         get
         {
            return (TransactionStatus) this._lastSeenTransactionStatus;
         }
         private set
         {
            Interlocked.Exchange( ref this._lastSeenTransactionStatus, (Int32) value );
         }
      }

      //public Boolean StandardConformingStrings
      //{
      //   get
      //   {
      //      return Convert.ToBoolean( this._standardConformingStrings );
      //   }
      //   set
      //   {
      //      Interlocked.Exchange( ref this._standardConformingStrings, Convert.ToInt32( value ) );
      //   }
      //}

      protected override async Task PerformDisposeStatementAsync(
         ReservedForStatement reservationObject
         )
      {
         var ioArgs = this.GetIOArgs();
         var pgReserved = (PgReservedForStatement) reservationObject;
         if ( !String.IsNullOrEmpty( pgReserved.StatementName ) )
         {
            // Need to close our named statement
            await new CloseMessage( true, pgReserved.StatementName ).SendMessageAsync( ioArgs, true );
         }

         // Simple statement already received RFQ in its MoveNext method
         if ( !pgReserved.IsSimple )
         {
            // Need to send SYNC
            await FrontEndMessageWithNoContent.SYNC.SendMessageAsync( ioArgs );

         }

         if ( !pgReserved.RFQEncountered )
         {
            // Then wait for RFQ
            // This happens for non-simple statements, or simple statements which cause exception when iterated over.
            BackendMessageObject msg; Int32 remaining;
            while ( ( (msg, remaining) = ( await this.ReadMessagesUntilMeaningful( null, dontThrowExceptions: true ) ) ).Item1.Code != BackendMessageCode.ReadyForQuery )
            {
               if ( remaining > 0 )
               {
                  ioArgs.Item4.CurrentMaxCapacity = remaining;
                  await ioArgs.Item2.ReadSpecificAmountAsync( ioArgs.Item4.Array, 0, remaining, ioArgs.Item3 );
               }
            }
         }
      }

      public BackendABIHelper MessageIOArgs { get; }

      public ResizableArray<Byte> Buffer { get; }

      public Stream Stream { get; }

#if !NETSTANDARD1_0
      public Socket Socket { get; }
#endif

      public ResizableArray<ResettableTransformable<Int32?, Int32>> DataRowColumnSizes { get; }

      public Boolean DisableBinaryProtocolSend { get; }
      public Boolean DisableBinaryProtocolReceive { get; }

      public event EventHandler<NotificationEventArgs> NotifyEvent;

      public async Task<Object> ConvertFromBytes( Int32 typeID, DataFormat dataFormat, Stream stream, Int32 byteCount )
      {
         if ( this.TypeRegistry.TryGetTypeInfo( typeID, out (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) typeInfo ) )
         {

            var limitedStream = StreamFactory.CreateLimitedReader(
                  this.Stream,
                  byteCount,
                  this.CurrentCancellationToken,
                  this.Buffer
                  );

            try
            {
               return await typeInfo.UnboundInfo.ReadBackendValue(
                  dataFormat,
                  typeInfo.BoundData,
                  this.MessageIOArgs,
                  limitedStream
                  );
            }
            finally
            {
               try
               {
                  await limitedStream.SkipThroughRemainingBytes();
               }
               catch
               {
                  // Ignore this one.
               }

            }

         }
         else if ( dataFormat == DataFormat.Text )
         {
            // Initial type load, or unknown type and format is textual
            await this.Stream.ReadSpecificAmountAsync( this.Buffer, 0, byteCount, this.CurrentCancellationToken );
            return this.MessageIOArgs.Encoding.Encoding.GetString( this.Buffer.Array, 0, byteCount );
         }
         else
         {
            // Unknown type, and data format is binary.
            throw new PgSQLException( $"The type ID {typeID} is not known." );
         }
      }

      internal async Task<(BackendMessageObject, Int32)> ReadMessagesUntilMeaningful(
         List<PgSQLError> notices,
         Func<Boolean> checkReadForNextMessage = null,
         ResizableArray<Byte> bufferToUse = null,
         Boolean dontThrowExceptions = false
      )
      {
         Boolean encounteredMeaningful;
         var ioArgs = this.GetIOArgs( bufferToUse );
         BackendMessageObject msg;
         Int32 remaining;
         do
         {
            (msg, remaining) = await BackendMessageObject.ReadBackendMessageAsync( ioArgs, this.DataRowColumnSizes );
            switch ( msg )
            {
               case PgSQLErrorObject errorObject:
                  encounteredMeaningful = false;
                  if ( errorObject.Code == BackendMessageCode.NoticeResponse )
                  {
                     if ( notices != null )
                     {
                        notices.Add( ( (PgSQLErrorObject) msg ).Error );
                     }
                  }
                  else if ( !dontThrowExceptions )
                  {
                     throw new PgSQLException( ( (PgSQLErrorObject) msg ).Error );
                  }
                  break;
               case NotificationMessage notification:
                  try
                  {
#if DEBUG
                     this.NotifyEvent?.Invoke( null, notification.Args );
#else
                     this.NotifyEvent.InvokeAllEventHandlers( evt => evt( null, notification.Args ), throwExceptions: false );
#endif
                  }
                  catch
                  {
                     // Ignore
                  }
                  encounteredMeaningful = false;
                  break;
               case ParameterStatus ps:
                  this._serverParameters[ps.Name] = ps.Value;
                  encounteredMeaningful = false;
                  break;
               default:
                  {
                     if ( msg is ReadyForQuery rfq )
                     {
                        this.LastSeenTransactionStatus = rfq.Status;
                     }
                     encounteredMeaningful = true;
                     break;
                  }

            }
         } while ( !encounteredMeaningful && ( checkReadForNextMessage?.Invoke() ?? true ) );

         return (msg, remaining);
      }

      public async Task PerformClose( CancellationToken token )
      {
         // Send termination message
         // Don't use this.CurrentCancellationToken, since one-time pool has already reset the token.
         // Furthermore, we might come here from other entrypoints than connection pool's UseConnection (e.g. when disposing caching connection pool)
         await FrontEndMessageWithNoContent.TERMINATION.SendMessageAsync( this.GetIOArgs( tokenToUse: token ) );
      }

      public async Task CheckNotificationsAsync()
      {
#if !NETSTANDARD1_0
         var socket = this.Socket;
         if ( socket == null )
         {
#endif
         // Just do "SELECT 1"; If we get any notifications 
         await this.CreateIterationArguments( this._vendorFunctionality.CreateStatementBuilder( "SELECT 1" ) ).EnumerateAsync( null );
#if !NETSTANDARD1_0
         }
         else
         {
            // First, check from the socket that we have any data pending
            Boolean SocketHasDataPending()
            {
               return socket.Available > 0 || socket.Poll( 1, SelectMode.SelectRead ) || socket.Available > 0;
            };
            if ( SocketHasDataPending() )
            {
               // There is pending data
               await this.UseStreamOutsideStatementAsync( async () =>
               {
                  await this.ReadMessagesUntilMeaningful(
                     null,
                     SocketHasDataPending
                     );
               } );
            }
            else
            {
               // TODO check if this connection is free to use.
            }
         }
#endif
      }

#if !NETSTANDARD1_0
      internal static async Task<(PostgreSQLProtocol Protocol, List<PgSQLError> notices)> PerformStartup(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         PgSQLConnectionCreationInfo creationParameters,
         CancellationToken token
         )
      {
         IPAddress GetAddressFromHost( String hostToParse )
         {
            if ( !IPAddress.TryParse( hostToParse, out IPAddress thisAddress ) )
            {
               thisAddress = creationParameters.DNS?.Invoke( hostToParse );
            }

            return thisAddress;
         }

         var creationData = creationParameters.CreationData;
         var connectionConfig = creationData?.Connection ?? throw new ArgumentException( "Please specify connection configuration." );
         var remoteHost = connectionConfig?.Host ?? throw new ArgumentException( "Please specify remote host in connection configuration." );
         var remoteAddress = GetAddressFromHost( remoteHost );
         if ( remoteAddress == null )
         {
            throw new InvalidOperationException( "No remote address supplied, either via host property, or via DNS event." );
         }

         var remoteEP = new IPEndPoint( remoteAddress, connectionConfig?.Port ?? throw new ArgumentException( "Please specify remote port in connection configuration." ) );

         Socket CreateSocket()
         {
            return new Socket(
            remoteAddress.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp
            );
         }
         var localEP = !String.IsNullOrEmpty( connectionConfig.LocalHost ) && connectionConfig.LocalPort > 0 ?
            new IPEndPoint( GetAddressFromHost( connectionConfig.LocalHost ), connectionConfig.LocalPort ) :
            null;

         async Task<Stream> InitNetworkStream( Socket thisSocket )
         {
            if ( localEP != null )
            {
               thisSocket.Bind( localEP );
            }

            await thisSocket.ConnectAsync( remoteEP );
            return new NetworkStream( thisSocket, true );
         }

         var socket = CreateSocket();
         var errorOccurred = false;
         Stream stream = null;
         try
         {
            stream = await InitNetworkStream( socket );

            var msgArgs = new BackendABIHelper( new UTF8EncodingInfo() );
            var buffer = new ResizableArray<Byte>( initialSize: 8, exponentialResize: true );
            var connectionMode = connectionConfig.ConnectionSSLMode;
            var isSSLRequired = connectionMode == ConnectionSSLMode.Required;
            if ( isSSLRequired || connectionMode == ConnectionSSLMode.Preferred )
            {
               await SSLRequestMessage.INSTANCE.SendMessageAsync( (msgArgs, stream, token, buffer) );

               var response = await msgArgs.ReadByte( stream, buffer, token );
               if ( response == (Byte) 'S' )
               {
                  // Start SSL session
                  Stream sslStream = null;
                  try
                  {
                     var provideSSLStream = creationParameters.ProvideSSLStream;
                     if ( provideSSLStream != null )
                     {
                        var clientCerts = new System.Security.Cryptography.X509Certificates.X509CertificateCollection();
                        creationParameters.ProvideClientCertificates?.Invoke( clientCerts );
                        sslStream = provideSSLStream.Invoke( stream, false, creationParameters.ValidateServerCertificate, creationParameters.SelectLocalCertificate, out AuthenticateAsClientAsync authenticateAsClient );
                        if ( isSSLRequired )
                        {
                           if ( sslStream == null )
                           {
                              throw new PgSQLException( "SSL stream creation callback returned null." );
                           }
                           else if ( authenticateAsClient == null )
                           {
                              throw new PgSQLException( "Authentication callback given by SSL stream creation callback was null." );
                           }
                        }
                        if ( sslStream != null && authenticateAsClient != null )
                        {
                           await authenticateAsClient( sslStream, remoteHost, clientCerts, connectionConfig.SSLProtocols, true )();
                           stream = sslStream;
                        }
                     }
                     else if ( isSSLRequired )
                     {
                        throw new PgSQLException( "Server accepted SSL request, but the creation parameters did not have callback to create SSL stream" );
                     }
                  }
                  catch ( Exception exc )
                  {
                     if ( !isSSLRequired )
                     {
                        // We close SSL stream in case authentication failed.
                        // Closing SSL stream will close underlying stream, which will close the socket...
                        // So we have to reconnect afterwards.
                        ( sslStream ?? stream ).DisposeSafely();
                        // ... so re-create it
                        socket = CreateSocket();
                        stream = await InitNetworkStream( socket );
                     }
                     else
                     {
                        throw new PgSQLException( "Unable to start SSL client.", exc );
                     }
                  }
               }
               else if ( isSSLRequired )
               {
                  // SSL session start was unsuccessful, and it is required -> can not continue
                  throw new PgSQLException( "Server does not support SSL." );
               }
            }
            return await PerformStartup(
               vendorFunctionality,
               creationData.Initialization,
               token,
               stream,
               msgArgs,
               buffer,
               socket
               );

         }
         catch
         {
            errorOccurred = true;
            throw;
         }
         finally
         {
            if ( errorOccurred )
            {
               stream.DisposeSafely();
            }
         }
      }

#endif

      internal static async Task<(PostgreSQLProtocol Protocol, List<PgSQLError> notices)> PerformStartup(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         PgSQLInitializationConfiguration initData,
         CancellationToken token,
         Stream stream
         )
      {
         return await PerformStartup(
            vendorFunctionality,
            initData,
            token,
            stream,
            new BackendABIHelper( new UTF8EncodingInfo() ),
            new ResizableArray<Byte>( initialSize: 8, exponentialResize: true )
#if !NETSTANDARD1_0
            ,null
#endif
            );
      }

      private static async Task<(PostgreSQLProtocol Protocol, List<PgSQLError> notices)> PerformStartup(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         PgSQLInitializationConfiguration initData,
         CancellationToken token,
         Stream stream,
         BackendABIHelper abiHelper,
         ResizableArray<Byte> buffer
#if !NETSTANDARD1_0
         , Socket socket
#endif
         )
      {
         var startupInfo = await DoConnectionInitialization(
            initData?.Database ?? throw new ArgumentException( "Please specify database configuration." ),
            (abiHelper, stream, token, buffer)
            );
         var protoConfig = initData.Protocol;
         var retVal = (
            new PostgreSQLProtocol(
               vendorFunctionality,
               protoConfig?.DisableBinaryProtocolSend ?? false,
               protoConfig?.DisableBinaryProtocolReceive ?? false,
               abiHelper,
               stream,
               buffer,
               startupInfo.ServerParameters,
               startupInfo.TransactionStatus,
               startupInfo.backendProcessID ?? 0
#if !NETSTANDARD1_0
               , socket
#endif
            ),
            startupInfo.Notices ?? new List<PgSQLError>()
            );

         await retVal.Item1.ReadTypesFromServer( protoConfig?.ForceTypeIDLoad ?? false, token );

         return retVal;
      }

      private static async Task<(IDictionary<String, String> ServerParameters, Int32? backendProcessID, Int32? backendKeyData, List<PgSQLError> Notices, TransactionStatus TransactionStatus)> DoConnectionInitialization(
         PgSQLDatabaseConfiguration dbConfig,
         MessageIOArgs ioArgs
         )
      {

         var encoding = ioArgs.Item1.Encoding.Encoding;
         var username = dbConfig.Username ?? throw new ArgumentException( "Please specify username in database configuration." );
         var parameters = new Dictionary<String, String>()
         {
            { "database", dbConfig.Database ?? throw new ArgumentException("Please specify database name in database configuration.") },
            { "user",username },
            { "DateStyle", "ISO" },
            { "client_encoding", encoding.WebName  },
            { "extra_float_digits", "2" },
            { "lc_monetary", "C" }
         };

         await new StartupMessage( 3 << 16, parameters ).SendMessageAsync( ioArgs );

         BackendMessageObject msg;
         List<PgSQLError> notices = null;
         Int32? backendProcessID = null;
         Int32? backendKeyData = null;
         TransactionStatus tStatus = 0;
         do
         {
            Int32 ignored;
            (msg, ignored) = await BackendMessageObject.ReadBackendMessageAsync( ioArgs, null );
            switch ( msg )
            {
               case ParameterStatus ps:
                  parameters[ps.Name] = ps.Value;
                  break;
               case AuthenticationResponse auth:
                  await ProcessAuth(
                     ioArgs,
                     auth,
                     username,
                     String.Equals( PgSQLDatabaseConfiguration.PasswordByteEncoding.WebName, encoding.WebName ) ?
                        dbConfig.PasswordBytes :
                        encoding.GetBytes( dbConfig.Password )
                     );
                  break;
               case PgSQLErrorObject error:
                  if ( error.Code == BackendMessageCode.NoticeResponse )
                  {
                     if ( notices == null )
                     {
                        notices = new List<PgSQLError>();
                     }
                     notices.Add( error.Error );
                  }
                  else
                  {
                     throw new PgSQLException( error.Error );
                  }
                  break;
               case BackendKeyData key:
                  backendProcessID = key.ProcessID;
                  backendKeyData = key.Key;
                  break;
               case ReadyForQuery rfq:
                  tStatus = rfq.Status;
                  break;
            }
         } while ( msg.Code != BackendMessageCode.ReadyForQuery );

         return (parameters, backendProcessID, backendKeyData, notices, tStatus);
      }

      private static async Task ProcessAuth(
         MessageIOArgs ioArgs,
         AuthenticationResponse msg,
         String username,
         Byte[] pw
         )
      {
         var authType = msg.RequestType;
         switch ( authType )
         {
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationClearTextPassword:
               if ( pw == null )
               {
                  throw new PgSQLException( "Backend requested password, but it was not supplied." );
               }
               await new PasswordMessage( pw ).SendMessageAsync( ioArgs );
               break;
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationMD5Password:
               if ( pw == null )
               {
                  throw new PgSQLException( "Backend requested password, but it was not supplied." );
               }

#if NETSTANDARD1_0
               throw new NotImplementedException( "Need to port MD5 to NETStandard1.0 via UtilPack." );
#else

               var buffer = ioArgs.Item4;
               using ( var md5 = System.Security.Cryptography.MD5.Create() )
               {
                  // Extract server salt before using args.Buffer
                  var idx = msg.AdditionalDataInfo.offset;
                  var args = ioArgs.Item1;
                  var serverSalt = buffer.Array.CreateAndBlockCopyTo( ref idx, msg.AdditionalDataInfo.count );

                  // Hash password with username as salt
                  var prehashLength = args.Encoding.Encoding.GetByteCount( username ) + pw.Length;
                  buffer.CurrentMaxCapacity = prehashLength;
                  idx = 0;
                  pw.BlockCopyTo( ref idx, buffer.Array, 0, pw.Length );
                  args.Encoding.Encoding.GetBytes( username, 0, username.Length, buffer.Array, pw.Length );
                  var hash = md5.ComputeHash( buffer.Array, 0, prehashLength );

                  // Write hash as hexadecimal string
                  buffer.CurrentMaxCapacity = hash.Length * 2 * args.Encoding.BytesPerASCIICharacter;
                  idx = 0;
                  foreach ( var hashByte in hash )
                  {
                     args.Encoding.WriteHexDecimal( buffer.Array, ref idx, hashByte );
                  }

                  // Hash result again with server-provided salt
                  buffer.CurrentMaxCapacity += serverSalt.Length;
                  var dummy = 0;
                  serverSalt.BlockCopyTo( ref dummy, buffer.Array, idx, serverSalt.Length );
                  hash = md5.ComputeHash( buffer.Array, 0, idx + serverSalt.Length );

                  // Send back string "md5" followed by hexadecimal hash value
                  buffer.CurrentMaxCapacity = 3 * args.Encoding.BytesPerASCIICharacter + hash.Length * 2 * args.Encoding.BytesPerASCIICharacter;
                  idx = 0;
                  var array = buffer.Array;
                  args.Encoding
                     .WriteASCIIByte( array, ref idx, (Byte) 'm' )
                     .WriteASCIIByte( array, ref idx, (Byte) 'd' )
                     .WriteASCIIByte( array, ref idx, (Byte) '5' );
                  foreach ( var hashByte in hash )
                  {
                     args.Encoding.WriteHexDecimal( array, ref idx, hashByte );
                  }

                  await new PasswordMessage( buffer.Array.CreateBlockCopy( idx ) ).SendMessageAsync( ioArgs );
               }
               break;
#endif
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationOk:
               // Nothing to do
               break;
            default:
               throw new PgSQLException( $"Authentication kind {authType} is not support." );
         }
      }
   }



   internal class PgReservedForStatement : ReservedForStatement
   {
      private Int32 _rfqEncountered;

      public PgReservedForStatement(
         Boolean isSimple,
         String statementName
         )
      {
         this.IsSimple = isSimple;
         this.StatementName = statementName;
         this._rfqEncountered = Convert.ToInt32( false );
      }

      public Boolean IsSimple { get; }

      public String StatementName { get; }

      public Boolean RFQEncountered => Convert.ToBoolean( this._rfqEncountered );

      public void RFQSeen()
      {
         Interlocked.Exchange( ref this._rfqEncountered, Convert.ToInt32( true ) );
      }
   }

#if NET40 || NET45

   // No SocketTaskExtensions in .NET 4.5...
   // TODO move to UtilPack.
   internal static class SocketTaskextensions
   {
      public static Task ConnectAsync( this Socket socket, EndPoint remoteEndPoint )
      {
         //token.ThrowIfCancellationRequested();
         var connectArgs = (socket, remoteEndPoint);
         return Task.Factory.FromAsync(
           ( cArgs, cb, state ) => cArgs.Item1.BeginConnect( cArgs.Item2, cb, state ),
           ( result ) => ( (Socket) result.AsyncState ).EndConnect( result ),
           connectArgs,
           socket
           );
      }
   }

#endif
}
