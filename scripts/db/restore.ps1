param(
    [Parameter(Mandatory = $true)]
    [string]$HostName,

    [int]$Port = 5432,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [switch]$ConfirmRestore
)

$ErrorActionPreference = "Stop"

if (-not $ConfirmRestore) {
    throw "Restore is destructive. Re-run with -ConfirmRestore after verifying the target database."
}

if (-not (Test-Path $BackupFile)) {
    throw "Backup file was not found: $BackupFile"
}

if (-not (Get-Command pg_restore -ErrorAction SilentlyContinue)) {
    throw "pg_restore was not found. Install PostgreSQL client tools and add them to PATH."
}

if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) {
    throw "Set PGPASSWORD in the current process before running the restore."
}

& pg_restore `
    --host $HostName `
    --port $Port `
    --username $Username `
    --dbname $Database `
    --clean `
    --if-exists `
    --no-owner `
    --no-privileges `
    $BackupFile

if ($LASTEXITCODE -ne 0) {
    throw "pg_restore failed with exit code $LASTEXITCODE."
}

Write-Host "Restore completed from: $BackupFile"
