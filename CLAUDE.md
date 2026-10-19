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
- 조작: A/D 또는 ←/→ 조향, S/↓ 브레이크(완전 정지), Q/E 좌/우 깜빡이, Space 손님 태우기, X 거절, Esc/P 일시정지, R 다시 시작, 상점은 로비의 정비사에게 말을 걸면 열림(숫자키로 구매), 게임오버에서 U = 로비(일시정지 화면에서도 U 를 두 번 누르면 지금까지의 영업을 정산하고 로비로: pauseLobbyArmed), O 설정(타이틀/로비/일시정지에서). 시작은 타이틀 화면에서 Space/Enter. 반응형(음악 리듬 없음).
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
  - Gameplay/DriveGame.Ambience.cs : 분위기 연결부(날씨를 하늘/조명/도시 그림에 칠함, 튜토리얼 진행, 설정 입력/저장). UI/DriveGame.Overlays.cs : 밤/비 막, 헤드라이트, 빗줄기, 와이퍼, 설정 화면, 튜토리얼 글자 그리기 **밤 조명**: 차(카메라)에 진짜 스포트라이트(headLight)와 운전석용 작은 점광(cabinLight)이 붙고, 빛을 못 받는 스프라이트(사람, 차, 건물 그림)는 NightLight(x,z)(스포트와 같은 원뿔+거리 감쇠)로 색을 어둡게 칠함(WorldView 의 Lit/TintBuilding). 정전도 같은 조명을 씀(NightK). 인스펙터 Night 헤더(headlightRange/Angle/Intensity, nightMinLight). 가로등 소품, 불길, 폭발, 괴수, 구급차는 안 어둡게
  - Gameplay/DriveGame.Pedestrians.cs : 보행자 연결부(치임/니어미스 결과를 목숨·점수·손님 리액션에 반영)
  - Cabin/DriveGame.Cabin.cs : 3D 운전석 부품과 배치(LayoutCabin)
  - Cabin/DriveGame.Navigator.cs : 대시보드 위 내비 화면
  - Cabin/DriveGame.Arms.cs : 3D 팔 IK, 손가락
  - Gameplay/DriveGame.Shift.cs : 하루 영업 모드(기본). shiftSeconds 동안 일하고 시간이 끝나면 새 손님을 막고, 태운 손님만 마저 모신 뒤 결산(요금 합계 - 사고 수리비, 평점 0~3). Tab(타이틀)으로 무한 모드와 전환, 모드별 최고 점수 따로
  - Gameplay/DriveGame.Flow.cs : 게임 흐름(타이틀 → 플레이 ↔ 일시정지), 게임오버 때 결과 글자와 최고 점수(PlayerPrefs "DontHitMe.Best") 저장
  - UI/DriveGame.Screens.cs : 타이틀/일시정지/게임오버 결과 화면(IMGUI)
  - UI/DriveGame.Blood.cs : 사람을 칠 때 앞유리에 튀는 피 얼룩(도형). 5초 안에 연속으로 치면 얼룩이 더 많고 커지고 화면 가장자리가 붉어짐, 안 치면 흘러내리며 3초쯤에 지워짐. 분노/무적 중에도 튐
  - UI/DriveGame.Gui.cs : IMGUI(HUD, 앞유리, 룸미러, 택시 UI)
