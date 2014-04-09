using System.IO;
using System.Windows.Forms;

namespace ScmNotifier
{
    public static class StartUpHelper
    {
        #region Constants

        private const string REG_Path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        
        #endregion

        #region Properties
        
        #region Public

        public static bool IsStartUp
        {
            get
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_Path, true);
                string appName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);

                string value = (string)key.GetValue(appName);

                return value != null ? string.Compare(appName, Application.ExecutablePath, true) == 0 : false;
            }
            set
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_Path, true);
                string appName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);

                if (value)
                {
                    key.SetValue(appName, Application.ExecutablePath.ToString());
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
        }
        
        #endregion

        #endregion
    }
}
