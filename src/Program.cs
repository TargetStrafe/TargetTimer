using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TargetTimer
{
    static class Program
    {
        private const string MutexName = "Global\\TargetTimer_Unique_Mutex_Lock";
        private const string EventName = "Global\\TargetTimer_ShowEvent_Signal";

        private static Mutex _appMutex;
        private static EventWaitHandle _showEventHandle;
        private static Thread _signalListenerThread;
        private static NotifyIcon _trayIcon;
        private static ContextMenuStrip _trayMenu;
        private static StorageManager _storage;
        private static TrackerEngine _engine;
        private static MainWindow _mainWindow;
        private static System.Windows.Forms.Timer _memoryTrimTimer;
        private static ToolStripMenuItem _autostartMenuItem;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool startSilent = false;

            if (args != null && args.Length > 0)
            {
                foreach (var a in args)
                {
                    string cmd = a.Trim().ToLowerInvariant();
                    if (cmd == "--report" || cmd == "-r" || cmd == "/report")
                    {
                        var st = new StorageManager();
                        DashboardGenerator.GenerateAndOpen(st);
                        return;
                    }
                    if (cmd == "--autostart-on")
                    {
                        AutostartManager.SetAutostart(true);
                        return;
                    }
                    if (cmd == "--autostart-off")
                    {
                        AutostartManager.SetAutostart(false);
                        return;
                    }
                    if (cmd == "--silent" || cmd == "-s" || cmd == "/silent" || cmd == "--autostart")
                    {
                        startSilent = true;
                    }
                }
            }

            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TargetTimer");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            string crashLog = Path.Combine(logDir, "crash.log");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.AppendAllText(crashLog, DateTime.Now + " Unhandled: " + e.ExceptionObject + "\r\n"); } catch { }
            };

            Application.ThreadException += (s, e) =>
            {
                try { File.AppendAllText(crashLog, DateTime.Now + " ThreadEx: " + e.Exception + "\r\n"); } catch { }
            };

            bool isNewInstance = false;
            try
            {
                _appMutex = new Mutex(true, MutexName, out isNewInstance);
            }
            catch (AbandonedMutexException)
            {
                isNewInstance = true;
            }
            catch
            {
                isNewInstance = true;
            }

            if (!isNewInstance)
            {
                try
                {
                    using (var ev = EventWaitHandle.OpenExisting(EventName))
                    {
                        ev.Set();
                    }
                }
                catch { }
                return;
            }

            try
            {
                _showEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            }
            catch { }

            try
            {
                Application.Run(new TrayAppContext(startSilent));
            }
            finally
            {
                Cleanup();
            }
        }

        class TrayAppContext : ApplicationContext
        {
            public TrayAppContext(bool startSilent)
            {
                InitializeApp(startSilent);
            }
        }

        private static void InitializeApp(bool startSilent)
        {
            AutostartManager.InitializeOnFirstRun();

            _storage = new StorageManager();
            _engine = new TrackerEngine(_storage);
            _mainWindow = new MainWindow(_storage, _engine, ExitApplication);

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Renderer = new ModernMenuRenderer();

            var openGuiItem = new ToolStripMenuItem("🖥️ Открыть TargetTimer (GUI)", null, (s, e) =>
            {
                ShowMainWindow();
            });
            openGuiItem.Font = new Font(_trayMenu.Font, FontStyle.Bold);

            var openDashboardItem = new ToolStripMenuItem("📊 Веб-отчёт в браузере", null, (s, e) =>
            {
                _storage.Flush();
                DashboardGenerator.GenerateAndOpen(_storage);
            });

            var openFolderItem = new ToolStripMenuItem("📁 Папка с данными", null, (s, e) =>
            {
                try
                {
                    Process.Start("explorer.exe", _storage.DataDirectory);
                }
                catch { }
            });

            _autostartMenuItem = new ToolStripMenuItem("⚙️ Автозапуск с Windows", null, (s, e) =>
            {
                bool current = AutostartManager.IsAutostartEnabled();
                AutostartManager.SetAutostart(!current);
            });
            _autostartMenuItem.Checked = AutostartManager.IsAutostartEnabled();

            AutostartManager.OnAutostartChanged += (enabled) =>
            {
                if (_autostartMenuItem != null)
                {
                    _autostartMenuItem.Checked = enabled;
                }
            };

            var updateItem = new ToolStripMenuItem("🔄 Обновления (v" + UpdateManager.CurrentVersion + ")", null, (s, e) =>
            {
                UpdateManager.CheckManual(_trayIcon);
            });

            var exitItem = new ToolStripMenuItem("🚪 Выход", null, (s, e) =>
            {
                ExitApplication();
            });

            _trayMenu.Items.Add(openGuiItem);
            _trayMenu.Items.Add(openDashboardItem);
            _trayMenu.Items.Add(openFolderItem);
            _trayMenu.Items.Add(_autostartMenuItem);
            _trayMenu.Items.Add(updateItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon
            {
                Text = "TargetTimer — Мониторинг времени",
                Icon = CreateTrayIcon(),
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) =>
            {
                ShowMainWindow();
            };

            if (AutostartManager.WasFirstRun)
            {
                _trayIcon.ShowBalloonTip(4000, "TargetTimer", "TargetTimer автоматически добавлен в автозапуск с Windows (в фоне). Отключить можно в Настройках или трее.", ToolTipIcon.Info);
            }

            _engine.Start();
            UpdateManager.InitAutoUpdate();

            if (_showEventHandle != null)
            {
                _signalListenerThread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            if (_showEventHandle.WaitOne())
                            {
                                if (_mainWindow != null && !_mainWindow.IsDisposed)
                                {
                                    _mainWindow.BeginInvoke(new Action(() => ShowMainWindow()));
                                }
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "TargetTimer_SignalListener"
                };
                _signalListenerThread.Start();
            }

            _memoryTrimTimer = new System.Windows.Forms.Timer();
            _memoryTrimTimer.Interval = 15000;
            _memoryTrimTimer.Tick += (s, e) =>
            {
                TrimWorkingSet();
            };
            _memoryTrimTimer.Start();

            if (!startSilent)
            {
                ShowMainWindow();
            }
            else
            {
                TrimWorkingSet();
            }
        }

        public static void ShowMainWindow()
        {
            if (_mainWindow != null && !_mainWindow.IsDisposed)
            {
                _mainWindow.ShowForm();
            }
        }

        public static void ExitApplication()
        {
            try
            {

                if (_mainWindow != null && !_mainWindow.IsDisposed)
                {
                    _mainWindow.ForceClose();
                }
                if (_engine != null)
                {
                    _engine.Stop();
                }
                Application.Exit();
            }
            catch { }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(0, GCCollectionMode.Optimized);
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        private static Icon CreateTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath, 32, 32);
                }
                Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (appIcon != null) return appIcon;
            }
            catch { }

            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, 32, 32),
                    Color.FromArgb(106, 20, 255),
                    Color.FromArgb(68, 240, 255),
                    LinearGradientMode.ForwardDiagonal))
                {
                    g.FillEllipse(brush, 2, 2, 28, 28);
                }

                using (Pen whitePen = new Pen(Color.White, 2.5f))
                {
                    whitePen.StartCap = LineCap.Round;
                    whitePen.EndCap = LineCap.Round;
                    g.DrawLine(whitePen, 16, 16, 16, 8);
                    g.DrawLine(whitePen, 16, 16, 22, 16);
                }

                using (Brush dotBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(dotBrush, 14.5f, 14.5f, 3, 3);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (_memoryTrimTimer != null)
                {
                    _memoryTrimTimer.Stop();
                    _memoryTrimTimer.Dispose();
                }

                if (_engine != null)
                {
                    _engine.Stop();
                }

                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }

                if (_showEventHandle != null)
                {
                    _showEventHandle.Close();
                }

                if (_appMutex != null)
                {
                    _appMutex.ReleaseMutex();
                    _appMutex.Close();
                }
            }
            catch { }
        }
    }

    class ModernMenuRenderer : ToolStripProfessionalRenderer
    {
        public ModernMenuRenderer() : base(new ModernColorTable()) { }
    }

    class ModernColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(20, 24, 36); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(20, 24, 36); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(20, 24, 36); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(20, 24, 36); } }
        public override Color MenuBorder { get { return Color.FromArgb(35, 42, 62); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(35, 42, 65); } }
        public override Color MenuStripGradientBegin { get { return Color.FromArgb(20, 24, 36); } }
        public override Color MenuStripGradientEnd { get { return Color.FromArgb(20, 24, 36); } }
        public override Color SeparatorDark { get { return Color.FromArgb(35, 42, 62); } }
        public override Color SeparatorLight { get { return Color.FromArgb(35, 42, 62); } }
    }
}
