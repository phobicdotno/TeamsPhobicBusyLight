using System.Reflection;
using Svg;

namespace TeamsPhobicBusyLight;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly SerialService _serial = new();
    private readonly MicDetectionService _mic = new();
    private readonly TeamsLogDetectionService _teamsLog = new();
    private GraphService? _graph;
    private AppSettings _settings;
    private bool? _lastState;
    private bool _manualOverride;
    private bool _manualState;

    public TrayApp()
    {
        _settings = AppSettings.Load();

        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(Color.Gray),
            Text = "Teams Phobic Busy Light — Disconnected",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _pollTimer = new System.Windows.Forms.Timer { Interval = _settings.PollIntervalSeconds * 1000 };
        _pollTimer.Tick += async (_, _) => await PollAsync();

        if (_settings.Mode == DetectionMode.Microphone && !string.IsNullOrEmpty(_settings.ComPort))
            _ = StartAsync();
        else if (_settings.Mode == DetectionMode.GraphApi && !string.IsNullOrEmpty(_settings.ClientId) && !string.IsNullOrEmpty(_settings.ComPort))
            _ = StartAsync();
        else if (_settings.Mode == DetectionMode.TeamsLogFile && !string.IsNullOrEmpty(_settings.ComPort))
            _ = StartAsync();
        else
            ShowSettings();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var versionItem = new ToolStripMenuItem($"Teams Phobic Busy Light {UpdateChecker.CurrentVersion}")
        {
            Enabled = false
        };
        menu.Items.Add(versionItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Force ON", null, (_, _) => { _manualOverride = true; _manualState = true; _serial.SetState(LightState.Busy); UpdateIcon(true); });
        menu.Items.Add("Force OFF", null, (_, _) => { _manualOverride = true; _manualState = false; _serial.SetState(LightState.Available); UpdateIcon(false); });
        menu.Items.Add("Auto", null, (_, _) => { _manualOverride = false; });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Check for Updates", null, async (_, _) => await CheckForUpdatesAsync());
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private async Task StartAsync()
    {
        // Open serial
        if (!_serial.IsOpen && !string.IsNullOrEmpty(_settings.ComPort))
            _serial.Open(_settings.ComPort);

        if (_settings.Mode == DetectionMode.GraphApi)
        {
            // Sign in to Graph
            if (_graph is null && !string.IsNullOrEmpty(_settings.ClientId))
            {
                _graph = new GraphService(_settings.ClientId, _settings.GetActiveActivities());
                var ok = await _graph.SignInAsync();
                if (!ok)
                {
                    _trayIcon.Text = "Teams Phobic Busy Light — Sign-in failed";
                    _trayIcon.Icon = CreateIcon(Color.Gray);
                    return;
                }
            }
        }
        else if (_settings.Mode == DetectionMode.TeamsLogFile)
        {
            _teamsLog.DetectTeamsInstallation();
        }

        var modeLabel = _settings.Mode switch
        {
            DetectionMode.Microphone => "Mic",
            DetectionMode.GraphApi => "Graph",
            DetectionMode.TeamsLogFile => "Teams Log",
            _ => "Unknown"
        };
        _trayIcon.Text = $"Teams Phobic Busy Light — Active ({modeLabel})";
        _pollTimer.Start();
    }

    private async Task PollAsync()
    {
        if (_manualOverride) return;

        bool? inMeeting;

        if (_settings.Mode == DetectionMode.Microphone)
        {
            inMeeting = _mic.IsMicrophoneInUse();
        }
        else if (_settings.Mode == DetectionMode.TeamsLogFile)
        {
            inMeeting = _teamsLog.IsInMeeting();
        }
        else
        {
            if (_graph is null) return;
            inMeeting = await _graph.IsInMeetingAsync();
        }

        if (inMeeting is null) return;

        if (inMeeting != _lastState)
        {
            _lastState = inMeeting;
            _serial.SetState(inMeeting.Value ? LightState.Busy : LightState.Available);
            UpdateIcon(inMeeting.Value);
        }
    }

    private void UpdateIcon(bool inMeeting)
    {
        _trayIcon.Icon = CreateIcon(inMeeting ? Color.Red : Color.LimeGreen);
        _trayIcon.Text = inMeeting ? "Teams Phobic Busy Light — IN MEETING" : "Teams Phobic Busy Light — Available";
    }

    private static Icon CreateIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void ShowSettings()
    {
        // Detect Teams installation for the settings dialog
        _teamsLog.DetectTeamsInstallation();

        var form = new Form
        {
            Text = "Teams Phobic Busy Light Settings",
            Width = 420,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            AutoScroll = true
        };

        var y = 15;

        // --- Debug status ---
        var lblDebug = new Label { Text = "Current Status:", Location = new Point(20, y), AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        y += 22;

        string debugInfo;
        if (_settings.Mode == DetectionMode.Microphone)
        {
            var micActive = _mic.IsMicrophoneInUse();
            debugInfo = $"Mode: Microphone Detection\nMic in use: {(micActive ? "YES" : "No")}\nActive app: {_mic.LastActiveApp ?? "—"}";
        }
        else if (_settings.Mode == DetectionMode.TeamsLogFile)
        {
            debugInfo = $"Mode: Teams Log File\nTeams: {_teamsLog.TeamsVersion ?? "—"}\nStatus: {_teamsLog.LastStatus ?? "—"}";
        }
        else
        {
            debugInfo = $"Mode: Graph API\nAvailability: {_graph?.LastAvailability ?? "—"}\nActivity: {_graph?.LastActivity ?? "—"}";
        }
        debugInfo += $"\nLight: {(_lastState == true ? "ON" : _lastState == false ? "OFF" : "—")}";

        var lblStatus = new Label { Text = debugInfo, Location = new Point(20, y), AutoSize = true, ForeColor = Color.DimGray };
        y += 68;

        var sep1 = new Label { Text = "", Location = new Point(20, y), Size = new Size(360, 1), BorderStyle = BorderStyle.Fixed3D };
        y += 10;

        // --- Detection mode ---
        var lblMode = new Label { Text = "Detection Mode:", Location = new Point(20, y), AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        y += 24;

        var rbMic = new RadioButton { Text = "Microphone in-use (zero config, works with any app)", Location = new Point(20, y), AutoSize = true, Checked = _settings.Mode == DetectionMode.Microphone };
        y += 24;
        var rbGraph = new RadioButton { Text = "Microsoft Graph API (Teams-specific, needs Azure ID)", Location = new Point(20, y), AutoSize = true, Checked = _settings.Mode == DetectionMode.GraphApi };
        y += 24;
        var rbTeamsLog = new RadioButton { Text = "Teams log file (local, no Azure ID — New Teams only)", Location = new Point(20, y), AutoSize = true, Checked = _settings.Mode == DetectionMode.TeamsLogFile };
        y += 22;

        // Teams detection status label
        var teamsDetectColor = _teamsLog.TeamsFound && _teamsLog.TeamsVersion == "New Teams (MSIX)" ? Color.Green : Color.OrangeRed;
        var lblTeamsDetect = new Label
        {
            Text = _teamsLog.DetectionInfo ?? "Teams not checked.",
            Location = new Point(36, y),
            AutoSize = true,
            ForeColor = teamsDetectColor,
            Font = new Font(Control.DefaultFont, FontStyle.Italic)
        };
        y += 22;

        var sep2 = new Label { Text = "", Location = new Point(20, y), Size = new Size(360, 1), BorderStyle = BorderStyle.Fixed3D };
        y += 10;

        // --- COM port ---
        var lblPort = new Label { Text = "COM Port:", Location = new Point(20, y), AutoSize = true };
        y += 22;
        var cmbPort = new ComboBox { Location = new Point(20, y), Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbPort.Items.AddRange(_serial.GetAvailablePorts());
        if (!string.IsNullOrEmpty(_settings.ComPort))
            cmbPort.SelectedItem = _settings.ComPort;
        y += 32;

        var sep3 = new Label { Text = "", Location = new Point(20, y), Size = new Size(360, 1), BorderStyle = BorderStyle.Fixed3D };
        y += 10;

        // --- Graph API settings (shown/hidden based on mode) ---
        var graphStartY = y;

        var lblGraphSection = new Label { Text = "Graph API Settings:", Location = new Point(20, y), AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        y += 22;

        var lblClient = new Label { Text = "Azure Client ID:", Location = new Point(20, y), AutoSize = true };
        y += 22;
        var txtClient = new TextBox { Text = _settings.ClientId, Location = new Point(20, y), Width = 360 };
        y += 28;

        var lblTriggers = new Label { Text = "Turn light ON for:", Location = new Point(20, y), AutoSize = true };
        y += 20;

        var graphControls = new List<Control> { lblGraphSection, lblClient, txtClient, lblTriggers };

        var checkboxes = new Dictionary<string, CheckBox>();
        foreach (var kv in _settings.ActivityTriggers)
        {
            var cb = new CheckBox { Text = kv.Key, Checked = kv.Value, Location = new Point(20, y), AutoSize = true };
            checkboxes[kv.Key] = cb;
            graphControls.Add(cb);
            form.Controls.Add(cb);
            y += 22;
        }
        y += 10;

        var graphEndY = y;

        // --- Controls below Graph section (repositioned dynamically) ---
        var sep4 = new Label { Text = "", Size = new Size(360, 1), BorderStyle = BorderStyle.Fixed3D };
        var lblArduino = new Label { Text = "Arduino Tools:", AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        var btnFlash = new Button { Text = "Flash Arduino Firmware", Width = 175, Height = 30 };
        var btnWiring = new Button { Text = "Wiring Diagram", Width = 175, Height = 30 };
        var lblFlashStatus = new Label { Text = "", Size = new Size(360, 40), ForeColor = Color.DimGray };
        var sep5 = new Label { Text = "", Size = new Size(360, 1), BorderStyle = BorderStyle.Fixed3D };
        var btnUpdate = new Button { Text = "Check for Updates", Width = 175, Height = 30 };
        var lblVersion = new Label { Text = UpdateChecker.CurrentVersion, AutoSize = true, ForeColor = Color.DimGray };
        var btnSave = new Button { Text = "Save && Connect", Width = 360, Height = 35 };

        // Reposition all controls below Graph section and resize form
        void RepositionControls()
        {
            var graphVisible = rbGraph.Checked;
            foreach (var c in graphControls) c.Visible = graphVisible;
            foreach (var kv in checkboxes) kv.Value.Visible = graphVisible;

            // Show Teams detection info only when Teams log mode selected
            lblTeamsDetect.Visible = rbTeamsLog.Checked;

            var cy = graphVisible ? graphEndY : graphStartY;

            sep4.Location = new Point(20, cy); cy += 10;
            lblArduino.Location = new Point(20, cy); cy += 24;
            btnFlash.Location = new Point(20, cy);
            btnWiring.Location = new Point(205, cy); cy += 40;
            lblFlashStatus.Location = new Point(20, cy); cy += 45;
            sep5.Location = new Point(20, cy); cy += 10;
            btnUpdate.Location = new Point(20, cy);
            lblVersion.Location = new Point(205, cy + 6); cy += 40;
            btnSave.Location = new Point(20, cy); cy += 50;

            form.ClientSize = new Size(form.ClientSize.Width, cy);
        }

        rbMic.CheckedChanged += (_, _) => RepositionControls();
        rbGraph.CheckedChanged += (_, _) => RepositionControls();
        rbTeamsLog.CheckedChanged += (_, _) => RepositionControls();

        btnFlash.Click += async (_, _) =>
        {
            var port = cmbPort.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(port))
            {
                lblFlashStatus.Text = "Select a COM port first.";
                lblFlashStatus.ForeColor = Color.OrangeRed;
                return;
            }

            btnFlash.Enabled = false;
            lblFlashStatus.ForeColor = Color.DimGray;
            var (success, output) = await ArduinoFlasher.FlashAsync(port, msg =>
            {
                if (form.InvokeRequired)
                    form.Invoke(() => lblFlashStatus.Text = msg);
                else
                    lblFlashStatus.Text = msg;
            });

            btnFlash.Enabled = true;
            lblFlashStatus.ForeColor = success ? Color.Green : Color.OrangeRed;
            lblFlashStatus.Text = success ? "Flash complete!" : $"Flash failed: {output[..Math.Min(output.Length, 120)]}";
        };

        btnWiring.Click += (_, _) => ShowWiringDiagram();
        btnUpdate.Click += async (_, _) => await CheckForUpdatesAsync();
        btnSave.Click += async (_, _) =>
        {
            if (rbMic.Checked)
                _settings.Mode = DetectionMode.Microphone;
            else if (rbGraph.Checked)
                _settings.Mode = DetectionMode.GraphApi;
            else
                _settings.Mode = DetectionMode.TeamsLogFile;

            _settings.ComPort = cmbPort.SelectedItem?.ToString() ?? "";
            _settings.ClientId = txtClient.Text.Trim();
            foreach (var kv in checkboxes)
                _settings.ActivityTriggers[kv.Key] = kv.Value.Checked;
            _settings.Save();
            form.Close();

            // Restart
            _pollTimer.Stop();
            _graph = null;
            _lastState = null;
            await StartAsync();
        };

        form.Controls.AddRange(new Control[] {
            lblDebug, lblStatus, sep1,
            lblMode, rbMic, rbGraph, rbTeamsLog, lblTeamsDetect, sep2,
            lblPort, cmbPort, sep3,
            lblGraphSection, lblClient, txtClient, lblTriggers,
            sep4, lblArduino, btnFlash, btnWiring, lblFlashStatus,
            sep5, btnUpdate, lblVersion,
            btnSave
        });

        RepositionControls();
        form.ShowDialog();
    }

    private static void ShowWiringDiagram()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("wiring-diagram.svg", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            MessageBox.Show("Wiring diagram resource not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var svgDoc = SvgDocument.Open<SvgDocument>(stream);
        var bmp = svgDoc.Draw(700, 420);

        var diagramForm = new Form
        {
            Text = "Teams Phobic Busy Light — Wiring Diagram",
            ClientSize = new Size(700, 420),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        var pictureBox = new PictureBox
        {
            Image = bmp,
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        diagramForm.Controls.Add(pictureBox);
        diagramForm.ShowDialog();
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await UpdateChecker.CheckForUpdateAsync();
        if (update is null)
        {
            MessageBox.Show($"You're up to date! ({UpdateChecker.CurrentVersion})", "No Updates",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var msg = $"Update to {update.TagName}? (current: {UpdateChecker.CurrentVersion})\nThe app will restart.";
        if (update.HexUrl is not null)
            msg += "\nIncludes firmware update.";

        var result = MessageBox.Show(msg, "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (result == DialogResult.Yes)
        {
            if (update.ExeUrl is not null)
            {
                try
                {
                    await UpdateChecker.SelfUpdateAsync(update.ExeUrl);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Update failed: {ex.Message}\n\nOpening release page instead.",
                        "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    UpdateChecker.OpenReleasePage(update.HtmlUrl);
                }
            }
            else
            {
                UpdateChecker.OpenReleasePage(update.HtmlUrl);
            }
        }
    }

    private void ExitApp()
    {
        _pollTimer.Stop();
        _serial.SetState(LightState.Off);
        _serial.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }
}