- **매니저(일반 C# 클래스, MonoBehaviour 아님. 설정 값은 씬에 저장된 DriveGame public 필드를 읽음)**
  - Gameplay/PedestrianManager.cs : 보행자 목록/생성 시점/이동/횡단/충돌·니어미스 판정. 이벤트 Hit, NearMiss 로 결과를 알림
  - Gameplay/TaxiManager.cs : 손님 호출→대화→탑승→도착 흐름, 목적지, 요금·팁·제한 시간. 이벤트 PopupRequested, TalkStarted, Arrived. 전역 enum TaxiState 도 여기
  - Gameplay/IntersectionManager.cs : 깜빡이, 교차로 회전(시간 기준 90도), 격자 좌표(gx, gy)/방향(heading) 추적, NavState. 이벤트 TurnSwapped, TurnCompleted(새 dist), PopupRequested
  - 난이도 완화(Balance 헤더): 씬에 Max Speed 40 이 저장돼 있어서 cruiseCap(28)으로 먼저 제한. 튀어나오는/멀리서 건너는 사람은 차 도착 몇 초 전(dartLeadTime/farLeadTime)에 건너기 시작해 속도가 빨라도 보일 시간이 같고, 동시에 건너는 사람은 maxCrossers 로 제한. 분노 폭발 중에는 peds.RageSurge 로 건너는 사람이 rageSurgeInterval 마다 몰려듦(보상)
  - 체력 회복: GameSession.TickHeal. 사고 없이 healSeconds(기본 50초) 달리면 하트 +1(최대치까지), 사고가 나면(RegisterHit) 처음부터 다시. 하트 아래 초록 막대로 진행도 표시. 업그레이드 '자가 수리'(6번)로 최대 40%까지 빨라짐
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
  - 좀비의 날(DayType.Zombie): 괴수/히어로처럼 하루 중 갑자기 시작해 zombieDuration(45초) 동안. PedestrianManager.ZombieMode 동안 새 보행자가 좀비(좀비 전용 그림, 느리고 비틀거림, Ped.zombie)나 생존자(평범한 시민 그림, Ped.survivor)가 되고 (색 구분이나 하늘의 초록 막은 일부러 없앰: 플레이어가 모습으로 알아본다. 뉴스/속보 문구에도 "초록/주황"을 쓰지 말 것. 그림이 없는 도형 모드에서만 초록/주황으로 구분) 좀비가 도로로 추가로 몰려나옴(zombieInterval). 좀비를 치면 사고가 아니라 보상(점수 zombieScore, 분노 게이지 zombieRageGain, 초록 얼룩, 철퍽 소리, 손님 환호, 니어미스 없음), 생존자를 치면 평소 사고. 미니건은 생존자를 쏘지 않음. 사태가 끝나면 남은 좀비는 평범한 시민이 됨. 튜닝은 인스펙터 Zombie 헤더, 연결은 DriveGame.Pedestrians.cs(OnZombieHit)/Events.cs. 디버그 0 키. 뉴스 한 벌은 NewsCatalog.Kits 의 Zombie 항목
  - Gameplay/DriveGame.Heat.cs : 도시 위험도 0~5단계(Day 6/12/18/26/36 부터, 인스펙터 Heat 헤더 heatDays). 단계마다 판 시작 레벨 상승(DifficultyManager.Heat), 동시 횡단 인원 +1(2단계마다), 랜덤 인카운트 간격 단축(EncounterManager.IntervalMul), 사건 있는 날 확률 증가(EventManager.Heat), 대신 요금 위험수당(+6%/단계). 로비와 아침 뉴스에 표시. 후반에 업그레이드로 쉬워지는 것을 상쇄하려는 장치
  - 외부 픽셀 아트 적용(World/PixelArt.cs + Tools/build-pixel-atlas.py): 산 에셋(Modern NPCs, Zombies, Pixel Art Enemies(외계인/UFO), Farm Animals(돼지), 지옥의 거인)을 파이썬이 잘라(Aseprite 태그를 보고 줄/칸을 정함) Assets/_External/_Generated/Resources/PixelAtlas.png + PixelAtlasData.json 한 장으로 만들고, 게임은 Resources 로 읽어 PixelArt.Random/Get(종류)로 애니메이션 세트(idle/walk/death)를 받음. 아틀라스는 _External 이라 git 에 안 올라가니 새 컴퓨터에서는 `python Tools/build-pixel-atlas.py` 를 먼저 돌릴 것. 파일이 없으면 예전 도형 그림으로 돌아감. 사람/좀비/외계인 그림은 왼쪽을 보고(PixelSet.Flip 이 뒤집음) 발이 기준점(ppu 는 몸 크기로 1.75m 에 맞춤). 쓰이는 곳: 보행자(Ped.art, 좀비/생존자 포함), 외계인 손님(대화 시작 때), 돼지(Obj.art), 경찰 바리케이드(옆으로 선 경찰차), 괴수(지옥 팩의 가마솥 악마 The Devil), 외계인 목적지 UFO(WorldView.ShowUfo). 새 캐릭터는 build_sets() 에 한 줄 추가. 대화창/룸미러/엔딩/로비 사장 대화의 얼굴은 각 그림의 서 있는 첫 칸에서 머리만 잘라 쓴다(build-pixel-atlas.py head_rect 가 JSON "head" 로 기록 -> PixelSet.Face, DriveGame.Gui.cs DrawArtFace, 손님 그림은 TaxiManager.PaxArt). 얼굴 자르는 비율을 바꾸면 스크립트를 다시 돌릴 것. 길가에서 기다리는 손님은 걷는 사람+서 있는 "stand" 그림(handbag, tablet, military, coffee girl, bodybuilder, flight attendant) 중 아무나, 외계인 손님은 처음부터 외계인, 은행 강도는 처음부터 Robber 그림(손님 유형은 나타날 때 미리 정함: TaxiManager.RollPax/GiveCallerArt). 이름은 그림의 성별에 맞는 영어 이름을 한글로(PassengerCatalog.NameFor, 배경은 뉴욕). 교차로 구간에서는 손님이 부르지 않게 위치를 옮김(TaxiManager.ClearOfIntersection). 새 그림은 발이 기준이라 인도 위에서는 SidewalkLift 만큼 올림, 로비의 사장(택시 회사 사장, Mafia Boss, "boss")과 플레이어(Japanese guy)도 스프라이트(DrawLobbySprite, 상점 초상). 쓰지 말 것: Blonde Glamour Model(원본/altered 둘 다). 아직 안 쓴 것: 소/닭, 지옥 팩의 나머지(히어로/빌런), 자동차(옆모습이라 같은 방향 교통에는 못 씀), 나머지 NPC
  - 3D 로우폴리 건물(기본 사용. World/CityKit.cs, WorldView.UpdateBuilding3D): Kenney City Kit Commercial(CC0)의 FBX 19개와 colormap.png 를 Assets/Game/Resources/CityKit 에 두고(CC0 라 저장소에 포함, 크레딧 Kenney. 원본 폴더 Assets/_External/도시맵) Resources.LoadAll 로 읽음. 길가 소품(가로등/소화전/쓰레기통/우체통)은 코드로 만든 3D 도형(UpdateProp3D), 가로등은 밤에 불빛 번짐. 세 구간(12m) 묶음마다 가운데 구간에 모델 하나를 세운다(모델 폭에 맞춰 6~12배 크기, 정면 +z 가 도로를 향하게 회전, 풀링). 인스펙터 City3D 헤더 use3DBuildings 로 켜고 끔(끄면 예전 스프라이트 건물로 복귀), cityKitFlip 으로 앞뒤 방향 뒤집기. 인도/건물 레이어라 헤드라이트를 안 받음. 건물만 3D 고 사람/차는 2D 스프라이트
  - 로비 업그레이드(UI/DriveGame.LobbyDraw.cs): 맵(건물/스카이라인/소품)은 기본이 예전 도형이고, 인스펙터 Lobby 헤더 lobbyCityTiles 를 켜면 도시 타일 버전(이하). 건물은 도시 타일 그림(winTiles/shopTiles/doorTiles/corniceTile)으로 쌓은 LTileBuilding(택시 회사 건물 "빅애플 택시" + 이웃 건물 3채, 차고 증축하면 라면 휴게실), 먼 스카이라인은 cityBackground 를 천천히 움직여 그림, 가로등/쓰레기통/우체통은 타일 스프라이트(DrawLobbyProps). 주인공 택시는 Retro car 그림(LTaxiSprite, 출발할 때 달리기 그림)에 택시 개조(황금 도색/미니건/날개)를 도형으로 얹음. 타일이나 아틀라스가 없으면 예전 도형 그림
  - Gameplay/DriveGame.Debug.cs : 디버그 패널(에디터/개발 빌드만, Del 두 번 = 저장 데이터(PlayerPrefs) 전부 초기화 후 장면 다시 불러오기). F1 로 열고 1 괴수 2 히어로 3 사람 많은 날 4 평범한 날 5 무작위 6 비 7 시각+3h 8 레벨+1 G 무적. 1~4 는 다음 판의 날 종류(forceDayType)도 고정
  - Gameplay/RageManager.cs : 분노 게이지(시간으로 rageFillSeconds 에 가득, 니어미스마다 rageNearMissGain 만큼 더 참). 가득 차면(손님을 태운 상태일 때만. 손님이 없으면 가득 찬 채로 대기하다가 태우는 순간) 자동 발동해 rageDuration 초 동안 무적(부딪혀도 목숨/요금/사고 기록 없음, 점수만)+rageSpeedMul 배 가속. 게이지는 팔(손) 색이 붉어지는 것으로 보여 줌. 연결은 Gameplay/DriveGame.Rage.cs
  - 로비의 사람들(DriveGame.Lobby.cs LobbyNpcs 표, 대사 StoryCatalog.LobbyChat): 사장(0, 상점), 제시카 비서(Lady with handbag), 세차 알바(Dealer 그림, 맨 왼쪽에서 사장님 하얀 고급차를 닦음. 말을 걸면 세차 중이라고 투덜거림), 동료 택시 기사 3명. 각자 말하는 얼굴/이름/말소리가 따로(lobbySpeaker*). 동료 택시는 승강장처럼 한 줄로 나란히(DrawLobbyStreet, 맨 앞이 주인공 택시). 도형 로비 건물은 시차 중간 건물+창문 격자+상점가+가로수(DrawLobbyMidCity/DrawLobbyShops). **쓰지 말 것: Car wash altered sheet(노출이 심한 그림이라 안 씀. 세차는 하얀 고급차+거품 코드 연출)**
  - Gameplay/LobbyManager.cs + Gameplay/DriveGame.Lobby.cs + UI/DriveGame.LobbyDraw.cs : 로비(2D 횡스크롤, 건물은 도형, 사람은 픽셀 아트). 타이틀에서 Space → 로비 → 택시 회사 사장(토니 사장) 옆 Space = 대사 후 상점 / 택시 옆 Space = 화면이 어두워지며 영업 시작. 게임오버에서 U = 로비, R = 바로 재시작. 흐름 enum GameFlow.Lobby
  - Gameplay/StoryCatalog.cs : 정비사 대사(첫 방문 소개, 어제 날 종류에 따른 인사, 분노 힌트, 상점 대사)와 아침 뉴스 원고. 스토리 대사는 이 파일만 고치면 됨
  - Cabin/DriveGame.News.cs + Gameplay/NewsCatalog.cs : 내비 뉴스. 토막(NewsBeat: 앵커/현장 연결/광고/긴급 속보)의 연속으로, 영업 시작 때 아침 뉴스, 괴수/히어로 사건이 시작되면 손님을 찾는 중에만 긴급 속보가 끼어듦. 날 종류별 "뉴스 한 벌"(배경 그림, 머리 띠 문구/점멸, 앵커 연기, 원고 풀, 자막)은 NewsCatalog.Kits 표에 있음. **새 날 종류(이벤트)를 만들면 NewsCatalog.Kits 에 한 줄을 추가하고, 배경이 새로 필요하면 DriveGame.News.cs 의 DrawNewsBg switch 에 한 가지를 더 그릴 것.** N 키로 건너뛰기, 손님을 태우면 지도로 복귀
  - Gameplay/ProjectManager.cs : 택시 개조 4단계(정비소 증축=업그레이드 20% 할인 / 황금 도색=분노 1.5배 오래+요금 / 지붕 양옆 미니건 / 하늘을 나는 택시=엔딩). 상점에서 Tab 으로 쪽을 넘겨 숫자키로 구매, 앞 단계를 먼저 끝내야 함, 끝내면 로비 모습이 바뀌고 정비사 병맛 대사
  - Gameplay/MinigunManager.cs + Cabin/DriveGame.Minigun.cs : 미니건 개조 후 분노 폭발 때 운전석 양옆으로 미니건이 튀어나와 돌며 앞(3~55m)의 보행자/차/콘/공/불길을 초당 7개 쓸어버림. 괴수, 히어로, 길가의 손님은 쏘지 않음. 처치마다 점수, 분노가 끝나면 처치 수 팝업
  - UI/DriveGame.Ending.cs : 엔딩. 하늘을 나는 택시 개조를 산 뒤 처음 태우는 일반 손님(은행 강도 제외)이 탑승하는 순간 시작(flow = GameFlow.Ending, PlayerPrefs "DontHitMe.EndingSeen" 로 한 번만). 택시 뒷모습 시점으로 날개가 펴지고 로켓처럼 치솟으며 카메라가 멀어지고, 하늘이 어두워지며 별/달이 나오고 달에 도착. 손님이 내내 소리 지르며 애원(대사 StoryCatalog.EndingTimes/EndingTexts). 모두 도형으로 그린 임시 그림, 타이밍은 인스펙터 Ending 헤더(endingLaunchAt/endingLandAt/endingShake). 끝나면 결산하고 로비로. 디버그 F4 로 바로 보기, Esc 로 6초 뒤부터 건너뛰기. 봇 -Ending 옵션으로 흐름 시험(화면은 못 봄)
  - 소리(외부 에셋): 음악/효과음/목소리/자동차 그림은 전부 Assets/_External/_Generated/Resources/{Audio,Cars} 에서 Resources 로 읽음(저장소에 안 올라감, 없으면 코드로 합성한 소리/도형으로 대체). Audio/bgm_{lobby,day,dusk,night,rage}.wav 와 stinger_{win,lose}.wav(하루 결산/게임오버, DriveGame.Flow.cs MusicTrack/SaveResultOnce), Audio/sfx/<이름>_<번호>.wav(같은 이름이 여럿이면 무작위, AudioManager.Pick; 엔진 eng0~6 기어 루프), Audio/voice/{m,f,a}NN.wav(말소리 조각: 남/여/외계인, AudioManager.Speak/PlayBlip 이 글자마다 짧게 잘라 음 높이만 바꿔 "음어" 소리를 냄. 손님은 DriveGame.Voice.cs, 뉴스는 News.cs, 사장님은 Lobby.cs), Audio/rain.wav, Cars/carNN_{rear,front}.png(World/CarArt.cs, 뒷모습을 교통 차에 씀, 폭 2m 로 맞춤). 새 컴퓨터에서는 원본 팩(Racing Neo Electronica 002, Gamemaster Audio Pro Sound Collection, FREE CAR SFX Pack, Free Pixel Pack Car Sprites #1)을 _External 에 두고 복사 스크립트를 다시 돌려야 함
  - **다국어(영어 기본 / 한국어)**: Gameplay/Loc.cs. 코드 안 글은 `Loc.T("한국어", "English")`, 대사/이름 표(배열)는 `"한국어‖English"` 한 줄로 적고 읽을 때 `Loc.S(...)`(PickOne/Pick/NewsBeat/LobbyManager.StartDialogue 가 이미 처리). ‖ 가 없으면 어느 언어든 그대로 나옴. **새 글자를 만들면 반드시 두 언어를 같이 쓸 것.** 금액은 `Loc.Money(원)`(영어는 $, 100으로 나눔). 배경은 뉴욕이라 등장인물 이름은 영어 이름(토니 사장=Boss Tony, 제시카 비서, 매니, 프랭키, 비니, 빅 마이크, 앵커 데이나, 기자 피트, 회사 Big Apple Taxi). 언어는 로비에서 비서(제시카)에게 말을 걸면 대사 뒤에 열리는 설정(또는 O 키)에서 바꿈(PlayerPrefs DontHitMe.Lang). 번역 안 한 글은 한국어로 나오니 이상하면 ‖ 를 확인할 것
  - 조작 키 설정: Keybinds(Loc.cs) 가 조향/브레이크/깜빡이/태우기/거절 키를 PlayerPrefs 에 저장. 방향키(조향/브레이크)와 Enter(태우기)는 늘 같이 먹음. 설정 화면의 "조작 키 설정"에서 바꿈. 새 조작을 만들면 Keybinds 에 동작을 추가하고 키 안내 글자(ControlsHint, 타이틀 ControlRow)도 갱신. 설정에 효과음 볼륨(sfxVolume)이 따로 있음
  - 이야기 분위기는 병맛 막장(StoryCatalog 의 대사 톤을 유지할 것)
  - Gameplay/DriveGame.Heist.cs + TaxiManager(HeistStage) : 랜덤 인카운트 "은행 강도 손님"(heistChance). 수상한 손님을 태우면 은행(1)으로 가고, 도착하면 손님이 들어가고 heistWaitSeconds 동안 이 자리에서 기다려야 함(2, 벗어나면 실패, TaxiState.Waiting), 손님이 뛰어와 거절 불가로 타면(3) 비밀 아지트까지 빠듯한 시간 안에 도망(요금 heistFareMul 배, 경찰 바리케이드=TrafficManager.TickPolice/Police 종류, 사이렌, 화면 가장자리 경광). 디버그 V = 다음 손님을 강도로
  - Gameplay/EncounterManager.cs + DriveGame.Encounters.cs : 랜덤 인카운트(레벨 2부터 45초 뒤, 이후 35~60초 간격으로 하나씩): 정전(Blackout01, 화면 어두워짐+헤드라이트), 라이벌 택시(TrafficManager.SpawnRival: 길가 손님을 가로챔, 먼저 태우면 못 가로챔), 팬 습격(PedestrianManager.FanRush: 튀어나오는 사람 연속+플래시), 돼지 떼(TrafficManager.TickPigs: 닿으면 속도만 감소), 긴급 차량(TrafficManager.SpawnAmbulance: 구급차가 뒤에서 사이렌을 울리며 플레이어가 선 차선으로 달려옴, 룸미러와 화면 가장자리에 붉은/푸른 빛, 앞지르는 순간 길을 막고 있으면 들이받혀 목숨 -1, 비켜 주면 +100점과 분노 게이지)
  - 손님 유별 사건(PaxType.quirk, TaxiManager.TickQuirk): 폭탄(속도 bombMinSpeed 아래면 도화선 소모, 터지면 목숨-1, 끝까지 가면 요금 x2) / 결혼식(급조향·급제동이면 부케가 앞유리에 붙고 행복도 하락, 행복하면 축의금) / 외계인(내비에 목적지 안 보임, UFO 빛줄기 기둥을 찾아 멈춤, 요금 x1.5) / 전직 레이서(시간 짧음, 평균 속도 빠를수록 요금 최대 x2, 사고 시 크게 감액) / 욱한 손님(6~10초마다 아무 표시 없이 갑자기: 고함 0.4초 -> 핸들을 잡힘. 차가 한쪽으로 쏠리고 조향 입력이 안 먹으며, A/D 를 연타해 가운데에서 양쪽으로 차는 게이지(MashL/MashR, 인스펙터 fighterMashStep/Decay/YankMax)를 둘 다 채워야 풀림. 안 채워도 fighterYankMax 초 뒤 풀림. 자막으로 설명하지 말 것). HUD 표시는 UI/DriveGame.QuirkHud.cs. 디버그 B = 다음 손님 유형 고정, Y/K/L/J = 정전/라이벌/팬/돼지
  - 손님의 말(사고 반응, 수다, 고함, 미니건 반응 등)은 모두 같은 얼굴 말풍선 상자(DrawTaxiGui)로 나온다. 평소 손님은 화면 오른쪽에 작게, 수다쟁이(PaxType.bigTalk)만 가운데에 크게(말로 화면을 가리는 것이 기믹)
  - 목적지 거리: TaxiManager.Accept 가 tripMinBlocks~tripMaxBlocks 칸(기본 6~11, 1칸=80m)에서 뽑고 옆 칸수는 tripSideBlocks 이내. (예전 destMaxAhead/destMaxSide 는 더 안 쓴다)
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
택시 게임(손님 태우기, 교차로 회전, 내비, 로비/상점/택시 개조, 사건/인카운트/분노/미니건/은행 강도 등)은 기능이 거의 다 들어가 있다. 지금은 **안정화와 밸런스 단계**. 컴파일 확인은 check-compile.ps1, 플레이 확인은 사용자가 Unity에서 한다. (예전 2a/2b/3-x 단계 메모는 모두 끝나서 지웠다. 필요하면 git 기록을 볼 것)

### 자동 시험 봇 (Unity 에디터를 끈 상태에서만 실행 가능)
- Assets/Game/Scripts/Gameplay/DriveGame.Bot.cs : 명령줄 -simbot 일 때만 켜지는 자동 운전 봇(에디터/개발 빌드 전용). 가상 키보드로 입력을 넣어 보행자를 피하고 손님을 태우고 목적지까지 가며, 하루가 끝나면 번 코인으로 업그레이드/개조를 산다. 보고서에 오류/예외, 사고 원인, 수입, 프레임 시간, 메모리, 정체 의심 구간을 남긴다. Editor/SimBotLauncher.cs 가 배치모드에서 장면을 열고 플레이를 시작한다.
- Tools/run-simbot.ps1 : 실행기(-Days -Skill 0|1|2 -Cover -NoShop -NoGraphics -Set "필드=값,필드=값" -DayType). 저장 데이터(PlayerPrefs)는 실행 전 레지스트리 백업, 후 복구. 결과는 SimReports/(gitignore).
- Tools/sweep-simbot.ps1 : 설정을 바꿔 가며 여러 번 돌려 SUMMARY 한 줄씩 모은다(밸런스 비교). 한 설정 24일 치에 4~5분.
- 한계: 봇은 사람이 아니다(브레이크가 서툴러 저속 충돌이 많다). 절대 난이도가 아니라 설정 간 상대 비교로만 쓴다. 화면 렌더링/IMGUI 비용, 소리, 조작감은 못 잰다(-nographics 와 같은 수준으로 돌아감). 같은 설정도 24일 기준 hits/min 이 ±0.2 흔들린다.
- -Cover 4일 시험(괴수/히어로/라이벌/구급차/은행 강도/손님 유형 5종 강제): 예외 0건, 경고 0건.

### 밸런스 조정 기록 (봇 기준, 첫날 같은 조건)
- 난이도: 사고율 2.17/분 → 약 1.3/분. maxCrossers 4→3, cruiseCap 28→24, dartLeadTime 2.6~3.6, farLeadTime 4~5.5, crossSpeed 1.8~3.0(가장 효과 큼), healSeconds 50→35. 이 값들은 씬에 저장돼 있지 않아 코드 기본값이 적용된다(씬을 저장했다면 인스펙터에서 확인).
- 경제: 평점 기준 6000/15000/28000, accidentFine 500, 택시 개조 4000/9000/24000/60000, 오늘의 목표 값 상향. 봇 기준 업그레이드는 12일 안에 전부, 개조 완료는 약 40일.
- 남은 약점: 업그레이드로 목숨이 5개가 되면 쉬워진다(12일 이후 사망 13%). 필요하면 후반 난이도 스케일(레벨 시작값 상승 등)을 넣을 것.

### 성능 작업 기록
- 켤 때 로딩 화면(UpdateLoading/DrawLoading, 소리·음악 미리 읽기 AudioManager.Preload). 로비/상점/엔딩에서는 카메라 culling mask 를 0 으로 해 3D 월드를 안 그림(camMaskCaptured 로 원래 마스크 저장). 크기가 바뀌는 글자는 LabelScaled(글꼴 크기 고정+화면 배율). 길가 소품 재질 공유(머티리얼 1145->약 214). 로비는 화면 밖 그림 생략. UI/DriveGame.Sections.cs 의 Sec()/SectionReport() 가 구간별 시간과 할당량을 재서 봇 보고서에 붙임(-simbot/-uishots 때만 기록). **소리 데이터 미리 읽기**: 소리 파일(_External 의 wav)이 전부 Preload Audio Data 꺼짐이라 Resources 로 불러오기만 하면 처음 재생할 때 파일을 읽어 30ms 쯤 멈춘다(Profiler: SoundManager.LoadFMODSound -> File.Open, 비+충돌 프레임 41.9ms 의 원인). AudioManager.Preload 가 로딩 중에 LoadAudioData() 로 효과음/음악/비/결산음을 메모리에 올리고 기다림(점검: 효과음 91개 첫 재생 최대 46.8ms -> 0.3ms, 로딩 화면은 약 2.5초). 새 소리 파일을 Resources 에 더하면 자동으로 포함됨. 점검 도구 -uishots 가 audit 에 "첫 재생 비용"을 적음. 사용자 실측: 비+사람 많이 친 장면 92 FPS(CPU main 10.8ms, Batches 1073, SetPass 560, 2560x1440 에디터).

### 빌드 시험
- Tools/run-build-test.ps1 : 에디터를 끄고 돌리면 개발 빌드(Editor/BuildTest.cs, Windows Mono)를 저장소 밖 D:\Unity\project\_Builds\DontHitMe-test 에 만들고, 그 exe 로 -uishots 화면 점검을 돌려 스크린샷을 SimReports/build-시각/ 에 남긴다(-SkipBuild, -SkipRun). 분홍색 셰이더/빠진 파일 확인용. Shader.Find 로 찾는 URP Lit/Unlit 은 GraphicsSettings 의 Always Included Shaders 에 넣어 둠(BuildTest.EnsureShaders, 메뉴 Game > Ensure URP Shaders Included). 빌드 폴더는 구매한 에셋이 들어 있어 절대 커밋하지 않는다. 첫 시험: 오류 0, 분홍색 없음(눈으로 확인), 소리와 조작감은 미확인. 빌드에서 로딩은 약 3.8초(소리 데이터 미리 읽기).

### 아직 안 고친 점검 지적 (심각도 낮음)
- 정지 화면(일시정지/게임오버)에 정전/팬 플래시/피격 붉은 막이 남음(Overlays/Gui). 분노 중 폭탄이 터지면 요금만 깎임. 분노 중 급감속이 부케를 날림. 일시정지에서 R로 재시작하면 최고점/Day 저장 안 됨. 회전 때 구급차가 판정 없이 사라짐. 구급차가 정차 중 손님 태우기와 겹칠 수 있음.
- 성능(코드 읽기 추정, 실측 없음): WorldView가 4m마다 건물 40칸 전체를 재설정(봇 측정상 로직 비용은 최대 9ms), 매 프레임 Transform ~600회 쓰기, IMGUI 호출 수 많음(비/혈흔/외곽선 글자), 로비에서도 3D 월드가 그려짐, Shader.Find 로 URP 셰이더를 찾음(빌드에서 분홍색 위험: Always Included Shaders 확인). 사용자가 Profiler/Stats(FPS, Batches, SetPass)로 실측해 알려 줄 것.

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
- GandalfHardcore 팩들(City Tiles, Modern NPCs, Zombies, Pixel Art Enemies, Farm Animals, Hell Asset Pack): 상업 사용과 수정 허용, 재판매·재배포·AI 학습 금지. 저장소가 공개라서 에셋 파일은 올리지 않는다. 크레딧 표기는 필수는 아니지만 권장(README 에 적을 것).

## Git
- 원격: https://github.com/oppa073-hub/Dont-Hit-Me.git
- 저장소는 **Public(포트폴리오용)**. 구매한 에셋(Assets/_External/)은 라이선스상 재배포 금지라 절대 커밋하지 말 것(.gitignore 로 제외, 이력에도 없음). 푸시 전에 `git status` 에 _External, 이미지/모델/소리 파일이 없는지 확인하고, 푸시는 확인받을 것.
