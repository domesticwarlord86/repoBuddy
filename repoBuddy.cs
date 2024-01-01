//!CompilerOption:AddRef:SharpSvn.dll
//!CompilerOption:AddRef:LibGit2Sharp.dll


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ff14bot.AClasses;
using ff14bot.Helpers;
using ff14bot.Managers;
using ICSharpCode.SharpZipLib.Zip;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Newtonsoft.Json;
using Version = System.Version;


namespace repoBuddy;

public class repoBuddy : BotPlugin
{
#if RB_CN
        public override string Name => "RB 资源更新器";
#else
    public override string Name => "repoBuddy";
#endif
    public override string Author => "Zimble";
    public override Version Version => new Version(1, 12);
    public override string Description => "Automatically update RB accessories from repositories";
    public override bool WantButton => true;
    public override string ButtonText => "Settings";
    public static DataSet repoDataSet = new DataSet();
    private static Color LogColor = Colors.Wheat;
    public bool restartNeeded = false;
    public static List<string> repoLog = new List<string>();
    public static Dictionary<string, List<string>> ddlDict = new Dictionary<string, List<string>>();

    public override void OnButtonPress()
    {
        CreateSettingsForm();
    }

    public override void OnEnabled()
    {
        //Thread waitThread = new Thread(WaitForDone);
        //waitThread.Start();

        Logging.Write(LogColor, $"[{Name}-v{Version}] checking for updates");
        RoutineManager.RoutineChanged += new EventHandler(WaitForLog);
        repoStart();
    }

    public override void OnInitialize()
    {
        GetRepoData();
        GetDdlData();
    }

    public void MigrateLlamaLibrary()
    {
        try
        {
            for (int i = 0; i < repoDataSet.Tables["Repo"].Rows.Count; i++)
            {
                if (repoDataSet.Tables["Repo"].Rows[i]["Name"].ToString() == "FCBuffPlugin")
                {
                    repoDataSet.Tables["Repo"].Rows.RemoveAt(i);
                }

                if (repoDataSet.Tables["Repo"].Rows[i]["Name"].ToString() == "LisbethVentures")
                {
                    repoDataSet.Tables["Repo"].Rows.RemoveAt(i);
                }
            }

            if (Directory.Exists($@"Plugins\FCBuffPlugin"))
            {
                ZipFolder($@"Plugins\FCBuffPlugin", $@"Plugins\FCBuffPlugin_{DateTime.Now.Ticks}.zip");
                Directory.Delete($@"Plugins\FCBuffPlugin", true);
                //restartNeeded = true;
            }

            if (Directory.Exists($@"Plugins\LisbethVentures"))
            {
                ZipFolder($@"Plugins\LisbethVentures", $@"Plugins\LisbethVentures_{DateTime.Now.Ticks}.zip");
                Directory.Delete($@"Plugins\LisbethVentures", true);
                //restartNeeded = true;
            }

            repoDataSet.WriteXml(Constants.ReposXmlPath);
        }
        catch (Exception e)
        {
            Logging.Write(LogColor,
                $"[{Name}-v{Version}] Cleaning up migrated LlamaLibrary misc. failed, delete LisbethVentures and FCBuffPlugin manually. {e}");
        }

        try
        {
            if (Directory.Exists($@"BotBases\LlamaLibrary"))
            {
                for (int i = 0; i < repoDataSet.Tables["Repo"].Rows.Count; i++)
                {
                    if (repoDataSet.Tables["Repo"].Rows[i]["Name"].ToString() == "LlamaLibrary")
                    {
                        repoDataSet.Tables["Repo"].Rows.RemoveAt(i);
                        repoDataSet.Tables["Repo"].Rows.Add("__LlamaLibrary", "Quest Behavior",
                            "https://github.com/nt153133/__LlamaLibrary.git/trunk");
                        repoDataSet.Tables["Repo"].Rows.Add("LlamaUtilities", "Botbase",
                            "https://github.com/nt153133/LlamaUtilities.git/trunk");
                        repoDataSet.Tables["Repo"].Rows.Add("ExtraBotbases", "Botbase",
                            "https://github.com/nt153133/ExtraBotbases.git/trunk");
                        repoDataSet.Tables["Repo"].Rows.Add("ResplendentTools", "Botbase",
                            "https://github.com/Sykel/ResplendentTools.git/trunk");
                        repoDataSet.Tables["Repo"].Rows.Add("LlamaPlugins", "Plugin",
                            "https://github.com/nt153133/LlamaPlugins.git/trunk");
                    }
                }

                repoDataSet.WriteXml(Constants.ReposXmlPath);
                //restartNeeded = true;
                ZipFolder($@"BotBases\LlamaLibrary", $@"BotBases\LlamaLibrary_{DateTime.Now.Ticks}.zip");
                Directory.Delete($@"BotBases\LlamaLibrary", true);
            }
        }
        catch (Exception e)
        {
            Logging.Write(LogColor,
                $"[{Name}-v{Version}] Archiving LlamaLibrary failed, please backup and delete manually. {e}");
        }
    }

