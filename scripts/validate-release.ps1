param(
    [switch]$SkipDatabaseTests,
    [switch]$SkipMigrationDriftCheck,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        throw "$Step failed with exit code $exitCode. Release-candidate validation stopped."
    }
}

Write-Host "== KorridorX release-candidate validation ==" -ForegroundColor Cyan

if (-not $SkipDatabaseTests -and [string]::IsNullOrWhiteSpace($env:KORRIDORX_TEST_CONNECTION_STRING)) {
    throw "KORRIDORX_TEST_CONNECTION_STRING must point to a disposable PostgreSQL database, or pass -SkipDatabaseTests."
}

Write-Host "[1/6] Clean"
Invoke-CheckedCommand -Step "Clean" -Command {
    dotnet clean KorridorX.slnx --configuration $Configuration
}

Write-Host "[2/6] Restore"
Invoke-CheckedCommand -Step "Restore" -Command {
    dotnet restore KorridorX.slnx
}

Write-Host "[3/6] Build"
Invoke-CheckedCommand -Step "Build" -Command {
    dotnet build KorridorX.slnx --configuration $Configuration --no-restore
}

if (-not $SkipDatabaseTests) {
    Write-Host "[4/6] Apply migrations to disposable RC database"
    $previousConnection = $env:ConnectionStrings__DefaultConnection
    try {
        $env:ConnectionStrings__DefaultConnection = $env:KORRIDORX_TEST_CONNECTION_STRING

        Invoke-CheckedCommand -Step "Database migration application" -Command {
            dotnet ef database update --no-build --configuration $Configuration
        }
    }
    finally {
        $env:ConnectionStrings__DefaultConnection = $previousConnection
    }
}
else {
    Write-Host "[4/6] Database migration application skipped"
}

Write-Host "[5/6] Tests"
Invoke-CheckedCommand -Step "Tests" -Command {
    dotnet test KorridorX.slnx `
        --configuration $Configuration `
        --no-build `
        --logger "trx;LogFileName=release-candidate-tests.trx" `
        --results-directory artifacts/release-candidate
}

if (-not $SkipMigrationDriftCheck) {
    Write-Host "[6/6] Pending EF model-change gate"
    Invoke-CheckedCommand -Step "Pending EF model-change gate" -Command {
        dotnet ef migrations has-pending-model-changes --no-build --configuration $Configuration
    }
}
else {
    Write-Host "[6/6] Pending EF model-change gate skipped"
}

Write-Host "Release-candidate validation completed successfully." -ForegroundColor Green
