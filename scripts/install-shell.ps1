$ErrorActionPreference = 'Stop'
$scriptParent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installRoot = if (Test-Path -LiteralPath (Join-Path $scriptParent 'AppxManifest.xml') -PathType Leaf) {
    $scriptParent
} else {
    [IO.Path]::GetFullPath((Join-Path $scriptParent 'artifacts\v2'))
}
$manifest = Join-Path $installRoot 'AppxManifest.xml'

foreach ($file in @('Backdrop.exe', 'Backdrop.Shell.dll', 'AppxManifest.xml')) {
    if (-not (Test-Path -LiteralPath (Join-Path $installRoot $file) -PathType Leaf)) {
        throw "Shell install is not built. Missing: $(Join-Path $installRoot $file). Run scripts/build-shell.ps1 first."
    }
}

$registered = @(Get-AppxPackage -Name 'Backdrop.ShellPrototype' -ErrorAction SilentlyContinue)
if ($registered.Count -gt 0) {
    Write-Output 'Backdrop direct-menu package is already registered for this user.'
    return
}

$unlockKeys = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock'
)
$developerRegistrationEnabled = $false
foreach ($unlockKey in $unlockKeys) {
    if (Test-Path -LiteralPath $unlockKey) {
        $unlock = Get-ItemProperty -LiteralPath $unlockKey
        if ($unlock.AllowDevelopmentWithoutDevLicense -eq 1) {
            $developerRegistrationEnabled = $true
            break
        }
    }
}
if (-not $developerRegistrationEnabled) {
    throw 'Per-user loose package registration is disabled. This script does not change AppModelUnlock or developer settings.'
}

Add-AppxPackage -Path $manifest -Register -ExternalLocation $installRoot
$package = Get-AppxPackage -Name 'Backdrop.ShellPrototype' -ErrorAction SilentlyContinue
if (-not $package) {
    throw 'Windows did not register the Backdrop shell package for this user.'
}
Write-Output "Registered $($package.PackageFullName) for the current user."
