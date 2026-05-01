<#
.SYNOPSIS
    Publishes UselessTerminal and builds a Setup .exe into the repo's installer\ folder.

.DESCRIPTION
    Runs `dotnet publish` for win-x64 (or win-arm64), then:
      - Writes installer\UselessTerminal-Setup-<version>-<runtime>.exe (Inno Setup 6 required)
      - Optionally creates installer\UselessTerminal-<version>-<runtime>.zip

    Staged publish output is under artifacts\publish\<runtime>\ (not shipped as-is).

    WebView2: Windows 10/11 normally have the Evergreen WebView2 Runtime; if the app fails to
    load the terminal page, install:
    https://developer.microsoft.com/microsoft-edge/webview2/

.PARAMETER Configuration
    Release (default) or Debug.

.PARAMETER Runtime
    win-x64 (default) or win-arm64.

.PARAMETER FrameworkDependent
    If set, publish framework-dependent (requires .NET 9 desktop runtime on the machine).
    Default is self-contained (larger folder, no separate runtime install).

.PARAMETER Version
    Version string for filenames (e.g. 1.2.3). If omitted, reads <Version> from the csproj,
    then falls back to 1.0.0.

.PARAMETER SkipZip
    Skip creating the ZIP (Setup .exe is still built unless -SkipInno).

.PARAMETER SkipInno
    Skip building the Setup .exe (publish + optional ZIP only). Use when Inno Setup is not installed.

.PARAMETER InnoCompiler
    Full path to ISCC.exe (Inno Setup Compiler). Default: auto-detect under Program Files.

.EXAMPLE
    .\scripts\Build-Installer.ps1

.EXAMPLE
    .\scripts\Build-Installer.ps1 -FrameworkDependent -Version 2.0.0

.EXAMPLE
    .\scripts\Build-Installer.ps1 -Runtime win-arm64 -SkipInno
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',

    [switch] $FrameworkDependent,

    [string] $Version = '',

    [switch] $SkipZip,

    [switch] $SkipInno,

    [string] $InnoCompiler = ''
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

function Find-InnoCompiler {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
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

$ArtifactsRoot = Join-Path $RepoRoot 'artifacts'
$PublishDir = Join-Path $ArtifactsRoot "publish\$Runtime"
$InstallerDir = Join-Path $RepoRoot 'installer'

Write-Host "Repository:  $RepoRoot"
Write-Host "Project:     $ProjectPath"
Write-Host "Configuration: $Configuration | Runtime: $Runtime"
Write-Host "Version:     $Version"
Write-Host "Publish to:  $PublishDir"
Write-Host "Installer dir: $InstallerDir"
Write-Host "Self-contained: $(-not $FrameworkDependent)"

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

Write-Host "`ndotnet $($publishArgs -join ' ')`n" -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exePath = Join-Path $PublishDir 'UselessTerminal.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Expected output missing: $exePath"
}

New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null

if (-not $SkipZip) {
    $zipPath = Join-Path $InstallerDir "UselessTerminal-$Version-$Runtime.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Write-Host "Creating ZIP: $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $PublishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "ZIP OK: $zipPath" -ForegroundColor Green
}

if (-not $SkipInno) {
    $iscc = $InnoCompiler
    if (-not $iscc) { $iscc = Find-InnoCompiler }
    if (-not $iscc) {
        throw @"
Inno Setup 6 not found (ISCC.exe). Install from https://jrsoftware.org/isdl.php
Or run with -SkipInno to publish without building installer\Setup.exe.
"@
    }

    $issPath = Join-Path $InstallerDir "UselessTerminal-$Version-$Runtime.iss"
    $setupBaseName = "UselessTerminal-Setup-$Version-$Runtime"
    $setupExePath = Join-Path $InstallerDir "$setupBaseName.exe"
    # Inno treats {...} as constants; double braces for a literal AppId GUID.
    $appGuidIss = '{{E8D4F9B2-6C1A-4F70-9E3D-2B7A8C5D1E0F}}'

    # Inno 6.7+: prefer x64compatible over deprecated plain x64 (see Compiler messages).
    $archAllowed = switch ($Runtime) {
        'win-x64' { 'x64compatible' }
        'win-arm64' { 'arm64' }
        default { 'x64compatible' }
    }
    $archInstall64 = $archAllowed

    # Forward slashes avoid escaping in Inno; trailing backslash on PublishDir breaks #define quoting.
    $publishIss = ($PublishDir.TrimEnd('\') -replace '\\', '/')
    $installerIss = ($InstallerDir.TrimEnd('\') -replace '\\', '/')

    $issContent = @"
; Generated by scripts/Build-Installer.ps1 -- change AppId only if you fork product identity.
#define MyAppName "UselessTerminal"
#define MyAppVersion "$Version"
#define MyAppPublisher "UselessTerminal"
#define MyAppExeName "UselessTerminal.exe"
#define PublishDir "$publishIss"

[Setup]
AppId=$appGuidIss
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=no
PrivilegesRequired=admin
OutputDir=$installerIss
OutputBaseFilename=$setupBaseName
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=$archAllowed
ArchitecturesInstallIn64BitMode=$archInstall64
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
"@

    Set-Content -LiteralPath $issPath -Value $issContent -Encoding UTF8
    Write-Host "Compiling installer with: $iscc" -ForegroundColor Cyan
    & $iscc $issPath
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $setupExePath)) {
        throw "Expected installer missing after compile: $setupExePath"
    }
    Write-Host "Setup OK: $setupExePath" -ForegroundColor Green
}

Write-Host "`nDone. Installer folder: $InstallerDir" -ForegroundColor Green
Write-Host "Publish staging: $PublishDir" -ForegroundColor DarkGray
