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
using System.Collections.Generic;
using System.Text;
using UtilPack;
using UtilPack.TabularData;

namespace CBAM.SQL.Implementation
{
   /// <summary>
   /// This class provides default and simple implementation for <see cref="SQLDataRow"/>.
   /// </summary>
   public class SQLDataRowImpl : AsyncDataRowImpl, SQLDataRow
   {
      private readonly ReadOnlyResettableLazy<SQLException[]> _warnings;

      /// <summary>
      /// Creates a new instance of <see cref="SQLDataRowImpl"/> with given parameters.
      /// </summary>
      /// <param name="rowMetadata">The <see cref="DataRowMetaData{TColumnMetaData}"/>.</param>
      /// <param name="columns">The columns array.</param>
      /// <param name="warnings">The resettable lazy to get warnings as array of <see cref="SQLException"/>s.</param>
      public SQLDataRowImpl(
         DataRowMetaData<AsyncDataColumnMetaData> rowMetadata,
         AsyncDataColumn[] columns,
         ReadOnlyResettableLazy<SQLException[]> warnings
         ) : base( rowMetadata, columns )
      {
         this._warnings = ArgumentValidator.ValidateNotNull( nameof( warnings ), warnings );
      }

      /// <summary>
      /// Implements <see cref="SQLStatementExecutionResult.Warnings"/> and gets current value of warnings as array of <see cref="SQLException"/>s.
      /// </summary>
      /// <value>Warnings as array of <see cref="SQLException"/>s.</value>
      public SQLException[] Warnings => this._warnings.Value;


   }
}
