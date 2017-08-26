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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;


namespace CBAM.SQL.PostgreSQL
{

   using TSyncTextualSizeInfo = ValueTuple<Int32, String>;

   /// <summary>
   /// This interface allows customization as to how <see cref="PgSQLConnection"/> handles SQL and CLR types and mapping between them.
   /// </summary>
   /// <seealso cref="PgSQLConnection.TypeRegistry"/>
   /// <seealso cref="PgSQLTypeFunctionality"/>
   public interface TypeRegistry
   {
      /// <summary>
      /// Asynchronously adds functionality for given types so that the <see cref="PgSQLConnection"/> of this <see cref="TypeRegistry"/> would know how to transform data sent by backend into correct CLR type when using <see cref="UtilPack.TabularData.AsyncDataColumn.TryGetValueAsync"/> method.
      /// </summary>
      /// <param name="typeFunctionalityInfos">The information about type functionalities, containing the type name in the database (e.g. <c>"int2"</c> or <c>"text"</c>), corresponding CLR type (e.g. <see cref="Int16"/> or <see cref="String"/>), and callback to create <see cref="TypeFunctionalityCreationResult"/> object from given <see cref="PgSQLTypeDatabaseData"/>.</param>
      /// <returns>Asynchronously returns the amount of functionalities actually processed. Any functionality information for the type name that occurred previously in <paramref name="typeFunctionalityInfos"/> will overwrite previous one.</returns>
      /// <exception cref="PgSQLException">If some error occurs during querying type IDs from database.</exception>
      ValueTask<Int32> AddTypeFunctionalitiesAsync( params (String DBTypeName, Type CLRType, Func<PgSQLTypeDatabaseData, TypeFunctionalityCreationResult> FunctionalityCreator)[] typeFunctionalityInfos );

      /// <summary>
      /// Tries to get <see cref="TypeFunctionalityInformation"/> for given type ID.
      /// </summary>
      /// <param name="typeID">The type ID, as stored in the database this connection is connected to.</param>
      /// <returns>A <see cref="TypeFunctionalityInformation"/> for given type ID, or <c>null</c> if type information for given type ID is not present in this <see cref="TypeRegistry"/>.</returns>
      TypeFunctionalityInformation TryGetTypeInfo( Int32 typeID );

      /// <summary>
      /// Tries to get <see cref="TypeFunctionalityInformation"/> for given CLR type.
      /// </summary>
      /// <param name="clrType">The CLR <see cref="Type"/>.</param>
      /// <returns>A <see cref="TypeFunctionalityInformation"/> for given CLR type, or <c>null</c> if type information for given CLR type is not present in this <see cref="TypeRegistry"/>.</returns>
      /// <remarks>
      /// The <see cref="TypeFunctionalityInformation.CLRType"/> property of returned <see cref="TypeFunctionalityInformation"/> may be different from <paramref name="clrType"/>, if <paramref name="clrType"/> inherits from some type that this <see cref="TypeRegistry"/> knows about.
      /// </remarks>
      TypeFunctionalityInformation TryGetTypeInfo( Type clrType );

   }

   //public struct TypeFunctionalityCreationParameters
   //{
   //   public TypeFunctionalityCreationParameters(
   //      TypeRegistry typeRegistry,
   //      PgSQLTypeDatabaseData databaseData
   //      )
   //   {
   //      this.TypeRegistry = ArgumentValidator.ValidateNotNull( nameof( typeRegistry ), typeRegistry );
   //      this.DatabaseData = ArgumentValidator.ValidateNotNull( nameof( databaseData ), databaseData );
   //   }

   //   public TypeRegistry TypeRegistry { get; }
   //   public PgSQLTypeDatabaseData DatabaseData { get; }
   //}

   /// <summary>
   /// This type is used as return type for callback which adds custom type functionality via <see cref="TypeRegistry.AddTypeFunctionalitiesAsync"/> methpood.
   /// </summary>
   public struct TypeFunctionalityCreationResult
   {
      /// <summary>
      /// Creates a new <see cref="TypeFunctionalityCreationResult"/> with given parameters.
      /// </summary>
      /// <param name="functionality">The <see cref="PgSQLTypeFunctionality"/> object.</param>
      /// <param name="isDefaultForCLRType">Whether the <paramref name="functionality"/> is the default for CLR type it represents.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="functionality"/> is <c>null</c>.</exception>
      public TypeFunctionalityCreationResult(
         PgSQLTypeFunctionality functionality,
         Boolean isDefaultForCLRType
         )
      {
         this.TypeFunctionality = ArgumentValidator.ValidateNotNull( nameof( functionality ), functionality );
         this.IsDefaultForCLRType = isDefaultForCLRType;
      }

      /// <summary>
      /// Gets the <see cref="PgSQLTypeFunctionality"/> object.
      /// </summary>
      /// <value>The <see cref="PgSQLTypeFunctionality"/> object.</value>
      /// <seealso cref="PgSQLTypeFunctionality"/>
      public PgSQLTypeFunctionality TypeFunctionality { get; }

      /// <summary>
      /// Gets the value indicating whether <see cref="TypeFunctionality"/> is the default for CLR type it represents.
      /// </summary>
      /// <value>The value indicating whether <see cref="TypeFunctionality"/> is the default for CLR type it represents.</value>
      public Boolean IsDefaultForCLRType { get; }
   }

