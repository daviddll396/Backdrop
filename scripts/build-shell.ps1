[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\v2'))
$shellRoot = Join-Path $projectRoot 'shell'

if (-not (Test-Path -LiteralPath $outputRoot -PathType Container) -or
    -not (Test-Path -LiteralPath (Join-Path $outputRoot 'Backdrop.exe') -PathType Leaf)) {
    throw "Published Backdrop.exe was not found in $outputRoot. Publish the app to artifacts/v2 first."
}

$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    throw 'Visual Studio Installer vswhere.exe was not found.'
}
$vsPath = & $vswhere -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if (-not $vsPath) {
    throw 'The MSVC x64 toolchain is missing. Install Microsoft.VisualStudio.Component.VC.Tools.x86.x64.'
}
$vcvars = Join-Path $vsPath 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) {
    throw "MSVC setup script was not found: $vcvars"
}

$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
$sdk = Get-ChildItem -LiteralPath $sdkRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.0\.\d+(\.\d+)?$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64' } |
    Where-Object { (Test-Path -LiteralPath (Join-Path $_ 'makeappx.exe') -PathType Leaf) -and
                   (Test-Path -LiteralPath (Join-Path $_ 'makepri.exe') -PathType Leaf) } |
    Select-Object -First 1
if (-not $sdk) {
    throw 'The Windows SDK packaging tools were not found. Install the Windows 11 SDK from Visual Studio Installer.'
}

$assetsRoot = Join-Path $outputRoot 'Assets'
if (-not (Test-Path -LiteralPath $assetsRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $assetsRoot | Out-Null
}
Add-Type -AssemblyName System.Drawing
foreach ($size in @(44, 150)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $margin = [int][Math]::Floor($size * 0.1)
        $graphics.DrawIcon([System.Drawing.SystemIcons]::Application,
            [System.Drawing.Rectangle]::new($margin, $margin, $size - (2 * $margin), $size - (2 * $margin)))
        $bitmap.Save((Join-Path $assetsRoot "Logo$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
$logo = [System.Drawing.Bitmap]::new(64, 64)
$logoGraphics = [System.Drawing.Graphics]::FromImage($logo)
try {
    $logoGraphics.Clear([System.Drawing.Color]::Transparent)
    $logoGraphics.DrawIcon([System.Drawing.SystemIcons]::Application, [System.Drawing.Rectangle]::new(0, 0, 64, 64))
    $logo.Save((Join-Path $assetsRoot 'Logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $logoGraphics.Dispose()
    $logo.Dispose()
}

$commandFile = Join-Path $outputRoot '.build-shell.cmd'
$shellDll = Join-Path $outputRoot 'Backdrop.Shell.dll'
$shellObject = Join-Path $outputRoot 'BackdropShell.obj'
$shellImportLib = Join-Path $outputRoot 'Backdrop.Shell.lib'
$harnessExe = Join-Path $outputRoot 'BackdropShellHarness.exe'
$harnessObject = Join-Path $outputRoot 'BackdropShellHarness.obj'
$outputScripts = Join-Path $outputRoot 'scripts'
$manifest = Join-Path $shellRoot 'AppxManifest.xml'
$definition = Join-Path $shellRoot 'BackdropShell.def'
$lines = @(
    '@echo off',
    'set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"',
    "call `"$vcvars`" >nul",
    'if errorlevel 1 exit /b %errorlevel%',
    "cl.exe /nologo /std:c++17 /EHsc /W4 /O2 /MT /utf-8 /DUNICODE /D_UNICODE /Fo:`"$shellObject`" /c `"$(Join-Path $shellRoot 'BackdropShell.cpp')`"",
    'if errorlevel 1 exit /b %errorlevel%',
    "link.exe /DLL /NOLOGO /MACHINE:X64 `"$shellObject`" /DEF:`"$definition`" /OUT:`"$shellDll`" /IMPLIB:`"$shellImportLib`" shell32.lib ole32.lib uuid.lib",
    'if errorlevel 1 exit /b %errorlevel%',
    "cl.exe /nologo /std:c++17 /EHsc /W4 /O2 /MT /utf-8 /DUNICODE /D_UNICODE /Fo:`"$harnessObject`" /c `"$(Join-Path $shellRoot 'BackdropShellHarness.cpp')`"",
    'if errorlevel 1 exit /b %errorlevel%',
    "link.exe /NOLOGO /MACHINE:X64 `"$harnessObject`" /OUT:`"$harnessExe`" shell32.lib ole32.lib uuid.lib",
    'exit /b %errorlevel%'
)
Set-Content -LiteralPath $commandFile -Value $lines -Encoding Ascii
try {
    & $env:ComSpec /d /c $commandFile
    if ($LASTEXITCODE -ne 0) {
        throw "MSVC failed to build the shell command (exit code $LASTEXITCODE)."
    }
}
finally {
    $expectedCommandFile = Join-Path $outputRoot '.build-shell.cmd'
    if ([IO.Path]::GetFullPath($commandFile) -eq [IO.Path]::GetFullPath($expectedCommandFile)) {
        Remove-Item -LiteralPath $expectedCommandFile -Force -ErrorAction SilentlyContinue
    }
}

Copy-Item -LiteralPath $manifest -Destination (Join-Path $outputRoot 'AppxManifest.xml') -Force
$stageRoot = Join-Path $outputRoot ('.sparse-package-stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stageRoot | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $outputRoot 'AppxManifest.xml') -Destination $stageRoot
    Copy-Item -LiteralPath $assetsRoot -Destination $stageRoot -Recurse
    & (Join-Path $sdk 'makeappx.exe') pack /d $stageRoot /p (Join-Path $outputRoot 'Backdrop.ShellPrototype.msix') /nv /o
    if ($LASTEXITCODE -ne 0) {
        throw "MakeAppx failed to create the sparse package (exit code $LASTEXITCODE)."
    }
}
finally {
    $fullStageRoot = [IO.Path]::GetFullPath($stageRoot)
    $expectedStageRoot = [IO.Path]::GetFullPath((Join-Path $outputRoot ([IO.Path]::GetFileName($stageRoot))))
    if ($fullStageRoot.StartsWith($outputRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -and
        $fullStageRoot -eq $expectedStageRoot -and (Test-Path -LiteralPath $expectedStageRoot -PathType Container)) {
        Remove-Item -LiteralPath $expectedStageRoot -Recurse -Force
    }
}

if (-not (Test-Path -LiteralPath $outputScripts -PathType Container)) {
    New-Item -ItemType Directory -Path $outputScripts | Out-Null
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-shell.ps1') -Destination $outputScripts -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall-shell.ps1') -Destination $outputScripts -Force

Write-Output "Built Backdrop.Shell.dll and sparse package in $outputRoot"
