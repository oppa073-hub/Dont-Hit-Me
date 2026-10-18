using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 택시 드라이브 게임: 운전석 1인칭 시점에서 둥글게 휜 도시 도로를 달리며 무단횡단자를 피하고 손님을 태우는 게임.
// 빈 씬에 이 컴포넌트 하나만 붙이면 도로/보행자/차 내부/HUD 전부 코드로 생성된다. (메뉴: Game > Create Drive Scene)
// 조작: A/D 또는 ←/→ 조향, S/↓ 브레이크, Q/E 좌/우 깜빡이, Space 손님 태우기, X 거절, R 재시작(게임오버 후)
// 원통 맵: 차는 제자리, 월드가 다가오며 앞쪽일수록 y = -curvature * z^2 로 아래로 휜다 (동물의 숲 방식).
// 이 클래스는 partial 이다: 역할별로 DriveGame.*.cs 파일에 나뉘어 있다 (World/, Gameplay/, Cabin/, UI/).
public partial class DriveGame : MonoBehaviour
{
    [Header("Assets (씬에 연결됨, 비어 있으면 도형으로 대체)")]
    public Texture2D buildingTiles;

    public Texture2D decoTiles;

    public Texture2D cityBackground;

    public Texture2D armAlbedo;

    public GameObject armsModel;

    public bool use3DArms = true;

    [Header("World")]
    [Tooltip("클수록 지평선이 가까워지고 더 둥글게 휜다")]
    public float curvature = 0.0008f;

    public float roadHalfWidth = 4f;

    [Tooltip("이 거리(m) 밖의 건물은 문/소품/지붕 장식/다가오는 쪽 단면을 끈다(최적화). 0 = 끔. 단면이 갑자기 사라져 보이면 키운다")]
    public float lodDistance = 70f;

    public float camHeight = 1.2f;

    [Header("Intersection / Turn (교차로와 회전)")]
    [Tooltip("교차로가 나오는 간격(구간 수, 1구간 = 4m). 20 = 80m마다")]
    public int intersectionEvery = 20;

    [Tooltip("교차로가 차지하는 구간 수. 2 = 8m")]
    public int intersectionLen = 2;

    [Tooltip("교차로에서 좌우로 뻗는 가로 도로 길이(m)")]
    public float crossStreetLength = 30f;

    [Tooltip("깜빡이를 켠 상태에서 교차로 중심까지 이 거리(m) 안으로 오면 회전을 시작한다. 작을수록 교차로 한가운데에서 돈다(0보다 커야 함)")]
    public float turnCenterTolerance = 1.5f;

    [Tooltip("회전(월드가 90도 도는 데) 걸리는 시간(초). 교차로 한가운데에서 이 시간 동안 돈다")]
    public float turnDuration = 1.1f;

    [Tooltip("깜빡이를 켠 상태에서 교차로 중심까지 이 거리(m) 안으로 오면 turnCruiseSpeed로 자동 감속한다")]
    public float turnSlowDistance = 22f;

