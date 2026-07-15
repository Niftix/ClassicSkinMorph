using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Classic Skin Morph")]
[assembly: AssemblyProduct("Classic Skin Morph")]
[assembly: AssemblyVersion("0.5.0.0")]
[assembly: AssemblyFileVersion("0.5.0.0")]

namespace ClassicSkinMorph
{
    internal enum LauncherState { Loading, Active, Error }

    internal sealed class LoadProgress
    {
        public int Percent;
        public string Status;
    }

    internal sealed class SweepCanvas : Control
    {
        public double Phase { get; set; }
        public SweepCanvas() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(10, 20, 40));
            int virtualWidth = Width * 3;
            float offsetX = (float)(-Width * 2 + Phase * Width * 4);
            var virtualGradient = new Rectangle(0, 0, virtualWidth, Math.Max(1, Height));
            using (var brush = new LinearGradientBrush(virtualGradient, Color.FromArgb(10, 20, 40), Color.FromArgb(10, 20, 40), 0f))
            {
                brush.InterpolationColors = new ColorBlend {
                    Colors = new[] { Color.FromArgb(10, 20, 40), Color.FromArgb(201, 169, 97), Color.FromArgb(244, 227, 168), Color.FromArgb(201, 169, 97), Color.FromArgb(10, 20, 40) },
                    Positions = new[] { 0f, .45f, .50f, .55f, 1f }
                };
                brush.TranslateTransform(offsetX, 0f, MatrixOrder.Append);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }

    internal sealed class GlowCanvas : Control
    {
        public double Phase { get; set; }
        public GlowCanvas() { DoubleBuffered = true; BackColor = Color.FromArgb(10, 20, 40); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            double pulse = (Math.Sin(Phase * Math.PI * 2 - Math.PI / 2) + 1) / 2;
            int goldAlpha = 38 + (int)(102 * pulse); // 15% -> 55%
            var shadowBounds = new Rectangle(16, 22, Width - 32, Height - 36);
            using (var shadowPath = new GraphicsPath())
            {
                shadowPath.AddEllipse(shadowBounds);
                using (var shadow = new PathGradientBrush(shadowPath))
                {
                    shadow.CenterColor = Color.FromArgb(90, 0, 0, 0);
                    shadow.SurroundColors = new[] { Color.Transparent };
                    shadow.FocusScales = new PointF(.65f, .55f);
                    e.Graphics.FillPath(shadow, shadowPath);
                }
            }
            var glowBounds = new Rectangle(5, 8, Width - 10, Height - 16);
            using (var glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(glowBounds);
                using (var glow = new PathGradientBrush(glowPath))
                {
                    glow.CenterColor = Color.FromArgb(goldAlpha, 201, 169, 97);
                    glow.SurroundColors = new[] { Color.Transparent };
                    glow.FocusScales = new PointF(.62f, .50f);
                    e.Graphics.FillPath(glow, glowPath);
                }
            }
        }
    }

