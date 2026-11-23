using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace ScoreBoard;

internal sealed class BoardView : Control
{
    private const float LayoutWidth = 1240;
    private const float LayoutHeight = 644;
    private static readonly Color HomeColor = Color.FromArgb(185, 45, 40);
    private static readonly Color AwayColor = Color.FromArgb(29, 81, 139);
    private static readonly Color BackgroundColor = Color.FromArgb(241, 240, 234);
    private static readonly Color FrameColor = Color.FromArgb(35, 39, 43);
    private static readonly Color ClockColor = Color.FromArgb(255, 200, 44);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly Match match;

    public BoardView(Match match)
    {
        this.match = match;
        BackColor = BackgroundColor;
        AccessibleName = "Canlı skor tabelası";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
            return;

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float scale = Math.Min(ClientSize.Width / LayoutWidth, ClientSize.Height / LayoutHeight);
        graphics.TranslateTransform((ClientSize.Width - LayoutWidth * scale) / 2, (ClientSize.Height - LayoutHeight * scale) / 2);
        graphics.ScaleTransform(scale, scale);

        DrawText(graphics, "ŞİLE STADI", new RectangleF(32, 8, 1176, 65), 48, FrameColor);
        FillRounded(graphics, new RectangleF(24, 85, 1192, 486), 15, FrameColor);
        FillRounded(graphics, new RectangleF(34, 95, 1172, 466), 8, Color.FromArgb(252, 251, 247));

        DrawTeamName(graphics, match.Home, HomeColor, 44);
        DrawTeamName(graphics, match.Away, AwayColor, 632);

        DrawLogo(graphics, match.Home, HomeColor, new RectangleF(67, 214, 200, 222));
        DrawLogo(graphics, match.Away, AwayColor, new RectangleF(973, 214, 200, 222));
        string homeScore = match.Home.Score.ToString();
        string awayScore = match.Away.Score.ToString();
        int scoreDigits = Math.Max(homeScore.Length, awayScore.Length);
        float scoreSize = scoreDigits == 1 ? 266 : scoreDigits == 2 ? 218 : 148;
        DrawText(graphics, homeScore, new RectangleF(279, 178, 300, 270), scoreSize, HomeColor, "Arial");
        DrawText(graphics, awayScore, new RectangleF(661, 178, 300, 270), scoreSize, AwayColor, "Arial");
        FillRounded(graphics, new RectangleF(591, 315, 58, 14), 3, FrameColor);

        FillRounded(graphics, new RectangleF(336, 454, 568, 96), 12, FrameColor);
        var clockTransform = graphics.Save();
        graphics.TranslateTransform(620, 454);
        graphics.ScaleTransform(1.4f, 1);
        DrawText(graphics, match.Clock.Display, new RectangleF(-185, 0, 370, 96), 94, ClockColor, "Arial");
        graphics.Restore(clockTransform);

        string localDateTime = DateTime.Now.ToString("dd MMMM yyyy  ·  HH:mm", Turkish);
        DrawText(graphics, localDateTime, new RectangleF(32, 576, 1176, 56), 30, FrameColor);
    }

    private static void DrawTeamName(Graphics graphics, Team team, Color accent, float x)
    {
        FillRounded(graphics, new RectangleF(x, 105, 564, 86), 9, accent);
        DrawText(graphics, team.Name.ToUpper(Turkish), new RectangleF(x + 18, 105, 528, 86), 64, Color.White);
    }

    private static void DrawLogo(Graphics graphics, Team team, Color accent, RectangleF bounds)
    {
        if (team.Logo is Image logo)
        {
            float scale = Math.Min(bounds.Width / logo.Width, bounds.Height / logo.Height);
            float width = logo.Width * scale;
            float height = logo.Height * scale;
            graphics.DrawImage(logo, new RectangleF(
                bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height));
            return;
        }

        float size = bounds.Width * 0.7f;
        var badge = new RectangleF(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
        FillRounded(graphics, badge, 19, Color.FromArgb(28, accent));
        string[] words = team.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string initials = words.Length == 1 ? words[0].Substring(0, 1) : words[0].Substring(0, 1) + words[1].Substring(0, 1);
        DrawText(graphics, initials.ToUpper(Turkish), badge, size * 0.43f, accent);
    }

    private static void DrawText(Graphics graphics, string text, RectangleF bounds, float maximumSize, Color color,
        string family = "Segoe UI")
    {
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
        };
        using var brush = new SolidBrush(color);
        using var maximumFont = new Font(family, maximumSize, FontStyle.Bold, GraphicsUnit.Pixel);
        float measured = graphics.MeasureString(text, maximumFont, new SizeF(10000, 1000), format).Width;
        if (measured <= bounds.Width)
        {
            graphics.DrawString(text, maximumFont, brush, bounds, format);
            return;
        }

        using var fittedFont = new Font(family, maximumSize * bounds.Width / measured, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString(text, fittedFont, brush, bounds, format);
    }

    private static void FillRounded(Graphics graphics, RectangleF bounds, float radius, Color color)
    {
        float diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }
}

internal sealed class DisplayWindow : Form
{
    public BoardView Board { get; }

    public DisplayWindow(Match match)
    {
        Text = "ScoreBoard · Skor tabelası";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Board = new BoardView(match) { Dock = DockStyle.Fill };
        Controls.Add(Board);
    }

    // A kiosk window should cover the selected monitor without changing its desktop resolution.
    public void MoveTo(Screen monitor) => Bounds = monitor.Bounds;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
