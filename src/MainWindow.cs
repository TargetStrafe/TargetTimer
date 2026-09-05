using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TargetTimer
{
    public class MainWindow : Form
    {
        private readonly StorageManager _storage;
        private readonly TrackerEngine _engine;
        private readonly Action _onExitApp;

        private Panel _headerPanel;
        private PictureBox _logoBox;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _liveStatusLabel;
        private Button _btnHtmlReport;
        private Button _btnMinimizeToTray;

        private Panel _metricCardsPanel;
        private MetricCard _cardToday;
        private MetricCard _cardTopApp;
        private MetricCard _cardLifetime;
        private MetricCard _cardRam;

        private Panel _tabBarPanel;
        private Button _tabBtnToday;
        private Button _tabBtnLifetime;
        private Button _tabBtnSettings;
        private Panel _tabIndicator;

        private Panel _contentPanel;
        private Panel _pageToday;
        private Panel _pageLifetime;
        private Panel _pageSettings;

        // Page Today controls
        private TextBox _txtSearchTodayApp;
        private ListView _lvTodayApps;
        private TextBox _txtSearchTodaySite;
        private ListView _lvTodaySites;

        // Page Lifetime controls
        private ListView _lvLifeApps;
        private ListView _lvLifeSites;
        private Label _lblLifeSummary;

        // Page Settings controls
        private CheckBox _chkAutostart;
        private CheckBox _chkMinimizeOnClose;
        private NumericUpDown _numAfkSeconds;
        private Button _btnCheckUpdate;
        private Button _btnOpenDataDir;
        private Button _btnTrimRam;
        private Label _lblVersionInfo;
        private Label _lblRamDetailed;

        private Timer _uiUpdateTimer;
        private int _refreshTickCount = 0;
        private bool _minimizeOnClose = true;
        private bool _isRealExit = false;

        public MainWindow(StorageManager storage, TrackerEngine engine, Action onExitApp)
        {
            _storage = storage;
            _engine = engine;
            _onExitApp = onExitApp;

            InitializeWindow();
            SetupControls();
            SwitchTab(0);

            _uiUpdateTimer = new Timer();
            _uiUpdateTimer.Interval = 1000;
            _uiUpdateTimer.Tick += (s, e) => OnUiTick();
            _uiUpdateTimer.Start();

            RefreshData(true);
        }

        private void InitializeWindow()
        {
            this.Text = "TargetTimer — Мониторинг времени";
            this.Size = new Size(980, 700);
            this.MinimumSize = new Size(880, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(14, 17, 26);
            this.ForeColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            this.DoubleBuffered = true;

            try
            {
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string iconPath = Path.Combine(exeDir, "icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            this.FormClosing += (s, e) =>
            {
                if (!_isRealExit && _minimizeOnClose && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                    TrimMemory();
                }
                else
                {
                    _uiUpdateTimer.Stop();
                }
            };
        }

        public void ShowForm()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Show();
            this.BringToFront();
            this.Activate();
            RefreshData(true);
        }

        public void ForceClose()
        {
            _isRealExit = true;
            this.Close();
        }

        private void SetupControls()
        {
            // 1. Top Header
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(20, 24, 36),
                Padding = new Padding(16, 10, 16, 10)
            };
            _headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(35, 42, 62)))
                {
                    e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
                }
            };

            _logoBox = new PictureBox
            {
                Size = new Size(44, 44),
                Location = new Point(16, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(28, 32, 48),
                Padding = new Padding(2)
            };
            _logoBox.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(106, 20, 255), 1.5f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _logoBox.Width - 1, _logoBox.Height - 1);
                }
            };
            LoadLogoImage(_logoBox);

            _titleLabel = new Label
            {
                Text = "TARGETTIMER",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(247, 251, 255),
                AutoSize = true,
                Location = new Point(68, 12)
            };

            _subtitleLabel = new Label
            {
                Text = "by TargetStrafe  •  v" + UpdateManager.CurrentVersion,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(106, 20, 255),
                AutoSize = true,
                Location = new Point(70, 38)
            };

            _liveStatusLabel = new Label
            {
                Text = "● Ожидание данных...",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(68, 240, 255),
                AutoSize = true,
                Location = new Point(280, 24),
                BackColor = Color.Transparent
            };

            var rightActionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 320,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnMinimizeToTray = CreateStyledButton("Свернуть в трей", 130, 34);
            _btnMinimizeToTray.Click += (s, e) =>
            {
                this.Hide();
                TrimMemory();
            };

            _btnHtmlReport = CreateStyledButton("Web-отчёт в браузере", 154, 34);
            _btnHtmlReport.Click += (s, e) =>
            {
                _storage.Flush();
                DashboardGenerator.GenerateAndOpen(_storage);
            };

            rightActionsPanel.Controls.Add(_btnMinimizeToTray);
            rightActionsPanel.Controls.Add(_btnHtmlReport);

            _headerPanel.Controls.Add(rightActionsPanel);
            _headerPanel.Controls.Add(_liveStatusLabel);
            _headerPanel.Controls.Add(_subtitleLabel);
            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_logoBox);

            // 2. Metric Cards Panel
            _metricCardsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = Color.FromArgb(14, 17, 26),
                Padding = new Padding(16, 10, 16, 10)
            };

            var cardsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            _cardToday = new MetricCard("ВРЕМЯ СЕГОДНЯ", "00:00:00", "Активное время", Color.FromArgb(68, 240, 255));
            _cardTopApp = new MetricCard("ТОП ПРИЛОЖЕНИЕ", "—", "Лидер сегодня", Color.FromArgb(106, 20, 255));
            _cardLifetime = new MetricCard("ЗА ВСЁ ВРЕМЯ", "0 ч", "Накоплено в базе", Color.FromArgb(255, 75, 225));
            _cardRam = new MetricCard("ОЗУ / ПАМЯТЬ", "0 МБ", "Фоновое потребление", Color.FromArgb(46, 213, 115));

            cardsTable.Controls.Add(_cardToday, 0, 0);
            cardsTable.Controls.Add(_cardTopApp, 1, 0);
            cardsTable.Controls.Add(_cardLifetime, 2, 0);
            cardsTable.Controls.Add(_cardRam, 3, 0);

            _metricCardsPanel.Controls.Add(cardsTable);

            // 3. Tab Bar
            _tabBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(20, 24, 36),
                Padding = new Padding(16, 0, 16, 0)
            };
            _tabBarPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(35, 42, 62)))
                {
                    e.Graphics.DrawLine(pen, 0, _tabBarPanel.Height - 1, _tabBarPanel.Width, _tabBarPanel.Height - 1);
                }
            };

            _tabBtnToday = CreateTabButton("Сегодня (Live)", 0);
            _tabBtnLifetime = CreateTabButton("За всё время (Lifetime)", 1);
            _tabBtnSettings = CreateTabButton("Настройки", 2);

            _tabBtnToday.Location = new Point(16, 0);
            _tabBtnLifetime.Location = new Point(180, 0);
            _tabBtnSettings.Location = new Point(380, 0);

            _tabIndicator = new Panel
            {
                Height = 3,
                Width = 140,
                BackColor = Color.FromArgb(106, 20, 255),
                Location = new Point(16, 39)
            };

            _tabBarPanel.Controls.Add(_tabBtnToday);
            _tabBarPanel.Controls.Add(_tabBtnLifetime);
            _tabBarPanel.Controls.Add(_tabBtnSettings);
            _tabBarPanel.Controls.Add(_tabIndicator);

            // 4. Content Container
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 17, 26),
                Padding = new Padding(16, 12, 16, 12)
            };

            SetupPageToday();
            SetupPageLifetime();
            SetupPageSettings();

            this.Controls.Add(_contentPanel);
            this.Controls.Add(_tabBarPanel);
            this.Controls.Add(_metricCardsPanel);
            this.Controls.Add(_headerPanel);
        }

        private void SetupPageToday()
        {
            _pageToday = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            // Left: Apps
            var pnlApps = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
            var lblAppsHeader = new Label
            {
                Text = "Программы и процессы",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Top,
                Height = 28
            };

            var searchAppPnl = CreateSearchBox("Поиск приложения...", out _txtSearchTodayApp);
            _txtSearchTodayApp.TextChanged += (s, e) => RenderTodayApps();

            _lvTodayApps = CreateStyledListView(new string[] { "Приложение", "Время", "Доля" }, new int[] { 190, 110, 140 });
            _lvTodayApps.Dock = DockStyle.Fill;

            pnlApps.Controls.Add(_lvTodayApps);
            pnlApps.Controls.Add(searchAppPnl);
            pnlApps.Controls.Add(lblAppsHeader);

            // Right: Sites
            var pnlSites = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };
            var lblSitesHeader = new Label
            {
                Text = "Сайты и вкладки браузеров",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Top,
                Height = 28
            };

            var searchSitePnl = CreateSearchBox("Поиск сайта...", out _txtSearchTodaySite);
            _txtSearchTodaySite.TextChanged += (s, e) => RenderTodaySites();

            _lvTodaySites = CreateStyledListView(new string[] { "Сайт / Ресурс", "Время", "Доля" }, new int[] { 190, 110, 140 });
            _lvTodaySites.Dock = DockStyle.Fill;

            pnlSites.Controls.Add(_lvTodaySites);
            pnlSites.Controls.Add(searchSitePnl);
            pnlSites.Controls.Add(lblSitesHeader);

            split.Controls.Add(pnlApps, 0, 0);
            split.Controls.Add(pnlSites, 1, 0);

            _pageToday.Controls.Add(split);
            _contentPanel.Controls.Add(_pageToday);
        }

        private void SetupPageLifetime()
        {
            _pageLifetime = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

            _lblLifeSummary = new Label
            {
                Text = "Накопленная статистика за всё время работы. Хранится локально и никогда не сбрасывается.",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
                ForeColor = Color.FromArgb(140, 150, 170),
                Dock = DockStyle.Top,
                Height = 28
            };

            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            var pnlApps = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
            var lblApps = new Label
            {
                Text = "Топ приложений за всё время",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Top,
                Height = 28
            };
            _lvLifeApps = CreateStyledListView(new string[] { "Приложение", "Общее время", "Доля" }, new int[] { 190, 110, 140 });
            _lvLifeApps.Dock = DockStyle.Fill;
            pnlApps.Controls.Add(_lvLifeApps);
            pnlApps.Controls.Add(lblApps);

            var pnlSites = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };
            var lblSites = new Label
            {
                Text = "Топ сайтов за всё время",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Top,
                Height = 28
            };
            _lvLifeSites = CreateStyledListView(new string[] { "Сайт / Ресурс", "Общее время", "Доля" }, new int[] { 190, 110, 140 });
            _lvLifeSites.Dock = DockStyle.Fill;
            pnlSites.Controls.Add(_lvLifeSites);
            pnlSites.Controls.Add(lblSites);

            split.Controls.Add(pnlApps, 0, 0);
            split.Controls.Add(pnlSites, 1, 0);

            _pageLifetime.Controls.Add(split);
            _pageLifetime.Controls.Add(_lblLifeSummary);
            _contentPanel.Controls.Add(_pageLifetime);
        }

        private void SetupPageSettings()
        {
            _pageSettings = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = true,
                Visible = false
            };

            int top = 10;

            // Section 1: Launch & Window behavior
            var grpBehavior = CreateSettingsCard("Параметры запуска и окна", 175);
            grpBehavior.Location = new Point(10, top);

            _chkAutostart = new CheckBox
            {
                Text = "Автозапуск вместе с Windows (в фоновом режиме)",
                AutoSize = true,
                Location = new Point(20, 36),
                ForeColor = Color.FromArgb(245, 247, 250),
                Checked = AutostartManager.IsAutostartEnabled()
            };
            _chkAutostart.CheckedChanged += (s, e) =>
            {
                if (AutostartManager.IsAutostartEnabled() != _chkAutostart.Checked)
                {
                    AutostartManager.SetAutostart(_chkAutostart.Checked);
                }
            };
            AutostartManager.OnAutostartChanged += (enabled) =>
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (_chkAutostart != null && _chkAutostart.Checked != enabled)
                        {
                            _chkAutostart.Checked = enabled;
                        }
                    }));
                }
            };

            _chkMinimizeOnClose = new CheckBox
            {
                Text = "Сворачивать в системный трей при нажатии на крестик (X)",
                AutoSize = true,
                Location = new Point(20, 68),
                ForeColor = Color.FromArgb(245, 247, 250),
                Checked = true
            };
            _chkMinimizeOnClose.CheckedChanged += (s, e) =>
            {
                _minimizeOnClose = _chkMinimizeOnClose.Checked;
            };

            var lblAfk = new Label
            {
                Text = "Таймаут AFK (секунд бездействия):",
                AutoSize = true,
                Location = new Point(20, 104),
                ForeColor = Color.FromArgb(140, 150, 170)
            };
            _numAfkSeconds = new NumericUpDown
            {
                Location = new Point(255, 101),
                Width = 70,
                Minimum = 30,
                Maximum = 1800,
                Value = _engine != null ? _engine.IdleThresholdSeconds : 120,
                BackColor = Color.FromArgb(24, 28, 42),
                ForeColor = Color.White
            };
            _numAfkSeconds.ValueChanged += (s, e) =>
            {
                if (_engine != null) _engine.IdleThresholdSeconds = (int)_numAfkSeconds.Value;
            };

            var btnExitCompletely = CreateStyledButton("Полностью закрыть программу", 240, 30);
            btnExitCompletely.Location = new Point(20, 134);
            btnExitCompletely.Click += (s, e) =>
            {
                _isRealExit = true;
                if (_onExitApp != null) _onExitApp();
                else Application.Exit();
            };

            grpBehavior.Controls.Add(_chkAutostart);
            grpBehavior.Controls.Add(_chkMinimizeOnClose);
            grpBehavior.Controls.Add(lblAfk);
            grpBehavior.Controls.Add(_numAfkSeconds);
            grpBehavior.Controls.Add(btnExitCompletely);
            _pageSettings.Controls.Add(grpBehavior);

            top += 190;

            // Section 2: Updates & GitHub
            var grpUpdates = CreateSettingsCard("Обновления и исходный код", 125);
            grpUpdates.Location = new Point(10, top);

            _lblVersionInfo = new Label
            {
                Text = "Версия: TargetTimer v" + UpdateManager.CurrentVersion + " | Автор: TargetStrafe\nРепозиторий: https://github.com/TargetStrafe/TargetTimer",
                AutoSize = true,
                Location = new Point(20, 36),
                ForeColor = Color.FromArgb(140, 150, 170)
            };

            _btnCheckUpdate = CreateStyledButton("Проверить обновления", 190, 32);
            _btnCheckUpdate.Location = new Point(20, 78);
            _btnCheckUpdate.Click += (s, e) =>
            {
                UpdateManager.CheckManual(null);
            };

            var btnOpenGitHub = CreateStyledButton("Открыть GitHub", 140, 32);
            btnOpenGitHub.Location = new Point(220, 78);
            btnOpenGitHub.Click += (s, e) =>
            {
                try { Process.Start("https://github.com/TargetStrafe/TargetTimer"); } catch { }
            };

            grpUpdates.Controls.Add(_lblVersionInfo);
            grpUpdates.Controls.Add(_btnCheckUpdate);
            grpUpdates.Controls.Add(btnOpenGitHub);
            _pageSettings.Controls.Add(grpUpdates);

            top += 140;

            // Section 3: Storage & Memory
            var grpStorage = CreateSettingsCard("Хранилище данных и оптимизация памяти", 145);
            grpStorage.Location = new Point(10, top);

            _lblRamDetailed = new Label
            {
                Text = "Данные сохраняются в: " + _storage.DataDirectory + "\nАрхитектура без накопления мусора: память очищается автоматически.",
                AutoSize = true,
                Location = new Point(20, 36),
                ForeColor = Color.FromArgb(140, 150, 170)
            };

            _btnOpenDataDir = CreateStyledButton("Открыть папку данных", 190, 32);
            _btnOpenDataDir.Location = new Point(20, 78);
            _btnOpenDataDir.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", _storage.DataDirectory); } catch { }
            };

            _btnTrimRam = CreateStyledButton("Сжать память (Trim)", 160, 32);
            _btnTrimRam.Location = new Point(220, 78);
            _btnTrimRam.Click += (s, e) =>
            {
                TrimMemory();
                UpdateRamCard();
                MessageBox.Show("Память успешно оптимизирована.", "TargetTimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            grpStorage.Controls.Add(_lblRamDetailed);
            grpStorage.Controls.Add(_btnOpenDataDir);
            grpStorage.Controls.Add(_btnTrimRam);
            _pageSettings.Controls.Add(grpStorage);

            _contentPanel.Controls.Add(_pageSettings);
        }

        private Panel CreateSettingsCard(string title, int height)
        {
            var card = new Panel
            {
                Width = 760,
                Height = height,
                BackColor = Color.FromArgb(20, 24, 36),
                Padding = new Padding(16)
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(35, 42, 62)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(68, 240, 255),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            card.Controls.Add(lblTitle);
            return card;
        }

        private Button CreateTabButton(string text, int tabIndex)
        {
            var btn = new Button
            {
                Text = text,
                Width = 160,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 150, 170),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 34, 50);
            btn.Click += (s, e) => SwitchTab(tabIndex);
            return btn;
        }

        private void SwitchTab(int index)
        {
            _pageToday.Visible = (index == 0);
            _pageLifetime.Visible = (index == 1);
            _pageSettings.Visible = (index == 2);

            _tabBtnToday.ForeColor = (index == 0) ? Color.White : Color.FromArgb(140, 150, 170);
            _tabBtnLifetime.ForeColor = (index == 1) ? Color.White : Color.FromArgb(140, 150, 170);
            _tabBtnSettings.ForeColor = (index == 2) ? Color.White : Color.FromArgb(140, 150, 170);

            if (index == 0) _tabIndicator.Location = new Point(_tabBtnToday.Left, 39);
            else if (index == 1) _tabIndicator.Location = new Point(_tabBtnLifetime.Left, 39);
            else _tabIndicator.Location = new Point(_tabBtnSettings.Left, 39);

            if (index == 1)
            {
                RenderLifetimeData();
            }
        }

        private void OnUiTick()
        {
            if (!this.Visible) return;

            _refreshTickCount++;

            string app = _engine != null ? _engine.CurrentApp : null;
            string site = _engine != null ? _engine.CurrentSite : null;
            bool isAfk = _engine != null && _engine.IsAfk;

            if (isAfk)
            {
                _liveStatusLabel.Text = "● Бездействие (AFK)";
                _liveStatusLabel.ForeColor = Color.FromArgb(255, 171, 0);
            }
            else if (!string.IsNullOrEmpty(app))
            {
                if (!string.IsNullOrEmpty(site))
                {
                    _liveStatusLabel.Text = "● Активно: " + app + " → " + site;
                }
                else
                {
                    _liveStatusLabel.Text = "● Активно: " + app;
                }
                _liveStatusLabel.ForeColor = Color.FromArgb(46, 213, 115);
            }
            else
            {
                _liveStatusLabel.Text = "● Мониторинг активен";
                _liveStatusLabel.ForeColor = Color.FromArgb(68, 240, 255);
            }

            UpdateMetricsQuick();

            if (_refreshTickCount % 5 == 0)
            {
                RefreshData(false);
            }

            if (_refreshTickCount % 10 == 0)
            {
                TrimMemory();
            }
        }

        private void UpdateMetricsQuick()
        {
            DayActivity today = _storage.GetCurrentDay();
            if (today != null)
            {
                _cardToday.ValueText = FormatDuration(today.ActiveSeconds);

                string topName = "—";
                int topSec = 0;
                foreach (var kvp in today.Apps)
                {
                    if (kvp.Value.Seconds > topSec)
                    {
                        topSec = kvp.Value.Seconds;
                        topName = kvp.Key;
                    }
                }
                if (topSec > 0 && today.ActiveSeconds > 0)
                {
                    int pct = (int)Math.Round((double)topSec / today.ActiveSeconds * 100);
                    _cardTopApp.ValueText = topName;
                    _cardTopApp.SubText = FormatDuration(topSec) + " (" + pct + "%)";
                }
            }

            UpdateRamCard();
        }

        private void UpdateRamCard()
        {
            try
            {
                using (var p = Process.GetCurrentProcess())
                {
                    double mb = p.WorkingSet64 / (1024.0 * 1024.0);
                    _cardRam.ValueText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} МБ", mb);
                }
            }
            catch { }
        }

        private void RefreshData(bool refreshLifetime)
        {
            RenderTodayApps();
            RenderTodaySites();

            if (refreshLifetime)
            {
                RenderLifetimeData();
            }
        }

        private void RenderTodayApps()
        {
            DayActivity day = _storage.GetCurrentDay();
            if (day == null) return;

            string filter = _txtSearchTodayApp.Text.Trim().ToLowerInvariant();

            var list = day.Apps.Values
                .Where(a => string.IsNullOrEmpty(filter) || a.Name.ToLowerInvariant().Contains(filter))
                .OrderByDescending(a => a.Seconds)
                .ToList();

            int total = day.ActiveSeconds > 0 ? day.ActiveSeconds : 1;

            _lvTodayApps.BeginUpdate();
            _lvTodayApps.Items.Clear();

            foreach (var item in list)
            {
                double pct = (double)item.Seconds / total * 100.0;
                var lvi = new ListViewItem(item.Name);
                lvi.SubItems.Add(FormatDuration(item.Seconds));
                lvi.SubItems.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}%", pct));
                lvi.Tag = pct;
                _lvTodayApps.Items.Add(lvi);
            }

            _lvTodayApps.EndUpdate();
        }

        private void RenderTodaySites()
        {
            DayActivity day = _storage.GetCurrentDay();
            if (day == null) return;

            string filter = _txtSearchTodaySite.Text.Trim().ToLowerInvariant();

            var siteMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalSiteSec = 0;

            foreach (var app in day.Apps.Values)
            {
                foreach (var s in app.Sites)
                {
                    if (string.IsNullOrEmpty(s.Key)) continue;
                    int cur;
                    siteMap.TryGetValue(s.Key, out cur);
                    siteMap[s.Key] = cur + s.Value;
                    totalSiteSec += s.Value;
                }
            }

            var list = siteMap
                .Where(kvp => string.IsNullOrEmpty(filter) || kvp.Key.ToLowerInvariant().Contains(filter))
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            if (totalSiteSec == 0) totalSiteSec = 1;

            _lvTodaySites.BeginUpdate();
            _lvTodaySites.Items.Clear();

            foreach (var item in list)
            {
                double pct = (double)item.Value / totalSiteSec * 100.0;
                var lvi = new ListViewItem(item.Key);
                lvi.SubItems.Add(FormatDuration(item.Value));
                lvi.SubItems.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}%", pct));
                lvi.Tag = pct;
                _lvTodaySites.Items.Add(lvi);
            }

            _lvTodaySites.EndUpdate();
        }

        private void RenderLifetimeData()
        {
            DayActivity totals = _storage.LoadDate("all-time");
            if (totals == null) return;

            double hours = totals.ActiveSeconds / 3600.0;
            _cardLifetime.ValueText = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} ч", hours);

            List<string> dates = _storage.GetAvailableDates();
            int daysCount = Math.Max(1, dates.Count);
            double avgHours = hours / daysCount;
            _lblLifeSummary.Text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Всего отслежено дней: {0} | Среднее активное время в день: {1:0.0} ч | Полный объём: {2:0.0} ч. База защищена от потерь.",
                daysCount, avgHours, hours);

            int totalAppSec = totals.ActiveSeconds > 0 ? totals.ActiveSeconds : 1;
            var appList = totals.Apps.Values.OrderByDescending(a => a.Seconds).ToList();

            _lvLifeApps.BeginUpdate();
            _lvLifeApps.Items.Clear();
            foreach (var item in appList)
            {
                double pct = (double)item.Seconds / totalAppSec * 100.0;
                var lvi = new ListViewItem(item.Name);
                lvi.SubItems.Add(FormatDuration(item.Seconds));
                lvi.SubItems.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}%", pct));
                lvi.Tag = pct;
                _lvLifeApps.Items.Add(lvi);
            }
            _lvLifeApps.EndUpdate();

            var siteMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalSiteSec = 0;
            foreach (var app in totals.Apps.Values)
            {
                foreach (var s in app.Sites)
                {
                    if (string.IsNullOrEmpty(s.Key)) continue;
                    int cur;
                    siteMap.TryGetValue(s.Key, out cur);
                    siteMap[s.Key] = cur + s.Value;
                    totalSiteSec += s.Value;
                }
            }
            if (totalSiteSec == 0) totalSiteSec = 1;

            var siteList = siteMap.OrderByDescending(kvp => kvp.Value).ToList();

            _lvLifeSites.BeginUpdate();
            _lvLifeSites.Items.Clear();
            foreach (var item in siteList)
            {
                double pct = (double)item.Value / totalSiteSec * 100.0;
                var lvi = new ListViewItem(item.Key);
                lvi.SubItems.Add(FormatDuration(item.Value));
                lvi.SubItems.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}%", pct));
                lvi.Tag = pct;
                _lvLifeSites.Items.Add(lvi);
            }
            _lvLifeSites.EndUpdate();
        }

        private void TrimMemory()
        {
            try
            {
                GC.Collect(0, GCCollectionMode.Optimized);
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        private static string FormatDuration(int seconds)
        {
            if (seconds < 60) return seconds + " с";
            int m = seconds / 60;
            int s = seconds % 60;
            if (m < 60) return string.Format("{0} м {1} с", m, s);
            int h = m / 60;
            m = m % 60;
            return string.Format("{0} ч {1} м", h, m);
        }

        private static Panel CreateSearchBox(string placeholder, out TextBox textBox)
        {
            var pnl = new Panel
            {
                Height = 34,
                BackColor = Color.FromArgb(24, 28, 42),
                Padding = new Padding(8, 7, 8, 5),
                Dock = DockStyle.Top
            };
            pnl.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(45, 54, 78)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
                }
            };
            var lblPrefix = new Label
            {
                Text = "Поиск:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(106, 20, 255),
                Dock = DockStyle.Left,
                Width = 56,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var txt = new TextBox
            {
                BackColor = Color.FromArgb(24, 28, 42),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                Dock = DockStyle.Fill
            };
            pnl.Controls.Add(txt);
            pnl.Controls.Add(lblPrefix);
            textBox = txt;
            return pnl;
        }

        private static Button CreateStyledButton(string text, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(245, 247, 250),
                BackColor = Color.FromArgb(28, 34, 50),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(50, 60, 85);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 48, 70);
            return btn;
        }

        private static readonly SolidBrush _brushHeader = new SolidBrush(Color.FromArgb(28, 34, 50));
        private static readonly Pen _penHeaderLine = new Pen(Color.FromArgb(40, 48, 72));
        private static readonly SolidBrush _brushHeaderText = new SolidBrush(Color.FromArgb(140, 150, 170));
        private static readonly SolidBrush _brushRowEven = new SolidBrush(Color.FromArgb(20, 24, 36));
        private static readonly SolidBrush _brushRowOdd = new SolidBrush(Color.FromArgb(24, 28, 42));
        private static readonly SolidBrush _brushRowSelected = new SolidBrush(Color.FromArgb(38, 46, 70));
        private static readonly SolidBrush _brushTrack = new SolidBrush(Color.FromArgb(32, 38, 55));
        private static readonly SolidBrush _brushBarFill = new SolidBrush(Color.FromArgb(106, 20, 255));
        private static readonly SolidBrush _brushPctText = new SolidBrush(Color.FromArgb(180, 190, 210));
        private static readonly SolidBrush _brushCol0 = new SolidBrush(Color.FromArgb(245, 247, 250));
        private static readonly SolidBrush _brushCol1 = new SolidBrush(Color.FromArgb(140, 150, 170));
        private static readonly StringFormat _sfNear = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
        private static readonly StringFormat _sfFar = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far };

        private ListView CreateStyledListView(string[] headers, int[] widths)
        {
            var lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(20, 24, 36),
                ForeColor = Color.FromArgb(245, 247, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                OwnerDraw = true
            };

            for (int i = 0; i < headers.Length; i++)
            {
                lv.Columns.Add(headers[i], widths[i]);
            }

            lv.DrawColumnHeader += (s, e) =>
            {
                e.Graphics.FillRectangle(_brushHeader, e.Bounds);
                e.Graphics.DrawLine(_penHeaderLine, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                var textRect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, new Font("Segoe UI", 8.5f, FontStyle.Bold), _brushHeaderText, textRect, _sfNear);
            };

            lv.DrawItem += (s, e) =>
            {
                e.DrawDefault = false;
            };

            lv.DrawSubItem += (s, e) =>
            {
                bool isSelected = e.Item.Selected;
                Brush bgBrush = isSelected ? _brushRowSelected : (e.ItemIndex % 2 == 0 ? _brushRowEven : _brushRowOdd);
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

                if (e.ColumnIndex == 2 && e.Item.Tag is double)
                {
                    double pct = (double)e.Item.Tag;
                    int barMaxWidth = e.Bounds.Width - 56;
                    int barW = (int)Math.Max(2, (barMaxWidth * Math.Min(pct, 100.0) / 100.0));
                    int barH = 8;
                    int barY = e.Bounds.Top + (e.Bounds.Height - barH) / 2;

                    e.Graphics.FillRectangle(_brushTrack, e.Bounds.Left + 6, barY, barMaxWidth, barH);

                    if (barW > 0)
                    {
                        e.Graphics.FillRectangle(_brushBarFill, e.Bounds.Left + 6, barY, barW, barH);
                    }

                    var textRect = new Rectangle(e.Bounds.Left + barMaxWidth + 10, e.Bounds.Top, 44, e.Bounds.Height);
                    e.Graphics.DrawString(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0}%", pct), new Font("Segoe UI", 8f), _brushPctText, textRect, _sfFar);
                }
                else
                {
                    Brush textBrush = (e.ColumnIndex == 0) ? _brushCol0 : _brushCol1;
                    var textRect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height);
                    e.Graphics.DrawString(e.SubItem.Text, lv.Font, textBrush, textRect, _sfNear);
                }
            };

            return lv;
        }

        private static void LoadLogoImage(PictureBox pb)
        {
            try
            {
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string png = Path.Combine(exeDir, "shield_hourglass_transparent.png");
                if (File.Exists(png))
                {
                    using (var stream = new FileStream(png, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        pb.Image = Image.FromStream(stream);
                    }
                    return;
                }
                string ico = Path.Combine(exeDir, "icon.ico");
                if (File.Exists(ico))
                {
                    using (var icon = new Icon(ico, 64, 64))
                    {
                        pb.Image = icon.ToBitmap();
                    }
                    return;
                }
            }
            catch { }
        }
    }

    public class MetricCard : Panel
    {
        private readonly Label _lblTitle;
        private readonly Label _lblValue;
        private readonly Label _lblSub;
        private readonly Color _accentColor;

        public string ValueText
        {
            get { return _lblValue.Text; }
            set { _lblValue.Text = value; }
        }

        public string SubText
        {
            get { return _lblSub.Text; }
            set { _lblSub.Text = value; }
        }

        public MetricCard(string title, string initialValue, string sub, Color accent)
        {
            _accentColor = accent;
            this.Dock = DockStyle.Fill;
            this.Margin = new Padding(4);
            this.BackColor = Color.FromArgb(20, 24, 36);
            this.Padding = new Padding(12, 10, 12, 10);

            this.Paint += (s, e) =>
            {
                using (var brush = new SolidBrush(_accentColor))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, 3, this.Height);
                }
                using (var pen = new Pen(Color.FromArgb(35, 42, 62)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            _lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 150, 170),
                Dock = DockStyle.Top,
                Height = 16
            };

            _lblValue = new Label
            {
                Text = initialValue,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(247, 251, 255),
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true
            };

            _lblSub = new Label
            {
                Text = sub,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 110, 130),
                Dock = DockStyle.Top,
                Height = 16,
                AutoEllipsis = true
            };

            this.Controls.Add(_lblSub);
            this.Controls.Add(_lblValue);
            this.Controls.Add(_lblTitle);
        }
    }
}
