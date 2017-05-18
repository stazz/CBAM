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
using System.IO;
using System.Text;
using UtilPack;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal static class Parser
   {
      // Helper class to keep track of how many chars has been read from the underlying reader
      private sealed class TextReaderWrapper : TextReader
      {
         private readonly TextReader _reader;
         private Int32 _charsRead;

         internal TextReaderWrapper( TextReader reader )
         {
            ArgumentValidator.ValidateNotNull( "Reader", reader );

            this._reader = reader;
            this._charsRead = 0;
         }

         public Int32 CharsRead
         {
            get
            {
               return this._charsRead;
            }
         }

         public override Int32 Read()
         {
            var retVal = this._reader.Read();
            if ( retVal != -1 )
            {
               ++this._charsRead;
            }
            return retVal;
         }

         public override Int32 Peek()
         {
            return this._reader.Peek();
         }

         protected override void Dispose( bool disposing )
         {
            // Do nothing - we don't want to close underlying reader.
         }
      }

      // Returns amount of characters read
      internal static Int32 ParseStringForNextSQLStatement( TextReader queryParam, Boolean standardConformingStrings, out Int32[] paramIndices )
      {
         using ( var query = new TextReaderWrapper( queryParam ) )
         {
            var parenthesisLevel = 0;
            List<Int32> paramIndicesList = null;
            var queryEndEncountered = false;
            Int32 prev1 = -1, prev2 = -1;
            Int32 c;

            while ( !queryEndEncountered && ( c = query.Read() ) != -1 )
            {
               switch ( c )
               {
                  case '\'':
                     ParseSingleQuotes( query, standardConformingStrings, prev1, prev2 );
                     break;
                  case '"':
                     ParseDoubleQuotes( query );
                     break;
                  case '-':
                     ParseLineComment( query );
                     break;
                  case '/':
                     ParseBlockComment( query );
                     break;
                  case '$':
                     ParseDollarQuotes( query, prev1 );
                     break;
                  case '(':
                     ++parenthesisLevel;
                     break;
                  case ')':
                     --parenthesisLevel;
                     break;
                  case '?':
                     if ( paramIndicesList == null )
                     {
                        paramIndicesList = new List<Int32>();
                     }
                     paramIndicesList.Add( query.CharsRead - 1 );
                     break;
                  case ';':
                     if ( parenthesisLevel == 0 )
                     {
                        queryEndEncountered = true;
                     }
                     break;
               }
               prev1 = c;
               prev2 = prev1;

            }

            paramIndices = paramIndicesList == null ? null : paramIndicesList.ToArray();

            return query.CharsRead;
         }

      }


      // See http://www.postgresql.org/docs/9.1/static/sql-syntax-lexical.html for String Constants with C-style Escapes
      // Returns index of the single quote character ending this single quote sequence
      internal static void ParseSingleQuotes( TextReader str, Boolean standardConformingStrings, Int32 prev1, Int32 prev2 )
      {
         Int32 c;
         if ( !standardConformingStrings
            && ( prev1 == 'e' || prev1 == 'E' )
            && prev2 != -1
            && CharTerminatesIdentifier( (Char) prev2 )
            )
         {
            // Treat backslashes as escape character
            var prev = -1;
            while ( ( c = str.Read() ) != -1 )
            {
               if ( c != '\\' && prev != '\\' && CheckSingleQuote( str, c ) )
               {
                  break;
               }
               prev = c;
            }
         }
         else
         {
            // Don't treat backslashes as escape character
            while ( ( c = str.Read() ) != -1 && !CheckSingleQuote( str, c ) ) ;
         }
      }

      // Returns index of the double quote character ending this double quote sequence
      internal static void ParseDoubleQuotes( TextReader str )
      {
         Int32 c;
         while ( ( c = str.Read() ) != -1 )
         {
            if ( c == '"' )
            {
               // Check for double-doublequote
               if ( str.Peek() == '"' )
               {
                  str.Read();
               }
               else
               {
                  break;
               }
            }
         }
      }

      // Returns index of the character ending line comment (newline), if it is line comment (-- <comment>) in question
      internal static void ParseLineComment( TextReader str )
      {
         if ( str.Peek() == '-' )
         {
            // Line comment starting
            Int32 c;
            while ( ( c = str.Read() ) != -1 && c != '\r' && c != '\n' ) ;
         }

      }

      // Returns index of the character ending block comment, if it is block comment (/* ... */ in question)
      internal static void ParseBlockComment( TextReader str )
      {
         if ( str.Peek() == '*' )
         {
            // Block comment starting
            // SQL spec says block comments nest
            var level = 1;
            str.Read();
            Int32 prev = -1, cur = -1;
            var levelChanged = false;
            while ( level != 0 && ( cur = str.Read() ) != -1 )
            {
               var oldLevel = level;
               if ( !levelChanged ) // Don't process '*/*' or '/*/' twice
               {
                  if ( prev == '*' && cur == '/' )
                  {
                     // Block comment ending
                     --level;
                  }
                  else if ( prev == '/' && cur == '*' )
                  {
                     // Nested block comment
                     ++level;
                  }
               }

               levelChanged = level != oldLevel;
               prev = cur;
            }
         }
      }

      // See http://www.postgresql.org/docs/9.1/static/sql-syntax-lexical.html for dollar quote spec
      internal static void ParseDollarQuotes( TextReader str, Int32 prev )
      {
         var c = str.Peek();
         if ( c != -1 && ( prev == -1 || !IsIdentifierContinuationCharacter( (Char) prev ) ) )
         {
            Char[] tag = null;
            if ( c == '$' )
            {
               tag = Empty<Char>.Array;
            }
            else if ( IsDollarQuoteTagStartCharacter( (Char) c ) )
            {
               var list = new List<Char>();
               while ( ( c = str.Peek() ) != -1 )
               {
                  if ( c == '$' )
                  {
                     tag = list.ToArray();
                     break;
                  }
                  else if ( !IsDollarQuoteTagContinuationCharacter( (Char) c ) )
                  {
                     break;
                  }
                  else
                  {
                     list.Add( (Char) str.Read() );
                  }
               }
            }

            if ( tag != null )
            {
               // Read the tag-ending dollar sign
               str.Read();
               var tagLen = tag.Length;

               var isEmptyTag = tagLen == 0;
               var array = isEmptyTag ? null : new Char[tagLen];
               var arrayIdx = tagLen - 1;
               while ( ( c = str.Read() ) != -1 )
               {
                  if ( c == '$' )
                  {
                     // Check if this is double-dollar-sign for empty tag, or that previous characters are same as tag
                     if ( isEmptyTag && prev == '$' )
                     {
                        break;
                     }
                     else if ( !isEmptyTag && CheckForCircularlyFilledArray( tag, tagLen, array, arrayIdx ) )
                     {
                        break;
                     }
                  }

                  if ( !isEmptyTag )
                  {
                     if ( tag.Length > 1 )
                     {
                        if ( arrayIdx == tag.Length - 1 )
                        {
                           arrayIdx = 0;
                        }
                        else
                        {
                           ++arrayIdx;
                        }
                     }
                     array[arrayIdx] = (Char) c;
                  }

                  prev = c;
               }
            }
         }
      }

      // Returns true if this character ends string literal
      private static Boolean CheckSingleQuote( TextReader str, Int32 prevChar )
      {
         var retVal = prevChar == '\'';
         if ( retVal )
         {
            var peek = str.Peek();
            if ( peek != -1 )
            {
               // Check for double quotes
               if ( peek == '\'' )
               {
                  str.Read();
                  retVal = false;
               }
               else if ( peek == '\n' || peek == '\r' )
               {
                  // Check for newline-separated string literal ( http://www.postgresql.org/docs/9.1/static/sql-syntax-lexical.html )
                  while ( peek == '\n' || peek == '\r' )
                  {
                     peek = str.Read();
                  }

                  if ( peek == '\'' )
                  {
                     retVal = false;
                  }
               }
            }
         }

         return retVal;
      }

      // Returns true if character terminates identifier in backend parser
      private static Boolean CharTerminatesIdentifier( Char c )
      {
         return c == '"' || IsSpace( c ) || IsOperatorChar( c );
      }

      // The functions below must be kept in sync with logic of pgsql/src/backend/parser/scan.l

      // Returns true if character is treated as space character in backend parser
      internal static Boolean IsSpace( Char c )
      {
         return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f';
      }

      // Returns true if the given character is a valid character for an operator in backend parser
      private static Boolean IsOperatorChar( Char c )
      {
         /*
          * Extracted from operators defined by {self} and {op_chars}
          * in pgsql/src/backend/parser/scan.l.
          */
         return ",()[].;:+-*/%^<>=~!@#&|`?".IndexOf( c ) != -1;
      }

      // Checks wehether character is valid as second or later character of an identifier
      private static Boolean IsIdentifierContinuationCharacter( Char c )
      {
         return ( c >= 'a' && c <= 'z' )
            || ( c >= 'A' && c <= 'Z' )
            || c == '_'
            || c > 127
            || ( c >= '0' && c <= '9' )
            || c == '$';
      }

      // Checks wthether character is valid as first character of dollar quote tag
      private static Boolean IsDollarQuoteTagStartCharacter( Char c )
      {
         return ( c >= 'a' && c <= 'z' )
            || ( c >= 'A' && c <= 'Z' )
            || c == '_'
            || c > 127;
      }

      // Checks whether character is valid as second or later character of dollar quote tag
      private static Boolean IsDollarQuoteTagContinuationCharacter( Char c )
      {
         return ( c >= 'a' && c <= 'z' )
            || ( c >= 'A' && c <= 'Z' )
            || c == '_'
            || ( c >= '0' && c <= '9' )
            || c > 127;

      }

      // auxArrayIndex = index of last set character in auxArray
      internal static Boolean CheckForCircularlyFilledArray( Char[] referenceDataArray, Int32 refLen, Char[] auxArray, Int32 auxArrayIndex )
      {
         var min = auxArrayIndex + 1 - refLen;
         var i = refLen - 1;
         if ( min >= 0 )
         {
            // Enough to check that last auxLen chars are same (do check backwards)
            for ( var j = auxArrayIndex; i >= 0; --i, --j )
            {
               if ( referenceDataArray[i] != auxArray[j] )
               {
                  return false;
               }
            }
         }
         else
         {
            var j = auxArrayIndex;
            for ( ; j >= 0; --j, --i )
            {
               if ( referenceDataArray[i] != auxArray[j] )
               {
                  return false;
               }
            }

            for ( j = auxArray.Length - 1; i >= 0; --i, --j )
            {
               if ( referenceDataArray[i] != auxArray[j] )
               {
                  return false;
               }
            }
         }

         return true;
      }
   }
}
