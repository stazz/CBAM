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
using CBAM.Abstractions;
using CBAM.Abstractions.Implementation;
using CBAM.NATS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.AsyncEnumeration;

using TDataProducerResult = System.Threading.Tasks.ValueTask<System.Collections.Generic.IEnumerable<CBAM.NATS.NATSPublishData>>;

namespace CBAM.NATS
{
   using TDataProducer = Func<TDataProducerResult>;

   namespace Implementation
   {

      using TDataProducerFactory = Func<TDataProducer>;

      internal sealed class NATSConnectionImpl : NATSConnection
      {

         public sealed class NATSSubscribeConnectionImpl : ConnectionImpl<NATSSubscribeStatement, NATSSubscribeStatementInformation, String, NATSMessage, NATSConnectionVendorFunctionality, NATSConnectionVendorFunctionalityImpl, NATSSubscribeConnectionFunctionality>
         {
            public NATSSubscribeConnectionImpl(
               NATSSubscribeConnectionFunctionality functionality
               ) : base( functionality )
            {
            }
         }

         public sealed class NATSPublishConnectionImpl : ConnectionImpl<NATSPublishStatement, NATSPublishStatementInformation, TDataProducerFactory, NATSPublishCompleted, NATSConnectionVendorFunctionality, NATSConnectionVendorFunctionalityImpl, NATSPublishConnectionFunctionality>
         {
            public NATSPublishConnectionImpl(
               NATSPublishConnectionFunctionality functionality
               ) : base( functionality )
            {
            }
         }

         //private readonly Socket _socket;
         private readonly ClientProtocol _protocol;
         private readonly NATSSubscribeConnectionImpl _subscribe;
         private readonly NATSPublishConnectionImpl _publish;

         public NATSConnectionImpl(
            //Socket socket,
            NATSConnectionVendorFunctionalityImpl vendorFunctionality,
            ClientProtocol protocol
            )
         {
            this.VendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
            //this._socket = ArgumentValidator.ValidateNotNull( nameof( socket ), socket );
            this._protocol = ArgumentValidator.ValidateNotNull( nameof( protocol ), protocol );
            this._subscribe = new NATSSubscribeConnectionImpl( new NATSSubscribeConnectionFunctionality( vendorFunctionality, protocol ) );
            this._publish = new NATSPublishConnectionImpl( new NATSPublishConnectionFunctionality( vendorFunctionality, protocol ) );
         }

         public NATSConnectionVendorFunctionality VendorFunctionality { get; }

         public Boolean DisableEnumerableObservability { get; set; }

