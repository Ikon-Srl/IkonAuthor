// file     : ObjectExtension.cs
// project  : UniversalTypeConverter
// author   : Thorsten Bruning
// date     : 28.07.2011

using System;
using System.Globalization;

namespace Ikon.ComponentModel {

    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class ObjectExtension {

        #region [ CanConvertTo<T> ]
        /// <summary>
        /// Determines whether the value can be converted to the specified type using the current CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <typeparam name="T">The Type to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <typeparamref name="T"/>; otherwise, false.</returns>
        public static bool CanConvertTo<T>(this object value) {
            return UniversalTypeConverter.CanConvertTo<T>(value);
        }

        /// <summary>
        /// Determines whether the value can be converted to the specified type using the given CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <typeparam name="T">The Type to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <typeparamref name="T"/>; otherwise, false.</returns>
        public static bool CanConvertTo<T>(this object value, CultureInfo culture) {
            return UniversalTypeConverter.CanConvertTo<T>(value, culture);
        }

        /// <summary>
        /// Determines whether the value can be converted to the specified type using the current CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <typeparam name="T">The Type to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <typeparamref name="T"/>; otherwise, false.</returns>
        public static bool CanConvertTo<T>(this object value, ConversionOptions options) {
            return UniversalTypeConverter.CanConvertTo<T>(value, options);
        }

        /// <summary>
        /// Determines whether the value can be converted to the specified type using the given CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <typeparam name="T">The Type to test.</typeparam>
        /// <param name="value">The value to test.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <typeparamref name="T"/>; otherwise, false.</returns>
        public static bool CanConvertTo<T>(this object value, CultureInfo culture, ConversionOptions options) {
            return UniversalTypeConverter.CanConvertTo<T>(value, culture, options);
        }
        #endregion

        #region [ TryConvertTo<T> ]
        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="result">An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvertTo<T>(this object value, out T result) {
            return UniversalTypeConverter.TryConvertTo(value, out result);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="result">An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvertTo<T>(this object value, out T result, CultureInfo culture) {
            return UniversalTypeConverter.TryConvertTo(value, out result, culture);
        }

        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="result">An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvertTo<T>(this object value, out T result, ConversionOptions options) {
            return UniversalTypeConverter.TryConvertTo(value, out result, options);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="result">An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvertTo<T>(this object value, out T result, CultureInfo culture, ConversionOptions options) {
            return UniversalTypeConverter.TryConvertTo(value, out result, culture, options);
        }
        #endregion

        #region [ ConvertTo<T> ]
        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <returns>An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static T ConvertTo<T>(this object value) {
            return UniversalTypeConverter.ConvertTo<T>(value);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <returns>An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static T ConvertTo<T>(this object value, CultureInfo culture) {
            return UniversalTypeConverter.ConvertTo<T>(value, culture);
        }

        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static T ConvertTo<T>(this object value, ConversionOptions options) {
            return UniversalTypeConverter.ConvertTo<T>(value, options);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <typeparam name="T">The Type to which the given value is converted.</typeparam>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>An Object instance of type <typeparamref name="T">T</typeparamref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static T ConvertTo<T>(this object value, CultureInfo culture, ConversionOptions options) {
            return UniversalTypeConverter.ConvertTo<T>(value, culture, options);
        }
        #endregion

        #region [ CanConvert ]
        /// <summary>
        /// Determines whether the value can be converted to the specified type using the current CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="destinationType">The Type to test.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <paramref name="destinationType"/>; otherwise, false.</returns>
        public static bool CanConvert(this object value, Type destinationType) {
            return UniversalTypeConverter.CanConvert(value, destinationType);
        }

        /// <summary>
        /// Determines whether the value can be converted to the specified type using the given CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="destinationType">The Type to test.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <paramref name="destinationType"/>; otherwise, false.</returns>
        public static bool CanConvert(this object value, Type destinationType, CultureInfo culture) {
            return UniversalTypeConverter.CanConvert(value, destinationType, culture);
        }

        /// <summary>
        /// Determines whether the value can be converted to the specified type using the current CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="destinationType">The Type to test.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <paramref name="destinationType"/>; otherwise, false.</returns>
        public static bool CanConvert(this object value, Type destinationType, ConversionOptions options) {
            return UniversalTypeConverter.CanConvert(value, destinationType, options);
        }

        /// <summary>
        /// Determines whether the value can be converted to the specified type using the given CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="destinationType">The Type to test.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> can be converted to <paramref name="destinationType"/>; otherwise, false.</returns>
        public static bool CanConvert(this object value, Type destinationType, CultureInfo culture, ConversionOptions options) {
            return UniversalTypeConverter.CanConvert(value, destinationType, culture, options);
        }
        #endregion

        #region [ TryConvert ]
        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="result">An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvert(this object value, Type destinationType, out object result) {
            return UniversalTypeConverter.TryConvert(value, destinationType, out result);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="result">An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvert(this object value, Type destinationType, out object result, CultureInfo culture) {
            return UniversalTypeConverter.TryConvert(value, destinationType, out result, culture);
        }

        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="result">An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvert(this object value, Type destinationType, out object result, ConversionOptions options) {
            return UniversalTypeConverter.TryConvert(value, destinationType, out result, options);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="result">An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref> if the operation succeeded.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryConvert(this object value, Type destinationType, out object result, CultureInfo culture, ConversionOptions options) {
            return UniversalTypeConverter.TryConvert(value, destinationType, out result, culture, options);
        }
        #endregion

        #region [ Convert ]
        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <returns>An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static object Convert(this object value, Type destinationType) {
            return UniversalTypeConverter.Convert(value, destinationType);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the <see cref="ConversionOptions">ConversionOptions</see>.<see cref="ConversionOptions.EnhancedTypicalValues">ConvertSpecialValues</see>.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <returns>An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static object Convert(this object value, Type destinationType, CultureInfo culture) {
            return UniversalTypeConverter.Convert(value, destinationType, culture);
        }

        /// <summary>
        /// Converts the value to the given Type using the current CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static object Convert(this object value, Type destinationType, ConversionOptions options) {
            return UniversalTypeConverter.Convert(value, destinationType, options);
        }

        /// <summary>
        /// Converts the value to the given Type using the given CultureInfo and the given <see cref="ConversionOptions">ConversionOptions</see>.
        /// </summary>
        /// <param name="value">The value wich is converted.</param>
        /// <param name="destinationType">The Type to which the given value is converted.</param>
        /// <param name="culture">The CultureInfo to use as the current culture.</param>
        /// <param name="options">The options wich are used for conversion.</param>
        /// <returns>An Object instance of type <paramref name="destinationType">destinationType</paramref> whose value is equivalent to the given <paramref name="value">value</paramref>.</returns>
        public static object Convert(this object value, Type destinationType, CultureInfo culture, ConversionOptions options) {
            return UniversalTypeConverter.Convert(value, destinationType, culture, options);
        }
        #endregion

    }
}
