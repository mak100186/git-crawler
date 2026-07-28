# .claude/scripts/render-diagrams.ps1
#
# Renders every Mermaid .mmd file in docs/diagrams/mmd to a PNG in
# docs/diagrams/img, via mermaid-cli (mmdc), dark theme. Installs mmdc
# if missing. Re-run after any .mmd edit - always re-renders, no
# incremental diffing, so there's no stale-cache risk.
#
# NOTE: comments/strings in this script must stay plain ASCII (no em
# dashes, smart quotes, etc). Windows PowerShell 5.1 (the default
# `powershell.exe`) misparses non-ASCII characters in a BOM-less UTF-8
# file, which corrupts brace/string matching later in the script.

param(
    [string]$MmdDir = "docs/diagrams/mmd",
    [string]$ImgDir = "docs/diagrams/img",
    [string]$Background = "#1e1e1e",
    [int]$Width = 1600,
    [int]$Height = 1200,
    [int]$Scale = 2
)

$ErrorActionPreference = "Stop"

function Find-Mmdc {
    $cmd = Get-Command mmdc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$mmdc = Find-Mmdc
if (-not $mmdc) {
    Write-Host "mmdc not found - installing @mermaid-js/mermaid-cli globally..."
    npm install -g @mermaid-js/mermaid-cli
    $mmdc = Find-Mmdc
    if (-not $mmdc) {
        Write-Error "mmdc install failed - install Node/npm first, then re-run this script."
        exit 1
    }
}

if (-not (Test-Path $MmdDir)) {
    Write-Host "No diagrams directory at $MmdDir - nothing to render."
    exit 0
}

New-Item -ItemType Directory -Force -Path $ImgDir | Out-Null

# Puppeteer needs --no-sandbox in some locked-down/CI environments.
# Written via .NET File.WriteAllText with an explicit no-BOM UTF8
# encoding, not `Out-File -Encoding utf8` - on Windows PowerShell 5.1,
# `-Encoding utf8` always prepends a BOM, and Node's JSON.parse (used by
# mmdc/puppeteer to read this file) rejects a BOM-prefixed JSON file.
$puppeteerConfigPath = Join-Path $PSScriptRoot "puppeteer-config.json"
if (-not (Test-Path $puppeteerConfigPath)) {
    [System.IO.File]::WriteAllText($puppeteerConfigPath, '{"args": ["--no-sandbox"]}', (New-Object System.Text.UTF8Encoding $false))
}

$mmdFiles = Get-ChildItem -Path $MmdDir -Filter "*.mmd" -File -ErrorAction SilentlyContinue
if (-not $mmdFiles) {
    Write-Host "No .mmd files found in $MmdDir"
    exit 0
}

$rendered = 0
foreach ($file in $mmdFiles) {
    $outPng = Join-Path $ImgDir ($file.BaseName + ".png")
    Write-Host "Rendering $($file.Name) -> $outPng"
    & mmdc -i $file.FullName -o $outPng -t dark -b $Background -w $Width -H $Height -s $Scale -p $puppeteerConfigPath
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to render $($file.Name)"
        continue
    }
    $rendered++
}

Write-Host "Done. $rendered / $($mmdFiles.Count) diagram(s) rendered to $ImgDir"
