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
using System.Threading.Tasks;
using UtilPack;

using TReader = UtilPack.PeekablePotentiallyAsyncReader<System.Char?>;

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
      internal static async ValueTask<Int32[]> ParseStringForNextSQLStatement(
         TReader reader,
         Boolean standardConformingStrings,
         Func<Int32> onParameter
         )
      {
         var parenthesisLevel = 0;
         List<Int32> paramIndicesList = null;
         var queryEndEncountered = false;
         Char? prev1 = null, prev2 = null;
         Char? c;

         while ( !queryEndEncountered && ( c = await reader.TryReadNextAsync() ).HasValue )
         {
            switch ( c )
            {
               case '\'':
                  await ParseSingleQuotes( reader, standardConformingStrings, prev1, prev2 );
                  break;
               case '"':
                  await ParseDoubleQuotes( reader );
                  break;
               case '-':
                  await ParseLineComment( reader );
                  break;
               case '/':
                  await ParseBlockComment( reader );
                  break;
               case '$':
                  await ParseDollarQuotes( reader, prev1 );
                  break;
               case '(':
                  ++parenthesisLevel;
                  break;
               case ')':
                  --parenthesisLevel;
                  break;
               case '?':
                  if ( onParameter != null )
                  {
                     if ( paramIndicesList == null )
                     {
                        paramIndicesList = new List<Int32>();
                     }
                     paramIndicesList.Add( onParameter() );
                  }
                  break;
               case ';':
                  if ( parenthesisLevel == 0 )
                  {
                     queryEndEncountered = true;
                  }
                  break;
            }
            prev2 = prev1;
            prev1 = c;

         }

         return paramIndicesList == null ? null : paramIndicesList.ToArray();

      }


      // See http://www.postgresql.org/docs/9.1/static/sql-syntax-lexical.html for String Constants with C-style Escapes
      // Returns index of the single quote character ending this single quote sequence
      internal static async ValueTask<Boolean> ParseSingleQuotes(
         TReader reader,
         Boolean standardConformingStrings,
         Char? prev1,
         Char? prev2
         )
      {
         Char? c;
         if ( !standardConformingStrings
            && prev1.HasValue
            && prev2.HasValue
            && ( prev1 == 'e' || prev1 == 'E' )
            && CharTerminatesIdentifier( prev2.Value )
            )
         {
            // C-Style escaping
            // Treat backslashes as escape character
            Char prev = '\0';
            while ( ( c = await reader.TryReadNextAsync() ).HasValue )
            {
               if ( c != '\\' && prev != '\\' && await CheckSingleQuote( reader, c.Value ) )
               {
                  break;
               }
               prev = c.Value;
            }
         }
         else
         {
            // Don't treat backslashes as escape character
            while ( ( c = await reader.TryReadNextAsync() ).HasValue && !await CheckSingleQuote( reader, c.Value ) ) ;
         }

         return true;
      }

      internal static async ValueTask<Boolean> ParseDoubleQuotes(
         TReader reader
         )
      {
         Char? c;
         while ( ( c = await reader.TryReadNextAsync() ).HasValue )
         {
            if ( c == '"' )
            {
               // Check for double-doublequote
               if ( ( await reader.TryPeekAsync() ).IsOfValue( '"' ) )
               {
                  await reader.ReadNextAsync();
               }
               else
               {
                  break;
               }
            }
         }

         return true;
      }

      internal static async ValueTask<Boolean> ParseLineComment(
         TReader reader
         )
      {
         if ( ( await reader.TryPeekAsync() ).IsOfValue( '-' ) )
         {
            // Line comment starting
            Char? c;
            while ( ( c = await reader.TryReadNextAsync() ).HasValue && c != '\r' && c != '\n' ) ;
         }
         return true;
      }


      internal static async ValueTask<Boolean> ParseBlockComment(
         TReader reader
         )
      {
         if ( ( await reader.TryPeekAsync() ).IsOfValue( '*' ) )
         {
            // Block comment starting
            // SQL spec says block comments nest
            var level = 1;
            await reader.ReadNextAsync();
            Char? prev = null;
            Char? cur = null;
            var levelChanged = false;
            while ( level != 0 && ( cur = await reader.ReadNextAsync() ).HasValue )
            {
               var oldLevel = level;
               if ( !levelChanged ) // Don't process '*/*' or '/*/' twice
               {
                  if ( prev.HasValue )
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
               }

               levelChanged = level != oldLevel;
               prev = cur;
            }
         }

         return true;
      }

      // See http://www.postgresql.org/docs/9.1/static/sql-syntax-lexical.html for dollar quote spec
      internal static async ValueTask<Boolean> ParseDollarQuotes(
         TReader reader,
         Char? prev
         )
      {
         var c = await reader.TryPeekAsync();
         if ( c.HasValue && ( !prev.HasValue || !IsIdentifierContinuationCharacter( prev.Value ) ) )
         {
            Char[] tag = null;
            if ( c == '$' )
            {
               tag = Empty<Char>.Array;
            }
            else if ( IsDollarQuoteTagStartCharacter( c.Value ) )
            {
               var list = new List<Char>();
               while ( ( c = await reader.TryPeekAsync() ).HasValue )
               {
                  if ( c == '$' )
                  {
                     tag = list.ToArray();
                     break;
                  }
                  else if ( !IsDollarQuoteTagContinuationCharacter( c.Value ) )
                  {
                     break;
                  }
                  else
                  {
                     list.Add( await reader.ReadNextAsync() );
                  }
               }
            }

            if ( tag != null )
            {
               // Read the tag-ending dollar sign
               await reader.ReadNextAsync();
               var tagLen = tag.Length;

               var isEmptyTag = tagLen == 0;
               var array = isEmptyTag ? null : new Char[tagLen];
               var arrayIdx = tagLen - 1;
               while ( ( c = await reader.TryReadNextAsync() ).HasValue )
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

         return true;
      }

      // Returns true if this character ends string literal
      private static async ValueTask<Boolean> CheckSingleQuote(
         TReader reader,
         Char prevChar
         )
      {
         var retVal = prevChar == '\'';
         if ( retVal )
         {
            Char? peek;
            if ( ( peek = await reader.TryPeekAsync() ).HasValue )
            {
               // Check for double quotes
               if ( peek == '\'' )
               {
                  await reader.ReadNextAsync();
                  retVal = false;
               }
               else if ( peek == '\n' || peek == '\r' )
               {
                  // Check for newline-separated string literal ( http://www.postgresql.org/docs/9.1/static/sql-syntax-lexical.html )
                  while ( peek.HasValue && peek == '\n' || peek == '\r' )
                  {
                     peek = await reader.ReadNextAsync();
                  }

                  if ( peek.HasValue && peek == '\'' )
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

public static partial class E_CBAM
{
   internal static Boolean IsOfValue( this Char? nullable, Char value )
   {
      return nullable.HasValue && nullable.Value == value;
   }
}