[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Status', 'Register', 'Unregister')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'
$clsid = '{7A01B163-C207-43BA-97A0-A2F356CBEB44}'
$verbName = 'Backdrop.Create'
$supportedExtensions = @('.png', '.jpg', '.jpeg', '.bmp', '.gif', '.tif', '.tiff')
$installRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = [IO.Path]::GetFullPath((Join-Path $installRoot 'Backdrop.exe'))
$server = [IO.Path]::GetFullPath((Join-Path $installRoot 'Backdrop.Shell.dll'))
$classesRoot = 'Software\Classes'
$classRootPath = "$classesRoot\CLSID\$clsid"
$inprocPath = "$classRootPath\InprocServer32"
$classes = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::CurrentUser,
    [Microsoft.Win32.RegistryView]::Registry64)

function Test-ValueEqual([string]$name, $actual, [string]$expected) {
    if ($name -eq '') {
        return [string]::Equals([string]$actual, $expected, [StringComparison]::OrdinalIgnoreCase)
    }
    return [string]::Equals([string]$actual, $expected, [StringComparison]::Ordinal)
}

function Get-ExpectedClassValues {
    return [ordered]@{
        '' = $server
        ThreadingModel = 'Apartment'
    }
}

function Get-ExpectedVerbValues([string]$extension) {
    return [ordered]@{
        ExplorerCommandHandler = $clsid
        MultiSelectModel = 'Player'
        MUIVerb = 'Create composition with Backdrop'
        Icon = "$executable,0"
    }
}

function Test-ValuesMatch([string]$path, [System.Collections.IDictionary]$expected, [bool]$requireAll) {
    $key = $classes.OpenSubKey($path, $false)
    if ($null -eq $key) { return $false }
    try {
        foreach ($name in $key.GetValueNames()) {
            if (-not $expected.Contains($name)) { return $false }
            if ($key.GetValueKind($name) -ne [Microsoft.Win32.RegistryValueKind]::String) { return $false }
            if (-not (Test-ValueEqual $name $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) ([string]$expected[$name]))) { return $false }
        }
        if ($requireAll) {
            foreach ($name in $expected.Keys) {
                if ($key.GetValueNames() -notcontains [string]$name) { return $false }
            }
        }
        return $key.GetSubKeyNames().Length -eq 0
    }
    finally { $key.Dispose() }
}

function Assert-KeyCanBeOwned([string]$path, [System.Collections.IDictionary]$expected) {
    $key = $classes.OpenSubKey($path, $false)
    if ($null -eq $key) { return }
    try {
        foreach ($name in $key.GetValueNames()) {
            if (-not $expected.Contains($name)) {
                throw "Backdrop will not replace a value owned by another application at HKCU:\$path ($name)."
            }
            if ($key.GetValueKind($name) -ne [Microsoft.Win32.RegistryValueKind]::String -or
                -not (Test-ValueEqual $name $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) ([string]$expected[$name]))) {
                throw "Backdrop will not replace a different Explorer registration at HKCU:\$path ($name)."
            }
        }
        if ($key.SubKeyCount -gt 0) {
            throw "Backdrop will not replace a key with child entries at HKCU:\$path."
        }
    }
    finally { $key.Dispose() }
}

function Assert-ClassParentCanBeOwned {
    $key = $classes.OpenSubKey($classRootPath, $false)
    if ($null -eq $key) { return }
    try {
        if ($key.ValueCount -gt 0) {
            throw "Backdrop will not replace values on the existing class key HKCU:\$classRootPath."
        }
        foreach ($child in $key.GetSubKeyNames()) {
            if ($child -ne 'InprocServer32') {
                throw "Backdrop will not replace another class registration at HKCU:\$classRootPath."
            }
        }
    }
    finally { $key.Dispose() }
}

function Get-VerbPath([string]$extension) {
    return "$classesRoot\SystemFileAssociations\$extension\shell\$verbName"
}

function New-RegistrationPlan([string]$path, [System.Collections.IDictionary]$expected) {
    $key = $classes.OpenSubKey($path, $false)
    $hadKey = $null -ne $key
    $existingNames = @()
    try {
        if ($null -ne $key) { $existingNames = @($key.GetValueNames()) }
    }
    finally { if ($null -ne $key) { $key.Dispose() } }

    $addedNames = @($expected.Keys | Where-Object { $existingNames -notcontains [string]$_ })
    return [pscustomobject]@{ Path = $path; Expected = $expected; HadKey = $hadKey; AddedNames = $addedNames }
}

function Remove-PlannedValues([object[]]$plans) {
    foreach ($plan in ($plans | Select-Object -Reverse)) {
        $key = $classes.OpenSubKey($plan.Path, $true)
        if ($null -eq $key) { continue }
        try {
            foreach ($name in $plan.AddedNames) {
                if ($key.GetValueNames() -contains [string]$name -and
                    $key.GetValueKind([string]$name) -eq [Microsoft.Win32.RegistryValueKind]::String -and
                    (Test-ValueEqual ([string]$name) $key.GetValue([string]$name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) ([string]$plan.Expected[$name]))) {
                    $key.DeleteValue([string]$name, $false)
                }
            }
        }
        finally { $key.Dispose() }

        if (-not $plan.HadKey) {
            $emptyKey = $classes.OpenSubKey($plan.Path, $false)
            if ($null -ne $emptyKey) {
                try { $canDelete = $emptyKey.ValueCount -eq 0 -and $emptyKey.SubKeyCount -eq 0 }
                finally { $emptyKey.Dispose() }
                if ($canDelete) { $classes.DeleteSubKey($plan.Path, $false) }
            }
        }
    }
}

