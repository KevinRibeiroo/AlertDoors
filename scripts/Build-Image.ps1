param([string]$ImageTag = 'alertdoors:local')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $taskRoot
try {
    $taskBranch = git branch --show-current
    if ($LASTEXITCODE -ne 0 -or $taskBranch -ne 'develop') { throw 'Build the development image from develop.' }
    docker build --platform linux/amd64 -t $ImageTag .
    if ($LASTEXITCODE -ne 0) { throw 'Docker build failed.' }
} finally { Pop-Location }
