## 한 줄 소개
운전석 1인칭 시점의 PC 아케이드 게임. 둥글게 휜 도시 도로에서 무단횡단자를 조향으로 피한다.
혼자 한 달 안에 소규모로 완성하는 것이 목표. 프로토타입은 끝났고, 지금은 코드 구조 정리와 제작 단계.

## 작업 규칙
- 코드를 짜기 전에 계획부터 설명하고 확인받을 것.
- 프로토타입이라 구조보다 속도 우선. 버릴 코드로 취급. 한 번에 하나의 질문만 검증.
- 튜닝 변수는 public 필드로 노출해서 인스펙터에서 바로 조절 가능하게 할 것.
- 확인하지 않은 것을 확인했다고 말하지 말 것. Unity 에디터를 직접 실행할 수 없으므로 컴파일/플레이 결과는 사용자가 알려 줌.
- 컴파일은 `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/check-compile.ps1` 로 Unity 없이도 확인할 수 있다(Unity 6 의 Roslyn 사용, Editor 폴더 제외). 코드를 바꾼 뒤에는 이걸 먼저 돌려 "컴파일 OK"가 나온 것만 커밋한다. 단 플레이 동작, 씬 직렬화, 에디터 스크립트는 확인하지 못하므로 그 부분은 여전히 사용자가 알려 줌.
- 삭제, 푸시, 저장소 생성 같은 되돌리기 어렵거나 외부로 나가는 작업은 실행 전에 확인받을 것.
- 기능 작업이 끝나면 qa-tester로 확인한다.
- 외부 에셋은 전부 Assets/_External/ 안에 둔다(.gitignore로 제외됨, 업로드 금지).

## 게임 규칙
- 차는 제자리, 월드가 다가온다. 도로는 앞쪽으로 갈수록 아래로 휜다(y = -curvature * z^2, 동물의 숲 방식).
- 조작: A/D 또는 ←/→ 조향, S/↓ 브레이크(완전 정지), Q/E 좌/우 깜빡이, Space 손님 태우기, X 거절, Esc/P 일시정지, R 다시 시작, U 업그레이드 상점(타이틀/게임오버에서, 숫자키로 구매), O 설정(타이틀/일시정지에서). 시작은 타이틀 화면에서 Space/Enter. 반응형(음악 리듬 없음).
- 택시: 인도의 손님(느낌표)에게 차를 붙여 정차하면 옆 시점 대화 후 탑승, 목적지 근처에서 정차하면 요금 정산. 태운 채 사람을 치면 손님 리액션과 요금 감액.
- 교차로: 80m마다 교차로. 깜빡이를 미리 켜 두면 교차로 22m 전부터 자동 감속하고, 교차로 중심(십자가 중심)에 도착했을 때 그 자리에서 약 1.1초 동안 월드가 90도 회전(진입 전에는 절대 돌지 않음. 회전 중간에 새 도로 구간으로 교체, 건물 배치가 바뀜). 도시는 격자 좌표(gx, gy)와 방향(heading)으로 추적.
- 보행자는 2D 스프라이트 빌보드. 양쪽 인도에 사람이 많이 걷고, 일부가 도로로 나온다(멀리서 이미 건너는 사람 / 가까이서 갑자기 튀어나오는 사람, 튀어나오기 전 연석에서 잠깐 멈칫). 도로를 건너는 사람과 충돌하면 목숨 -1(3개), 아슬아슬하게 피하면 니어미스 보너스. 인도만 걷는 사람은 충돌/점수 없음.
- 차 내부: 3D 핸들 + 3D 팔(arms.fbx)을 코드 IK로 핸들에 올림, 손가락은 접선축으로 굽혀 쥠. 왼팔은 항상 보여야 함.
- 도시: 상자 건물 몸통에 타일 스프라이트를 붙임. 건물 스프라이트는 월드에 고정(사람처럼 카메라를 쳐다보지 않음).

