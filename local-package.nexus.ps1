# Used to build Nexus package locally

dotnet build -c Release
$env:BUILD_DIR = "$PSScriptRoot/CustomFont/thunderstore/temp/plugins"
.\package-nexus.ps1