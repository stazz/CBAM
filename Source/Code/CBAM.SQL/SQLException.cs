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
using System.Linq;
using System.Text;

namespace CBAM.SQL
{
   /// <summary>
   /// This class is common base class for all exceptions which signal some sort of SQL error in syntax or backend processing.
   /// </summary>
   public class SQLException : Exception
   {
      /// <summary>
      /// Creates a new instance of <see cref="SQLException"/> with given message and optional inner exception.
      /// </summary>
      /// <param name="msg">The error message.</param>
      /// <param name="cause">The optional inner exception.</param>
      public SQLException( String msg, Exception cause = null )
         : base( msg, cause )
      {

      }
   }
}