    internal sealed class StatusCanvas : Control
    {
        public string StatusText { get; set; }
        public LauncherState State { get; set; }
        public double Phase { get; set; }
        public StatusCanvas() { DoubleBuffered = true; BackColor = Color.FromArgb(10, 20, 40); Font = new Font("Segoe UI", 9f, FontStyle.Bold); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = State == LauncherState.Active ? Color.FromArgb(61, 220, 110) : State == LauncherState.Error ? Color.FromArgb(224, 90, 78) : Color.FromArgb(76, 141, 255);
            string text = StatusText ?? "";
            Size measured = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int dotSpace = 18, start = (Width - measured.Width - dotSpace) / 2;
            double wave = State == LauncherState.Active ? 0 : (Math.Sin(Phase * Math.PI * 2 - Math.PI / 2) + 1) / 2;
            float scale = 1f + (float)(.3 * wave);
            int alpha = State == LauncherState.Active ? 255 : 255 - (int)(166 * wave);
            float diameter = 6f * scale;
            using (var brush = new SolidBrush(Color.FromArgb(alpha, color))) e.Graphics.FillEllipse(brush, start, (Height - diameter) / 2f, diameter, diameter);
            TextRenderer.DrawText(e.Graphics, text, Font, new Point(start + dotSpace, (Height - measured.Height) / 2), color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class ActiveCanvas : Control
    {
        public double Phase { get; set; }
        public ActiveCanvas() { DoubleBuffered = true; BackColor = Color.FromArgb(10, 20, 40); Font = new Font("Segoe UI", 9f, FontStyle.Bold); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            const string text = "YOU CAN NOW PLAY — CLASSIC & DEFAULT SKINS ACTIVE";
            Size measured = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int start = (Width - measured.Width - 18) / 2;
            float radius = 3.5f + (float)(10 * Phase);
            int alpha = (int)(128 * (1 - Phase));
            using (var ring = new Pen(Color.FromArgb(alpha, 61, 220, 110), 1.5f)) e.Graphics.DrawEllipse(ring, start + 3.5f - radius, Height / 2f - radius, radius * 2, radius * 2);
            using (var dot = new SolidBrush(Color.FromArgb(61, 220, 110))) e.Graphics.FillEllipse(dot, start, Height / 2f - 3.5f, 7, 7);
            TextRenderer.DrawText(e.Graphics, text, Font, new Point(start + 18, (Height - measured.Height) / 2), Color.FromArgb(61, 220, 110), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class ProgressCanvas : Control
    {
        private int percent;
        private float displayedPercent;
        private LauncherState state;
        public double Phase { get; set; }
        public int Percent { get { return percent; } set { percent = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public LauncherState State { get { return state; } set { state = value; Invalidate(); } }

        public ProgressCanvas()
        {
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            BackColor = Color.FromArgb(13, 26, 48);
        }

        public void AnimateStep()
        {
            displayedPercent += (percent - displayedPercent) * .16f;
            if (Math.Abs(percent - displayedPercent) < .1f) displayedPercent = percent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var border = new Pen(Color.FromArgb(42, 58, 87)))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            var fill = state == LauncherState.Active ? Color.FromArgb(47, 190, 92) :
                       state == LauncherState.Error ? Color.FromArgb(212, 69, 58) : Color.FromArgb(46, 99, 224);
            int width = (int)((Width - 2) * displayedPercent / 100f);
            if (width > 0)
            {
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, 1, 1, width, Height - 2);
                if (state != LauncherState.Active)
                {
                    var oldClip = e.Graphics.Clip;
                    e.Graphics.SetClip(new Rectangle(1, 1, width, Height - 2));
                    int shimmerX = (int)(-Width * .4 + Phase * Width * 1.8);
                    Point[] band = { new Point(shimmerX, 1), new Point(shimmerX + 42, 1), new Point(shimmerX + 18, Height - 1), new Point(shimmerX - 24, Height - 1) };
                    using (var shimmer = new SolidBrush(Color.FromArgb(72, 255, 255, 255))) e.Graphics.FillPolygon(shimmer, band);
                    e.Graphics.Clip = oldClip;
                }
                if (state != LauncherState.Active)
                    using (var edge = new Pen(Color.FromArgb(212, 175, 55), 2)) e.Graphics.DrawLine(edge, width, 1, width, Height - 2);
            }
            string text = state == LauncherState.Error ? "ERROR" : percent + "%";
            TextRenderer.DrawText(e.Graphics, text, Font, ClientRectangle, Color.FromArgb(244, 246, 250),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class SessionRecord
    {
        public string DataRoot { get; set; }
        public List<BackupRecord> Backups { get; set; }
        public List<string> ModIds { get; set; }
    }

    internal sealed class BackupRecord
    {
        public string Path { get; set; }
        public string Backup { get; set; }
        public bool Existed { get; set; }
    }

    internal static class Json
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 };
        public static Dictionary<string, object> ReadObject(string path)
        {
            return Serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
        }
        public static T Read<T>(string path) { return Serializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)); }
        public static void Write(string path, object value)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, Serializer.Serialize(value), new UTF8Encoding(false));
        }
    }

    internal sealed class LtkService
    {
        private readonly string root;
        private readonly string sessionPath;
        private readonly string dataRoot;
        private readonly string backupRoot;
        private SessionRecord session;

        public LtkService(string applicationRoot)
        {
            root = applicationRoot;
            sessionPath = Path.Combine(root, "state", "ltk-session.json");
            dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dev.leaguetoolkit.manager");
            backupRoot = Path.Combine(root, "state", "ltk-backup");
        }

        public int Start(string championsDirectory, IProgress<LoadProgress> progress)
        {
            Restore();
            EnsureNoConflictingProcesses();
            string ltkExe = Path.Combine(root, "LTK Manager", "ltk-manager.exe");
            string modsRoot = Path.Combine(root, "mods");
            if (!File.Exists(ltkExe)) throw new InvalidOperationException("LTK engine not found.");
            string[] packages = Directory.Exists(modsRoot) ? Directory.GetFiles(modsRoot, "*.fantome").OrderBy(x => x).ToArray() : new string[0];
            if (packages.Length == 0) throw new InvalidOperationException("No .fantome packages were found.");

            string profileRoot = Path.Combine(dataRoot, "profiles", "default");
            Directory.CreateDirectory(Path.Combine(dataRoot, "archives"));
            Directory.CreateDirectory(Path.Combine(dataRoot, "mods"));
            Directory.CreateDirectory(profileRoot);
            Directory.CreateDirectory(backupRoot);
            string settingsPath = Path.Combine(dataRoot, "settings.json");
            string libraryPath = Path.Combine(dataRoot, "library.json");
            string overlayPath = Path.Combine(profileRoot, "overlay.json");

            session = new SessionRecord { DataRoot = dataRoot, Backups = new List<BackupRecord>(), ModIds = new List<string>() };
            // The selected PBE installation is the engine's persistent target. Restoring an
            // older settings.json on shutdown can silently switch LTK back to live League.
            foreach (string path in new[] { libraryPath, overlayPath })
            {
                string backup = Path.Combine(backupRoot, Path.GetFileName(path));
                bool existed = File.Exists(path);
                if (existed) File.Copy(path, backup, true);
                session.Backups.Add(new BackupRecord { Path = path, Backup = backup, Existed = existed });
            }
            SaveSession();

            try
            {
                var library = File.Exists(libraryPath) ? Json.ReadObject(libraryPath) : new Dictionary<string, object> { { "version", 1 }, { "mods", new object[0] } };
                var entries = ToObjectList(library.ContainsKey("mods") ? library["mods"] : null);
                for (int i = 0; i < packages.Length; i++)
                {
                    var metadata = ReadFantomeMetadata(packages[i]);
                    string id = Guid.NewGuid().ToString();
                    session.ModIds.Add(id);
                    SaveSession();
                    File.Copy(packages[i], Path.Combine(dataRoot, "archives", id + ".fantome"), true);
                    string configDirectory = Path.Combine(dataRoot, "mods", id);
                    Directory.CreateDirectory(configDirectory);
                    string displayName = GetString(metadata, "Name", Path.GetFileNameWithoutExtension(packages[i]));
                    var config = new Dictionary<string, object> {
                        { "name", Slug(displayName) }, { "display_name", displayName },
                        { "version", GetString(metadata, "Version", "1.0") },
                        { "description", GetString(metadata, "Description", "") },
                        { "authors", new[] { GetString(metadata, "Author", "Unknown") } },
                        { "layers", new object[] { new Dictionary<string, object> { { "name", "base" }, { "priority", 0 }, { "description", "Base layer of the mod" } } } }
                    };
                    Json.Write(Path.Combine(configDirectory, "mod.config.json"), config);
                    entries.Add(new Dictionary<string, object> { { "id", id }, { "installedAt", DateTime.UtcNow.ToString("o") }, { "format", "fantome" } });
                    progress.Report(new LoadProgress { Percent = Math.Max(1, 30 * (i + 1) / packages.Length), Status = "LOADING CLASSIC SKINS..." });
                }
                library["mods"] = entries.ToArray();
                // LTK 1.11 reads enabled mods from the active library profile. Merely adding
                // archives (or writing overlay.json) imports them but leaves them disabled.
                var profiles = ToObjectList(library.ContainsKey("profiles") ? library["profiles"] : null);
                string activeProfileId = library.ContainsKey("activeProfileId") ? Convert.ToString(library["activeProfileId"]) : "";
                foreach (object profileValue in profiles)
                {
                    var profile = profileValue as Dictionary<string, object>;
                    if (profile == null) continue;
                    string profileId = profile.ContainsKey("id") ? Convert.ToString(profile["id"]) : "";
                    if (activeProfileId.Length == 0 || string.Equals(profileId, activeProfileId, StringComparison.OrdinalIgnoreCase))
                    {
                        profile["enabledMods"] = session.ModIds.ToArray();
                        profile["modOrder"] = session.ModIds.ToArray();
                        break;
                    }
                }
                library["profiles"] = profiles.ToArray();
                Json.Write(libraryPath, library);
                Json.Write(overlayPath, new Dictionary<string, object> {
                    { "version", 5 }, { "enabledMods", session.ModIds.ToArray() },
                    { "modFingerprints", new Dictionary<string, object>() }, { "gameFingerprint", 0 },
                    { "blockedWads", new[] { "map22.wad.client", "scripts.wad.client" } },
                    { "stringOverrideLocales", new object[0] }, { "wadFingerprints", new Dictionary<string, object>() },
                    { "linkedBinOffenders", new object[0] }
                });
                WriteSettings(settingsPath, championsDirectory);
                SaveSession();
                Process.Start(new ProcessStartInfo {
                    FileName = ltkExe, WorkingDirectory = Path.GetDirectoryName(ltkExe),
                    UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden
                });
                return packages.Length;
            }
            catch { Restore(); throw; }
        }

        public void Restore()
        {
            Kill("ltk-manager"); Kill("ltk_patcher_host"); Kill("cslol-host");
            if (!File.Exists(sessionPath)) return;
            try { session = Json.Read<SessionRecord>(sessionPath); } catch { return; }
            Thread.Sleep(300);
            foreach (var item in session.Backups ?? new List<BackupRecord>())
            {
                try { if (item.Existed && File.Exists(item.Backup)) File.Copy(item.Backup, item.Path, true); else if (!item.Existed && File.Exists(item.Path)) File.Delete(item.Path); } catch { }
            }
            foreach (string id in session.ModIds ?? new List<string>())
            {
                TryDeleteFile(Path.Combine(session.DataRoot, "archives", id + ".fantome"));
                TryDeleteDirectory(Path.Combine(session.DataRoot, "mods", id));
            }
            TryDeleteFile(sessionPath);
            TryDeleteDirectory(backupRoot);
            session = null;
        }

        public bool IsReady
        {
            get
            {
                return Process.GetProcessesByName("cslol-host").Any(p => !p.HasExited)
                    || Process.GetProcessesByName("ltk_patcher_host").Any(p => !p.HasExited)
                    || Process.GetProcessesByName("ltk-manager").Any(p => !p.HasExited && p.Responding);
            }
        }
        public void HideManagerWindow()
        {
            foreach (var process in Process.GetProcessesByName("ltk-manager"))
            {
                try { if (process.MainWindowHandle != IntPtr.Zero) ShowWindowAsync(process.MainWindowHandle, 0); } catch { }
            }
        }

        private void EnsureNoConflictingProcesses()
        {
            if (Process.GetProcessesByName("ltk-manager").Length + Process.GetProcessesByName("cslol-host").Length + Process.GetProcessesByName("ltk_patcher_host").Length > 0)
                throw new InvalidOperationException("Close LTK Manager before starting Classic Skin Morph.");
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessId FROM Win32_Process WHERE Name='League of Legends.exe'"))
                if (searcher.Get().Count > 0)
                    throw new InvalidOperationException("League processes are still running or stuck after a crash. Restart Windows, then start Classic Skin Morph before League.");
        }

        private void WriteSettings(string path, string championsDirectory)
        {
            string leaguePath = Path.GetFullPath(Path.Combine(championsDirectory, "..", "..", ".."));
            Dictionary<string, object> settings = File.Exists(path) ? Json.ReadObject(path) : DefaultSettings(leaguePath);
            settings["leaguePath"] = leaguePath.Replace('\\', '/'); settings["firstRunComplete"] = true;
            settings["minimizeToTray"] = true; settings["startInTray"] = true;
            settings["startInTrayUnlessUpdate"] = false; settings["alwaysStartPatcher"] = true;
            Json.Write(path, settings);
        }

        private static Dictionary<string, object> DefaultSettings(string path)
        {
            return new Dictionary<string, object> {
                {"leaguePath",path.Replace('\\','/')},{"modStoragePath",null},{"workshopPath",null},{"firstRunComplete",true},{"theme","system"},
                {"accentColor",new Dictionary<string,object>{{"preset",null},{"customHue",null}}},{"backdropImage",null},{"backdropBlur",null},
                {"libraryViewMode",null},{"patchTft",false},{"minimizeToTray",true},{"startInTray",true},{"autoRun",false},
                {"startInTrayUnlessUpdate",false},{"alwaysStartPatcher",true},{"migrationDismissed",false},{"reloadModsHotkey",null},
                {"killLeagueHotkey",null},{"killLeagueStopsPatcher",true},{"trustedDomains",new[]{"runeforge.dev","divineskins.gg"}},
                {"watcherEnabled",false},{"blockScriptsWad",true},{"linkedBinCheckEnabled",true},{"wadBlocklist",new object[0]},
                {"authorProfiles",new object[0]},{"defaultAuthorProfileId",null},{"hasSeenHddWarning",true},{"elevateInjector",false},
                {"autoCategorizationEnabled",true},{"enforceSkinhackScan",true},{"applyStringOverridesToAllLocales",false}
            };
        }

        private void SaveSession() { Json.Write(sessionPath, session); }
        private static List<object> ToObjectList(object value)
        {
            var list = new List<object>(); var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string)) foreach (object item in enumerable) list.Add(item);
            return list;
        }
        private static Dictionary<string, object> ReadFantomeMetadata(string path)
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.GetEntry("META/info.json");
                if (entry == null) throw new InvalidOperationException("META/info.json is missing from " + Path.GetFileName(path));
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            }
        }
        private static string GetString(Dictionary<string, object> map, string key, string fallback) { object value; return map.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : fallback; }
        private static string Slug(string value) { return Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-'); }
        private static void Kill(string name) { foreach (var p in Process.GetProcessesByName(name)) try { p.Kill(); p.WaitForExit(1000); } catch { } }
        private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
        private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    }

