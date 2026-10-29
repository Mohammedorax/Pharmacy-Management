[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $PackageDirectory).Path.TrimEnd('\','/')
if((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked package root is not allowed.'}
$files=[Collections.Generic.List[IO.FileInfo]]::new()
$directories=[Collections.Generic.Stack[string]]::new();$directories.Push($root)
while($directories.Count -gt 0){
    foreach($item in Get-ChildItem -LiteralPath $directories.Pop() -Force){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked package entries are not allowed.'}
        if($item.PSIsContainer){$directories.Push($item.FullName)}else{$files.Add($item)}
    }
}
$manifestPath=Join-Path $root 'MANIFEST.json'
$rows=@(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
if($rows.Count -eq 0){throw 'Package manifest is empty.'}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($row in $rows){
    $relative=[string]$row.Path
    if([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)|:' -or $relative -eq 'MANIFEST.json'){throw 'Invalid manifest path.'}
    $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(-not $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path escapes package.'}
    if(-not $seen.Add($path)){throw 'Duplicate manifest path.'}
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing packaged file: $relative"}
    if([string]$row.Sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne [string]$row.Sha256){throw "Package hash mismatch: $relative"}
}
foreach($file in $files){
    if($file.FullName -ne $manifestPath -and -not $seen.Contains($file.FullName)){throw ('Unmanifested packaged file: '+$file.FullName.Substring($root.Length+1))}
}
Write-Output "PASS package manifest: $($rows.Count) files, exact file set and SHA256; manifest itself excluded."