   /// <summary>
   /// This class contains all information about a single mapping between PostgreSQL type and CLR type.
   /// </summary>
   public class TypeFunctionalityInformation
   {
      /// <summary>
      /// Creates a new instance of <see cref="TypeFunctionalityInformation"/> with given parameters.
      /// </summary>
      /// <param name="clrType">The CLR <see cref="Type"/> that <paramref name="functionality"/> supports.</param>
      /// <param name="functionality">The <see cref="PgSQLTypeFunctionality"/> for this type information.</param>
      /// <param name="databaseData">The <see cref="PgSQLTypeDatabaseData"/> containing type name and type ID for this type information.</param>
      /// <remarks>The constructor is intended to be used mainly by <see cref="TypeRegistry"/> implementations.</remarks>
      /// <exception cref="ArgumentNullException">If any of <paramref name="clrType"/>, <paramref name="functionality"/> or <paramref name="databaseData"/> is <c>null</c>.</exception>
      public TypeFunctionalityInformation(
         Type clrType,
         PgSQLTypeFunctionality functionality,
         PgSQLTypeDatabaseData databaseData
         )
      {
         this.CLRType = ArgumentValidator.ValidateNotNull( nameof( clrType ), clrType );
         this.Functionality = ArgumentValidator.ValidateNotNull( nameof( functionality ), functionality );
         this.DatabaseData = ArgumentValidator.ValidateNotNull( nameof( databaseData ), databaseData );
      }

      /// <summary>
      /// Gets the CLR <see cref="Type"/> of this type information.
      /// </summary>
      /// <value>The CLR <see cref="Type"/> of this type information.</value>
      public Type CLRType { get; }

      /// <summary>
      /// Gets the <see cref="PgSQLTypeFunctionality"/> for this type information.
      /// </summary>
      /// <value>The <see cref="PgSQLTypeFunctionality"/> for this type information.</value>
      /// <seealso cref="PgSQLTypeFunctionality"/>
      public PgSQLTypeFunctionality Functionality { get; }

      /// <summary>
      /// Gets the <see cref="PgSQLTypeDatabaseData"/> for this type information.
      /// This data contains the type name in the database, along with type ID (<c>oid</c>).
      /// </summary>
      /// <value>The <see cref="PgSQLTypeDatabaseData"/> for this type information.</value>
      /// <seealso cref="PgSQLTypeDatabaseData"/>
      public PgSQLTypeDatabaseData DatabaseData { get; }
   }

   /// <summary>
   /// This interface contains all the API required by implementation of <see cref="PgSQLConnection"/> to serialize and deserialize values sent by PostgreSQL backend into CLR objects.
   /// Objects implementing this interface are registered to <see cref="TypeRegistry"/> of the single <see cref="PgSQLConnection"/>.
   /// </summary>
   /// <seealso cref="TypeRegistry"/>
   /// <seealso href="https://www.postgresql.org/docs/current/static/protocol.html"/>
   public interface PgSQLTypeFunctionality
   {
      /// <summary>
      /// Gets the value indicating whether this <see cref="PgSQLTypeFunctionality"/> supports reading the binary data format.
      /// </summary><
      /// <value>The value indicating whether this <see cref="PgSQLTypeFunctionality"/> supports reading the binary data format.</value>
      /// <seealso cref="DataFormat"/>
      Boolean SupportsReadingBinaryFormat { get; }

      /// <summary>
      /// Gets the value indicating whether this <see cref="PgSQLTypeFunctionality"/> supports writing the binary data format.
      /// </summary>
      /// <value>The value indicating whether this <see cref="PgSQLTypeFunctionality"/> supports writing the binary data format.</value>
      /// <seealso cref="DataFormat"/>
      Boolean SupportsWritingBinaryFormat { get; }

      /// <summary>
      /// Asynchronously performs deserializing of the value sent by backend into CLR object.
      /// </summary>
      /// <param name="dataFormat">The <see cref="DataFormat"/> the value is being sent by backend.</param>
      /// <param name="boundData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
      /// <param name="stream">The <see cref="StreamReaderWithResizableBufferAndLimitedSize"/> to use to read binary data from.</param>
      /// <returns>Asynchronously returns the CLR object deserialized from <paramref name="stream"/>.</returns>
      ValueTask<Object> ReadBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         );

      /// <summary>
      /// Gets the size of value recognized by this <see cref="PgSQLTypeFunctionality"/>, in bytes.
      /// </summary>
      /// <param name="dataFormat">The <see cref="DataFormat"/> value is being sent to backend.</param>
      /// <param name="boundData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
      /// <param name="value">The value recognized by this <see cref="PgSQLTypeFunctionality"/>.</param>
      /// <param name="isArrayElement">Whether the <paramref name="value"/> is being sent inside SQL array.</param>
      /// <returns>The <see cref="BackendSizeInfo"/> object containing the byte count and optional custom information.</returns>
      /// <seealso cref="DataFormat"/>
      /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <c>null</c>.</exception>
      BackendSizeInfo GetBackendSize(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         Object value,
         Boolean isArrayElement
         );

