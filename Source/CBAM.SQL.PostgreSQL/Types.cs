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
using CBAM.SQL.PostgreSQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL.PostgreSQL
{
   // TODO implement IPgTypeWithBackendTextFormat
   public struct PgSQLInterval : IComparable, IComparable<PgSQLInterval>, IEquatable<PgSQLInterval>
   {
      //// Getting decimal digits from System.Decimal: http://stackoverflow.com/questions/13477689/find-number-of-decimal-places-in-decimal-value-regardless-of-culture
      //private delegate Int32 GetDigitsDelegate( ref Decimal value );

      //private static class DecimalHelper
      //{
      //   public static readonly GetDigitsDelegate GetDigits;

      //   static DecimalHelper()
      //   {
      //      var value = Expression.Parameter( typeof( Decimal ).MakeByRefType(), "value" );

      //      //return (value.flags & ~Int32.MinValue) >> 16
      //      var digits = Expression.RightShift(
      //          Expression.And( Expression.Field( value, "flags" ), Expression.Constant( ~Int32.MinValue, typeof( Int32 ) ) ),
      //          Expression.Constant( 16, typeof( Int32 ) ) );

      //      GetDigits = Expression.Lambda<GetDigitsDelegate>( digits, value ).Compile();
      //   }
      //}


      #region Consts
      private const Int32 DAYS_PER_MONTH = 30;
      private const Int32 MONTHS_PER_YEAR = 12;
      private const Int64 TICKS_PER_MONTH = TimeSpan.TicksPerDay * DAYS_PER_MONTH;
      internal const Int64 TICKS_PER_MICROSECOND = TimeSpan.TicksPerMillisecond / 1000;
      internal const Int64 MICROSECONDS_PER_SECOND = 1000000;
      internal const Int64 MILLISECONDS_PER_SECOND = 1000;
      internal const Int64 SECONDS_PER_MINUTE = 60;
      internal const Int64 MINUTES_PER_HOUR = 60;

      #endregion

      #region Static

      public static PgSQLInterval MinValue = new PgSQLInterval( Int64.MinValue );
      public static PgSQLInterval MaxValue = new PgSQLInterval( Int64.MaxValue );
      public static PgSQLInterval Zero = new PgSQLInterval( 0 );

      internal static void AppendTimeInformation( StringBuilder sb, Int64 ticks )
      {
         sb.Append( Math.Abs( CalcHours( ticks ) ).ToString( "D2" ) ) // Hours
            .Append( ':' )
            .Append( Math.Abs( CalcMinutes( ticks ) ).ToString( "D2" ) ) // Minutes
            .Append( ':' )
            // Calculate seconds part (total seconds minus whole minutes in seconds)
            .Append( Math.Abs( ticks / (Decimal) TimeSpan.TicksPerSecond - ( ticks / TimeSpan.TicksPerMinute ) * 60 ).ToString( "0#.######", System.Globalization.CultureInfo.InvariantCulture.NumberFormat ) ); // Seconds
      }



      internal static Int32 CalcMicroseconds( Int64 ticks )
      {
         return (Int32) ( ( ticks / TICKS_PER_MICROSECOND ) % MICROSECONDS_PER_SECOND );
      }

      internal static Int32 CalcMilliseconds( Int64 ticks )
      {
         return (Int32) ( ( ticks / TimeSpan.TicksPerMillisecond ) % MILLISECONDS_PER_SECOND );
      }

      internal static Int32 CalcSeconds( Int64 ticks )
      {
         return (Int32) ( ( ticks / TimeSpan.TicksPerSecond ) % SECONDS_PER_MINUTE );
      }

      internal static Int32 CalcMinutes( Int64 ticks )
      {
         return (Int32) ( ( ticks / TimeSpan.TicksPerMinute ) % MINUTES_PER_HOUR );
      }

      internal static Int32 CalcHours( Int64 ticks )
      {
         return (Int32) ( ticks / TimeSpan.TicksPerHour );
      }

      #endregion

      #region Fields
      private readonly Int32 _months;
      private readonly Int32 _days;
      private readonly Int64 _ticks;

      #endregion

      #region Constructors

      public PgSQLInterval( Int64 ticks )
         : this( 0, 0, ticks )
      {

      }

      public PgSQLInterval( TimeSpan span )
         : this( span.Ticks )
      {

      }

      public PgSQLInterval( Int32 months, Int32 days, Int64 ticks )
      {
         this._months = months;
         this._days = days;
         this._ticks = ticks;
      }

      public PgSQLInterval( Int32 days, Int32 hours, Int32 minutes, Int32 seconds )
         : this( 0, days, new TimeSpan( hours, minutes, seconds ).Ticks )
      {
      }

      public PgSQLInterval( Int32 days, Int32 hours, Int32 minutes, Int32 seconds, Int32 milliseconds )
         : this( 0, days, new TimeSpan( 0, hours, minutes, seconds, milliseconds ).Ticks )
      {
      }

      public PgSQLInterval( Int32 months, Int32 days, Int32 hours, Int32 minutes, Int32 seconds, Int32 milliseconds )
         : this( months, days, new TimeSpan( 0, hours, minutes, seconds, milliseconds ).Ticks )
      {
      }

      public PgSQLInterval( Int32 years, Int32 months, Int32 days, Int32 hours, Int32 minutes, Int32 seconds, Int32 milliseconds )
         : this( years * 12 + months, days, new TimeSpan( 0, hours, minutes, seconds, milliseconds ).Ticks )
      {
      }

      #endregion

      #region Whole parts

      public Int64 Ticks
      {
         get
         {
            return this._ticks;
         }
      }

      public Int32 Microseconds
      {
         get
         {
            return CalcMicroseconds( this._ticks );
         }
      }

      public Int32 Milliseconds
      {
         get
         {
            return CalcMilliseconds( this._ticks );
         }
      }

      public Int32 Seconds
      {
         get
         {
            return CalcSeconds( this._ticks );
         }
      }

      public Int32 Minutes
      {
         get
         {
            return CalcMinutes( this._ticks );
         }
      }

      public Int32 Hours
      {
         get
         {
            return CalcHours( this._ticks );
         }
      }

      public Int32 Days
      {
         get
         {
            return this._days;
         }
      }

      public Int32 Months
      {
         get
         {
            return this._months;
         }
      }
      #endregion

      #region Total parts

      public Int64 TotalTicks
      {
         get
         {
            return this._ticks + this._days * TimeSpan.TicksPerDay + this._months * TICKS_PER_MONTH;
         }
      }

      public Double TotalMicroseconds
      {
         get
         {
            return this.TotalTicks / ( (Double) TICKS_PER_MICROSECOND );
         }
      }

      public Double TotalMilliseconds
      {
         get
         {
            return this.TotalTicks / ( (Double) TimeSpan.TicksPerMillisecond );
         }
      }

      public Double TotalSeconds
      {
         get
         {
            return this.TotalTicks / ( (Double) TimeSpan.TicksPerSecond );
         }
      }

      public Double TotalMinutes
      {
         get
         {
            return this.TotalTicks / ( (Double) TimeSpan.TicksPerMinute );
         }
      }

      public Double TotalHours
      {
         get
         {
            return this.TotalTicks / ( (Double) TimeSpan.TicksPerHour );
         }
      }

      public Double TotalDays
      {
         get
         {
            return this.TotalTicks / ( (Double) TimeSpan.TicksPerDay );
         }
      }

      public Double TotalMonths
      {
         get
         {
            return this.TotalTicks / ( (Double) TICKS_PER_MONTH );
         }
      }

      #endregion

      #region Justification

      public PgSQLInterval JustifyDays()
      {
         return new PgSQLInterval( this._months, this._days + (Int32) ( this._ticks / TimeSpan.TicksPerDay ), this._ticks % TimeSpan.TicksPerDay );
      }

      public PgSQLInterval UnjustifyDays()
      {
         return new PgSQLInterval( this._months, 0, this._ticks + this._days * TimeSpan.TicksPerDay );
      }

      public PgSQLInterval JustifyMonths()
      {
         return new PgSQLInterval( this._months + this._days / DAYS_PER_MONTH, this._days % DAYS_PER_MONTH, this._ticks );
      }

      public PgSQLInterval UnjustifyMonths()
      {
         return new PgSQLInterval( 0, this._days + this._months * DAYS_PER_MONTH, this._ticks );
      }

      public PgSQLInterval JustifyInterval()
      {
         return this.JustifyMonths().JustifyDays();
      }

      public PgSQLInterval UnjustifyInterval()
      {
         return new PgSQLInterval( 0, 0, this._ticks + this._days * TimeSpan.TicksPerDay + this._months * TICKS_PER_MONTH );
      }

      public PgSQLInterval Canonicalize()
      {
         return new PgSQLInterval( 0, this._days + this._months * DAYS_PER_MONTH + (Int32) ( this._ticks / TimeSpan.TicksPerDay ), this._ticks % TimeSpan.TicksPerDay );
      }

      #endregion

      #region Arithmetic

      public PgSQLInterval Add( PgSQLInterval another )
      {
         return new PgSQLInterval( this._months + another._months, this._days + another._days, this._ticks + another._ticks );
      }

      public PgSQLInterval Subtract( PgSQLInterval another )
      {
         return new PgSQLInterval( this._months - another._months, this._days - another._days, this._ticks - another._ticks );
      }

      public PgSQLInterval Negate()
      {
         return new PgSQLInterval( -this._months, -this._days, -this._ticks );
      }

      public PgSQLInterval Duration()
      {
         return this.UnjustifyInterval().Ticks < 0 ? this.Negate() : this;
      }

      #endregion

      #region Comparison

      public Int32 CompareTo( PgSQLInterval other )
      {
         return this.UnjustifyInterval().Ticks.CompareTo( other.UnjustifyInterval().Ticks );
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLInterval )
         {
            return this.CompareTo( (PgSQLInterval) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Boolean Equals( PgSQLInterval other )
      {
         return this._ticks == other._ticks && this._days == other._days && this._months == other._months;
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLInterval && this.Equals( (PgSQLInterval) obj );
      }

      public override Int32 GetHashCode()
      {
         return this.UnjustifyInterval().Ticks.GetHashCode();
      }

      #endregion

      #region Casts

      public static implicit operator TimeSpan( PgSQLInterval x )
      {
         return new TimeSpan( x._ticks + x._days * TimeSpan.TicksPerDay + x._months * TICKS_PER_MONTH );
      }

      public static implicit operator PgSQLInterval( TimeSpan x )
      {
         return new PgSQLInterval( x ).Canonicalize();
      }

      #endregion

      #region Creation from parts

      public static PgSQLInterval FromTicks( Int64 ticks )
      {
         return new PgSQLInterval( ticks ).Canonicalize();
      }

      public static PgSQLInterval FromMicroseconds( Double microseconds )
      {
         return FromTicks( (Int64) ( microseconds * ( TimeSpan.TicksPerMillisecond / 1000 ) ) );
      }

      public static PgSQLInterval FromMilliseconds( Double milliseconds )
      {
         return FromTicks( (Int64) ( milliseconds * TimeSpan.TicksPerMillisecond ) );
      }

      public static PgSQLInterval FromSeconds( Double seconds )
      {
         return FromTicks( (Int64) ( seconds * TimeSpan.TicksPerSecond ) );
      }

      public static PgSQLInterval FromMinutes( Double minutes )
      {
         return FromTicks( (Int64) ( minutes * TimeSpan.TicksPerMinute ) );
      }

      public static PgSQLInterval FromHours( Double hours )
      {
         return FromTicks( (Int64) ( hours * TimeSpan.TicksPerHour ) );
      }

      public static PgSQLInterval FromDays( Double days )
      {
         return FromTicks( (Int64) ( days * TimeSpan.TicksPerDay ) );
      }

      public static PgSQLInterval FromMonths( Double months )
      {
         return FromTicks( (Int64) ( months * TICKS_PER_MONTH ) );
      }

      #endregion

      #region Operators

      public static PgSQLInterval operator +( PgSQLInterval x, PgSQLInterval y )
      {
         return x.Add( y );
      }

      public static PgSQLInterval operator -( PgSQLInterval x, PgSQLInterval y )
      {
         return x.Subtract( y );
      }

      public static Boolean operator ==( PgSQLInterval x, PgSQLInterval y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLInterval x, PgSQLInterval y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLInterval x, PgSQLInterval y )
      {
         return x.UnjustifyInterval().Ticks < y.UnjustifyInterval().Ticks;
      }

      public static Boolean operator <=( PgSQLInterval x, PgSQLInterval y )
      {
         return x.UnjustifyInterval().Ticks <= y.UnjustifyInterval().Ticks;
      }

      public static Boolean operator >( PgSQLInterval x, PgSQLInterval y )
      {
         return !( x <= y );
      }

      public static Boolean operator >=( PgSQLInterval x, PgSQLInterval y )
      {
         return !( x < y );
      }

      public static PgSQLInterval operator +( PgSQLInterval x )
      {
         return x;
      }

      public static PgSQLInterval operator -( PgSQLInterval x )
      {
         return x.Negate();
      }

      #endregion

      #region To and from string

      public override String ToString()
      {
         var sb = new StringBuilder();

         // Months
         if ( this._months != 0 )
         {
            sb.Append( this._months ).Append( Math.Abs( this._months ) == 1 ? " mon " : " mons " );
         }

         // Days
         if ( this._days != 0 )
         {
            if ( this._months < 0 && this._days > 0 )
            {
               sb.Append( '+' );
            }
            sb.Append( this._days ).Append( Math.Abs( this._days ) == 1 ? " day " : " days " );
         }

         // The rest
         if ( this._ticks != 0 || sb.Length == 0 )
         {
            // The sign
            if ( this._ticks < 0 )
            {
               sb.Append( '-' );
            }
            else if ( this._days < 0 || ( this._days == 0 && this._months < 0 ) )
            {
               sb.Append( '+' );
            }
            AppendTimeInformation( sb, this._ticks );
         }

         return sb.ToString( 0, sb[sb.Length - 1] == ' ' ? ( sb.Length - 1 ) : sb.Length );
      }

      public static PgSQLInterval Parse( String str )
      {
         PgSQLInterval result; Exception error;
         TryParse( str, out result, out error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLInterval result )
      {
         Exception error;
         TryParse( str, out result, out error );
         return error == null;
      }

      private static void TryParse( String str, out PgSQLInterval result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLInterval );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            // Easymode for plurals
            str = str.Replace( 's', ' ' );
            error = null;

            // Initialize variables
            var years = 0;
            var months = 0;
            var days = 0;
            var ticks = 0L;

            // Years
            var idx = str.IndexOf( "year" );
            var start = 0;
            if ( idx > 0 && !Int32.TryParse( str.Substring( start, idx - start ), out years ) )
            {
               error = new FormatException( "Years were in invalid format." );
            }
            UpdateStartIndex( str, idx, ref start, 5 );

            // Months
            if ( error == null )
            {
               idx = str.IndexOf( "mon", start );
               if ( idx > 0 && !Int32.TryParse( str.Substring( start, idx - start ), out months ) )
               {
                  error = new FormatException( "Months were in invalid format." );
               }
               UpdateStartIndex( str, idx, ref start, 4 );
            }

            // Days
            if ( error == null )
            {
               idx = str.IndexOf( "day", start );
               if ( idx > 0 && !Int32.TryParse( str.Substring( start, idx - start ), out days ) )
               {
                  error = new FormatException( "Days were in invalid format." );
               }
               UpdateStartIndex( str, idx, ref start, 4 );
            }

            // Time
            if ( error == null )
            {
               Int32 hours, minutes; Decimal seconds; Boolean isNegative;
               ParseTime( str, start, out hours, out minutes, out seconds, out isNegative, ref error, false );

               if ( error == null )
               {
                  try
                  {
                     ticks = hours * TimeSpan.TicksPerHour + minutes * TimeSpan.TicksPerMinute + (Int64) ( seconds * TimeSpan.TicksPerSecond );
                  }
                  catch ( Exception exc )
                  {
                     // E.g. overflow exception
                     error = new FormatException( "Error when calculating ticks, ", exc );
                  }
               }
            }

            result = error == null ?
               new PgSQLInterval( years * MONTHS_PER_YEAR + months, days, ticks ) :
               default( PgSQLInterval );

         }
      }

      private static void UpdateStartIndex( String str, Int32 idx, ref Int32 startIdx, Int32 addition )
      {
         if ( idx >= 0 )
         {
            startIdx = ( idx + addition ) >= str.Length ? str.Length : ( idx + addition );
         }
      }

      internal static void ParseTime( String str, Int32 start, out Int32 hours, out Int32 minutes, out Decimal seconds, out Boolean isNegative, ref Exception error, Boolean timeIsMandatory )
      {
         hours = 0;
         minutes = 0;
         seconds = 0m;

         var seenOtherThanWhitespace = false;
         isNegative = false;
         var curState = 0; // 0 - hours, 1 - minutes, 2 - seconds
         var idx = start;
         var len = str.Length;
         while ( idx < len && error == null )
         {
            var c = str[idx];
            if ( !Char.IsWhiteSpace( c ) )
            {
               if ( c == '-' )
               {
                  if ( seenOtherThanWhitespace || isNegative )
                  {
                     error = new FormatException( "Unexpected minus sign." );
                  }
                  else
                  {
                     isNegative = true;
                  }
               }
               else if ( c == ':' || idx == len - 1 )
               {
                  var timeStr = str.Substring( start, idx - start + ( c == ':' ? 0 : 1 ) );
                  switch ( curState )
                  {
                     case 0: // Hours
                        if ( !Int32.TryParse( timeStr, out hours ) )
                        {
                           error = new FormatException( "Malformed hours." );
                        }
                        break;
                     case 1: // Minutes
                        if ( !Int32.TryParse( timeStr, out minutes ) )
                        {
                           error = new FormatException( "Malformed minutes." );
                        }
                        break;
                     case 2: // Seconds
                        if ( !Decimal.TryParse( timeStr, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out seconds ) )
                        {
                           error = new FormatException( "Malformed seconds." );
                        }
                        break;
                  }

                  ++curState;
                  start = idx + 1;
               }
               seenOtherThanWhitespace = true;

            }
            ++idx;
         }

         if ( curState == 0 && timeIsMandatory )
         {
            error = new FormatException( "Missing time information." );
         }

         if ( isNegative )
         {
            minutes = -minutes;
            seconds = -seconds;
         }
      }

      #endregion

   }

   public interface IPgSQLDate
   {
      #region Properties

      Int32 DayOfYear { get; }

      Int32 Year { get; }

      Int32 Month { get; }

      Int32 Day { get; }

      DayOfWeek DayOfWeek { get; }

      Int32 DaysSinceEra { get; }

      Boolean IsLeapYear { get; }

      #endregion
   }

   public struct PgSQLDate : IEquatable<PgSQLDate>, IComparable, IComparable<PgSQLDate>, IPgSQLDate
   {
      #region Consts
      public const Int32 MAX_YEAR = 5874897; // As per PostgreSQL documentation
      public const Int32 MIN_YEAR = -4714; // As per PostgreSQL documentation

      private const Int32 DAYS_IN_YEAR = 365; //Common years
      private const Int32 DAYS_IN_4YEARS = 4 * DAYS_IN_YEAR + 1; //Leap year every 4 years.
      private const Int32 DAYS_IN_CENTURY = 25 * DAYS_IN_4YEARS - 1; //Except no leap year every 100.
      private const Int32 DAYS_IN_4CENTURIES = 4 * DAYS_IN_CENTURY + 1; //Except leap year every 400.

      internal const String INFINITY = "infinity";
      internal const String MINUS_INFINITY = "-" + INFINITY;



      #endregion

      #region Static

      // Cumulative days in non-leap years
      private static readonly Int32[] CommonYearDays = new Int32[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };

      // Cumulative days in leap years
      private static readonly Int32[] LeapYearDays = new Int32[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };

      // Amount of days in non-leap year months
      private static readonly Int32[] CommonYearMaxes = new Int32[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

      // Amount of days in leap year months
      private static readonly Int32[] LeapYearMaxes = new Int32[] { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

      private static Boolean IsLeap( Int32 year )
      {
         //Every 4 years is a leap year
         //Except every 100 years isn't a leap year.
         //Except every 400 years is.
         // Also: http://support.microsoft.com/kb/214019 (doesn't cover 0 and negative years)
         if ( year < 1 )
         {
            ++year;
         }
         return ( year % 4 == 0 ) && ( ( year % 100 != 0 ) || ( year % 400 == 0 ) );
      }

      private static Int32 DaysForYears( Int32 years )
      {
         //Number of years after 1CE (0 for 1CE, -1 for 1BCE, 1 for 2CE).
         if ( years >= 1 )
         {
            --years;
         }

         return years / 400 * DAYS_IN_4CENTURIES //Blocks of 400 years with their leap and common years
                + years % 400 / 100 * DAYS_IN_CENTURY //Remaining blocks of 100 years with their leap and common years
                + years % 100 / 4 * DAYS_IN_4YEARS //Remaining blocks of 4 years with their leap and common years
                + years % 4 * DAYS_IN_YEAR //Remaining years, all common
                + ( years < 0 ? -1 : 0 ); //And 1BCE is leap.
      }

      private static Int32 ComponentsToDays( Int32 year, Int32 month, Int32 day )
      {
         if ( year == 0 || year < MIN_YEAR || year > MAX_YEAR )
         {
            throw new ArgumentOutOfRangeException( "Year" );
         }
         else if ( month < 1 || month > 12 )
         {
            throw new ArgumentOutOfRangeException( "Month" );
         }
         else
         {
            var isLeap = IsLeap( year );
            if ( day < 1 || day > ( isLeap ? 366 : 365 ) )
            {
               throw new ArgumentOutOfRangeException( "Day" );
            }
            else
            {
               return DaysForYears( year ) + ( isLeap ? LeapYearDays : CommonYearDays )[month - 1] + day - 1;
            }
         }
      }

      public static readonly PgSQLDate Epoch = new PgSQLDate( 1970, 1, 1 );
      public static readonly PgSQLDate MaxValue = new PgSQLDate( MAX_YEAR, 12, 31 );
      public static readonly PgSQLDate MinValue = new PgSQLDate( MIN_YEAR, 11, 24 );
      public static readonly PgSQLDate Era = new PgSQLDate( 0 );
      public static readonly PgSQLDate Infinity = new PgSQLDate( DateTime.MaxValue );
      public static readonly PgSQLDate MinusInfinity = new PgSQLDate( DateTime.MinValue );

      public static PgSQLDate Now
      {
         get
         {
            return new PgSQLDate( DateTime.Now );
         }
      }

      public static PgSQLDate Today
      {
         get
         {
            return Now;
         }
      }

      public static PgSQLDate Yesterday
      {
         get
         {
            return Now.AddDays( -1 );
         }
      }

      public static PgSQLDate Tomorrow
      {
         get
         {
            return Now.AddDays( 1 );
         }
      }

      #endregion

      #region Fields

      private readonly Int32 _days;

      #endregion

      #region Constructors

      public PgSQLDate( Int32 daysSinceEra )
      {
         this._days = daysSinceEra;
      }

      public PgSQLDate( PgSQLDate other )
         : this( other._days )
      {
      }

      public PgSQLDate( DateTime datetime )
         : this( (Int32) ( datetime.Ticks / TimeSpan.TicksPerDay ) )
      {
      }

      public PgSQLDate( Int32 year, Int32 month, Int32 day )
         : this( ComponentsToDays( year, month, day ) )
      {
      }

      #endregion

      #region Properties

      public Int32 DayOfYear
      {
         get
         {
            return this._days - DaysForYears( this.Year ) + 1;
         }
      }

      public Int32 Year
      {
         get
         {
            var start = ( (Int32) Math.Round( this._days / 365.2425 ) ) - 1;
            while ( DaysForYears( ++start ) <= this._days ) ;
            return start - 1;
         }
      }

      public Int32 Month
      {
         get
         {
            var max = this.DayOfYear;
            var array = this.IsLeapYear ? LeapYearDays : CommonYearDays;
            var i = 1;
            while ( max > array[i] )
            {
               ++i;
            }
            return i;
         }
      }

      public Int32 Day
      {
         get
         {
            return this.DayOfYear - ( this.IsLeapYear ? LeapYearDays : CommonYearDays )[this.Month - 1];
         }
      }

      public DayOfWeek DayOfWeek
      {
         get
         {
            return (DayOfWeek) ( ( this._days + 1 ) % 7 );
         }
      }

      public Int32 DaysSinceEra
      {
         get
         {
            return this._days;
         }
      }

      public Boolean IsLeapYear
      {
         get
         {
            return IsLeap( this.Year );
         }
      }

      #endregion

      #region Arithmetics

      public PgSQLDate AddDays( Int32 days )
      {
         return new PgSQLDate( this._days + days );
      }

      public PgSQLDate AddMonths( Int32 months )
      {
         var newYear = this.Year;
         var newMonth = this.Month + months;

         while ( newMonth > 12 )
         {
            newMonth -= 12;
            ++newYear;
            if ( newYear == 0 )
            {
               ++newYear; // No 'zero'eth year.
            }
         };
         while ( newMonth < 1 )
         {
            newMonth += 12;
            --newYear;
            if ( newYear == 0 )
            {
               --newYear; // No 'zero'eth year.
            }
         };
         var maxDay = ( IsLeap( newYear ) ? LeapYearMaxes : CommonYearMaxes )[newMonth - 1];
         var newDay = this.Day > maxDay ? maxDay : this.Day;
         return new PgSQLDate( newYear, newMonth, newDay );
      }

      public PgSQLDate AddYears( Int32 years )
      {
         var newYear = this.Year + years;
         if ( newYear >= 0 && this._days < 0 ) // No 'zero'eth year.
         {
            ++newYear;
         }
         else if ( newYear <= 0 && this._days >= 0 ) // No 'zero'eth year.
         {
            --newYear;
         }
         return new PgSQLDate( newYear, Month, Day );
      }

      public PgSQLDate Add( PgSQLInterval interval, Int32 carriedOverflow = 0 )
      {
         return this.AddMonths( interval.Months ).AddDays( interval.Days + carriedOverflow );
      }

      #endregion

      #region Comparison

      public Boolean Equals( PgSQLDate other )
      {
         return this._days == other._days;
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLDate && this.Equals( (PgSQLDate) obj );
      }

      public override Int32 GetHashCode()
      {
         return this._days.GetHashCode();
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLDate )
         {
            return this.CompareTo( (PgSQLDate) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Int32 CompareTo( PgSQLDate other )
      {
         return this._days.CompareTo( other._days );
      }

      #endregion

      #region Casts

      public static explicit operator DateTime( PgSQLDate x )
      {
         try
         {
            return new DateTime( x._days * TimeSpan.TicksPerDay );
         }
         catch
         {
            throw new InvalidCastException( "The given PostgreSQL date " + x + " can not be represented by " + typeof( DateTime ) + "." );
         }
      }

      public static explicit operator PgSQLDate( DateTime x )
      {
         return new PgSQLDate( (Int32) ( x.Ticks / TimeSpan.TicksPerDay ) );
      }

      #endregion

      #region Operators

      public static Boolean operator ==( PgSQLDate x, PgSQLDate y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLDate x, PgSQLDate y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLDate x, PgSQLDate y )
      {
         return x._days < y._days;
      }

      public static Boolean operator >( PgSQLDate x, PgSQLDate y )
      {
         return !( x._days <= y._days );
      }

      public static Boolean operator <=( PgSQLDate x, PgSQLDate y )
      {
         return x._days <= y._days;
      }

      public static Boolean operator >=( PgSQLDate x, PgSQLDate y )
      {
         return !( x._days > y._days );
      }

      public static PgSQLDate operator +( PgSQLDate date, PgSQLInterval interval )
      {
         return date.Add( interval );
      }

      public static PgSQLDate operator +( PgSQLInterval interval, PgSQLDate date )
      {
         return date.Add( interval );
      }

      public static PgSQLDate operator -( PgSQLDate date, PgSQLInterval interval )
      {
         return date.Add( -interval );
      }

      public static PgSQLInterval operator -( PgSQLDate dateX, PgSQLDate dateY )
      {
         return new PgSQLInterval( 0, dateX._days - dateY._days, 0 );
      }

      #endregion

      #region To and from string

      internal const Int32 INFINITY_CHAR_COUNT = 8;
      internal const Int32 MINUS_INFINITY_CHAR_COUNT = INFINITY_CHAR_COUNT + 1;
      private const Int32 MIN_NORMAL_CHAR_COUNT = YEAR_CHAR_COUNT + 1 + MONTH_CHAR_COUNT + 1 + DAY_CHAR_COUNT;
      private const Byte SEPARATOR = (Byte) '-';
      private const Int32 YEAR_CHAR_COUNT = 4;
      private const Int32 MONTH_CHAR_COUNT = 2;
      private const Int32 DAY_CHAR_COUNT = 2;
      private const Int32 BC_CHAR_COUNT = 3; // " BC"
      private const Byte BC_CHAR_1 = (Byte) ' ';
      private const Byte BC_CHAR_2 = (Byte) 'B';
      private const Byte BC_CHAR_3 = (Byte) 'C';

      public Int32 GetTextByteCount( IEncodingInfo encoding )
      {
         Int32 retVal;
         if ( this == Infinity )
         {
            retVal = INFINITY_CHAR_COUNT * encoding.BytesPerASCIICharacter;
         }
         else if ( this == MinusInfinity )
         {
            retVal = INFINITY_CHAR_COUNT * encoding.BytesPerASCIICharacter;
         }
         else
         {
            // yyyy-MM-dd
            retVal = MIN_NORMAL_CHAR_COUNT * encoding.BytesPerASCIICharacter;
            if ( this._days < 0 )
            {
               // " BC"
               retVal += BC_CHAR_COUNT * encoding.BytesPerASCIICharacter;
            }
         }
         return retVal;
      }

      public void WriteTextBytes( IEncodingInfo encoding, Byte[] array, ref Int32 offset )
      {
         if ( this == Infinity )
         {
            encoding.WriteString( array, ref offset, INFINITY );
         }
         else if ( this == MinusInfinity )
         {
            encoding.WriteString( array, ref offset, MINUS_INFINITY );
         }
         else
         {
            // Let's not allocate heap objects
            encoding
               .WriteIntegerTextual( array, ref offset, Math.Abs( this.Year ), YEAR_CHAR_COUNT )
               .WriteASCIIByte( array, ref offset, SEPARATOR ) // '-'
               .WriteIntegerTextual( array, ref offset, this.Month, MONTH_CHAR_COUNT )
               .WriteASCIIByte( array, ref offset, SEPARATOR ) // '-'
               .WriteIntegerTextual( array, ref offset, this.Day, DAY_CHAR_COUNT );
            if ( this._days < 0 )
            {
               // " BC"
               encoding
                  .WriteASCIIByte( array, ref offset, BC_CHAR_1 ) // ' '
                  .WriteASCIIByte( array, ref offset, BC_CHAR_2 ) // 'B'
                  .WriteASCIIByte( array, ref offset, BC_CHAR_3 ); // 'C'
            }
         }
      }

      public static PgSQLDate ParseBinaryText( IEncodingInfo encoding, Byte[] array, ref Int32 offset, Int32 count )
      {
         var increment = encoding.BytesPerASCIICharacter;
         switch ( increment * count )
         {
            case INFINITY_CHAR_COUNT:
               return Infinity;
            case MINUS_INFINITY_CHAR_COUNT:
               return MinusInfinity;
            default:
               var max = offset + count;
               var year = encoding.ParseInt32Textual( array, ref offset, (YEAR_CHAR_COUNT, true) );
               var month = encoding.EqualsOrThrow( array, ref offset, SEPARATOR ).ParseInt32Textual( array, ref offset, (MONTH_CHAR_COUNT, true) );
               var day = encoding.EqualsOrThrow( array, ref offset, SEPARATOR ).ParseInt32Textual( array, ref offset, (DAY_CHAR_COUNT, true) );
               if ( offset + 3 * encoding.BytesPerASCIICharacter < max )
               {
                  // " BC" may follow
                  max = offset;
                  if ( encoding.ReadASCIIByte( array, ref offset ) == BC_CHAR_1
                     && encoding.ReadASCIIByte( array, ref offset ) == BC_CHAR_2
                     && encoding.ReadASCIIByte( array, ref offset ) == BC_CHAR_3
                     )
                  {
                     year = -year;
                  }
                  else
                  {
                     // 'Reverse back'
                     offset = max;
                  }
               }
               return new PgSQLDate( year, month, day );
         }
      }

      public override String ToString()
      {
         // As per PostgreSQL documentation ISO 8601 format (same as in Npgsql)
         // Format of yyyy-MM-dd with " BC" for BCE and optional " AD" for CE which we omit here.
         return
             new StringBuilder( Math.Abs( this.Year ).ToString( "D4" ) ).Append( '-' ).Append( this.Month.ToString( "D2" ) ).Append( '-' ).Append(
                 this.Day.ToString( "D2" ) ).Append( this._days < 0 ? " BC" : "" ).ToString();
      }

      public static PgSQLDate Parse( String str )
      {
         PgSQLDate result; Exception error;
         TryParse( str, out result, out error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLDate result )
      {
         Exception error;
         return TryParse( str, out result, out error );
      }

      internal static Boolean TryParse( String str, out PgSQLDate result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLDate );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            str = str.Trim();
            error = null;
            if ( String.Equals( str, INFINITY, StringComparison.OrdinalIgnoreCase ) )
            {
               result = Infinity;
            }
            else if ( String.Equals( str, MINUS_INFINITY, StringComparison.OrdinalIgnoreCase ) )
            {
               result = MinusInfinity;
            }
            else
            {
               // ISO 8601 format assumed
               var start = 0;
               var idx = 0;
               var year = IntegerOrError( str, ref start, ref idx, ref error, "year", "month", '-', true );
               var month = IntegerOrError( str, ref start, ref idx, ref error, "month", "day", '-', true );
               var day = IntegerOrError( str, ref start, ref idx, ref error, "day", null, ' ', false );
               if ( error == null && start < str.Length && str.IndexOf( "BC", start, StringComparison.OrdinalIgnoreCase ) != -1 )
               {
                  year = -year;
               }

               result = error == null ?
                  new PgSQLDate( year, month, day ) :
                  default( PgSQLDate );
            }
         }
         return error == null;
      }

      private static Int32 IntegerOrError( String str, ref Int32 start, ref Int32 idx, ref Exception error, String thisDatePart, String nextDatePart, Char separator, Boolean separatorIsMandatory )
      {
         var result = 0;
         if ( error == null )
         {
            if ( start >= str.Length )
            {
               error = new FormatException( "Missing " + thisDatePart );
            }
            else
            {
               idx = str.IndexOf( separator, start );
               if ( idx == -1 && !separatorIsMandatory )
               {
                  idx = str.Length;
               }

               if ( idx == -1 )
               {
                  error = new FormatException( "Could not find " + thisDatePart + "-" + nextDatePart + " separator." );
               }
               else if ( !Int32.TryParse( str.Substring( start, idx - start ), out result ) )
               {
                  error = new FormatException( thisDatePart + " was malformed." );
               }
               else
               {
                  start = idx + 1;
               }
            }
         }
         return result;
      }

      #endregion

   }

   public interface IPgSQLTime
   {
      #region Properties

      Int64 Ticks { get; }

      Int32 Microseconds { get; }

      Int32 Milliseconds { get; }

      Int32 Seconds { get; }

      Int32 Minutes { get; }

      Int32 Hours { get; }

      #endregion
   }

   public struct PgSQLTime : IEquatable<PgSQLTime>, IComparable, IComparable<PgSQLTime>, IPgSQLTime
   {
      #region Static

      public static readonly PgSQLTime AllBalls = new PgSQLTime( 0 );

      public static PgSQLTime Now
      {
         get
         {
            return new PgSQLTime( DateTime.Now.TimeOfDay );
         }
      }

      #endregion

      #region Fields
      private readonly Int64 _ticks;
      #endregion

      #region Constructors

      public PgSQLTime( Int64 ticks )
      {
         if ( ticks == TimeSpan.TicksPerDay )
         {
            this._ticks = ticks;
         }
         else
         {
            ticks %= TimeSpan.TicksPerDay;
            this._ticks = ticks < 0 ? ticks + TimeSpan.TicksPerDay : ticks;
         }
      }

      public PgSQLTime( TimeSpan timeSpan )
         : this( timeSpan.Ticks )
      {
      }

      public PgSQLTime( DateTime dateTime )
         : this( dateTime.Ticks )
      {
      }

      public PgSQLTime( PgSQLInterval interval )
         : this( interval.Ticks )
      {
      }

      public PgSQLTime( PgSQLTime other )
         : this( other._ticks )
      {
      }

      public PgSQLTime( Int32 hours, Int32 minutes, Int32 seconds )
         : this( hours, minutes, seconds, 0 )
      {
      }

      public PgSQLTime( Int32 hours, Int32 minutes, Int32 seconds, Int32 microseconds )
         : this(
             hours * TimeSpan.TicksPerHour + minutes * TimeSpan.TicksPerMinute + seconds * TimeSpan.TicksPerSecond +
             microseconds * PgSQLInterval.TICKS_PER_MICROSECOND )
      {
      }

      public PgSQLTime( Int32 hours, Int32 minutes, Decimal seconds )
         : this( hours * TimeSpan.TicksPerHour + minutes * TimeSpan.TicksPerMinute + (Int64) ( seconds * TimeSpan.TicksPerSecond ) )
      {
      }

      #endregion

      #region Properties

      public Int64 Ticks
      {
         get
         {
            return this._ticks;
         }
      }

      public Int32 Microseconds
      {
         get
         {
            return PgSQLInterval.CalcMicroseconds( this._ticks );
         }
      }

      public Int32 Milliseconds
      {
         get
         {
            return PgSQLInterval.CalcMilliseconds( this._ticks );
         }
      }

      public Int32 Seconds
      {
         get
         {
            return PgSQLInterval.CalcSeconds( this._ticks );
         }
      }

      public Int32 Minutes
      {
         get
         {
            return PgSQLInterval.CalcMinutes( this._ticks );
         }
      }

      public Int32 Hours
      {
         get
         {
            return PgSQLInterval.CalcHours( this._ticks );
         }
      }

      #endregion

      #region Comparison

      public Boolean Equals( PgSQLTime other )
      {
         return this._ticks == other._ticks;
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLTime && this.Equals( (PgSQLTime) obj );
      }

      public override Int32 GetHashCode()
      {
         return this._ticks.GetHashCode();
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLTime )
         {
            return this.CompareTo( (PgSQLTime) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Int32 CompareTo( PgSQLTime other )
      {
         return this.Normalize()._ticks.CompareTo( other.Normalize()._ticks );
      }

      #endregion

      #region Normalization

      public PgSQLTime Normalize()
      {
         return new PgSQLTime( this._ticks % TimeSpan.TicksPerDay );
      }

      #endregion

      #region Arithmetics

      public PgSQLTime AddTicks( Int64 ticksAdded )
      {
         return new PgSQLTime( ( Ticks + ticksAdded ) % TimeSpan.TicksPerDay );
      }

      private PgSQLTime AddTicks( Int64 ticksAdded, out Int32 overflow )
      {
         var result = Ticks + ticksAdded;
         overflow = (Int32) ( result / TimeSpan.TicksPerDay );
         result %= TimeSpan.TicksPerDay;
         if ( result < 0 )
         {
            --overflow; //"carry the one"
         }
         return new PgSQLTime( result );
      }

      public PgSQLTime Add( PgSQLInterval interval )
      {
         return AddTicks( interval.Ticks );
      }

      internal PgSQLTime Add( PgSQLInterval interval, out Int32 overflow )
      {
         return AddTicks( interval.Ticks, out overflow );
      }

      public PgSQLTime Subtract( PgSQLInterval interval )
      {
         return AddTicks( -interval.Ticks );
      }

      public PgSQLInterval Subtract( PgSQLTime earlier )
      {
         return new PgSQLInterval( Ticks - earlier.Ticks );
      }

      #endregion

      #region Timezones

      public PgSQLTimeTZ AtTimeZone( PgSQLTimeZone timeZone )
      {
         return new PgSQLTimeTZ( this ).AtTimeZone( timeZone );
      }

      #endregion

      #region Casts

      public static explicit operator DateTime( PgSQLTime x )
      {
         return new DateTime( x._ticks, DateTimeKind.Unspecified );
      }

      public static explicit operator PgSQLTime( DateTime x )
      {
         return new PgSQLTime( x.Ticks );
      }

      public static explicit operator TimeSpan( PgSQLTime x )
      {
         return new TimeSpan( x._ticks );
      }

      public static explicit operator PgSQLTime( TimeSpan x )
      {
         return new PgSQLTime( x.Ticks );
      }

      public static explicit operator PgSQLInterval( PgSQLTime x )
      {
         return new PgSQLInterval( x._ticks );
      }

      public static explicit operator PgSQLTime( PgSQLInterval x )
      {
         return new PgSQLTime( x );
      }

      #endregion

      #region Operators

      public static Boolean operator ==( PgSQLTime x, PgSQLTime y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLTime x, PgSQLTime y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLTime x, PgSQLTime y )
      {
         return x.Ticks < y.Ticks;
      }

      public static Boolean operator <=( PgSQLTime x, PgSQLTime y )
      {
         return x.Ticks <= y.Ticks;
      }

      public static Boolean operator >( PgSQLTime x, PgSQLTime y )
      {
         return !( x.Ticks <= y.Ticks );
      }

      public static Boolean operator >=( PgSQLTime x, PgSQLTime y )
      {
         return !( x.Ticks < y.Ticks );
      }

      public static PgSQLTime operator +( PgSQLTime time, PgSQLInterval interval )
      {
         return time.Add( interval );
      }

      public static PgSQLTime operator +( PgSQLInterval interval, PgSQLTime time )
      {
         return time + interval;
      }

      public static PgSQLTime operator -( PgSQLTime time, PgSQLInterval interval )
      {
         return time.Subtract( interval );
      }

      public static PgSQLInterval operator -( PgSQLTime later, PgSQLTime earlier )
      {
         return later.Subtract( earlier );
      }

      #endregion

      #region To and from string

      private const Byte SEPARATOR = (Byte) ':';
      private const Byte MICRO_SEPARATOR = (Byte) '.';
      private const Int32 HOUR_CHAR_COUNT = 2;
      private const Int32 MINUTE_CHAR_COUNT = 2;
      private const Int32 SECOND_CHAR_COUNT = 2;
      private const Int32 MICRO_PRECISION = 6;

      internal static Int32 GetTextByteCount( IEncodingInfo encoding, Int64 ticks )
      {
         var retVal = 8 * encoding.BytesPerASCIICharacter; // HH:mm:ss
         var microSeconds = Math.Abs( PgSQLInterval.CalcMicroseconds( ticks ) );
         if ( microSeconds > 0 )
         {
            // Need to append '.' and micro second count
            retVal += encoding.BytesPerASCIICharacter + encoding.GetTextualFractionIntegerSize( microSeconds, MICRO_PRECISION );
         }

         return retVal;
      }

      internal static void WriteTextBytes( IEncodingInfo encoding, Int64 ticks, Byte[] array, ref Int32 offset )
      {
         encoding
            .WriteIntegerTextual( array, ref offset, Math.Abs( (Int32) ( ticks / TimeSpan.TicksPerHour ) ), HOUR_CHAR_COUNT )
            .WriteASCIIByte( array, ref offset, SEPARATOR ) // ':'
            .WriteIntegerTextual( array, ref offset, Math.Abs( (Int32) ( ( ticks / TimeSpan.TicksPerMinute ) % PgSQLInterval.MINUTES_PER_HOUR ) ), MINUTE_CHAR_COUNT )
            .WriteASCIIByte( array, ref offset, SEPARATOR ) // ':'
            .WriteIntegerTextual( array, ref offset, Math.Abs( (Int32) ( ( ticks / TimeSpan.TicksPerSecond ) % PgSQLInterval.SECONDS_PER_MINUTE ) ), SECOND_CHAR_COUNT );

         var microSeconds = Math.Abs( PgSQLInterval.CalcMicroseconds( ticks ) );
         if ( microSeconds != 0 )
         {
            encoding
               .WriteASCIIByte( array, ref offset, MICRO_SEPARATOR ) // '.'
               .WriteFractionIntegerTextual( array, ref offset, microSeconds, MICRO_PRECISION );
         }
      }

      public Int32 GetTextByteCount( IEncodingInfo encoding )
      {
         return GetTextByteCount( encoding, this._ticks );
      }

      public void WriteTextBytes( IEncodingInfo encoding, Byte[] array, ref Int32 offset )
      {
         WriteTextBytes( encoding, this._ticks, array, ref offset );
      }

      public static PgSQLTime ParseBinaryText( IEncodingInfo encoding, Byte[] array, ref Int32 offset, Int32 count )
      {
         var max = offset + count;
         var hours = encoding.ParseInt32Textual( array, ref offset, (HOUR_CHAR_COUNT, true) );
         var minutes = encoding.EqualsOrThrow( array, ref offset, SEPARATOR ).ParseInt32Textual( array, ref offset, (MINUTE_CHAR_COUNT, true) );
         var seconds = encoding.EqualsOrThrow( array, ref offset, SEPARATOR ).ParseInt32Textual( array, ref offset, (SECOND_CHAR_COUNT, true) );
         var oldIdx = offset;
         if ( encoding.TryParseOptionalNumber( array, ref offset, MICRO_SEPARATOR, (MICRO_PRECISION, false), max, out Int32 micros ) )
         {
            // When calculating trailing zeroes, we must take the prefix (MICRO_SEPARATOR) into account 
            var trailingZeroesCount = MICRO_PRECISION - ( offset - oldIdx - encoding.BytesPerASCIICharacter );
            while ( trailingZeroesCount > 0 )
            {
               micros *= 10;
               --trailingZeroesCount;
            }
         }

         return new PgSQLTime( hours, minutes, seconds, micros );
      }

      public override String ToString()
      {
         var sb = new StringBuilder();
         PgSQLInterval.AppendTimeInformation( sb, this._ticks );
         return sb.ToString();
      }

      public static PgSQLTime Parse( String str )
      {
         PgSQLTime result; Exception error;
         TryParse( str, out result, out error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLTime result )
      {
         Exception error;
         return TryParse( str, out result, out error );
      }

      internal static Boolean TryParse( String str, out PgSQLTime result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLTime );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            error = null;
            Int32 hours, minutes; Decimal seconds; Boolean isNegative;
            PgSQLInterval.ParseTime( str, 0, out hours, out minutes, out seconds, out isNegative, ref error, true );
            if ( hours < 0 || hours > 24 || minutes < 0 || minutes > 59 || seconds < 0m || seconds >= 60m || ( hours == 24 && ( minutes != 0 || seconds != 0m ) ) )
            {
               error = new FormatException( "One of the hours, minutes, or seconds (" + hours + ":" + minutes + ":" + seconds + ") was out of range." );
            }

            result = error == null ?
               new PgSQLTime( hours, minutes, seconds ) :
               default( PgSQLTime );
         }
         return error == null;
      }

      #endregion

   }

   public struct PgSQLTimeZone : IEquatable<PgSQLTimeZone>, IComparable, IComparable<PgSQLTimeZone>
   {
      #region Consts

      private const Int32 MINUTES_PER_HOUR = (Int32) PgSQLInterval.MINUTES_PER_HOUR;
      private const Int32 SECONDS_PER_MINUTE = (Int32) PgSQLInterval.SECONDS_PER_MINUTE;

      #endregion

      #region Static

      public static readonly PgSQLTimeZone UTC = new PgSQLTimeZone( 0 );

      public static PgSQLTimeZone CurrentTimeZone
      {
         get
         {
            return new PgSQLTimeZone( TimeZoneInfo.Local.GetUtcOffset( DateTime.Now ) );
         }
      }

      public static PgSQLTimeZone GetSolarTimeZone( Decimal longitude )
      {
         return new PgSQLTimeZone( (Int64) ( longitude / 15m * TimeSpan.TicksPerHour ) );
      }

      public static PgSQLTimeZone GetLocalTimeZone( PgSQLDate date )
      {
         return new PgSQLTimeZone( TimeZoneInfo.Local.GetUtcOffset(
            date.Year >= 1902 && date.Year < 2038 ?
               (DateTime) date :
               new DateTime( 2000, date.Month, date.Day )
            ) );
      }

      #endregion

      #region Fields

      private readonly Int32 _totalSeconds;

      #endregion

      #region Constructors

      public PgSQLTimeZone( Int64 ticks )
         : this( (Int32) ( ticks / TimeSpan.TicksPerSecond ) )
      {
      }

      public PgSQLTimeZone( Int32 hours, Int32 minutes )
         : this( hours, minutes, 0 )
      {
      }

      public PgSQLTimeZone( Int32 hours, Int32 minutes, Int32 seconds )
         : this( hours * MINUTES_PER_HOUR * SECONDS_PER_MINUTE + minutes * SECONDS_PER_MINUTE + seconds )
      {
      }

      public PgSQLTimeZone( PgSQLTimeZone other )
         : this( other._totalSeconds )
      {
      }

      public PgSQLTimeZone( PgSQLInterval interval )
         : this( interval.Ticks )
      {
      }

      public PgSQLTimeZone( TimeSpan timeSpan )
         : this( timeSpan.Ticks )
      {
      }

      private PgSQLTimeZone( Int32 seconds )
      {
         this._totalSeconds = seconds;
      }

      #endregion

      #region Properties

      public Int32 Hours
      {
         get
         {
            return this._totalSeconds / MINUTES_PER_HOUR / SECONDS_PER_MINUTE;
         }
      }

      public Int32 Minutes
      {
         get
         {
            return ( this._totalSeconds / MINUTES_PER_HOUR ) % SECONDS_PER_MINUTE;
         }
      }

      public Int32 Seconds
      {
         get
         {
            return this._totalSeconds % SECONDS_PER_MINUTE;
         }
      }

      public Int32 TotalSeconds
      {
         get
         {
            return this._totalSeconds;
         }
      }

      #endregion

      #region Comparison

      public Boolean Equals( PgSQLTimeZone other )
      {
         return this._totalSeconds == other._totalSeconds;
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLTimeZone && this.Equals( (PgSQLTimeZone) obj );
      }

      public override Int32 GetHashCode()
      {
         return this._totalSeconds.GetHashCode();
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLTimeZone )
         {
            return this.CompareTo( (PgSQLTimeZone) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Int32 CompareTo( PgSQLTimeZone other )
      {
         // Note: +01:00 is less than -01:00, so we have to invert the result (accomplished by comparing other to this instead of this to other)
         return other._totalSeconds.CompareTo( this._totalSeconds );
      }

      #endregion

      #region Casts

      public static implicit operator PgSQLTimeZone( PgSQLInterval interval )
      {
         return new PgSQLTimeZone( interval );
      }

      public static implicit operator PgSQLInterval( PgSQLTimeZone timeZone )
      {
         return new PgSQLInterval( timeZone._totalSeconds * TimeSpan.TicksPerSecond );
      }

      public static implicit operator PgSQLTimeZone( TimeSpan interval )
      {
         return new PgSQLTimeZone( (PgSQLInterval) interval );
      }

      public static implicit operator TimeSpan( PgSQLTimeZone timeZone )
      {
         return new TimeSpan( timeZone._totalSeconds * TimeSpan.TicksPerSecond );
      }

      #endregion

      #region Operators

      public static Boolean operator ==( PgSQLTimeZone x, PgSQLTimeZone y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLTimeZone x, PgSQLTimeZone y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLTimeZone x, PgSQLTimeZone y )
      {
         return x._totalSeconds < y._totalSeconds;
      }

      public static Boolean operator <=( PgSQLTimeZone x, PgSQLTimeZone y )
      {
         return x._totalSeconds <= y._totalSeconds;
      }

      public static Boolean operator >( PgSQLTimeZone x, PgSQLTimeZone y )
      {
         return !( x <= y );
      }

      public static Boolean operator >=( PgSQLTimeZone x, PgSQLTimeZone y )
      {
         return !( x < y );
      }

      public static PgSQLTimeZone operator -( PgSQLTimeZone tz )
      {
         return new PgSQLTimeZone( -tz._totalSeconds );
      }

      public static PgSQLTimeZone operator +( PgSQLTimeZone tz )
      {
         return tz;
      }

      #endregion

      #region To and from string

      private const Byte PREFIX_POS = (Byte) '+';
      private const Byte PREFIX_NEG = (Byte) '-';
      private const Byte SEPARATOR = (Byte) ':';
      private const Int32 HOURS_CHAR_COUNT = 2;
      private const Int32 MINUTES_CHAR_COUNT = 2;
      private const Int32 SECONDS_CHAR_COUNT = 2;

      public Int32 GetTextByteCount( IEncodingInfo encoding )
      {
         var retVal = 3 * encoding.BytesPerASCIICharacter; // +/-, and always 2 digits for hours
         var mins = this.Minutes;
         var secs = this.Seconds;
         if ( mins != 0 || secs != 0 )
         {
            retVal += 3 * encoding.BytesPerASCIICharacter; // For minutes, always
            if ( secs != 0 )
            {
               retVal += 3 * encoding.BytesPerASCIICharacter; // For seconds
            }
         }

         return retVal;
      }

      public void WriteTextBytes( IEncodingInfo encoding, Byte[] array, ref Int32 offset )
      {
         var hours = this.Hours;
         encoding.WriteASCIIByte( array, ref offset, hours > 0 ? PREFIX_POS : PREFIX_NEG ); // +/-
         hours = Math.Abs( hours );
         encoding.WriteIntegerTextual( array, ref offset, hours, HOURS_CHAR_COUNT );
         var mins = this.Minutes;
         var secs = this.Seconds;
         if ( mins != 0 || secs != 0 )
         {
            encoding
               .WriteASCIIByte( array, ref offset, SEPARATOR ) // ':'
               .WriteIntegerTextual( array, ref offset, Math.Abs( mins ), MINUTES_CHAR_COUNT );
            if ( secs != 0 )
            {
               encoding
                  .WriteASCIIByte( array, ref offset, SEPARATOR ) // ':'
                  .WriteIntegerTextual( array, ref offset, Math.Abs( secs ), SECONDS_CHAR_COUNT );
            }
         }
      }

      internal static PgSQLTimeZone ParseBinaryText( IEncodingInfo encoding, Byte[] array, ref Int32 offset, Int32 count )
      {
         var max = offset + count;
         var prefix = encoding.ReadASCIIByte( array, ref offset );
         if ( prefix != PREFIX_POS && prefix != PREFIX_NEG )
         {
            throw new FormatException( $"Expected timezone string to start with either '{PREFIX_POS}' or '{PREFIX_NEG}' characters." );
         }
         var hours = encoding.ParseInt32Textual( array, ref offset, (HOURS_CHAR_COUNT, true) );
         encoding.TryParseOptionalNumber( array, ref offset, SEPARATOR, (MINUTES_CHAR_COUNT, true), max, out Int32 minutes );
         encoding.TryParseOptionalNumber( array, ref offset, SEPARATOR, (SECONDS_CHAR_COUNT, true), max, out Int32 seconds );
         return new PgSQLTimeZone( hours, minutes, seconds );
      }

      public override String ToString()
      {
         var sb = new StringBuilder( this.Hours.ToString( "+0#;-0#" ) );
         if ( this.Minutes != 0 || this.Seconds != 0 )
         {
            sb.Append( ':' ).Append( Math.Abs( this.Minutes ).ToString( "D2" ) );
            if ( this.Seconds != 0 )
            {
               sb.Append( ':' ).Append( Math.Abs( this.Seconds ).ToString( "D2" ) );
            }
         }
         return sb.ToString();
      }

      public static PgSQLTimeZone Parse( String str )
      {
         PgSQLTimeZone result; Exception error;
         TryParse( str, out result, out error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLTimeZone result )
      {
         Exception error;
         return TryParse( str, out result, out error );
      }

      internal static Boolean TryParse( String str, out PgSQLTimeZone result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLTimeZone );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            error = null;
            var totalSeconds = 0;
            if ( str.Length <= 1 )
            {
               error = new FormatException( "Too short string." );
            }
            else
            {

               if ( error == null )
               {
                  Int32 hours, minutes; Decimal seconds; Boolean isNegative;
                  PgSQLInterval.ParseTime( str, 0, out hours, out minutes, out seconds, out isNegative, ref error, true );

                  if ( error == null )
                  {
                     seconds = Decimal.Truncate( seconds );
                     if ( seconds >= new Decimal( Int32.MinValue ) && seconds <= new Decimal( Int32.MaxValue ) )
                     {
                        totalSeconds = hours * MINUTES_PER_HOUR * SECONDS_PER_MINUTE + minutes * SECONDS_PER_MINUTE + (Int32) seconds;
                     }
                     else
                     {
                        error = new FormatException( "Seconds (" + seconds + ") are out of range for Int32." );
                     }
                  }
               }
            }

            result = error == null ?
               new PgSQLTimeZone( totalSeconds ) :
               default( PgSQLTimeZone );
         }
         return error == null;
      }

      #endregion
   }

   public interface IPgSQLTimeTZ : IPgSQLTime
   {
      #region Properties

      PgSQLTime LocalTime { get; }

      PgSQLTimeZone TimeZone { get; }

      PgSQLTime UTCTime { get; }

      #endregion
   }

   public struct PgSQLTimeTZ : IEquatable<PgSQLTimeTZ>, IComparable, IComparable<PgSQLTimeTZ>, IPgSQLTimeTZ
   {
      #region Static

      public static readonly PgSQLTimeTZ AllBalls = new PgSQLTimeTZ( PgSQLTime.AllBalls, PgSQLTimeZone.UTC );

      public static PgSQLTimeTZ Now
      {
         get
         {
            return new PgSQLTimeTZ( PgSQLTime.Now );
         }
      }

      public static PgSQLTimeTZ GetLocalMidnightFor( PgSQLDate date )
      {
         return new PgSQLTimeTZ( PgSQLTime.AllBalls, PgSQLTimeZone.GetLocalTimeZone( date ) );
      }

      #endregion

      #region Fields

      private readonly PgSQLTime _localTime;
      private readonly PgSQLTimeZone _timeZone;

      #endregion

      #region Constructors

      public PgSQLTimeTZ( PgSQLTime localTime, PgSQLTimeZone timeZone )
      {
         _localTime = localTime;
         _timeZone = timeZone;
      }

      public PgSQLTimeTZ( PgSQLTime localTime )
         : this( localTime, PgSQLTimeZone.CurrentTimeZone )
      {
      }

      public PgSQLTimeTZ( Int64 ticks )
         : this( new PgSQLTime( ticks ) )
      {
      }

      public PgSQLTimeTZ( TimeSpan time )
         : this( new PgSQLTime( time ) )
      {
      }

      public PgSQLTimeTZ( PgSQLInterval time )
         : this( new PgSQLTime( time ) )
      {
      }

      public PgSQLTimeTZ( PgSQLTimeTZ copyFrom )
         : this( copyFrom._localTime, copyFrom._timeZone )
      {
      }

      public PgSQLTimeTZ( Int32 hours, Int32 minutes, Int32 seconds )
         : this( new PgSQLTime( hours, minutes, seconds ) )
      {
      }

      public PgSQLTimeTZ( Int32 hours, Int32 minutes, Int32 seconds, Int32 microseconds )
         : this( new PgSQLTime( hours, minutes, seconds, microseconds ) )
      {
      }

      public PgSQLTimeTZ( Int32 hours, Int32 minutes, Decimal seconds )
         : this( new PgSQLTime( hours, minutes, seconds ) )
      {
      }

      public PgSQLTimeTZ( long ticks, PgSQLTimeZone timeZone )
         : this( new PgSQLTime( ticks ), timeZone )
      {
      }

      public PgSQLTimeTZ( TimeSpan time, PgSQLTimeZone timeZone )
         : this( new PgSQLTime( time ), timeZone )
      {
      }

      public PgSQLTimeTZ( PgSQLInterval time, PgSQLTimeZone timeZone )
         : this( new PgSQLTime( time ), timeZone )
      {
      }

      public PgSQLTimeTZ( Int32 hours, Int32 minutes, Int32 seconds, PgSQLTimeZone timeZone )
         : this( new PgSQLTime( hours, minutes, seconds ), timeZone )
      {
      }

      public PgSQLTimeTZ( Int32 hours, Int32 minutes, Int32 seconds, Int32 microseconds, PgSQLTimeZone timeZone )
         : this( new PgSQLTime( hours, minutes, seconds, microseconds ), timeZone )
      {
      }

      public PgSQLTimeTZ( Int32 hours, Int32 minutes, Decimal seconds, PgSQLTimeZone timeZone )
         : this( new PgSQLTime( hours, minutes, seconds ), timeZone )
      {
      }

      #endregion

      #region Properties

      public PgSQLTime LocalTime
      {
         get
         {
            return this._localTime;
         }
      }

      public PgSQLTimeZone TimeZone
      {
         get
         {
            return this._timeZone;
         }
      }

      public PgSQLTime UTCTime
      {
         get
         {
            return this.AtTimeZone( PgSQLTimeZone.UTC ).LocalTime;
         }
      }

      public Int32 Microseconds
      {
         get
         {
            return this._localTime.Microseconds;
         }
      }

      public Int32 Milliseconds
      {
         get
         {
            return this._localTime.Milliseconds;
         }
      }

      public Int32 Seconds
      {
         get
         {
            return this._localTime.Seconds;
         }
      }

      public Int32 Minutes
      {
         get
         {
            return this._localTime.Minutes;
         }
      }

      public Int32 Hours
      {
         get
         {
            return this._localTime.Hours;
         }
      }

      public Int64 Ticks
      {
         get
         {
            return this._localTime.Ticks;
         }
      }

      #endregion

      #region Comparison

      public Boolean Equals( PgSQLTimeTZ other )
      {
         return this._localTime.Equals( other._localTime ) && this._timeZone.Equals( other._timeZone );
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLTimeTZ && this.Equals( (PgSQLTimeTZ) obj );
      }

      public override Int32 GetHashCode()
      {
         // Like in point
         unchecked
         {
            return ( 17 * 23 + this._localTime.GetHashCode() ) * 23 + this._timeZone.GetHashCode();
         }
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLTimeTZ )
         {
            return this.CompareTo( (PgSQLTimeTZ) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Int32 CompareTo( PgSQLTimeTZ other )
      {
         var utcCompare = this.UTCTime.CompareTo( other.UTCTime );
         return utcCompare == 0 ? this._timeZone.CompareTo( other._timeZone ) : utcCompare;
      }

      #endregion

      #region Arithmetics

      public PgSQLTimeTZ Normalize()
      {
         return new PgSQLTimeTZ( this._localTime.Normalize(), this._timeZone );
      }

      public PgSQLTimeTZ AtTimeZone( PgSQLTimeZone timeZone )
      {
         return new PgSQLTimeTZ( this._localTime - this._timeZone + timeZone, timeZone );
      }

      public PgSQLTimeTZ AtTimeZone( PgSQLTimeZone timeZone, out Int32 overflow )
      {
         return new PgSQLTimeTZ( this._localTime.Add( timeZone - (PgSQLInterval) ( _timeZone ), out overflow ), timeZone );
      }

      public PgSQLTimeTZ Add( PgSQLInterval interval )
      {
         return new PgSQLTimeTZ( _localTime.Add( interval ), _timeZone );
      }

      internal PgSQLTimeTZ Add( PgSQLInterval interval, out Int32 overflow )
      {
         return new PgSQLTimeTZ( _localTime.Add( interval, out overflow ), _timeZone );
      }

      public PgSQLTimeTZ Subtract( PgSQLInterval interval )
      {
         return new PgSQLTimeTZ( _localTime.Subtract( interval ), _timeZone );
      }

      public PgSQLInterval Subtract( PgSQLTimeTZ earlier )
      {
         return _localTime.Subtract( earlier.AtTimeZone( _timeZone )._localTime );
      }

      #endregion

      #region Casts

      public static explicit operator DateTime( PgSQLTimeTZ x )
      {
         return new DateTime( x.AtTimeZone( PgSQLTimeZone.CurrentTimeZone ).Ticks, DateTimeKind.Local );
      }

      public static explicit operator PgSQLTimeTZ( DateTime x )
      {
         return new PgSQLTimeTZ( new PgSQLTime( x ) );
      }

      public static explicit operator TimeSpan( PgSQLTimeTZ x )
      {
         return (TimeSpan) x.LocalTime;
      }

      public static explicit operator PgSQLTimeTZ( TimeSpan x )
      {
         return new PgSQLTimeTZ( (PgSQLTime) x );
      }

      #endregion

      #region Operators

      public static Boolean operator ==( PgSQLTimeTZ x, PgSQLTimeTZ y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLTimeTZ x, PgSQLTimeTZ y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLTimeTZ x, PgSQLTimeTZ y )
      {
         return x.CompareTo( y ) < 0;
      }

      public static Boolean operator <=( PgSQLTimeTZ x, PgSQLTimeTZ y )
      {
         return x.CompareTo( y ) <= 0;
      }

      public static Boolean operator >( PgSQLTimeTZ x, PgSQLTimeTZ y )
      {
         return !( x <= y );
      }

      public static Boolean operator >=( PgSQLTimeTZ x, PgSQLTimeTZ y )
      {
         return !( x < y );
      }

      public static PgSQLTimeTZ operator +( PgSQLTimeTZ time, PgSQLInterval interval )
      {
         return time.Add( interval );
      }

      public static PgSQLTimeTZ operator +( PgSQLInterval interval, PgSQLTimeTZ time )
      {
         return time + interval;
      }

      public static PgSQLTimeTZ operator -( PgSQLTimeTZ time, PgSQLInterval interval )
      {
         return time.Subtract( interval );
      }

      public static PgSQLInterval operator -( PgSQLTimeTZ later, PgSQLTimeTZ earlier )
      {
         return later.Subtract( earlier );
      }

      #endregion

      #region To and from string

      public Int32 GetTextByteCount( IEncodingInfo encoding )
      {
         return this._localTime.GetTextByteCount( encoding ) + this._timeZone.GetTextByteCount( encoding );
      }

      public void WriteTextBytes( IEncodingInfo encoding, Byte[] array, ref Int32 offset )
      {
         this._localTime.WriteTextBytes( encoding, array, ref offset );
         this._timeZone.WriteTextBytes( encoding, array, ref offset );
      }

      public static PgSQLTimeTZ ParseBinaryText( IEncodingInfo encoding, Byte[] array, ref Int32 offset, Int32 count )
      {
         var oldIdx = offset;
         var time = PgSQLTime.ParseBinaryText( encoding, array, ref offset, count );
         count -= offset - oldIdx;
         return new PgSQLTimeTZ(
            time,
            PgSQLTimeZone.ParseBinaryText( encoding, array, ref offset, count )
            );
      }


      public override String ToString()
      {
         return this._localTime.ToString() + this._timeZone.ToString();
      }

      public static PgSQLTimeTZ Parse( String str )
      {
         PgSQLTimeTZ result; Exception error;
         TryParse( str, out result, out error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLTimeTZ result )
      {
         Exception error;
         return TryParse( str, out result, out error );
      }

      internal static Boolean TryParse( String str, out PgSQLTimeTZ result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLTimeTZ );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            error = null;
            //Search for timezone offset + or -
            var idx = Math.Max( str.IndexOf( '+' ), str.IndexOf( '-' ) );
            if ( idx < 0 )
            {
               error = new FormatException( "Missing time zone information." );
            }

            PgSQLTime time; PgSQLTimeZone tz;
            result = error == null && PgSQLTime.TryParse( str.Substring( 0, idx ), out time, out error ) && PgSQLTimeZone.TryParse( str.Substring( idx ), out tz, out error ) ?
               new PgSQLTimeTZ( time, tz ) :
               default( PgSQLTimeTZ );
         }
         return error == null;
      }

      #endregion

   }

   public interface IPgSQLTimestamp
   {
      #region Properties

      Boolean IsFinite { get; }

      Boolean IsInfinity { get; }

      Boolean IsMinusInfinity { get; }

      PgSQLDate Date { get; }

      #endregion
   }

   public struct PgSQLTimestamp : IEquatable<PgSQLTimestamp>, IComparable, IComparable<PgSQLTimestamp>, IPgSQLDate, IPgSQLTime, IPgSQLTimestamp
   {
      #region Inner types

      internal enum TSKind : byte
      {
         Normal,
         Infinity,
         MinusInfinity,
      }

      #endregion

      #region Static

      public static readonly PgSQLTimestamp Epoch = new PgSQLTimestamp( PgSQLDate.Epoch );
      public static readonly PgSQLTimestamp Era = new PgSQLTimestamp( PgSQLDate.Era );
      public static readonly PgSQLTimestamp Infinity = new PgSQLTimestamp( TSKind.Infinity );
      public static readonly PgSQLTimestamp MinusInfinity = new PgSQLTimestamp( TSKind.MinusInfinity );

      public static PgSQLTimestamp Now
      {
         get
         {
            return new PgSQLTimestamp( PgSQLDate.Now, PgSQLTime.Now );
         }
      }

      public static PgSQLTimestamp Today
      {
         get
         {
            return new PgSQLTimestamp( PgSQLDate.Now );
         }
      }

      public static PgSQLTimestamp Yesterday
      {
         get
         {
            return new PgSQLTimestamp( PgSQLDate.Yesterday );
         }
      }

      public static PgSQLTimestamp Tomorrow
      {
         get
         {
            return new PgSQLTimestamp( PgSQLDate.Tomorrow );
         }
      }

      #endregion

      #region Fields

      private readonly PgSQLDate _date;
      private readonly PgSQLTime _time;
      private readonly TSKind _kind;

      #endregion

      #region Constructors

      private PgSQLTimestamp( TSKind kind )
      {
         if ( kind != TSKind.Infinity && kind != TSKind.MinusInfinity )
         {
            throw new InvalidOperationException( "This constructor may only be used to create infinity or minus infinity timestamps." );
         }
         this._kind = kind;
         this._date = PgSQLDate.Era;
         this._time = PgSQLTime.AllBalls;
      }

      public PgSQLTimestamp( PgSQLDate date, PgSQLTime time )
      {
         this._kind = TSKind.Normal;
         this._date = date;
         this._time = time;
      }

      public PgSQLTimestamp( PgSQLDate date )
         : this( date, PgSQLTime.AllBalls )
      {
      }

      public PgSQLTimestamp( Int32 year, Int32 month, Int32 day, Int32 hours, Int32 minutes, Int32 seconds )
         : this( new PgSQLDate( year, month, day ), new PgSQLTime( hours, minutes, seconds ) )
      {
      }

      public PgSQLTimestamp( Int32 year, Int32 month, Int32 day, Int32 hours, Int32 minutes, Decimal seconds )
         : this( new PgSQLDate( year, month, day ), new PgSQLTime( hours, minutes, seconds ) )
      {
      }

      #endregion

      #region Properties

      public Boolean IsFinite
      {
         get
         {
            return this._kind == TSKind.Normal;
         }
      }

      public Boolean IsInfinity
      {
         get
         {
            return this._kind == TSKind.Infinity;
         }
      }

      public Boolean IsMinusInfinity
      {
         get
         {
            return this._kind == TSKind.MinusInfinity;
         }
      }

      public PgSQLDate Date
      {
         get
         {
            return this._date;
         }
      }

      public PgSQLTime Time
      {
         get
         {
            return this._time;
         }
      }

      public Int32 DayOfYear
      {
         get
         {
            return this._date.DayOfYear;
         }
      }

      public Int32 Year
      {
         get
         {
            return this._date.Year;
         }
      }

      public Int32 Month
      {
         get
         {
            return this._date.Month;
         }
      }

      public Int32 Day
      {
         get
         {
            return this._date.Day;
         }
      }

      public DayOfWeek DayOfWeek
      {
         get
         {
            return this._date.DayOfWeek;
         }
      }

      public Int32 DaysSinceEra
      {
         get
         {
            return this._date.DaysSinceEra;
         }
      }

      public Boolean IsLeapYear
      {
         get
         {
            return this._date.IsLeapYear;
         }
      }

      public Int64 Ticks
      {
         get
         {
            return this._time.Ticks;
         }
      }

      public Int32 Microseconds
      {
         get
         {
            return this._time.Microseconds;
         }
      }

      public Int32 Milliseconds
      {
         get
         {
            return this._time.Milliseconds;
         }
      }

      public Int32 Seconds
      {
         get
         {
            return this._time.Seconds;
         }
      }

      public Int32 Minutes
      {
         get
         {
            return this._time.Minutes;
         }
      }

      public Int32 Hours
      {
         get
         {
            return this._time.Hours;
         }
      }

      #endregion

      #region Arithmetics

      public PgSQLTimestamp AddDays( Int32 days )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
            case TSKind.MinusInfinity:
               return this;
            default:
               return new PgSQLTimestamp( this._date.AddDays( days ), this._time );
         }
      }

      public PgSQLTimestamp AddYears( Int32 years )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
            case TSKind.MinusInfinity:
               return this;
            default:
               return new PgSQLTimestamp( this._date.AddYears( years ), this._time );
         }
      }

      public PgSQLTimestamp AddMonths( Int32 months )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
            case TSKind.MinusInfinity:
               return this;
            default:
               return new PgSQLTimestamp( this._date.AddMonths( months ), this._time );
         }
      }

      public PgSQLTimestamp Add( PgSQLInterval interval )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
            case TSKind.MinusInfinity:
               return this;
            default:
               Int32 overflow;
               var time = this._time.Add( interval, out overflow );
               return new PgSQLTimestamp( this._date.Add( interval, overflow ), time );
         }
      }

      public PgSQLTimestamp Subtract( PgSQLInterval interval )
      {
         return Add( -interval );
      }

      public PgSQLInterval Subtract( PgSQLTimestamp timestamp )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
            case TSKind.MinusInfinity:
               throw new ArgumentOutOfRangeException( "this", "You cannot subtract infinity timestamps" );
         }
         switch ( timestamp._kind )
         {
            case TSKind.Infinity:
            case TSKind.MinusInfinity:
               throw new ArgumentOutOfRangeException( "timestamp", "You cannot subtract infinity timestamps" );
         }

         return new PgSQLInterval( 0, this._date.DaysSinceEra - timestamp._date.DaysSinceEra, this._time.Ticks - timestamp._time.Ticks );
      }

      #endregion

      #region Normalization

      public PgSQLTimestamp Normalize()
      {
         return this.Add( PgSQLInterval.Zero );
      }

      #endregion

      #region Time zone

      public PgSQLTimestampTZ AtTimeZone( PgSQLTimeZone timeZoneFrom, PgSQLTimeZone timeZoneTo )
      {
         Int32 overflow;
         var adjusted = new PgSQLTimeTZ( _time, timeZoneFrom ).AtTimeZone( timeZoneTo, out overflow );
         return new PgSQLTimestampTZ( _date.AddDays( overflow ), adjusted );
      }

      public PgSQLTimestampTZ AtTimeZone( PgSQLTimeZone timeZone )
      {
         return AtTimeZone( timeZone, PgSQLTimeZone.GetLocalTimeZone( _date ) );
      }

      #endregion

      #region Comparison

      public Boolean Equals( PgSQLTimestamp other )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               return other._kind == TSKind.Infinity;
            case TSKind.MinusInfinity:
               return other._kind == TSKind.MinusInfinity;
            default:
               return other._kind == TSKind.Normal && this._date.Equals( other._date ) && this._time.Equals( other._time );
         }
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLTimestamp && this.Equals( (PgSQLTimestamp) obj );
      }

      public override Int32 GetHashCode()
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               return Int32.MaxValue;
            case TSKind.MinusInfinity:
               return Int32.MinValue;
            default:
               // Like in point
               unchecked
               {
                  return ( 17 * 23 + this._date.GetHashCode() ) * 23 + this._time.GetHashCode();
               }
         }
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLTimestamp )
         {
            return this.CompareTo( (PgSQLTimestamp) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Int32 CompareTo( PgSQLTimestamp other )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               return other._kind == TSKind.Infinity ? 0 : 1;
            case TSKind.MinusInfinity:
               return other._kind == TSKind.MinusInfinity ? 0 : -1;
            default:
               var dateCompare = this._date.CompareTo( other._date );
               return dateCompare == 0 ? this._time.CompareTo( other._time ) : dateCompare;
         }
      }

      #endregion

      #region Casts and conversions

      public static explicit operator PgSQLTimestamp( DateTime x )
      {
         return x == DateTime.MaxValue ?
            Infinity : ( x == DateTime.MinValue ? MinusInfinity :
            new PgSQLTimestamp( new PgSQLDate( x ), new PgSQLTime( x.TimeOfDay ) ) );
      }

      public static explicit operator DateTime( PgSQLTimestamp x )
      {
         return x.ToDateTime( DateTimeKind.Unspecified );
      }

      public DateTime ToDateTime( DateTimeKind dtKind )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               return DateTime.MaxValue;
            case TSKind.MinusInfinity:
               return DateTime.MinValue;
            default:
               try
               {
                  return new DateTime( this._date.DaysSinceEra * TimeSpan.TicksPerDay + this._time.Ticks, dtKind );
               }
               catch ( Exception exc )
               {
                  throw new InvalidCastException( "", exc );
               }
         }
      }

      #endregion

      #region Operators

      public static Boolean operator ==( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return x.CompareTo( y ) < 0;
      }

      public static Boolean operator <=( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return x.CompareTo( y ) <= 0;
      }

      public static Boolean operator >( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return !( x <= y );
      }

      public static Boolean operator >=( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return !( x < y );
      }

      public static PgSQLTimestamp operator +( PgSQLTimestamp timestamp, PgSQLInterval interval )
      {
         return timestamp.Add( interval );
      }

      public static PgSQLTimestamp operator +( PgSQLInterval interval, PgSQLTimestamp timestamp )
      {
         return timestamp.Add( interval );
      }

      public static PgSQLTimestamp operator -( PgSQLTimestamp timestamp, PgSQLInterval interval )
      {
         return timestamp.Subtract( interval );
      }

      public static PgSQLInterval operator -( PgSQLTimestamp x, PgSQLTimestamp y )
      {
         return x.Subtract( y );
      }

      #endregion

      #region To and from string

      private const Byte SEPARATOR = (Byte) ' ';

      public Int32 GetTextByteCount( IEncodingInfo encoding )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               return encoding.Encoding.GetByteCount( PgSQLDate.INFINITY );
            case TSKind.MinusInfinity:
               return encoding.Encoding.GetByteCount( PgSQLDate.MINUS_INFINITY );
            default:
               return encoding.BytesPerASCIICharacter + this._date.GetTextByteCount( encoding ) + this._time.GetTextByteCount( encoding );
         }
      }

      public void WriteTextBytes( IEncodingInfo encoding, Byte[] array, ref Int32 offset )
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               encoding.Encoding.GetBytes( PgSQLDate.INFINITY, 0, PgSQLDate.INFINITY.Length, array, offset );
               break;
            case TSKind.MinusInfinity:
               encoding.Encoding.GetBytes( PgSQLDate.MINUS_INFINITY, 0, PgSQLDate.MINUS_INFINITY.Length, array, offset );
               break;
            default:
               this._date.WriteTextBytes( encoding, array, ref offset );
               encoding.WriteASCIIByte( array, ref offset, SEPARATOR ); // ' '
               this._time.WriteTextBytes( encoding, array, ref offset );
               break;
         }
      }

      public static PgSQLTimestamp ParseBinaryText( IEncodingInfo encoding, Byte[] array, ref Int32 offset, Int32 count )
      {
         switch ( count )
         {
            case PgSQLDate.INFINITY_CHAR_COUNT:
               return Infinity;
            case PgSQLDate.MINUS_INFINITY_CHAR_COUNT:
               return MinusInfinity;
            default:
               var oldIdx = offset;
               var date = PgSQLDate.ParseBinaryText( encoding, array, ref offset, count );
               encoding.EqualsOrThrow( array, ref offset, SEPARATOR );
               count -= offset - oldIdx;
               return new PgSQLTimestamp(
                  date,
                  PgSQLTime.ParseBinaryText( encoding, array, ref offset, count )
                  );
         }
      }

      public override String ToString()
      {
         switch ( this._kind )
         {
            case TSKind.Infinity:
               return PgSQLDate.INFINITY;
            case TSKind.MinusInfinity:
               return PgSQLDate.MINUS_INFINITY;
            default:
               return this._date.ToString() + " " + this._time.ToString();
         }
      }

      public static PgSQLTimestamp Parse( String str )
      {
         PgSQLTimestamp result; Exception error;
         TryParse( str, out result, out error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLTimestamp result )
      {
         Exception error;
         return TryParse( str, out result, out error );
      }

      private static Boolean TryParse( String str, out PgSQLTimestamp result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLTimestamp );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            str = str.Trim();
            error = null;
            if ( String.Equals( str, PgSQLDate.INFINITY, StringComparison.OrdinalIgnoreCase ) )
            {
               result = Infinity;
            }
            else if ( String.Equals( str, PgSQLDate.MINUS_INFINITY, StringComparison.OrdinalIgnoreCase ) )
            {
               result = MinusInfinity;
            }
            else
            {
               var idx = str.LastIndexOf( ' ' );
               if ( idx == -1 )
               {
                  error = new FormatException( "Failed to distinguish date and time in timestamp." );
               }

               PgSQLDate date; PgSQLTime time;
               result = error == null && PgSQLDate.TryParse( str.Substring( 0, idx ), out date, out error ) && PgSQLTime.TryParse( str.Substring( idx + 1 ), out time, out error ) ?
                  new PgSQLTimestamp( date, time ) :
                  default( PgSQLTimestamp );
            }
         }
         return error == null;
      }

      #endregion

   }

   public struct PgSQLTimestampTZ : IEquatable<PgSQLTimestampTZ>, IComparable, IComparable<PgSQLTimestampTZ>, IPgSQLDate, IPgSQLTimeTZ, IPgSQLTimestamp
   {
      #region Static

      public static readonly PgSQLTimestampTZ Epoch = new PgSQLTimestampTZ( PgSQLDate.Epoch, PgSQLTimeTZ.AllBalls );

      public static readonly PgSQLTimestampTZ Era = new PgSQLTimestampTZ( PgSQLDate.Era, PgSQLTimeTZ.AllBalls );

      public static readonly PgSQLTimestampTZ MinusInfinity = new PgSQLTimestampTZ( PgSQLTimestamp.TSKind.MinusInfinity );

      public static readonly PgSQLTimestampTZ Infinity = new PgSQLTimestampTZ( PgSQLTimestamp.TSKind.Infinity );

      public static PgSQLTimestampTZ Now
      {
         get
         {
            return new PgSQLTimestampTZ( PgSQLDate.Now, PgSQLTimeTZ.Now );
         }
      }

      public static PgSQLTimestampTZ Today
      {
         get
         {
            return new PgSQLTimestampTZ( PgSQLDate.Now );
         }
      }

      public static PgSQLTimestampTZ Yesterday
      {
         get
         {
            return new PgSQLTimestampTZ( PgSQLDate.Yesterday );
         }
      }

      public static PgSQLTimestampTZ Tomorrow
      {
         get
         {
            return new PgSQLTimestampTZ( PgSQLDate.Tomorrow );
         }
      }

      #endregion

      #region Fields

      private readonly PgSQLDate _date;
      private readonly PgSQLTimeTZ _time;
      private readonly PgSQLTimestamp.TSKind _kind;

      #endregion

      #region Constructors

      private PgSQLTimestampTZ( PgSQLTimestamp.TSKind kind )
      {
         if ( kind != PgSQLTimestamp.TSKind.Infinity && kind != PgSQLTimestamp.TSKind.MinusInfinity )
         {
            throw new InvalidOperationException( "This constructor may only be used to create infinity or minus infinity timestamps." );
         }
         this._kind = kind;
         this._date = PgSQLDate.Era;
         this._time = PgSQLTimeTZ.AllBalls;
      }

      public PgSQLTimestampTZ( PgSQLDate date, PgSQLTimeTZ time )
      {
         this._kind = PgSQLTimestamp.TSKind.Normal;
         this._date = date;
         this._time = time;
      }

      public PgSQLTimestampTZ( PgSQLDate date )
         : this( date, PgSQLTimeTZ.AllBalls )
      {
      }

      public PgSQLTimestampTZ( Int32 year, Int32 month, Int32 day, Int32 hours, Int32 minutes, Int32 seconds, PgSQLTimeZone? timeZone = null )
         : this( new PgSQLDate( year, month, day ), new PgSQLTimeTZ( hours, minutes, seconds, timeZone.HasValue ? timeZone.Value : PgSQLTimeZone.GetLocalTimeZone( new PgSQLDate( year, month, day ) ) ) )
      {
      }

      public PgSQLTimestampTZ( Int32 year, Int32 month, Int32 day, Int32 hours, Int32 minutes, Decimal seconds, PgSQLTimeZone? timeZone = null )
         : this( new PgSQLDate( year, month, day ), new PgSQLTimeTZ( hours, minutes, seconds, timeZone.HasValue ? timeZone.Value : PgSQLTimeZone.GetLocalTimeZone( new PgSQLDate( year, month, day ) ) ) )
      {
      }

      #endregion

      #region Properties

      public Int32 DayOfYear
      {
         get
         {
            return this._date.DayOfYear;
         }
      }

      public Int32 Year
      {
         get
         {
            return this._date.Year;
         }
      }

      public Int32 Month
      {
         get
         {
            return this._date.Month;
         }
      }

      public Int32 Day
      {
         get
         {
            return this._date.Day;
         }
      }

      public DayOfWeek DayOfWeek
      {
         get
         {
            return this._date.DayOfWeek;
         }
      }

      public Int32 DaysSinceEra
      {
         get
         {
            return this._date.DaysSinceEra;
         }
      }

      public Boolean IsLeapYear
      {
         get
         {
            return this._date.IsLeapYear;
         }
      }

      public PgSQLTime LocalTime
      {
         get
         {
            return this._time.LocalTime;
         }
      }

      public PgSQLTimeZone TimeZone
      {
         get
         {
            return this._time.TimeZone;
         }
      }

      public PgSQLTime UTCTime
      {
         get
         {
            return this._time.UTCTime;
         }
      }

      public Int64 Ticks
      {
         get
         {
            return this._time.Ticks;
         }
      }

      public Int32 Microseconds
      {
         get
         {
            return this._time.Microseconds;
         }
      }

      public Int32 Milliseconds
      {
         get
         {
            return this._time.Milliseconds;
         }
      }

      public Int32 Seconds
      {
         get
         {
            return this._time.Seconds;
         }
      }

      public Int32 Minutes
      {
         get
         {
            return this._time.Minutes;
         }
      }

      public Int32 Hours
      {
         get
         {
            return this._time.Hours;
         }
      }

      public Boolean IsFinite
      {
         get
         {
            return this._kind == PgSQLTimestamp.TSKind.Normal;
         }
      }

      public Boolean IsInfinity
      {
         get
         {
            return this._kind == PgSQLTimestamp.TSKind.Infinity;
         }
      }

      public Boolean IsMinusInfinity
      {
         get
         {
            return this._kind == PgSQLTimestamp.TSKind.MinusInfinity;
         }
      }

      public PgSQLDate Date
      {
         get
         {
            return this._date;
         }
      }

      public PgSQLTimeTZ Time
      {
         get
         {
            return this._time;
         }
      }

      #endregion

      #region Comparison

      public Boolean Equals( PgSQLTimestampTZ other )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return other._kind == PgSQLTimestamp.TSKind.Infinity;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return other._kind == PgSQLTimestamp.TSKind.MinusInfinity;
            default:
               return other._kind == PgSQLTimestamp.TSKind.Normal && this._date.Equals( other._date ) && this._time.Equals( other._time );
         }
      }

      public override Boolean Equals( Object obj )
      {
         return obj != null && obj is PgSQLTimestampTZ && this.Equals( (PgSQLTimestampTZ) obj );
      }

      public override Int32 GetHashCode()
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return Int32.MaxValue;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return Int32.MinValue;
            default:
               // Like in point
               unchecked
               {
                  return ( 17 * 23 + this._date.GetHashCode() ) * 23 + this._time.GetHashCode();
               }
         }
      }

      Int32 IComparable.CompareTo( Object obj )
      {
         if ( obj == null )
         {
            // This is always 'greater' than null
            return 1;
         }
         else if ( obj is PgSQLTimestampTZ )
         {
            return this.CompareTo( (PgSQLTimestampTZ) obj );
         }
         else
         {
            throw new ArgumentException( "Given object must be of type " + this.GetType() + " or null." );
         }
      }

      public Int32 CompareTo( PgSQLTimestampTZ other )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return other._kind == PgSQLTimestamp.TSKind.Infinity ? 0 : 1;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return other._kind == PgSQLTimestamp.TSKind.MinusInfinity ? 0 : -1;
            default:
               var dateCompare = this._date.CompareTo( other._date );
               return dateCompare == 0 ? this._time.CompareTo( other._time ) : dateCompare;
         }
      }

      #endregion

      #region Arithmetics

      public PgSQLTimestampTZ AddDays( Int32 days )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return this;
            default:
               return new PgSQLTimestampTZ( this._date.AddDays( days ), this._time );
         }
      }

      public PgSQLTimestampTZ AddYears( Int32 years )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return this;
            default:
               return new PgSQLTimestampTZ( this._date.AddYears( years ), this._time );
         }
      }

      public PgSQLTimestampTZ AddMonths( Int32 months )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return this;
            default:
               return new PgSQLTimestampTZ( this._date.AddMonths( months ), this._time );
         }
      }

      public PgSQLTimestampTZ Add( PgSQLInterval interval )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return this;
            default:
               int overflow;
               var time = this._time.Add( interval, out overflow );
               return new PgSQLTimestampTZ( this._date.Add( interval, overflow ), time );
         }
      }

      public PgSQLTimestampTZ Subtract( PgSQLInterval interval )
      {
         return Add( -interval );
      }

      public PgSQLInterval Subtract( PgSQLTimestampTZ timestamp )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
            case PgSQLTimestamp.TSKind.MinusInfinity:
               throw new ArgumentOutOfRangeException( "this", "You cannot subtract infinity timestamps" );
         }
         switch ( timestamp._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
            case PgSQLTimestamp.TSKind.MinusInfinity:
               throw new ArgumentOutOfRangeException( "timestamp", "You cannot subtract infinity timestamps" );
         }
         return new PgSQLInterval( 0, this._date.DaysSinceEra - timestamp._date.DaysSinceEra, ( this._time - timestamp._time ).Ticks );
      }

      #endregion

      #region Normalization

      public PgSQLTimestampTZ Normalize()
      {
         return this.Add( PgSQLInterval.Zero );
      }

      #endregion

      #region Time zone

      public PgSQLTimestamp AtTimeZone( PgSQLTimeZone timeZone )
      {
         Int32 overflow;
         var adjusted = this._time.AtTimeZone( timeZone, out overflow );
         return new PgSQLTimestamp( _date.AddDays( overflow ), adjusted.LocalTime );
      }

      #endregion

      #region Casts

      public static explicit operator PgSQLTimestampTZ( DateTime x )
      {
         return x == DateTime.MaxValue ?
            Infinity : ( x == DateTime.MinValue ? MinusInfinity :
            new PgSQLTimestampTZ( new PgSQLDate( x ), new PgSQLTimeTZ( x.TimeOfDay, x.Kind == DateTimeKind.Utc ? PgSQLTimeZone.UTC : PgSQLTimeZone.GetLocalTimeZone( new PgSQLDate( x ) ) ) ) );
      }

      public static explicit operator DateTime( PgSQLTimestampTZ x )
      {
         switch ( x._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return DateTime.MaxValue;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return DateTime.MinValue;
            default:
               var utc = x.AtTimeZone( PgSQLTimeZone.UTC );
               try
               {
                  return new DateTime( utc.Date.DaysSinceEra * TimeSpan.TicksPerDay + utc.Time.Ticks, DateTimeKind.Utc );
               }
               catch ( Exception exc )
               {
                  throw new InvalidCastException( "", exc );
               }

         }
      }

      public static explicit operator PgSQLTimestampTZ( DateTimeOffset x )
      {
         return x == DateTimeOffset.MaxValue ?
            Infinity : ( x == DateTimeOffset.MinValue ? MinusInfinity :
            new PgSQLTimestampTZ( new PgSQLDate( x.Year, x.Month, x.Day ), new PgSQLTimeTZ( x.TimeOfDay, new PgSQLTimeZone( x.Offset ) ) ) );
      }

      public static explicit operator DateTimeOffset( PgSQLTimestampTZ x )
      {
         switch ( x._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return DateTimeOffset.MaxValue;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return DateTimeOffset.MinValue;
            default:
               try
               {
                  return new DateTimeOffset( x._date.DaysSinceEra * TimeSpan.TicksPerDay + x._time.Ticks, x.TimeZone );
               }
               catch ( Exception exc )
               {
                  throw new InvalidCastException( "", exc );
               }

         }
      }

      #endregion

      #region Operators

      public static Boolean operator ==( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return x.Equals( y );
      }

      public static Boolean operator !=( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return !( x == y );
      }

      public static Boolean operator <( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return x.CompareTo( y ) < 0;
      }

      public static Boolean operator <=( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return x.CompareTo( y ) <= 0;
      }

      public static Boolean operator >( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return !( x <= y );
      }

      public static Boolean operator >=( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return !( x < y );
      }

      public static PgSQLTimestampTZ operator +( PgSQLTimestampTZ timestamp, PgSQLInterval interval )
      {
         return timestamp.Add( interval );
      }

      public static PgSQLTimestampTZ operator +( PgSQLInterval interval, PgSQLTimestampTZ timestamp )
      {
         return timestamp.Add( interval );
      }

      public static PgSQLTimestampTZ operator -( PgSQLTimestampTZ timestamp, PgSQLInterval interval )
      {
         return timestamp.Subtract( interval );
      }

      public static PgSQLInterval operator -( PgSQLTimestampTZ x, PgSQLTimestampTZ y )
      {
         return x.Subtract( y );
      }

      #endregion

      #region To and from string

      public Int32 GetTextByteCount( IEncodingInfo encoding )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return encoding.Encoding.GetByteCount( PgSQLDate.INFINITY );
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return encoding.Encoding.GetByteCount( PgSQLDate.MINUS_INFINITY );
            default:
               return encoding.BytesPerASCIICharacter + this._date.GetTextByteCount( encoding ) + this._time.GetTextByteCount( encoding );
         }
      }

      public void WriteTextBytes( IEncodingInfo encoding, Byte[] array, ref Int32 offset )
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               encoding.Encoding.GetBytes( PgSQLDate.INFINITY, 0, PgSQLDate.INFINITY.Length, array, offset );
               break;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               encoding.Encoding.GetBytes( PgSQLDate.MINUS_INFINITY, 0, PgSQLDate.MINUS_INFINITY.Length, array, offset );
               break;
            default:
               this._date.WriteTextBytes( encoding, array, ref offset );
               encoding.WriteASCIIByte( array, ref offset, 0x020 ); // ' '
               this._time.WriteTextBytes( encoding, array, ref offset );
               break;
         }
      }

      public static PgSQLTimestampTZ ParseBinaryText( IEncodingInfo encoding, Byte[] array, ref Int32 offset, Int32 count )
      {
         var oldIdx = offset;
         var ts = PgSQLTimestamp.ParseBinaryText( encoding, array, ref offset, count );
         if ( ts.IsInfinity )
         {
            return Infinity;
         }
         else if ( ts.IsMinusInfinity )
         {
            return MinusInfinity;
         }
         else
         {
            count -= offset - oldIdx;
            if ( count <= 0 )
            {
               throw new FormatException( "No room for time zone information" );
            }
            return new PgSQLTimestampTZ( ts.Date, new PgSQLTimeTZ( ts.Time, PgSQLTimeZone.ParseBinaryText( encoding, array, ref offset, count ) ) );
         }
      }

      public override String ToString()
      {
         switch ( this._kind )
         {
            case PgSQLTimestamp.TSKind.Infinity:
               return PgSQLDate.INFINITY;
            case PgSQLTimestamp.TSKind.MinusInfinity:
               return PgSQLDate.MINUS_INFINITY;
            default:
               return this._date.ToString() + " " + this._time.ToString();
         }
      }

      public static PgSQLTimestampTZ Parse( String str )
      {
         TryParse( str, out PgSQLTimestampTZ result, out Exception error );
         if ( error != null )
         {
            throw error;
         }
         return result;
      }

      public static Boolean TryParse( String str, out PgSQLTimestampTZ result )
      {
         Exception error;
         return TryParse( str, out result, out error );
      }

      private static Boolean TryParse( String str, out PgSQLTimestampTZ result, out Exception error )
      {
         if ( str == null )
         {
            result = default( PgSQLTimestampTZ );
            error = new ArgumentNullException( "String" );
         }
         else
         {
            str = str.Trim();
            error = null;
            if ( String.Equals( str, PgSQLDate.INFINITY, StringComparison.OrdinalIgnoreCase ) )
            {
               result = Infinity;
            }
            else if ( String.Equals( str, PgSQLDate.MINUS_INFINITY, StringComparison.OrdinalIgnoreCase ) )
            {
               result = MinusInfinity;
            }
            else
            {
               var idx = str.LastIndexOf( ' ' );
               if ( idx == -1 )
               {
                  error = new FormatException( "Failed to distinguish date and time in timestamp." );
               }

               PgSQLDate date; PgSQLTimeTZ time;
               result = error == null && PgSQLDate.TryParse( str.Substring( 0, idx ), out date, out error ) && PgSQLTimeTZ.TryParse( str.Substring( idx + 1 ), out time, out error ) ?
                  new PgSQLTimestampTZ( date, time ) :
                  default( PgSQLTimestampTZ );
            }
         }
         return error == null;
      }

      #endregion

   }


}

