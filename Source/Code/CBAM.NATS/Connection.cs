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
using AsyncEnumeration.Abstractions;
using CBAM.Abstractions;
using CBAM.NATS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using TDataProducerResult = System.Threading.Tasks.ValueTask<System.Collections.Generic.IEnumerable<CBAM.NATS.NATSPublishData>>;

namespace CBAM.NATS
{
   using TDataProducerFactory = Func<Func<TDataProducerResult>>;

   public interface NATSConnection :
      Connection<NATSSubscribeStatement, NATSSubscribeStatementInformation, String, NATSMessage, NATSConnectionVendorFunctionality>,
      Connection<NATSPublishStatement, NATSPublishStatementInformation, TDataProducerFactory, NATSPublishCompleted, NATSConnectionVendorFunctionality>,
      NATSConnectionObservability
   {
      Task<NATSMessage> RequestAsync( String subject, Byte[] data, Int32 offset, Int32 count );
   }

   public interface NATSConnectionVendorFunctionality :
      ConnectionVendorFunctionality<NATSSubscribeStatement, String>,
      ConnectionVendorFunctionality<NATSPublishStatement, TDataProducerFactory>
   {

   }

   // TODO this interface might not be required after all - it might encourage 'bad' behaviour (i.e. the exact moment when subscription protocol message is sent is not the moment when server recognizes and registers the subscription)
   public interface NATSConnectionObservability
   {
      event GenericEventHandler<AfterSubscriptionSentArgs> AfterSubscriptionSent;

      event GenericEventHandler<AfterPublishSentArgs> AfterPublishSent;
   }

   public sealed class AfterSubscriptionSentArgs
   {
      public AfterSubscriptionSentArgs(
         String subject,
         String queue,
         Int64? autoUnsubscribeAfter
         )
      {
         this.Subject = ArgumentValidator.ValidateNotEmpty( nameof( subject ), subject );
         this.Queue = queue;
         this.AutoUnsubscribeAfter = autoUnsubscribeAfter;
      }

      public String Subject { get; }

      public String Queue { get; }

      public Int64? AutoUnsubscribeAfter { get; }
   }

   public sealed class AfterPublishSentArgs
   {
      public AfterPublishSentArgs(
         String subject,
         String replyTo
         )
      {
         this.Subject = ArgumentValidator.ValidateNotEmpty( nameof( subject ), subject );
         this.ReplyTo = replyTo;
      }

      public String Subject { get; }

      public String ReplyTo { get; }
   }
}

public static partial class E_CBAM
{
   public static NATSSubscribeStatement CreateSubscribeStatementBuilder( this NATSConnectionVendorFunctionality vendorFunctionality, String subject )
   {
      return vendorFunctionality.CreateStatementBuilder( subject );
   }

   public static NATSPublishStatement CreatePublishStatementBuilder( this NATSConnectionVendorFunctionality vendorFunctionality, Func<Func<TDataProducerResult>> dataProducer )
   {
      return vendorFunctionality.CreateStatementBuilder( dataProducer );
   }

   public static NATSSubscribeStatement CreateSubscribeStatementBuilder( this NATSConnection connection, String subject )
   {
      return ( (Connection<NATSSubscribeStatement, NATSSubscribeStatementInformation, String, NATSMessage, NATSConnectionVendorFunctionality>) connection ).VendorFunctionality.CreateSubscribeStatementBuilder( subject );
   }

   public static IAsyncEnumerable<NATSMessage> SubscribeAsync( this NATSConnection connection, String subject, Int64 autoUnsubscribeAfter = 0, String queue = null )
   {
      var stmt = connection.CreateSubscribeStatementBuilder( subject );
      if ( !String.IsNullOrEmpty( queue ) )
      {
         stmt.Queue = queue;
      }
      if ( autoUnsubscribeAfter > 0 )
      {
         stmt.AutoUnsubscribeAfter = autoUnsubscribeAfter;
      }

      return connection.PrepareStatementForExecution( stmt );
   }

   public static NATSPublishStatement CreatePublishStatementBuilder( this NATSConnection connection, Func<Func<TDataProducerResult>> dataProducer )
   {
      return ( (Connection<NATSPublishStatement, NATSPublishStatementInformation, Func<Func<TDataProducerResult>>, NATSPublishCompleted, NATSConnectionVendorFunctionality>) connection ).VendorFunctionality.CreatePublishStatementBuilder( dataProducer );
   }

   public static Task<NATSMessage> RequestAsync( this NATSConnection connection, String subject, Byte[] data )
   {
      return connection.RequestAsync( subject, data, 0, data?.Length ?? 0 );
   }


   public static IAsyncEnumerable<NATSPublishCompleted> PublishWithStaticDataProducerForWholeArray( this NATSConnection connection, String subject, Byte[] array, String replySubject = null, Int64 repeatCount = 1, Int32 chunkCount = 1000 )
   {
      return connection.PublishWithStaticDataProducer( subject, array, 0, array?.Length ?? 0, replySubject, repeatCount, chunkCount );
   }

   public static IAsyncEnumerable<NATSPublishCompleted> PublishWithStaticDataProducer( this NATSConnection connection, String subject, Byte[] array, Int32 offset, Int32 count, String replySubject = null, Int64 repeatCount = 1, Int32 chunkCount = 1000 )
   {
      var chunk = Enumerable.Repeat( new NATSPublishData( subject, array, offset, count, replySubject ), chunkCount );
      return connection.PrepareStatementForExecution( connection.CreatePublishStatementBuilder( () =>
      {
         var remaining = repeatCount;
         return () =>
         {
            if ( remaining > 0 )
            {
               var original = remaining;
               remaining -= chunkCount;
               return new TDataProducerResult( remaining >= 0 ? chunk : chunk.Take( (Int32) original ) );
            }
            else
            {
               return default;
            }
         };
      } ) );
   }

   public static IAsyncEnumerable<NATSPublishCompleted> PublishWithDynamicSynchronousDataProducer( this NATSConnection connection, Func<IEnumerable<NATSPublishData>> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      return connection.PrepareStatementForExecution( connection.CreatePublishStatementBuilder( () =>
      {
         var remaining = repeatCount;
         return () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? new TDataProducerResult( producer() ) : default;
         };
      } ) );
   }

   public static IAsyncEnumerable<NATSPublishCompleted> PublishWithDynamicAsynchronousDataProducer( this NATSConnection connection, Func<Task<IEnumerable<NATSPublishData>>> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      return connection.PrepareStatementForExecution( connection.CreatePublishStatementBuilder( () =>
      {
         var remaining = repeatCount;
         return async () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? await producer() : default;
         };
      } ) );
   }

   public static IAsyncEnumerable<NATSPublishCompleted> PublishWithDynamicSynchronousDataProducer( this NATSConnection connection, Func<NATSPublishData> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      return connection.PrepareStatementForExecution( connection.CreatePublishStatementBuilder( () =>
      {
         var remaining = repeatCount;
         return () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? new TDataProducerResult( producer().Singleton() ) : default;
         };
      } ) );
   }

   public static IAsyncEnumerable<NATSPublishCompleted> PublishWithDynamicAsynchronousDataProducer( this NATSConnection connection, Func<Task<NATSPublishData>> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      return connection.PrepareStatementForExecution( connection.CreatePublishStatementBuilder( () =>
      {
         var remaining = repeatCount;
         return async () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? ( await producer() ).Singleton() : default;
         };
      } ) );
   }

}