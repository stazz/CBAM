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

namespace CBAM.MSBuild.Abstractions
{
   public abstract class AbstractCBAMConnectionUsingTask<TConnection> : Microsoft.Build.Utilities.Task, ICancelableTask
   {
      private readonly CancellationTokenSource _cancelTokenSource;

      public AbstractCBAMConnectionUsingTask()
      {
         this._cancelTokenSource = new CancellationTokenSource();
      }

      public void Cancel()
      {
         this._cancelTokenSource.Cancel( false );
      }

      public override Boolean Execute()
      {
         // Reacquire must be called in same thread as yield -> run our Task synchronously
         var retVal = false;
         var yieldCalled = false;
         try
         {
            try
            {
               if ( !this.RunSynchronously )
               {
                  this.BuildEngine3.Yield();
                  yieldCalled = true;
               }
               this.ExecuteTaskAsync().GetAwaiter().GetResult();

               retVal = true;
            }
            catch ( TaskCanceledException )
            {
               // Canceled, do nothing
            }
            catch ( OperationCanceledException )
            {
               // Canceled, do nothing
            }
            catch ( Exception exc )
            {
               // Only log if we did not receive cancellation
               if ( !this._cancelTokenSource.IsCancellationRequested )
               {
                  this.Log.LogError( exc.ToString() );
               }
            }
         }
         finally
         {
            if ( yieldCalled )
            {
               this.BuildEngine3.Reacquire();
            }
         }

         return retVal;
      }

      private async Task ExecuteTaskAsync()
      {
         var poolProvider = await this.AcquireConnectionPoolProvider();
         var poolCreationArgs = await this.ProvideConnectionCreationParameters( poolProvider );
         var pool = await this.AcquireConnectionPool( poolProvider, poolCreationArgs );
         await pool.UseConnectionAsync( this.UseConnection, this._cancelTokenSource.Token );

      }

      protected virtual ValueTask<ConnectionPoolProvider<TConnection>> AcquireConnectionPoolProvider()
      {
         var providerAssemblyLocation = this.ConnectionPoolProviderAssemblyLocation;
         var providerTypeName = this.ConnectionPoolProviderTypeName;
         var providerTypeNameSpecified = !String.IsNullOrEmpty( providerTypeName );
         var providerBaseType = typeof( ConnectionPoolProvider<TConnection> ).GetTypeInfo();
         Type providerType;
         String errorMessage;
         if ( String.IsNullOrEmpty( providerAssemblyLocation ) )
         {
            if ( providerTypeNameSpecified )
            {
               providerType = Type.GetType( providerTypeName, false );
               errorMessage = $"Failed to load type \"{providerTypeName}\", make sure the name is assembly-qualified.";
            }
            else
            {
               providerType = null;
               errorMessage = $"The task must receive {nameof( ConnectionPoolProviderAssemblyLocation )} and/or {nameof( ConnectionPoolProviderTypeName )} parameters.";
            }
         }
         else
         {
            var providerAssemblyTuple = new ExplicitAssemblyLoader().LoadAssemblyFrom( providerAssemblyLocation );
            var providerAssembly = providerAssemblyTuple.LoadedAssembly;
            if ( providerTypeNameSpecified )
            {
               providerType = providerAssembly.GetType( providerTypeName, false );
               errorMessage = $"No type \"{providerTypeName}\" in assembly located in \"{providerAssemblyLocation}\".";
            }
            else
            {
               providerType = providerAssembly.DefinedTypes.FirstOrDefault( t => providerBaseType.IsAssignableFrom( t ) )?.AsType();
               errorMessage = $"Failed to find any type implementing \"{providerBaseType.AssemblyQualifiedName}\" in assembly located in \"{providerAssemblyLocation}\".";
            }
         }

         if ( providerType == null )
         {
            throw new InvalidOperationException( errorMessage );
         }
         else if ( !providerBaseType.IsAssignableFrom( providerType ) )
         {
            throw new InvalidOperationException( $"The loaded connection pool provider type \"{providerType.AssemblyQualifiedName}\" does not implement \"{providerBaseType.AssemblyQualifiedName}\"." );
         }

         return new ValueTask<ConnectionPoolProvider<TConnection>>( (ConnectionPoolProvider<TConnection>) Activator.CreateInstance( providerType ) );
      }

      protected abstract Task UseConnection( TConnection connection );

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

      public Boolean RunSynchronously { get; set; }

      public String ConnectionPoolProviderAssemblyLocation { get; set; }

      public String ConnectionPoolProviderTypeName { get; set; }

      public String ConnectionConfigurationFilePath { get; set; }
   }
}