    internal sealed class MainForm : Form
    {
        private readonly string root;
        private readonly LtkService ltk;
        private readonly StatusCanvas status;
        private readonly ActiveCanvas active;
        private readonly ProgressCanvas progressBar;
        private readonly SweepCanvas accent;
        private readonly GlowCanvas logoGlow;
        private readonly PictureBox logo;
        private readonly System.Windows.Forms.Timer monitorTimer;
        private readonly System.Windows.Forms.Timer animationTimer;
        private readonly Stopwatch animationClock;
        private LauncherState state = LauncherState.Loading;
        private int packageCount;
        private int logStartLine;
        private string logPath;
        private bool sessionStarted;

        public MainForm()
        {
            root = AppDomain.CurrentDomain.BaseDirectory;
            ltk = new LtkService(root);
            Text = "Classic Skin Morph";
            ClientSize = new Size(500, 350);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 20, 40);
            ForeColor = Color.FromArgb(244, 246, 250);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            accent = new SweepCanvas { Location = new Point(0, 0), Size = new Size(500, 3) };
            Controls.Add(accent);

            string logoPath = Path.Combine(root, "assets", "classic-skin-morph-logo.png");
            logoGlow = new GlowCanvas { Location = new Point(100, 0), Size = new Size(300, 142) };
            Controls.Add(logoGlow);
            logo = new PictureBox { Location = new Point(20, 16), Size = new Size(260, 110), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            if (File.Exists(logoPath))
            {
                using (var source = Image.FromFile(logoPath)) logo.Image = new Bitmap(source);
            }
            logoGlow.Controls.Add(logo);

            status = new StatusCanvas { Location = new Point(20, 130), Size = new Size(460, 20), StatusText = "LOADING CLASSIC SKINS...", State = LauncherState.Loading };
            Controls.Add(status);

            progressBar = new ProgressCanvas { Location = new Point(40, 160), Size = new Size(420, 32), Percent = 0, State = LauncherState.Loading };
            Controls.Add(progressBar);

            active = new ActiveCanvas { Location = new Point(20, 200), Size = new Size(460, 20) };
            active.Visible = false;
            Controls.Add(active);

            Controls.Add(new Panel { Location = new Point(40, 230), Size = new Size(420, 1), BackColor = Color.FromArgb(35, 50, 80) });
            var patchTitle = MakeLabel(new Point(20, 239), new Size(460, 16), 8.5f, FontStyle.Bold, ContentAlignment.MiddleCenter, Color.FromArgb(212, 175, 55));
            patchTitle.Text = "PATCH NOTE V0.5";
            Controls.Add(patchTitle);
            var notes = MakeLabel(new Point(60, 266), new Size(400, 74), 8.5f, FontStyle.Regular, ContentAlignment.TopLeft, Color.FromArgb(170, 180, 198));
            notes.Text = "- Added Classic Jax and Master Yi PBE\r\n- Fiddlesticks, Garen, Heimerdinger and Karthus\r\n- Kassadin, Lux, Morgana, Nasus and Skarner\r\n- Teemo and Twitch";
            Controls.Add(notes);

            // WinForms inserts newly added controls at the front of the Z-order.
            // Keep the animated glow behind the static, sharp logo and all text.
            logoGlow.SendToBack();
            logo.BringToFront();
            status.BringToFront();
            accent.BringToFront();

            monitorTimer = new System.Windows.Forms.Timer { Interval = 500 };
            monitorTimer.Tick += TimerTick;
            animationClock = Stopwatch.StartNew();
            animationTimer = new System.Windows.Forms.Timer { Interval = 40 };
            animationTimer.Tick += AnimationTick;
            animationTimer.Start();
            Shown += async delegate { await BeginLoading(); };
            FormClosing += OnClosing;
        }