      /// <summary>
      /// Asynchronously performs serializing of the CLR object into binary data sent to backend.
      /// </summary>
      /// <param name="dataFormat">The <see cref="DataFormat"/> of the data, as expected by backend.</param>
      /// <param name="boundData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
      /// <param name="stream">The <see cref="StreamWriterWithResizableBufferAndLimitedSize"/> to write binary data to.</param>
      /// <param name="value">The CLR object to serialize.</param>
      /// <param name="additionalInfoFromSize">The the <see cref="BackendSizeInfo"/>, as returned by <see cref="GetBackendSize"/> method.</param>
      /// <param name="isArrayElement">Whether <paramref name="value"/> is being sent inside SQL array.</param>
      /// <returns>Asychronously returns after the <paramref name="value"/> has been serialized.</returns>
      /// <seealso cref="DataFormat"/>
      /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <c>null</c>.</exception>
      Task WriteBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamWriterWithResizableBufferAndLimitedSize stream,
         Object value,
         BackendSizeInfo additionalInfoFromSize,
         Boolean isArrayElement
         );

      /// <summary>
      /// Tries to change some object to the type recognized by this <see cref="PgSQLTypeFunctionality"/>.
      /// </summary>
      /// <param name="dbData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="obj">The object to change type to type recognized by this <see cref="PgSQLTypeFunctionality"/>.</param>
      /// <returns>The object of type recognized by this <see cref="PgSQLTypeFunctionality"/>.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is <c>null</c>.</exception>
      /// <exception cref="InvalidCastException">If this <see cref="PgSQLTypeFunctionality"/> does not know how to change type of given <paramref name="obj"/>.</exception>
      Object ChangeTypeFrameworkToPgSQL( PgSQLTypeDatabaseData dbData, Object obj );

      /// <summary>
      /// Changes the object deserialized by <see cref="ReadBackendValueAsync"/> method to another type.
      /// </summary>
      /// <param name="dbData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="obj">The object to change type.</param>
      /// <param name="typeTo">The type to change <paramref name="obj"/> to.</param>
      /// <returns>The object of given type.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is <c>null</c>.</exception>
      /// <exception cref="InvalidCastException">If this <see cref="PgSQLTypeFunctionality"/> does not know how to change type of given <paramref name="obj"/>.</exception>
      Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData dbData, Object obj, Type typeTo );
   }

   /// <summary>
   /// This structure contains information about the size of some object when it is being sent to PostgreSQL backend.
   /// The method <see cref="PgSQLTypeFunctionality.GetBackendSize"/> uses this structure as return type.
   /// </summary>
   public struct BackendSizeInfo
   {
      /// <summary>
      /// Creates a new instance of <see cref="BackendSizeInfo"/> with given parameters.
      /// </summary>
      /// <param name="byteCount">The amount of bytes that the object being serialized will take.</param>
      /// <param name="customInformation">Optional custom information to pass to <see cref="PgSQLTypeFunctionality.WriteBackendValueAsync"/> method.</param>
      public BackendSizeInfo(
         Int32 byteCount,
         Object customInformation = null
         )
      {
         this.ByteCount = byteCount;
         this.CustomInformation = customInformation;
      }

      /// <summary>
      /// Gets the amount of bytes that the object being serialized will take.
      /// </summary>
      /// <value>The amount of bytes that the object being serialized will take.</value>
      public Int32 ByteCount { get; }

      /// <summary>
      /// Gets optional custom information to pass to <see cref="PgSQLTypeFunctionality.WriteBackendValueAsync"/> method.
      /// </summary>
      /// <value>Optional custom information to pass to <see cref="PgSQLTypeFunctionality.WriteBackendValueAsync"/> method.</value>
      public Object CustomInformation { get; }
   }

   /// <summary>
   /// This class contains useful utilities and methods used by <see cref="PgSQLTypeFunctionality"/> objects when they serialize and deserialize CLR objects from data sent to and by backend.
   /// </summary>
   public class BackendABIHelper
   {
      private readonly BinaryStringPool _stringPool;

      /// <summary>
      /// Creates a new instance of <see cref="BackendABIHelper"/> with given parameters.
      /// </summary>
      /// <param name="encoding">The <see cref="IEncodingInfo"/> to use.</param>
      /// <param name="stringPool">The <see cref="BinaryStringPool"/> to use.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="encoding"/> or <paramref name="stringPool"/> is <c>null</c>.</exception>
      public BackendABIHelper(
         IEncodingInfo encoding,
         BinaryStringPool stringPool
         )
      {
         this._stringPool = ArgumentValidator.ValidateNotNull( nameof( stringPool ), stringPool );
         this.CharacterReader = new StreamCharacterReaderLogic( encoding );
         this.CharacterWriter = new StreamCharacterWriterLogic( encoding, 1024 );
      }

      /// <summary>
      /// Gets the <see cref="IEncodingInfo"/> of this connection.
      /// </summary>
      /// <value>The <see cref="IEncodingInfo"/> of this connection.</value>
      public IEncodingInfo Encoding
      {
         get
         {
            return this.CharacterReader.Encoding;
         }
      }

      /// <summary>
      /// Gets the <see cref="StreamCharacterReaderLogic"/> of this connection.
      /// </summary>
      /// <value>The <see cref="StreamCharacterReaderLogic"/> of this connection.</value>
      public StreamCharacterReaderLogic CharacterReader { get; }

      /// <summary>
      /// Gets the <see cref="StreamCharacterWriterLogic"/> of this connection.
      /// </summary>
      /// <value>The <see cref="StreamCharacterWriterLogic"/> of this connection.</value>
      public StreamCharacterWriterLogic CharacterWriter { get; }

      /// <summary>
      /// Gets pooled string or deserializes from given binary data and pools the string.
      /// </summary>
      /// <param name="array">The binary data.</param>
      /// <param name="offset">The offset in <paramref name="array"/> where to start reading data.</param>
      /// <param name="count">The amount of bytes to read in <paramref name="array"/>.</param>
      /// <returns>Pooled or deserialized string.</returns>
      public String GetStringWithPool( Byte[] array, Int32 offset, Int32 count )
      {
         return this._stringPool.GetString( array, offset, count );
      }

   }

   /// <summary>
   /// This enumeration describes the data format used when (de)serializing CLR objects from and to the backend.
   /// </summary>
   /// <seealso href="https://www.postgresql.org/docs/current/static/protocol-overview.html#PROTOCOL-FORMAT-CODES"/>
   public enum DataFormat : short
   {
      /// <summary>
      /// This value signifies that the binary data is in text format.
      /// </summary>
      Text = 0,

      /// <summary>
      /// This value signifies that the binary data is in binary format.
      /// </summary>
      Binary = 1,
   }

   /// <summary>
   /// This is utility class containing some useful and common information when (de)serializing CLR objects from and to the backend.
   /// </summary>
   public abstract class CommonPgSQLTypeFunctionalityInfo
   {
      static CommonPgSQLTypeFunctionalityInfo()
      {
         var format = (System.Globalization.NumberFormatInfo) System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
         format.NumberDecimalDigits = 15;
         NumberFormat = format;
      }

      /// <summary>
      /// Gets the <see cref="System.Globalization.NumberFormatInfo"/> to use when (de)serializing numerical values.
      /// </summary>
      /// <value>The <see cref="System.Globalization.NumberFormatInfo"/> to use when (de)serializing numerical values.</value>
      public static System.Globalization.NumberFormatInfo NumberFormat { get; }

   }

   /// <summary>
   /// This class implements <see cref="PgSQLTypeFunctionality"/> by redirecting all methods to callbacks given to constructor.
   /// </summary>
   /// <typeparam name="TValue">The type supported by this <see cref="DefaultPgSQLTypeFunctionality{TValue}"/>.</typeparam>
   /// <remarks>
   /// Usually, the <see cref="CreateSingleBodyUnboundInfo"/> method is used to create instances of this class.
   /// </remarks>
   public class DefaultPgSQLTypeFunctionality<TValue> : PgSQLTypeFunctionality
   {


      private readonly ReadFromBackend<TValue> _text2CLR;
      private readonly ReadFromBackend<TValue> _binary2CLR;
      private readonly ChangePgSQLToSystem<TValue> _pg2System;
      private readonly ChangeSystemToPgSQL<TValue> _system2PG;
      private readonly CalculateBackendSize<TValue, BackendSizeInfo> _clr2BinarySize;
      private readonly WriteToBackend<TValue> _clr2Binary;
      private readonly CalculateBackendSize<TValue, BackendSizeInfo> _clr2TextSize;
      private readonly WriteToBackend<TValue> _clr2Text;

      /// <summary>
      /// Creates a new instance of <see cref="DefaultPgSQLTypeFunctionality{TValue}"/> with given callbacks.
      /// Note that usually <see cref="CreateSingleBodyUnboundInfo"/> method is used to create new instance, but in case of more complex scenarios when whole data should not be read at once, this constructor may be used.
      /// </summary>
      /// <param name="text2CLR">The callback used by <see cref="ReadBackendValueAsync"/> method when the <see cref="DataFormat"/> is <see cref="DataFormat.Text"/>.</param>
      /// <param name="binary2CLR">The callback used by <see cref="ReadBackendValueAsync"/> method when the <see cref="DataFormat"/> is <see cref="DataFormat.Binary"/>.</param>
      /// <param name="clr2TextSize">The callback used by <see cref="GetBackendSize"/> method when the <see cref="DataFormat"/> is <see cref="DataFormat.Text"/>.</param>
      /// <param name="clr2BinarySize">The callback used by <see cref="GetBackendSize"/> method when the <see cref="DataFormat"/> is <see cref="DataFormat.Binary"/>.</param>
      /// <param name="clr2Text">The callback used by <see cref="WriteBackendValueAsync"/> method when the <see cref="DataFormat"/> is <see cref="DataFormat.Text"/>.</param>
      /// <param name="clr2Binary">The callback used by <see cref="WriteBackendValueAsync"/> method when the <see cref="DataFormat"/> is <see cref="DataFormat.Binary"/>.</param>
      /// <param name="pgSQL2System">The callack used by <see cref="ChangeTypePgSQLToFramework"/> method. If <c>null</c>, <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> will be used instead.</param>
      /// <param name="system2PgSQL">The callback used by <see cref="ChangeTypeFrameworkToPgSQL"/> method. If <c>null</c>, <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> will be used instead.</param>
      public DefaultPgSQLTypeFunctionality(
         ReadFromBackend<TValue> text2CLR,
         ReadFromBackend<TValue> binary2CLR,
         CalculateBackendSize<TValue, BackendSizeInfo> clr2TextSize,
         CalculateBackendSize<TValue, BackendSizeInfo> clr2BinarySize,
         WriteToBackend<TValue> clr2Text,
         WriteToBackend<TValue> clr2Binary,
         ChangePgSQLToSystem<TValue> pgSQL2System,
         ChangeSystemToPgSQL<TValue> system2PgSQL
         )
      {

         this._text2CLR = text2CLR;
         this._binary2CLR = binary2CLR;
         this._pg2System = pgSQL2System;
         this._system2PG = system2PgSQL;
         this._clr2BinarySize = clr2BinarySize;
         this._clr2Binary = clr2Binary;
         this._clr2TextSize = clr2TextSize;
         this._clr2Text = clr2Text;
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.SupportsReadingBinaryFormat"/> by checking whether the appropriate <see cref="ReadFromBackend{TValue}"/> callback given to constructor is not <c>null</c>.
      /// </summary>
      /// <value>Value indicating whether appropriate <see cref="ReadFromBackend{TValue}"/> callback was given to constructor.</value>
      public Boolean SupportsReadingBinaryFormat
      {
         get
         {
            return this._binary2CLR != null;
         }
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.SupportsWritingBinaryFormat"/> by checking whether the appropriate <see cref="CalculateBackendSize{TValue, TResult}"/> and <see cref="WriteToBackend{TValue}"/> callbacks given to constructor are not <c>null</c>.
      /// </summary>
      /// <value>Value indicating whether appropriate <see cref="CalculateBackendSize{TValue, TResult}"/> and <see cref="WriteToBackend{TValue}"/> callbacks were given to constructor.</value>
      public Boolean SupportsWritingBinaryFormat
      {
         get
         {
            return this._clr2BinarySize != null && this._clr2Binary != null;
         }
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.ChangeTypePgSQLToFramework"/> by either calling the <see cref="ChangePgSQLToSystem{TValue}"/> callback given to constructor, or <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.
      /// </summary>
      /// <param name="dbData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="obj">The object to change type.</param>
      /// <param name="typeTo">The type to change <paramref name="obj"/> to.</param>
      /// <returns>The object of given type.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is <c>null</c>.</exception>
      /// <exception cref="InvalidCastException">If this <see cref="PgSQLTypeFunctionality"/> does not know how to change type of given <paramref name="obj"/>.</exception>
      public Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData dbData, Object obj, Type typeTo )
      {
         ArgumentValidator.ValidateNotNull( nameof( obj ), obj );

         return this._pg2System == null ?
            Convert.ChangeType( obj, typeTo, System.Globalization.CultureInfo.InvariantCulture ) :
            this._pg2System( dbData, (TValue) obj, typeTo );
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.ChangeTypeFrameworkToPgSQL"/> by either calling the <see cref="ChangeSystemToPgSQL{TValue}"/> callback given to constructor, or <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.
      /// </summary>
      /// <param name="dbData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="obj">The object to change type to type recognized by this <see cref="PgSQLTypeFunctionality"/>.</param>
      /// <returns>The object of type recognized by this <see cref="PgSQLTypeFunctionality"/>.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is <c>null</c>.</exception>
      /// <exception cref="InvalidCastException">If this <see cref="PgSQLTypeFunctionality"/> does not know how to change type of given <paramref name="obj"/>.</exception>
      public Object ChangeTypeFrameworkToPgSQL( PgSQLTypeDatabaseData dbData, Object obj )
      {
         ArgumentValidator.ValidateNotNull( nameof( obj ), obj );

         return this._system2PG == null ?
            Convert.ChangeType( obj, typeof( TValue ), System.Globalization.CultureInfo.InvariantCulture ) :
            this._system2PG( dbData, obj );
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.GetBackendSize"/> by calling appropriate <see cref="CalculateBackendSize{TValue, TResult}"/> callback given to constructor.
      /// </summary>
      /// <param name="dataFormat">The <see cref="DataFormat"/> value is being sent to backend.</param>
      /// <param name="boundData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
      /// <param name="value">The value recognized by this <see cref="PgSQLTypeFunctionality"/>.</param>
      /// <param name="isArrayElement">Whether the <paramref name="value"/> is being sent inside SQL array.</param>
      /// <returns>The <see cref="BackendSizeInfo"/> object containing the byte count and optional custom information.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <c>null</c>.</exception>
      /// <exception cref="InvalidCastException">If <paramref name="value"/> is not of type <typeparamref name="TValue"/>.</exception>
      /// <exception cref="NotSupportedException">If given <paramref name="dataFormat"/> is not supported - either because required callback given to constructor was <c>null</c>, or because <paramref name="dataFormat"/> is something else than one of <see cref="DataFormat.Text"/> or <see cref="DataFormat.Binary"/>.</exception>
      public BackendSizeInfo GetBackendSize( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper args, Object value, Boolean isArrayElement )
      {
         ArgumentValidator.ValidateNotNull( nameof( value ), value );
         switch ( dataFormat )
         {
            case DataFormat.Text:
               return CheckDelegate( this._clr2TextSize, dataFormat )( boundData, args.Encoding, (TValue) value, isArrayElement );
            case DataFormat.Binary:
               return CheckDelegate( this._clr2BinarySize, dataFormat )( boundData, args.Encoding, (TValue) value, isArrayElement );
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.WriteBackendValueAsync"/> by calling appropriate <see cref="WriteToBackend{TValue}"/> callback given to constructor.
      /// </summary>
      /// <param name="dataFormat">The <see cref="DataFormat"/> of the data, as expected by backend.</param>
      /// <param name="boundData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
      /// <param name="stream">The <see cref="StreamWriterWithResizableBufferAndLimitedSize"/> to write binary data to.</param>
      /// <param name="value">The CLR object to serialize.</param>
      /// <param name="additionalInfoFromSize">The the <see cref="BackendSizeInfo"/>, as returned by <see cref="GetBackendSize"/> method.</param>
      /// <param name="isArrayElement">Whether <paramref name="value"/> is being sent inside SQL array.</param>
      /// <returns>Asychronously returns after the <paramref name="value"/> has been serialized.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <c>null</c>.</exception>
      /// <exception cref="InvalidCastException">If <paramref name="value"/> is not of type <typeparamref name="TValue"/>.</exception>
      /// <exception cref="NotSupportedException">If given <paramref name="dataFormat"/> is not supported - either because required callback given to constructor was <c>null</c>, or because <paramref name="dataFormat"/> is something else than one of <see cref="DataFormat.Text"/> or <see cref="DataFormat.Binary"/>.</exception>
      public Task WriteBackendValueAsync( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, Object value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement )
      {
         ArgumentValidator.ValidateNotNull( nameof( value ), value );
         switch ( dataFormat )
         {
            case DataFormat.Text:
               return CheckDelegate( this._clr2Text, dataFormat )( boundData, args, stream, (TValue) value, additionalInfoFromSize, isArrayElement );
            case DataFormat.Binary:
               return CheckDelegate( this._clr2Binary, dataFormat )( boundData, args, stream, (TValue) value, additionalInfoFromSize, isArrayElement );
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      /// <summary>
      /// Implements <see cref="PgSQLTypeFunctionality.ReadBackendValueAsync"/> by calling appropriate <see cref="ReadFromBackend{TValue}"/> callback given to constructor.
      /// </summary>
      /// <param name="dataFormat">The <see cref="DataFormat"/> the value is being sent by backend.</param>
      /// <param name="boundData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
      /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
      /// <param name="stream">The <see cref="StreamReaderWithResizableBufferAndLimitedSize"/> to use to read binary data from.</param>
      /// <returns>Asynchronously returns the CLR object deserialized from <paramref name="stream"/>.</returns>
      /// <exception cref="NotSupportedException">If given <paramref name="dataFormat"/> is not supported - either because required callback given to constructor was <c>null</c>, or because <paramref name="dataFormat"/> is something else than one of <see cref="DataFormat.Text"/> or <see cref="DataFormat.Binary"/>.</exception>
      public async ValueTask<Object> ReadBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper args,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         switch ( dataFormat )
         {
            case DataFormat.Binary:
               return await CheckDelegate( this._binary2CLR, dataFormat )( boundData, args, stream );
            case DataFormat.Text:
               return await CheckDelegate( this._text2CLR, dataFormat )( boundData, args, stream );
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      private static T CheckDelegate<T>( T del, DataFormat dataFormat )
         where T : class
      {
         if ( del == null )
         {
            throw new NotSupportedException( $"The data format {dataFormat} is not supported." );
         }
         return del;
      }

      /// <summary>
      /// Creates a new instance of <see cref="DefaultPgSQLTypeFunctionality{TValue}"/> which will read and write whole data at once in its <see cref="ReadFromBackend{TValue}"/> and <see cref="WriteToBackend{TValue}"/> callbacks, respectively, and then call given <see cref="ReadFromBackendSync{TValue}"/> and <see cref="WriteToBackendSync{TValue}"/> callbacks, respectively.
      /// </summary>
      /// <param name="text2CLR">Synchronous <see cref="ReadFromBackendSync{TValue}"/> callback to deserialize textual data into CLR object.</param>
      /// <param name="binary2CLR">Synchronous <see cref="ReadFromBackendSync{TValue}"/> callback to deserialize binary data into CLR object.</param>
      /// <param name="clr2TextSize">The <see cref="CalculateBackendSize{TValue, TResult}"/> callback to calculate byte count of CLR object serialized to textual data, or just to return <see cref="String"/> right away if it is more feasible.</param>
      /// <param name="clr2BinarySize">The <see cref="CalculateBackendSize{TValue, TResult}"/> callback to calculate byte count of CLR object serialized to textual data.</param>
      /// <param name="clr2Text">Synchronous <see cref="WriteToBackendSync{TValue, TResult}"/> callback to serialize CLR object into textual data.</param>
      /// <param name="clr2Binary">Synchronous <see cref="WriteToBackendSync{TValue, TSizeInfo}"/> callback to serialize CLR object into binary data.</param>
      /// <param name="pgSQL2System">Callback to convert object of type <typeparamref name="TValue"/> into given type.</param>
      /// <param name="system2PgSQL">Callback to convert object into <typeparamref name="TValue"/>.</param>
      /// <returns>A new instance of <see cref="DefaultPgSQLTypeFunctionality{TValue}"/> which uses given callbacks to implement <see cref="PgSQLTypeFunctionality"/>.</returns>
      /// <exception cref="ArgumentNullException">If <paramref name="text2CLR"/> is <c>null</c></exception>
      public static DefaultPgSQLTypeFunctionality<TValue> CreateSingleBodyUnboundInfo(
         ReadFromBackendSync<TValue> text2CLR,
         ReadFromBackendSync<TValue> binary2CLR,
         CalculateBackendSize<TValue, EitherOr<Int32, String>> clr2TextSize,
         CalculateBackendSize<TValue, Int32> clr2BinarySize,
         WriteToBackendSync<TValue, TSyncTextualSizeInfo> clr2Text,
         WriteToBackendSync<TValue, Int32> clr2Binary,
         ChangePgSQLToSystem<TValue> pgSQL2System,
         ChangeSystemToPgSQL<TValue> system2PgSQL
         )
      {
         ArgumentValidator.ValidateNotNull( nameof( text2CLR ), text2CLR );

         CalculateBackendSize<TValue, BackendSizeInfo> textSizeActual;
         if ( clr2TextSize == null )
         {
            if ( typeof( IFormattable ).GetTypeInfo().IsAssignableFrom( typeof( TValue ).GetTypeInfo() ) )
            {
               textSizeActual = ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) =>
               {
                  var str = ( (IFormattable) value ).ToString( null, CommonPgSQLTypeFunctionalityInfo.NumberFormat );
                  return new BackendSizeInfo( encoding.Encoding.GetByteCount( str ), str );
               };
            }
            else
            {
               textSizeActual = ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) =>
               {
                  var str = value.ToString();
                  return new BackendSizeInfo( encoding.Encoding.GetByteCount( str ), str );
               };
            }
         }
         else
         {
            textSizeActual = ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) =>
            {
               var thisTextSize = clr2TextSize( boundData, encoding, value, isArrayElement );
               return thisTextSize.IsFirst ? new BackendSizeInfo( thisTextSize.First ) : new BackendSizeInfo( encoding.Encoding.GetByteCount( thisTextSize.Second ), thisTextSize.Second );
            };
         }

         WriteToBackendSync<TValue, TSyncTextualSizeInfo> clr2TextActual;
         if ( clr2Text == null )
         {
            clr2TextActual = ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, TValue value, TSyncTextualSizeInfo additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               var str = additionalInfoFromSize.Item2;
               args.Encoding.Encoding.GetBytes( str, 0, str.Length, array, offset );
            };
         }
         else
         {
            clr2TextActual = clr2Text;
         }

         return new DefaultPgSQLTypeFunctionality<TValue>(
            async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamReaderWithResizableBufferAndLimitedSize stream ) =>
            {
               if ( stream != null )
               {
                  await stream.ReadAllBytesToBuffer();
               }
               return text2CLR( boundData, args, stream.Buffer, 0, (Int32) stream.TotalByteCount );
            },
            binary2CLR == null ? (ReadFromBackend<TValue>) null : async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamReaderWithResizableBufferAndLimitedSize stream ) =>
             {
                if ( stream != null )
                {
                   await stream.ReadAllBytesToBuffer();
                }

                return binary2CLR( boundData, args, stream.Buffer, 0, (Int32) stream.TotalByteCount );
             },
            textSizeActual,
            clr2BinarySize == null ? (CalculateBackendSize<TValue, BackendSizeInfo>) null : ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) => new BackendSizeInfo( clr2BinarySize( boundData, encoding, value, isArrayElement ) ),
            async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               (var offset, var count) = stream.ReserveBufferSegment( additionalInfoFromSize.ByteCount );
               clr2TextActual( boundData, args, stream.Buffer, offset, value, (additionalInfoFromSize.ByteCount, (String) additionalInfoFromSize.CustomInformation), isArrayElement );
               await stream.FlushAsync();
            },
            clr2Binary == null ? (WriteToBackend<TValue>) null : async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               (var offset, var count) = stream.ReserveBufferSegment( additionalInfoFromSize.ByteCount );
               clr2Binary( boundData, args, stream.Buffer, offset, value, additionalInfoFromSize.ByteCount, isArrayElement );
               await stream.FlushAsync();
            },
            pgSQL2System,
            system2PgSQL
            );
      }
   }

   /// <summary>
   /// This callback is used by <see cref="DefaultPgSQLTypeFunctionality{TValue}"/> in its <see cref="DefaultPgSQLTypeFunctionality{TValue}.ReadBackendValueAsync(DataFormat, PgSQLTypeDatabaseData, BackendABIHelper, StreamReaderWithResizableBufferAndLimitedSize)"/> method.
   /// </summary>
   /// <typeparam name="TValue">The type of the value understood by <see cref="DefaultPgSQLTypeFunctionality{TValue}"/>.</typeparam>
   /// <param name="dbData">The <see cref="PgSQLTypeDatabaseData"/> containing information about this type, specific to the database the <see cref="PgSQLConnection"/> is connected to.</param>
   /// <param name="helper">The <see cref="BackendABIHelper"/> application binary interface helper.</param>
   /// <param name="stream">The <see cref="StreamReaderWithResizableBufferAndLimitedSize"/> to use to read binary data from.</param>
   /// <returns>Potentially asynchronously returns deserialized value from <paramref name=""/></returns>
   public delegate ValueTask<TValue> ReadFromBackend<TValue>( PgSQLTypeDatabaseData dbData, BackendABIHelper helper, StreamReaderWithResizableBufferAndLimitedSize stream );
   public delegate Object ChangePgSQLToSystem<TValue>( PgSQLTypeDatabaseData dbData, TValue pgSQLObject, Type targetType );
   public delegate TValue ChangeSystemToPgSQL<TValue>( PgSQLTypeDatabaseData dbData, Object systemObject );
   public delegate TResult CalculateBackendSize<TValue, TResult>( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement );
   public delegate Task WriteToBackend<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement );

   public delegate TValue ReadFromBackendSync<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count );
   public delegate void WriteToBackendSync<TValue, TSizeInfo>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, TValue value, TSizeInfo additionalInfoFromSize, Boolean isArrayElement );

   public sealed class PgSQLTypeDatabaseData
   {
      public PgSQLTypeDatabaseData(
         String typeName,
         Int32 typeID,
         String arrayDelimiter,
         Int32 elementTypeID
         )
      {
         this.TypeName = typeName;
         this.TypeID = typeID;
         this.ArrayDelimiter = arrayDelimiter;
         this.ElementTypeID = elementTypeID;
      }

      public String TypeName { get; }
      public Int32 TypeID { get; }
      public Int32 ElementTypeID { get; }
      public String ArrayDelimiter { get; } // String because we might get surrogate pairs here...
   }

   public static partial class CBAMExtensions
   {

      public static Byte[] WritePgInt32( this Byte[] array, ref Int32 idx, Int32 value )
      {
         array.WriteInt32BEToBytes( ref idx, value );
         return array;
      }

      public static Int32 ReadPgInt32( this Byte[] array, ref Int32 idx )
      {
         return array.ReadInt32BEFromBytes( ref idx );
      }

      public static Int16 ReadPgInt16( this Byte[] array, ref Int32 idx )
      {
         return array.ReadInt16BEFromBytes( ref idx );
      }

      public static Int32 ReadPgInt16Count( this Byte[] array, ref Int32 idx )
      {
         return array.ReadUInt16BEFromBytes( ref idx );
      }

      // TODO move to utilpack
      public static Int32 CountOccurrances( this String str, String substring, StringComparison comparison )
      {
         var count = 0;

         if ( substring != null && substring.Length > 0 )
         {
            var idx = 0;
            while ( ( idx = str.IndexOf( substring, idx, comparison ) ) != -1 )
            {
               idx += substring.Length;
               ++count;
            }
         }

         return count;
      }
   }
}

