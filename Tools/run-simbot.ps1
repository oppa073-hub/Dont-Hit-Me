# 자동 운전 시험 봇을 Unity 배치모드로 돌린다. Unity 에디터를 먼저 꺼야 한다(프로젝트가 잠겨 있으면 실행하지 못한다).
# 쓰는 법:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-simbot.ps1 -Days 8 -Skill 1
#   -Days N      며칠 치 영업을 돌릴지(기본 5)        -Skill 0|1|2   0 완벽 / 1 능숙 / 2 평범(기본 1)
#   -Cover       사건을 번갈아 강제로 일으키고 무적으로 돌린다(오류 찾기용)
#   -Endless     무한 모드로 돌린다                   -NoShop   번 코인을 쓰지 않는다
#   -UiAudit     글자 넘침/한국어 남음 점검(SimReports/audit-en.txt)을 같이 켠다
#   -NoGraphics  화면 그리기를 끄고 더 빨리 돌린다(프레임 시간에 렌더링이 빠짐)
#   -MaxSec N    실제 시간 상한(기본 1500초)
# 시험은 저장 데이터(PlayerPrefs)를 지우고 시작하므로, 실행 전에 레지스트리를 백업하고 끝나면 되돌린다.
# 결과: SimReports/simbot-*.txt (보고서), SimReports/editor-*.log (Unity 로그)
param(
    [int]$Days = 5, [int]$Skill = 1, [switch]$Cover, [switch]$Endless, [switch]$NoShop, [switch]$NoGraphics, [switch]$UiAudit, [switch]$Dbg, [switch]$Ending, [switch]$Full, [int]$StartDay = 1, [switch]$NoUnav, [string]$Set = "", [int]$DayType = -1,
    [int]$Seed = 1234, [int]$MaxSec = 1500,
    [string]$UnityExe = "C:\unityproject\6000.3.5f2\Editor\Unity.exe"
)

$project = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path $UnityExe)) { "실패: Unity 를 찾지 못했습니다 ($UnityExe). -UnityExe 로 알려 주세요."; exit 2 }

# 에디터가 이 프로젝트를 열고 있으면 실행하지 않는다
$lock = Join-Path $project "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $fs = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close() }
    catch { "실패: Unity 에디터가 이 프로젝트를 열고 있습니다. 에디터를 끄고 다시 실행하세요."; exit 2 }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $project "SimReports"
New-Item -ItemType Directory -Force $out | Out-Null
$report = Join-Path $out "simbot-$stamp.txt"
$log = Join-Path $out "editor-$stamp.log"

# PlayerPrefs 백업 (에디터는 레지스트리에 저장한다)
$regKey = "HKCU\Software\Unity\UnityEditor\DefaultCompany\Don't Hit Me"
$backup = Join-Path $out "prefs-backup-$stamp.reg"
$hadKey = $false
& reg query $regKey *> $null
if ($LASTEXITCODE -eq 0) { $hadKey = $true; & reg export $regKey $backup /y *> $null }

$args2 = @("-batchmode", "-projectPath", "`"$project`"", "-logFile", "`"$log`"", "-simbot", "-simdays", $Days, "-simskill", $Skill, "-simseed", $Seed, "-simmaxsec", $MaxSec, "-simout", "`"$report`"")
if ($Cover) { $args2 += "-simcover" }
if ($Endless) { $args2 += @("-simmode", "endless") }
if ($NoShop) { $args2 += "-simnoshop" }
if ($NoGraphics) { $args2 += "-nographics" }
if ($UiAudit) { $args2 += @("-uiaudit", "-uiaudit-dir", "`"$out`"") }      # 글자가 상자 밖으로 나오는지/영어 모드에 한국어가 남았는지 SimReports/audit-*.txt 에 쌓는다 (화면을 그려야 하니 -NoGraphics 와 같이 쓰지 말 것)
if ($Dbg) { $args2 += "-simdbg" }
if ($Ending) { $args2 += "-simending" }
if ($Full) { $args2 += "-simfull" }
if ($StartDay -gt 1) { $args2 += @("-simday", $StartDay) }
if ($NoUnav) { $args2 += "-simnounav" }
if ($Set -ne "") { $args2 += @("-simset", "`"$Set`"") }
if ($DayType -ge 0) { $args2 += @("-simdaytype", $DayType) }

"Unity 시작... (보고서: $report)"
$p = Start-Process -FilePath $UnityExe -ArgumentList $args2 -PassThru
$limit = $MaxSec + 300
if (-not $p.WaitForExit($limit * 1000)) { "시간 초과: Unity 를 강제로 끕니다"; Stop-Process -Id $p.Id -Force }
$code = $p.ExitCode

# PlayerPrefs 복구 (시험이 만든 값은 지운다)
& reg delete $regKey /f *> $null
if ($hadKey) { & reg import $backup *> $null }
"저장 데이터 복구 완료 (원래 있던 키: $hadKey)"

if (Test-Path $report) { "보고서: $report" } else { "보고서가 만들어지지 않았습니다. 로그를 확인하세요: $log" }
"Unity 종료 코드: $code"
exit $code
