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
    public Vector2 curbWait = new Vector2(0.3f, 0.6f);

    [Tooltip("튀어나오는 사람의 달리기 속도(m/s)")]
    public Vector2 dartSpeed = new Vector2(4f, 6f);

    [Tooltip("멀리서 이미 건너는 사람이 움직이기 시작하는 거리(m)")]
    public Vector2 farTrigger = new Vector2(45f, 80f);

    [Tooltip("멀리서 건너는 사람의 걷는 속도(m/s)")]
    public Vector2 crossSpeed = new Vector2(1.2f, 2.4f);

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
    public Vector3 navPos = new Vector3(0.48f, -0.38f, 0.70f);

    [Tooltip("내비 화면 크기(m, 가로/세로)")]
    public Vector2 navSize = new Vector2(0.26f, 0.16f);

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

    [Header("Sound (코드로 만든 효과음, 외부 에셋 없음)")]
    public bool soundOn = true;
    [Range(0f, 1f)]
    public float masterVolume = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("엔진 소리 크기. 효과음보다 작게 둔다")]
    public float engineVolume = 0.25f;

    const float RimTube = 0.018f;            // 핸들 림 단면 반지름(m)

    public const int RebaseBlocks = 16;             // 회전이 끝나면 도로 좌표를 이 번째 교차로로 옮긴다(구간 번호가 음수가 되지 않게, float 정밀도를 위해 작게)

    public const float StripLen = 4f;

    const int StripCount = 40;

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

    Transform destMarker;                // 목적지 표지 기둥 (손님을 태운 동안만 보인다)

    void Awake()
    {
        whiteTex = MakeTexture(false);
        circleTex = MakeTexture(true);
        peds = new PedestrianManager(this, transform, MakePedSprite());
        peds.Hit += OnPedHit;
        peds.NearMiss += OnPedNearMiss;
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
                dash = Block("Dash")
            };
            s.dash.GetComponent<Renderer>().sharedMaterial = dashMat;
            for (int k = 0; k < 2; k++) s.bldg[k] = Block("Bldg");
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

        BuildBackdrop();
        BuildCabin();
        CreateSound();

        // 목적지 표지 기둥 (손님을 태운 동안만 보인다)
        destMarker = Block("DestMarker");
        destMarker.GetComponent<Renderer>().sharedMaterial = dashMat;
        destMarker.gameObject.SetActive(false);

        ResetGame();
    }

    // ---------- 게임 ----------

    void ResetGame()
    {
        peds.Reset(SpawnZ);                  // 시작부터 인도에 사람이 있다
        carX = lateralVel = steerAngle = 0f;
        speed = baseSpeed;
        dist = elapsed = 0f;
        session.Reset();
        ResetFlowState();
        popupUntil = 0f;

        taxi.Reset();
        camYaw = camYawTarget = 0f;
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
        var kb = Keyboard.current;
        if (kb == null) return;

        // 타이틀: 월드는 멈춰 있고 Space/Enter 로 시작한다
        if (flow == GameFlow.Title)
        {
            Time.timeScale = 0f;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) StartGame();
            UpdateWorldVisuals();
            return;
        }

        // 일시정지: Esc 또는 P (게임오버 화면에서는 쓰지 않는다)
        if (!gameOver && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
            flow = flow == GameFlow.Paused ? GameFlow.Playing : GameFlow.Paused;
        if (flow == GameFlow.Paused)
        {
            Time.timeScale = 0f;
            if (kb.rKey.wasPressedThisFrame) StartGame();
            UpdateWorldVisuals();
            return;
        }

        if (gameOver)
        {
            SaveResultOnce();
            road.ClearFade();                // 회전 장면 교체 중에 끝나도 GAME OVER 화면이 어둡게 남지 않게
            if (kb.rKey.wasPressedThisFrame) StartGame();
            UpdateWorldVisuals();
            return;
        }

        // 손님과 대화 중: 월드는 멈춰 있고 Space 로 태우거나 X 로 거절한다
        if (taxi.State == TaxiState.Talking)
        {
            session.Tick(Time.deltaTime);                            // 피격 직후 정차해도 붉은 화면이 남지 않게
            bool turned = Mathf.Abs(Mathf.DeltaAngle(camYaw, camYawTarget)) < 3f;
            if (turned && kb.spaceKey.wasPressedThisFrame) AcceptPassenger();
            else if (turned && kb.xKey.wasPressedThisFrame) DeclinePassenger();
            UpdateWorldVisuals();
            return;
        }

        float dt = Time.deltaTime;
        elapsed += dt;

        // 조향 (관성)
        float input = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        if (turning) input = 0f;                              // 회전 중에는 차가 알아서 교차로 중앙으로 간다
        lateralVel = Mathf.MoveTowards(lateralVel, input * maxLateral, lateralAccel * dt);
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
        bool brake = kb.sKey.isPressed || kb.downArrowKey.isPressed;
        float target = Mathf.Min(maxSpeed, baseSpeed + elapsed * speedRamp) * (brake ? brakeFactor : 1f);
        if (turning) target = turnCruiseSpeed;                // 회전 중에는 자동으로 감속/가속해서 같은 속도로 돈다
        else if (blinker != 0 && nextInterDist > 0f && nextInterDist < turnSlowDistance)
            target = Mathf.Min(target, turnCruiseSpeed);      // 깜빡이를 켠 채 교차로에 다가가면 미리 감속
        speed = Mathf.MoveTowards(speed, target, 20f * dt);
        float prevDist = dist;
        dist += speed * dt;
        if (turning) dist = Mathf.Min(dist, turnCenterRoad - 8f);   // 회전 중에는 교차로 중심을 넘어가지 않는다 (중심에서 제자리 회전)
        worldMove = dist - prevDist;                                // 이번 프레임에 월드가 실제로 다가온 거리 (보행자도 이만큼만 이동)
        session.AddDistance(dist - prevDist);

        // 깜빡이: Q 좌 / E 우 (다시 누르면 해제)
        if (kb.qKey.wasPressedThisFrame) road.ToggleBlinker(-1);
        if (kb.eKey.wasPressedThisFrame) road.ToggleBlinker(1);

        session.Tick(dt);

        // 보행자: 생성, 이동, 횡단, 충돌 판정 (회전 중에는 월드가 실제로 다가온 만큼만 움직인다)
        peds.Tick(dt, elapsed, turning ? worldMove : speed * dt, carX, turning, SpawnZ);
        if (!gameOver) UpdateTaxi(dt);       // 같은 프레임에 게임오버가 났으면 대화/정산을 시작하지 않는다
        if (!gameOver) UpdateIntersection(dt);
        UpdateWorldVisuals();
    }

    void LateUpdate()
    {
        if (cam == null) return;
        sound.Tick(speed, maxSpeed, road.Blinker, gameOver || flow != GameFlow.Playing);
        Vector3 off = Time.unscaledTime < shakeUntil ? (Vector3)(Random.insideUnitCircle * shakeHit) : Vector3.zero;
        cam.transform.position = new Vector3(carX - 0.4f, camHeight, 0f) + off;
        float roll = -lateralVel / maxLateral * 2.5f;
        camYaw = Mathf.MoveTowardsAngle(camYaw, camYawTarget, yawSpeed * Time.unscaledDeltaTime);
        cam.transform.rotation = Quaternion.Euler(0f, camYaw, roll);
        float fovTarget = taxi.State == TaxiState.Talking ? sideFov : 70f;
        cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, fovTarget, 120f * Time.unscaledDeltaTime);

        // 옆 시점(손님 대화) 중에는 손/핸들/대시보드를 숨긴다
        cabinVisible = taxi.State != TaxiState.Talking && Mathf.Abs(camYaw) < 4f;
        if (viewRoot != null && viewRoot.gameObject.activeSelf != cabinVisible) viewRoot.gameObject.SetActive(cabinVisible);
        if (cabinVisible && navScreen != null)
        {
            LayoutCabin();
            UpdateNav();
        }

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
