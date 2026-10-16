# Unity 를 열지 않고 게임 스크립트(Assets/Game/Scripts, Editor 제외)를 Unity 가 쓰는 Roslyn 컴파일러로 컴파일해 본다.
# 쓰는 법:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/check-compile.ps1
#           (Unity 설치 위치가 다르면)  ... -UnityData "C:\경로\6000.3.5f2\Editor\Data"
# 결과: 오류가 없으면 "컴파일 OK (N개 파일)" 와 종료 코드 0, 있으면 오류 목록과 종료 코드 1.
# 한계: 에디터 스크립트(Editor 폴더), 씬 직렬화, 런타임 동작은 확인하지 못한다.
#       Library/ScriptAssemblies 가 한 번은 만들어져 있어야 한다(Unity 로 한 번 열었던 프로젝트).
param([string]$UnityData = "C:\unityproject\6000.3.5f2\Editor\Data")

$project = Split-Path -Parent $PSScriptRoot
$out = Join-Path $env:TEMP "dont-hit-me-check"
New-Item -ItemType Directory -Force $out | Out-Null

$sep = [IO.Path]::DirectorySeparatorChar
$scriptsRoot = Join-Path $project "Assets${sep}Game${sep}Scripts"
$editorMarker = "${sep}Editor${sep}"
$files = @(Get-ChildItem $scriptsRoot -Recurse -Filter *.cs | Where-Object { $_.FullName.IndexOf($editorMarker) -lt 0 } | ForEach-Object { $_.FullName })

# 안전장치: 파일을 하나도 못 찾았으면 '통과'가 아니라 실패다
if ($files.Count -eq 0) { "실패: 컴파일할 .cs 파일을 찾지 못했습니다 ($scriptsRoot)"; exit 2 }
$csc = Join-Path $UnityData "DotNetSdkRoslyn${sep}csc.dll"
if (-not (Test-Path $csc)) { "실패: Unity 컴파일러를 찾지 못했습니다 ($csc). -UnityData 로 경로를 알려 주세요."; exit 2 }

$netstandardDll = Join-Path $UnityData "NetStandard${sep}ref${sep}2.1.0${sep}netstandard.dll"
$inputSystemDll = Join-Path $project "Library${sep}ScriptAssemblies${sep}Unity.InputSystem.dll"
$refs = @("-r:$netstandardDll", "-r:$inputSystemDll")
Get-ChildItem (Join-Path $UnityData "Managed${sep}UnityEngine") -Filter *.dll | ForEach-Object { $refs += "-r:" + $_.FullName }

$outDll = Join-Path $out "check.dll"
$cscArgs = @($csc, "-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9", "-warn:0", "-out:$outDll") + $refs + $files
$result = & dotnet @cscArgs 2>&1
if ($LASTEXITCODE -eq 0) { "컴파일 OK ($($files.Count)개 파일)"; exit 0 }
$result | Select-Object -First 60
exit 1
