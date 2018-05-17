/*
 * Copyright 2018 Stanislav Muhametsin. All rights Reserved.
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.NATS.Implementation
{
   using TStoredState = Queue<NATSMessageImpl>;


   internal static class ClientProtocolConsts
   {

      public const Byte CR = 0x0D;
      public const Byte LF = 0x0A;
      public const Byte SPACE = 0x20;
      public const Byte TAB = 0x09;
      public static readonly Byte[] CRLF = new Byte[] { CR, LF };
      public static readonly Byte[] LF_ARRAY = new Byte[] { CRLF[1] };
      public static readonly Byte[] PONG = new Byte[] { 0x50, 0x4F, 0x4E, 0x47, 0x0D, LF };

      public static readonly Byte[] SUB_PREFIX = new Byte[] { 0x53, 0x55, 0x42, SPACE };
      public static readonly Byte[] PUB_PREFIX = new Byte[] { 0x50, 0x55, 0x42, SPACE };
      public static readonly Byte[] UNSUB_PREFIX = new Byte[] { 0x55, 0x4E, 0x53, 0x55, 0x42, SPACE };
      public static readonly Byte[] CONNECT_PREFIX = new Byte[] { 0x43, 0x4F, 0x4E, 0x4E, 0x45, 0x43, 0x54, SPACE };

      public const Int32 READ_COUNT = 0x10000;

      public const Int32 UPPERCASE_MASK_FULL = 0x5F5F5F5F;
      public const Int32 INFO_INT = 0x494E464F;


      public static class Info
      {
         public const String SERVER_ID = "server_id";
         public const String VERSION = "version";
         public const String VERSION_GO = "go";
         public const String HOST = "host";
         public const String PORT = "port";
         public const String AUTH_REQUIRED = "auth_required";
         public const String SSL_REQUIRED = "ssl_required";
         public const String MAX_PAYLOAD = "max_payload";
         public const String CONNECT_URLS = "connect_urls";
      }

      public static class Connect
      {
         public const String VERBOSE = "verbose";
         public const String PEDANTIC = "pedantic";
         public const String SSL_REQUIRED = "ssl_required";
         public const String AUTH_TOKEN = "auth_token";
         public const String USER = "user";
         public const String PASSWORD = "pass";
         public const String NAME = "name";
         public const String LANGAUGE = "lang";
         public const String VERSION = "version";
         public const String PROTOCOL = "protocol";
      }

   }


   internal sealed class ServerInformation
   {
      [JsonProperty( ClientProtocolConsts.Info.SERVER_ID )]
      public String ServerID { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.VERSION )]
      public String ServerVersion { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.VERSION_GO )]
      public String GoVersion { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.HOST )]
      public String Host { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.PORT )]
      public Int32 Port { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.AUTH_REQUIRED )]
      public Boolean AuthenticationRequired { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.SSL_REQUIRED )]
      public Boolean SSLRequired { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.MAX_PAYLOAD )]
      public Int32 MaxPayload { get; set; }

      [JsonProperty( ClientProtocolConsts.Info.CONNECT_URLS )]
      public String[] ConnectionURLs { get; set; }
   }

   internal sealed class ClientInformation
   {
      [JsonProperty( ClientProtocolConsts.Connect.VERBOSE )]
      public Boolean IsVerbose { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.PEDANTIC )]
      public Boolean IsPedantic { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.SSL_REQUIRED )]
      public Boolean SSLRequired { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.AUTH_TOKEN, NullValueHandling = NullValueHandling.Ignore )]
      public String AuthenticationToken { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.USER, NullValueHandling = NullValueHandling.Ignore )]
      public String Username { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.PASSWORD, NullValueHandling = NullValueHandling.Ignore )]
      public String Password { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.NAME, NullValueHandling = NullValueHandling.Ignore )]
      public String ClientName { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.LANGAUGE, NullValueHandling = NullValueHandling.Ignore )]
      public String ClientLanguage { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.VERSION, NullValueHandling = NullValueHandling.Ignore )]
      public String ClientVersion { get; set; }

      [JsonProperty( ClientProtocolConsts.Connect.PROTOCOL, DefaultValueHandling = DefaultValueHandling.Ignore )]
      public Int32 ProtocolVersion { get; set; }

   }


   internal sealed class ClientProtocol : NATSConnectionObservability
   {
      private sealed class SubscriptionState
      {
         public SubscriptionState(
            String subject,
            Int64 subscriptionID,
            Boolean isGlobal
            )
         {
            this.SubscriptionID = subscriptionID;
            this.IsGlobal = isGlobal;
            this.MessageQueue = isGlobal ? null : new Queue<NATSMessageImpl>();
            this.CachedMessage = isGlobal || subject.IndexOf( "*" ) >= 0 ? null : new NATSMessageImpl( subject, subscriptionID, null, null, -1 );
            this.DataBuffer = isGlobal ? null : new ResizableArray<Byte>();
            this.ByteArrayPool = new LocklessInstancePoolForClassesNoHeapAllocations<InstanceHolder<ResizableArray<Byte>>>();
            this.RentedByteArrays = new List<InstanceHolder<ResizableArray<Byte>>>();
         }

         public Int64 SubscriptionID { get; }

         public Queue<NATSMessageImpl> MessageQueue { get; }

         public NATSMessageImpl CachedMessage { get; }

         public ResizableArray<Byte> DataBuffer { get; }

         public Boolean IsGlobal { get; }

         public LocklessInstancePoolForClassesNoHeapAllocations<InstanceHolder<ResizableArray<Byte>>> ByteArrayPool { get; }

         public List<InstanceHolder<ResizableArray<Byte>>> RentedByteArrays { get; }

      }

      public abstract class IOState
      {

         public IOState()
         {
            this.Lock = new AsyncLock();
            this.Buffer = new ResizableArray<Byte>( 0x100 );
         }

         public ResizableArray<Byte> Buffer { get; }

         public AsyncLock Lock { get; }
      }

      public sealed class WriteState : IOState
      {
         public WriteState(
            ) : base()
         {
         }
      }

      public sealed class ReadState : IOState
      {
         public ReadState(
            ) : base()
         {
            this.MessageSpaceIndices = new Int32[3];
            this.BufferAdvanceState = new BufferAdvanceState();
         }

         public BufferAdvanceState BufferAdvanceState { get; }

         public Int32[] MessageSpaceIndices { get; }
      }

      public sealed class ClientProtocolIOState
      {

         public ClientProtocolIOState(
            Stream stream,
            BinaryStringPool stringPool,
            IEncodingInfo encoding,
            WriteState writeState,
            ReadState readState
            )
         {
            this.Stream = ArgumentValidator.ValidateNotNull( nameof( stream ), stream );
            this.StringPool = ArgumentValidator.ValidateNotNull( nameof( stringPool ), stringPool );
            this.Encoding = ArgumentValidator.ValidateNotNull( nameof( encoding ), encoding );
            this.WriteState = writeState ?? new WriteState();
            this.ReadState = readState ?? new ReadState();
         }

         public WriteState WriteState { get; }

         public ReadState ReadState { get; }

         public Stream Stream { get; }

         public BinaryStringPool StringPool { get; }

         public IEncodingInfo Encoding { get; }
      }

      public sealed class GlobalSubscriptionEventArgs
      {
         public GlobalSubscriptionEventArgs( NATSMessageImpl message )
         {
            this.Message = ArgumentValidator.ValidateNotNull( nameof( message ), message );
         }
         public NATSMessageImpl Message { get; }
      }


      private readonly ClientProtocolIOState _state;
      private readonly ServerInformation _serverParameters;
      private readonly Byte[] _globalSubscriptionNameBytes;
      private readonly ConcurrentDictionary<Int64, SubscriptionState> _subscriptionStates;
      private readonly AsyncLazy<Int64> _globalSubscriptionID;

      private Int64 _currentID;
      private Int64 _globalSubscriptionSuffix;

      public ClientProtocol(
        ClientProtocolIOState state,
        ServerInformation serverParameters,
        String globalSubscriptionName = null
      )
      {
         this._state = ArgumentValidator.ValidateNotNull( nameof( state ), state );
         this._subscriptionStates = new ConcurrentDictionary<Int64, SubscriptionState>();
         this._serverParameters = ArgumentValidator.ValidateNotNull( nameof( serverParameters ), serverParameters );
         this._currentID = 0;
         this.GlobalSubscriptionPrefix = ( String.IsNullOrEmpty( globalSubscriptionName ) ? Guid.NewGuid().ToString( "N" ) : globalSubscriptionName ) + ".";
         this._globalSubscriptionNameBytes = state.Encoding.Encoding.GetBytes( this.GlobalSubscriptionPrefix );
         this._globalSubscriptionID = new AsyncLazy<Int64>( async () => ( await this.WriteSubscribe( this.GlobalSubscriptionPrefix + "*", null, -1, true ) ).Item1 );
      }


      public Boolean CanBeReturnedToPool => this._subscriptionStates.Count <= 0 || this._subscriptionStates.Values.All( s => s.IsGlobal );

      public Stream Stream => this._state.Stream;

      public String GlobalSubscriptionPrefix { get; }


      public event GenericEventHandler<GlobalSubscriptionEventArgs> GlobalSubscriptionMessageReceived;
      public event GenericEventHandler<AfterSubscriptionSentArgs> AfterSubscriptionSent;
      public event GenericEventHandler<AfterPublishSentArgs> AfterPublishSent;

      public Task<(Int64, TStoredState)> WriteSubscribe(
         String subject,
         String queue,
         Int64 autoUnsub
         ) => this.WriteSubscribe( subject, queue, autoUnsub, false );

      private async Task<(Int64, TStoredState)> WriteSubscribe(
         String subject,
         String queue,
         Int64 autoUnsub,
         Boolean isGlobal
         )
      {
         var state = this._state;
         var wState = state.WriteState;
         var id = Interlocked.Increment( ref this._currentID );

         using ( await wState.Lock.LockAsync() )
         {
            var buffer = wState.Buffer;
            var encoding = state.Encoding;
            var idx = 0;
            // Write 'SUB <subject> [queue group ]<sid>\r\n'
            var idSize = encoding.GetTextualIntegerRepresentationSize( id );
            if ( !String.IsNullOrEmpty( queue ) && queue.ContainsNonASCIICharacters( NATSStatementInformationImpl.IsInvalidASCIICharacter ) )
            {
               throw new InvalidOperationException( "Invalid queue name: " + queue );
            }
            var msgSize = 7 + subject.Length + ( String.IsNullOrEmpty( queue ) ? 0 : ( queue.Length + 1 ) ) + idSize;
            var array = wState.Buffer.SetCapacityAndReturnArray( msgSize );
            array
               .WriteASCIIString( ref idx, ClientProtocolConsts.SUB_PREFIX )
               .WriteASCIIString( ref idx, subject, false )
               .WriteASCIIString( ref idx, ClientProtocolConsts.SPACE );

            if ( !String.IsNullOrEmpty( queue ) )
            {
               array
                  .WriteASCIIString( ref idx, queue, false )
                  .WriteASCIIString( ref idx, ClientProtocolConsts.SPACE );
            }

            encoding.WriteIntegerTextual( array, ref idx, id, idSize );
            array.WriteASCIIString( ref idx, ClientProtocolConsts.CRLF );
            System.Diagnostics.Debug.Assert( idx == msgSize );

            await state.Stream.WriteAsync( array, 0, msgSize, default );
            if ( !isGlobal )
            {
               this.AfterSubscriptionSent?.InvokeAllEventHandlers( new AfterSubscriptionSentArgs( subject, queue, autoUnsub > 0 ? autoUnsub : default ), throwExceptions: false );
            }
            if ( autoUnsub > 0 )
            {
               await this.PerformUnsubscribe( id, autoUnsub );
            }
            await state.Stream.FlushAsync( default );

         }

         var retVal = new SubscriptionState( subject, id, isGlobal );
         if ( !this._subscriptionStates.TryAdd( id, retVal ) )
         {
            throw new Exception( "This should not be possible." );
         }

         return (id, retVal.MessageQueue);
      }

      public Task EnumerationEnded(
         Int64 id,
         Boolean hasAutoUnSub,
         Int64 currentAutoUnsub

         )
      {
         Task retVal;
         if ( hasAutoUnSub && currentAutoUnsub < 0 )
         {
            this._subscriptionStates.TryRemove( id, out var ignored );
            retVal = TaskUtils.CompletedTask;
         }
         else
         {
            retVal = this.WriteUnsubscribe( id, 0 );
         }

         return retVal;
      }

      public async Task WriteUnsubscribe(
         Int64 id,
         Int64 autoUnsubscribe
         )
      {
         if ( autoUnsubscribe <= 0 )
         {
            this._subscriptionStates.TryRemove( id, out var ignored );
         }

         var state = this._state;
         var wState = state.WriteState;
         using ( await wState.Lock.LockAsync() )
         {
            await this.PerformUnsubscribe( id, autoUnsubscribe );
            await state.Stream.FlushAsync( default );
         }

      }

      private async Task PerformUnsubscribe(
         Int64 id,
         Int64 autoUnsubscribe
         )
      {
         var state = this._state;
         var wState = state.WriteState;
         var encoding = state.Encoding;
         var idSize = encoding.GetTextualIntegerRepresentationSize( id );
         var autoSize = autoUnsubscribe > 0 ? encoding.GetTextualIntegerRepresentationSize( autoUnsubscribe ) : 0;
         var msgSize = 8 + idSize + ( autoSize > 0 ? ( autoSize + 1 ) : 0 );
         var array = wState.Buffer.SetCapacityAndReturnArray( msgSize );
         var idx = 0;
         array.WriteASCIIString( ref idx, ClientProtocolConsts.UNSUB_PREFIX );
         encoding.WriteIntegerTextual( array, ref idx, id, idSize );
         if ( autoSize > 0 )
         {
            array.WriteASCIIString( ref idx, ClientProtocolConsts.SPACE );
            encoding.WriteIntegerTextual( array, ref idx, autoUnsubscribe, autoSize );
         }
         array.WriteASCIIString( ref idx, ClientProtocolConsts.CRLF );
         System.Diagnostics.Debug.Assert( idx == msgSize );
         await state.Stream.WriteAsync( array, 0, msgSize, default );
      }

      public async Task WritePublish(
         IEnumerable<NATSPublishData> datas
         )
      {
         var state = this._state;
         var wState = state.WriteState;
         using ( await wState.Lock.LockAsync() )
         {
            var buffer = wState.Buffer;
            var encoding = state.Encoding;

            foreach ( var pData in datas )
            {
               var subject = pData.Subject;
               if ( !String.IsNullOrEmpty( subject ) )
               {
                  var count = pData.Count;
                  var reply = pData.ReplySubject;

                  var dataMsgSize = encoding.GetTextualIntegerRepresentationSize( count );
                  var msgSize = 9 + subject.Length + ( String.IsNullOrEmpty( reply ) ? 0 : ( reply.Length + 1 ) ) + dataMsgSize + count;
                  var array = wState.Buffer.SetCapacityAndReturnArray( msgSize );

                  var idx = 0;
                  array
                     .WriteASCIIString( ref idx, ClientProtocolConsts.PUB_PREFIX )
                     .WriteASCIIString( ref idx, subject, false )
                     .WriteASCIIString( ref idx, ClientProtocolConsts.SPACE );
                  if ( !String.IsNullOrEmpty( reply ) )
                  {
                     array
                        .WriteASCIIString( ref idx, reply, false )
                        .WriteASCIIString( ref idx, ClientProtocolConsts.SPACE );
                  }
                  encoding.WriteIntegerTextual( array, ref idx, count, dataMsgSize );
                  array.WriteASCIIString( ref idx, ClientProtocolConsts.CRLF );

                  if ( count > 0 )
                  {
                     Array.Copy( pData.Data, pData.Offset, array, idx, count );
                     idx += count;
                  }

                  array.WriteASCIIString( ref idx, ClientProtocolConsts.CRLF );

                  System.Diagnostics.Debug.Assert( idx == msgSize );
                  await state.Stream.WriteAsync( array, 0, msgSize, default );

                  this.AfterPublishSent?.InvokeAllEventHandlers( new AfterPublishSentArgs( subject, reply ), throwExceptions: false );
               }
            }

            await state.Stream.FlushAsync( default );
         }

      }

      private async Task WritePong()
      {
         var state = this._state;
         using ( await state.WriteState.Lock.LockAsync() )
         {
            await state.Stream.WriteAsync( ClientProtocolConsts.PONG, 0, 6, default );
            await state.Stream.FlushAsync( default );
         }
      }

      public async ValueTask<Boolean> PerformReadNext(
         Int64 subscriptionID
         )
      {
         if ( this._subscriptionStates.TryGetValue( subscriptionID, out var subState ) )
         {
            var pool = subState.ByteArrayPool;
            foreach ( var rentedByteArray in subState.RentedByteArrays )
            {
               pool.ReturnInstance( rentedByteArray );
            }

            subState.RentedByteArrays.Clear();
            var queue = subState.MessageQueue;
            while ( queue.Count <= 0 )
            {
               var rLock = this._state.ReadState.Lock;
               if ( queue.Count <= 0 )
               {
                  var lockScope = await rLock.TryLockAsync( TimeSpan.FromMilliseconds( 100 ) );
                  if ( lockScope.HasValue )
                  {
                     using ( lockScope.Value )
                     {
                        if ( queue.Count <= 0 )
                        {
                           await this.PerformRead();
                        }
                     }
                  }
               }
            }
         }

         return ( subState?.MessageQueue?.Count ?? 0 ) > 0;

      }

      public async Task<NATSMessage> RequestAsync( String subject, Byte[] data, Int32 offset, Int32 count )
      {
         await this._globalSubscriptionID;

         var replyTo = this.GlobalSubscriptionPrefix + Interlocked.Increment( ref this._globalSubscriptionSuffix );
         NATSMessage receivedMessage = null;
         void HandleGlobalSubEvent( GlobalSubscriptionEventArgs args )
         {
            if ( String.Equals( args.Message.Subject, replyTo ) )
            {
               Interlocked.Exchange( ref receivedMessage, args.Message );
            }
         };

         this.GlobalSubscriptionMessageReceived += HandleGlobalSubEvent;
         using ( new UsingHelper( () => this.GlobalSubscriptionMessageReceived -= HandleGlobalSubEvent ) )
         {
            await this.WritePublish(
               new NATSPublishData( subject, data, offset, count, replyTo ).Singleton()
               );
            var rLock = this._state.ReadState.Lock;
            do
            {
               var lockScope = await rLock.TryLockAsync( TimeSpan.FromMilliseconds( 100 ) );
               if ( lockScope.HasValue )
               {
                  using ( lockScope )
                  {
                     if ( receivedMessage == null )
                     {
                        await this.PerformRead();
                     }
                  }
               }
            } while ( receivedMessage == null );

         }

         return receivedMessage;
      }


      private async Task PerformRead()
      {
         var states = this._subscriptionStates;
         //const Int32 MIN_MESSAGE_SIZE = 5;

         var state = this._state;
         var rState = state.ReadState;
         var stream = state.Stream;
         var buffer = rState.Buffer;
         var encodingInfo = state.Encoding;
         var stringPool = state.StringPool;

         var advanceState = rState.BufferAdvanceState;

         await stream.ReadUntilMaybeAsync( buffer, advanceState, ClientProtocolConsts.CRLF, ClientProtocolConsts.READ_COUNT );
         var crIdx = advanceState.BufferOffset;
         while ( crIdx >= 0 )
         {
            var array = buffer.Array;
            // First byte will be integer's uppermost byte, second byte second uppermost, etc
            var idx = 0;
            var msgHeader = array.ReadInt32BEFromBytes( ref idx );
            var additionalByte = array[idx++];
            // At the end of the following switch statement, advanceState.BufferOffset should point to the CR byte of the last CRLF of this message
            // Examine first byte
            // x & 0x5F is to make ASCII lowercase letters (a-z) into uppercase letters (A-Z)
            switch ( msgHeader & 0xFF000000 )
            {
               case 0x2B000000: // 2B = '+'
                  if ( ( msgHeader & 0x005F5FFF ) == 0x004F4B0D && additionalByte == 0x0A ) // +OK\r\n
                  {
                     // OK -message -> ignore
                  }
                  else
                  {
                     throw new Exception( "Protocol error" );
                  }
                  break;
               case 0x2D000000: // 2D = '-'
                  if ( ( msgHeader & 0x005F5F5F ) == 0x00455252 && ( additionalByte == ClientProtocolConsts.SPACE || additionalByte == ClientProtocolConsts.TAB ) )
                  {
                     // -ERR<space/tab> -message, read textual error message
                     // TODO close connection on errors that are fatal
                     throw new Exception( "Protocol error: " + stringPool.GetString( buffer.Array, idx, advanceState.BufferOffset - idx ) );
                  }
                  else
                  {
                     throw new Exception( "Protocol error" );
                  }
               default:
                  if ( ( msgHeader & 0x5F5F5F00 ) == 0x4D534700 ) // MSG
                  {
                     additionalByte = (Byte) ( msgHeader & 0x000000FF );
                     if ( additionalByte == ClientProtocolConsts.SPACE || additionalByte == ClientProtocolConsts.TAB )
                     {
                        // Read the rest of the header
                        array = buffer.Array;

                        // Count spaces/tabs (TODO make this treat multiple consecutive spaces as one)
                        var spacesSeen = 0;
                        var spaceIndices = rState.MessageSpaceIndices;
                        Array.Clear( spaceIndices, 0, spaceIndices.Length );
                        var end = advanceState.BufferOffset;
                        for ( var i = idx; i < end; ++i )
                        {
                           if ( array[i] == ClientProtocolConsts.SPACE || array[i] == ClientProtocolConsts.TAB )
                           {
                              var oldValue = spaceIndices[spacesSeen];
                              spaceIndices[spacesSeen] = i;
                              if ( oldValue < i - 1 )
                              {
                                 ++spacesSeen;
                              }
                           }
                        }
                        if ( spacesSeen < 2 || spacesSeen > 3 )
                        {
                           throw new Exception( "Protocol error" );
                        }

                        // MSG <subject> <sid> [reply-to] <#bytes>\r\n[payload]\r\n
                        var subjStart = idx - 1;
                        var subjLen = spaceIndices[0] - subjStart;
                        idx = spaceIndices[0] + 1;
                        var subID = encodingInfo.ParseInt64Textual( array, ref idx, (spaceIndices[1] - spaceIndices[0] - 1, true) );
                        var replyTo = spacesSeen > 2 ?
                           stringPool.GetString( array, spaceIndices[1] + 1, spaceIndices[2] - spaceIndices[1] - 1 ) :
                           null;
                        idx = spaceIndices[spacesSeen - 1] + 1;
                        var payloadSize = encodingInfo.ParseInt32Textual( array, ref idx, (end - idx, true) );
                        if ( payloadSize < 0 )
                        {
                           throw new Exception( "Protocol error" );
                        }
                        var readFromStreamCount = end + 2 + payloadSize + 2 - advanceState.BufferTotal;
                        if ( readFromStreamCount > 0 )
                        {
                           await stream.ReadSpecificAmountAsync( buffer, advanceState.BufferTotal, readFromStreamCount, default );
                           advanceState.ReadMore( readFromStreamCount );
                        }
                        if ( states.TryGetValue( subID, out var subState ) )
                        {
                           NATSMessageImpl readMessage;
                           if ( replyTo == null && payloadSize <= 0 && subState.CachedMessage != null )
                           {
                              // Use cached instance when no reply, no data, and same subject
                              readMessage = subState.CachedMessage;
                           }
                           else
                           {
                              // TODO we can pool & rent NATSMessageImpl instances too, for when there is no reply and same subject. 
                              // Rent byte array instance and make the message use it
                              var byteArrayInstance = subState.ByteArrayPool.TakeInstance() ?? new InstanceHolder<ResizableArray<Byte>>( new ResizableArray<Byte>( initialSize: payloadSize ) );
                              subState.RentedByteArrays.Add( byteArrayInstance );
                              var messageData = byteArrayInstance.Instance.SetCapacityAndReturnArray( payloadSize );
                              Array.Copy( buffer.Array, end + 2, messageData, 0, payloadSize );
                              readMessage = new NATSMessageImpl( stringPool.GetString( array, subjStart, subjLen ), subID, replyTo, messageData, payloadSize );
                           }


                           if ( subState.IsGlobal )
                           {
                              this.GlobalSubscriptionMessageReceived?.Invoke( new GlobalSubscriptionEventArgs( readMessage ) );
                           }
                           else
                           {
                              subState.MessageQueue.Enqueue( readMessage );
                           }
                        }

                        advanceState.Advance( payloadSize + 2 );
                     }
                     else
                     {
                        throw new Exception( "Protocol error" );
                     }
                  }
                  else
                  {
                     var additionalByte2 = array[idx++];
                     switch ( msgHeader & ClientProtocolConsts.UPPERCASE_MASK_FULL )
                     {
                        case 0x50494E47: // PING
                           if ( additionalByte == ClientProtocolConsts.CR && additionalByte2 == ClientProtocolConsts.LF )
                           {
                              // Send back PONG ( we purposefully don't 'await' for this task)
#pragma warning disable 4014
                              this.WritePong();
#pragma warning restore 4014
                           }
                           else
                           {
                              throw new Exception( "Protocol error" );
                           }
                           break;
                        case 0x504F4E47: // PONG
                                         // Currently, unused, since client never sends pings.
                           if ( additionalByte == ClientProtocolConsts.CR && additionalByte2 == ClientProtocolConsts.LF )
                           {
                           }
                           else
                           {
                              throw new Exception( "Protocol error" );
                           }
                           break;
                        case ClientProtocolConsts.INFO_INT: // INFO
                           if ( additionalByte == ClientProtocolConsts.SPACE || additionalByte == ClientProtocolConsts.TAB )
                           {
                              var info = DeserializeInfoMessage( buffer.Array, idx, advanceState.BufferOffset - idx, encodingInfo.Encoding );
                              // TODO implement dynamic handling of INFO message
                           }
                           else
                           {
                              throw new Exception( "Protocol error" );
                           }
                           break;
                        default:
                           throw new Exception( "Protocol error" );
                     }
                  }
                  break;
            }

            // Remember to shift remaining data to the beginning of byte array
            // TODO optimize: we don't need to do this on every loop.
            SetPreReadLength( rState );
            crIdx = array.IndexOfArray( 0, advanceState.BufferTotal, ClientProtocolConsts.CRLF );
            advanceState.Advance( crIdx < 0 ? advanceState.BufferTotal : crIdx );
         }
      }

      internal static void SetPreReadLength( ReadState rState )
      {
         var aState = rState.BufferAdvanceState;
         var end = aState.BufferOffset;
         var preReadLength = aState.BufferTotal;
         // Messages end with CRLF
         end += 2;
         var remainingData = preReadLength - end;
         if ( remainingData > 0 )
         {
            var array = rState.Buffer.Array;
            Array.Copy( array, end, array, 0, remainingData );
         }
         aState.Reset();
         aState.ReadMore( remainingData );

      }

      internal static ServerInformation DeserializeInfoMessage( Byte[] array, Int32 offset, Int32 count, Encoding encoding )
      {
         using ( var mStream = new MemoryStream( array, offset, count ) )
         using ( var reader = new StreamReader( mStream, encoding ) )
         using ( var jReader = new JsonTextReader( reader ) )
         {
            //   return (JObject) JToken.Load( jReader );
            return JsonSerializer.CreateDefault().Deserialize<ServerInformation>( jReader );
         }
      }

      internal static Byte[] SerializeConnectMessage( ClientInformation clientInfo, Encoding encoding )
      {
         using ( var mStream = new MemoryStream( 0x100 ) )
         {
            using ( var writer = new StreamWriter( mStream, encoding ) )
            {
               JsonSerializer.CreateDefault().Serialize( writer, clientInfo );
               writer.Flush();
               return mStream.ToArray();
            }
         }
      }

      internal static async Task InitializeNewConnection(
         ClientInformation clientInfo,
         Encoding encoding,
         WriteState wState,
         Stream stream,
         CancellationToken token
         )
      {
         var connectBytes = SerializeConnectMessage( clientInfo, encoding );

         var msgLength = 10 + connectBytes.Length;
         var array = wState.Buffer.SetCapacityAndReturnArray( msgLength );
         var idx = 0;
         array.WriteASCIIString( ref idx, ClientProtocolConsts.CONNECT_PREFIX );
         connectBytes.CopyTo( array, idx );
         idx += connectBytes.Length;
         array.WriteASCIIString( ref idx, ClientProtocolConsts.CRLF );

         await stream.WriteAsync( array, 0, msgLength, token );
         await stream.FlushAsync( token );
      }
   }
}

