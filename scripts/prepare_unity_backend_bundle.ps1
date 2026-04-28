param(
  [string]$RepositoryRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
)

$ErrorActionPreference = "Stop"

$source = Join-Path $RepositoryRoot "OperatorsDraft"
$target = Join-Path $RepositoryRoot "unity-agentic-vis-pipeline\Assets\StreamingAssets\EvoFlowBackend"

if (-not (Test-Path -LiteralPath (Join-Path $source "server.py"))) {
  throw "OperatorsDraft backend was not found at: $source"
}

$resolvedRepo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$resolvedSource = (Resolve-Path -LiteralPath $source).Path

if (-not $resolvedSource.StartsWith($resolvedRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to copy from outside repository: $resolvedSource"
}

if (Test-Path -LiteralPath $target) {
  $resolvedTarget = (Resolve-Path -LiteralPath $target).Path
  if (-not $resolvedTarget.StartsWith($resolvedRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove target outside repository: $resolvedTarget"
  }

  Get-ChildItem -LiteralPath $resolvedTarget -Recurse -Force | ForEach-Object { $_.Attributes = "Normal" }
  Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $target | Out-Null

$excludedDirectoryNames = @(
  ".build",
  ".dotnet",
  ".git",
  ".local",
  ".nuget",
  ".python_deps",
  "__pycache__",
  "build",
  "dist",
  "Library",
  "bin",
  "obj"
)

$excludedFileNames = @(
  ".env",
  ".env.local"
)

Get-ChildItem -LiteralPath $source -Recurse -Force | ForEach-Object {
  $item = $_
  $relative = $item.FullName.Substring($source.Length).TrimStart("\", "/")
  $relativeParts = $relative -split "[\\/]+"

  foreach ($part in $relativeParts) {
    if ($excludedDirectoryNames -contains $part) {
      return
    }
  }

  if ($item.PSIsContainer) {
    New-Item -ItemType Directory -Force -Path (Join-Path $target $relative) | Out-Null
    return
  }

  if ($excludedFileNames -contains $item.Name) {
    return
  }

  if ($item.Name -like "*.pyc" -or $item.Name -like "*.tmp" -or $item.Name -like ".tmp_*" -or $item.Name -like "*.log" -or $item.Name -like "*.spec") {
    return
  }

  $destination = Join-Path $target $relative
  $destinationDirectory = Split-Path -Parent $destination
  New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
  Copy-Item -LiteralPath $item.FullName -Destination $destination -Force
}

Write-Host "Bundled EvoFlow backend to:"
Write-Host "  $target"
Write-Host ""
Write-Host "Real secrets were not copied. Configure DASHSCOPE_API_KEY on the target machine."
