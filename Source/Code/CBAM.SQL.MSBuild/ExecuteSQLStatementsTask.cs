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
using AsyncEnumeration.Observability;
using CBAM.SQL.ExecuteStatements.Library;
using Microsoft.Build.Framework;
using ResourcePooling.Async.Abstractions;
using ResourcePooling.Async.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilPack;
using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String, System.Threading.CancellationToken, System.Threading.Tasks.Task<System.Reflection.Assembly>>;

namespace CBAM.SQL.MSBuild
{
   /// <summary>
   /// This task extends <see cref="AbstractSQLConnectionUsingTask"/> in order to implement SQL dump functionality: executing SQL statements located in some file(s) against the database.
   /// </summary>
   public class ExecuteSQLStatementsTask : AbstractSQLConnectionUsingTask, SQLExecutionConfiguration
   {
      private readonly Lazy<String[]> _files;
      private readonly Lazy<String[]> _fileEncodings;

      /// <summary>
      /// Creates a new instance of <see cref="ExecuteSQLStatementsTask"/> with given callback to load NuGet assemblies.
      /// </summary>
      /// <param name="nugetResolver">The callback to asynchronously load assembly based on NuGet package ID and version.</param>
      /// <seealso cref="AbstractResourceUsingTask{TResource}(TNuGetPackageResolverCallback)"/>
      public ExecuteSQLStatementsTask( TNuGetPackageResolverCallback nugetResolver )
         : base( nugetResolver )
      {
         this._files = new Lazy<String[]>( () => this.TransformSQLFilePaths( "FullPath", p =>
         {
            try
            {
               return Path.GetFullPath( p );
            }
            catch
            {
               return p;
            }
         } ) );
         this._fileEncodings = new Lazy<String[]>( () => this.TransformSQLFilePaths( "Encoding" ) );
      }

      /// <summary>
      /// This method implements <see cref="AbstractResourceUsingTask{TResource}.CheckTaskParametersBeforeResourcePoolUsage"/> and checks that all file paths passed via <see cref="SQLFilePaths"/> property exist.
      /// </summary>
      /// <returns><c>true</c> if all file paths passed via <see cref="SQLFilePaths"/> property exist; <c>false</c> otherwise.</returns>
      protected override Boolean CheckTaskParametersBeforeResourcePoolUsage()
      {
         return this._files.Value.All( path =>
         {
            var retVal = false;
            try
            {
               retVal = File.Exists( path );
            }
            catch
            {

            }
            if ( !retVal )
            {
               this.Log.LogError( $"Path \"{path}\" did not exist or was invalid." );
            }
            return retVal;
         } );
      }

      /// <summary>
      /// This method implements <see cref="AbstractResourceUsingTask{TResource}.UseResource"/> and sequentially executes SQL statements from files given in <see cref="SQLFilePaths"/>.
      /// </summary>
      /// <param name="connection">The <see cref="SQLConnection"/> acquired from the connection pool loaded by <see cref="AbstractResourceUsingTask{TResource}"/>.</param>
      /// <returns>Always asynchronously returns <c>true</c>.</returns>
      protected override async Task<Boolean> UseResource( AsyncResourceUsage<SQLConnection> connection )
      {
         await this.ExecuteSQLStatements(
            connection.Resource,
            sql => this.Log.LogMessage( MessageImportance.Low, "Statement: {0}", sql ),
            commandResult => this.Log.LogMessage( MessageImportance.Low, "Result: {0} statement, {1} row{2} affected.", commandResult.CommandTag, commandResult.AffectedRows, commandResult.AffectedRows == 1 ? "" : "s" ),
            ( file, statementCount ) => this.Log.LogMessage( MessageImportance.High, "Executed {0} statements from \"{1}\".", statementCount, file ),
            sqlError => this.Log.LogError( sqlError.ToString() ),
            otherError => this.Log.LogErrorFromException( otherError ),
            connection.CancellationToken
            );
         return true;
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
      /// The value is transformed into <see cref="Encoding"/> by using <see cref="Encoding.GetEncoding(String)"/> property.
      /// If this property is not specified, the <see cref="Encoding.UTF8"/> encoding will be used.
      /// </remarks>
      public String DefaultFileEncoding { get; set; }

      String[] SQLExecutionConfiguration.Files { get; }

      String[] SQLExecutionConfiguration.FileEncodings { get; }

      /// <summary>
      /// Gets or sets the behaviour when an exception occurs within processing statements.
      /// </summary>
      /// <value>The behaviour when an exception occurs within processing statements.</value>
      public WhenExceptionInMultipleStatements SQLErrorStrategy { get; set; } = WhenExceptionInMultipleStatements.Continue;

      private String[] TransformSQLFilePaths( String metadata, Func<String, String> processor = null )
      {
         return this.SQLFilePaths.Select( f =>
         {
            var md = f.GetMetadata( metadata );
            if ( processor != null )
            {
               md = processor( md );
            }
            return md;
         } ).ToArray();
      }
   }
}
