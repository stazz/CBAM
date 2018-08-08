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
using CBAM.NATS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

using TDataProducerResult = System.Threading.Tasks.ValueTask<System.Collections.Generic.IEnumerable<CBAM.NATS.NATSPublishData>>;

namespace CBAM.NATS
{

   using TDataProducerFactory = Func<Func<TDataProducerResult>>;

   public interface NATSStatementInformation
   {
      String Subject { get; }
   }

   public interface NATSPublishStatementInformation
   {
      //String ReplySubject { get; }

      TDataProducerFactory DataProducerFactory { get; }

   }

   public interface NATSSubscribeStatementInformation : NATSStatementInformation
   {
      String Queue { get; }

      Int64 AutoUnsubscribeAfter { get; }

      Func<NATSMessage, Boolean> DynamicUnsubscription { get; }
   }

   public interface NATSStatement : NATSStatementInformation
   {
      NATSStatementInformation NATSStatementInformation { get; }
   }

   public interface NATSSubscribeStatement : NATSStatement, NATSSubscribeStatementInformation
   {
      new String Queue { get; set; }

      new Int64 AutoUnsubscribeAfter { get; set; }

      new Func<NATSMessage, Boolean> DynamicUnsubscription { get; set; }

   }

   public interface NATSPublishStatement : NATSPublishStatementInformation
   {
      NATSPublishStatementInformation NATSStatementInformation { get; }

      new TDataProducerFactory DataProducerFactory { get; set; }


   }

   public struct NATSPublishData
   {
      public NATSPublishData(
         String subject,
         Byte[] data,
         Int32 offset = -1,
         Int32 count = -1,
         String replySubject = null
         )
      {
         this.Subject = ArgumentValidator.ValidateNotEmpty( nameof( subject ), subject );
         this.Data = data;
         this.Offset = Math.Max( 0, offset );
         this.Count = count < 0 ? Math.Max( 0, ( data?.Length ?? 0 ) - this.Offset ) : count;
         this.ReplySubject = String.IsNullOrEmpty( replySubject ) ? null : replySubject;
      }

      public String Subject { get; }

      public String ReplySubject { get; }

      public Byte[] Data { get; }
      public Int32 Offset { get; }
      public Int32 Count { get; }

   }
}

public static partial class E_CBAM
{
   public static NATSPublishStatement WithStaticDataProducerForWholeArray( this NATSPublishStatement statement, String subject, Byte[] array, String replySubject = null, Int64 repeatCount = 1, Int32 chunkCount = 1000 )
   {
      return statement.WithStaticDataProducer( subject, array, 0, array?.Length ?? 0, replySubject, repeatCount, chunkCount );
   }

   public static NATSPublishStatement WithStaticDataProducer( this NATSPublishStatement statement, String subject, Byte[] array, Int32 offset, Int32 count, String replySubject = null, Int64 repeatCount = 1, Int32 chunkCount = 1000 )
   {
      var chunk = Enumerable.Repeat( new NATSPublishData( subject, array, offset, count, replySubject ), chunkCount );
      statement.DataProducerFactory = () =>
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
      };
      return statement;
   }

   public static NATSPublishStatement WithDynamicSynchronousDataProducer( this NATSPublishStatement statement, Func<IEnumerable<NATSPublishData>> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      statement.DataProducerFactory = () =>
      {
         var remaining = repeatCount;
         return () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? new TDataProducerResult( producer() ) : default;
         };
      };
      return statement;
   }

   public static NATSPublishStatement WithDynamicAsynchronousDataProducer( this NATSPublishStatement statement, Func<Task<IEnumerable<NATSPublishData>>> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      statement.DataProducerFactory = () =>
      {
         var remaining = repeatCount;
         return async () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? await producer() : default;
         };
      };

      return statement;
   }

   public static NATSPublishStatement WithDynamicSynchronousDataProducer( this NATSPublishStatement statement, Func<NATSPublishData> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      statement.DataProducerFactory = () =>
      {
         var remaining = repeatCount;
         return () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? new TDataProducerResult( producer().Singleton() ) : default;
         };
      };
      return statement;
   }

   public static NATSPublishStatement WithDynamicAsynchronousDataProducer( this NATSPublishStatement statement, Func<Task<NATSPublishData>> producer, Int64 repeatCount = -1 )
   {
      var hasMax = repeatCount >= 0;
      statement.DataProducerFactory = () =>
      {
         var remaining = repeatCount;
         return async () =>
         {
            return !hasMax || Interlocked.Decrement( ref remaining ) >= 0 ? ( await producer() ).Singleton() : default;
         };
      };

      return statement;
   }
}