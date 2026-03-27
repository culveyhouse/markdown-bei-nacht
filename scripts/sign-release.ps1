[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "",
    [string]$InstallerOutputDir = "",
    [string]$SigningAccountName = "culveyhouse-signing",
    [string]$CertificateProfileName = "public-trust",
    [string]$Endpoint = "https://eus.codesigning.azure.net",
    [string]$CorrelationId = "",
    [string]$IsccPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RequiredFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [string[]]$CandidatePaths
    )

    foreach ($candidatePath in $CandidatePaths) {
        if (-not [string]::IsNullOrWhiteSpace($candidatePath) -and (Test-Path $candidatePath)) {
            return $candidatePath
        }
    }

    throw "$Description was not found. Checked: $($CandidatePaths -join ', ')"
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$FailureMessage = "Command failed."
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE."
    }
}

function Get-IsccPath {
    param(
        [string]$ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (-not (Test-Path $ConfiguredPath)) {
            throw "Inno Setup compiler not found at '$ConfiguredPath'."
        }

        return $ConfiguredPath
    }

    $systemLocalAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $candidatePaths = @(
        (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $systemLocalAppData "Programs\Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    return Get-RequiredFilePath -Description "Inno Setup compiler" -CandidatePaths $candidatePaths
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\publish"
}

if ([string]::IsNullOrWhiteSpace($InstallerOutputDir)) {
    $InstallerOutputDir = Join-Path $repoRoot "artifacts\installer"
}

if ([string]::IsNullOrWhiteSpace($CorrelationId)) {
    $CorrelationId = "markdown-bei-nacht-release-{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss")
}

$signtoolCandidates = @(
    (Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName)
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$signtool = Get-RequiredFilePath -Description "Windows SDK SignTool x64" -CandidatePaths $signtoolCandidates

$azureCliCandidates = @(
    (Get-Command "az.cmd" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$azureCli = Get-RequiredFilePath -Description "Azure CLI" -CandidatePaths $azureCliCandidates

$artifactSigningDlibCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Microsoft\MicrosoftArtifactSigningClientTools\Azure.CodeSigning.Dlib.dll"),
    (Get-ChildItem "$env:LOCALAPPDATA\Microsoft" -Recurse -Filter "Azure.CodeSigning.Dlib.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName)
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$artifactSigningDlib = Get-RequiredFilePath -Description "Artifact Signing dlib" -CandidatePaths $artifactSigningDlibCandidates

$env:PATH = "{0};{1}" -f (Split-Path $azureCli -Parent), $env:PATH

Write-Host "Checking Azure CLI login..."
Invoke-ExternalCommand -FilePath $azureCli -Arguments @("account", "show", "--query", "id", "-o", "tsv") -FailureMessage "Azure CLI is not logged in. Run 'az login --use-device-code' and try again."

$publishScript = Join-Path $scriptRoot "publish.ps1"
$publishOutput = Join-Path $OutputRoot $Runtime
$installerPath = Join-Path $InstallerOutputDir "MarkdownBeiNacht-Setup.exe"

$metadataDir = Join-Path $env:LOCALAPPDATA "MarkdownBeiNachtSigning"
$metadataPath = Join-Path $metadataDir "metadata.json"
New-Item -ItemType Directory -Force -Path $metadataDir | Out-Null

$metadata = [ordered]@{
    Endpoint = $Endpoint
    CodeSigningAccountName = $SigningAccountName
    CertificateProfileName = $CertificateProfileName
    CorrelationId = $CorrelationId
    ExcludeCredentials = @(
        "EnvironmentCredential",
        "WorkloadIdentityCredential",
        "ManagedIdentityCredential",
        "SharedTokenCacheCredential",
        "VisualStudioCredential",
        "VisualStudioCodeCredential",
        "AzurePowerShellCredential",
        "AzureDeveloperCliCredential",
        "InteractiveBrowserCredential"
    )
}
$metadata | ConvertTo-Json -Depth 3 | Set-Content -Path $metadataPath -Encoding ascii

$filesToSign = @(
    (Join-Path $publishOutput "MarkdownBeiNacht.exe"),
    (Join-Path $publishOutput "MarkdownBeiNacht.dll"),
    (Join-Path $publishOutput "MarkdownBeiNacht.Core.dll")
)

Write-Host "Publishing application..."
Invoke-ExternalCommand -FilePath "powershell.exe" -Arguments @(
    "-ExecutionPolicy", "Bypass",
    "-File", $publishScript,
    "-Configuration", $Configuration,
    "-Runtime", $Runtime,
    "-OutputRoot", $OutputRoot
) -FailureMessage "Publish step failed."

foreach ($filePath in $filesToSign) {
    if (-not (Test-Path $filePath)) {
        throw "Expected publish output not found: $filePath"
    }
}

Write-Host "Signing published application binaries..."
$publishSignArguments = @(
    "sign",
    "/v",
    "/debug",
    "/fd", "SHA256",
    "/tr", "http://timestamp.acs.microsoft.com",
    "/td", "SHA256",
    "/dlib", $artifactSigningDlib,
    "/dmdf", $metadataPath
) + $filesToSign
Invoke-ExternalCommand -FilePath $signtool -Arguments $publishSignArguments -FailureMessage "Signing the published application binaries failed."

$resolvedIsccPath = Get-IsccPath -ConfiguredPath $IsccPath

Write-Host "Building installer from signed publish output..."
Push-Location (Join-Path $repoRoot "installer")
try {
    Invoke-ExternalCommand -FilePath $resolvedIsccPath -Arguments @("MarkdownBeiNacht.iss") -FailureMessage "Installer build failed."
}
finally {
    Pop-Location
}

if (-not (Test-Path $installerPath)) {
    throw "Expected installer output not found: $installerPath"
}

Write-Host "Signing installer..."
Invoke-ExternalCommand -FilePath $signtool -Arguments @(
    "sign",
    "/v",
    "/debug",
    "/fd", "SHA256",
    "/tr", "http://timestamp.acs.microsoft.com",
    "/td", "SHA256",
    "/dlib", $artifactSigningDlib,
    "/dmdf", $metadataPath,
    $installerPath
) -FailureMessage "Signing the installer failed."

Write-Host "Verifying final installer signature..."
Invoke-ExternalCommand -FilePath $signtool -Arguments @(
    "verify",
    "/pa",
    "/v",
    $installerPath
) -FailureMessage "Installer signature verification failed."

Write-Host ""
Write-Host "Signed release artifacts:"
foreach ($filePath in $filesToSign + $installerPath) {
    Write-Host " - $filePath"
}
Write-Host ""
Write-Host "Artifact Signing metadata: $metadataPath"