public static partial class E_CBAM
{
   private const Int32 NULL_BYTE_COUNT = -1;

   public static async ValueTask<(Object Value, Int32 BytesReadFromStream)> ReadBackendValueCheckNull(
      this PgSQLTypeFunctionality typeFunctionality,
      DataFormat dataFormat,
      PgSQLTypeDatabaseData boundData,
      BackendABIHelper helper,
      StreamReaderWithResizableBufferAndLimitedSize stream
      )
   {
      await stream.ReadOrThrow( sizeof( Int32 ) );
      var byteCount = 0;
      var length = stream.Buffer.ReadPgInt32( ref byteCount );
      Object retVal;
      if ( length >= 0 )
      {
         byteCount += length;
         using ( var limitedStream = stream.CreateWithLimitedSizeAndSharedBuffer( length ) )
         {
            try
            {
               retVal = await typeFunctionality.ReadBackendValueAsync(
                  dataFormat,
                  boundData,
                  helper,
                  limitedStream
                  );
            }
            finally
            {
               try
               {
                  await limitedStream.SkipThroughRemainingBytes();
               }
               catch
               {
                  // Ignore
               }
            }
         }
      }
      else
      {
         retVal = null;
      }

      return (retVal, byteCount);
   }

   public static BackendSizeInfo GetBackendTextSizeCheckNull( this PgSQLTypeFunctionality typeFunctionality, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement )
   {
      return value == null ? new BackendSizeInfo( NULL_BYTE_COUNT ) : typeFunctionality.GetBackendSize( DataFormat.Text, boundData, helper, value, isArrayElement );
   }