         event GenericEventHandler<EnumerationStartedEventArgs<NATSSubscribeStatementInformation>> AsyncEnumerationObservation<NATSMessage, NATSSubscribeStatementInformation>.BeforeEnumerationStart
         {
            add
            {
               this._subscribe.BeforeEnumerationStart += value;
            }
            remove
            {
               this._subscribe.BeforeEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<NATSMessage>.BeforeEnumerationStart
         {
            add
            {
               this._subscribe.BeforeEnumerationStart += value;
            }
            remove
            {
               this._subscribe.BeforeEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationStartedEventArgs<NATSSubscribeStatementInformation>> AsyncEnumerationObservation<NATSMessage, NATSSubscribeStatementInformation>.AfterEnumerationStart
         {
            add
            {
               this._subscribe.AfterEnumerationStart += value;
            }
            remove
            {
               this._subscribe.AfterEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<NATSMessage>.AfterEnumerationStart
         {
            add
            {
               this._subscribe.AfterEnumerationStart += value;
            }

            remove
            {
               this._subscribe.AfterEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationItemEventArgs<NATSMessage, NATSSubscribeStatementInformation>> AsyncEnumerationObservation<NATSMessage, NATSSubscribeStatementInformation>.AfterEnumerationItemEncountered
         {
            add
            {
               this._subscribe.AfterEnumerationItemEncountered += value;
            }
            remove
            {
               this._subscribe.AfterEnumerationItemEncountered -= value;
            }
         }

         event GenericEventHandler<EnumerationItemEventArgs<NATSMessage>> AsyncEnumerationObservation<NATSMessage>.AfterEnumerationItemEncountered
         {
            add
            {
               this._subscribe.AfterEnumerationItemEncountered += value;
            }
            remove
            {
               this._subscribe.AfterEnumerationItemEncountered -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs<NATSSubscribeStatementInformation>> AsyncEnumerationObservation<NATSMessage, NATSSubscribeStatementInformation>.BeforeEnumerationEnd
         {
            add
            {
               this._subscribe.BeforeEnumerationEnd += value;
            }
            remove
            {
               this._subscribe.BeforeEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<NATSMessage>.BeforeEnumerationEnd
         {
            add
            {
               this._subscribe.BeforeEnumerationEnd += value;
            }
            remove
            {
               this._subscribe.BeforeEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs<NATSSubscribeStatementInformation>> AsyncEnumerationObservation<NATSMessage, NATSSubscribeStatementInformation>.AfterEnumerationEnd
         {
            add
            {
               this._subscribe.AfterEnumerationEnd += value;
            }
            remove
            {
               this._subscribe.AfterEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<NATSMessage>.AfterEnumerationEnd
         {
            add
            {
               this._subscribe.AfterEnumerationEnd += value;
            }
            remove
            {
               this._subscribe.AfterEnumerationEnd -= value;
            }
         }




         event GenericEventHandler<EnumerationStartedEventArgs<NATSPublishStatementInformation>> AsyncEnumerationObservation<NATSPublishCompleted, NATSPublishStatementInformation>.BeforeEnumerationStart
         {
            add
            {
               this._publish.BeforeEnumerationStart += value;
            }
            remove
            {
               this._publish.BeforeEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<NATSPublishCompleted>.BeforeEnumerationStart
         {
            add
            {
               this._publish.BeforeEnumerationStart += value;
            }
            remove
            {
               this._publish.BeforeEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationStartedEventArgs<NATSPublishStatementInformation>> AsyncEnumerationObservation<NATSPublishCompleted, NATSPublishStatementInformation>.AfterEnumerationStart
         {
            add
            {
               this._publish.AfterEnumerationStart += value;
            }
            remove
            {
               this._publish.AfterEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<NATSPublishCompleted>.AfterEnumerationStart
         {
            add
            {
               this._publish.AfterEnumerationStart += value;
            }
            remove
            {
               this._publish.AfterEnumerationStart -= value;
            }
         }

         event GenericEventHandler<EnumerationItemEventArgs<NATSPublishCompleted, NATSPublishStatementInformation>> AsyncEnumerationObservation<NATSPublishCompleted, NATSPublishStatementInformation>.AfterEnumerationItemEncountered
         {
            add
            {
               this._publish.AfterEnumerationItemEncountered += value;
            }
            remove
            {
               this._publish.AfterEnumerationItemEncountered -= value;
            }
         }

         event GenericEventHandler<EnumerationItemEventArgs<NATSPublishCompleted>> AsyncEnumerationObservation<NATSPublishCompleted>.AfterEnumerationItemEncountered
         {
            add
            {
               this._publish.AfterEnumerationItemEncountered += value;
            }
            remove
            {
               this._publish.AfterEnumerationItemEncountered -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs<NATSPublishStatementInformation>> AsyncEnumerationObservation<NATSPublishCompleted, NATSPublishStatementInformation>.BeforeEnumerationEnd
         {
            add
            {
               this._publish.BeforeEnumerationEnd += value;
            }
            remove
            {
               this._publish.BeforeEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<NATSPublishCompleted>.BeforeEnumerationEnd
         {
            add
            {
               this._publish.BeforeEnumerationEnd += value;
            }
            remove
            {
               this._publish.BeforeEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs<NATSPublishStatementInformation>> AsyncEnumerationObservation<NATSPublishCompleted, NATSPublishStatementInformation>.AfterEnumerationEnd
         {
            add
            {
               this._publish.AfterEnumerationEnd += value;
            }
            remove
            {
               this._publish.AfterEnumerationEnd -= value;
            }
         }

         event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<NATSPublishCompleted>.AfterEnumerationEnd
         {
            add
            {
               this._publish.AfterEnumerationEnd += value;
            }
            remove
            {
               this._publish.AfterEnumerationEnd -= value;
            }
         }

         public IAsyncEnumerable<NATSMessage> PrepareStatementForExecution( NATSSubscribeStatementInformation statement )
         {
            return this._subscribe.PrepareStatementForExecution( statement );
         }

         public IAsyncEnumerable<NATSPublishCompleted> PrepareStatementForExecution( NATSPublishStatementInformation statement )
         {
            return this._publish.PrepareStatementForExecution( statement );
         }

         public async Task<NATSMessage> RequestAsync( String subject, Byte[] data, Int32 offset, Int32 count )
         {
            return await this._protocol.RequestAsync( subject, data, offset, count );
         }

         public event GenericEventHandler<AfterSubscriptionSentArgs> AfterSubscriptionSent
         {
            add
            {
               this._protocol.AfterSubscriptionSent += value;
            }
            remove
            {
               this._protocol.AfterSubscriptionSent -= value;
            }
         }

         public event GenericEventHandler<AfterPublishSentArgs> AfterPublishSent
         {
            add
            {
               this._protocol.AfterPublishSent += value;
            }
            remove
            {
               this._protocol.AfterPublishSent -= value;
            }
         }

         //         internal CancellationTokenSource UsageStarted()
         //         {
         //            var retVal = new CancellationTokenSource();
         //#pragma warning disable 4014
         //            //this.RunReader( retVal.Token );
         //#pragma warning restore 4014
         //            return retVal;
         //         }

         //internal void UsageEnded( CancellationTokenSource cancellationTokenSource )
         //{
         //   cancellationTokenSource.Cancel();
         //}

         //         private async Task RunReader( CancellationToken token )
         //         {
         //            var socket = this._socket;
         //            var seenIDs = new HashSet<Int64>();
         //            while ( !token.IsCancellationRequested )
         //            {
         //               if ( socket.Available > 0 || socket.Poll( 1, SelectMode.SelectRead ) || socket.Available > 0 )
         //               {
         //                  await this._protocol.PerformRead( seenIDs );
         //               }
         //               else
         //               {
         //                  await
         //#if NET40
         //                     TaskEx
         //#else
         //                     Task
         //#endif
         //                     .Delay( 50 );

         //               }
         //            }
         //         }

      }

      internal sealed class NATSSubscribeConnectionFunctionality : DefaultConnectionFunctionality<NATSSubscribeStatement, NATSSubscribeStatementInformation, String, NATSConnectionVendorFunctionalityImpl, NATSMessage>
      {

         private readonly ClientProtocol _protocol;

         public NATSSubscribeConnectionFunctionality(
            NATSConnectionVendorFunctionalityImpl vendor,
            ClientProtocol protocol
            ) : base( vendor )
         {
            this._protocol = ArgumentValidator.ValidateNotNull( nameof( protocol ), protocol );
         }

         protected override IAsyncEnumerable<NATSMessage> CreateEnumerable( NATSSubscribeStatementInformation metadata )
         {
            var protocol = this._protocol;
            var originalAutoUnsub = metadata.AutoUnsubscribeAfter;
            var hasAutoUnSub = originalAutoUnsub > 0;
            var subj = metadata.Subject;
            var queue = metadata.Queue;
            var dynamicUnsubscribe = metadata.DynamicUnsubscription;

            return AsyncEnumerationFactory.CreateStatefulWrappingEnumerable( () =>
            {
               Queue<NATSMessageImpl> messageQueue = null;
               Int64 id = -1;
               var currentAutoUnsub = originalAutoUnsub;
               var dynamicallyUnsubscribed = false;
               return AsyncEnumerationFactory.CreateWrappingStartInfo(
                  async () =>
                  {
                     if ( messageQueue == null )
                     {
                        (id, messageQueue) = await protocol.WriteSubscribe( subj, queue, currentAutoUnsub );
                     }
                     return !dynamicallyUnsubscribed && ( !hasAutoUnSub || currentAutoUnsub > 0 ? await protocol.PerformReadNext( id ) : false );
                  },
                  ( out Boolean success ) =>
                  {
                     success = ( !hasAutoUnSub || currentAutoUnsub > 0 ) && messageQueue.Count > 0;
                     if ( hasAutoUnSub && success )
                     {
                        --currentAutoUnsub;
                     }

                     var retVal = success ? messageQueue.Dequeue() : default;
                     if ( success && ( dynamicUnsubscribe?.Invoke( retVal ) ?? false ) )
                     {
                        success = false;
                        dynamicallyUnsubscribed = true;
                        retVal = default;
                     }

                     return retVal;
                  },
                  () => protocol.EnumerationEnded( id, hasAutoUnSub, currentAutoUnsub )
                  );

            } );
         }

         protected override NATSSubscribeStatementInformation GetInformationFromStatement( NATSSubscribeStatement statement )
         {
            return (NATSSubscribeStatementInformation) statement?.NATSStatementInformation;
         }

         protected override void ValidateStatementOrThrow( NATSSubscribeStatementInformation statement )
         {
            ArgumentValidator.ValidateNotNull( nameof( statement ), statement );
         }

      }

      internal sealed class NATSPublishConnectionFunctionality : DefaultConnectionFunctionality<NATSPublishStatement, NATSPublishStatementInformation, TDataProducerFactory, NATSConnectionVendorFunctionalityImpl, NATSPublishCompleted>
      {
         private readonly ClientProtocol _protocol;

         public NATSPublishConnectionFunctionality(
            NATSConnectionVendorFunctionalityImpl vendor,
            ClientProtocol protocol
            ) : base( vendor )
         {
            this._protocol = ArgumentValidator.ValidateNotNull( nameof( protocol ), protocol );
         }



         protected override IAsyncEnumerable<NATSPublishCompleted> CreateEnumerable( NATSPublishStatementInformation metadata )
         {
            const Int32 INITIAL = 0;
            const Int32 STARTED = 1;
            const Int32 PENDING_NEXT = 2;
            var protocol = this._protocol;
            var dpFactory = metadata.DataProducerFactory;

            return AsyncEnumerationFactory.CreateStatefulWrappingEnumerable( () =>
            {
               var state = INITIAL;
               TDataProducer dp = null;
               return AsyncEnumerationFactory.CreateWrappingStartInfo(
                  async () =>
                  {
                     if ( state == INITIAL )
                     {
                        dp = dpFactory?.Invoke();
                     }
                     Interlocked.Exchange( ref state, STARTED );
                     var datas = dp == null ? default : await dp();
                     if ( datas != null )
                     {
                        await protocol.WritePublish( datas );
                     }
                     return datas != null;
                  },
                  ( out Boolean success ) =>
                  {
                     success = state == STARTED;
                     if ( success )
                     {
                        Interlocked.Exchange( ref state, PENDING_NEXT );
                     }
                     return default( NATSPublishCompleted );
                  },
                  null
                  );
            } );
         }

         protected override NATSPublishStatementInformation GetInformationFromStatement( NATSPublishStatement statement )
         {
            return statement.NATSStatementInformation;
         }

         protected override void ValidateStatementOrThrow( NATSPublishStatementInformation statement )
         {
            ArgumentValidator.ValidateNotNull( nameof( statement ), statement );
         }
      }

      internal sealed class NATSConnectionVendorFunctionalityImpl : NATSConnectionVendorFunctionality
      {
         public static NATSConnectionVendorFunctionalityImpl Instance { get; } = new NATSConnectionVendorFunctionalityImpl();

         private NATSConnectionVendorFunctionalityImpl()
         {

         }


         NATSSubscribeStatement ConnectionVendorFunctionality<NATSSubscribeStatement, String>.CreateStatementBuilder( String subject )
         {
            var q = new Reference<String>();
            var a = new Reference<Int64>();
            var d = new Reference<Func<NATSMessage, Boolean>>();
            return new NATSSubscribeStatementImpl(
               new NATSSubscribeStatementInformationImpl( subject, q, a, d ),
               q,
               a,
               d
               );
         }

         NATSPublishStatement ConnectionVendorFunctionality<NATSPublishStatement, TDataProducerFactory>.CreateStatementBuilder( TDataProducerFactory dataProducerFactory )
         {
            var dp = new Reference<TDataProducerFactory>()
            {
               Value = dataProducerFactory
            };

            return new NATSPublishStatementImpl(
               new NATSPublishStatementInformationImpl( dp ),
               dp
               );
         }
      }
   }

}
