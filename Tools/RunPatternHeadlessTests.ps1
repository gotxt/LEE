param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$output = Join-Path $workspace 'Logs/PatternValidation'
$sdk = Get-ChildItem -LiteralPath "$UnityData/DotNetSdk/sdk" -Directory | Select-Object -First 1
$compiler = Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
& "$UnityData/DotNetSdk/dotnet.exe" $compiler -nologo -target:exe -nostdlib+ "-r:$UnityData/UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll" "-out:$output/PatternHeadlessTests.exe" "$PSScriptRoot/PatternHeadlessTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
foreach ($module in @('CoreModule','AudioModule','SharedInternalsModule')) {
    Copy-Item -LiteralPath "$UnityData/Managed/UnityEngine/UnityEngine.$module.dll" -Destination $output -Force
}
$nunit = Get-ChildItem -LiteralPath "$workspace/Library/PackageCache" -Recurse -Filter 'nunit.framework.dll' | Select-Object -First 1
Copy-Item -LiteralPath $nunit.FullName -Destination $output -Force
Push-Location $output
try {
    & "$UnityData/MonoBleedingEdge/bin/mono.exe" './PatternHeadlessTests.exe' | Tee-Object -FilePath './HeadlessTests.log'
    if ($LASTEXITCODE -ne 0) { throw 'Headless pattern tests failed' }
} finally { Pop-Location }
