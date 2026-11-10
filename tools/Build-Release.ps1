<#
.SYNOPSIS
  Baut die Single-File-EXE und das MSI – so wie der Release-Workflow.

.EXAMPLE
  pwsh tools/Build-Release.ps1 -Version 1.0.0
  Ergebnis: artifacts/LSMDisplayResizer-1.0.0-win-x64.exe sowie -de.msi und -en.msi
#>
param(
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publish = Join-Path $root 'publish'
$artifacts = Join-Path $root 'artifacts'

Push-Location $root
try {
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    New-Item -ItemType Directory -Force $artifacts | Out-Null

    dotnet publish src/LSMDisplayResizer/LSMDisplayResizer.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true "-p:Version=$Version" -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fehlgeschlagen' }

    dotnet build installer/LSMDisplayResizer.Installer.wixproj -c Release "-p:Version=$Version" "-p:AppPublishDir=$publish\"
    if ($LASTEXITCODE -ne 0) { throw 'MSI-Build fehlgeschlagen' }

    Copy-Item (Join-Path $publish 'LSMDisplayResizer.exe') (Join-Path $artifacts "LSMDisplayResizer-$Version-win-x64.exe") -Force
    foreach ($c in @{ 'de-DE' = 'de'; 'en-US' = 'en' }.GetEnumerator()) {
        $msi = Get-ChildItem installer/bin -Recurse -Filter 'LSMDisplayResizer.msi' | Where-Object { $_.Directory.Name -eq $c.Key } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $msi) { throw "MSI für $($c.Key) fehlt" }
        Copy-Item $msi.FullName (Join-Path $artifacts "LSMDisplayResizer-$Version-win-x64-$($c.Value).msi") -Force
    }

    Get-ChildItem $artifacts -Filter "LSMDisplayResizer-$Version-*" | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
}
finally {
    Pop-Location
}
