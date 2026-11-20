using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ScoreBoard;

internal sealed class BoardView : Control
{
    public static readonly Color HomeColor = Color.FromArgb(56, 232, 181);
    public static readonly Color AwayColor = Color.FromArgb(255, 186, 84);
    private static readonly Color BackgroundColor = Color.FromArgb(8, 14, 24);
    private static readonly Color White = Color.FromArgb(246, 250, 255);
    private static readonly Color Muted = Color.FromArgb(167, 185, 208);
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

        using (var backdrop = new LinearGradientBrush(ClientRectangle, Color.FromArgb(20, 35, 52), BackgroundColor, 85f))
            graphics.FillRectangle(backdrop, ClientRectangle);

        float ratio = (float)ClientSize.Width / ClientSize.Height;
        bool portrait = ratio < 1.18f;
        float height = portrait ? 900 : 640;
        float width = portrait ? Math.Max(460, Math.Min(900, ratio * height)) : Math.Max(755, Math.Min(1560, ratio * height));
        float scale = Math.Min(ClientSize.Width / width, ClientSize.Height / height);
        graphics.TranslateTransform((ClientSize.Width - width * scale) / 2, (ClientSize.Height - height * scale) / 2);
        graphics.ScaleTransform(scale, scale);

        if (portrait)
            DrawPortrait(graphics, width);
        else
            DrawLandscape(graphics, width);
    }

    private void DrawLandscape(Graphics graphics, float width)
    {
        float middle = width / 2;
        DrawClock(graphics, middle, 31);
        float sideWidth = middle - 67;
        DrawTeam(graphics, match.Home, HomeColor, 41, sideWidth);
        DrawTeam(graphics, match.Away, AwayColor, width - 41 - sideWidth, sideWidth);
        FillRounded(graphics, new RectangleF(middle - 16, 416, 32, 7), 3.5f, Muted);
    }

    private void DrawPortrait(Graphics graphics, float width)
    {
        DrawClock(graphics, width / 2, 65);
        DrawPortraitTeam(graphics, match.Home, HomeColor, width, 245);
        DrawPortraitTeam(graphics, match.Away, AwayColor, width, 555);
    }

    private void DrawClock(Graphics graphics, float center, float top)
    {
        FillRounded(graphics, new RectangleF(center - 135, top, 270, 130), 25, Color.FromArgb(225, 8, 17, 29));
        DrawText(graphics, "MAÇ SÜRESİ", new RectangleF(center - 117, top + 13, 234, 24), 17, Muted);
        DrawText(graphics, match.Clock.Display, new RectangleF(center - 125, top + 39, 250, 80), 69, White, "Arial");
    }

    private static void DrawTeam(Graphics graphics, Team team, Color accent, float x, float width)
    {
        var center = x + width / 2;
        FillRounded(graphics, new RectangleF(x, 174, width, 419), 24, Color.FromArgb(215, 12, 23, 37));
        FillRounded(graphics, new RectangleF(center - 31, 171, 62, 5), 2.5f, accent);
        DrawLogo(graphics, team, accent, new RectangleF(center - 66, 226, 132, 132));
        DrawText(graphics, team.Score.ToString(), new RectangleF(x + 10, 356, width - 20, 162), 156, White, "Arial");
        DrawText(graphics, team.Name, new RectangleF(x + 14, 519, width - 28, 57), 39, White);
    }

    private static void DrawPortraitTeam(Graphics graphics, Team team, Color accent, float width, float y)
    {
        FillRounded(graphics, new RectangleF(29, y, width - 58, 239), 23, Color.FromArgb(215, 12, 23, 37));
        FillRounded(graphics, new RectangleF(60, y - 3, 62, 5), 2.5f, accent);
        DrawLogo(graphics, team, accent, new RectangleF(61, y + 48, 107, 107));
        DrawText(graphics, team.Score.ToString(), new RectangleF(185, y + 40, width - 235, 132), 120, White, "Arial");
        DrawText(graphics, team.Name, new RectangleF(48, y + 174, width - 96, 51), 38, White);
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
        DrawText(graphics, initials.ToUpper(new System.Globalization.CultureInfo("tr-TR")), badge, size * 0.43f, accent);
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
