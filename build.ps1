# Compile IC-7760 Link en un exe autonome avec le compilateur fourni avec Windows (.NET Framework 4.x). Rien à installer.
param([string]$Out = 'dist')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$bin  = [IO.Path]::Combine($root, $Out)
New-Item -ItemType Directory -Force $bin | Out-Null
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$a = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', "/out:$bin\IC7760Link.exe",
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
  "/resource:$root\doc\ic7760.civ,ic7760.civ", "/win32manifest:$root\src\app.manifest")
$ico = Join-Path $root 'src\IC7760Link.ico'
if (Test-Path $ico) { $a += "/win32icon:$ico" }
& $csc @a @src
if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée' }
"OK -> $bin\IC7760Link.exe"