        private async Task BeginLoading()
        {
            try
            {
                string champions = EnsurePbeConfiguration();
                if (champions == null) { Close(); return; }
                HideEnemySummonerEmotes(champions);
                logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dev.leaguetoolkit.manager", "logs", "ltk-manager." + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                logStartLine = File.Exists(logPath) ? File.ReadLines(logPath).Count() : 0;
                SetState(LauncherState.Loading, "LOADING CLASSIC SKINS...", 0);
                var reporter = new Progress<LoadProgress>(p => SetState(LauncherState.Loading, "LOADING CLASSIC SKINS...", p.Percent));
                packageCount = await Task.Run(() => ltk.Start(champions, reporter));
                sessionStarted = true;
                status.StatusText = "LOADING CLASSIC SKINS...";
                monitorTimer.Start();
            }
            catch (Exception ex) { SetState(LauncherState.Error, ex.Message.ToUpperInvariant(), progressBar.Percent); }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            ltk.HideManagerWindow();
            if (ltk.IsReady)
            {
                SetState(LauncherState.Active, "CLASSIC SKINS LOADED", 100);
                monitorTimer.Interval = 1000;
                return;
            }
            if (!sessionStarted || packageCount <= 0 || !File.Exists(logPath)) return;
            try
            {
                int built = File.ReadLines(logPath).Skip(logStartLine).Count(line => line.Contains("Patched WAD complete"));
                built = Math.Min(packageCount, built);
                int value = Math.Min(95, 30 + 65 * built / packageCount);
                SetState(LauncherState.Loading, "LOADING CLASSIC SKINS...", value);
            }
            catch { }
        }

