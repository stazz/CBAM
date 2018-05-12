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
using System.Linq;
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
   /// <summary>
   /// This task extends <see cref="AbstractSQLConnectionUsingTask"/> in order to implement SQL dump functionality: executing SQL statements located in some file(s) against the database.
   /// </summary>
   public class ExecuteSQLStatementsTask : AbstractSQLConnectionUsingTask
   {
      /// <summary>
      /// Creates a new instance of <see cref="ExecuteSQLStatementsTask"/> with given callback to load NuGet assemblies.
      /// </summary>
      /// <param name="nugetResolver">The callback to asynchronously load assembly based on NuGet package ID and version.</param>
      /// <seealso cref="UtilPack.ResourcePooling.MSBuild.AbstractResourceUsingTask{TResource}(TNuGetPackageResolverCallback)"/>
      public ExecuteSQLStatementsTask( TNuGetPackageResolverCallback nugetResolver )
         : base( nugetResolver )
      {

      }

      /// <summary>
      /// This method implements <see cref="UtilPack.ResourcePooling.MSBuild.AbstractResourceUsingTask{TResource}.CheckTaskParametersBeforeResourcePoolUsage"/> and checks that all file paths passed via <see cref="SQLFilePaths"/> property exist.
      /// </summary>
      /// <returns><c>true</c> if all file paths passed via <see cref="SQLFilePaths"/> property exist; <c>false</c> otherwise.</returns>
      protected override Boolean CheckTaskParametersBeforeResourcePoolUsage()
      {
         return this.GetAllFilePaths().All( t =>
         {
            var retVal = false;
            try
            {
               retVal = File.Exists( t.Item2 );
            }
            catch
            {

            }
            if ( !retVal )
            {
               this.Log.LogError( $"Path \"{t.Item2}\" did not exist or was invalid." );
            }
            return retVal;
         } );
      }

      private IEnumerable<(ITaskItem, String)> GetAllFilePaths() => this.SQLFilePaths.Select( f =>
      {
         var path = f.GetMetadata( "FullPath" );
         try
         {
            path = Path.GetFullPath( path );
         }
         catch
         {

         }

         return (f, path);
      } );

      /// <summary>
      /// This method implements <see cref="UtilPack.ResourcePooling.MSBuild.AbstractResourceUsingTask{TResource}.UseResource(TResource)"/> and sequentially executes SQL statements from files given in <see cref="SQLFilePaths"/>.
      /// </summary>
      /// <param name="connection">The <see cref="SQLConnection"/> acquired from the connection pool loaded by <see cref="UtilPack.ResourcePooling.MSBuild.AbstractResourceUsingTask{TResource}"/>.</param>
      /// <returns>Always asynchronously returns <c>true</c>.</returns>
      protected override async Task<Boolean> UseResource( SQLConnection connection )
      {
         var defaultEncoding = GetEncoding( this.DefaultFileEncoding ) ?? Encoding.UTF8;
         connection.DisableEnumerableObservability = false;
         var whenExceptionInMultipleStatements = this.WhenExceptionInMultipleStatements;
         using ( var helper = new UsingHelper( () =>
           {
              connection.BeforeEnumerationStart -= this.Connection_BeforeStatementExecutionStart;
              connection.AfterEnumerationItemEncountered -= this.Connection_AfterStatementExecutionItemEncountered;
           } ) )
         {
            connection.BeforeEnumerationStart += this.Connection_BeforeStatementExecutionStart;
            connection.AfterEnumerationItemEncountered += this.Connection_AfterStatementExecutionItemEncountered;

            foreach ( var tuple in this.GetAllFilePaths() )
            {
               var path = tuple.Item2;

               try
               {
                  using ( var fs = File.Open( path, FileMode.Open, FileAccess.Read, FileShare.Read ) )
                  {
                     this.Log.LogMessage(
                        MessageImportance.High,
                        "Executed {0} statements from \"{1}\".",
                        await connection.ExecuteStatementsFromStreamAsync(
                           fs,
                           GetEncoding( tuple.Item1.GetMetadata( "Encoding" ) ) ?? defaultEncoding,
                           onException: exc =>
                           {
                              this.Log.LogError( exc.ToString() );
                              return whenExceptionInMultipleStatements;
                           },
                           token: this.CancellationToken
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
         }
         return true;
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

      /// <summary>
      /// Gets or sets the paths for files containing SQL statements to execute.
      /// </summary>
      /// <value>The paths for files containing SQL statements to execute.</value>
      /// <remarks>
      /// Each item may have <c>"Encoding"</c> metadata, which will be used if specified.
      /// Otherwise, the encoding will be the one specified by <see cref="DefaultFileEncoding"/>.
      /// </remarks>
      [Required]
      public ITaskItem[] SQLFilePaths { get; set; }

      /// <summary>
      /// Gets or sets the default <see cref="Encoding"/> for the files specified in <see cref="SQLFilePaths"/> property.
      /// </summary>
      /// <value>The default <see cref="Encoding"/> for the files specified in <see cref="SQLFilePaths"/> property.</value>
      /// <remarks>
      /// The value is transformed into <see cref="Encoding"/> by using <see cref="Encoding.GetEncoding(string)"/> property.
      /// If this property is not specified, the <see cref="Encoding.UTF8"/> encoding will be used.
      /// </remarks>
      public String DefaultFileEncoding { get; set; }

      /// <summary>
      /// Gets or sets the behaviour when an exception occurs within processing statements.
      /// </summary>
      /// <value>The behaviour when an exception occurs within processing statements.</value>
      public WhenExceptionInMultipleStatements WhenExceptionInMultipleStatements { get; set; } = WhenExceptionInMultipleStatements.Continue;

      private static Encoding GetEncoding( String encodingName )
      {
         return String.IsNullOrEmpty( encodingName ) ?
            null :
            Encoding.GetEncoding( encodingName );
      }
   }
}
