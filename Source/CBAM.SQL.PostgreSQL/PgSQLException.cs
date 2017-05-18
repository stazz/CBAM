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

namespace CBAM.SQL.PostgreSQL
{
   public sealed class PgSQLException : SQLException
   {
      private readonly PgSQLError[] _errors;

      public PgSQLException( PgSQLError error )
         : base( error.ToString() )
      {
         this._errors = new PgSQLError[] { error };
      }

      public PgSQLException( IList<PgSQLError> errors )
         : base( errors[0].ToString() )
      {
         this._errors = errors.ToArray();
      }

      public PgSQLException( String msg )
         : this( msg, null )
      {

      }

      public PgSQLException( String msg, Exception cause )
         : base( msg, cause )
      {
         this._errors = new PgSQLError[0];
      }

      public PgSQLError[] Errors
      {
         get
         {
            return this._errors;
         }
      }

      public override String ToString()
      {
         // Return textual description of all errors.
         var sb = new StringBuilder();
         sb.AppendLine( this.GetType().FullName );
         foreach ( var error in this._errors )
         {
            sb.Append( error );
         }
         sb.Append( this.StackTrace );

         return sb.ToString();
      }
   }

   public sealed class PgSQLError
   {
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

      public String Severity { get; }
      public String Code { get; }
      public String Message { get; }
      public String Detail { get; }
      public String Hint { get; }
      public String Position { get; }
      public String InternalPosition { get; }
      public String InternalQuery { get; }
      public String Where { get; }
      public String File { get; }
      public String Line { get; }
      public String Routine { get; }
      public String SchemaName { get; }
      public String TableName { get; }
      public String ColumnName { get; }
      public String DatatypeName { get; }
      public String ConstraintName { get; }

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

public static partial class E_PgSQL
{
   public static Boolean HasErrorCodes( this PgSQLException exception, params String[] codes )
   {
      return exception != null
         && exception.Errors.Length > 0
         && exception.Errors.Any( e => codes.Any( c => String.Equals( e.Code, c, StringComparison.OrdinalIgnoreCase ) ) );
   }
}