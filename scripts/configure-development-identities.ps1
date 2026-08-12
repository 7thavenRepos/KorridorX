[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot "..\KorridorX.csproj"),
    [string]$EmailDomain = "dev.korridorx.test",
    [string]$CountryCode = "CA",
    [string]$BusinessName = "KorridorX Development Business"
)

$ErrorActionPreference = "Stop"

if ($env:ASPNETCORE_ENVIRONMENT -eq "Production") {
    throw "This helper is for local Development only and will not run while ASPNETCORE_ENVIRONMENT=Production."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK was not found on PATH."
}

if (-not (Test-Path $ProjectPath)) {
    throw "KorridorX project file not found: $ProjectPath"
}

if ([string]::IsNullOrWhiteSpace($EmailDomain)) {
    throw "EmailDomain is required."
}

if ($CountryCode -notmatch '^[A-Za-z]{2,10}$') {
    throw "CountryCode must contain between two and ten letters."
}

function ConvertFrom-LocalSecureString {
    param(
        [Parameter(Mandatory = $true)]
        [SecureString]$Value
    )

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Read-ValidSeedPassword {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt
    )

    while ($true) {
        $secureValue = Read-Host $Prompt -AsSecureString
        $plainValue = ConvertFrom-LocalSecureString $secureValue

        if ($plainValue.Length -ge 8 -and
            $plainValue -cmatch '[A-Z]' -and
            $plainValue -cmatch '[a-z]' -and
            $plainValue -match '[0-9]') {
            return $plainValue
        }

        Write-Warning "Use at least eight characters with an uppercase letter, a lowercase letter, and a number."
    }
}

function Set-KorridorXUserSecret {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Key,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    & dotnet user-secrets set $Key $Value --project $ProjectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to store the local setting '$Key'."
    }
}

$testPassword = Read-ValidSeedPassword "Development test-user password"
$superAdminPassword = Read-ValidSeedPassword "Development Super Administrator password"
$normalizedDomain = $EmailDomain.Trim().ToLowerInvariant()
$normalizedCountry = $CountryCode.Trim().ToUpperInvariant()

$users = @(
    @{ Path = "Consumer"; FirstName = "Consumer"; LastName = "Tester"; Email = "consumer@$normalizedDomain"; CountryCode = $normalizedCountry },
    @{ Path = "BusinessOwner"; FirstName = "Business"; LastName = "Owner"; Email = "business.owner@$normalizedDomain"; CountryCode = $normalizedCountry },
    @{ Path = "BusinessAdministrator"; FirstName = "Business"; LastName = "Administrator"; Email = "business.admin@$normalizedDomain"; CountryCode = $normalizedCountry },
    @{ Path = "ComplianceOfficer"; FirstName = "Compliance"; LastName = "Officer"; Email = "compliance@$normalizedDomain"; CountryCode = "" },
    @{ Path = "SupportOfficer"; FirstName = "Support"; LastName = "Officer"; Email = "support@$normalizedDomain"; CountryCode = "" },
    @{ Path = "OperationsOfficer"; FirstName = "Operations"; LastName = "Officer"; Email = "operations@$normalizedDomain"; CountryCode = "" },
    @{ Path = "Administrator"; FirstName = "Platform"; LastName = "Administrator"; Email = "admin@$normalizedDomain"; CountryCode = "" }
)

try {
    Set-KorridorXUserSecret "IdentitySeed:SeedTestUsers" "true"

    Set-KorridorXUserSecret "IdentitySeed:SuperAdmin:FirstName" "Super"
    Set-KorridorXUserSecret "IdentitySeed:SuperAdmin:LastName" "Administrator"
    Set-KorridorXUserSecret "IdentitySeed:SuperAdmin:Email" "superadmin@$normalizedDomain"
    Set-KorridorXUserSecret "IdentitySeed:SuperAdmin:Password" $superAdminPassword
    Set-KorridorXUserSecret "IdentitySeed:SuperAdmin:CountryCode" $normalizedCountry

    foreach ($user in $users) {
        $prefix = "IdentitySeed:TestUsers:$($user.Path)"
        Set-KorridorXUserSecret "${prefix}:FirstName" $user.FirstName
        Set-KorridorXUserSecret "${prefix}:LastName" $user.LastName
        Set-KorridorXUserSecret "${prefix}:Email" $user.Email
        Set-KorridorXUserSecret "${prefix}:Password" $testPassword

        if (-not [string]::IsNullOrWhiteSpace($user.CountryCode)) {
            Set-KorridorXUserSecret "${prefix}:CountryCode" $user.CountryCode
        }
    }

    Set-KorridorXUserSecret "IdentitySeed:Business:Name" $BusinessName
    Set-KorridorXUserSecret "IdentitySeed:Business:CountryCode" $normalizedCountry
}
finally {
    $testPassword = $null
    $superAdminPassword = $null
}

Write-Host "Development identity settings saved outside the repository." -ForegroundColor Green
Write-Host "Start KorridorX in Development to create the eight login identities." -ForegroundColor Green
