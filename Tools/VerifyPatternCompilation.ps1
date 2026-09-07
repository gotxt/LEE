param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Set-Location $workspace
$output = Join-Path $workspace 'Logs/PatternValidation'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sdk = Get-ChildItem -LiteralPath "$UnityData/DotNetSdk/sdk" -Directory | Select-Object -First 1
$compiler = Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
$response = Get-ChildItem -LiteralPath 'Library/Bee/artifacts' -Recurse -Filter 'Assembly-CSharp.rsp' | Select-Object -First 1
if (!$response) { throw 'Unity compilation response files are unavailable. Open the project in Unity once.' }
foreach ($assembly in @('Assembly-CSharp', 'Assembly-CSharp-Editor')) {
    $file = Join-Path $response.DirectoryName ($assembly + '.rsp')
    $argsList = @($compiler, '-nologo', '-target:library', '-langversion:9.0', '-nostdlib+', "-out:$output/$assembly.dll")
    foreach ($line in Get-Content -LiteralPath $file) {
        if ($line -match '^-r:') {
            $ref = $line.Substring(3).Trim('"')
            if ($ref -match '[/\\]Assembly-CSharp\.ref\.dll$') { $ref = "$output/Assembly-CSharp.dll" }
            $argsList += "-r:$ref"
        }
        elseif ($line -match '^-define:') { $argsList += $line }
    }
    $folders = if ($assembly -eq 'Assembly-CSharp') { @('Assets/Scripts') } else { @('Assets/Editor', 'Assets/Tests/Editor') }
    foreach ($folder in $folders) { $argsList += @(Get-ChildItem -LiteralPath $folder -Recurse -Filter '*.cs' | ForEach-Object FullName) }
    $generatedResponse = Join-Path $output ($assembly + '.rsp')
    $quoted = @($argsList | Select-Object -Skip 1 | ForEach-Object {
        if ($_ -match '^-r:') { '-r:"' + $_.Substring(3) + '"' }
        elseif ($_ -match '^-out:') { '-out:"' + $_.Substring(5) + '"' }
        elseif ($_ -match '\.cs$') { '"' + $_ + '"' }
        else { $_ }
    })
    [IO.File]::WriteAllLines($generatedResponse, $quoted)
    & "$UnityData/DotNetSdk/dotnet.exe" $compiler "@$generatedResponse"
    if ($LASTEXITCODE -ne 0) { throw "$assembly compilation failed" }
    Write-Output "$assembly compiled against installed Unity references."
}
