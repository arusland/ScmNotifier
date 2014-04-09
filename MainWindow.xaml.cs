using ScmNotifier.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScmNotifier
{
    public partial class MainWindow : Window
    {
        #region Fields

        private GitManager _GitManager;
        private System.Windows.Forms.ContextMenuStrip _NotifyContextMenu;
        private System.Windows.Forms.NotifyIcon _NotifyIcon;
        private bool _CanClose;
        private readonly Brush _NormalBrush;
        private string _LastErrorText;

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            _NormalBrush = new SolidColorBrush(Color.FromRgb(50, 45, 228));
            var version = this.GetType().Assembly.GetName().Version;
            Title = String.Format("Scm Notifier - {0}.{1}", version.Major, version.Minor);

            _GitManager = new GitManager(GetPathesFromSetting(), Settings.Default.UpdateTimeInSeconds);
            _GitManager.OnNewLogItems += GitManager_OnNewLogItems;
            _GitManager.Start();

            // menu
            _NotifyContextMenu = new System.Windows.Forms.ContextMenuStrip();
            var menuReloadSvn = new System.Windows.Forms.ToolStripMenuItem("Reload");
            _NotifyContextMenu.Items.Add(menuReloadSvn);
            menuReloadSvn.Click += menuReloadGit_Click;
            _NotifyContextMenu.Items.Add("-");
            var menuExit = new System.Windows.Forms.ToolStripMenuItem("E&xit");
            menuExit.Click += OnMenuExitClicked;
            _NotifyContextMenu.Items.Add(menuExit);

            // motifyIcon
            _NotifyIcon = new System.Windows.Forms.NotifyIcon();
            _NotifyIcon.MouseClick += notifyIcon_MouseClick;
            _NotifyIcon.BalloonTipClicked += notifyIcon_BalloonTipClicked;
            _NotifyIcon.Icon = Properties.Resources.git_blue;
            _NotifyIcon.Visible = true;
            _NotifyIcon.ContextMenuStrip = _NotifyContextMenu;

            ClearLog();
        }

        #region Methods

        #region Private

        private void AddLogHeader()
        {
            AddLogItem("Time", "Local branch", "Commit", "Remote branch", Brushes.Black, "Project", "Subject", "Commiter");
        }

        private void ClearLog()
        {
            LogGrid.RowDefinitions.Clear();
            LogGrid.Children.Clear();
            _NotifyIcon.Text = Title;
            AddLogHeader();
        }

        private void ShowWindow()
        {
            Show();
            Activate();
        }

        private static IEnumerable<string> GetPathesFromSetting()
        {
            List<string> result = new List<string>();
            string[] splited = Settings.Default.GitPathes.Split(new char[] { ';' });

            result = splited.Where(p => !string.IsNullOrEmpty(p)).ToList();

            for (int i = 0; i < result.Count; i++)
            {
                var st = result[i];

                string envVar;
                if (ContainsEnvVariable(st, out envVar))
                {
                    var envVarVal = Environment.GetEnvironmentVariable(envVar.Substring(1, envVar.Length - 2));

                    if (envVarVal.IsNotNullOrEmpty())
                    {
                        st = st.Replace(envVar, envVarVal);
                    }
                }

                yield return st;
            }
        }

        private static bool ContainsEnvVariable(string st, out string envVar)
        {
            bool res = false;
            envVar = null;
            // not very good pattern - allows to have several % in the string
            // so the result should be checked later
            Regex r = new Regex(@"%.+%");

            var m = r.Match(st);

            if (m.Success && m.Value.Count(x => x == '%') == 2)
            {
                res = true;

                envVar = m.Value;
            }

            return res;
        }

        private string DateToString(DateTime time)
        {
            return String.Format("{0} ({1})", time.ToString(), TimeHelper.FromTimeSpan(DateTime.Now.Subtract(time)));
        }

        private void AddItemToLog(GitManager.NewLogItemEventArgs result)
        {
            foreach (var error in result.Errors)
            {
                var rowDef = new RowDefinition();
                LogGrid.RowDefinitions.Add(rowDef);

                var labelDate = new Label();
                labelDate.Foreground = Brushes.Red;
                labelDate.Content = DateTime.Now.ToString();
                LogGrid.Children.Add(labelDate);

                var labelError = new Label();
                labelError.Foreground = Brushes.Red;
                labelError.Content = error.Message;
                LogGrid.Children.Add(labelError);

                var rowIndex = LogGrid.RowDefinitions.Count - 1;
                Grid.SetRow(labelDate, rowIndex);
                Grid.SetRow(labelError, rowIndex);
                Grid.SetColumn(labelError, 1);
                Grid.SetColumnSpan(labelError, 3);
            }

            foreach (var item in result.Items)
            {
                if (item.Commit.IsShort)
                {
                    AddLogItem(DateTime.Now.ToString(), item.Branch.Name, item.Commit.Hash,
                        String.Format("{0}/{1}", item.Branch.Remote.Remote.Name, item.Branch.Remote.Name), _NormalBrush, item.ProjectName);
                }
                else
                {
                    AddLogItem(DateTime.Now.ToString(), item.Branch.Name, item.Commit.Hash,
                    String.Format("{0}/{1}", item.Branch.Remote.Remote.Name, item.Branch.Remote.Name), _NormalBrush, item.ProjectName,
                    item.Commit.Subject, item.Commit.Commiter);
                }
            }

            scrollViewerMain.ScrollToBottom();

            if (result.Errors.Any())
            {
                var error = result.Errors.First();

                if (_LastErrorText != error.Message)
                {
                    _LastErrorText = error.Message;
                    var tooltip = error.Message;
                    _NotifyIcon.ShowBalloonTip(10000, "Scm Notifier - Error", tooltip, System.Windows.Forms.ToolTipIcon.Error);
                    SetTrayText("ERROR: " + tooltip);
                }
            }
            else if (result.Items.Any())
            {
                var tooltip = new StringBuilder();

                foreach (var item in result.Items)
                {
                    if (tooltip.Length > 0)
                    {
                        tooltip.Append(Environment.NewLine);
                    }

                    if (item.Commit.IsShort)
                    {
                        tooltip.AppendFormat("[{4}]: Commit {0} on '{1}' (from '{2}/{3}')", HashToShort(item.Commit.Hash), item.Branch.Name,
                            item.Branch.Remote.Remote.Name, item.Branch.Remote.Name, item.ProjectName);
                    }
                    else
                    {
                        tooltip.AppendFormat("[{0}] ({1}/{2}) {6}: Commit ({3}) by {4} - '{5}'",
                            item.ProjectName, item.Branch.Remote.Remote.Name, item.Branch.Remote.Name,
                            HashToShort(item.Commit.Hash), item.Commit.Commiter, item.Commit.Subject, DateToString(item.Commit.Date));
                    }
                }
                _NotifyIcon.ShowBalloonTip(10000, "Scm Notifier", tooltip.ToString(), System.Windows.Forms.ToolTipIcon.Info);
                SetTrayText(tooltip.ToString());
            }

            _NotifyIcon.Icon = result.Errors.Any() ? Properties.Resources.git_red : Properties.Resources.git_blue;
        }

        private string HashToShort(string hash)
        {
            return String.Format("{0}...{1}", hash.Substring(0, 5), hash.Substring(hash.Length - 2));
        }

        private void SetTrayText(string trayText)
        {
            _NotifyIcon.Text = trayText.Substring(0, Math.Min(trayText.Length, 63));
        }

        private void AddLogItem(string date, string branch, string commit, string remoteBranch, Brush brush, string projectName,
            string subject = "<N/A>", string commiter = "<N/A>")
        {
            var rowDef = new RowDefinition();
            LogGrid.RowDefinitions.Add(rowDef);

            var labelDate = new Label();
            labelDate.Foreground = brush;
            labelDate.Content = date;
            LogGrid.Children.Add(labelDate);

            var labelCommiter = new Label();
            labelCommiter.Foreground = brush;
            labelCommiter.Content = commiter;
            LogGrid.Children.Add(labelCommiter);

            var labelProject = new Label();
            labelProject.Foreground = brush;
            labelProject.Content = projectName;
            LogGrid.Children.Add(labelProject);

            var labelBranch = new Label();
            labelBranch.Foreground = brush;
            labelBranch.Content = branch;
            LogGrid.Children.Add(labelBranch);

            var labelCommit = new Label();
            labelCommit.Foreground = brush;
            labelCommit.Content = commit;
            LogGrid.Children.Add(labelCommit);

            var labelSubject = new Label();
            labelSubject.Foreground = brush;
            labelSubject.Content = subject;
            LogGrid.Children.Add(labelSubject);

            var labelRemoteBranch = new Label();
            labelRemoteBranch.Foreground = brush;
            labelRemoteBranch.Content = remoteBranch;
            LogGrid.Children.Add(labelRemoteBranch);

            var rowIndex = LogGrid.RowDefinitions.Count - 1;
            Grid.SetRow(labelDate, rowIndex);
            Grid.SetRow(labelBranch, rowIndex);
            Grid.SetRow(labelCommiter, rowIndex);
            Grid.SetRow(labelProject, rowIndex);
            Grid.SetRow(labelCommit, rowIndex);
            Grid.SetRow(labelSubject, rowIndex);
            Grid.SetRow(labelRemoteBranch, rowIndex);
            Grid.SetColumn(labelCommiter, 1);
            Grid.SetColumn(labelProject, 2);
            Grid.SetColumn(labelBranch, 3);
            Grid.SetColumn(labelSubject, 4);
            Grid.SetColumn(labelCommit, 5);
            Grid.SetColumn(labelRemoteBranch, 6);
        }

        #endregion

        #endregion

        #region Event Handlers

        private void menuReloadGit_Click(object sender, EventArgs e)
        {
            _GitManager.Reload();
            _LastErrorText = string.Empty;
        }

        private void OnMenuExitClicked(object sender, EventArgs e)
        {
            _CanClose = true;
            Close();
        }

        private void notifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            ShowWindow();
        }

        private void notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (this.IsVisible)
                {
                    Hide();
                }
                else
                {
                    ShowWindow();
                }
            }
        }

        private void GitManager_OnNewLogItems(object sender, GitManager.NewLogItemEventArgs e)
        {
            AddItemToLog(e);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_CanClose)
            {
                _NotifyIcon.Visible = false;
                _GitManager.Stop();
            }
            else
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearLog();
        }

        #endregion
    }
}
