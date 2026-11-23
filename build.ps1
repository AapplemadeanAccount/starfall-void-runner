param(
    [ValidateSet("anycpu", "x86", "x64")]
    [string]$Platform = "anycpu",
    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$outDir = Join-Path $root "build"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$outExe = if ($OutFile) { [System.IO.Path]::GetFullPath($OutFile) } else { Join-Path $outDir "Starfall.exe" }
$outParent = Split-Path -Parent $outExe
if ($outParent) { New-Item -ItemType Directory -Force -Path $outParent | Out-Null }

$cscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $null
foreach ($candidate in $cscCandidates) {
    if (Test-Path $candidate) { $csc = $candidate; break }
}

if (-not $csc) {
    Write-Error "No in-box C# compiler found. Install the .NET Framework 4.8 developer pack or the .NET SDK and run: dotnet build -c Release"
}

$refDir = Join-Path $root "tools\net48refs\build\.NETFramework\v4.8"
$sources = Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName }

if (Test-Path $refDir) {
    Write-Host "Using .NET Framework 4.8 reference assemblies:"
    Write-Host "  $refDir"
    & $csc /nologo /noconfig /target:exe /platform:$Platform /optimize+ /debug- /nostdlib+ `
        /reference:"$refDir\mscorlib.dll" `
        /reference:"$refDir\System.dll" `
        /reference:"$refDir\System.Core.dll" `
        /out:"$outExe" `
        $sources
} else {
    Write-Host "No 4.8 reference assemblies present; compiling against the installed framework runtime."
    & $csc /nologo /target:exe /platform:$Platform /optimize+ /debug- /out:"$outExe" $sources
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Built: $outExe"
Write-Host "Platform: $Platform"
Write-Host "Target framework: .NET Framework 4.8 (no WinForms, no external packages)"
