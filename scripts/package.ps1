$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$archivePath = Join-Path $outputRoot 'MEMS-Assignment-1-source.zip'
if (Test-Path -LiteralPath $archivePath) { throw 'Archive already exists. Rename it before packaging again.' }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $projectRoot -File -Recurse -Force | ForEach-Object {
        $relativePath = $_.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        if ($relativePath -match '(^|/)(bin|obj|App_Data|artifacts|node_modules|\.git|\.tools|\.vs|\.vscode|\.codex|\.agents|\.aws)(/|$)' -or
            $relativePath -match '(^|/)appsettings\.(Development|Production)\.json$|\.db($|-)|\.(pfx|publishsettings|user)$|(^|/)\.env$') { return }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relativePath) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Host "Source archive prepared: $archivePath"
