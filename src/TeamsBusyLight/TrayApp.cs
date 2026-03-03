using System.Reflection;
using Svg;

namespace TeamsBusyLight;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly SerialService _serial = new();
    private readonly MicDetectionService _mic = new();
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
            Text = "Teams Busy Light — Disconnected",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _pollTimer = new System.Windows.Forms.Timer { Interval = _settings.PollIntervalSeconds * 1000 };
        _pollTimer.Tick += async (_, _) => await PollAsync();

        if (_settings.Mode == DetectionMode.Microphone && !string.IsNullOrEmpty(_settings.ComPort))
            _ = StartAsync();
        else if (_settings.Mode == DetectionMode.GraphApi && !string.IsNullOrEmpty(_settings.ClientId) && !string.IsNullOrEmpty(_settings.ComPort))
            _ = StartAsync();
        else
            ShowSettings();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Force ON", null, (_, _) => { _manualOverride = true; _manualState = true; _serial.SetLight(true); UpdateIcon(true); });
        menu.Items.Add("Force OFF", null, (_, _) => { _manualOverride = true; _manualState = false; _serial.SetLight(false); UpdateIcon(false); });
        menu.Items.Add("Auto", null, (_, _) => { _manualOverride = false; });
        menu.Items.Add(new ToolStripSeparator());
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
                    _trayIcon.Text = "Teams Busy Light — Sign-in failed";
                    _trayIcon.Icon = CreateIcon(Color.Gray);
                    return;
                }
            }
        }

        var modeLabel = _settings.Mode == DetectionMode.Microphone ? "Mic" : "Graph";
        _trayIcon.Text = $"Teams Busy Light — Active ({modeLabel})";
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
        else
        {
            if (_graph is null) return;
            inMeeting = await _graph.IsInMeetingAsync();
        }

        if (inMeeting is null) return;

        if (inMeeting != _lastState)
        {
            _lastState = inMeeting;
            _serial.SetLight(inMeeting.Value);
            UpdateIcon(inMeeting.Value);
        }
    }

    private void UpdateIcon(bool inMeeting)
    {
        _trayIcon.Icon = CreateIcon(inMeeting ? Color.Red : Color.LimeGreen);
        _trayIcon.Text = inMeeting ? "Teams Busy Light — IN MEETING" : "Teams Busy Light — Available";
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
        var form = new Form
        {
            Text = "Teams Busy Light Settings",
            Size = new Size(420, 720),
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
        y += 30;

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

        // Toggle Graph section visibility based on mode
        void UpdateGraphVisibility()
        {
            var visible = rbGraph.Checked;
            foreach (var c in graphControls) c.Visible = visible;
            foreach (var kv in checkboxes) kv.Value.Visible = visible;
        }

        rbMic.CheckedChanged += (_, _) => UpdateGraphVisibility();
        rbGraph.CheckedChanged += (_, _) => UpdateGraphVisibility();

        var sep4 = new Label { Text = "", Location = new Point(20, y), Size = new Size(360, 1), BorderStyle = BorderStyle.Fixed3D };
        y += 10;

        // --- Arduino Tools ---
        var lblArduino = new Label { Text = "Arduino Tools:", Location = new Point(20, y), AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        y += 24;

        var btnFlash = new Button { Text = "Flash Arduino Firmware", Location = new Point(20, y), Width = 175, Height = 30 };
        var btnWiring = new Button { Text = "Wiring Diagram", Location = new Point(205, y), Width = 175, Height = 30 };
        y += 40;

        var lblFlashStatus = new Label { Text = "", Location = new Point(20, y), Size = new Size(360, 40), ForeColor = Color.DimGray };
        y += 45;

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

        // --- Save button ---
        var btnSave = new Button { Text = "Save && Connect", Location = new Point(20, y), Width = 360, Height = 35 };
        btnSave.Click += async (_, _) =>
        {
            _settings.Mode = rbMic.Checked ? DetectionMode.Microphone : DetectionMode.GraphApi;
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
            lblMode, rbMic, rbGraph, sep2,
            lblPort, cmbPort, sep3,
            lblGraphSection, lblClient, txtClient, lblTriggers,
            sep4, lblArduino, btnFlash, btnWiring, lblFlashStatus,
            btnSave
        });

        UpdateGraphVisibility();
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
            Text = "Teams Busy Light — Wiring Diagram",
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

    private void ExitApp()
    {
        _pollTimer.Stop();
        _serial.SetLight(false);
        _serial.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }
}
