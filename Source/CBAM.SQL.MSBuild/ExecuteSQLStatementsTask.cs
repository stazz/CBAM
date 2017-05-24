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
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL.MSBuild
{
   public class ExecuteSQLStatementsTask : AbstractSQLConnectionUsingTask
   {
      protected override async Task UseConnection( SQLConnection connection )
      {
         Encoding encoding;
         var encodingName = this.FileEncoding;
         if ( String.IsNullOrEmpty( encodingName ) )
         {
            encoding = Encoding.UTF8;
         }
         else
         {
            encoding = Encoding.GetEncoding( encodingName );
         }

         using ( var fs = System.IO.File.Open( this.SQLStatementsFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read ) )
         {
            await connection.ExecuteStatementsFromStreamAsync(
               fs,
               encoding.CreateDefaultEncodingInfo()
               );
         }
      }

      [Required]
      public String SQLStatementsFilePath { get; set; }

      public String FileEncoding { get; set; }
   }
}
