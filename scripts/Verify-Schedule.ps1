$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
dotnet test (Join-Path $taskRoot 'tests/AlertDoors.Tests/AlertDoors.Tests.csproj') --filter FullyQualifiedName~ScheduleTests --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Schedule verification failed.' }