        private void AnimationTick(object sender, EventArgs e)
        {
            double seconds = animationClock.Elapsed.TotalSeconds;
            accent.Phase = (seconds % 3.2) / 3.2;
            logoGlow.Phase = (seconds % 2.6) / 2.6;
            status.Phase = (seconds % 1.4) / 1.4;
            progressBar.Phase = (seconds % 1.8) / 1.8;
            active.Phase = (seconds % 1.6) / 1.6;
            accent.Invalidate(); logoGlow.Invalidate(); status.Invalidate(); active.Invalidate();
            progressBar.AnimateStep();
        }

        private void SetState(LauncherState newState, string message, int percent)
        {
            state = newState;
            progressBar.State = newState;
            progressBar.Percent = newState == LauncherState.Active ? 100 : percent;
            if (newState == LauncherState.Active)
            {
                status.StatusText = "CLASSIC SKINS LOADED";
                status.State = LauncherState.Active;
                active.Visible = true;
            }
            else if (newState == LauncherState.Error)
            {
                status.StatusText = message;
                status.State = LauncherState.Error;
                active.Visible = false;
            }
            else
            {
                status.StatusText = message;
                status.State = LauncherState.Loading;
                active.Visible = false;
            }
        }

        private string EnsurePbeConfiguration()
        {
            string configPath = Path.Combine(root, "config.json");
            string examplePath = Path.Combine(root, "config.example.json");
            Dictionary<string, object> config;
            if (File.Exists(configPath)) config = Json.ReadObject(configPath);
            else if (File.Exists(examplePath)) config = Json.ReadObject(examplePath);
            else config = new Dictionary<string, object>();
            object value; string champions = config.TryGetValue("pbeChampionsDirectory", out value) ? Convert.ToString(value) : "";
            if (!ValidChampionsDirectory(champions))
            {
                using (var dialog = new FolderBrowserDialog { Description = "First-time setup: select your League of Legends PBE folder", ShowNewFolderButton = false })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return null;
                    string selected = dialog.SelectedPath;
                    string direct = Path.Combine(selected, "Game", "DATA", "FINAL", "Champions");
                    string nested = Path.Combine(selected, "Riot Games", "League of Legends (PBE)", "Game", "DATA", "FINAL", "Champions");
                    champions = ValidChampionsDirectory(selected) ? selected : ValidChampionsDirectory(direct) ? direct : ValidChampionsDirectory(nested) ? nested : null;
                    if (champions == null) { MessageBox.Show(this, "Invalid PBE folder.", "Classic Skin Morph", MessageBoxButtons.OK, MessageBoxIcon.Error); return null; }
                }
            }
            config["pbeChampionsDirectory"] = Path.GetFullPath(champions);
            if (!config.ContainsKey("modLibrary")) config["modLibrary"] = "mods";
            Json.Write(configPath, config);
            return Path.GetFullPath(champions);
        }

