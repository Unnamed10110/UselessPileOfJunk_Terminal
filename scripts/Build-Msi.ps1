<#
.SYNOPSIS
    Publishes UselessTerminal and builds an MSI into the repo's installer\ folder.

.DESCRIPTION
    Runs dotnet publish for win-x64 (or win-arm64), then builds
    installer\UselessTerminal-Setup-<version>-<runtime>.msi with the WiX CLI.

    Requires:
      - .NET SDK (dotnet)
      - WiX Toolset CLI (dotnet tool install --global wix) - installed automatically if missing

    Staged publish output is under artifacts\publish\<runtime>\.

.PARAMETER Configuration
    Release (default) or Debug.

.PARAMETER Runtime
    win-x64 (default) or win-arm64.

.PARAMETER FrameworkDependent
    If set, publish framework-dependent (requires .NET 9 desktop runtime on the machine).
    Default is self-contained.

.PARAMETER Version
    Version string for the MSI (e.g. 1.2.3). Must be up to four numeric parts for WiX.
    If omitted, reads Version from the csproj, then falls back to 1.0.0.

.PARAMETER SkipPublish
    Reuse existing artifacts\publish\<runtime>\ without republishing.

.EXAMPLE
    .\scripts\Build-Msi.ps1

.EXAMPLE
    .\scripts\Build-Msi.ps1 -Version 1.2.0 -Runtime win-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',

    [switch] $FrameworkDependent,

    [string] $Version = '',

    [switch] $SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CsprojVersion {
    param([string] $ProjectPath)
    try {
        [xml] $doc = Get-Content -LiteralPath $ProjectPath -Raw
        foreach ($pg in $doc.Project.PropertyGroup) {
            if ($pg.Version) { return $pg.Version.Trim() }
        }
    }
    catch { }
    return $null
}

function Normalize-MsiVersion {
    param([string] $Raw)
    $parts = @($Raw -split '[^\d]+' | Where-Object { $_ -ne '' })
    if ($parts.Count -eq 0) { return '1.0.0' }
    while ($parts.Count -lt 3) { $parts += '0' }
    if ($parts.Count -gt 4) { $parts = $parts[0..3] }
    return ($parts -join '.')
}

function Ensure-WixCli {
    $requiredVersion = '5.0.2'
    $toolsPath = Join-Path $env:USERPROFILE '.dotnet\tools'
    if ($env:PATH -notlike "*$toolsPath*") {
        $env:PATH = "$toolsPath;$env:PATH"
    }

    $wix = Get-Command wix -ErrorAction SilentlyContinue
    $haveOk = $false
    if ($wix) {
        $verText = (& wix --version 2>$null | Out-String).Trim()
        if ($verText -like "$requiredVersion*") { $haveOk = $true }
    }

    if (-not $haveOk) {
        Write-Host "Installing WiX CLI $requiredVersion (global dotnet tool)..." -ForegroundColor Cyan
        if ($wix) {
            & dotnet tool uninstall --global wix | Out-Host
        }
        & dotnet tool install --global wix --version $requiredVersion | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install WiX CLI $requiredVersion"
        }
        if ($env:PATH -notlike "*$toolsPath*") {
            $env:PATH = "$toolsPath;$env:PATH"
        }
        $wix = Get-Command wix -ErrorAction SilentlyContinue
        if (-not $wix) {
            throw "WiX CLI ('wix') is not available after install. Reopen the terminal and retry."
        }
    }

    return [string]$wix.Source
}

$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$ProjectPath = Join-Path $RepoRoot 'src\UselessTerminal\UselessTerminal.csproj'
if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project not found: $ProjectPath"
}

if (-not $Version) {
    $Version = Get-CsprojVersion -ProjectPath $ProjectPath
    if (-not $Version) { $Version = '1.0.0' }
}
$MsiVersion = Normalize-MsiVersion -Raw $Version

$ArtifactsRoot = Join-Path $RepoRoot 'artifacts'
$PublishDir = Join-Path $ArtifactsRoot "publish\$Runtime"
$InstallerDir = Join-Path $RepoRoot 'installer'
$WixWorkDir = Join-Path $ArtifactsRoot "wix\$Runtime"
$UpgradeCode = 'E8D4F9B2-6C1A-4F70-9E3D-2B7A8C5D1E0F'

Write-Host "Repository:     $RepoRoot"
Write-Host "Project:        $ProjectPath"
Write-Host "Configuration:  $Configuration | Runtime: $Runtime"
Write-Host "Version:        $Version (MSI: $MsiVersion)"
Write-Host "Publish to:     $PublishDir"
Write-Host "Installer dir:  $InstallerDir"
Write-Host "Self-contained: $(-not $FrameworkDependent)"

if (-not $SkipPublish) {
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
    if (Test-Path -LiteralPath (Join-Path $PublishDir '*')) {
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

    $publishArgs = @(
        'publish'
        $ProjectPath
        '-c', $Configuration
        '-r', $Runtime
        '-o', $PublishDir
        '--nologo'
        '-p:PublishTrimmed=false'
    )
    if ($FrameworkDependent) {
        $publishArgs += @('--self-contained', 'false')
    }
    else {
        $publishArgs += @('--self-contained', 'true')
    }

    Write-Host ""
    Write-Host ("dotnet " + ($publishArgs -join ' ')) -ForegroundColor Cyan
    Write-Host ""
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
}

$exePath = Join-Path $PublishDir 'UselessTerminal.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Expected output missing: $exePath (run without -SkipPublish first)"
}

