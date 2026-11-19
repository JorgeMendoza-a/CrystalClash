# Reconstruye el módulo de Cloud Code (CloudCode/Build/CrystalClashRanked.ccm).
# Requiere el .NET SDK 8 instalado. Ejecutar desde la carpeta CloudCode:
#   powershell -ExecutionPolicy Bypass -File build_module.ps1
$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "Build\publish"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish "$PSScriptRoot\CrystalClashRanked\Project\CrystalClashRanked.csproj" -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o $out
$ccm = Join-Path $PSScriptRoot "Build\CrystalClashRanked.ccm"
if (Test-Path $ccm) { Remove-Item $ccm }
Compress-Archive -Path "$out\*" -DestinationPath "$ccm.zip"
Move-Item "$ccm.zip" $ccm
Remove-Item $out -Recurse -Force
Write-Host "Listo: $ccm"