   public static BackendSizeInfo GetBackendBinarySizeCheckNull( this PgSQLTypeFunctionality typeFunctionality, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement )
   {
      return value == null ? new BackendSizeInfo( NULL_BYTE_COUNT ) : typeFunctionality.GetBackendSize( DataFormat.Binary, boundData, helper, value, isArrayElement );
   }

   public static async Task WriteBackendValueCheckNull(
      this PgSQLTypeFunctionality typeFunctionality,
      DataFormat dataFormat,
      PgSQLTypeDatabaseData boundData,
      BackendABIHelper helper,
      StreamWriterWithResizableBufferAndLimitedSize stream,
      Object value,
      BackendSizeInfo additionalInfoFromSize,
      Boolean isArrayElement
      )
   {
      (var offset, var count) = stream.ReserveBufferSegment( sizeof( Int32 ) );
      stream.Buffer.WritePgInt32( ref offset, value == null ? NULL_BYTE_COUNT : additionalInfoFromSize.ByteCount );
      if ( value != null && additionalInfoFromSize.ByteCount > 0 )
      {
         await typeFunctionality.WriteBackendValueAsync( dataFormat, boundData, helper, stream, value, additionalInfoFromSize, isArrayElement );
      }
      await stream.FlushAsync();
   }
}

