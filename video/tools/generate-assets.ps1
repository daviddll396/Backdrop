$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$dotnet = Join-Path $repoRoot 'artifacts\tools\dotnet\dotnet.exe'
$outputDirectory = Join-Path $repoRoot 'artifacts\video-source-assets'
$projectDirectory = Join-Path $env:TEMP ("BackdropVideoAssetGenerator-" + [guid]::NewGuid().ToString('N'))
$projectPath = Join-Path $projectDirectory 'GenerateAssets.csproj'
$sourcePath = Join-Path $projectDirectory 'Program.cs'

if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "The pinned local .NET SDK was not found: $dotnet"
}

New-Item -ItemType Directory -Path $projectDirectory | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$backdropProject = [System.Security.SecurityElement]::Escape((Join-Path $repoRoot 'Backdrop.csproj'))

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$backdropProject" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding utf8

@'
using System.Drawing.Imaging;
using System.Reflection;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: GenerateAssets <output-directory>");
    return 2;
}

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);

var backdrop = Assembly.Load("Backdrop");
var rendererType = backdrop.GetType("Backdrop.BackdropRenderer", throwOnError: true)!;
var settingsType = backdrop.GetType("Backdrop.AppSettings", throwOnError: true)!;
var createScreen = rendererType.GetMethod("CreateSampleAppScreen", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(rendererType.FullName, "CreateSampleAppScreen");
var renderBitmaps = rendererType.GetMethod("RenderBitmaps", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(rendererType.FullName, "RenderBitmaps");

using var overview = CreateHighResolutionScreen(createScreen, 0);
using var activity = CreateHighResolutionScreen(createScreen, 1);
using var spending = CreateHighResolutionScreen(createScreen, 2);
overview.Save(Path.Combine(outputDirectory, "input-overview.png"), ImageFormat.Png);
activity.Save(Path.Combine(outputDirectory, "input-activity.png"), ImageFormat.Png);
spending.Save(Path.Combine(outputDirectory, "input-spending.png"), ImageFormat.Png);

SaveComposition([overview], "Auto", "Row", "AutoGradient", "SoftGrain", outputDirectory, renderBitmaps, settingsType);
SaveComposition([overview, activity, spending], "Wide16x9", "Row", "AutoGradient", "SoftGrain", outputDirectory, renderBitmaps, settingsType);
SaveComposition([overview, activity, spending], "Wide16x9", "Row", "Pattern", "SoftGrain", outputDirectory, renderBitmaps, settingsType, "#3B3D40");
SaveComposition([overview, activity, spending], "Wide16x9", "Row", "Pattern", "Dots", outputDirectory, renderBitmaps, settingsType, "#34363A");

return 0;

static Bitmap CreateHighResolutionScreen(MethodInfo createScreen, int page)
{
    using var native = (Bitmap)createScreen.Invoke(null, [400, 800, page])!;
    var highResolution = new Bitmap(800, 1600, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    try
    {
        using var graphics = Graphics.FromImage(highResolution);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(native, new Rectangle(0, 0, highResolution.Width, highResolution.Height));
        return highResolution;
    }
    catch
    {
        highResolution.Dispose();
        throw;
    }
}

static void SaveComposition(
    IReadOnlyList<Bitmap> images,
    string ratio,
    string layout,
    string background,
    string pattern,
    string outputDirectory,
    MethodInfo renderBitmaps,
    Type settingsType,
    string color = "#303137")
{
    var settings = Activator.CreateInstance(settingsType, nonPublic: true)
        ?? throw new InvalidOperationException("Could not create renderer settings.");
    SetEnum(settingsType, settings, "CanvasRatio", ratio);
    SetEnum(settingsType, settings, "Layout", layout);
    SetEnum(settingsType, settings, "BackgroundMode", background);
    SetEnum(settingsType, settings, "BackgroundPattern", pattern);
    settingsType.GetProperty("OutputWidth")!.SetValue(settings, 1920);
    settingsType.GetProperty("PaddingPercent")!.SetValue(settings, 9);
    settingsType.GetProperty("ShadowStrength")!.SetValue(settings, 0.16d);
    settingsType.GetProperty("BackgroundColor1Hex")!.SetValue(settings, color);
    settingsType.GetProperty("BackgroundColor2Hex")!.SetValue(settings, color);

    using var result = (Bitmap)renderBitmaps.Invoke(null, [images, settings, CancellationToken.None])!;
    var name = images.Count == 1 ? "single.png" : background == "Pattern" && pattern == "SoftGrain"
        ? "background-grain.png"
        : background == "Pattern" && pattern == "Dots" ? "background-dots.png"
        : "multi.png";
    result.Save(Path.Combine(outputDirectory, name), ImageFormat.Png);
}

static void SetEnum(Type settingsType, object settings, string name, string value)
{
    var property = settingsType.GetProperty(name)
        ?? throw new MissingMemberException(settingsType.FullName, name);
    property.SetValue(settings, Enum.Parse(property.PropertyType, value));
}
'@ | Set-Content -LiteralPath $sourcePath -Encoding utf8

& $dotnet run --project $projectPath -c Release -- $outputDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Sample asset generation failed with exit code $LASTEXITCODE"
}
