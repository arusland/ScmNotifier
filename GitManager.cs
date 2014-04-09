using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ScmNotifier
{
    public class GitManager
    {
        #region Classes

        private class ProcessBufferHandler
        {
            public Stream stream;
            public StringBuilder sb;
            public Encoding encoding;
            public State state;

            public enum State
            {
                Running,
                Stopped
            }

            public ProcessBufferHandler(Stream stream, Encoding encoding)
            {
                this.stream = stream;
                this.sb = new StringBuilder();
                this.encoding = encoding;
                state = State.Running;
            }
            public void ProcessBuffer()
            {
                sb.Append(new StreamReader(stream, encoding).ReadToEnd());
                state = State.Stopped;
            }

            public string ReadToEnd()
            {
                return sb.ToString();
            }

            public void Wait()
            {
                while (state != State.Stopped)
                {
                    Thread.Sleep(1000);
                }
            }
        }


        private static class FormatHelper
        {
            public const string FORMAT_GitLog = "log -1 %BRANCH_NAME% --pretty=format:\"<%H>1 <%an>2 <%ae>3 <%ai>4 <%s>5 <%d>6\"";
        }

        public class Remote
        {
            private static Regex _SshUrl;

            static Remote()
            {
                _SshUrl = new Regex(@"^(ssh://)*([\w]+)@([^:/]+)([:/]*)(.*)$");
            }

            public Remote(string name, string url)
            {
                Name = name;
                Url = url;
                LastCommitCommand = GetCommand(url);
            }

            public string Name
            {
                get;
                private set;
            }

            public string Url
            {
                get;
                private set;
            }

            public string LastCommitCommand
            {
                get;
                private set;
            }

            public bool HasLastCommitCommand
            {
                get
                {
                    return LastCommitCommand.IsNotNullOrEmpty();
                }
            }

            public override string ToString()
            {
                return String.Format("Name: {0}", Name);
            }

            private string GetCommand(string url)
            {
                var m = _SshUrl.Match(url);

                if (m.Success)
                {
                    if (m.Groups[4].Value.IsNotEmpty())
                    {
                        return String.Format("-n {0}@{1} \"cd {2} && git {3}\"", m.Groups[2].Value, m.Groups[3].Value, m.Groups[5].Value,
                            FormatHelper.FORMAT_GitLog.Replace('"', '\''));
                    }
                    else
                    {
                        return String.Format("-n {0}@{1} \"git {2}\"", m.Groups[2].Value, m.Groups[3].Value, FormatHelper.FORMAT_GitLog.Replace('"', '\''));
                    }
                }

                return string.Empty;
            }
        }

        public class RemoteBranch
        {
            public RemoteBranch(string branchPath, Func<Remote> remote)
            {
                Name = branchPath.Split('/').Last();
                BranchPath = branchPath;
                RemoteLazy = remote;
            }

            public string Name
            {
                get;
                private set;
            }

            public string BranchPath
            {
                get;
                private set;
            }

            public Remote Remote
            {
                get
                {
                    return RemoteLazy();
                }
            }

            private Func<Remote> RemoteLazy
            {
                get;
                set;
            }

            public override string ToString()
            {
                return String.Format("Name: {0}; Remote branch: ({1})", Name, RemoteLazy());
            }
        }

        public class Commit
        {
            public Commit(string hash, string commiter, string email, string date, string subject, string branch)
            {
                Hash = hash;
                Commiter = commiter;
                Email = email;
                Date = date.IsNotNullOrEmpty() ? DateTime.Parse(date) : DateTime.Now;
                Subject = subject;
                Branch = branch;
                IsShort = false;
            }

            public Commit(string hash, string branch)
                : this(hash, null, null, null, null, branch)
            {
                IsShort = true;
            }

            public string Hash
            {
                get;
                private set;
            }

            public string Commiter
            {
                get;
                private set;
            }

            public string Email
            {
                get;
                private set;
            }

            public DateTime Date
            {
                get;
                private set;
            }

            public string Subject
            {
                get;
                private set;
            }

            public string Branch
            {
                get;
                private set;
            }

            public bool IsShort
            {
                get;
                private set;
            }

            public override string ToString()
            {
                if (Commiter.IsNotNullOrEmpty() && Email.IsNotNullOrEmpty() && Subject.IsNotNullOrEmpty())
                {
                    return String.Format("[{0}, {3}, {1}, {2}, {4}]", Commiter, Email, Hash, Subject, Branch);
                }
                else
                {
                    return String.Format("[{0}, {1}]", Hash, Branch);
                }
            }
        }

        public class Branch
        {
            public Branch(string name, RemoteBranch remote, Commit lastCommit)
            {
                Name = name;
                Remote = remote;
                LastCommit = lastCommit;
            }

            public string Name
            {
                get;
                private set;
            }

            public RemoteBranch Remote
            {
                get;
                private set;
            }

            public Commit LastCommit
            {
                get;
                private set;
            }

            public override string ToString()
            {
                string lastCommit = LastCommit != null ? LastCommit.ToString() : "<not found>";

                if (Remote != null)
                {
                    return String.Format("Name: {0} with remote branch '{1}' on '{2}'; Last Commit - {3}", Name, Remote.Name, Remote.Remote.Name, lastCommit);
                }
                else
                {
                    return String.Format("Name: {0} without remote branch; Last Commit - {1}", Name, lastCommit);
                }
            }
        }

        public class NotifyItem
        {
            public NotifyItem(Commit commit, Branch branch, string projectName)
            {
                Commit = commit;
                Branch = branch;
                ProjectName = projectName;
            }

            public Commit Commit
            {
                get;
                private set;
            }

            public Branch Branch
            {
                get;
                private set;
            }

            public string ProjectName
            {
                get;
                private set;
            }
        }


        public class NewLogItemEventArgs : EventArgs
        {
            public NewLogItemEventArgs(IList<NotifyItem> logItems, IList<Exception> errors)
            {
                Items = logItems;
                Errors = errors;
            }

            public IList<Exception> Errors
            {
                get;
                private set;
            }

            public IList<NotifyItem> Items
            {
                get;
                private set;
            }
        }

        private struct LoadingResult
        {
            public IList<Exception> Errors;

            public IList<NotifyItem> Items;
        }

        #endregion

        #region Constants

        private const int TIMEOUT_Step = 1000;

        #endregion

        #region Fields

        private BackgroundWorker _Worker;
        private string[] _ProjectPathes;
        private int _Period;
        // _LastHashes["git path"] = ["local branch name", "SHA1"]
        private Dictionary<string, Dictionary<string, string>> _LastHashes;
        private bool _DoReload;
        private readonly Regex _RgxRemote;
        private readonly Regex _RgxBranchLine;
        private readonly Regex _RgxRemoteLine;
        private readonly Regex _RgxCommitLine;
        private readonly Regex _RgxValueLine;
        private readonly Regex _RgxLogLine;
        private readonly IDictionary<string, IList<Commit>> _RemoteItemsCache;

        #endregion

        #region Ctors

        public GitManager(IEnumerable<string> projectPathes, int periodInSeconds)
        {
            if (projectPathes == null)
            {
                throw new ArgumentNullException("projectPathes");
            }

            _RemoteItemsCache = new Dictionary<string, IList<Commit>>();

            _RgxRemote = new Regex(@"^([a-z0-9]+)\s+([^\s]+)$", RegexOptions.Multiline);
            _RgxRemoteLine = new Regex(@"^\[remote \""([^""]+)\""\]$");
            _RgxBranchLine = new Regex(@"^\[branch \""([^""]+)\""\]$");
            _RgxValueLine = new Regex(@"^\s+(\w+)\s+=\s+([^=\s]+)");
            _RgxCommitLine = new Regex(@"^commit\s+([a-z0-9]+)");
            _RgxLogLine = new Regex(@"^<(\w+)>1 <([^\0]+)>2 <([^\0]+)>3 <([^\0]+)>4 <([^\0]+)>5 <\s\(([^\)\(]+)\)>6", RegexOptions.Multiline);

            _ProjectPathes = projectPathes.ToArray();
            _LastHashes = new Dictionary<string, Dictionary<string, string>>();
            _Period = Math.Max(periodInSeconds, 1) * 1000;
            _Worker = new BackgroundWorker();
            _Worker.WorkerReportsProgress = true;
            _Worker.WorkerSupportsCancellation = true;
            _Worker.ProgressChanged += Worker_ProgressChanged;
            _Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            _Worker.DoWork += Worker_DoWork;
        }

        #endregion

        #region Properties

        #region Public

        public bool IsStarted
        {
            get
            {
                return _Worker.IsBusy;
            }
        }

        #endregion

        #endregion

        #region Methods

        #region Public

        public void Start()
        {
            if (_ProjectPathes.Length > 0)
            {
                _Worker.RunWorkerAsync();
            }
        }

        public void Stop()
        {
            _Worker.CancelAsync();
        }

        public void Reload()
        {
            _DoReload = true;
        }

        #endregion

        #region Private

        private void Log(string msg)
        {
            Debug.WriteLine(msg);
        }

        private StreamReader Exec(string filename, string arguments, string workdir)
        {
            Log(String.Format("Exec: {0} {1}", filename, arguments));

            if (workdir.IsNotNullOrEmpty())
            {
                Directory.SetCurrentDirectory(workdir);
            }

            StringBuilder rcvData = new StringBuilder();

            var p = new System.Diagnostics.Process();

            if (filename.Contains("ssh") && Environment.GetEnvironmentVariable("HOME").IsNullOrEmpty())
            {
                p.StartInfo.EnvironmentVariables.Add("HOME", ScmNotifier.Properties.Settings.Default.SshFolder.IsNotNullOrEmpty() ?
                    ScmNotifier.Properties.Settings.Default.SshFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }

            p.StartInfo.FileName = filename;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.WorkingDirectory = workdir;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.ErrorDialog = false;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.StartInfo.CreateNoWindow = true;
            p.EnableRaisingEvents = true;
            p.OutputDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) =>
                {
                    rcvData.AppendLine(e.Data);
                };
            p.ErrorDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) =>
                {
                    if (e.Data.IsNotNullOrEmpty())
                    {
                        Log(e.Data);
                    }
                };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (p.WaitForExit(1000 * 60))
            {
                if (p.HasExited)
                {
                    var ms = new MemoryStream(GetBytes(rcvData.ToString()));
                    return new StreamReader(ms, Encoding.UTF8);
                }

                return new StreamReader(new MemoryStream());
            }

            ProcessUtil.KillProcessTree(p);
            throw new InvalidOperationException(String.Format("Executing command timeout: [{0} {1}]", filename, arguments));
        }

        private void ProcessStream(object handler)
        {
            ((ProcessBufferHandler)handler).ProcessBuffer();
        }

        static byte[] GetBytes(string str)
        {
            return System.Text.Encoding.UTF8.GetBytes(str);
        }

        private IList<Commit> GetRemoteItems(Branch branch, string path)
        {
            try
            {
                if (branch.Remote.Remote.HasLastCommitCommand)
                {
                    var remoteSignature = String.Format("{0}|{1}|{2}", branch.Remote.Remote.Name, branch.Remote.Name, path);

                    if (_RemoteItemsCache.ContainsKey(remoteSignature))
                    {
                        return _RemoteItemsCache[remoteSignature];
                    }

                    var stream = Exec("ssh", branch.Remote.Remote.LastCommitCommand.Replace("%BRANCH_NAME%", branch.Remote.Name), null);
                    var result = new List<Commit>();

                    string line = stream.ReadLine();

                    while (line.IsNotNull())
                    {
                        var commit = ParseCommit(line);
                        if (commit.IsNotNull())
                        {
                            result.Add(commit);
                            Log(String.Format("Found remote commit: {0}", commit));
                        }

                        line = stream.ReadLine();
                    }

                    if (result.Any())
                    {
                        _RemoteItemsCache[remoteSignature] = result;

                        return result;
                    }
                }

                return GetMinimalRemoteItems(branch, path);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                throw new InvalidOperationException(ex.Message);
            }
        }

        private IList<Commit> GetMinimalRemoteItems(Branch branch, string path)
        {
            try
            {
                var remoteSignature = String.Format("{0}|{1}|{2}", branch.Remote.Remote.Name, branch.Remote.Name, path);

                if (_RemoteItemsCache.ContainsKey(remoteSignature))
                {
                    return _RemoteItemsCache[remoteSignature];
                }

                var stream = Exec("git", String.Format("ls-remote --heads {0}", branch.Remote.Remote.Name), path);
                var result = new List<Commit>();

                string line = stream.ReadLine();

                while (line.IsNotNull())
                {
                    var commit = ParseMinimalCommit(line);

                    if (commit.IsNotNull())
                    {
                        result.Add(commit);
                        Log(String.Format("Found remote commit: {0}", result.Last()));
                    }

                    line = stream.ReadLine();
                }

                _RemoteItemsCache[remoteSignature] = result;

                return result;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                throw new InvalidOperationException(ex.Message);
            }
        }

        private Commit ParseMinimalCommit(string line)
        {
            var match = _RgxRemote.Match(line);

            if (match.Success)
            {
                var splitted = match.Groups[2].Value.Split('/');

                if (splitted.Length <= 0)
                {
                    throw new InvalidOperationException("Invalid format from git: " + line);
                }

                return new Commit(match.Groups[1].Value, splitted[splitted.Length - 1]);
            }

            return null;
        }

        private Commit ParseCommit(string line)
        {
            var match = _RgxLogLine.Match(line);

            if (match.Success)
            {
                var splitted = match.Groups[6].Value.Split(',');

                if (splitted.Length <= 0)
                {
                    throw new InvalidOperationException("Invalid format from git: " + line);
                }

                return new Commit(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value,
                    match.Groups[5].Value, splitted[splitted.Length - 1].Trim());
            }

            return null;
        }

        private IList<Branch> LoadBranches(string path)
        {
            var finfo = new FileInfo(Path.Combine(path, ".git", "config"));

            if (!finfo.Exists)
            {
                throw new InvalidOperationException("Invalid git dir: " + path);
            }

            var remotes = new List<Remote>();
            var branches = new List<Branch>();

            using (var sr = new StreamReader(finfo.FullName))
            {
                string line = sr.ReadLine();

                while (line != null)
                {
                    var match = _RgxRemoteLine.Match(line);

                    if (match.Success)
                    {
                        remotes.Add(LoadRemote(match.Groups[1].Value, sr, out line));
                        continue;
                    }
                    else
                    {
                        match = _RgxBranchLine.Match(line);

                        if (match.Success)
                        {
                            branches.Add(LoadBranch(match.Groups[1].Value, sr, remotes, path, out line));
                            continue;
                        }
                    }

                    line = sr.ReadLine();
                }
            }

            branches.ForEach(p => Log(String.Format("Found local branch: {0}", p)));

            return branches;
        }

        private Remote LoadRemote(string name, StreamReader sr, out string line)
        {
            line = sr.ReadLine();

            while (line.IsNotNullOrEmpty())
            {
                var match = _RgxValueLine.Match(line);

                if (match.Success)
                {
                    switch (match.Groups[1].Value)
                    {
                        case "url":
                            return new Remote(name, match.Groups[2].Value);
                        default:
                            Log(String.Format("ScmNotifier: skip unknown config value - {0}={1}", match.Groups[1].Value, match.Groups[2].Value));
                            break;
                    }
                }
                else
                {
                    break;
                }

                line = sr.ReadLine();
            }

            return new Remote(name, string.Empty);
        }

        private Branch LoadBranch(string name, StreamReader sr, IList<Remote> remotes, string path, out string line)
        {
            line = sr.ReadLine();
            string remoteValue = null, mergeValue = null;

            while (line.IsNotNullOrEmpty())
            {
                var match = _RgxValueLine.Match(line);

                if (match.Success)
                {
                    switch (match.Groups[1].Value)
                    {
                        case "remote":
                            remoteValue = match.Groups[2].Value;
                            break;
                        case "merge":
                            mergeValue = match.Groups[2].Value;
                            break;
                        default:
                            Log(String.Format("WARNING: ScmNotifier: skip unknown config value - {0}={1}", match.Groups[1].Value, match.Groups[2].Value));
                            break;
                    }
                }
                else
                {
                    break;
                }

                line = sr.ReadLine();
            }

            if (remoteValue.IsNotNullOrEmpty() && mergeValue.IsNotNullOrEmpty())
            {
                return new Branch(name, new RemoteBranch(mergeValue, () => remotes.Single(p => p.Name == remoteValue)), GetBranchLastCommit(path, name));
            }

            return new Branch(name, null, null);
        }

        private Commit GetBranchLastCommit(string path, string name)
        {
            var stream = Exec("git", FormatHelper.FORMAT_GitLog.Replace("%BRANCH_NAME%", name), path);
            string line = stream.ReadLine();

            while (line.IsNotNull())
            {
                var commit = ParseCommit(line);

                if (commit.IsNotNull())
                {
                    return commit;
                }

                line = stream.ReadLine();
            }

            return null;
        }

        private string GetProjectName(string path)
        {
            var dirInfo = new DirectoryInfo(path);

            if (dirInfo.Exists)
            {
                return dirInfo.Name;
            }

            return path;
        }

        private IList<NotifyItem> GetNewItems(out IList<Exception> errors)
        {
            var result = new List<NotifyItem>();
            errors = new List<Exception>();

            foreach (string path in _ProjectPathes)
            {
                try
                {
                    Log("==Begin getting info for path: " + path);

                    _RemoteItemsCache.Clear();
                    var branches = LoadBranches(path);
                    string projectName = GetProjectName(path);

                    foreach (var branch in branches.Where(p => p.Remote.IsNotNull() && p.LastCommit.IsNotNull()))
                    {
                        var remoteItems = GetRemoteItems(branch, path);
                        var remItem = remoteItems.FirstOrDefault(p => branch.Remote.Name == p.Branch);

                        if (remItem.IsNotNull() && remItem.Hash != branch.LastCommit.Hash)
                        {
                            if (_LastHashes.ContainsKey(path))
                            {
                                if (_LastHashes[path].ContainsKey(branch.Name))
                                {
                                    if (_LastHashes[path][branch.Name] != remItem.Hash)
                                    {
                                        _LastHashes[path][branch.Name] = remItem.Hash;
                                        result.Add(new NotifyItem(remItem, branch, projectName));
                                    }
                                }
                                else
                                {
                                    _LastHashes[path][branch.Name] = remItem.Hash;
                                    result.Add(new NotifyItem(remItem, branch, projectName));
                                }
                            }
                            else
                            {
                                _LastHashes[path] = new Dictionary<string, string>();
                                _LastHashes[path][branch.Name] = remItem.Hash;
                                result.Add(new NotifyItem(remItem, branch, projectName));
                            }
                        }
                    }

                    Log("==End getting info for path: " + path);
                }
                catch (System.Exception ex)
                {
                    var msg = String.Format("{1} for path '{0}'.", path, ex.Message);
                    Log(msg);
                    errors.Add(new InvalidOperationException(msg, ex));
                }
            }

            result.ForEach(p => String.Format("New commit: Project: {0}; Branch: {1}; RemotePath: {2}", p.ProjectName, p.Branch.Name, p.Commit.Branch));

            return result;
        }

        #endregion

        #endregion

        #region Events

        public event EventHandler<NewLogItemEventArgs> OnNewLogItems;

        #endregion

        #region Event Handlers

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Log("SVNManager: stopped");
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (OnNewLogItems != null)
            {
                if (e.UserState is LoadingResult)
                {
                    LoadingResult result = (LoadingResult)e.UserState;
                    OnNewLogItems(this, new NewLogItemEventArgs(result.Items, result.Errors));
                }
                else
                {
                    throw new InvalidOperationException("Unsupported: " + e.UserState.ToString());
                }
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool lastWasError = false;

            while (true)
            {
                if (_Worker.CancellationPending)
                {
                    break;
                }

                if (_DoReload)
                {
                    _DoReload = false;
                    _LastHashes.Clear();
                }

                _RemoteItemsCache.Clear();

                try
                {
                    IList<Exception> errors;
                    var newLogItems = GetNewItems(out errors);
                    lastWasError = errors.Count > 0 && newLogItems.Count <= 0;
                    _Worker.ReportProgress(0, new LoadingResult() { Errors = errors, Items = newLogItems });
                }
                catch (System.Exception ex)
                {
                    Log(ex.ToString());

                    IList<Exception> errors = new List<Exception>();
                    errors.Add(ex);
                    lastWasError = true;
                    _Worker.ReportProgress(0, new LoadingResult() { Errors = errors });
                }

                int timeout = lastWasError ? TIMEOUT_Step : _Period;

                while (timeout > 0 && !(_Worker.CancellationPending || _DoReload))
                {
                    Thread.Sleep(TIMEOUT_Step);

                    timeout -= TIMEOUT_Step;
                }
            }
        }

        #endregion
    }
}
