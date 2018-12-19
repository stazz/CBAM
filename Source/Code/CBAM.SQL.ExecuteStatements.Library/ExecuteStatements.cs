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
using AsyncEnumeration.Observability;
using CBAM.SQL;
using CBAM.SQL.ExecuteStatements.Library;
using ResourcePooling.Async.Abstractions;
using ResourcePooling.Async.ConfigurationLoading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String, System.Threading.CancellationToken, System.Threading.Tasks.Task<System.Reflection.Assembly>>;

namespace CBAM.SQL.ExecuteStatements.Library
{
   /// <summary>
   /// This configuration interface provides readonly access to data needed by <see cref="E_CBAM.ExecuteSQLStatements(SQLExecutionConfiguration, ResourceFactoryDynamicCreationFileBasedConfiguration, TNuGetPackageResolverCallback, Action{String}, Action{SingleCommandExecutionResult}, Action{String, Int64}, Action{SQLException}, Action{Exception}, CancellationToken)"/> and <see cref="E_CBAM.ExecuteSQLStatements(SQLExecutionConfiguration, SQLConnection, Action{String}, Action{SingleCommandExecutionResult}, Action{String, Int64}, Action{SQLException}, Action{Exception}, CancellationToken)"/> methods provided by this library.
   /// </summary>
   public interface SQLExecutionConfiguration
   {
      /// <summary>
      /// Gets the paths to files containing SQL code to run.
      /// </summary>
      /// <value>The paths to files containing SQL code to run.</value>
      String[] Files { get; }

      /// <summary>
      /// Gets the encoding for each file. File which should use <see cref="DefaultFileEncoding"/> should have <c>null</c> element here.
      /// </summary>
      /// <value>The encoding for each file.</value>
      String[] FileEncodings { get; }

      /// <summary>
      /// Gets the default encoding for files. If <c>null</c> or empty, a <see cref="UTF8Encoding"/> will be used.
      /// </summary>
      /// <value>The default encoding for files.</value>
      String DefaultFileEncoding { get; }

      /// <summary>
      /// Gets the default strategy when error occurs.
      /// </summary>
      /// <value>The default strategy when error occurs.</value>
      WhenExceptionInMultipleStatements SQLErrorStrategy { get; }
   }

   /// <summary>
   /// This class implements <see cref="SQLExecutionConfiguration"/> with setters in order to be useable with Microsoft.Extensions.Configuration packages.
   /// </summary>
   public class DefaultSQLExecutionConfiguration : SQLExecutionConfiguration
   {
      /// <inheritdoc />
      public String[] Files { get; set; }

      /// <inheritdoc />
      public String[] FileEncodings { get; set; }

      /// <inheritdoc />
      public String DefaultFileEncoding { get; set; }

