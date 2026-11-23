using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ScoreBoard;

internal sealed class Match : IDisposable
{
    public Team Home { get; } = new Team("Ev Sahibi");
    public Team Away { get; } = new Team("Deplasman");
    public MatchClock Clock { get; } = new MatchClock();

    public void Reset()
    {
        Home.Score = 0;
        Away.Score = 0;
        Clock.Set(0, 0);
    }

    public void Dispose()
    {
        Home.Dispose();
        Away.Dispose();
    }
}

internal sealed class MatchClock
{
    private readonly Stopwatch stopwatch = new Stopwatch();
    private TimeSpan offset;

    public bool IsRunning => stopwatch.IsRunning;
    public int Seconds => (int)Math.Min((offset + stopwatch.Elapsed).TotalSeconds, 59999);
    public string Display => $"{Seconds / 60:00}:{Seconds % 60:00}";

    public void Start() => stopwatch.Start();
    public void Pause() => stopwatch.Stop();

    public void Set(int minutes, int seconds)
    {
        stopwatch.Reset();
        offset = TimeSpan.FromSeconds(minutes * 60 + seconds);
    }
}

internal sealed class Team : IDisposable
{
    private readonly string defaultName;
    private string name = "";
    private int score;

    public Team(string defaultName) => this.defaultName = defaultName;
    public string Name
    {
        get => string.IsNullOrWhiteSpace(name) ? defaultName : name.Trim();
        set => name = value;
    }

    public int Score
    {
        get => score;
        set => score = Math.Max(0, Math.Min(value, 999));
    }

    public Bitmap? Logo { get; private set; }
    public byte[]? LogoData { get; private set; }

    public void ImportLogo(string path)
    {
        using var file = File.OpenRead(path);
        using var original = Image.FromStream(file, true, true);
        var scale = Math.Min(1d, 640d / Math.Max(original.Width, original.Height));
        using var resized = new Bitmap(
            Math.Max(1, (int)Math.Round(original.Width * scale)),
            Math.Max(1, (int)Math.Round(original.Height * scale)), PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(original, 0, 0, resized.Width, resized.Height);
        }

        using var encoded = new MemoryStream();
        resized.Save(encoded, ImageFormat.Png);
        SetLogo(encoded.ToArray());
    }

    public void SetLogo(byte[]? data)
    {
        Bitmap? next = null;
        if (data != null)
        {
            using var stream = new MemoryStream(data, false);
            using var source = Image.FromStream(stream, true, true);
            next = new Bitmap(source);
        }

        Logo?.Dispose();
        Logo = next;
        LogoData = data;
    }

    public void Dispose() => Logo?.Dispose();
}

[DataContract]
internal sealed class TeamSettings
{
    [DataMember] public string? Name { get; set; }
    [DataMember] public string? Logo { get; set; }

    public static TeamSettings Capture(Team team) => new TeamSettings
    {
        Name = team.Name,
        Logo = team.LogoData == null ? null : Convert.ToBase64String(team.LogoData)
    };

    public void Apply(Team team)
    {
        team.Name = Name ?? "";
        if (Logo == null)
            return;

        try
        {
            team.SetLogo(Convert.FromBase64String(Logo));
        }
        catch (Exception error) when (error is FormatException || error is ArgumentException || error is OutOfMemoryException)
        {
            // A damaged image should not prevent either team from loading.
        }
    }
}

[DataContract]
internal sealed class Preferences
{
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScoreBoard");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "teams.json");
    private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(Preferences));

    [DataMember] public TeamSettings? Home { get; set; }
    [DataMember] public TeamSettings? Away { get; set; }
    [DataMember] public string? DisplayDevice { get; set; }

    public static Preferences Load(Match match)
    {
        Preferences saved;
        try
        {
            using var stream = File.OpenRead(FilePath);
            saved = (Preferences?)Serializer.ReadObject(stream) ?? new Preferences();
        }
        catch (Exception error) when (error is IOException || error is SerializationException || error is UnauthorizedAccessException)
        {
            saved = new Preferences();
        }

        ApplyTeam(saved.Home, match.Home, "Galatasaray", "ScoreBoard.logo.gs.png");
        ApplyTeam(saved.Away, match.Away, "Fenerbahçe", "ScoreBoard.logo.fb.png");
        return saved;
    }

    private static void ApplyTeam(TeamSettings? saved, Team team, string defaultName, string logoResource)
    {
        if (saved != null)
        {
            saved.Apply(team);
            return;
        }

        team.Name = defaultName;
        using var logo = typeof(Preferences).Assembly.GetManifestResourceStream(logoResource);
        if (logo == null)
            return;

        using var data = new MemoryStream();
        logo.CopyTo(data);
        team.SetLogo(data.ToArray());
    }

    public static void Save(Match match, string? displayDevice)
    {
        var saved = new Preferences
        {
            Home = TeamSettings.Capture(match.Home),
            Away = TeamSettings.Capture(match.Away),
            DisplayDevice = displayDevice
        };
        Directory.CreateDirectory(DirectoryPath);
        string temporaryPath = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = File.Create(temporaryPath))
                Serializer.WriteObject(stream, saved);

            if (File.Exists(FilePath))
                File.Replace(temporaryPath, FilePath, null);
            else
                File.Move(temporaryPath, FilePath);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
