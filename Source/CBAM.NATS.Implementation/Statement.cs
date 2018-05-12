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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

using TDataProducerResult = System.Threading.Tasks.ValueTask<System.Collections.Generic.IEnumerable<CBAM.NATS.NATSPublishData>>;

namespace CBAM.NATS
{
   using TDataProducer = Func<TDataProducerResult>;
   namespace Implementation
   {
      using TDataProducerFactory = Func<TDataProducer>;

      // TODO Move to UtilPack
      internal sealed class Reference<T>
      {

         public T Value { get; set; }
      }

      internal abstract class NATSStatementInformationImpl : NATSStatementInformation
      {
         public NATSStatementInformationImpl( String subject )
         {
            this.Subject = ArgumentValidator.ValidateNotEmpty( nameof( subject ), subject );
            if ( subject.ContainsNonASCIICharacters( IsInvalidASCIICharacter ) )
            {
               throw new ArgumentException( "Invalid subject name: " + subject );
            }
         }

         public String Subject { get; }

         public static Boolean IsInvalidASCIICharacter( Byte ch )
         {
            return ch <= 0x20;
         }
      }

      internal sealed class NATSSubscribeStatementInformationImpl : NATSStatementInformationImpl, NATSSubscribeStatementInformation
      {
         private readonly Reference<String> _queue;
         private readonly Reference<Int64> _autoUnsubscribeAfter;
         private readonly Reference<Func<NATSMessage, Boolean>> _dynamicUnsubscribe;

         public NATSSubscribeStatementInformationImpl(
            String subject,
            Reference<String> queue,
            Reference<Int64> autoUnsubscribeAfter,
            Reference<Func<NATSMessage, Boolean>> dynamicUnsubscribe
            ) : base( subject )
         {
            this._queue = ArgumentValidator.ValidateNotNull( nameof( queue ), queue );
            this._autoUnsubscribeAfter = ArgumentValidator.ValidateNotNull( nameof( autoUnsubscribeAfter ), autoUnsubscribeAfter );
            this._dynamicUnsubscribe = ArgumentValidator.ValidateNotNull( nameof( dynamicUnsubscribe ), dynamicUnsubscribe );
         }

         public String Queue => this._queue.Value;

         public Int64 AutoUnsubscribeAfter => this._autoUnsubscribeAfter.Value;

         public Func<NATSMessage, Boolean> DynamicUnsubscription => this._dynamicUnsubscribe.Value;
      }

      internal abstract class NATSStatementImpl : NATSStatement
      {
         internal NATSStatementImpl(
            NATSStatementInformationImpl information
            )
         {
            this.NATSStatementInformation = information;
         }

         public NATSStatementInformation NATSStatementInformation { get; }

         public String Subject => this.NATSStatementInformation.Subject;
      }

      internal sealed class NATSSubscribeStatementImpl : NATSStatementImpl, NATSSubscribeStatement
      {
         private readonly Reference<String> _queue;
         private readonly Reference<Int64> _autoUnsubscribeAfter;
         private readonly Reference<Func<NATSMessage, Boolean>> _dynamicUnsubscribe;

         internal NATSSubscribeStatementImpl(
            NATSSubscribeStatementInformationImpl information,
            Reference<String> queue,
            Reference<Int64> autoUnsubscribeAfter,
            Reference<Func<NATSMessage, Boolean>> dynamicUnsubscribe
            ) : base( information )
         {
            this._queue = ArgumentValidator.ValidateNotNull( nameof( queue ), queue );
            this._autoUnsubscribeAfter = ArgumentValidator.ValidateNotNull( nameof( autoUnsubscribeAfter ), autoUnsubscribeAfter );
            this._dynamicUnsubscribe = ArgumentValidator.ValidateNotNull( nameof( dynamicUnsubscribe ), dynamicUnsubscribe );
         }

         public String Queue
         {
            get => this._queue.Value;
            set => this._queue.Value = value;
         }

         public Int64 AutoUnsubscribeAfter
         {
            get => this._autoUnsubscribeAfter.Value;
            set => this._autoUnsubscribeAfter.Value = value;
         }

         public Func<NATSMessage, Boolean> DynamicUnsubscription
         {
            get => this._dynamicUnsubscribe.Value;
            set => this._dynamicUnsubscribe.Value = value;
         }

         String NATSSubscribeStatementInformation.Queue => this.Queue;

         Func<NATSMessage, Boolean> NATSSubscribeStatementInformation.DynamicUnsubscription => this.DynamicUnsubscription;
      }

      internal sealed class NATSPublishStatementImpl : NATSPublishStatement
      {
         private readonly Reference<TDataProducerFactory> _dataProducer;

         internal NATSPublishStatementImpl(
            NATSPublishStatementInformationImpl information,
            Reference<TDataProducerFactory> dataProducer
            )
         {
            this.NATSStatementInformation = ArgumentValidator.ValidateNotNull( nameof( information ), information );
            this._dataProducer = ArgumentValidator.ValidateNotNull( nameof( dataProducer ), dataProducer );
         }

         public TDataProducerFactory DataProducerFactory
         {
            get => this._dataProducer.Value;
            set => this._dataProducer.Value = value;
         }

         public NATSPublishStatementInformation NATSStatementInformation { get; }

         TDataProducerFactory NATSPublishStatementInformation.DataProducerFactory => this.DataProducerFactory;
      }

      internal sealed class NATSPublishStatementInformationImpl : NATSPublishStatementInformation
      {
         private readonly Reference<TDataProducerFactory> _dataProducer;

         public NATSPublishStatementInformationImpl(
            Reference<TDataProducerFactory> dataProducer
            )
         {
            this._dataProducer = ArgumentValidator.ValidateNotNull( nameof( dataProducer ), dataProducer );
         }

         public TDataProducerFactory DataProducerFactory => this._dataProducer.Value;
      }
   }
}

namespace UtilPack
{
   public static partial class UtilPackExtensions
   {
      public static Boolean ContainsNonASCIICharacters( this String str, Func<Byte, Boolean> additionalCheck = null )
      {
         var max = str.Length;
         var retVal = false;
         for ( var i = 0; i < str.Length && !retVal; ++i )
         {
            var ch = str[i];
            if ( ch > Byte.MaxValue || ( additionalCheck?.Invoke( (Byte) ch ) ?? false ) )
            {
               retVal = true;
            }
         }

         return retVal;
      }
   }
}
