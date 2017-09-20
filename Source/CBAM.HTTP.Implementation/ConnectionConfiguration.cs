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
using System.Net;
using System.Text;
using UtilPack;

namespace CBAM.HTTP
{
   public class HTTPConnectionEndPointConfiguration
   {
      public HTTPConnectionEndPointConfiguration(
         HTTPConnectionEndPointConfigurationData data
         )
      {
         this.Data = ArgumentValidator.ValidateNotNull( nameof( data ), data );
      }

      public HTTPConnectionEndPointConfigurationData Data { get; }

   }

   public class HTTPConnectionEndPointConfigurationData
   {
      public String Host { get; set; }
      public Int32 Port { get; set; }
      public Boolean IsSecure { get; set; }
   }

   public class HTTPConnectionConfiguration
   {
      public HTTPConnectionConfiguration(
         HTTPConnectionConfigurationData data
         )
      {
         this.Data = ArgumentValidator.ValidateNotNull( nameof( data ), data );
      }

      public HTTPConnectionConfigurationData Data { get; }

   }

   public class HTTPConnectionConfigurationData
   {
      public Int32 MaxReadBufferSize { get; set; }
      public Int32 MaxWriteBufferSize { get; set; }
   }
}
