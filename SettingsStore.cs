using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Backdrop;

internal enum CanvasRatio { Auto, Wide16x9, Square1x1, Portrait4x5 }
internal enum CompositionLayout { Auto, Row, Grid }
internal enum BackgroundMode { AutoGradient, SolidColor, CustomGradient, Pattern }
internal enum BackgroundPattern { SoftGrain, Dots }

internal sealed class AppSettings
{
    public int OutputWidth { get; set; } = 1920;
    public int PaddingPercent { get; set; } = 10;
    public double ShadowStrength { get; set; } = 0.18;
    public CanvasRatio CanvasRatio { get; set; } = CanvasRatio.Wide16x9;
    public CompositionLayout Layout { get; set; } = CompositionLayout.Auto;
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.AutoGradient;
    public string BackgroundColor1Hex { get; set; } = "#303137";
    public string BackgroundColor2Hex { get; set; } = "#4B4C53";
    public BackgroundPattern BackgroundPattern { get; set; } = BackgroundPattern.SoftGrain;

    public AppSettings Copy() => new()
    {
        OutputWidth = OutputWidth,
        PaddingPercent = PaddingPercent,
        ShadowStrength = ShadowStrength,
        CanvasRatio = CanvasRatio,
        Layout = Layout,
        BackgroundMode = BackgroundMode,
        BackgroundColor1Hex = BackgroundColor1Hex,
        BackgroundColor2Hex = BackgroundColor2Hex,
        BackgroundPattern = BackgroundPattern
    };

    public bool IsValid() =>
        OutputWidth is >= 640 and <= 4096 &&
        PaddingPercent is >= 0 and <= 25 &&
        double.IsFinite(ShadowStrength) && ShadowStrength is >= 0 and <= 0.4 &&
        Enum.IsDefined(CanvasRatio) &&
        Enum.IsDefined(Layout) &&
        Enum.IsDefined(BackgroundMode) &&
        IsValidColorHex(BackgroundColor1Hex) &&
        IsValidColorHex(BackgroundColor2Hex) &&
        Enum.IsDefined(BackgroundPattern);

    private static bool IsValidColorHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' &&
        uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
}

internal sealed record SettingsLoad(AppSettings Settings, string? Warning);

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<CanvasRatio>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<CompositionLayout>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<BackgroundMode>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<BackgroundPattern>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    public static string PathName => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Backdrop",
        "settings.json");

    public static SettingsLoad Load() => LoadPath(PathName);

    public static SettingsLoad LoadPath(string path)
    {
        if (!File.Exists(path))
            return new(new AppSettings(), null);

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
            if (settings is null || !settings.IsValid())
                return Corrupt();

            return new(settings, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Corrupt();
        }
    }

    public static void Save(AppSettings settings) => SavePath(PathName, settings);

    public static void SavePath(string path, AppSettings settings)
    {
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings), "Settings are outside the supported range.");

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch { }
            throw;
        }
    }

    private static SettingsLoad Corrupt() => new(
        new AppSettings(),
        "Backdrop could not read its saved preferences. It will use the defaults until you save new preferences.");
}
