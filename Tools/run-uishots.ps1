# 화면 점검 도구를 Unity 배치모드로 돌린다: 글자가 상자 밖으로 나오는지/영어 모드에 한국어가 남았는지 점검하고 스크린샷(PNG)을 남긴다.
# Unity 에디터를 먼저 꺼야 한다. 쓰는 법:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-uishots.ps1 -Lang 0
#   -Lang 0|1   0 영어(기본) / 1 한국어
# 결과: SimReports/uishots-언어-시각/ 폴더의 audit-*.txt 와 *.png, SimReports/editor-uishots-*.log (Unity 로그)
param([int]$Lang = 0, [string]$UnityExe = "C:\unityproject\6000.3.5f2\Editor\Unity.exe", [int]$MaxSec = 600)

$project = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path $UnityExe)) { "실패: Unity 를 찾지 못했습니다 ($UnityExe)"; exit 2 }
$lock = Join-Path $project "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $fs = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close() }
    catch { "실패: Unity 에디터가 이 프로젝트를 열고 있습니다. 에디터를 끄고 다시 실행하세요."; exit 2 }
}
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $project "SimReports"
$dir = Join-Path $out ("uishots-" + $(if ($Lang -eq 1) { "ko" } else { "en" }) + "-$stamp")
New-Item -ItemType Directory -Force $dir | Out-Null
$log = Join-Path $out "editor-uishots-$stamp.log"

$regKey = "HKCU\Software\Unity\UnityEditor\DefaultCompany\Don't Hit Me"
$backup = Join-Path $out "prefs-backup-$stamp.reg"
$hadKey = $false
& reg query $regKey *> $null
if ($LASTEXITCODE -eq 0) { $hadKey = $true; & reg export $regKey $backup /y *> $null }

$args2 = @("-batchmode", "-projectPath", "`"$project`"", "-logFile", "`"$log`"", "-uishots", "`"$dir`"", "-uilang", $Lang)
"Unity 시작... (결과 폴더: $dir)"
$p = Start-Process -FilePath $UnityExe -ArgumentList $args2 -PassThru
if (-not $p.WaitForExit($MaxSec * 1000)) { "시간 초과: Unity 를 강제로 끕니다"; Stop-Process -Id $p.Id -Force }
$code = $p.ExitCode

& reg delete $regKey /f *> $null
if ($hadKey) { & reg import $backup *> $null }
"저장 데이터 복구 완료"
Get-ChildItem $dir | Select-Object Name, Length | Format-Table -AutoSize | Out-String
"Unity 종료 코드: $code (로그: $log)"
exit $code
