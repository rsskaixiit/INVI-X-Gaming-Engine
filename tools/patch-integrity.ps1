param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'

$placeholder = [System.Text.Encoding]::ASCII.GetBytes('DEADBEEF' * 8)
$bytes = [System.IO.File]::ReadAllBytes($AssemblyPath)

$offsets = New-Object System.Collections.Generic.List[int]
$limit = $bytes.Length - $placeholder.Length
for ($i = 0; $i -le $limit; $i++) {
    if ($bytes[$i] -ne $placeholder[0]) { continue }
    $match = $true
    for ($j = 1; $j -lt $placeholder.Length; $j++) {
        if ($bytes[$i + $j] -ne $placeholder[$j]) { $match = $false; break }
    }
    if ($match) {
        $offsets.Add($i)
        $i += $placeholder.Length - 1
    }
}

if ($offsets.Count -ne 1) {
    throw "integrity patch: expected exactly 1 placeholder in $AssemblyPath, found $($offsets.Count)"
}

$zeroed = [byte[]]$bytes.Clone()
$offset = $offsets[0]
for ($k = 0; $k -lt $placeholder.Length; $k++) { $zeroed[$offset + $k] = 0 }

$sha = [System.Security.Cryptography.SHA256]::Create()
$digest = $sha.ComputeHash($zeroed)
$hex = -join ($digest | ForEach-Object { $_.ToString('x2') })
$hexBytes = [System.Text.Encoding]::ASCII.GetBytes($hex)
[Array]::Copy($hexBytes, 0, $bytes, $offset, $hexBytes.Length)
[System.IO.File]::WriteAllBytes($AssemblyPath, $bytes)

Write-Host "[INVI X] integrity hash embedded: $hex"
