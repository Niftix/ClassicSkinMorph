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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

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

    internal sealed class HoverIconButton : Control
    {
        public double HoverAmount { get; private set; }
        private bool hovered;
        public HoverIconButton()
        {
            DoubleBuffered = true; Cursor = Cursors.Hand; TabStop = false;
            MouseEnter += delegate { hovered = true; };
            MouseLeave += delegate { hovered = false; };
        }
        public void AnimateStep()
        {
            double target = hovered ? 1.0 : 0.0;
            HoverAmount += (target - HoverAmount) * .22;
            if (Math.Abs(target - HoverAmount) < .01) HoverAmount = target;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int gold = (int)(90 + 130 * HoverAmount);
            int fill = (int)(12 + 25 * HoverAmount);
            using (var background = new SolidBrush(Color.FromArgb(fill, 201, 169, 97)))
                e.Graphics.FillRectangle(background, new Rectangle(1, 1, Width - 3, Height - 3));
            using (var border = new Pen(Color.FromArgb(gold, 201, 169, 97), 1f + (float)HoverAmount))
                e.Graphics.DrawRectangle(border, new Rectangle(1, 1, Width - 3, Height - 3));
            Color textColor = Blend(Color.FromArgb(170, 180, 198), Color.FromArgb(244, 227, 168), HoverAmount);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        private static Color Blend(Color from, Color to, double amount)
        {
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount),
                (int)(from.B + (to.B - from.B) * amount));
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

        public void Reset()
        {
            percent = 0;
            displayedPercent = 0;
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

    internal sealed class UserPreferences
    {
        public bool LoadingScreen { get; set; }
        public bool RankedBorders { get; set; }
        public List<string> DisabledChampions { get; set; }
        [ScriptIgnore]
        public string ActiveJadeTier { get; set; }
        public UserPreferences() { LoadingScreen = true; RankedBorders = true; DisabledChampions = new List<string>(); }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly TreeView tree;
        private bool syncing;
        public UserPreferences Preferences { get; private set; }

        public SettingsForm(string root, UserPreferences current)
        {
            Text = "Classic Skin Morph Settings"; ClientSize = new Size(390, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(10, 20, 40);
            ForeColor = Color.FromArgb(244, 246, 250); ShowInTaskbar = false;
            tree = new TreeView { Location = new Point(16, 16), Size = new Size(358, 400), CheckBoxes = true,
                BackColor = Color.FromArgb(13, 26, 48), ForeColor = Color.FromArgb(220, 225, 235), BorderStyle = BorderStyle.FixedSingle };
            var champions = new TreeNode("Champions") { Name = "champions", Checked = true };
            string mods = Path.Combine(root, "mods");
            foreach (string package in Directory.GetFiles(mods, "*.fantome").OrderBy(path => path))
            {
                string key = Path.GetFileNameWithoutExtension(package).ToLowerInvariant();
                if (IsLoadingPackage(key)) continue;
                string display = Regex.Replace(key, "-(classic|base)$", "", RegexOptions.IgnoreCase).Replace('-', ' ');
                display = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(display);
                var node = new TreeNode(display) { Name = key, Tag = "champion", Checked = !current.DisabledChampions.Contains(key) };
                champions.Nodes.Add(node);
            }
            champions.Checked = champions.Nodes.Cast<TreeNode>().All(node => node.Checked);
            var loading = new TreeNode("Loading Screen") { Name = "loading", Checked = current.LoadingScreen };
            loading.Nodes.Add(new TreeNode("Ranked Borders") { Name = "ranked", Tag = "ranked", Checked = current.LoadingScreen && current.RankedBorders });
            tree.Nodes.Add(champions); tree.Nodes.Add(loading); tree.ExpandAll();
            tree.AfterCheck += TreeAfterCheck; Controls.Add(tree);
            var save = new Button { Text = "SAVE", DialogResult = DialogResult.OK, Location = new Point(218, 430), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = "CANCEL", DialogResult = DialogResult.Cancel, Location = new Point(299, 430), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat };
            save.FlatAppearance.BorderColor = cancel.FlatAppearance.BorderColor = Color.FromArgb(201, 169, 97);
            save.ForeColor = cancel.ForeColor = Color.FromArgb(244, 227, 168); save.BackColor = cancel.BackColor = BackColor;
            Controls.Add(save); Controls.Add(cancel); AcceptButton = save; CancelButton = cancel;
            FormClosing += delegate {
                if (DialogResult != DialogResult.OK) return;
                Preferences = new UserPreferences { LoadingScreen = loading.Checked, RankedBorders = loading.Checked && loading.Nodes[0].Checked,
                    DisabledChampions = champions.Nodes.Cast<TreeNode>().Where(node => !node.Checked).Select(node => node.Name).ToList() };
            };
        }

        private void TreeAfterCheck(object sender, TreeViewEventArgs e)
        {
            if (syncing) return; syncing = true;
            try {
                foreach (TreeNode child in e.Node.Nodes) child.Checked = e.Node.Checked;
                // Champion children control the aggregate Champions checkbox.
                // Loading Screen and Ranked Borders are independent: turning
                // ranked borders off must keep the S1 loading screen enabled.
                if (e.Node.Parent != null && e.Node.Parent.Name == "champions")
                    e.Node.Parent.Checked = e.Node.Parent.Nodes.Cast<TreeNode>().All(node => node.Checked);
            } finally { syncing = false; }
        }
        internal static bool IsLoadingPackage(string key)
        {
            return key.StartsWith("loading-screen-") || key.Contains("ranked-borders");
        }
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

        public int Start(string championsDirectory, UserPreferences preferences, IProgress<LoadProgress> progress)
        {
            Restore();
            EnsureNoConflictingProcesses();
            string ltkExe = Path.Combine(root, "LTK Manager", "ltk-manager.exe");
            string modsRoot = Path.Combine(root, "mods");
            if (!File.Exists(ltkExe)) throw new InvalidOperationException("LTK engine not found.");
            const string season1Key = "loading-screen-season1-jade";
            string[] packages = Directory.Exists(modsRoot) ? Directory.GetFiles(modsRoot, "*.fantome").Where(path => {
                string key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                // The S1 package is always the shared loading-screen base. Ranked
                // frames are now drawn per player by the runtime overlay; loading a
                // rank package here would incorrectly apply one frame to everybody.
                if (key.StartsWith("loading-screen-rank-")) return false;
                if (key == season1Key) return preferences.LoadingScreen;
                if (key == "loading-screen-black") return preferences.LoadingScreen;
                if (key.Contains("ranked-borders")) return false;
                if (key.StartsWith("loading-screen-")) return false;
                return !preferences.DisabledChampions.Contains(key);
            }).OrderBy(x => x).ToArray() : new string[0];
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

        public void ResetForReload()
        {
            Restore();
            // Reset the active UI output and overlay metadata, but preserve
            // unchanged multi-gigabyte WADs (especially Map11). Deleting the
            // whole overlay makes LTK rebuild every map and can exhaust its
            // process before the patcher host starts.
            string profileRoot = Path.Combine(dataRoot, "profiles", "default");
            TryDeleteFile(Path.Combine(profileRoot, "overlay", "DATA", "FINAL", "UI.wad.client"));
            TryDeleteFile(Path.Combine(profileRoot, "override_meta.bin"));
            TryDeleteFile(Path.Combine(profileRoot, "overlay.json"));
            TryDeleteFile(Path.Combine(dataRoot, ".overlay-build-version"));
        }

        public bool IsReady
        {
            get
            {
                return Process.GetProcessesByName("cslol-host").Any(p => !p.HasExited)
                    || Process.GetProcessesByName("ltk_patcher_host").Any(p => !p.HasExited);
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

    internal sealed class RankSnapshot
    {
        public readonly List<string> Order = new List<string>();
        public readonly List<string> Chaos = new List<string>();
        public double GameTime;
    }

    internal sealed class RankOverlayForm : Form
    {
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private readonly string root;
        private readonly List<string> orderRanks;
        private readonly List<string> chaosRanks;
        private readonly Dictionary<string, Image> frames = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public RankOverlayForm(string rootPath, IEnumerable<string> order, IEnumerable<string> chaos)
        {
            root = rootPath;
            orderRanks = order.ToList(); chaosRanks = chaos.ToList();
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            BackColor = Color.Magenta; TransparencyKey = Color.Magenta; DoubleBuffered = true;
            foreach (string tier in new[] { "wood", "silver", "gold", "plat", "diamond", "legend" })
            {
                string path = Path.Combine(root, "assets", "rank-overlays", tier + ".png");
                if (File.Exists(path)) using (var source = Image.FromFile(path)) frames[tier] = new Bitmap(source);
            }
            SyncToGameWindow();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE; return cp; } }

        public void SyncToGameWindow()
        {
            Process game = Process.GetProcessesByName("League of Legends").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            NativeRect rect;
            if (game != null && GetWindowRect(game.MainWindowHandle, out rect) && rect.Right > rect.Left && rect.Bottom > rect.Top)
                Bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            else Bounds = Screen.PrimaryScreen.Bounds;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            DrawTeam(e.Graphics, orderRanks, true); DrawTeam(e.Graphics, chaosRanks, false);
        }

        private void DrawTeam(Graphics graphics, List<string> ranks, bool top)
        {
            if (ranks.Count == 0) return;
            float cardHeight = Math.Min(Height * 0.445f, Width * 0.40f);
            float cardWidth = cardHeight * 401f / 726f;
            float gap = Math.Max(8f, cardWidth * 0.065f);
            float groupWidth = ranks.Count * cardWidth + (ranks.Count - 1) * gap;
            float left = (Width - groupWidth) / 2f;
            float y = top ? Height * 0.022f : Height - Height * 0.022f - cardHeight;
            for (int index = 0; index < ranks.Count; index++)
            {
                string key = NormalizeTier(ranks[index]); Image frame;
                if (key == "salt" || !frames.TryGetValue(key, out frame)) continue;
                graphics.DrawImage(frame, left + index * (cardWidth + gap), y, cardWidth, cardHeight);
            }
        }

        private static string NormalizeTier(string tier)
        {
            tier = (tier ?? "").Trim().ToLowerInvariant();
            if (tier == "platinum") return "plat";
            if (tier == "challenger") return "legend";
            return tier;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (Image frame in frames.Values) frame.Dispose();
            base.Dispose(disposing);
        }
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
        private readonly HoverIconButton launchButton;
        private readonly HoverIconButton githubButton;
        private readonly HoverIconButton settingsButton;
        private readonly System.Windows.Forms.Timer monitorTimer;
        private readonly System.Windows.Forms.Timer rankOverlayTimer;
        private readonly System.Windows.Forms.Timer animationTimer;
        private readonly Stopwatch animationClock;
        private readonly Stopwatch loadingClock = new Stopwatch();
        private LauncherState state = LauncherState.Loading;
        private int packageCount;
        private int logStartLine;
        private string logPath;
        private bool sessionStarted;
        private bool isLoading;
        private readonly string preferencesPath;
        private UserPreferences preferences;
        private string championsDirectory;
        private RankOverlayForm rankOverlay;
        private bool rankLookupRunning;
        private bool rankOverlayHandled;

        public MainForm()
        {
            root = AppDomain.CurrentDomain.BaseDirectory;
            ltk = new LtkService(root);
            preferencesPath = Path.Combine(root, "state", "user-preferences.json");
            preferences = LoadPreferences();
            Text = "Classic Skin Morph";
            ClientSize = new Size(560, 307);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 20, 40);
            ForeColor = Color.FromArgb(244, 246, 250);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            accent = new SweepCanvas { Location = new Point(0, 0), Size = new Size(560, 3) };
            Controls.Add(accent);

            launchButton = new HoverIconButton { Location = new Point(12, 12), Size = new Size(72, 28), Text = "LOAD", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            githubButton = new HoverIconButton { Location = new Point(12, 45), Size = new Size(72, 28), Text = "GITHUB", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            settingsButton = new HoverIconButton { Location = new Point(516, 12), Size = new Size(32, 28), Text = "⚙", Font = new Font("Segoe UI Symbol", 13f, FontStyle.Regular) };
            Controls.Add(launchButton);
            Controls.Add(githubButton);
            Controls.Add(settingsButton);
            launchButton.Click += async delegate { await BeginLoading(); };
            githubButton.Click += delegate {
                try { Process.Start(new ProcessStartInfo("https://github.com/Xitfin/ClassicSkinMorph") { UseShellExecute = true }); }
                catch { }
            };
            settingsButton.Click += SettingsClicked;

            string logoPath = Path.Combine(root, "assets", "classic-skin-morph-logo.png");
            logoGlow = new GlowCanvas { Location = new Point(130, 0), Size = new Size(300, 142) };
            Controls.Add(logoGlow);
            logo = new PictureBox { Location = new Point(20, 16), Size = new Size(260, 110), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            if (File.Exists(logoPath))
            {
                using (var source = Image.FromFile(logoPath)) logo.Image = new Bitmap(source);
            }
            logoGlow.Controls.Add(logo);

            status = new StatusCanvas { Location = new Point(50, 188), Size = new Size(460, 20), StatusText = "READY TO LOAD", State = LauncherState.Loading };
            Controls.Add(status);

            progressBar = new ProgressCanvas { Location = new Point(70, 211), Size = new Size(420, 32), Percent = 0, State = LauncherState.Loading };
            Controls.Add(progressBar);

            active = new ActiveCanvas { Location = new Point(50, 243), Size = new Size(460, 20) };
            active.Visible = false;
            Controls.Add(active);

            var about = new RichTextBox {
                Location = new Point(60, 134), Size = new Size(440, 44), BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(10, 20, 40), ForeColor = Color.FromArgb(170, 180, 198),
                Font = new Font("Segoe UI", 8.5f), ReadOnly = true, TabStop = false,
                ScrollBars = RichTextBoxScrollBars.None, DetectUrls = false
            };
            about.Text = "Classic Skin Morph is a free and open-source tool based on LTK Manager. Designed for nostalgic players, it restores legacy assets to transform your game and let you relive the classic Rift experience !";
            about.SelectAll(); about.SelectionAlignment = HorizontalAlignment.Center;
            foreach (string emphasized in new[] { "Classic Skin Morph", "LTK Manager" })
            {
                int index = about.Text.IndexOf(emphasized, StringComparison.Ordinal);
                if (index >= 0) { about.Select(index, emphasized.Length); about.SelectionFont = new Font("Segoe UI", 8.5f, FontStyle.Bold); }
            }
            about.Select(0, 0);
            Controls.Add(about);

            Controls.Add(new Panel { Location = new Point(70, 268), Size = new Size(420, 1), BackColor = Color.FromArgb(35, 50, 80) });
            var patchTitle = MakeLabel(new Point(50, 277), new Size(460, 16), 8.5f, FontStyle.Bold, ContentAlignment.MiddleCenter, Color.FromArgb(212, 175, 55));
            patchTitle.Text = "▼ PATCH NOTE V1.0";
            patchTitle.Cursor = Cursors.Hand;
            Controls.Add(patchTitle);
            var notes = MakeLabel(new Point(90, 303), new Size(400, 82), 8.5f, FontStyle.Regular, ContentAlignment.TopLeft, Color.FromArgb(170, 180, 198));
            notes.Text = "- Authentic Season 1 loading screen and classic UI\r\n- Jade rank-aware S3 borders from Salt to Legend\r\n- Classic splash arts, HUD icons, and legacy models\r\n- Improved PBE compatibility and LTK integration";
            notes.Visible = false;
            Controls.Add(notes);
            bool patchExpanded = false;
            EventHandler togglePatch = delegate {
                patchExpanded = !patchExpanded;
                notes.Visible = patchExpanded;
                patchTitle.Text = (patchExpanded ? "▲" : "▼") + " PATCH NOTE V1.0";
                ClientSize = new Size(560, patchExpanded ? 397 : 307);
            };
            patchTitle.Click += togglePatch;

            // WinForms inserts newly added controls at the front of the Z-order.
            // Keep the animated glow behind the static, sharp logo and all text.
            logoGlow.SendToBack();
            logo.BringToFront();
            status.BringToFront();
            accent.BringToFront();
            launchButton.BringToFront();
            githubButton.BringToFront();
            settingsButton.BringToFront();

            monitorTimer = new System.Windows.Forms.Timer { Interval = 500 };
            monitorTimer.Tick += TimerTick;
            rankOverlayTimer = new System.Windows.Forms.Timer { Interval = 500 };
            rankOverlayTimer.Tick += RankOverlayTick;
            rankOverlayTimer.Start();
            animationClock = Stopwatch.StartNew();
            animationTimer = new System.Windows.Forms.Timer { Interval = 40 };
            animationTimer.Tick += AnimationTick;
            animationTimer.Start();
            FormClosing += OnClosing;
        }

        private async Task BeginLoading()
        {
            if (isLoading) return;
            isLoading = true; launchButton.Enabled = false;
            try
            {
                sessionStarted = false;
                packageCount = 0;
                monitorTimer.Interval = 250;
                progressBar.Reset();
                if (launchButton.Text == "RELOAD")
                    await Task.Run(() => ltk.ResetForReload());
                string champions = EnsurePbeConfiguration();
                if (champions == null) { Close(); return; }
                championsDirectory = champions;
                preferences.ActiveJadeTier = DetectJadeTier(champions);
                if (preferences.LoadingScreen && preferences.RankedBorders && string.IsNullOrWhiteSpace(preferences.ActiveJadeTier))
                    throw new InvalidOperationException("JADE RANK COULD NOT BE DETECTED. CLICK LOAD AGAIN.");
                HideEnemySummonerEmotes(champions);
                logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dev.leaguetoolkit.manager", "logs", "ltk-manager." + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                logStartLine = File.Exists(logPath) ? File.ReadLines(logPath).Count() : 0;
                SetState(LauncherState.Loading, "LOADING CLASSIC SKINS...", 0);
                loadingClock.Restart();
                monitorTimer.Start();
                // LTK reports package-import progress up to 30%, then starts the much longer
                // overlay build. Late 30% callbacks must never move our visual fallback back.
                var reporter = new Progress<LoadProgress>(p => SetState(
                    LauncherState.Loading,
                    "LOADING CLASSIC SKINS...",
                    Math.Max(progressBar.Percent, p.Percent)));
                packageCount = await Task.Run(() => ltk.Start(champions, preferences, reporter));
                sessionStarted = true;
                status.StatusText = "LOADING CLASSIC SKINS...";
                launchButton.Text = "RELOAD";
            }
            catch (Exception ex) { SetState(LauncherState.Error, ex.Message.ToUpperInvariant(), progressBar.Percent); }
            finally { isLoading = false; launchButton.Enabled = true; }
        }

        private UserPreferences LoadPreferences()
        {
            try {
                if (File.Exists(preferencesPath)) {
                    var loaded = Json.Read<UserPreferences>(preferencesPath);
                    if (loaded.DisabledChampions == null) loaded.DisabledChampions = new List<string>();
                    return loaded;
                }
            } catch { }
            return new UserPreferences();
        }

        private void SettingsClicked(object sender, EventArgs e)
        {
            using (var dialog = new SettingsForm(root, preferences))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Preferences == null) return;
                preferences = dialog.Preferences; Json.Write(preferencesPath, preferences);
                launchButton.Text = sessionStarted ? "RELOAD" : "LOAD";
                status.StatusText = sessionStarted ? "SETTINGS CHANGED — CLICK RELOAD" : "READY TO LOAD";
                status.State = LauncherState.Loading; active.Visible = false;
            }
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
            if (loadingClock.IsRunning && progressBar.Percent >= 30)
            {
                int visualProgress = 30 + (int)Math.Min(62, loadingClock.Elapsed.TotalSeconds * 62.0 / 60.0);
                SetState(LauncherState.Loading, "LOADING CLASSIC SKINS...", Math.Max(progressBar.Percent, visualProgress));
            }
            if (!sessionStarted || packageCount <= 0)
            {
                return;
            }
            try
            {
                int built = File.Exists(logPath)
                    ? File.ReadLines(logPath).Skip(logStartLine).Count(line => line.Contains("Patched WAD complete"))
                    : 0;
                built = Math.Min(packageCount, built);
                int reportedValue = 30 + 65 * built / packageCount;
                // Some LTK builds no longer append overlay progress to the daily log.
                // Keep the UI moving while the engine rebuilds, but never claim completion
                // before cslol-host/ltk_patcher_host is actually ready.
                int fallbackValue = 30 + (int)Math.Min(62, loadingClock.Elapsed.TotalSeconds * 62.0 / 90.0);
                int value = Math.Min(95, Math.Max(progressBar.Percent, Math.Max(reportedValue, fallbackValue)));
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
            launchButton.AnimateStep();
            githubButton.AnimateStep();
            settingsButton.AnimateStep();
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

        private static string DetectJadeTier(string championsDirectory)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    string installRoot = Path.GetFullPath(Path.Combine(championsDirectory, "..", "..", "..", ".."));
                    string lockfile = Path.Combine(installRoot, "lockfile");
                    if (!File.Exists(lockfile)) throw new IOException("PBE lockfile is not ready.");
                    string lockText;
                    using (var stream = new FileStream(lockfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                        lockText = reader.ReadToEnd();
                    string[] values = lockText.Split(':');
                    if (values.Length < 4) throw new IOException("PBE lockfile is incomplete.");
                    string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + values[3]));
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    using (var client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
                        string json = client.DownloadString("https://127.0.0.1:" + values[2] + "/lol-ranked/v1/current-ranked-stats");
                        Match jade = Regex.Match(json, "\\\"JADE_RANKED_SOLO_5x5\\\"\\s*:\\s*\\{.*?\\\"tier\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (jade.Success && jade.Groups[1].Value.Length > 0) return jade.Groups[1].Value;
                    }
                }
                catch { }
                if (attempt < 4) System.Threading.Thread.Sleep(350);
            }
            return "";
        }

        private async void RankOverlayTick(object sender, EventArgs e)
        {
            bool gameRunning = Process.GetProcessesByName("League of Legends").Any();
            if (!gameRunning)
            {
                if (rankOverlay != null) { rankOverlay.Close(); rankOverlay = null; }
                rankOverlayHandled = false; rankLookupRunning = false;
                return;
            }
            if (rankOverlay != null)
            {
                rankOverlay.SyncToGameWindow();
                double gameTime = ReadLiveGameTime();
                if (gameTime > 1.0) { rankOverlay.Close(); rankOverlay = null; rankOverlayHandled = true; }
                return;
            }
            if (rankOverlayHandled || rankLookupRunning || !sessionStarted || !preferences.LoadingScreen || !preferences.RankedBorders || string.IsNullOrEmpty(championsDirectory)) return;
            rankLookupRunning = true;
            try
            {
                RankSnapshot snapshot = await Task.Run(() => ReadLiveRanks(championsDirectory));
                if (snapshot != null && snapshot.GameTime <= 1.0 && snapshot.Order.Count > 0 && snapshot.Chaos.Count > 0)
                {
                    rankOverlay = new RankOverlayForm(root, snapshot.Order, snapshot.Chaos);
                    rankOverlay.Show(); rankOverlay.BringToFront();
                }
            }
            catch { }
            finally { rankLookupRunning = false; }
        }

        private static double ReadLiveGameTime()
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                using (var client = new WebClient())
                {
                    var data = new JavaScriptSerializer().DeserializeObject(client.DownloadString("https://127.0.0.1:2999/liveclientdata/gamestats")) as Dictionary<string, object>;
                    object value; return data != null && data.TryGetValue("gameTime", out value) ? Convert.ToDouble(value) : -1;
                }
            }
            catch { return -1; }
        }

        private static RankSnapshot ReadLiveRanks(string champions)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            var serializer = new JavaScriptSerializer();
            object parsed;
            using (var live = new WebClient()) parsed = serializer.DeserializeObject(live.DownloadString("https://127.0.0.1:2999/liveclientdata/playerlist"));
            object[] players = parsed as object[];
            if (players == null || players.Length < 2) return null;

            string installRoot = Path.GetFullPath(Path.Combine(champions, "..", "..", "..", ".."));
            string[] lockValues = File.ReadAllText(Path.Combine(installRoot, "lockfile")).Split(':');
            if (lockValues.Length < 4) return null;
            string auth = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + lockValues[3]));
            string clientBase = "https://127.0.0.1:" + lockValues[2];
            var snapshot = new RankSnapshot { GameTime = ReadLiveGameTime() };
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.Authorization] = auth;
                foreach (object item in players)
                {
                    var player = item as Dictionary<string, object>; object riotValue, teamValue;
                    if (player == null || !player.TryGetValue("riotId", out riotValue) || !player.TryGetValue("team", out teamValue)) continue;
                    string summonerJson = client.DownloadString(clientBase + "/lol-summoner/v1/summoners?name=" + Uri.EscapeDataString(Convert.ToString(riotValue)));
                    var summoner = serializer.DeserializeObject(summonerJson) as Dictionary<string, object>; object puuidValue;
                    if (summoner == null || !summoner.TryGetValue("puuid", out puuidValue)) continue;
                    string rankedJson = client.DownloadString(clientBase + "/lol-ranked/v1/ranked-stats/" + Uri.EscapeDataString(Convert.ToString(puuidValue)));
                    var ranked = serializer.DeserializeObject(rankedJson) as Dictionary<string, object>; object mapValue;
                    string tier = "SALT";
                    if (ranked != null && ranked.TryGetValue("queueMap", out mapValue))
                    {
                        var queueMap = mapValue as Dictionary<string, object>; object jadeValue;
                        if (queueMap != null && queueMap.TryGetValue("JADE_RANKED_SOLO_5x5", out jadeValue))
                        {
                            var jadeRank = jadeValue as Dictionary<string, object>; object tierValue;
                            if (jadeRank != null && jadeRank.TryGetValue("tier", out tierValue) && !string.IsNullOrWhiteSpace(Convert.ToString(tierValue))) tier = Convert.ToString(tierValue);
                        }
                    }
                    if (string.Equals(Convert.ToString(teamValue), "ORDER", StringComparison.OrdinalIgnoreCase)) snapshot.Order.Add(tier);
                    else snapshot.Chaos.Add(tier);
                }
            }
            return snapshot;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            monitorTimer.Stop();
            rankOverlayTimer.Stop();
            if (rankOverlay != null) { rankOverlay.Close(); rankOverlay = null; }
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
