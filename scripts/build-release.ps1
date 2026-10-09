[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$dotnet = Join-Path $artifactsRoot 'tools\dotnet\dotnet.exe'
$compiler = Join-Path $artifactsRoot 'tools\InnoSetupPortable\tools\ISCC.exe'
$project = Join-Path $projectRoot 'Backdrop.csproj'
$installerScript = Join-Path $projectRoot 'installer\Backdrop.iss'
$shellBuildScript = Join-Path $PSScriptRoot 'build-shell.ps1'
$classicShellScript = Join-Path $PSScriptRoot 'classic-shell.ps1'
$stageName = "release-stage-$Version-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"
$stageRoot = [IO.Path]::GetFullPath((Join-Path $artifactsRoot $stageName))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $artifactsRoot 'release-output'))
$outputPrefix = $artifactsRoot.TrimEnd('\') + '\'

foreach ($path in @($dotnet, $compiler, $project, $installerScript, $shellBuildScript, $classicShellScript)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release input is missing: $path" }
}
if (-not $stageRoot.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not $outputRoot.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release output paths must stay inside the project artifacts directory.'
}
if (Test-Path -LiteralPath $stageRoot) {
    throw "Release staging already exists; use a new build time or remove this generated folder after checking it: $stageRoot"
}

$sdkVersion = (& $dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.401') {
    throw "The release build requires portable .NET SDK 10.0.401. Found '$sdkVersion'."
}
$compilerSignature = Get-AuthenticodeSignature -LiteralPath $compiler
if ($compilerSignature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
    $compilerSignature.SignerCertificate.Subject -notmatch 'Pyrsys B\.V\.') {
    throw 'The pinned Inno Setup compiler signature is not valid for Pyrsys B.V.'
}

New-Item -ItemType Directory -Path $stageRoot | Out-Null
if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $outputRoot | Out-Null
}

& $dotnet publish $project -c Release -f net10.0-windows -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version -o $stageRoot
if ($LASTEXITCODE -ne 0) { throw "The .NET publish failed with exit code $LASTEXITCODE." }

if (-not (Test-Path -LiteralPath (Join-Path $stageRoot 'Backdrop.exe') -PathType Leaf)) {
    throw 'The self-contained application executable is missing from the release stage.'
}

& $shellBuildScript -OutputRoot $stageRoot -SkipSparsePackage
if ($LASTEXITCODE -ne 0) { throw "The native shell build failed with exit code $LASTEXITCODE." }

$stageScripts = Join-Path $stageRoot 'scripts'
if (-not (Test-Path -LiteralPath $stageScripts -PathType Container)) {
    New-Item -ItemType Directory -Path $stageScripts | Out-Null
}
Copy-Item -LiteralPath $classicShellScript -Destination $stageScripts -Force

$selfCheckRoot = Join-Path $stageRoot 'self-check'
$selfCheckExe = Join-Path $stageRoot 'Backdrop.exe'
$selfCheckOutput = Join-Path $stageRoot 'self-check.stdout.txt'
$selfCheckError = Join-Path $stageRoot 'self-check.stderr.txt'
$selfCheckProcess = Start-Process -FilePath $selfCheckExe `
    -ArgumentList @('--self-check', ('"{0}"' -f $selfCheckRoot)) `
    -RedirectStandardOutput $selfCheckOutput -RedirectStandardError $selfCheckError `
    -WindowStyle Hidden -Wait -PassThru
if ($selfCheckProcess.ExitCode -ne 0) {
    $details = if (Test-Path -LiteralPath $selfCheckError -PathType Leaf) { Get-Content -LiteralPath $selfCheckError -Raw } else { '' }
    throw "The staged application self-check failed with exit code $($selfCheckProcess.ExitCode). $details"
}

$lastRunFile = Join-Path $selfCheckRoot 'last-run.txt'
if (-not (Test-Path -LiteralPath $lastRunFile -PathType Leaf)) { throw 'The self-check did not record its output folder.' }
$selfCheckRun = (Get-Content -LiteralPath $lastRunFile -Raw).Trim()
$harness = Join-Path $stageRoot 'BackdropShellHarness.exe'
$sampleOne = Join-Path $selfCheckRun 'inputs\portrait one.png'
$sampleTwo = Join-Path $selfCheckRun 'inputs\portrait two.png'
foreach ($path in @($harness, $sampleOne, $sampleTwo)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "The native harness input is missing: $path" }
}
& $harness $sampleOne $sampleTwo
if ($LASTEXITCODE -ne 0) { throw "The native shell harness failed with exit code $LASTEXITCODE." }

$outputName = "Backdrop-$Version-x64-setup"
$installerPath = Join-Path $outputRoot "$outputName.exe"
$checksumPath = "$installerPath.sha256"
if (Test-Path -LiteralPath $installerPath) { Remove-Item -LiteralPath $installerPath -Force }
if (Test-Path -LiteralPath $checksumPath) { Remove-Item -LiteralPath $checksumPath -Force }

& $compiler "/DAppVersion=$Version" "/DPayloadRoot=$stageRoot" "/DOutputDir=$outputRoot" "/DOutputName=$outputName" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed to build the installer (exit code $LASTEXITCODE)." }
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) { throw "The installer was not created: $installerPath" }

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
if ($signature.Status -notin @([System.Management.Automation.SignatureStatus]::NotSigned, [System.Management.Automation.SignatureStatus]::Valid)) {
    throw "The installer has an invalid Authenticode signature status: $($signature.Status)."
}
$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
Set-Content -LiteralPath $checksumPath -Value "$hash *$([IO.Path]::GetFileName($installerPath))" -Encoding ASCII

Write-Output 'Backdrop installer candidate built and checked.'
Write-Output "Version: $Version"
Write-Output "SDK: $sdkVersion"
Write-Output "Inno compiler signer: $($compilerSignature.SignerCertificate.Subject)"
Write-Output "Installer signature: $($signature.Status)"
Write-Output "Installer: $installerPath"
Write-Output "SHA-256: $checksumPath"
Write-Output "Staged payload: $stageRoot"