    public void CreateSettingsForm()
    {
        SettingsForm settingsForm = new SettingsForm();
        settingsForm.ShowDialog();
    }

    public void GetRepoData()
    {
        var settingsFileInfo = new FileInfo(Constants.ReposXmlPath);
        if (!settingsFileInfo.Exists || settingsFileInfo.Length == 0)
        {
            File.Copy(Constants.DefaultReposXmlPath, Constants.ReposXmlPath, overwrite: true);
        }

        repoDataSet.Clear();
        repoDataSet.ReadXml(Constants.ReposXmlPath);
    }

    public void GetDdlData()
    {
        using (StreamReader file = File.OpenText(Constants.DdlsJsonPath))
        {
            JsonSerializer serializer = new JsonSerializer();
            ddlDict = (Dictionary<string, List<string>>)serializer.Deserialize(file,
                typeof(Dictionary<string, List<string>>));
        }
    }

    static repoBuddy()
    {
        AppDomain.CurrentDomain.AppendPrivatePath(Constants.RepoBuddyDirectory);
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        //  AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomainLibgit2sharp_AssemblyResolve);
        //  AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomaingit2a2bde63_AssemblyResolve);
    }

    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) //force load sharpsvn
    {
        string path = Constants.LibGit2SharpDllPath;
        string path2 = Constants.git2a2bde63DllPath;

        try
        {
            Unblock(path);
            Unblock(path2);
        }
        catch (Exception)
        {
            // pass
        }

        AssemblyName asmName = new AssemblyName(args.Name);

        if (asmName.Name != "LibGit2Sharp")
        {
            return null;
        }

        return Assembly.LoadFrom(path);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool DeleteFile(string name);

    public static bool Unblock(string fileName)
    {
        return DeleteFile(fileName + ":Zone.Identifier");
    }

    public static void RestartRebornBuddy()
    {
        //AppDomain.CurrentDomain.ProcessExit += new EventHandler(RebornBuddy_Exit);
        //
        //void RebornBuddy_Exit (object sender, EventArgs e)
        //{
        //	Process.Start("rebornbuddy", "-a"); //autologin using stored key
        //}

        Process RBprocess = Process.GetCurrentProcess();
        Process.Start(Constants.WatchdogBatPath, $"{RBprocess.Id} {ff14bot.Core.Memory.Process.Id}");
        RBprocess.CloseMainWindow();
    }

    public void WriteLog(List<string> array, string msg)
    {
        array.Add(msg);
        Logging.Write(LogColor, msg);
    }

    #region rebornbuddy init thread logic

    public void WaitForLog(object obj, EventArgs eve)
    {
        RoutineManager.RoutineChanged -= WaitForLog;
        Logging.Write(LogColor, $"[{Name}-v{Version}] waiting for Logs to end...");
        System.Timers.Timer logwatch = new System.Timers.Timer();
        logwatch.Interval = 3000;
        logwatch.AutoReset = true;
        logwatch.Enabled = true;
        logwatch.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
        Logging.OnLogMessage += new Logging.LogMessageDelegate(RestartTimer);

        void RestartTimer(ReadOnlyCollection<Logging.LogMessage> message)
        {
            logwatch.Stop();
            logwatch.Start();
        }

        void OnTimedEvent(object o, System.Timers.ElapsedEventArgs e)
        {
            Logging.Write(LogColor, $"[{Name}-v{Version}] RB fully loaded!");
            Logging.OnLogMessage -= RestartTimer;
            logwatch.Elapsed -= OnTimedEvent;
            logwatch.Stop();
            logwatch.Dispose();

            using (StreamReader file = File.OpenText(@"Plugins\repoBuddy\repoLog.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                repoLog = (List<string>)serializer.Deserialize(file, typeof(List<string>));

                foreach (string change in repoLog)
                {
                    Logging.Write(LogColor, change);
                }
            }

            using (StreamWriter file = File.CreateText(@"Plugins\repoBuddy\repoLog.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                repoLog.Clear();
                serializer.Serialize(file, repoLog);
            }
        }
    }

    #endregion

    #region repo logic

    public void repoStart()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Parallel.ForEach(repoDataSet.Tables["Repo"].Rows.Cast<DataRow>(), row =>
        {
            string repoName = row[0].ToString();
            string repoType = row[1].ToString() + "s";
            string repoUrl = row[2].ToString();
            string repoPath = $@"{repoType}\{repoName}";


            long currentLap;
            long totalLap;
            currentLap = stopwatch.ElapsedMilliseconds;

            try
            {
                if (Directory.Exists($@"{repoPath}\.svn"))
                {
                    // Delete directory so we can download a git version
                    DeleteDirectory($"{repoPath}");
                    // Create clean directory
                    Directory.CreateDirectory($"{repoPath}");
                }

                if (Directory.Exists($@"{repoPath}\.git"))
                {
                    #region Credentials

                    // Credential information to Pull
                    LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
                    options.FetchOptions = new FetchOptions();
                    options.FetchOptions.CredentialsProvider = new CredentialsHandler(
                        (url, usernameFromUrl, types) =>
                            new UsernamePasswordCredentials()
                            {
                                Username = $"{Settings.Instance.UserName}",
                                Password = $"{Settings.Instance.GithubPat}"
                            });
                    var signature = new LibGit2Sharp.Signature(
                        new Identity("repoBuddy", "MERGE_USER_EMAIL"), DateTimeOffset.Now);
                    
                    // Credential information to Fetch
                    FetchOptions fetchOptions = new FetchOptions();
                    fetchOptions.CredentialsProvider = new CredentialsHandler((url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials()
                        {
                            Username = $"{Settings.Instance.UserName}",
                            Password = $"{Settings.Instance.GithubPat}"
                        });

                    #endregion
                    
                    using (var repo = new Repository($@"{repoPath}\.git"))
                    {
                        repo.RetrieveStatus();
                        
                        // Fetch remote information to see if update is needed
                        string logMessage = "";
                        foreach (Remote remote in repo.Network.Remotes)
                        {
                            IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                            Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, logMessage);
                        }

                        Logging.Write(LogColor,
                            $"[{Name}-v{Version}] {repoName} LocalCurrent: {repo.Commits.Count().ToString()} BehindBy {repo.Head.TrackingDetails.BehindBy}");
                        
                        // If our local branch is behind the origin, run an update.
                        if (repo.Head.TrackingDetails.BehindBy > 0)
                        {
                            Logging.Write(LogColor,
                                $"[{Name}-v{Version}] Updated {repoName} to {repo.Commits.Count().ToString()}, BehindBy {repo.Head.TrackingDetails.BehindBy}");
                            
                            // This section was meant to log the different commit messages, but I haven't gotten it to work yet.
                            #region Logging

                            var trackingBranch = repo.Head.TrackedBranch; 
                            var log = repo.Commits.QueryBy(new CommitFilter
                            {
                                IncludeReachableFrom = trackingBranch.Tip.Id,
                                ExcludeReachableFrom = repo.Head.Tip.Id,
                            });

                            var count = log.Count(); //Counts the number of log entries

                            //iterate the commits that represent the difference between your last 
                            //push to the remote branch and latest commits
                            foreach (var commit in log)
                            {
                                Logging.Write(LogColor, $"[{Name}-v{Version}] {commit.Message}");
                            }

                            #endregion

                            // Pull
                            Commands.Pull(repo, signature, options);
                        }
                    }
                }
                else
                {
                    // Credential information to fetch
                    LibGit2Sharp.CloneOptions options = new LibGit2Sharp.CloneOptions();
                    options.FetchOptions.CredentialsProvider = new CredentialsHandler(
                        (url, usernameFromUrl, types) =>
                            new UsernamePasswordCredentials()
                            {
                                Username = $"{Settings.Instance.UserName}",
                                Password = $"{Settings.Instance.GithubPat}"
                            });
                    Logging.Write(LogColor,
                        $"[{Name}-v{Version}] Attempting to clone {repoName} from {repoUrl} to {repoPath}");
                    Repository.Clone(repoUrl, repoPath, options);
                    totalLap = stopwatch.ElapsedMilliseconds - currentLap;
                    WriteLog(repoLog, $"[{Name}-v{Version}] {repoName} checkout complete in {totalLap} ms.");
                    if (repoType != "Profiles")
                    {
                        restartNeeded = true;
                    }
                }
            }

            finally
            {
            }
        });
        stopwatch.Stop();
        Logging.Write(LogColor, $"[{Name}-v{Version}] processes complete in {stopwatch.ElapsedMilliseconds} ms.");

        MigrateLlamaLibrary();

        if (repoLog.Count > 0)
        {
            using (StreamWriter file = File.CreateText(Constants.RepoLogJsonPath))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, repoLog);
            }
        }

        if (restartNeeded)
        {
            Logging.Write(LogColor, $"[{Name}-v{Version}] Restarting to reload assemblies.");
            RestartRebornBuddy();
        }
    }

    #endregion

    #region ddl logic

    public static void DirectDownload(string path, string Url)
    {
        var bytes = DownloadLatestVersion(Url).Result;

        if (bytes == null || bytes.Length == 0)
        {
            Logging.Write(LogColor, $"[Error] Bad product data returned.");
            return;
        }

        if (!Extract(bytes, path))
        {
            Logging.Write(LogColor, $"[Error] Could not extract new files.");
            return;
        }
    }

    private static bool Extract(byte[] files, string directory)
    {
        try
        {
            using (var stream = new MemoryStream(files))
            {
                var zip = new FastZip();
                var previous = ZipConstants.DefaultCodePage;
                ZipConstants.DefaultCodePage = 437;
                zip.ExtractZip(stream, directory, FastZip.Overwrite.Always, null, null, null, false, true);
                ZipConstants.DefaultCodePage = previous;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logging.Write(LogColor, $"[Error] Could not extract new files. {ex}");
            return false;
        }
    }

    public static void ZipFolder(string sourceFolder, string zipPath)
    {
        var zip = new FastZip();
        var previous = ZipConstants.DefaultCodePage;
        ZipConstants.DefaultCodePage = 437;
        zip.CreateZip(zipPath, sourceFolder, true, null);
        ZipConstants.DefaultCodePage = previous;
    }

    private static async Task<byte[]> DownloadLatestVersion(string Url)
    {
        using (var client = new HttpClient())
        {
            byte[] responseMessageBytes;
            try
            {
                responseMessageBytes = client.GetByteArrayAsync(Url).Result;
            }
            catch (Exception e)
            {
                Logging.Write(LogColor, e.Message);
                return null;
            }

            return responseMessageBytes;
        }
    }

    #endregion

    public void DeleteDirectory(string targetDir)
    {
        File.SetAttributes(targetDir, FileAttributes.Normal);

        string[] files = Directory.GetFiles(targetDir);
        string[] dirs = Directory.GetDirectories(targetDir);

        foreach (string file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs)
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(targetDir, false);
    }

    public class Settings : JsonSettings
    {
        private static Settings _instance;

        public static Settings Instance
        {
            get
            {
                return _instance ?? (_instance = new Settings());
                ;
            }
        }

        public Settings() : base(Path.Combine(SettingsPath, "repoBuddy.json"))
        {
        }

        [Setting] public uint Id { get; set; }

        private string _userName;

        [DefaultValue(null)]
        public string UserName
        {
            get => _userName;
            set
            {
                if (value == _userName)
                {
                    return;
                }

                _userName = value;
            }
        }

        private string _githubPat;

        [DefaultValue(null)]
        public string GithubPat
        {
            get => _githubPat;
            set
            {
                if (value == _githubPat)
                {
                    return;
                }

                _githubPat = value;
            }
        }
    }
}