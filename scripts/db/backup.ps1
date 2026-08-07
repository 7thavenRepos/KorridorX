param(
    [Parameter(Mandatory = $true)]
    [string]$HostName,

    [int]$Port = 5432,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [string]$OutputDirectory = "./backups"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command pg_dump -ErrorAction SilentlyContinue)) {
    throw "pg_dump was not found. Install PostgreSQL client tools and add them to PATH."
}

if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) {
    throw "Set PGPASSWORD in the current process before running the backup."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$file = Join-Path $OutputDirectory "korridorx-$timestamp.dump"

& pg_dump `
    --host $HostName `
    --port $Port `
    --username $Username `
    --dbname $Database `
    --format custom `
    --compress 9 `
    --no-owner `
    --no-privileges `
    --file $file

if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit code $LASTEXITCODE."
}

Write-Host "Backup created: $file"