        private static bool ValidChampionsDirectory(string path)
        {
            try { return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && Directory.GetFiles(path, "*.wad.client").Length > 0; } catch { return false; }
        }

        private static void HideEnemySummonerEmotes(string championsDirectory)
        {
            string installRoot = Path.GetFullPath(Path.Combine(championsDirectory, "..", "..", "..", ".."));
            string configRoot = Path.Combine(installRoot, "Config");
            string gameConfig = Path.Combine(configRoot, "game.cfg");
            if (File.Exists(gameConfig))
            {
                string text = File.ReadAllText(gameConfig);
                string updated = Regex.Replace(text, @"(?im)^HideEnemySummonerEmotes\s*=\s*\d+\s*$", "HideEnemySummonerEmotes=1");
                if (updated == text && !Regex.IsMatch(text, @"(?im)^HideEnemySummonerEmotes\s*="))
                    updated += (updated.EndsWith("\n") ? "" : Environment.NewLine) + "HideEnemySummonerEmotes=1" + Environment.NewLine;
                if (updated != text) File.WriteAllText(gameConfig, updated, new UTF8Encoding(false));
            }

            string persisted = Path.Combine(configRoot, "PersistedSettings.json");
            if (File.Exists(persisted))
            {
                string text = File.ReadAllText(persisted);
                string pattern = @"(\""name\""\s*:\s*\""HideEnemySummonerEmotes\""\s*,\s*\""value\""\s*:\s*\""?)\d+(\""?)";
                string updated = Regex.Replace(text, pattern, "${1}1$2", RegexOptions.IgnoreCase);
                if (updated != text) File.WriteAllText(persisted, updated, new UTF8Encoding(false));
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            monitorTimer.Stop();
            animationTimer.Stop();
            if (!sessionStarted && !File.Exists(Path.Combine(root, "state", "ltk-session.json"))) return;
            status.StatusText = "STOPPING AND CLEANING UP SKINS...";
            status.State = LauncherState.Loading;
            active.Visible = false;
            Refresh();
            ltk.Restore();
        }

