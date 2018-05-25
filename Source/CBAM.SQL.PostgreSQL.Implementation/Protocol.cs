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
using TBoundTypeInfo = System.ValueTuple<System.Type, CBAM.SQL.PostgreSQL.PgSQLTypeFunctionality, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using CBAM.Abstractions;

using MessageIOArgs = System.ValueTuple<CBAM.SQL.PostgreSQL.BackendABIHelper, System.IO.Stream, System.Threading.CancellationToken, UtilPack.ResizableArray<System.Byte>>;
using CBAM.Abstractions.Implementation;
using UtilPack.AsyncEnumeration;
using UtilPack.Cryptography.Digest;
using UtilPack.Cryptography.SASL;
using UtilPack.Cryptography.SASL.SCRAM;

#if !NETSTANDARD1_0
using System.Net.Sockets;
#endif

namespace CBAM.SQL.PostgreSQL.Implementation
{
   using TStatementExecutionSimpleTaskParameter = System.ValueTuple<SQLStatementExecutionResult, Func<ValueTask<(Boolean, SQLStatementExecutionResult)>>>;
   using TSASLAuthState = System.ValueTuple<SASLMechanism, SASLCredentialsSCRAMForClient, ResizableArray<Byte>, IEncodingInfo>;

   internal sealed partial class PostgreSQLProtocol : SQLConnectionFunctionalitySU<PgSQLConnectionVendorFunctionality>
   {


      private Int32 _lastSeenTransactionStatus;
      private readonly IDictionary<String, String> _serverParameters;
      //private Int32 _standardConformingStrings;
      private readonly Version _serverVersion;

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
         ) : base( vendorFunctionality )
      {
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
         this.ServerParameters = new System.Collections.ObjectModel.ReadOnlyDictionary<String, String>( serverParameters );
         this.TypeRegistry = new TypeRegistryImpl( vendorFunctionality, sql => this.PrepareStatementForExecution( vendorFunctionality.CreateStatementBuilder( sql ), out var dummy ) );

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
         this.EnqueuedNotifications = new Queue<NotificationEventArgs>();
      }

      public TypeRegistryImpl TypeRegistry { get; }

      public Int32 BackendProcessID { get; }

      public IReadOnlyDictionary<String, String> ServerParameters { get; }

      protected override ReservedForStatement CreateReservationObject( SQLStatementBuilderInformation stmt )
      {
         return new PgReservedForStatement(
#if DEBUG
            stmt,
#endif
            stmt.IsSimple(),
            stmt.HasBatchParameters() ? "cbam_statement" : null
            );
      }

      protected override void ValidateStatementOrThrow( SQLStatementBuilderInformation statement )
      {
         ArgumentValidator.ValidateNotNull( nameof( statement ), statement );
         if ( statement.BatchParameterCount > 1 )
         {
            // Verify that all columns have same typeIDs
            var first = statement
               .GetParametersEnumerable( 0 )
               .Select( param => this.TypeRegistry.TryGetTypeInfo( param.ParameterCILType ).DatabaseData.TypeID )
               .ToArray();
            var max = statement.BatchParameterCount;
            for ( var i = 1; i < max; ++i )
            {
               var j = 0;
               foreach ( var param in statement.GetParametersEnumerable( i ) )
               {
                  if ( first[j] != this.TypeRegistry.TryGetTypeInfo( param.ParameterCILType ).DatabaseData.TypeID )
                  {
                     throw new PgSQLException( "When using batch parameters, columns must have same type IDs for all batch rows." );
                  }
                  ++j;
               }
            }
         }
      }

      private static (Int32[] ParameterIndices, TypeFunctionalityInformation[] TypeInfos, Int32[] TypeIDs) GetVariablesForExtendedQuerySequence(
         SQLStatementBuilderInformation stmt,
         TypeRegistry typeRegistry,
         Func<SQLStatementBuilderInformation, Int32, StatementParameter> paramExtractor
         )
      {
         var pCount = stmt.SQLParameterCount;
         TypeFunctionalityInformation[] typeInfos;
         Int32[] typeIDs;
         if ( pCount > 0 )
         {
            typeInfos = new TypeFunctionalityInformation[pCount];
            typeIDs = new Int32[pCount];
            for ( var i = 0; i < pCount; ++i )
            {
               var param = paramExtractor( stmt, i );
               var typeInfo = typeRegistry.TryGetTypeInfo( param.ParameterCILType );
               typeInfos[i] = typeInfo;
               typeIDs[i] = typeInfo?.DatabaseData?.TypeID ?? 0;
            }
         }
         else
         {
            typeInfos = Empty<TypeFunctionalityInformation>.Array;
            typeIDs = Empty<Int32>.Array;
         }

         return (( (PgSQLStatementBuilderInformation) stmt ).ParameterIndices, typeInfos, typeIDs);
      }