public static partial class E_CBAM
{


   internal static IEncodingInfo EqualsOrThrow( this IEncodingInfo encoding, Byte[] array, ref Int32 offset, Byte b )
   {
      if ( array[offset] != b )
      {
         throw new FormatException( "Missing required " + (Char) b + "." );
      }
      offset += encoding.BytesPerASCIICharacter;
      return encoding;
   }

   internal static Boolean TryParseOptionalNumber(
      this IEncodingInfo encoding,
      Byte[] array,
      ref Int32 offset,
      Byte prefix,
      (Int32 CharCount, Boolean CharCountExactMatch) charCount,
      Int32 maxOffset, // Exclusive
      out Int32 parsedNumber
      )
   {
      var oldIdx = offset;
      var retVal = offset < maxOffset && encoding.ReadASCIIByte( array, ref offset ) == prefix;
      if ( retVal )
      {
         parsedNumber = encoding.ParseInt32Textual( array, ref offset, charCount );
      }
      else
      {
         // 'Reverse back'
         offset = oldIdx;
         parsedNumber = 0;
      }

      return retVal;
   }



   //public static async Task<(Int32 result, Int32 bytesLeft)> ReadInt32( this BackendABIHelper args, Stream stream, CancellationToken token, Int32 bytesLeft )
   //{
   //   await args.ReadAllBytes( stream, 0, sizeof( Int32 ), token, sizeof( Int32 ) );
   //   return (args.Buffer.Array.ReadInt32BEFromBytesNoRef( 0 ), bytesLeft - sizeof( Int32 ));
   //}

