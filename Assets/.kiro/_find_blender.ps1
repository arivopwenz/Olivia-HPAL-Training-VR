$roots = @(
  'C:\Program Files\Blender Foundation',
  'C:\Program Files (x86)\Blender Foundation',
  (Join-Path $env:LOCALAPPDATA 'Programs'),
  'C:\Program Files\Steam\steamapps\common',
  $env:USERPROFILE
)
foreach ($r in $roots) {
  if (Test-Path $r) {
    Get-ChildItem -Path $r -Recurse -Filter 'blender.exe' -ErrorAction SilentlyContinue |
      Select-Object -ExpandProperty FullName
  }
}
