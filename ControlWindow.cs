using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScoreBoard;

internal sealed class ControlWindow : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(15, 22, 33);
    private static readonly Color PanelColor = Color.FromArgb(24, 35, 50);
    private static readonly Color FieldColor = Color.FromArgb(34, 49, 67);
    private static readonly Color TextColor = Color.FromArgb(242, 247, 255);
    private static readonly Color MutedColor = Color.FromArgb(171, 189, 211);
    private static readonly Color AccentColor = Color.FromArgb(56, 232, 181);

    private readonly Match match = new Match();
    private readonly BoardView preview;
    private readonly Timer refresh = new Timer { Interval = 100 };
    private readonly ComboBox monitors = new ComboBox();
    private readonly Button displayButton;
    private readonly Button clockButton;
    private readonly Label clockReadout;
    private readonly Label homeScore;
    private readonly Label awayScore;
    private readonly NumericUpDown minutes;
    private readonly NumericUpDown seconds;
    private DisplayWindow? display;
    private string? preferredDevice;
    private int displayedSeconds = -1;
    private bool updatingMonitors;

    public ControlWindow()
    {
        preferredDevice = Preferences.Load(match).DisplayDevice;
        Text = "ScoreBoard";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(770, 648);
        ClientSize = new Size(1060, 755);

        var page = Grid(1, 5);
        page.Padding = new Padding(22, 11, 22, 11);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(page);
        page.Controls.Add(Caption("ScoreBoard", 24, TextColor, true), 0, 0);

        preview = new BoardView(match) { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 10) };
        page.Controls.Add(preview, 0, 1);

        var displayBar = Grid(5, 1);
        displayBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 91));
        displayBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        displayBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195));
        displayBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        displayBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        displayBar.Controls.Add(Caption("Önizleme", 10, MutedColor), 0, 0);

        monitors.Dock = DockStyle.Top;
        monitors.Margin = new Padding(0, 7, 0, 0);
        monitors.AccessibleName = "Skor tabelasının gösterileceği ekran";
        monitors.DropDownStyle = ComboBoxStyle.DropDownList;
        monitors.FlatStyle = FlatStyle.Flat;
        monitors.BackColor = SystemColors.Window;
        monitors.ForeColor = SystemColors.WindowText;
        monitors.SelectedIndexChanged += (_, _) =>
        {
            if (updatingMonitors || !(monitors.SelectedItem is MonitorItem item))
                return;

            preferredDevice = item.Monitor.DeviceName;
            display?.MoveTo(item.Monitor);
        };
        displayBar.Controls.Add(monitors, 2, 0);
        displayButton = ActionButton("Tam ekran aç", (_, _) => ToggleDisplay(), true);
        displayBar.Controls.Add(displayButton, 4, 0);
        page.Controls.Add(displayBar, 0, 2);

        var controls = Grid(5, 1);
        controls.Margin = new Padding(0, 10, 0, 0);
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        controls.Controls.Add(BuildTeamPanel(match.Home, "Ev sahibi", BoardView.HomeColor, out homeScore), 0, 0);
        controls.Controls.Add(BuildTeamPanel(match.Away, "Deplasman", BoardView.AwayColor, out awayScore), 2, 0);

        var clockPanel = Card(out var clockContent);
        clockContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        clockContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        clockContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        clockContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        clockContent.Controls.Add(Caption("Maç saati", 12, TextColor, true), 0, 0);
        clockReadout = Caption("00:00", 25, TextColor, true, ContentAlignment.MiddleCenter, "Arial");
        clockReadout.AccessibleName = "Maç süresi";
        clockContent.Controls.Add(clockReadout, 0, 1);
        clockButton = ActionButton("Başlat", (_, _) => ToggleClock(), true);
        clockContent.Controls.Add(clockButton, 0, 2);
        var clockFields = Grid(3, 2);
        clockFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        clockFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        clockFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        clockFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        clockFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        minutes = TimeField(999, "Dakika");
        seconds = TimeField(59, "Saniye");
        clockFields.Controls.Add(Caption("Dakika", 8, MutedColor), 0, 0);
        clockFields.Controls.Add(Caption("Saniye", 8, MutedColor), 1, 0);
        clockFields.Controls.Add(minutes, 0, 1);
        clockFields.Controls.Add(seconds, 1, 1);
        clockFields.Controls.Add(ActionButton("Süreyi ayarla", (_, _) =>
        {
            match.Clock.Set((int)minutes.Value, (int)seconds.Value);
            UpdateBoard();
        }), 2, 1);
        clockContent.Controls.Add(clockFields, 0, 3);
        controls.Controls.Add(clockPanel, 4, 0);
        page.Controls.Add(controls, 0, 3);

        var footer = Grid(2, 1);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        footer.Padding = new Padding(0, 8, 0, 0);
        footer.Controls.Add(Caption("Tam ekrandan çıkmak için Esc tuşuna basın.", 9, MutedColor), 0, 0);
        footer.Controls.Add(ActionButton("Maçı sıfırla", (_, _) => ResetMatch()), 1, 0);
        page.Controls.Add(footer, 0, 4);

        refresh.Tick += (_, _) =>
        {
            if (match.Clock.Seconds >= 59999 && match.Clock.IsRunning)
            {
                match.Clock.Pause();
                UpdateBoard();
            }
            else if (match.Clock.Seconds != displayedSeconds)
                UpdateBoard();
        };
        RefreshMonitors();
        UpdateBoard();
        refresh.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var workArea = Screen.FromControl(this).WorkingArea;
        var available = new Size(Math.Max(MinimumSize.Width, workArea.Width - 32),
            Math.Max(MinimumSize.Height, workArea.Height - 26));
        Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
        Location = new Point(workArea.Left + (workArea.Width - Width) / 2, workArea.Top + (workArea.Height - Height) / 2);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        refresh.Dispose();
        display?.Close();
        match.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            Preferences.Save(match, preferredDevice);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is SerializationException)
        {
            if (e.CloseReason == CloseReason.UserClosing)
                MessageBox.Show(this, "Takım ve ekran ayarları kaydedilemedi.", "ScoreBoard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        base.OnFormClosing(e);
    }

    private Panel BuildTeamPanel(Team team, string role, Color accent, out Label score)
    {
        var panel = Card(out var content);
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        content.Controls.Add(Caption(role, 12, accent, true), 0, 0);
        var nameInput = new TextBox
        {
            Text = team.Name, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 0), MaxLength = 60,
            BackColor = FieldColor, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = role + " takımının adı"
        };
        nameInput.TextChanged += (_, _) =>
        {
            team.Name = nameInput.Text;
            UpdateBoard();
        };
        content.Controls.Add(nameInput, 0, 1);

        var scoreLine = Grid(3, 1);
        scoreLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        scoreLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scoreLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        var minus = ActionButton("−", (_, _) => { team.Score--; UpdateBoard(); });
        var plus = ActionButton("+", (_, _) => { team.Score++; UpdateBoard(); });
        minus.AccessibleName = role + " skoru azalt";
        plus.AccessibleName = role + " skoru artır";
        score = Caption("0", 25, TextColor, true, ContentAlignment.MiddleCenter, "Arial");
        score.AccessibleName = role + " skoru";
        scoreLine.Controls.Add(minus, 0, 0);
        scoreLine.Controls.Add(score, 1, 0);
        scoreLine.Controls.Add(plus, 2, 0);
        scoreLine.Padding = new Padding(0, 6, 0, 6);
        content.Controls.Add(scoreLine, 0, 2);

        var logos = Grid(3, 1);
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6));
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        var remove = ActionButton("Kaldır", (_, _) => { team.SetLogo(null); UpdateBoard(); });
        remove.AccessibleName = role + " logosunu kaldır";
        remove.Enabled = team.Logo != null;
        remove.Click += (_, _) => remove.Enabled = false;
        var upload = ActionButton("Logo yükle", (_, _) => { ImportLogo(team); remove.Enabled = team.Logo != null; });
        upload.AccessibleName = role + " logosunu yükle";
        logos.Controls.Add(upload, 0, 0);
        logos.Controls.Add(remove, 2, 0);
        content.Controls.Add(logos, 0, 3);
        return panel;
    }

    private void ImportLogo(Team team)
    {
        using var dialog = new OpenFileDialog
        {
            Title = team.Name + " logosunu seçin",
            Filter = "Görsel dosyaları|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            team.ImportLogo(dialog.FileName);
            UpdateBoard();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
            error is ArgumentException || error is ExternalException || error is OutOfMemoryException)
        {
            MessageBox.Show(this, "Görsel açılamadı. Geçerli bir PNG, JPEG veya BMP dosyası seçin.",
                "Logo yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ToggleClock()
    {
        if (match.Clock.IsRunning)
            match.Clock.Pause();
        else if (match.Clock.Seconds < 59999)
            match.Clock.Start();
        UpdateBoard();
    }

    private void ResetMatch()
    {
        if (MessageBox.Show(this, "İki takımın skoru ve maç süresi sıfırlansın mı?", "Maçı sıfırla",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        match.Reset();
        minutes.Value = 0;
        seconds.Value = 0;
        UpdateBoard();
    }

    private void UpdateBoard()
    {
        homeScore.Text = match.Home.Score.ToString();
        awayScore.Text = match.Away.Score.ToString();
        clockReadout.Text = match.Clock.Display;
        clockButton.Text = match.Clock.IsRunning ? "Duraklat" : match.Clock.Seconds == 0 ? "Başlat" : "Devam et";
        clockButton.Enabled = match.Clock.Seconds < 59999;
        displayedSeconds = match.Clock.Seconds;
        preview.Invalidate();
        display?.Board.Invalidate();
    }

    private void ToggleDisplay()
    {
        if (display != null)
        {
            display.Close();
            return;
        }

        if (!(monitors.SelectedItem is MonitorItem item))
            return;

        preferredDevice = item.Monitor.DeviceName;
        var window = new DisplayWindow(match);
        display = window;
        window.FormClosed += (_, _) =>
        {
            display = null;
            displayButton.Text = "Tam ekran aç";
        };
        window.MoveTo(item.Monitor);
        window.Show();
        window.MoveTo(item.Monitor);
        displayButton.Text = "Tam ekran kapat";
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke(new Action(RefreshMonitors));
    }

    private void RefreshMonitors()
    {
        var screens = Screen.AllScreens.OrderByDescending(screen => screen.Primary).ToArray();
        var current = screens.FirstOrDefault(screen => screen.DeviceName == preferredDevice);
        var selected = current ?? screens.FirstOrDefault(screen => !screen.Primary) ?? screens.FirstOrDefault();
        updatingMonitors = true;
        monitors.Items.Clear();
        for (int i = 0; i < screens.Length; i++)
        {
            var item = new MonitorItem(screens[i], i + 1);
            int index = monitors.Items.Add(item);
            if (screens[i] == selected)
                monitors.SelectedIndex = index;
        }
        updatingMonitors = false;
        displayButton.Enabled = selected != null;
        if (selected != null)
            display?.MoveTo(selected);
    }

    private static TableLayoutPanel Grid(int columns, int rows) => new TableLayoutPanel
    {
        Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty
    };

    private static Panel Card(out TableLayoutPanel content)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(13, 10, 13, 10),
            BackColor = PanelColor
        };
        content = Grid(1, 4);
        panel.Controls.Add(content);
        return panel;
    }

    private static Label Caption(string value, float fontSize, Color color, bool bold = false,
        ContentAlignment alignment = ContentAlignment.MiddleLeft, string family = "Segoe UI") => new Label
    {
        Text = value, Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = alignment,
        ForeColor = color, Font = new Font(family, fontSize, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    private static Button ActionButton(string value, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = value, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentColor : FieldColor, ForeColor = primary ? BackgroundColor : TextColor,
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static NumericUpDown TimeField(decimal max, string name) => new NumericUpDown
    {
        Dock = DockStyle.Top, Margin = new Padding(0, 6, 5, 0), Maximum = max,
        BackColor = FieldColor, ForeColor = TextColor, TextAlign = HorizontalAlignment.Center,
        AccessibleName = name, BorderStyle = BorderStyle.FixedSingle
    };

    private sealed class MonitorItem
    {
        public Screen Monitor { get; }
        private readonly int position;

        public MonitorItem(Screen monitor, int position)
        {
            Monitor = monitor;
            this.position = position;
        }

        public override string ToString() =>
            $"{position}. ekran · {Monitor.Bounds.Width} × {Monitor.Bounds.Height}";
    }
}
