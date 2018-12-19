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
using System.Threading;
using UtilPack;

namespace CBAM.NATS.Implementation
{
   internal sealed class NATSMessageImpl : NATSMessage
   {
      private Byte[] _data;
      private Int32 _dataLength;

      public NATSMessageImpl(
         String subject,
         Int64 subID,
         String replyTo,
         Byte[] data,
         Int32 dataLength
         )
      {
         this.Subject = ArgumentValidator.ValidateNotEmpty( nameof( subject ), subject );
         this.SubscriptionID = subID;
         this.ReplyTo = replyTo;
         this._data = data ?? Empty<Byte>.Array;
         this._dataLength = dataLength;
      }

      public String Subject { get; }

      public Int64 SubscriptionID { get; }

      public String ReplyTo { get; }

      public Int32 DataLength => this._data.Length;

      public Int32 CopyDataTo( Byte[] array, Int32 offsetInMessage, Int32 offsetInArray, Int32 count )
      {
         if ( offsetInMessage < 0 )
         {
            offsetInMessage = 0;
         }
         else if ( offsetInMessage >= this._dataLength )
         {
            offsetInMessage = this._dataLength - 1;
         }

         if ( count < 0 || count > this._dataLength - offsetInMessage )
         {
            count = this._dataLength - offsetInMessage;
         }

         this._data.CopyTo( array, ref offsetInMessage, offsetInArray, count );

         return count;
      }

      public Byte GetSingleByteAt( Int32 offsetInMessage )
      {
         if ( offsetInMessage < 0 || offsetInMessage >= this._dataLength )
         {
            throw new ArgumentException( nameof( offsetInMessage ) );
         }
         return this._data[offsetInMessage];
      }

      internal void SetData( Byte[] data, Int32 dataLength )
      {
         Interlocked.Exchange( ref this._data, data ?? Empty<Byte>.Array );
         Interlocked.Exchange( ref this._dataLength, dataLength );
      }
   }

   internal sealed class NATSPublishCompletedImpl : NATSPublishCompleted
   {

      public static NATSPublishCompleted Instance { get; } = new NATSPublishCompletedImpl();

      private NATSPublishCompletedImpl()
      {

      }
   }
}
