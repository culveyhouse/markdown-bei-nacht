[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$IsccPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$systemLocalAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration -Runtime $Runtime

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($isccCommand) {
        $IsccPath = $isccCommand.Source
    }
    else {
        $candidatePaths = @(
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
            (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
            (Join-Path $systemLocalAppData "Programs\Inno Setup 6\ISCC.exe")
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

        foreach ($candidatePath in $candidatePaths) {
            if (Test-Path $candidatePath) {
                $IsccPath = $candidatePath
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path $IsccPath)) {
    throw "Inno Setup compiler not found at '$IsccPath'. Install Inno Setup 6 or pass -IsccPath explicitly."
}

Push-Location (Join-Path $repoRoot "installer")
try {
    & $IsccPath "MarkdownBeiNacht.iss"
}
finally {
    Pop-Location
}

