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
using CBAM.SQL.PostgreSQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilPack;

namespace CBAM.SQL.PostgreSQL
{
   /// <summary>
   /// This class extends <see cref="SQLException"/> to provide PostgreSQL-specific <see cref="PgSQLError"/> objects describing errors occurred in backend.
   /// </summary>
   /// <seealso cref="PgSQLError"/>
   public sealed class PgSQLException : SQLException
   {
      /// <summary>
      /// Creates a new instance of <see cref="PgSQLException"/> with one instance of backend <see cref="PgSQLError"/>.
      /// </summary>
      /// <param name="error">The information about backend error.</param>
      public PgSQLException( PgSQLError error )
         : base( error?.ToString() )
      {
         this.Errors = error == null ? Empty<PgSQLError>.Array : new PgSQLError[] { error };
      }

      /// <summary>
      /// Creates a new instance of <see cref="PgSQLException"/> with many instances of backend <see cref="PgSQLError"/>s.
      /// </summary>
      /// <param name="errors">Information about backend errors.</param>
      public PgSQLException( IList<PgSQLError> errors )
         : base( errors == null || errors.Count == 0 ? null : errors[0].ToString() )
      {
         this.Errors = errors?.ToArray() ?? Empty<PgSQLError>.Array;
      }

      /// <summary>
      /// Creates a new instance of <see cref="PgSQLException"/> with given message and optional inner exception.
      /// </summary>
      /// <param name="msg">The textual message describing an error.</param>
      /// <param name="cause">The optional inner exception.</param>
      public PgSQLException( String msg, Exception cause = null )
         : base( msg, cause )
      {
         this.Errors = Empty<PgSQLError>.Array;
      }

      /// <summary>
      /// Gets the array of all backend errors this <see cref="PgSQLException"/> holds.
      /// </summary>
      /// <value>The array of all backend errors this <see cref="PgSQLException"/> holds.</value>
      /// <seealso cref="PgSQLError"/>
      public PgSQLError[] Errors { get; }

   }

   /// <summary>
   /// This class encapsulates all information about an error occurred at PostgreSQL backend process.
   /// </summary>
   public sealed class PgSQLError
   {
      /// <summary>
      /// Creates a new instance of <see cref="PgSQLError"/> with given parameters.
      /// Any and all of the parameters may be <c>null</c>.
      /// </summary>
      /// <param name="severity">The error severity.</param>
      /// <param name="code">The error code.</param>
      /// <param name="message">The informative error message.</param>
      /// <param name="detail">Information about the details of error.</param>
      /// <param name="hint">Any possible hint about detecting or avoiding an error.</param>
      /// <param name="position">The position information in SQL code.</param>
      /// <param name="internalPosition">Internal position information in source code.</param>
      /// <param name="internalQuery">Internal query.</param>
      /// <param name="where">Function information in source code where error occurred.</param>
      /// <param name="file">The file name of source code where error occurred.</param>
      /// <param name="line">The line number of source code file where error occurred.</param>
      /// <param name="routine">Routine name, if applicable.</param>
      /// <param name="schemaName">Schema name, if applicable.</param>
      /// <param name="tableName">Table name, if applicable.</param>
      /// <param name="columnName">Column name, if applicable.</param>
      /// <param name="datatypeName">The type name of data, if applicable.</param>
      /// <param name="constraintName">The constraint name, if applicable.</param>
      public PgSQLError(
         String severity,
         String code,
         String message,
         String detail,
         String hint,
         String position,
         String internalPosition,
         String internalQuery,
         String where,
         String file,
         String line,
         String routine,
         String schemaName,
         String tableName,
         String columnName,
         String datatypeName,
         String constraintName
         )
      {
         this.Severity = severity;
         this.Code = code;
         this.Message = message;
         this.Detail = detail;
         this.Hint = hint;
         this.Position = position;
         this.InternalPosition = internalPosition;
         this.InternalQuery = internalQuery;
         this.Where = where;
         this.File = file;
         this.Line = line;
         this.Routine = routine;
         this.SchemaName = schemaName;
         this.TableName = tableName;
         this.ColumnName = columnName;
         this.DatatypeName = datatypeName;
         this.ConstraintName = constraintName;
      }

      /// <summary>
      /// Gets the error severity.
      /// </summary>
      /// <value>The error severity.</value>
      public String Severity { get; }

      /// <summary>
      /// Gets the error code.
      /// </summary>
      /// <value>The error code.</value>
      public String Code { get; }

