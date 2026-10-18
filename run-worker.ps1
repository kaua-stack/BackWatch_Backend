$ErrorActionPreference = 'Stop'

$dotnet = 'C:\dotnet\dotnet.exe'
$secretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\9a139e55-19ac-4b98-9c60-e32feaef27c8\secrets.json'

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw ".NET was not found at $dotnet."
}

if (-not (Test-Path -LiteralPath $secretsPath)) {
    throw 'API database credentials were not found in .NET User Secrets.'
}

$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
$connectionString = $secrets.'ConnectionStrings:DefaultConnection'
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'ConnectionStrings:DefaultConnection is missing from .NET User Secrets.'
}

$env:ConnectionStrings__DefaultConnection = $connectionString
try {
    & $dotnet run --project (Join-Path $PSScriptRoot 'src\ApiReliabilitySuite.Workers\ApiReliabilitySuite.Workers.csproj')
    if ($LASTEXITCODE -ne 0) {
        throw "Health check worker exited with code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    $connectionString = $null
    $secrets = $null
}
