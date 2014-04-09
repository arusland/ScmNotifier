using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ScmNotifier
{
    public static class StringExtensions
    {
        #region Methods

        #region Public

        public static bool Compare(this string str, string value, bool ignoreCase)
        {
            return String.Compare(str, value, ignoreCase) == 0;
        }

        public static string GetShort(this string str, int length = 7)
        {
            if (str == null)
            {
                throw new ArgumentNullException("str");
            }

            var result = str;
            if (str.Length > length)
            {
                result = String.Format("{0}...", str.Substring(0, 7));
            }

            return result;
        }

        public static bool IsAlfaDigit(this string value)
        {
            var res = true;

            if (value.IsNotNullOrEmpty())
            {
                res = Regex.IsMatch(value, "^\\w*$");
            }

            return res;
        }

        public static bool IsEmpty(this string str)
        {
            if (str == null)
            {
                throw new ArgumentNullException("str");
            }

            return str == string.Empty;
        }

        public static bool IsNotEmpty(this string str)
        {
            return !str.IsEmpty();
        }

        public static bool IsNotNullOrEmpty(this string str)
        {
            return str != null && str.IsNotEmpty();
        }

        public static bool IsNotNullOrWhiteSpace(this string str)
        {
            return str != null && str.Trim().IsNotEmpty();
        }

        public static bool IsNullOrEmpty(this string str)
        {
            return !str.IsNotNullOrEmpty();
        }

        public static T ParseEnum<T>(this string value)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(String.Format("'{0}' cannot be parsed as {1}", value, typeof(T)), ex);
            }
        }

        public static string[] Split(this string value, string separator)
        {
            if (value.Any())
            {
                return value.Split(new[] { separator }, StringSplitOptions.None);
            }

            return new string[] { };
        }

        public static string[] SplitNotEmpty(this string value, char separator)
        {
            return value.Split(separator).Select(p => p.Trim()).Where(p => p.IsNotNullOrEmpty()).ToArray();
        }

        public static string ToNullIfEmpty(this string value)
        {
            return value.IsNullOrEmpty() ? null : value;
        }

        public static string Upper(this string value)
        {
            return value.ToUpper(CultureInfo.CurrentCulture);
        }

        public static string UpperFirstChar(this string value)
        {
            if (value.Any())
            {
                return value[0].ToString().ToUpper(CultureInfo.CurrentCulture) + value.Substring(1, value.Length - 1);
            }

            return string.Empty;
        }

        #endregion

        #endregion
    }
}