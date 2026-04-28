param(
  [string]$RepositoryRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$backend = Join-Path $repo "OperatorsDraft"
$dist = Join-Path $backend "dist"
$build = Join-Path $backend "build"
$operatorPublish = Join-Path $backend "OperatorRunnerPublished"

function Invoke-NativeStep {
  param(
    [string]$Description,
    [scriptblock]$Command
  )

  Write-Host $Description
  & $Command
  if ($LASTEXITCODE -ne 0) {
    throw "$Description failed with exit code $LASTEXITCODE."
  }
}

if (-not (Test-Path -LiteralPath (Join-Path $backend "server.py"))) {
  throw "Backend server.py was not found at: $backend"
}

Push-Location $backend
try {
  Remove-Item -LiteralPath $dist, $build, $operatorPublish -Recurse -Force -ErrorAction SilentlyContinue

  Invoke-NativeStep "[1/4] Building packaged Python backend server..." {
    python -m PyInstaller --clean --noconfirm --onefile --name EvoFlowBackend server.py
  }

  Invoke-NativeStep "[2/4] Building packaged Python EvoFlow runner..." {
    python -m PyInstaller --clean --noconfirm --onefile --name EvoFlowRunner evoflow\operator_search_main.py
  }

  Invoke-NativeStep "[3/4] Publishing self-contained C# OperatorRunner..." {
    dotnet publish OperatorRunner\OperatorRunner.csproj `
      -c Release `
      -r win-x64 `
      --self-contained true `
      -p:PublishSingleFile=true `
      -p:IncludeNativeLibrariesForSelfExtract=true `
      -p:UseAppHost=true `
      -o $operatorPublish
  }
}
finally {
  Pop-Location
}

Invoke-NativeStep "[4/4] Preparing Unity StreamingAssets backend bundle..." {
  powershell -ExecutionPolicy Bypass -File (Join-Path $repo "scripts\prepare_unity_backend_bundle.ps1") -RepositoryRoot $repo
}

$bundle = Join-Path $repo "unity-agentic-vis-pipeline\Assets\StreamingAssets\EvoFlowBackend"
Copy-Item -LiteralPath (Join-Path $dist "EvoFlowBackend.exe") -Destination (Join-Path $bundle "EvoFlowBackend.exe") -Force
Copy-Item -LiteralPath (Join-Path $dist "EvoFlowRunner.exe") -Destination (Join-Path $bundle "EvoFlowRunner.exe") -Force
Copy-Item -LiteralPath $operatorPublish -Destination (Join-Path $bundle "OperatorRunnerPublished") -Recurse -Force

Write-Host ""
Write-Host "Windows backend bundle is ready:"
Write-Host "  $bundle"
Write-Host ""
Write-Host "The Unity app will start EvoFlowBackend.exe automatically."
Write-Host "Set DASHSCOPE_API_KEY on the target machine before launching the app."
