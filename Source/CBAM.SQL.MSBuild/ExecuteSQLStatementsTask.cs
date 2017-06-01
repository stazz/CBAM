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
using CBAM.MSBuild.Abstractions;
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UtilPack;
using Microsoft.Build.Utilities;
using System.IO;

using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String[], System.Boolean, System.String, System.Reflection.Assembly>;
using CBAM.Abstractions;

namespace CBAM.SQL.MSBuild
{
   public class ExecuteSQLStatementsTask : AbstractSQLConnectionUsingTask
   {
      public ExecuteSQLStatementsTask( TNuGetPackageResolverCallback nugetResolver )
         : base( nugetResolver )
      {

      }

      protected override Boolean CheckTaskParametersBeforeConnectionPoolUsage()
      {
         return File.Exists( Path.GetFullPath( this.SQLStatementsFilePath ) );
      }

      protected override async System.Threading.Tasks.Task UseConnection( SQLConnection connection )
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

         connection.BeforeStatementExecutionStart += this.Connection_BeforeStatementExecutionStart;
         connection.AfterStatementExecutionItemEncountered += this.Connection_AfterStatementExecutionItemEncountered;

         using ( new UsingHelper( () =>
         {
            connection.BeforeStatementExecutionStart -= this.Connection_BeforeStatementExecutionStart;
            connection.AfterStatementExecutionItemEncountered -= this.Connection_AfterStatementExecutionItemEncountered;
         } ) )
         {
            var path = Path.GetFullPath( this.SQLStatementsFilePath );
            using ( var fs = File.Open( path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read ) )
            {
               this.Log.LogMessage(
                  MessageImportance.High,
                  "Executed {0} statements from \"{1}\".",
                  await connection.ExecuteStatementsFromStreamAsync(
                     fs,
                     encoding.CreateDefaultEncodingInfo()
                  ),
                  path
                  );
            }
         }
      }

      private void Connection_BeforeStatementExecutionStart( StatementExecutionStartedEventArgs<StatementBuilder> args )
      {
         this.Log.LogMessage( MessageImportance.Low, "Statement: {0}", args.Statement.SQL );
      }

      private void Connection_AfterStatementExecutionItemEncountered( StatementExecutionResultEventArgs<SQLStatementExecutionResult> args )
      {
         if ( args.Item is SingleCommandExecutionResult commandResult )
         {
            this.Log.LogMessage( MessageImportance.Low, "Result: {0} statement, {1} rows affected.", commandResult.CommandTag, commandResult.AffectedRows );
         }
      }

      [Required]
      public String SQLStatementsFilePath { get; set; }

      public String FileEncoding { get; set; }
   }
}
