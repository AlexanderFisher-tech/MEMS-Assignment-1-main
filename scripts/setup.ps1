$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$webRoot = Join-Path $projectRoot 'src/Mems.Web'
$settingsPath = Join-Path $webRoot 'appsettings.Development.json'
$databasePath = Join-Path $webRoot 'App_Data/mems.db'
New-Item -ItemType Directory -Path (Join-Path $webRoot 'App_Data') -Force | Out-Null
if (!(Test-Path -LiteralPath $settingsPath)) {
    @{
        ConnectionStrings = @{ DefaultConnection = "Data Source=$databasePath" }
        Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning'; 'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning' } }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $settingsPath
}
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
dotnet restore (Join-Path $projectRoot 'MEMS.sln')
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build (Join-Path $projectRoot 'MEMS.sln') --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project $webRoot
if ($LASTEXITCODE -ne 0) { throw 'Migration failed.' }
Write-Host 'Setup complete. Run: dotnet run --project src/Mems.Web --launch-profile http'