## 구조
- Assets/Game/Scripts/DriveGame.cs : 게임의 뼈대(인스펙터 설정 값 public 필드 전부, 공통 상수/상태, Awake/Update/LateUpdate/ResetGame). 클래스 `DriveGame`은 partial 이고 아래 파일들에 역할별로 나뉨(MonoBehaviour 는 이 하나, 씬에 붙은 것도 이것)
  - World/DriveGame.WorldBuild.cs : 타일/배경/텍스처에서 스프라이트·재질·도로/건물 오브젝트 만들기
  - World/DriveGame.WorldView.cs : 도로 구간/건물을 매 프레임 배치, 회전 연출, 건물 해시(월드 좌표 기준)
  - Gameplay/DriveGame.Intersection.cs : 교차로 연결부(IntersectionManager 상태를 blinker/heading/gx/gy/turning 등 읽기 전용 속성으로 보여 주고, 회전이 끝나면 차·사람·구간을 정리)
  - Gameplay/DriveGame.Guidance.cs : 목적지를 앞/옆 칸수로 바꾸고 안내 문장 만들기
  - Gameplay/DriveGame.Taxi.cs : 택시 연결부(TaxiManager 이벤트를 받아 차 세우기/시점/점수/팝업에 반영)
  - Gameplay/DriveGame.Traffic.cs : 난이도/교통 연결부(레벨 적용, 차 충돌/니어미스 결과를 속도·목숨·점수·팝업에 반영). World/DriveGame.TrafficBuild.cs : 교통 임시 스프라이트 그리기
  - Gameplay/DriveGame.Meta.cs : 성장 연결부(업그레이드를 목숨/무적/택시에 적용, 판이 끝나면 코인 지급, 목표 판정, 상점 키 입력). UI/DriveGame.Shop.cs : 상점 화면과 오늘의 목표 표시
  - Gameplay/DriveGame.Ambience.cs : 분위기 연결부(날씨를 하늘/조명/도시 그림에 칠함, 튜토리얼 진행, 설정 입력/저장). UI/DriveGame.Overlays.cs : 밤/비 막, 헤드라이트, 빗줄기, 와이퍼, 설정 화면, 튜토리얼 글자 그리기
  - Gameplay/DriveGame.Pedestrians.cs : 보행자 연결부(치임/니어미스 결과를 목숨·점수·손님 리액션에 반영)
  - Cabin/DriveGame.Cabin.cs : 3D 운전석 부품과 배치(LayoutCabin)
  - Cabin/DriveGame.Navigator.cs : 대시보드 위 내비 화면
  - Cabin/DriveGame.Arms.cs : 3D 팔 IK, 손가락
  - Gameplay/DriveGame.Shift.cs : 하루 영업 모드(기본). shiftSeconds 동안 일하고 시간이 끝나면 새 손님을 막고, 태운 손님만 마저 모신 뒤 결산(요금 합계 - 사고 수리비, 평점 0~3). Tab(타이틀)으로 무한 모드와 전환, 모드별 최고 점수 따로
  - Gameplay/DriveGame.Flow.cs : 게임 흐름(타이틀 → 플레이 ↔ 일시정지), 게임오버 때 결과 글자와 최고 점수(PlayerPrefs "DontHitMe.Best") 저장
  - UI/DriveGame.Screens.cs : 타이틀/일시정지/게임오버 결과 화면(IMGUI)
  - UI/DriveGame.Gui.cs : IMGUI(HUD, 앞유리, 룸미러, 택시 UI)
