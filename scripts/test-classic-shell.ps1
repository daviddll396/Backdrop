[CmdletBinding()]
param(
    [string]$HelperPath = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($HelperPath)) {
    $HelperPath = Join-Path $PSScriptRoot 'classic-shell.ps1'
}
$clsid = '{7A01B163-C207-43BA-97A0-A2F356CBEB44}'
$foreignClsid = '{BF2D7A88-0C9A-45A7-9E62-C9F7AA284E88}'
$classPath = "Software\Classes\CLSID\$clsid"
$inprocPath = "$classPath\InprocServer32"
$pngVerbPath = 'Software\Classes\SystemFileAssociations\.png\shell\Backdrop.Create'
$jpgVerbPath = 'Software\Classes\SystemFileAssociations\.jpg\shell\Backdrop.Create'
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::CurrentUser,
    [Microsoft.Win32.RegistryView]::Registry64)
$createdPaths = [Collections.Generic.List[string]]::new()
$fixtureExpected = @{}

function Assert([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function New-ForeignKey([string]$path, [hashtable]$values) {
    $existing = $registry.OpenSubKey($path, $false)
    if ($null -ne $existing) {
        $existing.Dispose()
        throw "Safety test stopped because the target key appeared after preflight: HKCU:\$path"
    }
    $key = $registry.CreateSubKey($path, $true)
    if ($null -eq $key) { throw "Could not create safety-test fixture: HKCU:\$path" }
    $createdPaths.Add($path)
    $fixtureExpected[$path] = $values
    try {
        foreach ($name in $values.Keys) {
            $key.SetValue([string]$name, [string]$values[$name], [Microsoft.Win32.RegistryValueKind]::String)
        }
    }
    finally { $key.Dispose() }
}

function Test-KeyValues([string]$path, [hashtable]$expected) {
    $key = $registry.OpenSubKey($path, $false)
    if ($null -eq $key) { return $false }
    try {
        if ($key.ValueCount -ne $expected.Count) { return $false }
        foreach ($name in $expected.Keys) {
            if ($key.GetValueNames() -notcontains [string]$name -or
                $key.GetValueKind([string]$name) -ne [Microsoft.Win32.RegistryValueKind]::String -or
                $key.GetValue([string]$name) -cne [string]$expected[$name]) { return $false }
        }
        return $true
    }
    finally { $key.Dispose() }
}

try {
    if (-not (Test-Path -LiteralPath $HelperPath -PathType Leaf)) { throw "Helper script was not found: $HelperPath" }
    foreach ($path in @($classPath, $inprocPath, $pngVerbPath, $jpgVerbPath)) {
        $existing = $registry.OpenSubKey($path, $false)
        if ($null -ne $existing) {
            $existing.Dispose()
            throw "Safety test stopped because the target key already exists: HKCU:\$path"
        }
    }
}
catch {
    $registry.Dispose()
    throw
}

try {
    $foreignClass = @{ '' = 'C:\Example\ForeignShell.dll'; ThreadingModel = 'Apartment'; ForeignMarker = 'preserve-class' }
    $foreignPngVerb = @{
        ExplorerCommandHandler = $clsid
        MultiSelectModel = 'Player'
        MUIVerb = 'Foreign command'
        Icon = 'C:\Example\Foreign.exe,0'
        ForeignMarker = 'preserve-png'
    }
    $foreignJpgVerb = @{
        ExplorerCommandHandler = $foreignClsid
        MultiSelectModel = 'Document'
        MUIVerb = 'Other application'
        Icon = 'C:\Example\Other.exe,0'
        ForeignMarker = 'preserve-jpg'
    }
    New-ForeignKey $inprocPath $foreignClass
    New-ForeignKey $pngVerbPath $foreignPngVerb
    New-ForeignKey $jpgVerbPath $foreignJpgVerb

    $output = @(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $HelperPath -Action Unregister 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Unregister helper failed during the safety check: $($output -join [Environment]::NewLine)" }
    Assert (($output -join '').Trim() -eq 'not-installed') 'Status text from Unregister was unexpected.'
    Assert (Test-KeyValues $inprocPath $foreignClass) 'Unregister changed a foreign COM server key.'
    Assert (Test-KeyValues $pngVerbPath $foreignPngVerb) 'Unregister changed a verb whose icon points to another executable.'
    Assert (Test-KeyValues $jpgVerbPath $foreignJpgVerb) 'Unregister changed a verb owned by another CLSID.'

    Write-Output 'Classic Explorer registration ownership checks passed.'
}
finally {
    for ($index = $createdPaths.Count - 1; $index -ge 0; $index--) {
        $path = $createdPaths[$index]
        $expected = $fixtureExpected[$path]
        $key = $registry.OpenSubKey($path, $false)
        if ($null -ne $key) {
            $safeToDelete = $true
            try {
                if ($key.SubKeyCount -gt 0) { $safeToDelete = $false }
                foreach ($name in $key.GetValueNames()) {
                    if (-not $expected.ContainsKey([string]$name) -or
                        $key.GetValueKind($name) -ne [Microsoft.Win32.RegistryValueKind]::String -or
                        $key.GetValue($name) -cne [string]$expected[$name]) {
                        $safeToDelete = $false
                        break
                    }
                }
            }
            finally { $key.Dispose() }
            if ($safeToDelete) {
                try { $registry.DeleteSubKey($path, $false) }
                catch { Write-Warning "Could not remove safety-test fixture HKCU:\$path; it was left in place." }
            }
        }
    }

    # CreateSubKey creates the CLSID parent as part of this fixture. Delete it
    # only when it is empty; never recurse into or remove unrelated registrations.
    if ($createdPaths.Contains($inprocPath)) {
        $classParent = $registry.OpenSubKey($classPath, $false)
        if ($null -ne $classParent) {
            try { $emptyClassParent = $classParent.ValueCount -eq 0 -and $classParent.SubKeyCount -eq 0 }
            finally { $classParent.Dispose() }
            if ($emptyClassParent) {
                try { $registry.DeleteSubKey($classPath, $false) }
                catch { Write-Warning "Could not remove empty safety-test parent HKCU:\$classPath; it was left in place." }
            }
        }
    }
    $registry.Dispose()
}