      /// <inheritdoc />
      public WhenExceptionInMultipleStatements SQLErrorStrategy { get; set; }
   }
}

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// Given information in this <see cref="SQLExecutionConfiguration"/> and <see cref="ResourceFactoryDynamicCreationFileBasedConfiguration"/>, along with custom callbacks for various functionality, executes SQL statements from the files to database.
   /// </summary>
   /// <param name="sqlFileConfiguration">This <see cref="SQLExecutionConfiguration"/> containing information about SQL files and execution behaviour.</param>
   /// <param name="sqlConnectionConfiguration">The <see cref="ResourceFactoryDynamicCreationFileBasedConfiguration"/> containing information about how to connect to SQL database.</param>
   /// <param name="loadNuGetPackageAssembly">The callback to load NuGet package.</param>
   /// <param name="beforeSQLStatement">The callback executed before each SQL statement. May be <c>null</c>.</param>
   /// <param name="afterSQLStatement">The callback executed after each SQL statement. May be <c>null</c>.</param>
   /// <param name="afterSQLFile">The callback executed after each SQL file has been finished. May be <c>null</c>.</param>
   /// <param name="onSQLError">The callback executed if SQL error occurs. May be <c>null</c>.</param>
   /// <param name="onOtherError">The callback executed if other error occurs. May be <c>null</c>.</param>
   /// <param name="token">The <see cref="CancellationToken"/> to use for asynchronous operations.</param>
   /// <returns>Asynchronously returns <c>void</c>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLExecutionConfiguration"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="sqlConnectionConfiguration"/> or <paramref name="loadNuGetPackageAssembly"/> is <c>null</c>.</exception>
   public static async Task ExecuteSQLStatements(
      this SQLExecutionConfiguration sqlFileConfiguration,
      ResourceFactoryDynamicCreationFileBasedConfiguration sqlConnectionConfiguration,
      TNuGetPackageResolverCallback loadNuGetPackageAssembly,
      Action<String> beforeSQLStatement,
      Action<SingleCommandExecutionResult> afterSQLStatement,
      Action<String, Int64> afterSQLFile,
      Action<SQLException> onSQLError,
      Action<Exception> onOtherError,
      CancellationToken token
      )
   {
      var usage = await ( await ArgumentValidator.ValidateNotNull( nameof( sqlConnectionConfiguration ), sqlConnectionConfiguration ).CreateAsyncResourceFactoryUsingConfiguration<SQLConnection>( loadNuGetPackageAssembly, token ) )
         .CreateOneTimeUseResourcePool()
         .GetResourceUsageAsync( token );
      try
      {
         await sqlFileConfiguration.ExecuteSQLStatements(
            usage.Resource,
            beforeSQLStatement,
            afterSQLStatement,
            afterSQLFile,
            onSQLError,
            onOtherError,
            usage.CancellationToken
            );
      }
      finally
      {
         await usage.DisposeAsync();
      }
   }

   /// <summary>
   /// Given information in this <see cref="SQLExecutionConfiguration"/> and <see cref="SQLConnection"/>, along with custom callbacks for various functionality, executes SQL statements from the files to database.
   /// </summary>
   /// <param name="sqlFileConfiguration">This <see cref="SQLExecutionConfiguration"/> containing information about SQL files and execution behaviour.</param>
   /// <param name="connection">The existing <see cref="SQLConnection"/> to the SQL database.</param>
   /// <param name="beforeSQLStatement">The callback executed before each SQL statement. May be <c>null</c>.</param>
   /// <param name="afterSQLStatement">The callback executed after each SQL statement. May be <c>null</c>.</param>
   /// <param name="afterSQLFile">The callback executed after each SQL file has been finished. May be <c>null</c>.</param>
   /// <param name="onSQLError">The callback executed if SQL error occurs. May be <c>null</c>.</param>
   /// <param name="onOtherError">The callback executed if other error occurs. May be <c>null</c>.</param>
   /// <param name="token">The <see cref="CancellationToken"/> to use for asynchronous operations.</param>
   /// <returns>Asynchronously returns <c>void</c>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLExecutionConfiguration"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="connection"/> is <c>null</c>.</exception>
   public static async Task ExecuteSQLStatements(
      this SQLExecutionConfiguration sqlFileConfiguration,
      SQLConnection connection,
      Action<String> beforeSQLStatement,
      Action<SingleCommandExecutionResult> afterSQLStatement,
      Action<String, Int64> afterSQLFile,
      Action<SQLException> onSQLError,
      Action<Exception> onOtherError,
      CancellationToken token
      )
   {
      Encoding GetEncoding( String encodingName )
      {
         return String.IsNullOrEmpty( encodingName ) ?
            null :
            Encoding.GetEncoding( encodingName );
      }

      void Connection_BeforeStatementExecutionStart( EnumerationStartedEventArgs<SQLStatementBuilderInformation> args )
      {
         beforeSQLStatement?.Invoke( args.Metadata.SQL );
      }

      void Connection_AfterStatementExecutionItemEncountered( EnumerationItemEventArgs<SQLStatementExecutionResult> args )
      {
         if ( args.Item is SingleCommandExecutionResult commandResult )
         {
            afterSQLStatement?.Invoke( commandResult );
         }
      }

      ArgumentValidator.ValidateNotNull( nameof( connection ), connection ).DisableEnumerableObservability = false;
      var defaultEncoding = GetEncoding( sqlFileConfiguration.DefaultFileEncoding ) ?? Encoding.UTF8;
      var whenExceptionInMultipleStatements = sqlFileConfiguration.SQLErrorStrategy;
      using ( var helper = new UsingHelper( () =>
      {
         connection.BeforeEnumerationStart -= Connection_BeforeStatementExecutionStart;
         connection.AfterEnumerationItemEncountered -= Connection_AfterStatementExecutionItemEncountered;
      } ) )
      {
         connection.BeforeEnumerationStart += Connection_BeforeStatementExecutionStart;
         connection.AfterEnumerationItemEncountered += Connection_AfterStatementExecutionItemEncountered;

         var files = sqlFileConfiguration.Files;
         var fileEncodings = sqlFileConfiguration.FileEncodings;
         foreach ( (var path, var encoding) in sqlFileConfiguration.Files.SideBySideWith( sqlFileConfiguration.FileEncodings ) )
         {
            try
            {
               using ( var fs = File.Open( path, FileMode.Open, FileAccess.Read, FileShare.Read ) )
               {
                  var statementCount = await connection.ExecuteStatementsFromStreamAsync(
                        fs,
                        GetEncoding( encoding ) ?? defaultEncoding,
                        onException: exc =>
                        {
                           onSQLError?.Invoke( exc );
                           return whenExceptionInMultipleStatements;
                        },
                        token: token
                     );
                  afterSQLFile?.Invoke( path, statementCount );
               }
            }
            catch ( Exception exc )
            {
               onOtherError?.Invoke( exc );
            }
         }
      }
   }

   // TODO Move to UtilPack + add up to 8-parametrized versions
   internal static IEnumerable<(T, U)> SideBySideWith<T, U>( this IEnumerable<T> first, IEnumerable<U> second )
   {
      using ( var firstEnumerator = first.GetEnumerator() )
      using ( var secondEnumerator = second.GetEnumerator() )
      {
         var secondNotEnded = true;
         while ( firstEnumerator.MoveNext() )
         {
            yield return (firstEnumerator.Current, secondNotEnded && ( secondNotEnded = secondEnumerator.MoveNext() ) ? secondEnumerator.Current : default);
         }
      }
   }
}