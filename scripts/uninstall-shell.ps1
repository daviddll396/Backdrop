$ErrorActionPreference = 'Stop'
$packages = @(Get-AppxPackage -Name 'Backdrop.ShellPrototype' -ErrorAction SilentlyContinue)
foreach ($package in $packages) {
    Remove-AppxPackage -Package $package.PackageFullName
}
Write-Output 'Removed the Backdrop shell package for the current user.'
