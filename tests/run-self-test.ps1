$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$exe = Join-Path $root "dist\FastViewer.exe"
$report = Join-Path $root "dist\FastViewer.selftest.txt"

Push-Location $root
try {
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /win32icon:assets\FastViewer.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:dist\FastViewer.exe src\FastViewer.cs
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

    if (Test-Path $report) { Remove-Item -LiteralPath $report -Force }
    $process = Start-Process -FilePath $exe -ArgumentList @("--self-test", $report) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        if (Test-Path $report) { Get-Content -LiteralPath $report }
        throw "Self-test failed with exit code $($process.ExitCode)"
    }

    Get-Content -LiteralPath $report
}
finally {
    Pop-Location
}
