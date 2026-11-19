<#
.SYNOPSIS
    Builds the self-contained LongPress.exe and the Inno Setup installer.

.DESCRIPTION
    Stops any running LongPress, runs `dotnet publish -c Release`, then compiles
    installer\LongPress.iss with Inno Setup 7. The installer is written to
    installer\Output\LongPress-Setup-<version>.exe, where <version> is the
    <Version> in LongPress.csproj.

.PARAMETER Iscc
    Full path to ISCC.exe. If omitted, the ISCC environment variable is used, then
    the usual Inno Setup 7 install locations are searched.
#>
[CmdletBinding()]
param(
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Find-Iscc {
    param([string]$Explicit)

    if ($Explicit) {
        if (Test-Path -LiteralPath $Explicit -PathType Leaf) { return (Resolve-Path -LiteralPath $Explicit).Path }
        throw "ISCC.exe not found at the -Iscc path: $Explicit"
    }
    if ($env:ISCC) {
        if (Test-Path -LiteralPath $env:ISCC -PathType Leaf) { return $env:ISCC }
        throw "ISCC.exe not found at the path in the ISCC environment variable: $env:ISCC"
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
        if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe' }
        if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe' }
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c -PathType Leaf) { return $c }
    }

    $found = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Programs') -Recurse -Filter ISCC.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($found) { return $found.FullName }

    throw "Couldn't find ISCC.exe (Inno Setup 7). Install Inno Setup 7, or pass -Iscc <path to ISCC.exe>, or set the ISCC environment variable."
}

$isccPath = Find-Iscc $Iscc
Write-Host "Using $isccPath"

[xml]$csproj = Get-Content -LiteralPath (Join-Path $root 'LongPress.csproj') -Raw
$version = ($csproj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <Version> found in LongPress.csproj." }
$version = $version.Trim()
Write-Host "Version $version"

# A running copy locks publish\LongPress.exe.
Stop-Process -Name LongPress -ErrorAction SilentlyContinue

Push-Location $root
try {
    dotnet publish -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }

    $exe = Join-Path $root 'publish\LongPress.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "dotnet publish didn't produce $exe." }

    # Retry: Dropbox (or antivirus) often locks the freshly published exe for a few seconds.
    for ($attempt = 1; ; $attempt++) {
        & $isccPath "/DAppVersion=$version" (Join-Path $root 'installer\LongPress.iss')
        if ($LASTEXITCODE -eq 0) { break }
        if ($attempt -ge 6) { throw "ISCC failed (exit code $LASTEXITCODE)." }
        Write-Warning "ISCC failed (exit code $LASTEXITCODE); retrying in 3 seconds."
        Start-Sleep -Seconds 3
    }
}
finally {
    Pop-Location
}

$setup = Join-Path $root "installer\Output\LongPress-Setup-$version.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw "ISCC finished but $setup is missing." }
$sizeMb = [math]::Round((Get-Item -LiteralPath $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "Installer: $setup ($sizeMb MB)"
