$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactRoot = Join-Path $repoRoot 'artifacts'
$packageName = 'MouseGestureDesktop-win-x64-portable'
# Use a fresh staging directory so old publish files cannot enter the package.
$stagingRoot = Join-Path $artifactRoot ([Guid]::NewGuid().ToString('N'))
$publishDir = Join-Path $stagingRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
dotnet publish (Join-Path $repoRoot 'MouseGestureDesktop/MouseGestureDesktop.csproj') `
    -c Release -p:PublishProfile=Portable -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

foreach ($requiredFile in @('MouseGestureDesktop.exe', 'coreclr.dll', 'hostfxr.dll', 'System.Windows.Forms.dll')) {
    if (-not (Test-Path (Join-Path $publishDir $requiredFile))) {
        throw "Portable package is missing $requiredFile"
    }
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $publishDir
Compress-Archive -Path $publishDir -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $packageName.zip" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii
Write-Host "Portable package: $zipPath"
