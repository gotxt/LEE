$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/Resources/Effects/Audio'
New-Item -ItemType Directory -Path $root -Force | Out-Null
foreach ($cue in @(@('Warning', 660, 0.14), @('Impact', 100, 0.24))) {
    $samples = [int][Math]::Ceiling(22050 * $cue[2])
    $writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $root ($cue[0] + '.wav'))))
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $samples * 2))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
        $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]22050); $writer.Write([int]44100)
        $writer.Write([int16]2); $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($samples * 2))
        for ($i = 0; $i -lt $samples; $i++) {
            $sample = [Math]::Sin($i * $cue[1] * 2 * [Math]::PI / 22050) * (1.0 - $i / [double]$samples) * 12000
            $writer.Write([int16]$sample)
        }
    } finally { $writer.Dispose() }
}
