[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
$dotnet = Join-Path $repoRoot ".dotnet8\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$env:DOTNET_ROOT = (Split-Path $dotnet -Parent)
$env:PATH = "$($env:DOTNET_ROOT);$($env:PATH)"
$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-cli"
$env:HOME = $env:DOTNET_CLI_HOME
$env:USERPROFILE = Join-Path $repoRoot ".localprofile"
$env:APPDATA = Join-Path $env:USERPROFILE "AppData\Roaming"
$env:LOCALAPPDATA = Join-Path $env:USERPROFILE "AppData\Local"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
$env:NUGET_HTTP_CACHE_PATH = Join-Path $env:LOCALAPPDATA "NuGet\v3-cache"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$arguments = @("build", "MarkdownBeiNacht.sln", "-m:1", "-c", $Configuration, "-v", "minimal")
if ($NoRestore) {
    $arguments += "--no-restore"
}

& $dotnet @arguments

