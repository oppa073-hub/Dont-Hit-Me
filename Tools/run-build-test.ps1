# 개발 빌드를 만들고(Unity 배치모드), 만든 exe 로 화면 점검(-uishots)을 돌려 스크린샷을 남긴다. 에디터에서는 괜찮은데 빌드에서만 생기는 문제(분홍색 셰이더, 빠진 파일)를 찾는다.
# Unity 에디터를 먼저 꺼야 한다. 쓰는 법:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-build-test.ps1
#   -SkipBuild  빌드를 건너뛰고 있는 빌드로만 점검   -SkipRun  빌드만 하고 점검은 안 함   -Lang 0|1
# 빌드 폴더는 저장소 밖이다(구매한 에셋이 들어 있어서 절대 커밋/푸시하지 않는다). 점검 결과는 SimReports/build-시각/ (gitignore).
param([string]$BuildDir = "D:\Unity\project\_Builds\DontHitMe-test", [int]$Lang = 0, [switch]$SkipBuild, [switch]$SkipRun,
      [string]$UnityExe = "C:\unityproject\6000.3.5f2\Editor\Unity.exe", [int]$MaxSec = 900)

$project = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $project "SimReports"
New-Item -ItemType Directory -Force $out | Out-Null

if (-not $SkipBuild) {
    if (-not (Test-Path $UnityExe)) { "실패: Unity 를 찾지 못했습니다 ($UnityExe)"; exit 2 }
    $lock = Join-Path $project "Temp\UnityLockfile"
    if (Test-Path $lock) {
        try { $fs = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close() }
        catch { "실패: Unity 에디터가 이 프로젝트를 열고 있습니다. 에디터를 끄고 다시 실행하세요."; exit 2 }
    }
    $blog = Join-Path $out "build-editor-$stamp.log"
    $a = @("-batchmode", "-quit", "-projectPath", "`"$project`"", "-logFile", "`"$blog`"", "-executeMethod", "BuildTest.Build", "-buildDir", "`"$BuildDir`"")
    "빌드 시작... (폴더: $BuildDir, 몇 분 걸립니다)"
    $p = Start-Process -FilePath $UnityExe -ArgumentList $a -PassThru
    if (-not $p.WaitForExit($MaxSec * 1000)) { "시간 초과: Unity 를 강제로 끕니다"; Stop-Process -Id $p.Id -Force }
    "빌드 종료 코드: $($p.ExitCode)  (로그: $blog)"
    Select-String -Path $blog -Pattern "\[BuildTest\]" | ForEach-Object { $_.Line }
    if ($p.ExitCode -ne 0) { exit $p.ExitCode }
}

if ($SkipRun) { exit 0 }
$exe = Join-Path $BuildDir "DontHitMe.exe"
if (-not (Test-Path $exe)) { "실패: 빌드 파일이 없습니다 ($exe)"; exit 2 }
$dir = Join-Path $out "build-$stamp"
New-Item -ItemType Directory -Force $dir | Out-Null
$rlog = Join-Path $out "build-player-$stamp.log"

# 저장 데이터(PlayerPrefs)는 실행 전 백업, 후 복구
$regKey = "HKCU\Software\DefaultCompany\Don't Hit Me"
$backup = Join-Path $out "prefs-backup-build-$stamp.reg"
$hadKey = $false
& reg query $regKey *> $null
if ($LASTEXITCODE -eq 0) { $hadKey = $true; & reg export $regKey $backup /y *> $null }

$a2 = @("-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0", "-logFile", "`"$rlog`"", "-uishots", "`"$dir`"", "-uilang", $Lang)
"빌드 실행... (스크린샷: $dir)"
$p2 = Start-Process -FilePath $exe -ArgumentList $a2 -PassThru
if (-not $p2.WaitForExit($MaxSec * 1000)) { "시간 초과: 게임을 강제로 끕니다"; Stop-Process -Id $p2.Id -Force }

& reg delete $regKey /f *> $null
if ($hadKey) { & reg import $backup *> $null }
"저장 데이터 복구 완료"
Get-ChildItem $dir | Select-Object -First 40 Name, Length | Format-Table -AutoSize | Out-String
"게임 종료 코드: $($p2.ExitCode)  (로그: $rlog)"
$errs = Select-String -Path $rlog -Pattern "Exception|Shader .* not found|pink|Error" -ErrorAction SilentlyContinue | Select-Object -First 15
if ($errs) { "로그의 의심 줄:"; $errs | ForEach-Object { "  " + $_.Line } }
