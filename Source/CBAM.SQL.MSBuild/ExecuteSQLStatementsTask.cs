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

using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String, System.Threading.Tasks.Task<System.Reflection.Assembly>>;
using CBAM.Abstractions;
using UtilPack.AsyncEnumeration;

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
         return true;
      }

      //protected override Boolean CheckTaskParametersBeforeConnectionPoolUsage()
      //{
      //   var fp = Path.GetFullPath( this.SQLStatementsFilePath );
      //   var retVal = File.Exists( fp );
      //   if ( !retVal )
      //   {
      //      this.Log.LogError( "SQL statements \"{0}\" file does not exist", fp );
      //   }
      //   return retVal;

      //}

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

         connection.BeforeEnumerationStart += this.Connection_BeforeStatementExecutionStart;
         connection.AfterEnumerationItemEncountered += this.Connection_AfterStatementExecutionItemEncountered;

         using ( new UsingHelper( () =>
         {
            connection.BeforeEnumerationStart -= this.Connection_BeforeStatementExecutionStart;
            connection.AfterEnumerationItemEncountered -= this.Connection_AfterStatementExecutionItemEncountered;
         } ) )
         {
            foreach ( var item in this.SQLFilePaths )
            {
               var path = item.GetMetadata( "FullPath" );
               var exists = false;
               try
               {
                  path = Path.GetFullPath( path );
                  exists = File.Exists( path );
               }
               catch
               {
                  // Ignore
               }

               if ( exists )
               {
                  try
                  {
                     using ( var fs = File.Open( path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read ) )
                     {
                        this.Log.LogMessage(
                           MessageImportance.High,
                           "Executed {0} statements from \"{1}\".",
                           await connection.ExecuteStatementsFromStreamAsync(
                              fs,
                              encoding,
                              onException: exc =>
                              {
                                 this.Log.LogError( exc.ToString() );
                                 return WhenExceptionInMultipleStatements.Continue;
                              }
                           ),
                           path
                           );
                     }
                  }
                  catch ( Exception exc )
                  {
                     this.Log.LogErrorFromException( exc );
                  }
               }
               else
               {
                  this.Log.LogWarning( "Path {0} did not exist or was invalid.", path );
               }
            }
         }
      }

      private void Connection_BeforeStatementExecutionStart( EnumerationStartedEventArgs<SQLStatementBuilderInformation> args )
      {
         this.Log.LogMessage( MessageImportance.Low, "Statement: {0}", args.Metadata.SQL );
      }

      private void Connection_AfterStatementExecutionItemEncountered( EnumerationItemEventArgs<SQLStatementExecutionResult> args )
      {
         if ( args.Item is SingleCommandExecutionResult commandResult )
         {
            this.Log.LogMessage( MessageImportance.Low, "Result: {0} statement, {1} row{2} affected.", commandResult.CommandTag, commandResult.AffectedRows, commandResult.AffectedRows == 1 ? "" : "s" );
         }
      }

      [Required]
      public ITaskItem[] SQLFilePaths { get; set; }

      public String FileEncoding { get; set; }
   }
}