      /// <summary>
      /// Gets the informative error message.
      /// </summary>
      /// <value>The informative error message.</value>
      public String Message { get; }

      /// <summary>
      /// Gets the information about the details of error.
      /// </summary>
      /// <value>The information about the details of error.</value>
      public String Detail { get; }

      /// <summary>
      /// Gets any possible hint about detecting or avoiding an error.
      /// </summary>
      /// <value>Any possible hint about detecting or avoiding an error.</value>
      public String Hint { get; }

      /// <summary>
      /// Gets the position information in SQL code.
      /// </summary>
      /// <value>The position information in SQL code.</value>
      public String Position { get; }

      /// <summary>
      /// Gets the internal position information in source code.
      /// </summary>
      /// <value>The internal position information in source code.</value>
      public String InternalPosition { get; }

      /// <summary>
      /// Gets the internal query.
      /// </summary>
      /// <value>The internal query.</value>
      public String InternalQuery { get; }

      /// <summary>
      /// Gets the function information in source code where error occurred.
      /// </summary>
      /// <value>The function information in source code where error occurred.</value>
      public String Where { get; }

      /// <summary>
      /// Gets the file name of source code where error occurred.
      /// </summary>
      /// <value>The file name of source code where error occurred.</value>
      public String File { get; }

      /// <summary>
      /// Gets the line number of source code file where error occurred.
      /// </summary>
      /// <value>The line number of source code file where error occurred.</value>
      public String Line { get; }

      /// <summary>
      /// Gets the routine name, if applicable.
      /// </summary>
      /// <value>The routine name, if applicable.</value>
      public String Routine { get; }

      /// <summary>
      /// Gets the schema name, if applicable.
      /// </summary>
      /// <value>The schema name, if applicable.</value>
      public String SchemaName { get; }

      /// <summary>
      /// Gets the table name, if applicable.
      /// </summary>
      /// <value>The table name, if applicable.</value>
      public String TableName { get; }

      /// <summary>
      /// Gets the column name, if applicable.
      /// </summary>
      /// <value>The column name, if applicable.</value>
      public String ColumnName { get; }

      /// <summary>
      /// Gets the type name of data, if applicable.
      /// </summary>
      /// <value>The type name of data, if applicable.</value>
      public String DatatypeName { get; }

      /// <summary>
      /// Gets the constraint name, if applicable.
      /// </summary>
      /// <value>The constraint name, if applicable.</value>
      public String ConstraintName { get; }

      /// <summary>
      /// Overrides <see cref="Object.ToString"/> to provide simple textual description of the object.
      /// </summary>
      /// <returns>The <see cref="Message"/>, followed by <see cref="Severity"/> if it is not <c>null</c> and not empty, followed by <see cref="Code"/> if it is not <c>null</c> and not empty, followed by <see cref="Hint"/> if it is not <c>null</c> and not empty.</returns>
      public override String ToString()
      {
         var sb = new StringBuilder();

         sb.Append( this.Message );

         if ( !String.IsNullOrEmpty( this.Severity ) )
         {
            sb.Append( ", Severity: " ).Append( this.Severity );
         }
         if ( !String.IsNullOrEmpty( this.Code ) )
         {
            sb.Append( ", Code: " ).Append( this.Code );
         }
         if ( !String.IsNullOrEmpty( this.Hint ) )
         {
            sb.Append( ", Hint: " ).Append( this.Hint );
         }

         return sb.ToString();
      }
   }
}

/// <summary>
/// This class contains extension methods for types defined in this assembly.
/// </summary>
public static partial class E_CBAM
{
   /// <summary>
   /// Helper method to check whether this <see cref="PgSQLException"/> has any given error codes.
   /// </summary>
   /// <param name="exception">This <see cref="PgSQLException"/>.</param>
   /// <param name="codes">The codes to check.</param>
   /// <returns><c>true</c> if this <see cref="PgSQLException"/> is not <c>null</c>, and has at least one <see cref="PgSQLError"/> in its <see cref="PgSQLException.Errors"/> array with error code contained in given <paramref name="codes"/>.</returns>
   public static Boolean HasErrorCodes( this PgSQLException exception, params String[] codes )
   {
      return exception != null
         && !codes.IsNullOrEmpty()
         && exception.Errors.Length > 0
         && exception.Errors.Any( e => codes.Any( c => String.Equals( e.Code, c, StringComparison.OrdinalIgnoreCase ) ) );
   }
}