- **매니저(일반 C# 클래스, MonoBehaviour 아님. 설정 값은 씬에 저장된 DriveGame public 필드를 읽음)**
  - Gameplay/PedestrianManager.cs : 보행자 목록/생성 시점/이동/횡단/충돌·니어미스 판정. 이벤트 Hit, NearMiss 로 결과를 알림
  - Gameplay/TaxiManager.cs : 손님 호출→대화→탑승→도착 흐름, 목적지, 요금·팁·제한 시간. 이벤트 PopupRequested, TalkStarted, Arrived. 전역 enum TaxiState 도 여기
  - Gameplay/IntersectionManager.cs : 깜빡이, 교차로 회전(시간 기준 90도), 격자 좌표(gx, gy)/방향(heading) 추적, NavState. 이벤트 TurnSwapped, TurnCompleted(새 dist), PopupRequested
  - Gameplay/GameSession.cs : 목숨, 점수, 니어미스/충돌 횟수, 무적 시간, 피격 붉은 화면, 게임오버 여부. DriveGame.Session.cs 가 lives/score/gameOver 등을 읽기 전용 속성으로 보여 줌
  - Gameplay/AudioManager.cs : 배경음악(인스펙터 Bgm Clip 에 넣으면 그 곡, 비우면 코드로 만든 임시 곡)과 빗소리도 여기. 외부 에셋 없이 코드로 합성한 효과음(엔진 소리는 속도에 따라 높낮이, 깜빡이 틱톡, 충돌, 니어미스, 도착 종소리, 손님 등장 차임). DriveGame.Sound.cs 가 연결. public: soundOn, masterVolume, engineVolume. **소리는 아직 귀로 확인 전**
  - Gameplay/DifficultyManager.cs : 플레이 시간에 따른 레벨(levelSeconds 마다 +1, 최대 maxLevel)과 레벨별 배율(보행자 간격, 튀어나오는 비율/속도). 이벤트 LevelChanged. DriveGame.Traffic.cs 가 보행자/교통에 적용하고 HUD 에 LV 표시
  - Gameplay/TrafficManager.cs : 다른 차(승용차/트럭/고장 난 차)와 돌발 장애물(공사 콘, 굴러오는 공+뒤따라 튀어나오는 아이) 생성/이동/충돌/CLOSE PASS 판정. 이벤트 Hit, NearMiss. 차는 목숨 -1, 콘은 속도만 감소, 공은 무해(경고용). 스프라이트는 코드로 그린 임시 그림이고 인스펙터 carSprite/truckSprite/coneSprite/ballSprite 에 넣으면 그걸 쓴다
  - Gameplay/UpgradeManager.cs : 코인과 영구 업그레이드 5종(여분 목숨, 무적 시간, 팁, 요금, 제한 시간)을 PlayerPrefs 에 저장. 업그레이드를 늘리려면 Defs 표에 한 줄 + 효과 속성 추가
  - Gameplay/GoalManager.cs : 오늘의 목표 3개(날짜 시드라 하루 동안 고정, 달성 기록은 PlayerPrefs). 달성하면 이벤트 Completed 로 보상 코인을 알림
  - Gameplay/WeatherManager.cs : 하루 시계(dayLength 초에 24시간)와 비. 하늘색/조명/밤 막 색을 계산만 하고 칠하지는 않음. weatherOn 으로 끔
  - Gameplay/TutorialManager.cs : 처음 한 판 동안 단계별 안내(조향 → 손님 → 내비/깜빡이 → 정산). 끝나면 PlayerPrefs 에 기록, 설정에서 다시 켬
  - Gameplay/EventManager.cs : 날짜 시스템. 판(하루)마다 날 종류를 뽑음: 평범(첫날은 항상) / 사람 많은 날(보행자·손님 증가) / 괴수 침공 / 히어로와 빌런 전투. 괴수·히어로 날은 하루 중 무작위 시각에 "긴급 속보"와 함께 갑자기 시작. Day 번호는 PlayerPrefs 저장. 인스펙터 forceDayType 으로 날 고정, 플레이 중 F2=괴수, F3=히어로 즉시 시작
  - Gameplay/TrafficManager.Events.cs : 괴수(다음 교차로 뒤에서 도로 전체를 막음, 연기 기둥, 부딪히면 목숨 -1)와 히어로 레이저(경고 뒤 한쪽 차선을 쓸며 불길을 남김, 반대 차선으로 피함). 월드 연출은 World/DriveGame.Battle.cs, 연결은 Gameplay/DriveGame.Events.cs
  - Gameplay/DriveGame.Debug.cs : 디버그 패널(에디터/개발 빌드만). F1 로 열고 1 괴수 2 히어로 3 사람 많은 날 4 평범한 날 5 무작위 6 비 7 시각+3h 8 레벨+1 G 무적. 1~4 는 다음 판의 날 종류(forceDayType)도 고정
  - Gameplay/RageManager.cs : 분노 게이지(시간으로 rageFillSeconds 에 가득, 니어미스마다 rageNearMissGain 만큼 더 참). 가득 차면(손님을 태운 상태일 때만. 손님이 없으면 가득 찬 채로 대기하다가 태우는 순간) 자동 발동해 rageDuration 초 동안 무적(부딪혀도 목숨/요금/사고 기록 없음, 점수만)+rageSpeedMul 배 가속. 게이지는 팔(손) 색이 붉어지는 것으로 보여 줌. 연결은 Gameplay/DriveGame.Rage.cs
  - Gameplay/PassengerCatalog.cs : 손님 유형 표(PaxType, PaxTypes), 대사, 피부/머리색. 대사·요금 배율은 이 파일만 고치면 됨
  - 새 매니저를 만들 때: 클래스는 DriveGame 설정(public 필드)을 생성자로 받고, 게임 결과(점수/목숨/팝업)는 이벤트로 알리며, 차 상태(속도, 위치)는 Tick 인자로 받는다.
- Assets/Game/Scripts/Editor/DriveSceneCreator.cs : 메뉴 Game → Create Drive Scene
- Assets/Game/Scenes/DriveScene.unity : 에셋 참조가 연결된 씬
- 씬이 스크립트를 GUID 로 참조하므로 DriveGame.cs 와 .meta 는 항상 함께 움직일 것. 외부 에셋은 Assets/_External/ 그대로.
- 에셋: GandalfHardcore City Tiles, arms.fbx, arm_albedo_*.png

## 기술 환경
- Unity 6 (6000.3.5f2), URP, 새 Input System. Active Input Handling은 Both 또는 Input System.
- PC 전용. 해상도는 16:9 기준.

## 진행 상황 (새 대화는 여기부터 읽을 것)
게임 방향: 단순 "보행자 피하기"에서 **택시 게임**(손님 태우기 + 교차로 회전 + 내비)으로 확장 중.
- 완료(화면 확인됨): 3D 팔/핸들 쥐기, 인도/무단횡단 보행자, 건물 원거리 LOD 최적화, 택시 1단계(손님 호출 `!` → 정차 → 옆 시점 대화 → 탑승 → 룸미러 얼굴 → 목적지 정차 시 요금 → 태운 채 사람 치면 손님 리액션).
- 완료(사용자가 화면 확인함): **교차로 + 깜빡이(Q/E) 회전**. 깜빡이를 미리 켜 두면 교차로 진입 전에는 도로가 돌지 않고(`Turn Slow Distance` 22m부터 감속), 차가 교차로 중심에 도착하면(`Turn Center Tolerance` 1.5m 안) 그 자리에서 `Turn Duration` 1.1초 동안 월드가 중심 둘레로 0~90도 회전, 38도에서 짧게 어두워지며 새 도로 구간으로 교체, 끝나면 좌표 재설정. 회전 진행은 거리가 아니라 시간 기준.
- 격자 좌표(gx, gy)와 방향(heading)은 추적 중이지만 아직 화면 HUD 텍스트로만 쓰임.

## 다음 할 일 (순서대로, 한 단계에 질문 하나씩)
1. (거의 끝) 회전 마무리: 시간 값 public화와 qa 지적 수정은 끝남(커밋 606c1ae, 아직 푸시 안 함). 남은 것은 회전 중 화면이 왼쪽으로 쏠리는지 화면 확인뿐.
2. **2단계 미니맵/내비** — 설계 검토(design-reviewer) 끝남. **2a 코드 작성 완료(아직 Unity에서 화면 확인 전, 커밋 전)**. 처음부터 미니맵을 넣지 않고 2a → 2b로 쪼갠다.
   - 2a 구현 메모: `DestRel(f,r,sd)` 한 곳에서 목적지를 계산(회전 중엔 회전 후 상태 기준), `NavHint`가 안내 글자, 목적지는 `destX/destY`, 트립 거리는 맨해튼 칸수×교차로 간격. `skipCross`/`destDist`/`tripDistance` 제거, CompleteTurn은 차를 중심 +0.01m에 놓음. 새 public: `destMaxAhead`(3), `destMaxSide`(2), `arriveRadius`(9m).
   - 2a qa 지적 수정 완료: CompleteTurn에서 `nextInterDist` 즉시 갱신(거짓 "지나쳤어요" 방지), `NavState` 헬퍼, `arriveRadius` 하한 1m, 도착 반경 안 안내("브레이크로 정차하세요"), 상단 정보줄 GC 캐시.
   - **2b 미니맵 → 3D 운전석 + 대시보드 내비로 변경(코드 작성 완료, 화면 확인 전)**: 사용자 요청으로 "실제 운전석에서 앞유리 너머로 보는 느낌". 2D 지붕/필러/2D 미니맵을 없애고 카메라 자식(viewRoot)에 3D 천장, 기울어진 A필러, 대시보드(+앞유리 끝선), 계기판 후드(속도 TextMesh), 대시보드 위 내비(받침대+베젤+화면)를 만듦. 내비 지도는 화면 안의 쿼드 풀(`UpdateNav`: 진행 방향 위쪽 격자, 화살촉, 목적지 마름모, L자 안내선, 하단 "목적지 남은 m"). 앞유리 위쪽 선팅/반사 줄은 IMGUI. `LayoutCabin`이 매 프레임 public 값(`dashTopY`, `hoodTopY`, `roofY`, `navPos`, `navSize`, `miniMapRange`, `pillarL/RBase/Top`, `pillarThickness`)을 반영하므로 플레이 중 인스펙터로 맞춤.
   - **화면 확인할 것**: (0) 운전석 부품이 화면 어디에 보이는지, 내비가 핸들·오른팔과 겹치지 않는지, 앞 도로/보행자가 충분히 보이는지, 3D 글자(속도/방향)가 보이는지, 진행 방향이 위쪽이고 회전 때 맞게 도는지 (1) 직진으로 교차로 3개 지나면 HUD gy=3 (2) 우회전 후 방향 "동" (3) 목적지 기둥 위치/안내 글자가 맞는지, 안내대로 가면 도착하는지 (4) 지나치면 "지나쳤어요" 1회 + 돌아오는 안내 (5) 회전 직후 화면/기둥이 튀지 않는지(CompleteTurn +0.01 변경).
   - 시작 전 사용자 확인(선택): 직진으로 교차로 3개를 지나면 HUD gy가 3인지, 우회전하면 방향이 "동"인지(격자 추적이 맞는지가 전제).
   - **2a (격자 목적지 + 안내 글자)**:
     1. 목적지는 AcceptPassenger 에서만 정한다(BeginTalk 아님). 기준은 "다음 교차로 N = (gx,gy)+heading 방향". N 기준 앞 f'=0..3칸, 옆 r=-2..2칸에서 (0,0)만 제외하고 무작위.
     2. 도착: `!turning && speed<=pickupSpeed && |sd|<=arriveRadius`. sd = 목적지가 N이면 zC(=nextInterDist), 방금 지난 노드면 zC-period. `arriveRadius`는 public(기본 8~10m, 지금 14m는 교차로 입구 10m 전에서도 도착 처리되어 너무 넓음). `0<nextInterDist` 대신 `<=`.
     3. 지나침: 목적지가 지난 노드이고 `-sd>arriveRadius`이면 "지나쳤어요" + 요금 -20%를 트립당 1회(bool, AcceptPassenger 에서 리셋). 목적지는 다시 안 뽑고 옆길로 돌아 재접근.
     4. HUD 안내 글자: "앞 N칸, 오른쪽/왼쪽 M칸 — 다음 교차로에서 우회전". f,r 은 매 프레임 재계산(자동 재경로). f'≤0 이면 N에서 r 부호 쪽(r=0 이면 우회전), 문자열은 (f,r)이 바뀔 때만 새로 만든다(OnGUI GC).
     5. 요금/남은 거리/HUD(`tripMeters` 쓰는 BeginTalk, Arrive, HUD 3곳)를 모두 "맨해튼 칸수 × 80m"로 통일. 하나라도 남기면 표시와 요금이 어긋남.
     6. 목적지 기둥: `mz = 목적지==N ? zC : (목적지==지난노드 ? zC-period : 숨김)`. 회전 중 숨김은 유지.
     7. 회전 중(`turning`)에는 "끝난 뒤 상태"(노드 gx+HeadX, 방향 (heading+turnDir)%4)로 계산해 CompleteTurn 때 튀지 않게.
     8. CompleteTurn 의 `- 0.01f` 를 `+ 0.01f` 로 바꿔 차를 중심 직후에 놓으면 kNext 가 바로 다음 교차로가 되어 `skipCross` 가 필요 없고 "N=(gx,gy)+F" 가 항상 성립. 건물 장면 교체와 무관한지 화면 확인 필요(어려우면 skipCross 일 때 N=(gx,gy) 분기를 헬퍼 한 곳에만 둔다).
     9. 제거: `destDist`, `destDist = dist + 140`. ResetGame 에 새 필드(목적지 좌표, 지나침 플래그, 힌트 캐시) 초기화 추가.
   - **2b (미니맵 그리기)**: 2a 에서 안내가 맞다고 확인된 뒤. 핸들 옆 IMGUI 사각형, 진행 방향 위쪽, 내 위치 삼각형, 목적지, L자 안내선(주변 ±3칸 격자선). ~40줄 수준.
   - 하지 말 것: 일반 경로탐색, 3D 대시보드 미니맵, 지나침 때 목적지 재추첨, 이번 단계의 시간 제한/건물 이름-좌표 매핑, 미니맵 줌/스크롤.
3. 폴리시 (사용자 승인된 순서: 3-1 → 3-2 → 3-3, 한 단계씩 qa 후 커밋):
   - **3-1 손님 유형 다양화: 코드 작성 완료(화면 확인 전)**. `PaxTypes` 표(회사원/할머니/학생/아저씨/아주머니/VIP)에 이름·목적지·인사·리액션·감사 대사, `fareMul`(요금 배율), `hitSense`(사고 감액 민감도), `hair`, `weight`. `CurrentFare()`가 HUD와 정산의 요금을 한 곳에서 계산. 도착 시 3초간 감사 대사. 표는 코드 상단 static 배열이라 인스펙터가 아니라 코드에서 고침.
   - **3-1 커밋됨(d2a01f4, 푸시 전)**.
   - **3-2 요금/시간 제한: 코드 작성 + qa 반영 완료, 커밋됨(화면 확인 전)**. 탑승 시 `tripLimit = 거리 ÷ (그 순간 순항 속도 × limitSpeedRatio) × pax.timeMul + timeBuffer`(속도 반영: 씬의 baseSpeed가 20이라 고정 초는 너무 널널했음), 안에 도착하면 팁(`CurrentTip`, 최대 `tipMax`×유형 `tipMul`, HUD에도 예상 팁 표시), 넘기면 초당 `lateDropPerSec`씩 감액. 사고/지나침/지각 감액을 합쳐도 요금은 기본의 20% 밑으로 안 내려감. 늦게 도착하면 지각용 감사 대사. 시간 초과는 감액만(목숨 감소 없음). 밸런스는 플레이로 맞출 것.
   - **3-3 건물 일관성: 코드 작성 완료(화면 확인 전, 커밋 전)**. `citySalt` 제거. 건물 해시를 "두 교차로 사이 구간의 월드 좌표(양의 방향 기준으로 통일) + 구간 안 위치 + 좌우"로(`UpdateBuilding`의 `segKey`/`hidx`, `UpdateWorldVisuals`가 `rendNodeX/Y/K`, `rendHead` 설정). 반대 방향으로 지나면 위치와 좌우를 뒤집어 같은 건물이 나오게 함. 회전 후 새 도로는 K=RebaseBlocks. **화면 확인 필수**: (1) 한 바퀴 돌아 같은 길로 돌아오면 건물이 같은지 (2) 반대 방향에서 같은 건물이 반대쪽에 보이는지 (3) 회전 장면 교체/완료 때 건물이 튀지 않는지. 이상하면 이 커밋만 되돌릴 것(격자 좌표 추적이 틀려 있으면 건물이 어긋남).
- 설계 검토에서 정한 원칙: 일반 그래프 경로탐색 금지, 처음부터 3D 대시보드 미니맵 금지, 파일 분할은 partial class 로 완료(동작 변경 없이 나눔). 매니저 분리는 보행자, 택시, 교차로, 진행 기록(GameSession)까지 완료. 3D 운전석/내비/팔과 HUD(IMGUI)는 매니저가 아니라 화면 표시라서 partial 파일로 둠(필요해지면 View 클래스로 분리). 새 매니저는 한 번에 하나씩 분리하고 매 단계 Tools/check-compile.ps1 + qa 로 확인.

## 알려진 한계 / 확인 필요
- 3D 팔: arms.fbx의 socket 본(주먹 안 쥐는 점)에 림을 넣고, 손가락은 림에 닿을 때까지 굽힘. 사용자가 인스펙터에서 직접 맞춘 값이 씬(DriveScene.unity)에 저장되어 있음(Grip Roll, Thumb Up, Grip Nudge, Finger Pad, Hand Angle, Shoulder Center, Arm Length).
- 성능: 건물 원거리 LOD(`Lod Distance`)를 넣었지만 Stats(FPS/Batches/SetPass) 실측값은 아직 없음.
- Unity-MCP 패키지는 도메인 리로드를 멈추게 해서 제거함(다시 설치하면 재발 가능). `Assets/Plugins/NuGet`은 이그노어된 잔여물.
- 씬에 직렬화된 public 값이 코드 기본값보다 우선한다. 기본값을 바꿀 때는 DriveScene.unity의 해당 줄도 확인할 것.
- 이미 열려 있는 씬의 컴포넌트는 스크립트를 다시 컴파일해도 옛 값을 유지한다(씬 파일에 저장하지 않았어도). 기본값을 바꿔도 안 바뀌어 보이면 필드 이름을 바꾸거나 인스펙터에서 값을 직접 고칠 것(`turnStartDistance` → `turnCenterTolerance`로 바꾼 이유).

## 작업 방식 메모
- 코드 작성 전 계획 확인은 한 번만. 계획이 승인되면 그 안의 수정은 묻지 않고 진행(되돌리기 어려운 것만 확인).
- 커밋 제목은 한국어. 에셋은 Assets/_External/ 에만.
- 기능 작업 후 qa-tester(정적 점검), 큰 설계는 구현 전에 design-reviewer로 검토. 에이전트 추가는 먼저 제안.
- 사용자는 개발 입문에 가깝고 한국어를 쓰므로 쉽게 설명한다.

## 에셋 라이선스
- GandalfHardcore City Tiles: 상업 사용과 수정 허용, 재판매·재배포 금지. 저장소는 Private 유지.

## Git
- 원격: https://github.com/oppa073-hub/Dont-Hit-Me.git
- 저장소는 Private. 푸시 전 확인받을 것.
