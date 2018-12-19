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
using CBAM.SQL.ExecuteStatements.Application;
using CBAM.SQL.ExecuteStatements.Library;
using NuGetUtils.Lib.EntryPoint;
using ResourcePooling.Async.ConfigurationLoading;
using System;
using System.Threading;
using System.Threading.Tasks;
using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String, System.Threading.CancellationToken, System.Threading.Tasks.Task<System.Reflection.Assembly>>;

[assembly: ConfiguredEntryPoint( typeof( Application ), nameof( Application.Main ) )]

namespace CBAM.SQL.ExecuteStatements.Application
{
   /// <summary>
   /// This class contains the entrypoint for application executing SQL files to SQL databases.
   /// </summary>
   /// <remarks>
   /// The application is meant to be run using nuget-exec .NET Core (global) tool.
   /// </remarks>
   public static class Application
   {
      /// <summary>
      /// This method is call-through to <see cref="M:E_CBAM.ExecuteSQLStatements(CBAM.SQL.ExecuteStatements.Library.SQLExecutionConfiguration,ResourcePooling.Async.ConfigurationLoading.ResourceFactoryDynamicCreationFileBasedConfiguration,System.Func{System.String,System.String,System.String,System.Threading.CancellationToken,System.Threading.Tasks.Task{System.Reflection.Assembly}},System.Action{System.String},System.Action{CBAM.SQL.SingleCommandExecutionResult},System.Action{System.String,System.Int64},System.Action{CBAM.SQL.SQLException},System.Action{System.Exception},System.Threading.CancellationToken)"/>
      /// The various callbacks print to <see cref="Console.Out"/> and <see cref="Console.Error"/>.
      /// </summary>
      /// <param name="sqlFileConfiguration">The configuration about SQL files.</param>
      /// <param name="sqlConnectionConfiguration">The configuration about SQL connection.</param>
      /// <param name="loadNuGetPackageAssembly">The callback to load NuGet assemblies (will be provided by nuget-exec).</param>
      /// <param name="token">The <see cref="CancellationToken"/> to use for asynchronous operations (will be provided by nuget-exec).</param>
      /// <returns></returns>
      public static Task Main(
         DefaultSQLExecutionConfiguration sqlFileConfiguration,
         DefaultResourceFactoryDynamicCreationFileBasedConfiguration sqlConnectionConfiguration,
         TNuGetPackageResolverCallback loadNuGetPackageAssembly,
         CancellationToken token
         )
      {
         return sqlFileConfiguration.ExecuteSQLStatements(
            sqlConnectionConfiguration,
            loadNuGetPackageAssembly,
            sql => Console.Out.WriteLineAsync( $"SQL: {sql}" ),
            result => Console.Out.WriteLineAsync( $"Result: {result.CommandTag} statement, {result.AffectedRows} row{( result.AffectedRows == 1 ? "" : "s" )} affected." ),
            ( file, statementsCount ) => Console.Out.WriteLineAsync( $"Executed {statementsCount} statements from \"{file}\"." ),
            sqlError => Console.Error.WriteLineAsync( $"SQL error: {sqlError.Message}" ),
            otherError => Console.Error.WriteLineAsync( $"Other error:\n{otherError}" ),
            token
            );
      }
   }
}
