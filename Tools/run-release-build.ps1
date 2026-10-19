# 출시용(itch.io 등) 빌드를 만든다. Unity 에디터를 먼저 꺼야 한다. 쓰는 법:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-release-build.ps1
# 결과 폴더는 저장소 밖(기본 D:\Unity\project\_Builds\DontHitMe-release). 구매한 에셋이 들어 있어 절대 커밋/푸시하지 않는다.
param([string]$BuildDir = "D:\Unity\project\_Builds\DontHitMe-release", [string]$UnityExe = "C:\unityproject\6000.3.5f2\Editor\Unity.exe", [int]$MaxSec = 1500)
$project = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path $UnityExe)) { "실패: Unity 를 찾지 못했습니다 ($UnityExe)"; exit 2 }
$lock = Join-Path $project "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $fs = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close() }
    catch { "실패: Unity 에디터가 이 프로젝트를 열고 있습니다. 에디터를 끄고 다시 실행하세요."; exit 2 }
}
$out = Join-Path $project "SimReports"
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out ("build-release-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")
$a = @("-batchmode", "-quit", "-projectPath", "`"$project`"", "-logFile", "`"$log`"", "-executeMethod", "BuildTest.BuildRelease", "-buildDir", "`"$BuildDir`"")
"출시 빌드 시작... (폴더: $BuildDir)"
$p = Start-Process -FilePath $UnityExe -ArgumentList $a -PassThru
if (-not $p.WaitForExit($MaxSec * 1000)) { "시간 초과: Unity 를 강제로 끕니다"; Stop-Process -Id $p.Id -Force }
"종료 코드: $($p.ExitCode)  (로그: $log)"
Select-String -Path $log -Pattern "\[BuildTest\]" | ForEach-Object { $_.Line }
# itch.io 에 올릴 때 필요 없는 디버그 정보 폴더는 지운다
Get-ChildItem $BuildDir -Directory -Filter "*BurstDebugInformation_DoNotShip" -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Recurse -Force; "지움: " + $_.Name }
exit $p.ExitCode
