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
using CBAM.SQL.MSBuild;
using System;
using System.Reflection;

namespace CBAM.SQL.PostgreSQL.Tests.MSBuild
{
   class Program
   {
      private const String TARGET = "Build";

      static void Main( String[] args )
      {
         var projRootElem = Microsoft.Build.Construction.ProjectRootElement.Create();
         var taskType = typeof( ExecuteSQLStatementsTask );
         var taskName = taskType.FullName;
         var usingTask = projRootElem.AddUsingTask( taskName, null, taskType.GetTypeInfo().Assembly.FullName );
         var target = projRootElem.AddTarget( TARGET );
         var taskExecution = target.AddTask( taskName );
         taskExecution.SetParameter(
            nameof( CBAM.MSBuild.Abstractions.AbstractCBAMConnectionUsingTask<Object>.ConnectionPoolProviderTypeName ),
            "CBAM.SQL.PostgreSQL.PgSQLConnectionPoolProvider"
            );
         taskExecution.SetParameter(
            nameof( CBAM.MSBuild.Abstractions.AbstractCBAMConnectionUsingTask<Object>.ConnectionPoolProviderAssemblyLocation ),
            @"..\CBAM.SQL.PostgreSQL.Implementation\bin\Debug\netcoreapp1.1\CBAM.SQL.PostgreSQL.Implementation.dll"
            );
         taskExecution.SetParameter(
            nameof( CBAM.MSBuild.Abstractions.AbstractCBAMConnectionUsingTask<Object>.ConnectionConfigurationFilePath ),
            @"..\CBAM.SQL.PostgreSQL.Tests\test_config.json"
            );
         taskExecution.SetParameter(
            nameof( ExecuteSQLStatementsTask.SQLStatementsFilePath ),
            "test_dump.txt"
            );

         var result = Microsoft.Build.Execution.BuildManager.DefaultBuildManager.Build(
            new Microsoft.Build.Execution.BuildParameters(),
            new Microsoft.Build.Execution.BuildRequestData( new Microsoft.Build.Execution.ProjectInstance( projRootElem ), new[] { TARGET } )
            );
         if ( result.ResultsByTarget[TARGET].ResultCode != Microsoft.Build.Execution.TargetResultCode.Success )
         {
            throw new Exception( "The task execution was not success." );
         }
      }
   }
}