New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null
if (Test-Path -LiteralPath $WixWorkDir) {
    Remove-Item -LiteralPath $WixWorkDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $WixWorkDir | Out-Null

$wixExe = Ensure-WixCli | Select-Object -Last 1
Write-Host "WiX CLI: $wixExe" -ForegroundColor DarkGray

$useUiExt = $false
try {
    $extOut = & wix extension list 2>&1 | Out-String
    if ($extOut -notmatch 'WixToolset\.UI\.wixext') {
        Write-Host "Adding WiX UI extension 5.0.2..." -ForegroundColor Cyan
        & wix extension add WixToolset.UI.wixext/5.0.2 | Out-Host
    }
    $extOut = & wix extension list 2>&1 | Out-String
    if ($extOut -match 'WixToolset\.UI\.wixext') { $useUiExt = $true }
}
catch {
    Write-Host "WiX UI extension unavailable; building MSI with minimal UI." -ForegroundColor Yellow
    $useUiExt = $false
}

$msiBaseName = "UselessTerminal-Setup-$Version-$Runtime"
$msiOutPath = Join-Path $InstallerDir "$msiBaseName.msi"
$wxsPath = Join-Path $WixWorkDir 'Product.wxs'

$platform = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64' }
$publishBind = ($PublishDir.TrimEnd('\') -replace '\\', '/')

$uiNamespace = ''
$uiXml = ''
if ($useUiExt) {
    $uiNamespace = ' xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui"'
    $uiXml = @'

    <ui:WixUI Id="WixUI_InstallDir" InstallDirectory="INSTALLFOLDER" />
'@
}

$wxs = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"$uiNamespace>
  <Package
      Name="Useless Terminal"
      Manufacturer="UselessTerminal"
      Version="$MsiVersion"
      UpgradeCode="$UpgradeCode"
      Scope="perMachine">
    <MajorUpgrade
        DowngradeErrorMessage="A newer version of [ProductName] is already installed. Remove it first if you need to install this version." />
    <MediaTemplate EmbedCab="yes" CompressionLevel="high" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="UselessTerminal" />
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="AppProgramsFolder" Name="UselessTerminal" />
    </StandardDirectory>

    <StandardDirectory Id="DesktopFolder" />

    <Feature Id="Main" Title="Useless Terminal" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentRef Id="StartMenuShortcut" />
      <ComponentRef Id="DesktopShortcut" />
    </Feature>

    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">
      <Files Include="!(bindpath.publish)\**">
        <Exclude Files="!(bindpath.publish)\**\*.pdb" />
      </Files>
    </ComponentGroup>

    <Component Id="StartMenuShortcut" Directory="AppProgramsFolder" Guid="*">
      <Shortcut
          Id="StartMenuShortcutLink"
          Name="Useless Terminal"
          Description="Useless Terminal"
          Target="[INSTALLFOLDER]UselessTerminal.exe"
          WorkingDirectory="INSTALLFOLDER" />
      <RemoveFolder Id="RemoveAppProgramsFolder" On="uninstall" />
      <RegistryValue
          Root="HKCU"
          Key="Software\UselessTerminal"
          Name="StartMenuShortcut"
          Type="integer"
          Value="1"
          KeyPath="yes" />
    </Component>

    <Component Id="DesktopShortcut" Directory="DesktopFolder" Guid="*">
      <Shortcut
          Id="DesktopShortcutLink"
          Name="Useless Terminal"
          Description="Useless Terminal"
          Target="[INSTALLFOLDER]UselessTerminal.exe"
          WorkingDirectory="INSTALLFOLDER" />
      <RegistryValue
          Root="HKCU"
          Key="Software\UselessTerminal"
          Name="DesktopShortcut"
          Type="integer"
          Value="1"
          KeyPath="yes" />
    </Component>
$uiXml
  </Package>
</Wix>
"@

Set-Content -LiteralPath $wxsPath -Value $wxs -Encoding UTF8

if (Test-Path -LiteralPath $msiOutPath) {
    Remove-Item -LiteralPath $msiOutPath -Force
}

$wixArgs = @(
    'build'
    $wxsPath
    '-arch', $platform
    '-bindpath', "publish=$publishBind"
    '-out', $msiOutPath
    '-nologo'
)
if ($useUiExt) {
    $wixArgs += @('-ext', 'WixToolset.UI.wixext')
}

Write-Host ""
Write-Host ("wix " + ($wixArgs -join ' ')) -ForegroundColor Cyan
Write-Host ""
& wix @wixArgs
if ($LASTEXITCODE -ne 0) {
    throw "wix build failed with exit code $LASTEXITCODE"
}
if (-not (Test-Path -LiteralPath $msiOutPath)) {
    throw "Expected MSI missing after build: $msiOutPath"
}

$sizeMb = [math]::Round((Get-Item -LiteralPath $msiOutPath).Length / 1MB, 1)
Write-Host ""
Write-Host "MSI OK: $msiOutPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "Publish staging: $PublishDir" -ForegroundColor DarkGray
