[CmdletBinding()]
param(
    [switch]$Configure,
    [switch]$RunBackend,
    [string]$Username,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$helperProject = Join-Path $PSScriptRoot 'SharedDatabase/SharedDatabase.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Hay cai .NET 10 SDK, mo lai terminal va chay lai script.'
}

$helperArguments = @('--root', $repoRoot)
if ($Configure) { $helperArguments += '--configure' }
if ($RunBackend) { $helperArguments += '--run' }
if ($Username) { $helperArguments += @('--username', $Username) }
if ($Help) { $helperArguments += '--help' }

& dotnet run --project $helperProject -- @helperArguments
exit $LASTEXITCODE
