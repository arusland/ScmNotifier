using System;
using System.Collections.Generic;

namespace ScmNotifier
{
    public static class ObjectExtensions
    {
        #region Methods

        #region Public

        public static T[] AsArray<T>(this T value)
        {
            return new[] { value };
        }

        public static IList<T> AsList<T>(this T value)
        {
            return new List<T> { value };
        }

        public static Version GetAssemblyVersion(this object value)
        {
            return value.GetType().Assembly.GetName().Version;
        }

        /// <summary>
        /// Safe method calling.
        /// </summary>
        /// <param name="value"> </param>
        /// <param name="handler">Called if value is null.</param>
        /// <returns>true if method called; otherwise false.</returns>
        public static bool IfNotPresent<T>(this T value, Action<T> handler)
        {
            if (value.IsNull())
            {
                handler(value);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Safe cast with null handling.
        /// </summary>
        /// <typeparam name="T">Cast type.</typeparam>
        /// <param name="value"> </param>
        /// <param name="defHandler">Called if valye is null.</param>
        /// <returns>Object value of type T.</returns>
        public static T IfNull<T>(this object value, Func<T> defHandler)
        {
            if (value.IsNull())
            {
                return defHandler();
            }

            return (T)value;
        }

        /// <summary>
        /// Safe method calling with result return.
        /// </summary>
        /// <typeparam name="T">Return result type.</typeparam>
        /// <typeparam name="TResult"> </typeparam>
        /// <param name="value"> </param>
        /// <param name="handler">Called if value is not null.</param>
        /// <returns>result by handler if value not null; null (or default) otherwise.</returns>
        public static TResult IfPresent<T, TResult>(this T value, Func<T, TResult> handler)
        {

            if (value.IsNotNull())
            {
                return handler(value);
            }

            return default(TResult);
        }

        /// <summary>
        /// Safe method calling with result return.
        /// </summary>
        /// <typeparam name="T">Return result type.</typeparam>
        /// <typeparam name="TResult"> </typeparam>
        /// <param name="value"> </param>
        /// <param name="handler">Called if value is not null.</param>
        /// <param name="nullHandler">Called if value is null.</param>
        /// <returns>result by handler if value not null; null (or default) otherwise.</returns>
        public static TResult IfPresent<T, TResult>(this T value, Func<T, TResult> handler, Func<TResult> nullHandler)
        {
            if (value.IsNotNull())
            {
                return handler(value);
            }

            return nullHandler();
        }

        /// <summary>
        /// Safe method calling.
        /// </summary>
        /// <param name="value"> </param>
        /// <param name="handler">Called if value is not null.</param>
        /// <returns>true if method called; otherwise false.</returns>
        public static bool IfPresent<T>(this T value, Action<T> handler)
        {
            if (value.IsNotNull())
            {
                handler(value);

                return true;
            }

            return false;
        }

        public static bool IsNotNull(this object value)
        {
            return value != null;
        }

        public static bool IsNotTypeOf(this object value, Type type)
        {
            return !value.IsTypeOf(type);
        }

        public static bool IsNull(this object value)
        {
            return value == null || value is DBNull;
        }

        public static bool IsTypeOf(this object value, Type type)
        {
            if (value.IsNull())
            {
                throw new ArgumentNullException("value");
            }
            if (type.IsNull())
            {
                throw new ArgumentNullException("type");
            }

            return type.IsInstanceOfType(value);
        }

        /// <summary>
        /// Safe type converting. Object cannot be null.
        /// </summary>
        /// <returns>Object of type T</returns>
        public static T To<T>(this object value)
        {
            if (value.IsNull())
            {
                throw new ArgumentNullException("value");
            }

            if (value is T)
            {
                return (T)value;
            }

            throw new InvalidOperationException(String.Format("value is not of type {0}, but of type {1}", typeof(T), value.GetType()));
        }


        /// <summary>
        /// Safe type converting with nulls allowing.
        /// </summary>
        /// <returns>Object of type T</returns>
        public static T ToWithNull<T>(this object value)
        {
            if (value.IsNull())
            {
                return default(T);
            }

            if (value is T)
            {
                return (T)value;
            }

            throw new InvalidOperationException(String.Format("value is not of type {0}, but of type {1}", typeof(T), value.GetType()));
        }

        #endregion

        #endregion
    }
}
