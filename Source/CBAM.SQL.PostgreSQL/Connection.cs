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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL.PostgreSQL
{
   public partial interface PgSQLConnection : SQLConnection
   {
      event EventHandler<NotificationEventArgs> NotificationEvent;

      Task CheckNotificationsAsync();

      TypeRegistry TypeRegistry { get; }

      Int32 BackendProcessID { get; }
   }

   public sealed class NotificationEventArgs : EventArgs
   {
      private readonly Int32 _pid;
      private readonly String _name;
      private readonly String _payload;

      public NotificationEventArgs( Int32 pid, String name, String payload )
      {
         this._pid = pid;
         this._name = name;
         this._payload = payload;
      }

      public Int32 ProcessID
      {
         get
         {
            return this._pid;
         }
      }

      public String Name
      {
         get
         {
            return this._name;
         }
      }

      public String Payload
      {
         get
         {
            return this._payload;
         }
      }
   }
}