   //public static async ValueTask<String> ReadZeroTerminatedString( this BackendABIHelper args, Stream stream, CancellationToken token )
   //{
   //   var offset = 0;
   //   Byte lastByte;
   //   do
   //   {
   //      args.Buffer.CurrentMaxCapacity = offset + 1;
   //      await args.ReadAllBytes( stream, offset, 1, token );
   //      lastByte = args.Buffer.Array[offset];
   //      ++offset;
   //   } while ( lastByte != 0 );

   //   // TODO string pool
   //   return args.GetString( 0, offset - 1 );
   //}

   //public static Byte ReadByte( this BackendABIHelper args,  ref Int32 offset )
   //{
   //   return args.Buffer.Array.ReadByteFromBytes( ref offset );
   //}

   //public static Int16 ReadInt16( this BackendABIHelper args, ref Int32 offset )
   //{
   //   return args.Buffer.Array.ReadInt16BEFromBytes( ref offset );
   //}

   //public static Int32 ReadInt16Count( this BackendABIHelper args, ref Int32 offset )
   //{
   //   return args.Buffer.Array.ReadUInt16BEFromBytes( ref offset );
   //}



   //public static String ReadZString( this BackendABIHelper args, ref Int32 offset )
   //{
   //   var array = args.Buffer.Array;
   //   var start = offset;
   //   while ( array[offset++] != 0 ) ;

