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
using CBAM.NATS;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.CBAM.NATS.Implementation
{
   public class AbstractNATSTest
   {
      protected const Int32 TIMEOUT = 10000;

      //private const String CONTAINER_ID_PROPERTY = "ContainerID";

      //public TestContext TestContext { get; set; }

      //[TestInitialize]
      //public Task InitializeNATSServer()
      //{
      //   // TestInitialize method does not pick up Timeout attribute, so we have to do this ourselves:
      //   return PerformInitializeNATSServer()
      //      .TimeoutAfter( TimeSpan.FromMinutes( 5 ) ); // Give 5 mins to potentially download image + start it
      //}

      protected static NATSConnectionCreationInfoData GetNATSConfiguration(
         Boolean encrypted = false
         )
      {
         return new ConfigurationBuilder()
            .AddJsonFile( System.IO.Path.GetFullPath( Environment.GetEnvironmentVariable( $"CBAM_TEST_NATS_CONFIG{ ( encrypted ? "_ENCRYPTED" : "" ) }" ) ) )
            .Build()
            .Get<NATSConnectionCreationInfoData>();
      }

      //private static async Task<String> PerformInitializeNATSServer()
      //{
      //   // 1. Start process: docker run --rm -d -p 127.0.0.1:x:4222 nats 
      //   //    x from env var, if x is all numeric chars -> then "127.0.0.1:x", otherwise just "x"
      //   var containerID = await StartNATSContainer();

      //   // 2. Wait till port from config becomes available, by periodically connecting to the port and seeing if connection is successful
      //   var config = GetNATSConfiguration();

      //}

      //private static async Task<String> StartNATSContainer()
      //{
      //   // 1. Start process: docker run --rm -d -p 127.0.0.1:x:4222 nats 
      //   //    x from env var, if x is all numeric chars -> then "127.0.0.1:x", otherwise just "x"
      //   var portSpec = Environment.GetEnvironmentVariable( "CBAM_TEST_NATS_HOST_PORT_SPEC" );
      //   var p = new Process()
      //   {
      //      StartInfo = new ProcessStartInfo()
      //      {
      //         FileName = "docker",
      //         ArgumentList =
      //         {
      //            "run", // Run container
      //            "--rm", // Remove container when it exits/is killed
      //            "-d", // Don't wait for the container to exit before exiting this process
      //            "-p", // Port specification
      //            ( portSpec.Any(c => !Char.IsDigit(c)) ?
      //               portSpec : // Non-numeric characters, use portSpec as such
      //               $"127.0.0.1:{portSpec}" ) + ":4222", // Only numeric characters, use "127.0.0.1" prefix in order not to expose to other machines
      //            "nats:1.3.0-linux" // Image name
      //         },
      //         UseShellExecute = false,
      //         CreateNoWindow = true,
      //         RedirectStandardOutput = true,
      //         RedirectStandardError = true,
      //      }
      //   };

      //   var stdout = new StringBuilder();
      //   p.OutputDataReceived += ( sender, args ) =>
      //   {
      //      if ( args.Data is String line ) // Will be null on process shutdown
      //      {
      //         stdout.Append( line );
      //      }
      //   };
      //   var stderr = new StringBuilder();
      //   p.ErrorDataReceived += ( sender, args ) =>
      //   {
      //      if ( args.Data is String line ) // Will be null on process shutdown
      //      {
      //         stderr.Append( line );
      //      }
      //   };

      //   p.Start();
      //   p.BeginOutputReadLine();
      //   p.BeginErrorReadLine();

      //   while ( !p.WaitForExit( 0 ) )
      //   {
      //      await Task.Delay( 500 );
      //   }

      //   // Process.HasExited has following documentation:
      //   // When standard output has been redirected to asynchronous event handlers, it is possible that output processing will
      //   // not have completed when this property returns true. To ensure that asynchronous event handling has been completed,
      //   // call the WaitForExit() overload that takes no parameter before checking HasExited.
      //   p.WaitForExit();
      //   while ( !p.HasExited )
      //   {
      //      await Task.Delay( 50 );
      //   }

      //   if ( stderr.Length > 0 )
      //   {
      //      throw new Exception( "Error when starting NATS container: " + stderr );
      //   }

      //   return stdout.ToString();
      //}

      //private static async Task WaitForNATSContainer( IPEndPoint ep )
      //{
      //   using ( var sock = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp ) )
      //   {
      //      try
      //      {
      //         await sock.ConnectAsync( ep ).TimeoutAfter( TimeSpan.FromMilliseconds( 500 ) );
      //      }
      //      catch
      //      {

      //      }
      //   }
      //}
   }
}

//public static class E_CBAM
//{
//   // From https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
//   // Probably should be moved to UtilPack
//   public static async Task<TResult> TimeoutAfter<TResult>( this Task<TResult> task, TimeSpan timeout )
//   {

//      using ( var timeoutCancellationTokenSource = new CancellationTokenSource() )
//      {
//         var completedTask = await Task.WhenAny( task, Task.Delay( timeout, timeoutCancellationTokenSource.Token ) );
//         if ( completedTask == task )
//         {
//            // Cancel token in order to cancel delay task, which uses up system timer
//            timeoutCancellationTokenSource.Cancel();
//            return await task;  // Very important in order to propagate exceptions
//         }
//         else
//         {
//            throw new TimeoutException( "The operation has timed out." );
//         }
//      }
//   }

//   public static async Task TimeoutAfter( this Task task, TimeSpan timeout )
//   {

//      using ( var timeoutCancellationTokenSource = new CancellationTokenSource() )
//      {
//         var completedTask = await Task.WhenAny( task, Task.Delay( timeout, timeoutCancellationTokenSource.Token ) );
//         if ( completedTask == task )
//         {
//            // Cancel token in order to cancel delay task, which uses up system timer
//            timeoutCancellationTokenSource.Cancel();
//            await task;  // Very important in order to propagate exceptions
//         }
//         else
//         {
//            throw new TimeoutException( "The operation has timed out." );
//         }
//      }
//   }
//}