        private static Label MakeLabel(Point location, Size size, float fontSize, FontStyle style, ContentAlignment alignment, Color color)
        {
            return new Label { Location = location, Size = size, Font = new Font("Segoe UI", fontSize, style), TextAlign = alignment, ForeColor = color, BackColor = Color.Transparent };
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (!args.Any(value => string.Equals(value, "--skip-update", StringComparison.OrdinalIgnoreCase)))
            {
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string updater = Path.Combine(root, "ClassicSkinMorph-Launcher.ps1");
                try
                {
                    if (!File.Exists(updater))
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        using (var client = new WebClient())
                        {
                            client.Headers[HttpRequestHeader.UserAgent] = "ClassicSkinMorph-Bootstrap";
                            client.DownloadFile("https://raw.githubusercontent.com/Niftix/ClassicSkinMorph/master/ClassicSkinMorph-Launcher.ps1", updater);
                        }
                    }
                    Process.Start(new ProcessStartInfo {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + updater + "\"",
                        WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    return;
                }
                catch (Exception ex)
                {
                    bool localInstallComplete = File.Exists(Path.Combine(root, "LTK Manager", "ltk-manager.exe")) &&
                        Directory.Exists(Path.Combine(root, "mods")) && Directory.Exists(Path.Combine(root, "assets"));
                    if (!localInstallComplete)
                    {
                        MessageBox.Show("Unable to download Classic Skin Morph files from GitHub.\r\n\r\n" + ex.Message,
                            "Classic Skin Morph", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    // A complete local installation remains usable when GitHub is temporarily unavailable.
                }
            }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e) { LogCrash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { LogCrash(e.ExceptionObject as Exception); };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainForm()); }
            catch (Exception ex) { LogCrash(ex); }
        }

        private static void LogCrash(Exception exception)
        {
            try
            {
                string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "crash.log"), DateTime.Now.ToString("o") + Environment.NewLine + (exception == null ? "Unknown error" : exception.ToString()) + Environment.NewLine);
            }
            catch { }
        }
    }
}
