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
using CBAM.Abstractions;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String, System.Threading.Tasks.Task<System.Reflection.Assembly>>;

namespace CBAM.MSBuild.Abstractions
{
   public abstract class AbstractCBAMConnectionUsingTask<TConnection> : Microsoft.Build.Utilities.Task, ICancelableTask
   {
      private readonly CancellationTokenSource _cancellationSource;
      private readonly TNuGetPackageResolverCallback _nugetPackageResolver;

      public AbstractCBAMConnectionUsingTask( TNuGetPackageResolverCallback nugetPackageResolver )
      {
         this._cancellationSource = new CancellationTokenSource();
         this._nugetPackageResolver = nugetPackageResolver;
      }

      public override Boolean Execute()
      {
         // Reacquire must be called in same thread as yield -> run our Task synchronously
         var retVal = false;
         if ( this.CheckTaskParametersBeforeConnectionPoolUsage() )
         {
            var yieldCalled = false;
            var be = (IBuildEngine3) this.BuildEngine;
            try
            {
               try
               {
                  if ( !this.RunSynchronously )
                  {
                     be.Yield();
                     yieldCalled = true;
                  }
                  this.ExecuteTaskAsync().GetAwaiter().GetResult();

                  retVal = !this.Log.HasLoggedErrors;
               }
               catch ( OperationCanceledException )
               {
                  // Canceled, do nothing
               }
               catch ( Exception exc )
               {
                  // Only log if we did not receive cancellation
                  if ( !this._cancellationSource.IsCancellationRequested )
                  {
                     this.Log.LogErrorFromException( exc );
                  }
               }
            }
            finally
            {
               if ( yieldCalled )
               {
                  be.Reacquire();
               }
            }
         }
         return retVal;
      }

      public void Cancel()
      {
         this._cancellationSource.Cancel( false );
      }

      public async Task ExecuteTaskAsync()
      {
         var poolProvider = await this.AcquireConnectionPoolProvider();
         if ( poolProvider == null )
         {
            this.Log.LogError( "Failed to acquire connection pool provider." );
         }
         else
         {
            var poolCreationArgs = await this.ProvideConnectionCreationParameters( poolProvider );
            var pool = await this.AcquireConnectionPool( poolProvider, poolCreationArgs );
            await pool.UseConnectionAsync( this.UseConnection, this._cancellationSource.Token );
         }
      }

      protected abstract Boolean CheckTaskParametersBeforeConnectionPoolUsage();

      protected virtual ValueTask<Object> ProvideConnectionCreationParameters(
         ConnectionPoolProvider<TConnection> poolProvider
         )
      {
         var path = this.ConnectionConfigurationFilePath;
         if ( String.IsNullOrEmpty( path ) )
         {
            throw new InvalidOperationException( "Connection configuration file path was not provided." );
         }

         return new ValueTask<Object>( new ConfigurationBuilder()
            .AddJsonFile( System.IO.Path.GetFullPath( path ) )
            .Build()
            .Get( poolProvider.DefaultTypeForCreationParameter ) );
      }

      protected virtual ValueTask<ConnectionPool<TConnection>> AcquireConnectionPool(
         ConnectionPoolProvider<TConnection> provider,
         Object poolCreationArgs
         )
      {
         return new ValueTask<ConnectionPool<TConnection>>( provider.CreateOneTimeUseConnectionPool( poolCreationArgs ) );
      }

      protected abstract Task UseConnection( TConnection connection );

      protected async ValueTask<ConnectionPoolProvider<TConnection>> AcquireConnectionPoolProvider()
      {
         var resolver = this._nugetPackageResolver;
         ConnectionPoolProvider<TConnection> retVal = null;
         if ( resolver != null )
         {
            var assembly = await this._nugetPackageResolver(
               this.ConnectionPoolProviderPackageID, // package ID
               this.ConnectionPoolProviderVersion,  // optional package version
               this.ConnectionPoolProviderAssemblyPath // optional assembly path within package
               );
            if ( assembly != null )
            {
               // Now search for the type
               var typeName = this.ConnectionPoolProviderTypeName;
               var parentType = typeof( ConnectionPoolProvider<TConnection> ).GetTypeInfo();
               var checkParentType = !String.IsNullOrEmpty( typeName );
               Type providerType;
               if ( checkParentType )
               {
                  // Instantiate directly
                  providerType = assembly.GetType( typeName, false, false );
               }
               else
               {
                  // Search for first available
                  providerType = assembly.DefinedTypes.FirstOrDefault( t => parentType.IsAssignableFrom( t ) )?.AsType();
               }

               if ( providerType != null )
               {
                  if ( !checkParentType || parentType.IsAssignableFrom( providerType.GetTypeInfo() ) )
                  {
                     // All checks passed, instantiate the pool provider
                     retVal = (ConnectionPoolProvider<TConnection>) Activator.CreateInstance( providerType );
                  }
                  else
                  {
                     this.Log.LogError( $"The type \"{providerType.FullName}\" in \"{assembly}\" does not have required parent type \"{parentType.FullName}\"." );
                  }
               }
               else
               {
                  this.Log.LogError( $"Failed to find type within assembly in \"{assembly}\", try specify {nameof( ConnectionPoolProviderTypeName )} parameter." );
               }
            }
            else
            {
               this.Log.LogError( $"Failed to load connection pool provider package \"{this.ConnectionPoolProviderPackageID}\"." );
            }
         }
         else
         {
            this.Log.LogError( "Task must be provided callback to load NuGet packages (just make constructor taking it as argument and use UtilPack.NuGet.MSBuild task factory)." );
         }

         return retVal;
      }

      public Boolean RunSynchronously { get; set; }

      [Required]
      public String ConnectionPoolProviderPackageID { get; set; }

      public String ConnectionPoolProviderVersion { get; set; }

      public String ConnectionPoolProviderAssemblyPath { get; set; }

      public String ConnectionPoolProviderTypeName { get; set; }

      public String ConnectionConfigurationFilePath { get; set; }
   }
}
