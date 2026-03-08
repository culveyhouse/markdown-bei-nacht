[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "",
    [string]$BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\publish"
}

$dotnet = Join-Path $repoRoot ".dotnet8\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$publishOutput = Join-Path $OutputRoot $Runtime
$installerDependencyDir = Join-Path $repoRoot "installer\dependencies"
$bootstrapperPath = Join-Path $installerDependencyDir "MicrosoftEdgeWebView2Setup.exe"

if (Test-Path $publishOutput) {
    foreach ($item in Get-ChildItem $publishOutput -Force) {
        try {
            Remove-Item $item.FullName -Recurse -Force
        }
        catch {
            throw "Publish output '$publishOutput' is in use. Close any running Markdown bei Nacht instance launched from that folder, or publish to a different -OutputRoot."
        }
    }
}
else {
    New-Item -ItemType Directory -Force $publishOutput | Out-Null
}
New-Item -ItemType Directory -Force $installerDependencyDir | Out-Null

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

& $dotnet publish (Join-Path $repoRoot "src\MarkdownBeiNacht\MarkdownBeiNacht.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -o $publishOutput
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Get-ChildItem $publishOutput -Recurse -Directory -Filter "*.WebView2" | Remove-Item -Recurse -Force
Get-ChildItem $publishOutput -Recurse -Filter "*.pdb" | Remove-Item -Force

if (-not (Test-Path $bootstrapperPath)) {
    try {
        Invoke-WebRequest -UseBasicParsing $BootstrapperUrl -OutFile $bootstrapperPath
    }
    catch {
        Write-Warning "Could not download the WebView2 bootstrapper. The installer can still be built, but runtime bootstrapping will be skipped unless the file is added manually."
    }
}

Write-Host "Published application to $publishOutput"
Write-Host "WebView2 bootstrapper path: $bootstrapperPath"
