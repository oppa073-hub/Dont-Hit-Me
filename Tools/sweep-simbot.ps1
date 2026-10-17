# 설정 값을 바꿔 가며 봇을 여러 번 돌려 SUMMARY 한 줄씩 모은다 (밸런스 비교용). Unity 에디터는 꺼져 있어야 한다.
# 쓰는 법:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/sweep-simbot.ps1 -Configs "기준=;쉬움=maxCrossers=3,cruiseCap=24" -Days 10 -Skill 1
#   -Configs  "이름=설정;이름=설정"  (설정은 run-simbot 의 -Set 과 같다: 필드=값,필드=값. 비우면 현재 값 그대로)  구분은 | 로: "a=x=1|b=y=2"
param([string]$Configs, [int]$Days = 10, [int]$Skill = 1, [int]$DayType = 0, [int]$Seed = 1234, [switch]$Shop)
$here = $PSScriptRoot
$project = Split-Path -Parent $here
$out = Join-Path $project "SimReports"
$result = Join-Path $out ("sweep-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".txt")
foreach ($c in ($Configs -split '\|')) {
    $i = $c.IndexOf('=')
    $label = if ($i -ge 0) { $c.Substring(0, $i) } else { $c }
    $set = if ($i -ge 0) { $c.Substring($i + 1) } else { "" }
    $args2 = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $here "run-simbot.ps1"), "-Days", $Days, "-Skill", $Skill, "-Seed", $Seed, "-NoGraphics", "-DayType", $DayType, "-MaxSec", 1500)
    if (-not $Shop) { $args2 += "-NoShop" }
    if ($set -ne "") { $args2 += @("-Set", $set) }
    $before = Get-ChildItem (Join-Path $out "simbot-*.txt") -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
    & powershell @args2 | Out-Null
    $new = Get-ChildItem (Join-Path $out "simbot-*.txt") | Where-Object { $before -notcontains $_.Name } | Sort-Object LastWriteTime | Select-Object -Last 1
    $line = if ($new) { (Select-String -Path $new.FullName -Pattern "^SUMMARY" | Select-Object -Last 1).Line } else { "SUMMARY 실패" }
    $text = "[$label] $line"
    $text
    Add-Content -Path $result -Value $text -Encoding UTF8
}
"결과 파일: $result"
