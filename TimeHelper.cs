using System;
using System.Text;

namespace ScmNotifier
{
    public static class TimeHelper
    {
        #region Methods
        
        #region Public
        
        public static string FromTimeSpan(TimeSpan ts)
        {
            var result = new StringBuilder();

            Action<int, string> appendPart = (part, str) =>
                {
                    if (part > 0)
                    {
                        result.AppendFormat(" {0} {1}{2}", part, str, part > 1 ? "s" : string.Empty);
                    }
                };

            appendPart(ts.Days, "day");
            appendPart(ts.Hours, "hour");
            appendPart(ts.Minutes, "min");
            appendPart(ts.Seconds, "sec");

            return result.ToString().Trim();
        }             
        
        #endregion
        
        #endregion
    }
}