   //   return args.GetString( start, offset - start - 1 );
   //}

   //public static async Task SkipBytes( this BackendABIHelper args, Stream stream, Int32 count, CancellationToken token )
   //{
   //   var array = args.Buffer.Array;
   //   do
   //   {
   //      await args.ReadAllBytes( stream, 0, Math.Min( array.Length, count ), token );
   //      count -= array.Length; // TODO this + while condition might behave erratically on very large array length - count differences (underflow)
   //   } while ( count > 0 );
   //}

   //public static async Task<Int32> ReadAllBytes( this BackendABIHelper args, Stream stream, Int32 offset, Int32 count, CancellationToken token, Int32 bytesLeft )
   //{
   //   var bytesToRead = count - offset;
   //   if ( bytesLeft <= 0 || bytesToRead > bytesLeft )
   //   {
   //      throw new EndOfStreamException( $"Not enough bytes left (wanted {bytesToRead}, but has {bytesLeft} bytes left)." );
   //   }
   //   await args.ReadAllBytes( stream, offset, count, token );
   //   return bytesLeft - bytesToRead;
   //}

   //public static async Task ReadAllBytes( this BackendABIHelper args, Stream stream, Byte[] array, Int32 offset, Int32 count, CancellationToken token )
   //{
   //   await stream.ReadSpecificAmountAsync( array, offset, count, token );
   //}







}