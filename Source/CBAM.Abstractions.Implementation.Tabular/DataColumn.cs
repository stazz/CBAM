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
using System;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.TabularData;

namespace CBAM.Abstractions.Implementation.Tabular
{
   public abstract class DataColumnSUKSWithConnectionFunctionality<TConnectionFunctionality, TStatement, TStatementInformation, TStatementCreationArgs, TEnumerationItem, TVendor> : DataColumnSUKS
      where TStatement : TStatementInformation
      where TConnectionFunctionality : ConnectionFunctionalitySU<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerationItem, TVendor>
      where TEnumerationItem : class
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   {

      public DataColumnSUKSWithConnectionFunctionality(
         DataColumnMetaData metadata,
         Int32 thisStreamIndex,
         AsyncDataColumn previousColumn,
         TConnectionFunctionality connectionFunctionality,
         ReservedForStatement reservedForStatement
         ) : base( metadata, thisStreamIndex, previousColumn )
      {
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );
         this.ReservedForStatement = ArgumentValidator.ValidateNotNull( nameof( reservedForStatement ), reservedForStatement );
      }

      protected TConnectionFunctionality ConnectionFunctionality { get; }

      protected ReservedForStatement ReservedForStatement { get; }

      protected override async ValueTask<Int32> DoReadFromStreamAsync( Byte[] array, Int32 offset, Int32 count )
      {
         return await this.ConnectionFunctionality.UseStreamWithinStatementAsync( this.ReservedForStatement, async () => await this.ReadFromStreamWhileReservedAsync( array, offset, count ) );
      }

      protected abstract ValueTask<Int32> ReadFromStreamWhileReservedAsync( Byte[] array, Int32 offset, Int32 count );

   }
}
