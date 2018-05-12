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
using System.Text;
using UtilPack;

namespace CBAM.NATS
{
   public interface NATSMessage
   {
      String Subject { get; }
      Int64 SubscriptionID { get; }

      String ReplyTo { get; }

      Int32 DataLength { get; }

      Int32 CopyDataTo( Byte[] array, Int32 offset, Int32 count = -1 );
   }

   public interface NATSPublishCompleted
   {

   }
}

public static partial class E_CBAM
{

   public static Byte[] CreateDataArray( this NATSMessage obj )
   {
      var len = obj.DataLength;
      Byte[] retVal;
      if ( len > 0 )
      {
         retVal = new Byte[obj.DataLength];
         obj.CopyDataTo( retVal, 0 );
      }
      else
      {
         retVal = Empty<Byte>.Array;
      }

      return retVal;

   }

   public static void CopyDataTo( this NATSMessage message, Byte[] array ) => message.CopyDataTo( array, 0 );
}