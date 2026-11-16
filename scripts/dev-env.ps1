param([switch]$Seed)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$settingsPath = Join-Path $repo '.local/settings.json'
if (!(Test-Path -LiteralPath $settingsPath)) {
    New-Item -ItemType Directory -Force (Join-Path $repo '.local') | Out-Null
    $settings = [ordered]@{
        POSTGRES_PASSWORD = ('Local!' + [Guid]::NewGuid().ToString('N'))
        DataProtection__Path = (Join-Path $repo '.local/keys')
    }
    foreach ($account in @('AdminA','AdminB','Security','Bar','Customer')) { $settings["Seed__Passwords__$account"] = 'Demo!' + [Guid]::NewGuid().ToString('N') }
    $settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath
    Write-Host 'Created persistent local configuration in .local/settings.json. This file contains demo credentials; keep it private.'
}
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
foreach ($newAccount in @('Cashier','Promoter')) {
if (!$settings.PSObject.Properties[('Seed__Passwords__' + $newAccount)]) {
    $settings | Add-Member -NotePropertyName ('Seed__Passwords__' + $newAccount) -NotePropertyValue ('Demo!' + [Guid]::NewGuid().ToString('N'))
    $settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath
}
}
foreach ($property in $settings.PSObject.Properties) { [Environment]::SetEnvironmentVariable($property.Name, [string]$property.Value, 'Process') }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5080'
$env:Seed__Enabled = $Seed.IsPresent.ToString()
$env:ConnectionStrings__Database = "Host=localhost;Port=55432;Database=clubplatform;Username=clubplatform;Password=$env:POSTGRES_PASSWORD"
$env:TestDatabase__Connection = $env:ConnectionStrings__Database
$localSdk = Join-Path $env:LOCALAPPDATA 'boboteca-dotnet'
if (Test-Path -LiteralPath (Join-Path $localSdk 'dotnet.exe')) { $env:DOTNET_ROOT = $localSdk; $env:PATH = "$localSdk;$env:PATH" }

