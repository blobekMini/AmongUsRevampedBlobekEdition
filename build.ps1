param(
    [switch]$NoIncrement
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'AmongUsRevamped.csproj'
$buildNumberPath = Join-Path $root 'build-number.txt'
$beVersionPath = Join-Path $root 'BEversion.txt'
$baseVersion = '2.3.0'
$buildNumber = 0
$beVersion = 0

if (Test-Path $buildNumberPath) {
    $buildNumberText = (Get-Content $buildNumberPath -Raw).Trim()
    if ($buildNumberText -and -not [int]::TryParse($buildNumberText, [ref]$buildNumber)) {
        throw "Invalid build number in $buildNumberPath."
    }
}
if (Test-Path $beVersionPath) {
    $beVersionText = (Get-Content $beVersionPath -Raw).Trim()
    if ($beVersionText -and -not [int]::TryParse($beVersionText, [ref]$beVersion)) {
        throw "Invalid Blobek Edition version in $beVersionPath."
    }
}
if ($beVersion -lt 1) {
    throw "Blobek Edition version must be a positive number in $beVersionPath."
}

if (-not $NoIncrement) {
    $buildNumber++
    [System.IO.File]::WriteAllText($buildNumberPath, $buildNumber.ToString())
}
elseif ($buildNumber -lt 1) {
    throw "Cannot build without incrementing because $buildNumberPath has no positive build number."
}

$buildVersion = "$baseVersion.$beVersion.$buildNumber"
$fileVersion = "$baseVersion.$buildNumber"
Write-Host "Building Among Us Revamped Blobek Edition v$buildVersion..."
dotnet build (Join-Path $root 'AmongUsRevamped.csproj') -c Release `
    "-p:Version=$baseVersion" `
    "-p:AssemblyVersion=$baseVersion.0" `
    "-p:FileVersion=$fileVersion" `
    "-p:InformationalVersion=$buildVersion" `
    '-p:IncludeSourceRevisionInInformationalVersion=false'
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Build complete: $buildVersion"
Write-Host "Output: $(Join-Path $root 'bin\Release\net6.0\AmongUsRevamped.dll')"
