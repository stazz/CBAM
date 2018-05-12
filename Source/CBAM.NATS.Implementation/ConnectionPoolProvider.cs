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
using UtilPack;
using UtilPack.ResourcePooling;

namespace CBAM.NATS.Implementation
{
   public sealed class NATSConnectionPoolProvider : AbstractAsyncResourceFactoryProvider<NATSConnection, NATSConnectionCreationInfo>
   {
      public static AsyncResourceFactory<NATSConnection, NATSConnectionCreationInfo> Factory { get; } = new DefaultAsyncResourceFactory<NATSConnection, NATSConnectionCreationInfo>( config => new NATSConnectionFactory( config, Encoding.ASCII.CreateDefaultEncodingInfo() ) );


      public NATSConnectionPoolProvider()
         : base( typeof( NATSConnectionCreationInfoData ) )
      {
      }

      protected override AsyncResourceFactory<NATSConnection, NATSConnectionCreationInfo> CreateFactory()
      {
         return Factory;
      }

      protected override NATSConnectionCreationInfo TransformFactoryParameters( Object creationParameters )
      {
         ArgumentValidator.ValidateNotNull( nameof( creationParameters ), creationParameters );

         NATSConnectionCreationInfo retVal;
         if ( creationParameters is NATSConnectionCreationInfoData creationData )
         {
            retVal = new NATSConnectionCreationInfo( creationData );

         }
         else if ( creationParameters is NATSConnectionCreationInfo creationInfo )
         {
            retVal = creationInfo;
         }
         else
         {
            throw new ArgumentException( $"The {nameof( creationParameters )} must be instance of {typeof( NATSConnectionCreationInfoData ).FullName}." );
         }

         return retVal;
      }
   }
}
