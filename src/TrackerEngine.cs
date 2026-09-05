using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TargetTimer
{
    public class TrackerEngine
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private readonly StorageManager _storage;
        private Thread _workerThread;
        private volatile bool _isRunning;

        public event Action<string, string, bool> OnActivityTick;

        public string CurrentApp { get; private set; }
        public string CurrentSite { get; private set; }
        public bool IsAfk { get; private set; }
        public int IdleThresholdSeconds { get; set; }

        public TrackerEngine(StorageManager storage)
        {
            _storage = storage;
            IdleThresholdSeconds = 120;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal,
                Name = "TargetTimerEngine"
            };
            _workerThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(1500);
            }
            _storage.Flush();
        }

        private void WorkerLoop()
        {
            int tickCounter = 0;

            while (_isRunning)
            {
                tickCounter++;

                try
                {
                    IntPtr hWnd = IntPtr.Zero;
                    string processName = null;
                    bool afk = CheckIsAfk();

                    if (afk)
                    {
                        CurrentApp = "AFK";
                        CurrentSite = null;
                        IsAfk = true;

                        _storage.RecordAfk(1);
                        if (OnActivityTick != null)
                        {
                            OnActivityTick("AFK", null, true);
                        }
                    }
                    else
                    {
                        hWnd = GetForegroundWindow();
                        if (hWnd != IntPtr.Zero)
                        {
                            processName = GetProcessName(hWnd);
                        }

                        if (string.IsNullOrEmpty(processName))
                        {
                            processName = "explorer.exe";
                        }

                        string windowTitle = (hWnd != IntPtr.Zero) ? GetWindowTitle(hWnd) : "Рабочий стол";
                        string site = null;
                        if (BrowserDetector.IsBrowser(processName) && hWnd != IntPtr.Zero)
                        {
                            site = BrowserDetector.DetectSite(hWnd, processName, windowTitle);
                        }

                        CurrentApp = processName;
                        CurrentSite = site;
                        IsAfk = false;

                        _storage.RecordActive(processName, site, 1);

                        if (OnActivityTick != null)
                        {
                            OnActivityTick(processName, site, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        string errLog = Path.Combine(_storage.DataDirectory, "..", "tracker_error.log");
                        File.AppendAllText(errLog, DateTime.Now + " TrackerError: " + ex + "\r\n");
                    }
                    catch { }
                }

                try
                {
                    // Flush data to disk on tick 5 and then every 30 seconds
                    if (tickCounter == 5 || tickCounter % 30 == 0)
                    {
                        _storage.Flush();
                    }
                }
                catch { }

                Thread.Sleep(1000);
            }
        }

        private bool CheckIsAfk()
        {
            var lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (GetLastInputInfo(ref lii))
            {
                uint currentTick = (uint)Environment.TickCount;
                uint idleTicks = currentTick >= lii.dwTime ? (currentTick - lii.dwTime) : 0;
                return idleTicks >= (IdleThresholdSeconds * 1000);
            }
            return false;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(512);
            int len = GetWindowText(hWnd, sb, sb.Capacity);
            return len > 0 ? sb.ToString() : string.Empty;
        }

        private static string GetProcessName(IntPtr hWnd)
        {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0) return null;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        string fullPath = sb.ToString();
                        return Path.GetFileName(fullPath);
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }

            try
            {
                using (var proc = Process.GetProcessById((int)pid))
                {
                    return proc.ProcessName + ".exe";
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