      private MessageIOArgs GetIOArgs( ResizableArray<Byte> bufferToUse = null, CancellationToken? tokenToUse = null )
      {
         return (this.MessageIOArgs, this.Stream, tokenToUse ?? this.CurrentCancellationToken, bufferToUse ?? this.Buffer);
      }

      protected override async ValueTask<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsBatch(
         SQLStatementBuilderInformation statement,
         ReservedForStatement reservedState
         )
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
                  // This happens when e.g. doing SELECT schema.function(x, y, z) -> can return NULLs or rows, we don't care.
                  break; // throw new PgSQLException( "Batch statements may only be used for non-query statements." );
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
         SQLStatementBuilderInformation statement,
         TypeFunctionalityInformation[] typeInfos,
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
                  statement.GetParametersEnumerable( j ),
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
         var buffer = new ResizableArray<Byte>( initialSize: 8, exponentialResize: true );

         for ( var i = 0; i < affectedRows.Length; ++i )
         {
            var msg = ( await this.ReadMessagesUntilMeaningful( notices, bufferToUse: buffer ) ).Item1;
            if ( msg is MessageWithNoContents nc && msg.Code == BackendMessageCode.BindComplete )
            {
               // Bind was successul - now read result of execute message
               msg = null;
               while ( msg == null )
               {
                  Int32 remaining;
                  (msg, remaining) = await this.ReadMessagesUntilMeaningful( notices, bufferToUse: buffer );
                  switch ( msg )
                  {
                     case CommandComplete cc:
                        Interlocked.Exchange( ref affectedRows[i], cc.AffectedRows ?? 0 );
                        if ( commandTag[0] == null )
                        {
                           Interlocked.Exchange( ref commandTag[0], cc.CommandTag );
                        }
                        break;
                     case DataRowObject dr:
                        // Skip thru data
                        await this.Stream.ReadSpecificAmountAsync( buffer.SetCapacityAndReturnArray( remaining ), 0, remaining, this.CurrentCancellationToken );
                        // And read more
                        msg = null;
                        break;
                     default:
                        throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
                  }
               }
            }
            else
            {
               throw new PgSQLException( "Unrecognized response at this point: " + msg.Code );
            }
         }
      }

      protected override async ValueTask<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsPrepared(
         SQLStatementBuilderInformation statement,
         ReservedForStatement reservedState
         )
      {
         (var parameterIndices, var typeInfos, var typeIDs) = GetVariablesForExtendedQuerySequence( statement, this.TypeRegistry, ( stmt, idx ) => stmt.GetParameterInfo( idx ) );
         var ioArgs = this.GetIOArgs();

         // First, send the parse message
         await new ParseMessage( statement.SQL, parameterIndices, typeIDs ).SendMessageAsync( ioArgs, true );

         // Then send bind message
         var bindMsg = new BindMessage( statement.GetParametersEnumerable(), parameterIndices.Length, typeInfos, this.DisableBinaryProtocolSend, this.DisableBinaryProtocolReceive );
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
         Func<ValueTask<(Boolean, SQLStatementExecutionResult)>> moveNext = null;
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
                  var streamArray = new PgSQLDataRowColumn[seenRD.Fields.Length];
                  var mdArray = new PgSQLDataColumnMetaDataImpl[streamArray.Length];
                  PgSQLDataRowColumn prevCol = null;
                  for ( var i = 0; i < streamArray.Length; ++i )
                  {
                     var curField = seenRD.Fields[i];
                     var curMD = new PgSQLDataColumnMetaDataImpl( this, curField.DataFormat, curField.dataTypeID, this.TypeRegistry.TryGetTypeInfo( curField.dataTypeID ), curField.name );
                     var curStream = new PgSQLDataRowColumn( curMD, i, prevCol, this, reservedState, curField );
                     prevCol = curStream;
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

      protected override async ValueTask<TStatementExecutionSimpleTaskParameter> ExecuteStatementAsSimple(
         SQLStatementBuilderInformation stmt,
         ReservedForStatement reservedState
         )
      {
         // Send Query message
         await new QueryMessage( stmt.SQL ).SendMessageAsync( this.GetIOArgs() );

         // Then wait for appropriate response
         List<PgSQLError> notices = new List<PgSQLError>();
         Func<ValueTask<(Boolean, SQLStatementExecutionResult)>> drMoveNext = null;

         // We have to always set moveNext, since we might be executing arbitrary amount of SQL statements in simple StatementBuilder.
         Func<ValueTask<(Boolean, SQLStatementExecutionResult)>> moveNext = async () =>
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
                        var streamArray = new PgSQLDataRowColumn[seenRD.Fields.Length];
                        var mdArray = new PgSQLDataColumnMetaDataImpl[streamArray.Length];
                        PgSQLDataRowColumn prevCol = null;
                        for ( var i = 0; i < streamArray.Length; ++i )
                        {
                           var curField = seenRD.Fields[i];
                           var curMD = new PgSQLDataColumnMetaDataImpl( this, curField.DataFormat, curField.dataTypeID, this.TypeRegistry.TryGetTypeInfo( curField.dataTypeID ), curField.name );
                           var curStream = new PgSQLDataRowColumn( curMD, i, prevCol, this, reservedState, curField );
                           prevCol = curStream;
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

      private async Task<(Boolean, SQLStatementExecutionResult)> MoveNextAsync(
         ReservedForStatement reservationObject,
         PgSQLDataRowColumn[] streams,
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
               await colStream.SkipBytesAsync( this.Buffer.Array );
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
            return (Success: retVal, Item: dataRow);
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

         // TODO The new moveNextEnded parameter could tell that instead of RFQEncountered property, investigate that
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

      public Queue<NotificationEventArgs> EnqueuedNotifications { get; }

      internal async ValueTask<Object> ConvertFromBytes(
         Int32 typeID,
         DataFormat dataFormat,
         EitherOr<ReservedForStatement, Stream> stream,
         Int32 byteCount
         )
      {
         var actualStream = stream.IsFirst ? this.Stream : stream.Second;
         var typeInfo = this.TypeRegistry.TryGetTypeInfo( typeID );
         if ( typeInfo != null )
         {
            var limitedStream = StreamFactory.CreateLimitedReader(
                  actualStream,
                  byteCount,
                  this.CurrentCancellationToken,
                  this.Buffer
                  );

            try
            {
               return await typeInfo.Functionality.ReadBackendValueAsync(
                  dataFormat,
                  typeInfo.DatabaseData,
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
            await actualStream.ReadSpecificAmountAsync( this.Buffer, 0, byteCount, this.CurrentCancellationToken );
            return this.MessageIOArgs.GetStringWithPool( this.Buffer.Array, 0, byteCount );
         }
         else
         {
            // Unknown type, and data format is binary.
            throw new PgSQLException( $"The type ID {typeID} is not known." );
         }
      }

      internal async ValueTask<(BackendMessageObject, Int32)> ReadMessagesUntilMeaningful(
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
                  this.EnqueuedNotifications.Enqueue( notification.Args );
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

#if !NETSTANDARD1_0
      private Boolean SocketHasDataPending()
      {
         var socket = this.Socket;
         return socket.Available > 0 || socket.Poll( 1, SelectMode.SelectRead ) || socket.Available > 0;
      }
#endif

      public async ValueTask<NotificationEventArgs[]> CheckNotificationsAsync()
      {
         // TODO this could be optimized a little, if we notice EnqueuedNotifications.Count > 0, then just don't read from stream at all. We still need to use statement protection regions tho.
         NotificationEventArgs[] args = null;

         NotificationEventArgs[] GetEnqueuedNotifications()
         {
            var enqueued = this.EnqueuedNotifications.ToArray();
            this.EnqueuedNotifications.Clear();
            return enqueued;
         }

#if !NETSTANDARD1_0
         var socket = this.Socket;
         if ( socket == null )
         {
#endif
         // Just do "SELECT 1"; to get any notifications
         var enumerable = this.PrepareStatementForExecution( this.VendorFunctionality.CreateStatementBuilder( "SELECT 1" ), out var dummy )
            .AsObservable();
         // Use GetEnqueuedNotifications while we are still inside statement reservation region, by registering to BeforeEnumerationEnd
         enumerable.BeforeEnumerationEnd += ( eArgs ) => args = GetEnqueuedNotifications();
         await enumerable.EnumerateSequentiallyAsync();
#if !NETSTANDARD1_0
         }
         else
         {
            // First, check from the socket that we have any data pending

            var hasDataPending = this.SocketHasDataPending();
            if ( hasDataPending || this.EnqueuedNotifications.Count > 0 )
            {
               // There is pending data
               // We always must use UseStreamOutsideStatementAsync method, since modifying this.EnqueuedNotifications outside that will result in concurrent modification exceptions
               await this.UseStreamOutsideStatementAsync( async () =>
               {
                  // If we call "ReadMessagesUntilMeaningful" with no socket data pending, we will never break free of loop properly.
                  if ( hasDataPending )
                  {
                     await this.ReadMessagesUntilMeaningful(
                        null,
                        this.SocketHasDataPending
                        );
                  }
                  args = GetEnqueuedNotifications();
                  return false;
               } );
            }
         }
#endif

         return args ?? Empty<NotificationEventArgs>.Array;

      }


      public IAsyncEnumerable<NotificationEventArgs> ListenToNotificationsAsync()
      {
#if !NETSTANDARD1_0
         if ( this.Socket == null )
         {
#else
         throw new NotSupportedException( "No socket available for this method." );
#endif
#if !NETSTANDARD1_0
         }

         var enqueued = this.EnqueuedNotifications;
         Boolean KeepReadingMore()
         {
            return enqueued.Count <= 0 || ( enqueued.Count <= 1000 && this.SocketHasDataPending() );
         }

         async Task PerformReadForNotifications()
         {
            if ( enqueued.Count <= 0 )
            {
               await this.ReadMessagesUntilMeaningful( null, KeepReadingMore );
            }
         }

         return AsyncEnumerationFactory.CreateStatefulWrappingEnumerable( () =>
         {
            PgReservedForStatement reservation = null;
            return AsyncEnumerationFactory.CreateWrappingStartInfo(
               async () =>
               {
                  if ( reservation == null )
                  {
                     reservation = new PgReservedForStatement(
#if DEBUG
                        null,
#endif
                        true,
                        null
                        );
                     reservation.RFQSeen();
                     await this.UseStreamOutsideStatementAsync( reservation, PerformReadForNotifications, false, true );
                  }
                  else
                  {
                     await this.UseStreamWithinStatementAsync( reservation, PerformReadForNotifications, true );
                  }

                  return enqueued.Count > 0;
               },
               ( out Boolean success ) =>
               {
                  success = enqueued.Count > 0;
                  return success ? enqueued.Dequeue() : default;
               },
               () =>
               {
                  return this.DisposeStatementAsync( reservation );
               }
               );
         } );
#endif
      }

      public static async Task<(PostgreSQLProtocol Protocol, List<PgSQLError> notices)> PerformStartup(
         PgSQLConnectionVendorFunctionality vendorFunctionality,
         PgSQLConnectionCreationInfo creationInfo,
         CancellationToken token,
         Stream stream,
         BackendABIHelper abiHelper,
         ResizableArray<Byte> buffer
#if !NETSTANDARD1_0
         , Socket socket
#endif
         )
      {
         var initData = creationInfo?.CreationData?.Initialization ?? throw new PgSQLException( "Please specify initialization configuration." );
         var startupInfo = await DoConnectionInitialization(
            creationInfo,
            (abiHelper, stream, token, buffer)
            );
         var protoConfig = initData?.Protocol;
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

      internal const String SERVER_PARAMETER_DATABASE = "database";

      private static async Task<(IDictionary<String, String> ServerParameters, Int32? backendProcessID, Int32? backendKeyData, List<PgSQLError> Notices, TransactionStatus TransactionStatus)> DoConnectionInitialization(
         PgSQLConnectionCreationInfo creationInfo,
         MessageIOArgs ioArgs
         )
      {
         var dbConfig = creationInfo?.CreationData?.Initialization?.Database ?? throw new ArgumentException( "Please specify database configuration" );
         var authConfig = creationInfo?.CreationData?.Initialization?.Authentication ?? throw new ArgumentException( "Please specify authentication configuration" );

         var encoding = ioArgs.Item1.Encoding.Encoding;
         var username = authConfig.Username ?? throw new ArgumentException( "Please specify username in authentication configuration." );
         var parameters = new Dictionary<String, String>()
         {
            { SERVER_PARAMETER_DATABASE, dbConfig.Name ?? throw new ArgumentException("Please specify database name in database configuration.") },
            { "user",username },
            { "DateStyle", "ISO" },
            { "client_encoding", encoding.WebName  },
            { "extra_float_digits", "2" },
            { "lc_monetary", "C" }
         };
         var sp = dbConfig.SearchPath;
         if ( !String.IsNullOrEmpty( sp ) )
         {
            parameters.Add( "search_path", sp );
         }

         await new StartupMessage( 3 << 16, parameters ).SendMessageAsync( ioArgs );

         BackendMessageObject msg;
         List<PgSQLError> notices = null;
         Int32? backendProcessID = null;
         Int32? backendKeyData = null;
         TransactionStatus tStatus = 0;
         Object saslState = null;
         try
         {
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
                     var newSaslState = await ProcessAuth(
                        creationInfo,
                        username,
                        ioArgs,
                        auth,
                        saslState
                        );
                     if ( newSaslState != null )
                     {
                        saslState = newSaslState;
                     }
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
         }
         finally
         {
            DisposeSASLState( saslState );
         }
         return (parameters, backendProcessID, backendKeyData, notices, tStatus);
      }

      private static async Task<Object> ProcessAuth(
         PgSQLConnectionCreationInfo creationInfo,
         String username,
         MessageIOArgs ioArgs,
         AuthenticationResponse msg,
         Object saslState
         )
      {
         var authType = msg.RequestType;
         var initData = creationInfo.CreationData.Initialization.Database;
         switch ( authType )
         {
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationClearTextPassword:
               await new PasswordMessage( GetPasswordBytes( creationInfo, ioArgs ) ).SendMessageAsync( ioArgs );
               break;
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationMD5Password:
               await HandleMD5Authentication( ioArgs, msg, username, GetPasswordBytes( creationInfo, ioArgs ) ).SendMessageAsync( ioArgs );
               break;
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationOk:
               // Nothing to do
               break;
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationSASL:
               var saslResult = ( HandleSASLAuthentication_Start( creationInfo, ioArgs, username, msg ) );
               saslState = saslResult.Item2;
               await ( saslResult.Item1 ?? throw new PgSQLException( "Authentication failed." ) ).SendMessageAsync( ioArgs );
               break;
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationSASLContinue:
               await ( HandleSASLAuthentication_Continue( ioArgs, msg, saslState ) ?? throw new PgSQLException( "Authentication failed." ) ).SendMessageAsync( ioArgs );
               break;
            case AuthenticationResponse.AuthenticationRequestType.AuthenticationSASLFinal:
               HandleSASLAuthentication_Final( creationInfo, ioArgs, msg, saslState );
               break;
            default:
               throw new PgSQLException( $"Authentication kind {authType} is not support." );
         }

         return saslState;
      }

      private static Byte[] GetPasswordBytes(
         PgSQLConnectionCreationInfo creationInfo,
         MessageIOArgs ioArgs
         )
      {
         var authConfig = creationInfo.CreationData.Initialization.Authentication;
         var encoding = ioArgs.Item1.Encoding.Encoding;
         return ( String.Equals( PgSQLAuthenticationConfiguration.PasswordByteEncoding.WebName, encoding.WebName ) ?
            authConfig.PasswordBytes :
            encoding.GetBytes( authConfig.Password ) ) ?? throw new PgSQLException( "Backend requested password, but it was not supplied." );
      }

      // Having this in separate method also won't force load of UtilPack.Cryptography assemblies if other than MD5/SASL authentication mechanism is used
      private static PasswordMessage HandleMD5Authentication(
         MessageIOArgs ioArgs,
         AuthenticationResponse msg,
         String username,
         Byte[] pw
         )
      {
         var buffer = ioArgs.Item4;
         var helper = ioArgs.Item1;

         if ( pw == null )
         {
            throw new PgSQLException( "Backend requested password, but it was not supplied." );
         }
         using ( var md5 = new UtilPack.Cryptography.Digest.MD5() )
         {
            // Extract server salt before using args.Buffer

            var serverSalt = buffer.Array.CreateArrayCopy( msg.AdditionalDataInfo.offset, msg.AdditionalDataInfo.count );

            // Hash password with username as salt
            var prehashLength = helper.Encoding.Encoding.GetByteCount( username ) + pw.Length;
            buffer.CurrentMaxCapacity = prehashLength;
            var idx = 0;
            pw.CopyTo( buffer.Array, ref idx, 0, pw.Length );
            helper.Encoding.Encoding.GetBytes( username, 0, username.Length, buffer.Array, pw.Length );
            var hash = md5.ComputeDigest( buffer.Array, 0, prehashLength );

            // Write hash as hexadecimal string
            buffer.CurrentMaxCapacity = hash.Length * 2 * helper.Encoding.BytesPerASCIICharacter;
            idx = 0;
            foreach ( var hashByte in hash )
            {
               helper.Encoding.WriteHexDecimal( buffer.Array, ref idx, hashByte );
            }

            // Hash result again with server-provided salt
            buffer.CurrentMaxCapacity += serverSalt.Length;
            var dummy = 0;
            serverSalt.CopyTo( buffer.Array, ref dummy, idx, serverSalt.Length );
            hash = md5.ComputeDigest( buffer.Array, 0, idx + serverSalt.Length );

            // Send back string "md5" followed by hexadecimal hash value
            buffer.CurrentMaxCapacity = 3 * helper.Encoding.BytesPerASCIICharacter + hash.Length * 2 * helper.Encoding.BytesPerASCIICharacter;
            idx = 0;
            var array = buffer.Array;
            helper.Encoding
               .WriteASCIIByte( array, ref idx, (Byte) 'm' )
               .WriteASCIIByte( array, ref idx, (Byte) 'd' )
               .WriteASCIIByte( array, ref idx, (Byte) '5' );
            foreach ( var hashByte in hash )
            {
               helper.Encoding.WriteHexDecimal( array, ref idx, hashByte );
            }

            var retValArray = new Byte[idx + 1]; // Remember string-terminating zero
            dummy = 0;
            array.CopyTo( retValArray, ref dummy, 0, idx );
            return new PasswordMessage( retValArray );
         }


      }

      private static (PasswordMessage, Object) HandleSASLAuthentication_Start(
         PgSQLConnectionCreationInfo creationInfo,
         MessageIOArgs ioArgs,
         String username,
         AuthenticationResponse msg
         )
      {
         var idx = msg.AdditionalDataInfo.offset;
         var count = msg.AdditionalDataInfo.count;
         var buffer = ioArgs.Item4;
         while ( count > 0 && buffer.Array[idx + count - 1] == 0 )
         {
            --count;
         }
         var protocolEncoding = ioArgs.Item1.Encoding;
         var authSchemes = protocolEncoding.Encoding.GetString( buffer.Array, idx, count );

         var mechanismInfo = creationInfo.CreateSASLMechanism?.Invoke( authSchemes ) ?? throw new PgSQLException( "Failed to provide SASL mechanism information." );
         var mechanism = mechanismInfo.Item1 ?? throw new PgSQLException( "Failed to provide SASL mechanism." );
         var mechanismName = mechanismInfo.Item2 ?? throw new PgSQLException( "Failed to provide SASL mechanism name." );
         var authConfig = creationInfo.CreationData.Initialization.Authentication;
         var pwDigest = authConfig.PasswordDigest;
         var credentials = pwDigest.IsNullOrEmpty() ?
            new SASLCredentialsSCRAMForClient( username, authConfig.Password ) :
            new SASLCredentialsSCRAMForClient( username, pwDigest );
         var writeBuffer = new ResizableArray<Byte>();
         var saslEncoding = new UTF8Encoding( false, true ).CreateDefaultEncodingInfo();
         var challengeResult = mechanism.ChallengeAsync( credentials.CreateChallengeArguments(
            Empty<Byte>.Array,
            -1,
            -1,
            writeBuffer,
            0,
            saslEncoding
            ) ).GetResultForceSynchronous();

         (PasswordMessage, Object) retVal;
         if ( !challengeResult.IsFirst || challengeResult.First.Item2 != SASLChallengeResult.MoreToCome )
         {
            retVal = default;
         }
         else
         {
            // SASL initial response is: null-terminated string for mechanism name, length of initial response, and initial response as byte array
            var bytesWritten = challengeResult.First.Item1;
            var pwArray = new Byte[
               protocolEncoding.Encoding.GetByteCount( mechanismName ) + protocolEncoding.BytesPerASCIICharacter
               + sizeof( Int32 )
               + bytesWritten
               ];
            idx = protocolEncoding.Encoding.GetBytes( mechanismName, 0, mechanismName.Length, pwArray, 0 ) + 1;
            pwArray.WritePgInt32( ref idx, bytesWritten );
            var dummy = 0;
            writeBuffer.Array.CopyTo( pwArray, ref dummy, idx, bytesWritten );

            retVal = (
               new PasswordMessage( pwArray ),
               new TSASLAuthState( mechanism, credentials, writeBuffer, saslEncoding )
               );
         }

         return retVal;

      }

      private static PasswordMessage HandleSASLAuthentication_Continue(
         MessageIOArgs ioArgs,
         AuthenticationResponse msg,
         Object state
         )
      {
         var challengeResult = HandleSASLAuthentication_ContinueOrFinal( ioArgs, msg, state );
         PasswordMessage retVal;
         if ( challengeResult.IsSecond || challengeResult.First.Item2 != SASLChallengeResult.MoreToCome )
         {
            retVal = default;
         }
         else
         {
            // Responses are password messages with whole SASL message as content
            retVal = new PasswordMessage( ( (TSASLAuthState) state ).Item3.Array.CreateArrayCopy( 0, challengeResult.First.Item1 ) );
         }

         return retVal;
      }

      private static void HandleSASLAuthentication_Final(
         PgSQLConnectionCreationInfo creationInfo,
         MessageIOArgs ioArgs,
         AuthenticationResponse msg,
         Object state
         )
      {
         var challengeResult = HandleSASLAuthentication_ContinueOrFinal( ioArgs, msg, state );
         if ( challengeResult.IsSecond || challengeResult.First.Item2 != SASLChallengeResult.Completed )
         {
            throw new PgSQLException( "Authentication failed." );
         }
         else
         {
            try
            {
               creationInfo.OnSASLSCRAMSuccess?.Invoke( ( (TSASLAuthState) state ).Item2.PasswordDigest );
            }
            catch
            {
               // Ignore...
            }

            DisposeSASLState( state );
         }
      }

      private static EitherOr<(Int32, SASLChallengeResult), Int32> HandleSASLAuthentication_ContinueOrFinal(
         MessageIOArgs ioArgs,
         AuthenticationResponse msg,
         Object state
         )
      {
         var idx = msg.AdditionalDataInfo.offset;
         var count = msg.AdditionalDataInfo.count;
         var buffer = ioArgs.Item4;

         var saslState = (TSASLAuthState) state;
         return saslState.Item1.ChallengeAsync( saslState.Item2.CreateChallengeArguments(
            buffer.Array,
            idx,
            count,
            saslState.Item3,
            0,
            saslState.Item4
            ) ).GetResultForceSynchronous();
      }

      private static void DisposeSASLState( Object state )
      {
         if ( state is TSASLAuthState saslState )
         {
            saslState.Item1?.DisposeSafely();
            saslState.Item3?.Array?.Clear();
         }
      }

      internal class PgReservedForStatement : ReservedForStatement
      {
         private Int32 _rfqEncountered;

         public PgReservedForStatement(
#if DEBUG
         Object statement,
#endif
         Boolean isSimple,
            String statementName
            )
#if DEBUG
         : base( statement )
#endif
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

   }

   // TODO move to utilpack
   internal static class E_TODO
   {
      public static T GetResultForceSynchronous<T>( this ValueTask<T> task )
      {
         return task.IsCompleted ? task.Result : throw new InvalidOperationException( "ValueTask is not completed when it should've been." );
      }
   }
}