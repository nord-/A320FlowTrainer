# setup.ps1 - A320 Flow Trainer setup script
# Installs all dependencies and builds the app

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot

Write-Host ""
Write-Host "=== A320 Flow Trainer Setup ===" -ForegroundColor Cyan
Write-Host ""

# -------------------------------------------------------
# 1. .NET 10 SDK
# -------------------------------------------------------
Write-Host "[1/4] Checking .NET 10 SDK..." -ForegroundColor Yellow

$dotnetOk = $false
try {
    $sdkList = & dotnet --list-sdks 2>$null
    if ($sdkList -match "^10\.") {
        $dotnetOk = $true
    }
} catch { }

if ($dotnetOk) {
    Write-Host "  .NET 10 SDK already installed. Skipping." -ForegroundColor Green
} else {
    Write-Host "  Installing .NET 10 SDK via winget..."
    winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Failed to install .NET 10 SDK." -ForegroundColor Red
        Write-Host "  Install manually from https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Red
        exit 1
    }
    # Refresh PATH so dotnet is available in this session
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    Write-Host "  .NET 10 SDK installed." -ForegroundColor Green
}

# -------------------------------------------------------
# 2. Vosk speech recognition model
# -------------------------------------------------------
Write-Host "[2/4] Checking Vosk model..." -ForegroundColor Yellow

$modelDir = Join-Path $projectRoot "A320FlowTrainer\model"

if (Test-Path (Join-Path $modelDir "conf")) {
    Write-Host "  Vosk model already present. Skipping." -ForegroundColor Green
} else {
    Write-Host "  Downloading vosk-model-small-en-us-0.15..."
    $zipUrl = "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip"
    $zipFile = Join-Path $env:TEMP "vosk-model-small-en-us-0.15.zip"

    Invoke-WebRequest -Uri $zipUrl -OutFile $zipFile -UseBasicParsing

    Write-Host "  Extracting..."
    $extractDir = Join-Path $env:TEMP "vosk-extract"
    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
    Expand-Archive -Path $zipFile -DestinationPath $extractDir -Force

    # The zip contains a folder named vosk-model-small-en-us-0.15 - copy its contents
    $innerDir = Join-Path $extractDir "vosk-model-small-en-us-0.15"
    if (-not (Test-Path $modelDir)) { New-Item -ItemType Directory -Path $modelDir -Force | Out-Null }
    Copy-Item -Path "$innerDir\*" -Destination $modelDir -Recurse -Force

    # Cleanup
    Remove-Item $zipFile -Force -ErrorAction SilentlyContinue
    Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "  Vosk model installed to $modelDir" -ForegroundColor Green
}

# -------------------------------------------------------
# 3. Piper voice model
# -------------------------------------------------------
Write-Host "[3/4] Checking Piper voice model..." -ForegroundColor Yellow

$piperDir = Join-Path $projectRoot "tools\piper"
$onnxFile = Join-Path $piperDir "en_US-joe-medium.onnx"
$jsonFile = Join-Path $piperDir "en_US-joe-medium.onnx.json"

$hfBase = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/joe/medium"

if ((Test-Path $onnxFile) -and (Test-Path $jsonFile)) {
    Write-Host "  Piper voice model already present. Skipping." -ForegroundColor Green
} else {
    if (-not (Test-Path $piperDir)) { New-Item -ItemType Directory -Path $piperDir -Force | Out-Null }

    if (-not (Test-Path $onnxFile)) {
        Write-Host "  Downloading en_US-joe-medium.onnx (~60 MB)..."
        Invoke-WebRequest -Uri "$hfBase/en_US-joe-medium.onnx" -OutFile $onnxFile -UseBasicParsing
    }

    if (-not (Test-Path $jsonFile)) {
        Write-Host "  Downloading en_US-joe-medium.onnx.json..."
        Invoke-WebRequest -Uri "$hfBase/en_US-joe-medium.onnx.json" -OutFile $jsonFile -UseBasicParsing
    }

    Write-Host "  Piper voice model installed to $piperDir" -ForegroundColor Green
}

# -------------------------------------------------------
# 4. Build / Publish
# -------------------------------------------------------
Write-Host "[4/4] Publishing app..." -ForegroundColor Yellow

Push-Location (Join-Path $projectRoot "A320FlowTrainer")
try {
    & dotnet publish -c Release -r win-x64 --self-contained
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: dotnet publish failed." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Setup complete! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run the app:" -ForegroundColor White
Write-Host "  cd A320FlowTrainer" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""