    [Tooltip("회전 중 장면 교체 때 화면이 어두워졌다 밝아지는 전체 시간(초). 가장 어두운 순간(절반)에 도로가 교체된다")]
    public float swapFadeTime = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("장면 교체 때 가장 어두워지는 정도. 0 = 안 어두워짐(교체 순간이 그대로 보임), 1 = 완전히 까맣게")]
    public float swapFadeDarkness = 0.7f;

    [Tooltip("회전 중 차가 도로 가운데로 모이는 속도(m/s)")]
    public float turnCenteringSpeed = 4f;

    [Tooltip("회전이 끝난 뒤 목숨이 깎이지 않는 무적 시간(초). 이 동안 부딪히면 감속/요금 감액은 일어난다")]
    public float postTurnInvuln = 0.5f;

    [Tooltip("회전하는 동안 자동으로 맞춰지는 속도(m/s). 브레이크를 밟지 않아도 이 속도로 돈다")]
    public float turnCruiseSpeed = 9f;

    [Range(10f, 80f)]
    [Tooltip("회전 각도가 이 값(도)을 넘는 순간, 옛 도로 대신 새 도로를 보여 주는 장면 교체가 일어난다(짧게 어두워짐)")]
    public float turnSwapAngle = 38f;

    [Header("Car")]
    public float baseSpeed = 12f;          // m/s

    public float maxSpeed = 30f;

    public float speedRamp = 0.2f;         // 초당 증가

    [Tooltip("브레이크를 누를 때 목표 속도에 곱하는 값. 0 = 완전히 멈춤(택시는 정차해야 해서 0)")]
    public float brakeFactor = 0f;
    bool wasBrake;
    float screechAt;

    public float maxLateral = 6f;          // 최대 횡이동 속도

    public float lateralAccel = 25f;       // 조향 관성 (작을수록 묵직)

    [Header("Pedestrians (인도를 걷다가 일부가 도로로 나온다)")]
    [Tooltip("사람 한 명이 나타나는 간격(초). 인도 보행자도 포함이라 짧다")]
    public float spawnInterval = 0.9f;

    public float minSpawnInterval = 0.35f;

    public float intervalRamp = 0.012f;

    public int maxPeds = 30;

    [Tooltip("게임 시작 때 인도에 미리 깔아 둘 사람 수")]
    public int initialCrowd = 16;

    [Range(0f, 1f)]
    [Tooltip("인도만 걷는 사람 비율 (도로로 안 나옴)")]
    public float sidewalkerRatio = 0.6f;

    [Range(0f, 1f)]
    [Tooltip("멀리서 이미 건너기 시작하는 사람 비율. 나머지(1 - 위 두 값)는 가까이서 갑자기 튀어나오는 사람")]
    public float farCrossRatio = 0.15f;

    [Tooltip("인도에서 도로 가장자리까지 떨어진 거리(m) 범위")]
    public Vector2 sidewalkOffset = new Vector2(0.6f, 3.5f);

    [Tooltip("인도를 따라 걷는 속도(m/s). 부호 없이 크기만, 방향은 무작위")]
    public Vector2 sidewalkWalkSpeed = new Vector2(0.5f, 1.4f);

    [Tooltip("차가 이 거리(m) 안으로 오면 갑자기 튀어나오는 사람이 움직이기 시작한다")]
    public Vector2 dartTrigger = new Vector2(16f, 30f);

    [Tooltip("튀어나오기 전 연석에서 멈칫하는 시간(초). 0이면 예고 없이 바로")]
    public Vector2 curbWait = new Vector2(0.5f, 0.9f);

    [Tooltip("튀어나오는 사람의 달리기 속도(m/s)")]
    public Vector2 dartSpeed = new Vector2(4f, 6f);

    [Tooltip("멀리서 이미 건너는 사람이 움직이기 시작하는 거리(m)")]
    public Vector2 farTrigger = new Vector2(45f, 80f);

    [Tooltip("멀리서 건너는 사람의 걷는 속도(m/s)")]
    public Vector2 crossSpeed = new Vector2(1.8f, 3.0f);       // 봇 시험: 빨리 건너면 도로에 겹쳐 있는 시간이 줄어 사고가 약 30% 줄었다

    public float nearMissDist = 2.2f;      // 중심 간 횡거리 (2.3 이상이면 안전)

    public int nearMissBonus = 50;

    [Header("Taxi (손님 태우기)")]
    [Tooltip("손님이 새로 나타나기까지 걸리는 시간(초) 범위. 손님을 내려 준 뒤/놓친 뒤부터 센다")]
    public Vector2 callerDelay = new Vector2(3f, 7f);

    [Tooltip("손님이 나타나는 거리(m)")]
    public float callerDistance = 60f;

    [Tooltip("이 속도(m/s) 이하이면 정차로 본다")]
    public float pickupSpeed = 1.5f;

    [Tooltip("손님과 차 사이 최대 좌우 거리(m). 차를 손님 쪽 인도로 붙여야 한다")]
    public float pickupLateral = 3.6f;

    [Tooltip("목적지는 '다음 교차로'에서 앞쪽으로 최대 몇 칸까지 뽑을지 (0이면 다음 교차로 옆길만)")]
    public int destMaxAhead = 3;

    [Tooltip("목적지는 진행 방향 좌/우로 최대 몇 칸까지 뽑을지")]
    public int destMaxSide = 2;
    [Tooltip("손님을 태운 곳(방금 지난 교차로)에서 목적지까지의 거리 범위(칸, 1칸 = 80m). 길수록 요금과 제한 시간도 늘어난다")]
    public int tripMinBlocks = 6;
    public int tripMaxBlocks = 11;
    [Tooltip("목적지가 옆길 쪽으로 최대 몇 칸까지 벗어날 수 있는가")]
    public int tripSideBlocks = 4;

    [Tooltip("목적지 교차로 중심에서 이 거리(m) 안에서 정차하면 도착. 이만큼 지나치면 '지나쳤어요'")]
    public float arriveRadius = 9f;

    [Range(0.2f, 1.5f)]
    [Tooltip("제한 시간 계산용 평균 속도 비율. 손님을 태운 순간의 순항 속도 × 이 값으로 목적지까지 걸리는 시간을 잡는다. 작을수록 시간이 넉넉하다 (정차/감속/회전 시간을 반영한 값)")]
    public float limitSpeedRatio = 0.7f;

    [Tooltip("제한 시간에 더해 주는 여유(초). 정차, 깜빡이 감속, 회전 시간을 위한 값")]
    public float timeBuffer = 12f;

    [Tooltip("제한 시간을 넘긴 뒤 초당 깎이는 요금 비율 (0.02 = 초당 2%)")]
    public float lateDropPerSec = 0.02f;

    [Range(0f, 1f)]
    [Tooltip("지각 감액만의 하한. 사고/지나침 감액과 합쳐도 요금은 기본의 20% 밑으로는 내려가지 않는다")]
    public float lateMinMul = 0.3f;

    [Tooltip("제한 시간을 절반 이상 남기고 도착할 때 받는 팁(원)의 최대값. 손님 유형의 팁 배율이 곱해진다")]
    public int tipMax = 1500;

    public int baseFare = 2000;

    [Tooltip("미터당 요금(원)")]
    public int farePerMeter = 3;

    [Range(0f, 1f)]
    [Tooltip("손님을 태운 채 사람을 칠 때마다 요금에 곱하는 값")]
    public float hitFareMul = 0.8f;

    [Tooltip("옆 시점으로 돌릴 때의 시야각(작을수록 손님이 크게 보임)")]
    public float sideFov = 42f;

    [Tooltip("시점 전환 속도(도/초)")]
    public float yawSpeed = 220f;

    [Header("Rules / Feel")]
    public int maxLives = 3;

    public float invulnTime = 1.0f;

    [Tooltip("사고 없이 이만큼(초) 달리면 하트가 하나 회복된다. 사고가 나면 처음부터 다시 센다")]
    public float healSeconds = 35f;

    public float hitSpeedFactor = 0.4f;

    public float hitStop = 0.08f;

    public float shakeHit = 0.15f;

    [Header("운전석 (카메라 로컬 좌표 m: x 오른쪽, y 위, z 앞). 플레이 중 바꿔도 바로 반영")]
    [Tooltip("대시보드 윗면 높이. 낮출수록 앞 도로가 많이 보인다")]
    public float dashTopY = -0.55f;

    [Tooltip("계기판 후드(핸들 너머 속도계) 윗면 높이")]
    public float hoodTopY = -0.30f;

    [Tooltip("천장 아랫면 높이. 낮출수록 화면 위쪽이 많이 가려진다")]
    public float roofY = 0.52f;

    [Tooltip("내비 화면 중심 위치. 화면은 자동으로 운전자 눈을 향한다")]
    public Vector3 navPos = new Vector3(0.48f, -0.364f, 0.70f);       // 화면을 20% 키우면서 아랫단이 그대로 있도록 y 를 올렸다

    [Tooltip("내비 화면 크기(m, 가로/세로)")]
    public Vector2 navSize = new Vector2(0.312f, 0.192f);       // 예전 0.26 x 0.16 에서 20% 키움. 안의 글자도 같은 비율로 커진다

    [Tooltip("내비 지도에 보일 격자 칸 수(위아래 각각). 클수록 넓게 보이고 작게 그려진다")]
    public int miniMapRange = 3;

    [Tooltip("왼쪽 A필러 아래/위 끝 (운전석 쪽, 가까움)")]
    public Vector3 pillarLBase = new Vector3(-0.90f, -0.56f, 0.95f);

    public Vector3 pillarLTop = new Vector3(-0.56f, 0.55f, 0.50f);

    [Tooltip("오른쪽 A필러 아래/위 끝 (조수석 쪽, 멀어서 화면 오른쪽 끝에 조금만 보인다)")]
    public Vector3 pillarRBase = new Vector3(1.10f, -0.56f, 1.00f);

    public Vector3 pillarRTop = new Vector3(0.85f, 0.55f, 0.60f);

    public float pillarThickness = 0.07f;

    [Header("Cabin (카메라 로컬 좌표, 단위 m)")]
    public Vector3 wheelCenter = new Vector3(0f, -0.36f, 0.55f);

    public float wheelRadius = 0.17f;

    public float wheelMaxAngle = 100f;     // 최대 핸들 회전각(도)

    public Vector3 shoulderCenter = new Vector3(0f, -0.33f, 0.04f);

    public float armLength = 0.60f;        // 어깨~손목 전체 길이로 모델을 스케일

    public float handAngle = 30f;          // 핸들 3시/9시 기준 위로 몇 도 (10시-2시 = 30)

    [Range(0f, 1f)]
    [Tooltip("손이 핸들 회전을 따라가는 정도. 1 = 핸들과 함께 돈다(팔이 화면 밖으로 빠질 수 있음), 0 = 손은 고정이고 림만 미끄러진다")]
    public float gripFollow = 0.4f;

    [Range(0f, 180f)]
    [Tooltip("손목 접기(도). 주먹은 림에 둔 채 손을 림 둘레로 돌린다. 클수록 손목이 접혀 손가락이 앞유리 쪽 아래로 숨고 손등/주먹 마디가 위로 온다. 작을수록 손가락이 바깥으로 펴져 보인다")]
    public float gripRoll = 85f;

    [Tooltip("엄지가 림을 따라 위(12시) 쪽을 향한다. 끄면 손을 뒤집어 아래를 향하게 한다")]
    public bool thumbUp = true;

    [Tooltip("손 위치 미세 조정(m). x = 손가락 방향, y = 손바닥 방향, z = 엄지 방향")]
    public Vector3 gripNudge = Vector3.zero;

    [Tooltip("손가락 3마디 최대 굽힘각(도). 실제로는 림에 닿는 순간 멈춘다")]
    public Vector3 fingerCurl = new Vector3(90f, 100f, 80f);

    [Tooltip("엄지 3마디 최대 굽힘각(도). 림에 닿는 순간 멈춘다")]
    public Vector3 thumbCurl = new Vector3(40f, 50f, 40f);

    [Range(0f, 1f)]
    [Tooltip("손가락을 최소 이 비율만큼은 항상 굽힌다(최대 굽힘 대비). 림에 닿지 않는 손가락은 끝까지 오므린다")]
    public float minCurl = 0.55f;

    [Tooltip("손가락 두께 여유(m). 손가락이 림을 파고들면 키우고, 림에서 뜨면 줄인다")]
    public float fingerPad = 0.008f;

    [Header("Rage (분노: 손님을 태웠을 때 게이지가 차 있으면 자동 발동. 무적 + 가속. 니어미스로 더 빨리 찬다. 팔이 붉어져서 알려 준다)")]
    public bool rageOn = true;
    [Tooltip("분노 게이지가 처음부터 가득 차는 데 걸리는 시간(초)")]
    public float rageFillSeconds = 60f;
    [Tooltip("분노가 이어지는 시간(초)")]
    public float rageDuration = 8f;
    [Tooltip("분노 중 목표 속도 배율 (최고 속도를 넘을 수 있다)")]
    public float rageSpeedMul = 1.35f;
    [Range(0f, 1f)]
    [Tooltip("아슬아슬하게 피할 때마다 차는 게이지 (1 = 한 번에 가득)")]
    public float rageNearMissGain = 0.15f;
    [Tooltip("분노 중 부딪혔을 때 얻는 점수")]
    public int rageSmashScore = 30;

    [Header("Encounters (랜덤 인카운트: 정전, 라이벌 택시, 팬 습격, 돼지 떼. 손님 유형별 사건은 PassengerCatalog)")]
    public bool encountersOn = true;
    [Tooltip("운전을 시작하고 첫 사건이 나올 때까지의 시간(초). 레벨 2부터 나온다")]
    public float encounterFirstDelay = 45f;
    [Tooltip("사건이 끝난 뒤 다음 사건까지의 시간(초) 범위")]
    public Vector2 encounterInterval = new Vector2(35f, 60f);
    [Tooltip("라이벌 택시가 손님 쪽으로 달려오는 속도(m/s)")]
    public float rivalSpeed = 11f;
    [Tooltip("폭탄 손님: 이 속도(m/s) 아래로 느려지면 도화선이 탄다")]
    public float bombMinSpeed = 8f;
    [Tooltip("폭탄 손님: 느린 동안 초당 줄어드는 도화선 (0.3이면 약 3.3초 만에 폭발)")]
    public float bombDrain = 0.3f;
    [Tooltip("욱한 손님: 핸들을 잡힌 채 버티는 최대 시간(초). A/D 연타로 게이지를 못 채워도 이 시간이 지나면 풀린다")]
    public float fighterYankMax = 8f;
    [Tooltip("욱한 손님: A 또는 D 를 한 번 누를 때 그쪽 게이지가 차는 양 (0.15면 한쪽에 약 7번)")]
    public float fighterMashStep = 0.15f;
    [Tooltip("욱한 손님: 차던 게이지가 초당 줄어드는 양 (0.1이면 천천히 줄어듦. 클수록 더 빠르게 연타해야 함)")]
    public float fighterMashLeak = 0.1f;
    [Tooltip("돼지 그림. 비우면 코드로 그린 임시 그림")]
    public Sprite pigSprite;
    [Tooltip("구급차 그림(뒷모습). 비우면 코드로 그린 임시 그림")]
    public Sprite ambulanceSprite;
    [Tooltip("구급차가 플레이어보다 이만큼(m/s) 더 빠르게 달려 앞지른다. 작을수록 비킬 시간이 길다")]
    public float ambulanceSpeedAdd = 14f;

    [Header("Heist (랜덤 인카운트: 수상한 손님이 은행까지 가서 기다리게 하고, 나오면 경찰에게 쫓긴다)")]
    public bool heistOn = true;
    [Range(0f, 1f)]
    [Tooltip("손님을 태울 때 수상한 손님일 확률")]
    public float heistChance = 0.12f;
    [Tooltip("은행 앞에서 손님을 기다려야 하는 시간(초)")]
    public float heistWaitSeconds = 9f;
    [Tooltip("도망칠 때 제한 시간 배율 (작을수록 빠듯)")]
    public float heistTimeMul = 0.6f;
    [Tooltip("도망 손님의 요금 배율")]
    public float heistFareMul = 3f;
    [Tooltip("경찰 바리케이드가 나오는 간격(초)")]
    public float roadblockInterval = 6f;

    [Header("Balance (난이도 완화. 씬에 저장된 Max Speed 가 커도 이 상한이 먼저 적용된다)")]
    [Tooltip("순항 속도의 상한(m/s). 이보다 빨라지지 않는다 (분노 가속은 별도)")]
    public float cruiseCap = 24f;
    [Tooltip("한꺼번에 도로를 건너거나 건너려고 나와 있는 사람의 최대 수 (팬 습격/분노 때 몰려든 사람은 예외)")]
    public int maxCrossers = 3;
    [Tooltip("갑자기 튀어나오는 사람이 차가 도착하기 이만큼(초) 전에 건너기 시작한다 (범위). 클수록 일찍 보여서 쉽다")]
    public Vector2 dartLeadTime = new Vector2(2.6f, 3.6f);
    [Tooltip("멀리서 이미 건너는 사람: 차가 도착하기 이만큼(초) 전에 건너기 시작한다")]
    public Vector2 farLeadTime = new Vector2(4.0f, 5.5f);
    [Tooltip("분노 폭발 중 몰려드는 사람이 생기는 간격(초). 작을수록 많이 몰려든다")]
    public float rageSurgeInterval = 0.22f;

    [Header("Minigun (분노 폭발 때 양옆에서 튀어나와 앞을 쓸어버림. 택시 개조 3단계를 사면 켜짐)")]
    [Tooltip("1초에 몇 개를 쓸어버리는가")]
    public float gunKillsPerSec = 7f;
    [Tooltip("이 거리(m) 안의 앞쪽 물체를 쏜다")]
    public float gunRange = 55f;
    [Tooltip("하나 처치할 때마다 얻는 점수")]
    public int gunKillScore = 10;

    [Header("Ending (하늘을 나는 택시를 산 뒤 첫 손님을 태우면 달까지 날아간다. 대사는 StoryCatalog.EndingTexts)")]
    [Tooltip("날개가 펴지며 날아오르는 시각(초)")]
    public float endingLaunchAt = 2.5f;
    [Tooltip("달에 도착하는 시각(초)")]
    public float endingLandAt = 24f;
    [Tooltip("화면 흔들림 세기 배율 (0이면 흔들리지 않음)")]
    public float endingShake = 1f;

    [Header("Lobby (로비)")]
    [Tooltip("로비의 건물/스카이라인/소품을 도시 타일 그림으로 그린다 (끄면 예전 도형 맵). 택시와 사람은 이 값과 상관없이 픽셀 아트")]
    public bool lobbyCityTiles = false;

    [Header("City3D (3D 로우폴리 건물 테스트: Tools/stage-citykit.py 로 모델을 복사해 둬야 보인다. 끄면 예전 스프라이트 건물)")]
    public bool use3DBuildings = true;
    [Tooltip("모델 한 채가 도로를 따라 차지하는 길이(m). 모델 폭에 맞춰 크기를 정하는 기준 (6~12배 안으로 제한)")]
    public float cityKitLength = 11.5f;
    [Tooltip("3D 건물 크기 배율 (1 = 기본)")]
    public float cityKitScale = 1f;
    [Tooltip("건물 정면이 도로 반대쪽을 보면 켠다 (모델의 앞 방향이 반대일 때)")]
    public bool cityKitFlip;
    [Tooltip("교차로 옆길(아직 회전 전)에 미리 세워 두는 건물 수. 옆길 양쪽 가장자리마다 이만큼 (0이면 안 세움)")]
    public int crossBuildings = 1;

    [Header("Heat (도시 위험도 0~5: Day 가 오를수록 어려워지고, 대신 요금에 위험수당이 붙는다)")]
    [Tooltip("이 Day 부터 위험도가 1, 2, 3, 4, 5 단계가 된다")]
    public int[] heatDays = { 6, 12, 18, 26, 36 };
    [Tooltip("위험도 한 단계마다 판을 시작하는 레벨이 이만큼 올라간다 (차와 장애물이 일찍 나온다)")]
    public float heatLevelPerStep = 0.8f;
    [Tooltip("위험도가 이 단계 오를 때마다 동시에 건너는 사람 수 상한이 1 늘어난다")]
    public int heatCrossersStep = 2;
    [Tooltip("위험도 한 단계마다 랜덤 인카운트 간격이 이 비율만큼 짧아진다")]
    public float heatEncounterCut = 0.07f;
    [Tooltip("위험도 한 단계마다 사건이 있는 날(괴수, 히어로, 좀비)이 나올 확률이 이만큼 늘어난다")]
    public float heatEventBonus = 0.03f;
    [Tooltip("위험도 한 단계마다 보행자 생성 간격이 이만큼 짧아진다 (사람이 더 자주 나온다)")]
    public float heatPedCut = 0.08f;
    [Tooltip("위험도 한 단계마다 갑자기 튀어나오는 사람 비율이 이만큼 늘어난다")]
    public float heatDartBonus = 0.03f;
    [Tooltip("위험도 한 단계마다 체력(하트) 회복이 이 비율만큼 느려진다")]
    public float heatHealSlow = 0.25f;
    [Tooltip("위험도 한 단계마다 요금에 붙는 위험수당 비율")]
    public float heatFareBonus = 0.06f;

    [Header("Night (밤의 헤드라이트: 진짜 스포트라이트 + 사람/차/건물 그림은 같은 빛 모양으로 색을 어둡게 칠함)")]
    [Tooltip("헤드라이트 켜기 (끄면 밤에 아주 어둡다)")]
    public bool headlightOn = true;
    [Tooltip("불빛이 닿는 거리(m)")]
    public float headlightRange = 45f;
    [Tooltip("불빛 하나가 퍼지는 각도(도, 전체 각). 좁을수록 도로 안만 비춘다")]
    public float headlightAngle = 22f;
    [Tooltip("왼쪽/오른쪽 헤드라이트가 차 가운데에서 떨어진 거리(m)")]
    public float headlightSpacing = 0.95f;
    [Tooltip("두 헤드라이트 불빛이 앞쪽 이 거리(m)에서 한곳으로 모인다. 작을수록 가깝게 모이고, 아주 크면 평행하게 나간다")]
    public float headlightConverge = 28f;
    [Tooltip("도로 밖(인도, 건물)에도 불빛이 닿는 정도 (0 = 도로만 보임, 1 = 인도까지 보임)")]
    public float headlightSideLight = 0f;
    [Tooltip("스포트라이트 밝기 (도로와 건물 몸통 같은 3D 부분). 거리 제곱에 반비례해 약해지므로 큰 값을 쓴다")]
    public float headlightIntensity = 650f;
    [Tooltip("한밤에 불빛이 안 닿는 곳의 밝기 (0 = 칠흑, 1 = 낮)")]
    public float nightMinLight = 0f;
    [Tooltip("밤에 운전석 안(핸들, 팔)을 비추는 아주 작은 조명 밝기. 손이 빛나 보이면 낮추고 0이면 완전히 끈다")]
    public float cabinGlow = 0.03f;

    [Header("Zombie (좀비의 날: 초록 좀비는 치면 보상, 주황 생존자는 치면 사고)")]
    [Tooltip("좀비 사태가 이어지는 시간(초)")]
    public float zombieDuration = 45f;
    [Tooltip("사태 중 새로 생기는 보행자 중 좀비가 되는 비율")]
    public float zombieRatio = 0.5f;
    [Tooltip("사태 중 새로 생기는 보행자 중 생존자(치면 안 됨)가 되는 비율")]
    public float zombieSurvivorRatio = 0.14f;
    [Tooltip("사태 중 도로로 나오는 좀비가 추가로 생기는 간격(초)")]
    public float zombieInterval = 0.6f;
    [Tooltip("좀비가 걷는 속도 배율 (작을수록 느림)")]
    public float zombieSlow = 0.6f;
    [Tooltip("좀비를 칠 때 얻는 점수")]
    public int zombieScore = 30;
    [Tooltip("좀비를 칠 때 차오르는 분노 게이지 (1 = 가득)")]
    public float zombieRageGain = 0.04f;

    [Header("Events (날짜와 돌발 사건: 평범한 날 / 사람 많은 날 / 괴수 침공 / 히어로와 빌런)")]
    [Tooltip("시험용. -1 = 무작위, 0 평범, 1 사람 많은 날, 2 괴수 침공, 3 히어로와 빌런. 플레이 중 F2 = 괴수, F3 = 히어로를 바로 시작")]
    public int forceDayType = -1;
    [Tooltip("괴수 사건이 이어지는 시간(초). 이 동안 괴수가 계속 나타난다")]
    public float kaijuDuration = 50f;
    [Tooltip("괴수가 있는 동안 차가 낼 수 있는 최고 속도(m/s). 교차로에서 돌 시간을 준다")]
    public float kaijuSpeedCap = 12f;
    [Tooltip("히어로와 빌런 전투가 이어지는 시간(초)")]
    public float heroDuration = 45f;
    [Tooltip("히어로 레이저가 나오는 간격(초, 이 값부터 +3초 사이)")]
    public float laserInterval = 4.5f;
    [Tooltip("레이저가 남긴 불길이 타오르는 시간(초)")]
    public float laserFlameLife = 7f;
    [Tooltip("비워 두면 코드로 그린 임시 그림을 쓴다. 괴수/히어로/빌런은 정면 그림, 불길과 연기는 바닥이 아래")]
    public Sprite kaijuSprite;
    [Tooltip("히어로와 빌런이 싸우는 곳까지의 거리(m). 멀수록 멀리서 싸우는 느낌 (예전 78)")]
    public float battleDistance = 135f;
    [Tooltip("히어로와 빌런이 화면에서 보이는 크기 배율 (예전 = 1). 작을수록 멀리 있는 것처럼 보인다")]
    public float battleSize = 0.4f;
    public Sprite heroSprite;
    public Sprite villainSprite;
    public Sprite flameSprite;
    public Sprite smokeSprite;

    [Header("Shift (하루 영업 모드. 타이틀에서 Tab 으로 무한 모드와 바꾼다)")]
    [Tooltip("영업 시간(초). 이 시간이 지나면 새 손님을 받지 않고 결산한다")]
    public float shiftSeconds = 180f;
    [Tooltip("영업 시간이 끝났을 때 태우고 있는 손님을 마저 모시는 데 주는 최대 시간(초)")]
    public float shiftOvertimeMax = 60f;
    [Tooltip("사고(사람/차에 부딪힘) 한 건당 결산에서 깎는 수리비(원)")]
    public int accidentFine = 500;
    [Tooltip("순수익이 이 값 이상이면 평점 1 / 2 / 3")]
    public int shiftStar1 = 6000;
    public int shiftStar2 = 15000;
    public int shiftStar3 = 28000;

    [Header("Difficulty (시간이 지나면 레벨이 오른다)")]
    [Tooltip("레벨이 한 단계 오르는 데 걸리는 플레이 시간(초)")]
    public float levelSeconds = 30f;
    public int maxLevel = 10;

    [Header("Traffic (다른 차와 돌발 장애물. 레벨이 낮으면 안 나온다)")]
    [Tooltip("이 레벨부터 다른 차가 나온다 (트럭은 +1, 고장 난 차는 +2 레벨부터)")]
    public int vehicleStartLevel = 2;
    [Tooltip("이 레벨부터 굴러오는 공(뒤따라 아이가 튀어나옴)이 나온다 (공사 콘은 +1 레벨부터)")]
    public int hazardStartLevel = 2;
    [Tooltip("차가 나타나는 기본 간격(초). 레벨이 오를수록 줄어든다")]
    public float vehicleInterval = 9f;
    [Tooltip("돌발 장애물이 나타나는 기본 간격(초)")]
    public float hazardInterval = 11f;
    public float minTrafficInterval = 4f;
    [Tooltip("다른 차가 스스로 달리는 속도(m/s) 범위. 내 속도보다 느려야 따라잡는다")]
    public Vector2 vehicleSpeed = new Vector2(3f, 7f);
    [Tooltip("도로에 동시에 있을 수 있는 차/장애물 수")]
    public int maxTraffic = 6;
    [Tooltip("차 옆을 이 거리(m)보다 가깝게 비껴가면 CLOSE PASS 보너스")]
    public float vehicleNearMissMargin = 0.35f;
    public int vehicleNearMissBonus = 25;
    [Tooltip("비워 두면 코드로 그린 임시 그림을 쓴다. 넣으면 그 스프라이트를 그대로 쓴다 (뒷모습). 차/콘은 pivot 아무거나, 공은 pivot Center. 충돌 폭은 그림 폭에 맞춰진다")]
    public Sprite carSprite;
    public Sprite truckSprite;
    public Sprite coneSprite;
    public Sprite ballSprite;

    [Header("Music (배경음악)")]
    [Tooltip("여기에 AudioClip 을 넣으면 그 곡이 반복 재생된다. 비워 두면 코드로 만든 임시 곡이 나온다")]
    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float bgmVolume = 0.35f;
    [Header("Rain Glass")]
    [Tooltip("비 올 때 앞유리에 물방울이 맺히는 속도 (초당 개수, 비가 가장 셀 때). 많이 맺힐수록 앞유리가 뿌옇게도 흐려진다")]
    public float rainDropsPerSec = 18f;
    [Tooltip("앞유리에 동시에 맺힐 수 있는 최대 물방울 수")]
    public int rainDropLimit = 280;
    [Tooltip("가장 큰 물방울의 지름 (720p 화면 기준 px)")]
    public float rainDropBig = 36f;
    [Tooltip("와이퍼가 한 번 닦고 난 뒤 다음 닦기까지의 시간(초). 비가 세면 더 자주")]
    public float rainWipeInterval = 8f;
    [Tooltip("와이퍼가 한 번 왕복하는 데 걸리는 시간(초)")]
    public float rainWipeSeconds = 1.3f;
    [Tooltip("손님이 말할 때(그리고 뉴스 앵커) 나는 웅얼 소리 크기 (동물의 숲 말투)")]
    public float voicePaxVolume = 0.7f;
    [Tooltip("로비 사장님(상점)의 웅얼 소리 크기")]
    public float voiceBossVolume = 0.7f;

    [Header("Weather (낮/밤과 비)")]
    [Tooltip("끄면 항상 맑은 낮")]
    public bool weatherOn = true;
    [Tooltip("하루(24시간)가 흐르는 데 걸리는 플레이 시간(초)")]
    public float dayLength = 300f;
    [Range(0f, 1f)]
    [Tooltip("판을 시작할 때의 시각. 0.25 = 아침 6시, 0.5 = 낮 12시, 0.75 = 저녁 6시")]
    public float startTimeOfDay = 0.33f;
    [Range(0f, 1f)]
    [Tooltip("날씨가 바뀔 때마다 비가 올 확률")]
    public float rainChance = 0.5f;
    [Range(0f, 1f)]
    [Tooltip("비 올 때 핸들이 얼마나 미끄러운지 (조향 반응이 이만큼 느려진다)")]
    public float rainSlip = 0.35f;

    [Header("UI (글자)")]
    [Tooltip("비워 두면 기본 글꼴. 한글을 지원하는 ttf/otf 를 넣으면 모든 화면 글자에 적용된다")]
    public Font uiFont;

    [Header("Options (설정 화면에서 바꾼다)")]
    public bool shakeEnabled = true;

    [Header("Sound (코드로 만든 효과음, 외부 에셋 없음)")]
    public bool soundOn = true;
    [Range(0f, 1f)]
    public float masterVolume = 0.6f;
    [Tooltip("효과음(소리, 목소리, 비소리) 크기. 설정에서 바꾼다")]
    public float sfxVolume = 1f;
    [Range(0f, 1f)]
    [Tooltip("엔진 소리 크기. 효과음보다 작게 둔다")]
    public float engineVolume = 0.25f;

    const float RimTube = 0.018f;            // 핸들 림 단면 반지름(m)

    public const int RebaseBlocks = 16;             // 회전이 끝나면 도로 좌표를 이 번째 교차로로 옮긴다(구간 번호가 음수가 되지 않게, float 정밀도를 위해 작게)

    public const float StripLen = 4f;

    const int BackStrips = 12;                    // 카메라 뒤쪽(48m)에도 도로/건물을 깐다: 손님이 뒤쪽에 있어 카메라가 뒤를 볼 때 빈 공간이 안 나오게
    const int StripCount = 40 + BackStrips;

    public const float CarHalfWidth = 1.0f;       // 보행자 매니저도 충돌 판정에 쓴다

    const float TileW = 26f / 32f;           // 타일 좌우 3px 테두리를 잘라 낸 폭 (이음매의 검은 줄/간격 제거)

    const int TileCols = 4;                  // 한 구간(4m)에 가로 4칸 -> 타일 1칸 = 정확히 1m

    const float TileScale = StripLen / (TileCols * TileW);

    Camera cam;

    Shader litShader;

    float carX, lateralVel, speed, dist, elapsed;

    float steerAngle;

    float hitStopUntil, shakeUntil, popupUntil;      // 연출 타이머 (히트스톱, 화면 흔들림, 팝업)

    // 시점: 손님과 대화할 때 옆으로 돌린다 (camYawTarget 은 목표, camYaw 는 현재)
    float camYaw, camYawTarget;
    float camPitch, camPitchTarget;      // 대화 때 손님을 화면 위쪽 가운데에 두려고 살짝 내려다본다

    Transform destMarker;                // 목적지 표지 기둥 (손님을 태운 동안만 보인다)

    void Awake()
    {
        whiteTex = MakeTexture(false);
        circleTex = MakeTexture(true);
        peds = new PedestrianManager(this, transform, MakePedSprite());
        peds.Hit += OnPedHit;
        peds.NearMiss += OnPedNearMiss;
        CreateTraffic();
        CreateEvents();
        CreateEncounters();
        CreateRage();
        CreateMinigun();
        CreateMeta();
        CreateLobby();
        CreateAmbience();
        LoadBest();
        CreateSession();
        CreateRoad();
        CreateTaxi();
        litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.68f, 0.68f, 0.72f);
        var lgo = new GameObject("Light");
        lgo.transform.SetParent(transform, false);
        lgo.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
        var light = lgo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.8f;
        sunLight = light;

        roadA = Mat(new Color(0.28f, 0.28f, 0.3f));
        roadB = Mat(new Color(0.32f, 0.32f, 0.34f));
        swA = Mat(new Color(0.62f, 0.6f, 0.55f));
        swB = Mat(new Color(0.55f, 0.53f, 0.5f));
        bodyMats = new[]
        {
            Mat(new Color(0.40f, 0.45f, 0.50f)),
            Mat(new Color(0.72f, 0.62f, 0.46f)),
            Mat(new Color(0.50f, 0.12f, 0.12f)),
            Mat(new Color(0.55f, 0.15f, 0.35f))
        };
        dashMat = Mat(new Color(0.95f, 0.9f, 0.5f));
        groundMat = Mat(new Color(0.2f, 0.22f, 0.2f));         // 도로/인도/건물 아래 넓은 땅 (옆길 쪽이나 건물 밑이 비어 보이지 않게)
        darkMat = Mat(new Color(0.07f, 0.07f, 0.09f));

        hasCity = buildingTiles != null && decoTiles != null;
        if (hasCity) BuildCitySprites();

        for (int i = 0; i < StripCount; i++)
        {
            var s = new Strip
            {
                road = Block("Road"),
                swL = Block("SwL"),
                swR = Block("SwR"),
                dash = Block("Dash"),
                ground = Block("Ground")
            };
            s.ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            s.ground.gameObject.layer = SideLayer;
            s.dash.GetComponent<Renderer>().sharedMaterial = dashMat;
            for (int k = 0; k < 2; k++) s.bldg[k] = Block("Bldg");
            // 인도와 건물 몸통은 헤드라이트를 받지 않는 레이어: 도로만 환하게 보인다 (스프라이트 쪽은 headlightSideLight 로 조절)
            s.swL.gameObject.layer = SideLayer;
            s.swR.gameObject.layer = SideLayer;
            for (int k = 0; k < 2; k++) s.bldg[k].gameObject.layer = SideLayer;
            if (hasCity)
            {
                foreach (var side in s.sides)
                {
                    side.upper = MakeTiled("Upper", -1);
                    side.ground = MakeTiled("Ground", -1);
                    side.cornice = MakeTiled("Cornice", -1);
                    side.fUpper = MakeTiled("FUpper", -1);
                    side.fGround = MakeTiled("FGround", -1);
                    side.fCornice = MakeTiled("FCornice", -1);
                    side.door = MakeSR("Door", 0);
                    side.prop = MakeSR("Prop", 1);
                }
            }
            strips[i] = s;
        }

        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = false;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = hasCity ? new Color(128f / 255f, 194f / 255f, 206f / 255f) : new Color(0.55f, 0.75f, 0.95f);

        CreateHeadlights();
        BuildBackdrop();
        BuildCabin();
        BuildBattle();
        BuildMinigun();
        CreateSound();

        // 목적지 표지 기둥 (손님을 태운 동안만 보인다)
        destMarker = Block("DestMarker");
        destMarker.GetComponent<Renderer>().sharedMaterial = dashMat;
        destMarker.gameObject.SetActive(false);

        ResetGame();
        BotInit();
    }

    // ---------- 게임 ----------

    void ResetGame()
    {
        UpdateHeat();                        // 맨 먼저: 오늘의 도시 위험도 (다른 초기화가 쓴다)
        newsActive = false;
        ClearBlood();
        gun.Reset();
        ResetEncounters();
        rage.Reset();
        ResetEvents();                       // 오늘이 어떤 날인지 먼저 정한다 (사람 많은 날은 시작 인원이 달라진다)
        peds.Reset(SpawnZ);                  // 시작부터 인도에 사람이 있다
        ResetTraffic();
        carX = lateralVel = steerAngle = 0f;
        speed = baseSpeed;
        dist = elapsed = 0f;
        ResetAmbience();
        ApplyUpgrades();                     // 산 업그레이드를 먼저 적용해야 목숨에 반영된다
        session.Reset();
        ResetFlowState();
        popupUntil = 0f;

        taxi.Reset();
        ResetShift();
        goals.Reset();
        camYaw = camYawTarget = 0f;
        camPitch = camPitchTarget = 0f;
        hudTimeLeft = int.MinValue;
        road.Reset();
        hintF = int.MinValue;
        hudRem = -1;
        foreach (var s in strips) if (s != null) s.lastIdx = int.MinValue;
        if (destMarker != null) destMarker.gameObject.SetActive(false);
    }

    void Update()
    {
        Time.timeScale = (!gameOver && flow == GameFlow.Playing && Time.unscaledTime < hitStopUntil) ? 0f : 1f;
        BotUpdate();                         // 자동 운전 시험 봇 (명령줄 -simbot 일 때만 동작)
        var kb = Keyboard.current;
        if (kb == null) return;
        if (HandleDebugKeys(kb)) return;

        // 설정: 월드는 멈춰 있고 W/S 로 고르고 A/D 로 바꾼다
        if (flow == GameFlow.Settings)
        {
            Time.timeScale = 0f;
            UpdateSettings(kb);
            UpdateWorldVisuals();
            return;
        }

        // 로비: 2D 횡스크롤. A/D 로 걷고 Space 로 정비사에게 말을 걸거나 택시에 탄다
        if (flow == GameFlow.Lobby)
        {
            Time.timeScale = 0f;
            UpdateLobby(kb);
            return;
        }

        // 상점: 월드는 멈춰 있고 숫자키로 업그레이드를 산다
        if (flow == GameFlow.Shop)
        {
            Time.timeScale = 0f;
            UpdateShop(kb);
            UpdateWorldVisuals();
            return;
        }

        // 타이틀: 월드는 멈춰 있고 Space/Enter 로 시작한다
        if (flow == GameFlow.Ending)
        {
            Time.timeScale = 1f;
            UpdateEnding(kb);
            return;
        }

        if (flow == GameFlow.Title)
        {
            Time.timeScale = 0f;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) EnterLobby();
            else if (kb.oKey.wasPressedThisFrame) OpenSettings(GameFlow.Title);
            else if (kb.tabKey.wasPressedThisFrame) ToggleMode();
            UpdateWorldVisuals();
            return;
        }

        // 일시정지: Esc 또는 P (게임오버 화면에서는 쓰지 않는다)
        if (!gameOver && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
            flow = flow == GameFlow.Paused ? GameFlow.Playing : GameFlow.Paused;
        if (flow == GameFlow.Paused)
        {
            Time.timeScale = 0f;
            if (kb.rKey.wasPressedThisFrame)
            {
                SaveResultOnce();            // 일시정지에서 다시 시작해도 이번 판의 코인, 최고 점수, 날짜 기록은 남긴다
                StartGame();
            }
            else if (kb.oKey.wasPressedThisFrame) OpenSettings(GameFlow.Paused);
            UpdateWorldVisuals();
            return;
        }

        if (gameOver)
        {
            SaveResultOnce();
            if (rage.Active) rage.Reset();   // 분노/미니건 중에 끝나도 결과 화면에 붉은 손과 미니건이 남지 않게
            road.ClearFade();                // 회전 장면 교체 중에 끝나도 GAME OVER 화면이 어둡게 남지 않게
            if (kb.rKey.wasPressedThisFrame) StartGame();
            else if (kb.uKey.wasPressedThisFrame) EnterLobby();
            else if (kb.tabKey.wasPressedThisFrame) ToggleMode();      // 다음 판의 모드를 바꾼다
            UpdateWorldVisuals();
            return;
        }

        // 손님과 대화 중: 월드는 멈춰 있고 Space 로 태우거나 X 로 거절한다
        if (taxi.State == TaxiState.Talking)
        {
            session.Tick(Time.deltaTime);                            // 피격 직후 정차해도 붉은 화면이 남지 않게
            bool turned = Mathf.Abs(Mathf.DeltaAngle(camYaw, camYawTarget)) < 3f;
            if (turned && (Keybinds.Pressed(kb, Keybinds.Accept) || kb.enterKey.wasPressedThisFrame)) AcceptPassenger();
            else if (turned && Keybinds.Pressed(kb, Keybinds.Decline) && !taxi.HeistLocked) DeclinePassenger();
            UpdateWorldVisuals();
            return;
        }

        float dt = Time.deltaTime;
        elapsed += dt;

        // 조향 (관성)
        float input = (Keybinds.Held(kb, Keybinds.SteerRight) || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (Keybinds.Held(kb, Keybinds.SteerLeft) || kb.leftArrowKey.isPressed ? 1f : 0f);
        if (taxi.State == TaxiState.Carrying)
        {
            if (taxi.Grabbed)                                                          // 핸들을 잡혔다: A, D 를 번갈아 연타해 게이지를 채운다
            {
                if (Keybinds.Pressed(kb, Keybinds.SteerLeft) || kb.leftArrowKey.wasPressedThisFrame) taxi.Mash(-1);
                if (Keybinds.Pressed(kb, Keybinds.SteerRight) || kb.rightArrowKey.wasPressedThisFrame) taxi.Mash(1);
            }
            if (taxi.SteerYank != 0) input = taxi.SteerYank;                           // 욱한 손님이 핸들을 확 잡아챈다: 막을 수 없다
            else if (taxi.SteerFlip) input = -input;                                   // 핸들을 쥔 채 반대로 돌린다
        }
        if (turning) input = 0f;                              // 회전 중에는 차가 알아서 교차로 중앙으로 간다
        lateralVel = Mathf.MoveTowards(lateralVel, input * maxLateral, lateralAccel * (1f - rainSlip * weather.Rain) * dt);
        float limit = roadHalfWidth - CarHalfWidth;
        carX += lateralVel * dt;
        if (turning) carX = Mathf.MoveTowards(carX, 0f, turnCenteringSpeed * dt);
        if (Mathf.Abs(carX) > limit)
        {
            carX = Mathf.Clamp(carX, -limit, limit);
            lateralVel = 0f;
        }
        steerAngle = Mathf.Lerp(steerAngle, lateralVel / maxLateral * wheelMaxAngle, dt * 10f);

        // 속도
        bool brake = Keybinds.Held(kb, Keybinds.Brake) || kb.downArrowKey.isPressed;
        if (brake && !wasBrake && speed > 12f && Time.unscaledTime > screechAt)      // 빠르게 달리다 브레이크를 밟는 순간 타이어 끽
        {
            sound.PlayBrake();
            screechAt = Time.unscaledTime + 1.5f;
        }
        wasBrake = brake;
        float target = Mathf.Min(Mathf.Min(maxSpeed, cruiseCap), baseSpeed + elapsed * speedRamp) * (brake ? brakeFactor : 1f);
        target *= rage.SpeedMul;                              // 분노 중에는 더 빨리 달린다
        if (turning) target = turnCruiseSpeed;                // 회전 중에는 자동으로 감속/가속해서 같은 속도로 돈다
        else if (blinker != 0 && nextInterDist > 0f && nextInterDist < turnSlowDistance)
            target = Mathf.Min(target, turnCruiseSpeed);      // 깜빡이를 켠 채 교차로에 다가가면 미리 감속
        if (traffic.HasKaiju) target = Mathf.Min(target, kaijuSpeedCap);       // 괴수가 길을 막고 있으면 차들이 속도를 줄인다
        speed = Mathf.MoveTowards(speed, target, (rage.Active && speed > target ? 40f : 20f) * dt);     // 분노 중에는 빨라진 만큼 브레이크도 강하게
        float prevDist = dist;
        dist += speed * dt;
        if (turning) dist = Mathf.Min(dist, turnCenterRoad - 8f);   // 회전 중에는 교차로 중심을 넘어가지 않는다 (중심에서 제자리 회전)
        worldMove = dist - prevDist;                                // 이번 프레임에 월드가 실제로 다가온 거리 (보행자도 이만큼만 이동)
        session.AddDistance(dist - prevDist);

        // 깜빡이: Q 좌 / E 우 (다시 누르면 해제)
        if (Keybinds.Pressed(kb, Keybinds.BlinkLeft)) road.ToggleBlinker(-1);
        if (Keybinds.Pressed(kb, Keybinds.BlinkRight)) road.ToggleBlinker(1);

        session.Tick(dt);
        session.TickHeal(dt);

        // 보행자: 생성, 이동, 횡단, 충돌 판정 (회전 중에는 월드가 실제로 다가온 만큼만 움직인다)
        peds.PlayerSpeed = speed;
        peds.Tick(dt, elapsed, turning ? worldMove : speed * dt, carX, turning, SpawnZ);
        UpdateTraffic(dt, turning ? worldMove : speed * dt);
        UpdateGoals();
        UpdateAmbience(dt);
        UpdateShift(dt);                     // 영업 시간이 다 되면 새 손님을 막고, 손님이 없으면 영업 종료
        if (!gameOver) UpdateEvents(dt, turning ? worldMove : speed * dt);
        UpdateRage(dt);
        if (!gameOver)
        {
            UpdateMinigun(dt);           // 같은 프레임에 끝났으면 새 사건/처치가 더 일어나지 않게
            UpdatePolice(dt);
            UpdateEncounters(dt);
        }
        UpdateNews(dt, kb);
        if (!gameOver) UpdateTaxi(dt);       // 같은 프레임에 게임오버가 났으면 대화/정산을 시작하지 않는다
        if (!gameOver) UpdateIntersection(dt);
        UpdateWorldVisuals();
    }

    void LateUpdate()
    {
        if (cam == null) return;
        sound.Tick(speed, maxSpeed, road.Blinker, gameOver || flow != GameFlow.Playing);
        if (!gameOver) sound.StopStinger();
        sound.TickMusic(flow == GameFlow.Ending ? 0f : weather.Rain, (flow == GameFlow.Paused || gameOver) ? 0.45f : 1f, MusicTrack());
        Vector3 off = shakeEnabled && Time.unscaledTime < shakeUntil ? (Vector3)(Random.insideUnitCircle * shakeHit) : Vector3.zero;
        cam.transform.position = new Vector3(carX - 0.4f, camHeight, 0f) + off;
        float roll = -lateralVel / maxLateral * 2.5f;
        camYaw = Mathf.MoveTowardsAngle(camYaw, camYawTarget, yawSpeed * Time.unscaledDeltaTime);
        camPitch = Mathf.MoveTowards(camPitch, camPitchTarget, 120f * Time.unscaledDeltaTime);
        cam.transform.rotation = Quaternion.Euler(camPitch, camYaw, roll);
        // 대화 중에는 손님이 멀수록 더 확대해서, 떨어진 곳에 태워도 손님이 화면에 크게 보인다
        float talkFov = TalkFov();
        float fovTarget = taxi.State == TaxiState.Talking ? talkFov : 70f;
        cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, fovTarget, 120f * Time.unscaledDeltaTime);

        // 옆 시점(손님 대화) 중에는 손/핸들/대시보드를 숨긴다
        cabinVisible = taxi.State != TaxiState.Talking && Mathf.Abs(camYaw) < 4f;
        if (viewRoot != null && viewRoot.gameObject.activeSelf != cabinVisible) viewRoot.gameObject.SetActive(cabinVisible);
        if (cabinVisible && navScreen != null)
        {
            LayoutCabin();
            UpdateNav();
            UpdateMinigunView();
        }

        UpdateRageTint();

        // 핸들 회전 → 손 위치가 따라 움직이므로 IK 는 그 뒤에 푼다
        if (wheelSpin != null) wheelSpin.localRotation = Quaternion.Euler(0f, 0f, -steerAngle);
        if (arms3D && cabinVisible)
        {
            UpdateGrip(armLeft);
            UpdateGrip(armRight);
            SolveArm(armLeft);
            SolveArm(armRight);
            OrientHand(armLeft);
            OrientHand(armRight);
            CurlFingers(armLeft);
            CurlFingers(armRight);
        }
    }
}
