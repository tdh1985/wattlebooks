# Copyright (c) 2026 Tim Downey. Licensed under the MIT License.
#
# Publishes Wattlebooks for Apple Silicon and Intel Macs and wraps each build in
# Wattlebooks.app, packed as dist/Wattlebooks-macos-<arch>.tar.gz.
# A tar keeps the executable bit, which a zip made on Windows loses.
#
#   pwsh build/make-mac-app.ps1

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src/InvoiceDesk.Desktop'
[xml]$props = Get-Content (Join-Path $repo 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

foreach ($build in @(@{ Profile = 'MacArm'; Rid = 'osx-arm64'; Arch = 'arm64' }, @{ Profile = 'MacIntel'; Rid = 'osx-x64'; Arch = 'x64' })) {
    dotnet publish $project -p:PublishProfile=$($build.Profile)
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $($build.Rid)" }

    $staging = Join-Path $repo "dist/mac-$($build.Arch)"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    $app = Join-Path $staging 'Wattlebooks.app'
    New-Item -ItemType Directory -Force (Join-Path $app 'Contents/MacOS'), (Join-Path $app 'Contents/Resources') | Out-Null
    Copy-Item (Join-Path $repo "dist/$($build.Rid)/Wattlebooks") (Join-Path $app 'Contents/MacOS/Wattlebooks')
    Copy-Item (Join-Path $project 'Mac/InvoiceDesk.icns') (Join-Path $app 'Contents/Resources/InvoiceDesk.icns')
    (Get-Content (Join-Path $project 'Mac/Info.plist') -Raw).Replace('$(Version)', $version) |
        Set-Content (Join-Path $app 'Contents/Info.plist') -NoNewline -Encoding utf8NoBOM

    $archive = Join-Path $repo "dist/Wattlebooks-macos-$($build.Arch).tar.gz"
    $file = [IO.File]::Create($archive)
    $gzip = [IO.Compression.GZipStream]::new($file, [IO.Compression.CompressionLevel]::Optimal)
    $tar = [Formats.Tar.TarWriter]::new($gzip, [Formats.Tar.TarEntryFormat]::Pax, $false)
    try {
        $dirMode = [IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute'
        $fileMode = [IO.UnixFileMode]'UserRead, UserWrite, GroupRead, OtherRead'
        foreach ($dir in @('Wattlebooks.app/', 'Wattlebooks.app/Contents/', 'Wattlebooks.app/Contents/MacOS/', 'Wattlebooks.app/Contents/Resources/')) {
            $entry = [Formats.Tar.PaxTarEntry]::new([Formats.Tar.TarEntryType]::Directory, $dir)
            $entry.Mode = $dirMode
            $tar.WriteEntry($entry)
        }
        foreach ($item in @(
                @{ Path = 'Wattlebooks.app/Contents/Info.plist'; Mode = $fileMode },
                @{ Path = 'Wattlebooks.app/Contents/Resources/InvoiceDesk.icns'; Mode = $fileMode },
                @{ Path = 'Wattlebooks.app/Contents/MacOS/Wattlebooks'; Mode = $dirMode })) {
            $entry = [Formats.Tar.PaxTarEntry]::new([Formats.Tar.TarEntryType]::RegularFile, $item.Path)
            $entry.Mode = $item.Mode
            $entry.DataStream = [IO.File]::OpenRead((Join-Path $staging $item.Path))
            $tar.WriteEntry($entry)
            $entry.DataStream.Dispose()
        }
    }
    finally {
        $tar.Dispose(); $gzip.Dispose(); $file.Dispose()
    }
    Remove-Item $staging -Recurse -Force
    Write-Host "made $archive"
}
