using System.Windows;

namespace ScmNotifier
{
    public partial class App : Application
    {
        #region Ctors
        
        public App()
        {
            if (!StartUpHelper.IsStartUp)
            {
                StartUpHelper.IsStartUp = true;
            }
        }
        
        #endregion

    }
}
