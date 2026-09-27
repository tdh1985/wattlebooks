# Copyright (c) 2026 Tim Downey. Licensed under the MIT License.
#
# Packs the Windows app as dist/InvoiceDesk-<version>.msix for the Microsoft Store.
# It's left unsigned because the Store signs it with Microsoft's own certificate.
#
#   pwsh build/make-msix.ps1

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content (Join-Path $repo 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

# the newest windows sdk that has both packaging tools
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64" -Directory |
    Where-Object { (Test-Path (Join-Path $_ 'makeappx.exe')) -and (Test-Path (Join-Path $_ 'makepri.exe')) } |
    Sort-Object { [version]($_.Parent.Name) } | Select-Object -Last 1
if (-not $sdk) { throw 'makeappx.exe and makepri.exe not found, install the Windows SDK' }

dotnet publish (Join-Path $repo 'src/InvoiceDesk.App') -p:PublishProfile=SingleExe
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

$staging = Join-Path $repo 'dist/msix'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item (Join-Path $repo 'dist/InvoiceDesk.exe') $staging
Copy-Item (Join-Path $PSScriptRoot 'msix/Assets') $staging -Recurse
(Get-Content (Join-Path $PSScriptRoot 'msix/AppxManifest.xml') -Raw).Replace('$(Version)', $version) |
    Set-Content (Join-Path $staging 'AppxManifest.xml') -NoNewline -Encoding utf8NoBOM

# resources.pri lets windows pick the right scale of each tile image
$priConfig = Join-Path $env:TEMP 'invoicedesk-priconfig.xml'
& (Join-Path $sdk 'makepri.exe') createconfig /cf $priConfig /dq en-AU /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig failed' }
# one package holds every scale, so the split meant for bundles is dropped
$config = Get-Content $priConfig -Raw
[regex]::Replace($config, '(?s)<packaging>.*?</packaging>', '') | Set-Content $priConfig -NoNewline
& (Join-Path $sdk 'makepri.exe') new /pr $staging /cf $priConfig /mn (Join-Path $staging 'AppxManifest.xml') /of (Join-Path $staging 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed' }

$package = Join-Path $repo "dist/InvoiceDesk-$version.msix"
& (Join-Path $sdk 'makeappx.exe') pack /d $staging /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed' }
Remove-Item $staging -Recurse -Force
Write-Host "made $package"
