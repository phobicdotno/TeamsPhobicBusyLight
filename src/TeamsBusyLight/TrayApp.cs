namespace TeamsBusyLight;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly SerialService _serial = new();
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

        // Auto-start if settings are configured
        if (!string.IsNullOrEmpty(_settings.ClientId) && !string.IsNullOrEmpty(_settings.ComPort))
            _ = StartAsync();
        else
            ShowSettings();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Force ON", null, (_, _) => { _manualOverride = true; _manualState = true; _serial.SetLight(true); UpdateIcon(true); });
        menu.Items.Add("Force OFF", null, (_, _) => { _manualOverride = true; _manualState = false; _serial.SetLight(false); UpdateIcon(false); });
        menu.Items.Add("Auto (Teams)", null, (_, _) => { _manualOverride = false; });
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

        // Sign in to Graph
        if (_graph is null && !string.IsNullOrEmpty(_settings.ClientId))
        {
            _graph = new GraphService(_settings.ClientId);
            var ok = await _graph.SignInAsync();
            if (!ok)
            {
                _trayIcon.Text = "Teams Busy Light — Sign-in failed";
                _trayIcon.Icon = CreateIcon(Color.Gray);
                return;
            }
        }

        _trayIcon.Text = "Teams Busy Light — Active";
        _pollTimer.Start();
    }

    private async Task PollAsync()
    {
        if (_manualOverride || _graph is null) return;

        var inMeeting = await _graph.IsInMeetingAsync();
        if (inMeeting is null) return; // error, keep last state

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
            Size = new Size(400, 250),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lblClient = new Label { Text = "Azure Client ID:", Location = new Point(20, 20), AutoSize = true };
        var txtClient = new TextBox { Text = _settings.ClientId, Location = new Point(20, 45), Width = 340 };

        var lblPort = new Label { Text = "COM Port:", Location = new Point(20, 80), AutoSize = true };
        var cmbPort = new ComboBox { Location = new Point(20, 105), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbPort.Items.AddRange(_serial.GetAvailablePorts());
        if (!string.IsNullOrEmpty(_settings.ComPort))
            cmbPort.SelectedItem = _settings.ComPort;

        var btnSave = new Button { Text = "Save && Connect", Location = new Point(20, 150), Width = 340, Height = 35 };
        btnSave.Click += async (_, _) =>
        {
            _settings.ClientId = txtClient.Text.Trim();
            _settings.ComPort = cmbPort.SelectedItem?.ToString() ?? "";
            _settings.Save();
            form.Close();
            _pollTimer.Stop();
            _graph = null;
            await StartAsync();
        };

        form.Controls.AddRange(new Control[] { lblClient, txtClient, lblPort, cmbPort, btnSave });
        form.ShowDialog();
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
