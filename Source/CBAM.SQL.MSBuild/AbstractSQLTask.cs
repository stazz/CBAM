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
using UtilPack.ResourcePooling.MSBuild;

using TNuGetPackageResolverCallback = System.Func<System.String, System.String, System.String, System.Threading.Tasks.Task<System.Reflection.Assembly>>;

namespace CBAM.SQL.MSBuild
{
   /// <summary>
   /// This class binds the generic parameter of <see cref="AbstractResourceUsingTask{TResource}"/> to <see cref="SQLConnection"/>.
   /// </summary>
   public abstract class AbstractSQLConnectionUsingTask : AbstractResourceUsingTask<SQLConnection>
   {
      /// <summary>
      /// Initializes new instance of <see cref="AbstractSQLConnectionUsingTask"/> with given callback to load NuGet assemblies.
      /// </summary>
      /// <param name="nugetResolver">The callback to asynchronously load assembly based on NuGet package ID and version.</param>
      /// <seealso cref="AbstractResourceUsingTask{TResource}(TNuGetPackageResolverCallback)"/>
      public AbstractSQLConnectionUsingTask( TNuGetPackageResolverCallback nugetResolver )
         : base( nugetResolver )
      {

      }
   }

}