function Test-Registration {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $server -PathType Leaf)) { return $false }
    if (-not (Test-ValuesMatch $inprocPath (Get-ExpectedClassValues) $true)) { return $false }
    foreach ($extension in $supportedExtensions) {
        if (-not (Test-ValuesMatch (Get-VerbPath $extension) (Get-ExpectedVerbValues $extension) $true)) { return $false }
    }
    return $true
}

function Remove-OwnedValues([string]$path, [System.Collections.IDictionary]$expected, [string[]]$anchors) {
    $key = $classes.OpenSubKey($path, $true)
    if ($null -eq $key) { return }
    $removed = $false
    try {
        foreach ($anchor in $anchors) {
            if (-not $expected.Contains($anchor) -or
                $key.GetValueNames() -notcontains $anchor -or
                $key.GetValueKind($anchor) -ne [Microsoft.Win32.RegistryValueKind]::String -or
                -not (Test-ValueEqual $anchor $key.GetValue($anchor, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) ([string]$expected[$anchor]))) {
                return
            }
        }
        foreach ($name in $expected.Keys) {
            if ($key.GetValueNames() -contains [string]$name -and
                $key.GetValueKind([string]$name) -eq [Microsoft.Win32.RegistryValueKind]::String -and
                (Test-ValueEqual ([string]$name) $key.GetValue([string]$name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) ([string]$expected[$name]))) {
                $key.DeleteValue([string]$name, $false)
                $removed = $true
            }
        }
    }
    finally { $key.Dispose() }

    if ($removed) {
        $emptyKey = $classes.OpenSubKey($path, $false)
        if ($null -ne $emptyKey) {
            try { $canDelete = $emptyKey.ValueCount -eq 0 -and $emptyKey.SubKeyCount -eq 0 }
            finally { $emptyKey.Dispose() }
            if ($canDelete) { $classes.DeleteSubKey($path, $false) }
        }
    }
}

function Notify-ShellChanged {
    $typeName = 'BackdropShellChange' + [Guid]::NewGuid().ToString('N')
    $source = @"
using System;
using System.Runtime.InteropServices;
public static class $typeName {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
"@
    Add-Type -TypeDefinition $source -ErrorAction Stop
    $type = ([type]$typeName)
    $type::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
}

try {
    switch ($Action) {
        'Status' {
            if (Test-Registration) { Write-Output 'installed' } else { Write-Output 'not-installed' }
        }
        'Register' {
            if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Backdrop.exe was not found beside this script: $executable" }
            if (-not (Test-Path -LiteralPath $server -PathType Leaf)) { throw "Backdrop.Shell.dll was not found beside this script: $server" }

            Assert-ClassParentCanBeOwned
            $registrationPlan = @()
            $classValues = Get-ExpectedClassValues
            Assert-KeyCanBeOwned $inprocPath $classValues
            $registrationPlan += New-RegistrationPlan $inprocPath $classValues
            foreach ($extension in $supportedExtensions) {
                $verbPath = Get-VerbPath $extension
                $verbValues = Get-ExpectedVerbValues $extension
                Assert-KeyCanBeOwned $verbPath $verbValues
                $registrationPlan += New-RegistrationPlan $verbPath $verbValues
            }
            $classParent = $classes.OpenSubKey($classRootPath, $false)
            $classParentExisted = $null -ne $classParent
            if ($null -ne $classParent) { $classParent.Dispose() }

            # Remove only the developer prototype that used this CLSID. Do not change
            # AppModelUnlock or any other package registration.
            $prototype = @(Get-AppxPackage -Name 'Backdrop.ShellPrototype' -ErrorAction SilentlyContinue)
            foreach ($package in $prototype) {
                if ($package.Publisher -ne 'CN=BackdropShellPrototype' -or
                    $package.PackageFamilyName -ne 'Backdrop.ShellPrototype_ten8cy104z80g') {
                    throw "A package named Backdrop.ShellPrototype has an unexpected publisher or family identity. It was not removed."
                }
                Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
            }

            try {
                foreach ($plan in $registrationPlan) {
                    $key = $classes.CreateSubKey($plan.Path, $true)
                    try {
                        foreach ($name in $plan.Expected.Keys) {
                            $key.SetValue([string]$name, [string]$plan.Expected[$name], [Microsoft.Win32.RegistryValueKind]::String)
                        }
                    }
                    finally { $key.Dispose() }
                }
                Notify-ShellChanged
            }
            catch {
                Remove-PlannedValues $registrationPlan
                if (-not $classParentExisted) {
                    $parent = $classes.OpenSubKey($classRootPath, $false)
                    if ($null -ne $parent) {
                        try { $emptyParent = $parent.ValueCount -eq 0 -and $parent.SubKeyCount -eq 0 }
                        finally { $parent.Dispose() }
                        if ($emptyParent) { $classes.DeleteSubKey($classRootPath, $false) }
                    }
                }
                throw
            }
            Write-Output 'installed'
        }
        'Unregister' {
            foreach ($extension in $supportedExtensions) {
                Remove-OwnedValues (Get-VerbPath $extension) (Get-ExpectedVerbValues $extension) @('ExplorerCommandHandler', 'Icon')
            }
            Remove-OwnedValues $inprocPath (Get-ExpectedClassValues) @('')

            $parent = $classes.OpenSubKey($classRootPath, $false)
            if ($null -ne $parent) {
                try { $emptyParent = $parent.ValueCount -eq 0 -and $parent.SubKeyCount -eq 0 }
                finally { $parent.Dispose() }
                if ($emptyParent) { $classes.DeleteSubKey($classRootPath, $false) }
            }
            Notify-ShellChanged
            Write-Output 'not-installed'
        }
    }
}
finally {
    if ($null -ne $classes) { $classes.Dispose() }
}
