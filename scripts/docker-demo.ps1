param([switch]$SkipBuild, [int]$Port = 5081)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    . ./scripts/dev-env.ps1 -Seed
    docker compose -p infra -f infra/compose.yaml up -d --wait
    if ($LASTEXITCODE) { throw 'PostgreSQL did not start.' }
    if (!$SkipBuild) { docker build -f infra/Dockerfile -t clubplatform:f5 .; if ($LASTEXITCODE) { throw 'Image build failed.' } }
    $env:ConnectionStrings__Database = "Host=db;Port=5432;Database=clubplatform;Username=clubplatform;Password=$env:POSTGRES_PASSWORD"
    $env:Payments__Provider = 'WhatsAppManual'
    $env:DataProtection__Path = '/keys'
    docker volume create clubplatform_f1_keys | Out-Null
    docker run --rm --user 0 --entrypoint sh -v clubplatform_f1_keys:/keys clubplatform:f5 -c 'chown 1654:1654 /keys'
    if ($LASTEXITCODE) { throw 'Could not initialize key volume.' }
    docker run --rm --network infra_default -e ASPNETCORE_ENVIRONMENT=Development -e ConnectionStrings__Database -e DataProtection__Path -v clubplatform_f1_keys:/keys clubplatform:f5 --migrate
    if ($LASTEXITCODE) { throw 'Migration failed.' }
    $existing = @(docker ps -a --format '{{.Names}}')
    if ($Port -eq 5081 -and $existing -contains 'clubplatform-f2-preview') { docker stop clubplatform-f2-preview | Out-Null }
    if ($Port -eq 5081 -and $existing -contains 'clubplatform-f4-preview') { docker stop clubplatform-f4-preview | Out-Null }
    if ($existing -contains 'clubplatform-f5-preview') { docker stop clubplatform-f5-preview | Out-Null; docker rm clubplatform-f5-preview | Out-Null }
    $runArgs = @('run','-d','--name','clubplatform-f5-preview','--network','infra_default','-p',"127.0.0.1:${Port}:8080",'-v','clubplatform_f1_keys:/keys','-e','ASPNETCORE_ENVIRONMENT=Development','-e','Seed__Enabled=true','-e','ConnectionStrings__Database','-e','Payments__Provider','-e','DataProtection__Path')
    foreach ($name in @('AdminA','AdminB','Security','Bar','Customer','Cashier','Promoter')) { $runArgs += @('-e',"Seed__Passwords__$name") }
    $runArgs += 'clubplatform:f5'
    docker @runArgs | Out-Null
    if ($LASTEXITCODE) { throw 'Demo container failed to start.' }
    Write-Host "Demo: http://localhost:$Port (WhatsApp reservations; manual payment verification)."
} finally { Pop-Location }
