using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 프로토타입 3: "앞유리 너머로 무단횡단자를 먼저 알아보고 핸들로 피하는 게 재미있는가?"
// 프로토타입이라 속도를 위해 의도적으로 한 파일에 모아 둔 코드. 빈 씬에 이 컴포넌트 하나만 붙이면 도로/보행자/차 내부/HUD 전부 코드로 생성된다.
// 조작: A/D 또는 ←/→ = 조향, S 또는 ↓ = 브레이크, R = 재시작(게임오버 후)
// 원통 맵: 차는 제자리, 월드가 다가오며 앞쪽일수록 y = -curvature * z^2 로 아래로 휜다 (동물의 숲 방식).
// 에셋: 도시 타일(2D, 건물 정면은 도로를 향해 고정 / 사람처럼 카메라를 쳐다보지 않음), arms.fbx (3D 팔, 코드 IK 로 핸들을 잡음)
public class ProtoDrive : MonoBehaviour
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
    [Tooltip("이 속도(m/s) 이하일 때만 깜빡이 방향으로 회전한다. 넘으면 직진")]
    public float turnSpeed = 10f;

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
    [Tooltip("목적지까지 거리(m) 범위")]
    public Vector2 tripDistance = new Vector2(250f, 600f);
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

    class Ped
    {
        public Transform t;
        public SpriteRenderer sr;
        public float x, z, dir, walkSpeed, pauseLeft = -1f, minLat = 999f, fallT = -1f, phase;
        public bool scored, crossed;      // crossed: 도로에 나온 적이 있는 사람만 충돌/니어미스 점수 대상
        public Color baseColor;
        public int kind;                  // 0 인도 보행자, 1 멀리서 이미 건너는 사람, 2 가까이서 갑자기 튀어나오는 사람
        public int st;                    // 0 인도 걷기, 1 연석으로 이동, 2 연석에서 멈칫, 3 도로 건너는 중
        public float side;                // 인도 쪽 (-1 왼쪽, +1 오른쪽)
        public float walkZ;               // 인도를 따라 걷는 속도(월드 기준, +는 멀어지는 쪽)
        public float triggerZ, waitTime, curbX;
        public bool caller;               // 택시를 부르는 손님 (머리 위에 !)
    }

    enum TaxiState { Cruise, Talking, Carrying }

    class Side
    {
        public SpriteRenderer upper, ground, cornice, door, prop;          // 도로 쪽 정면
        public SpriteRenderer fUpper, fGround, fCornice;                   // 다가오는 쪽(-z) 면
        public int floors = 3, pal;
        public float setback;
        public float propScale = 1f;
        public bool wantDoor, wantProp;   // 이 건물이 문/소품을 가졌는지 (가까울 때만 실제로 켠다)
        public bool far;                  // 마지막으로 적용한 원거리 단순화 상태
    }

    class Strip
    {
        public Transform road, swL, swR, dash;
        public Transform[] bldg = new Transform[2];   // 건물 몸통(상자). 면마다 스프라이트를 붙인다
        public Side[] sides = { new Side(), new Side() };
        public int lastIdx = int.MinValue;
    }

    class Finger
    {
        public Transform[] bones;      // 1,2,3 마디
        public Transform tip;          // _end
        public Quaternion[] rest;
        public bool thumb;             // 최대 굽힘각은 fingerCurl / thumbCurl
        public Vector3 axisLocal;      // 손목 로컬 굽힘축 (+각도로 돌리면 손바닥 쪽으로 굽는다)
    }

    class ArmIK
    {
        public Transform upper, fore, wrist, twist0, twist1;
        public float lenU, lenF;
        public Transform target;
        public float baseAngle;        // 핸들이 안 돌았을 때 잡는 위치 (도)
        public float gripAngle;        // 지금 잡는 위치 (라디안, 핸들 로컬)
        public float thumbSense = 1f;  // 엄지가 향할 림 방향(반시계 +1 / 시계 -1). 핸들을 돌려도 손이 갑자기 뒤집히지 않게 고정
        public Quaternion restUpper, restFore, restWrist, restTwist0, restTwist1;   // 에셋 원래 자세 (로컬 회전)
        // 에셋 기본 자세에서 잰 손 모양: 손가락 방향 F, 손바닥 방향 P (손목 로컬), 좌우(chir), 손목→쥐는 점(socket)
        public Vector3 fwdLocal, palmLocal, socketK;
        public float chir = 1f;        // cross(F, P) 가 엄지 쪽이면 +1, 반대면 -1 (왼손/오른손)
        public bool handOk;
        public Vector3 gripPoint, wristGoal, fwdD, palmD;   // 이번 프레임 목표
        public Vector3 poleLocal;
        public List<Finger> fingers = new List<Finger>();
    }

    const float RimTube = 0.018f;            // 핸들 림 단면 반지름(m)
    const float StripLen = 4f;
    const int StripCount = 40;
    const float CarHalfWidth = 1.0f;
    const float PedHalfWidth = 0.3f;
    const float TileW = 26f / 32f;           // 타일 좌우 3px 테두리를 잘라 낸 폭 (이음매의 검은 줄/간격 제거)
    const int TileCols = 4;                  // 한 구간(4m)에 가로 4칸 -> 타일 1칸 = 정확히 1m
    const float TileScale = StripLen / (TileCols * TileW);

    readonly List<Ped> peds = new List<Ped>();
    readonly Strip[] strips = new Strip[StripCount];

    Camera cam;
    Shader litShader;
    Sprite pedSprite;
    Texture2D whiteTex, circleTex;
    Material roadA, roadB, swA, swB, dashMat, darkMat;
    Material[] bodyMats;

    // 도시 타일 스프라이트
    Sprite[] winTiles = new Sprite[4], doorTiles = new Sprite[4], shopTiles = new Sprite[3];
    Sprite corniceTile, lampSprite, lightRedSprite, lightGreenSprite, trashSprite, mailSprite;
    bool hasCity;

    // 3D 차 내부
    Transform viewRoot, wheelSpin;
    ArmIK armLeft, armRight;
    bool arms3D;

    float carX, lateralVel, speed, dist, elapsed, nextSpawn, distScore;
    float steerAngle;
    int lives, score, nearMisses, hits;
    float hitStopUntil, shakeUntil, hurtFlash, invuln, popupUntil;
    string popupText = "";
    Color popupColor = Color.white;
    bool gameOver;

    // 택시 상태
    TaxiState taxi;
    Ped caller;
    float callerTimer, camYaw, camYawTarget, tripMeters, fareMul = 1f, destDist, reactUntil;
    int fareTotal, trips;
    string paxName = "", paxLine = "", destName = "", reactText = "";
    Color paxSkin, paxHair, paxShirt;
    Transform destMarker;
    bool cabinVisible = true;

    // 교차로 / 회전 상태
    int blinker;                         // -1 좌, 0 없음, +1 우
    int heading, gx, gy, citySalt, handledInter = -1, pendingTurn;
    float turnT = -1f, fade, nextInterDist;
    bool turnApplied;
    int hudInterDist = -1, hudInterBlinker = -2, hudInterHeading = -1, hudInterGx = int.MinValue, hudInterGy = int.MinValue;
    string hudInterText = "";
    const float TurnFadeOut = 0.25f, TurnFadeIn = 0.35f;
    static readonly int[] HeadX = { 0, 1, 0, -1 };
    static readonly int[] HeadY = { 1, 0, -1, 0 };
    static readonly string[] HeadNames = { "북", "동", "남", "서" };

    static readonly string[] PaxNames = { "김 사장님", "박 할머니", "이 학생", "최 아저씨", "정 부장님", "한 아주머니" };
    static readonly string[] DestNames = { "시청", "중앙역", "대학병원", "공항", "호텔", "시장", "도서관", "공원 입구" };
    static readonly string[] HelloLines =
    {
        "택시! 여기요! {0}까지 가 주세요.",
        "기사님, {0}으로 빨리 부탁드려요.",
        "{0}까지 가 주세요. 늦었어요!",
        "어휴, 잡았다. {0}요, 천천히 안전하게요."
    };
    static readonly string[] ReactLines = { "꺄악!!", "기사님! 조심하세요!!", "방금 사람 쳤어요!?", "으아악!!", "뭐예요 지금!?" };
    static readonly Color[] SkinTones =
    {
        new Color(0.96f, 0.80f, 0.66f), new Color(0.88f, 0.68f, 0.52f), new Color(0.72f, 0.52f, 0.38f), new Color(0.98f, 0.86f, 0.76f)
    };
    static readonly Color[] HairTones =
    {
        new Color(0.10f, 0.08f, 0.07f), new Color(0.32f, 0.20f, 0.12f), new Color(0.75f, 0.72f, 0.70f), new Color(0.55f, 0.35f, 0.15f)
    };

    float SpawnZ => Mathf.Min(StripCount * StripLen - 12f, Mathf.Sqrt(camHeight / curvature) + Mathf.Sqrt(1.75f / curvature) + 8f);
    float Drop(float z) => -curvature * z * z;

    void Awake()
    {
        whiteTex = MakeTexture(false);
        circleTex = MakeTexture(true);
        pedSprite = MakePedSprite();
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

        // 목적지 표지 기둥 (손님을 태운 동안만 보인다)
        destMarker = Block("DestMarker");
        destMarker.GetComponent<Renderer>().sharedMaterial = dashMat;
        destMarker.gameObject.SetActive(false);

        ResetGame();
    }

    // ---------- 에셋 → 스프라이트 ----------

    Sprite Tile(Texture2D tex, int px, int pyFromTop, int w, int h, float pivotY = 0f)
    {
        tex.filterMode = FilterMode.Point;
        var rect = new Rect(px, tex.height - pyFromTop - h, w, h);
        return Sprite.Create(tex, rect, new Vector2(0.5f, pivotY), 32f, 0, SpriteMeshType.FullRect);
    }

    void BuildCitySprites()
    {
        for (int i = 0; i < 4; i++)
        {
            winTiles[i] = Tile(buildingTiles, 128 + 3, i * 64, 26, 64);    // 창문(회색/베이지/빨강/분홍), 좌우 3px 테두리 제외
            doorTiles[i] = Tile(buildingTiles, 64, i * 64, 32, 64);    // 문
        }
        for (int i = 0; i < 3; i++) shopTiles[i] = Tile(buildingTiles, 128 + i * 32 + 3, 256, 26, 64);   // 1층 상점
        corniceTile = Tile(buildingTiles, 480 + 3, 0, 26, 32);            // 지붕 장식

        lampSprite = Tile(decoTiles, 192, 32, 32, 96);
        lightRedSprite = Tile(decoTiles, 160, 0, 32, 128);
        lightGreenSprite = Tile(decoTiles, 224, 0, 32, 128);
        trashSprite = Tile(decoTiles, 96, 0, 32, 32);
        mailSprite = Tile(decoTiles, 128, 0, 32, 32);
    }

    void BuildBackdrop()
    {
        if (cityBackground == null) return;
        cityBackground.filterMode = FilterMode.Point;
        var sp = Sprite.Create(cityBackground, new Rect(0, 0, cityBackground.width, cityBackground.height), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);

        const float z = 350f;
        float width = 2f * z * Mathf.Tan(32.5f * Mathf.Deg2Rad);          // 한 장이 가로 약 65도
        float scale = width / (cityBackground.width / 100f);
        float bottom = z * Mathf.Tan(-1f * Mathf.Deg2Rad);

        // 굽은 도로 때문에 배경 스프라이트 아래로 비어 보이는 틈을, 스카이라인 바닥색 판으로 메운다
        var fill = new GameObject("BackdropFill");
        fill.transform.SetParent(cam.transform, false);
        fill.transform.localPosition = new Vector3(0f, bottom + 0.5f, z + 1f);
        fill.transform.localScale = new Vector3(width * 4f, 900f, 1f);
        var fsr = fill.AddComponent<SpriteRenderer>();
        fsr.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 1f), whiteTex.width);
        fsr.color = new Color(98f / 255f, 122f / 255f, 163f / 255f);
        fsr.sortingOrder = -11;
        for (int k = -1; k <= 1; k++)
        {
            var go = new GameObject("Backdrop" + k);
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(k * width * 0.998f, bottom, z);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = -10;
        }
    }

    SpriteRenderer MakeSR(string n, int order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }

    SpriteRenderer MakeTiled(string n, int order)
    {
        var sr = MakeSR(n, order);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        return sr;
    }

    Transform Block(string blockName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = blockName;
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    Material Mat(Color c)
    {
        var m = new Material(litShader);
        m.SetColor("_BaseColor", c);
        m.color = c;
        return m;
    }

    // ---------- 차 내부 (3D 핸들 + 3D 팔 IK) ----------

    void BuildCabin()
    {
        var rootGo = new GameObject("CabinView");
        rootGo.transform.SetParent(cam.transform, false);
        viewRoot = rootGo.transform;

        // 대시보드: 화면 아래쪽을 가리는 어두운 판
        var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dash.name = "Dashboard";
        Destroy(dash.GetComponent<Collider>());
        dash.transform.SetParent(viewRoot, false);
        dash.transform.localPosition = new Vector3(0f, -0.62f, 0.85f);
        dash.transform.localScale = new Vector3(2.4f, 0.14f, 1.1f);
        dash.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.14f, 0.14f, 0.17f));

        // 핸들: 토러스 + 스포크
        var pivot = new GameObject("Wheel").transform;
        pivot.SetParent(viewRoot, false);
        pivot.localPosition = wheelCenter;
        Vector3 awayFromDriver = new Vector3(0f, -0.55f, 0.83f);
        pivot.localRotation = Quaternion.LookRotation(awayFromDriver, Vector3.up);

        wheelSpin = new GameObject("WheelSpin").transform;
        wheelSpin.SetParent(pivot, false);

        var ring = new GameObject("Rim");
        ring.transform.SetParent(wheelSpin, false);
        ring.AddComponent<MeshFilter>().sharedMesh = MakeTorus(wheelRadius, RimTube, 36, 10);
        ring.AddComponent<MeshRenderer>().sharedMaterial = darkMat;

        for (int k = 0; k < 2; k++)
        {
            var sp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(sp.GetComponent<Collider>());
            sp.transform.SetParent(wheelSpin, false);
            sp.transform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            sp.transform.localScale = new Vector3(wheelRadius * 2f, 0.02f, 0.02f);
            sp.GetComponent<Renderer>().sharedMaterial = darkMat;
        }
        var hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(hub.GetComponent<Collider>());
        hub.transform.SetParent(wheelSpin, false);
        hub.transform.localScale = new Vector3(0.07f, 0.07f, 0.04f);
        hub.GetComponent<Renderer>().sharedMaterial = darkMat;

        // 팔
        arms3D = use3DArms && armsModel != null && SetupArms();
    }

    bool SetupArms()
    {
        GameObject go;
        try
        {
            go = Instantiate(armsModel, viewRoot);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ProtoDrive: arms.fbx 를 생성하지 못해 2D 팔로 대체합니다. (" + e.Message + ")  씬의 Arms Model 을 arms.fbx 로 다시 지정하세요.");
            return false;
        }
        go.name = "Arms";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        var bones = new Dictionary<string, Transform>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        string[] need = { "bicep.l", "forearm.l", "wrist.l", "bicep.r", "forearm.r", "wrist.r" };
        foreach (var n in need)
        {
            if (!bones.ContainsKey(n))
            {
                Debug.LogWarning("ProtoDrive: arms.fbx 에서 본 '" + n + "' 을 찾지 못해 2D 팔로 대체합니다.");
                Destroy(go);
                return false;
            }
        }

        // 재질: URP 에서 분홍색으로 깨지는 것을 막기 위해 직접 만든 Lit 재질로 교체
        var mat = new Material(litShader);
        mat.SetColor("_BaseColor", Color.white);
        if (armAlbedo != null)
        {
            mat.SetTexture("_BaseMap", armAlbedo);
            mat.mainTexture = armAlbedo;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
        }

        // 1) 팔 길이에 맞춰 스케일 (모델 단위를 모르므로 본 거리로 측정)
        float rawLen = Vector3.Distance(bones["bicep.l"].position, bones["forearm.l"].position)
                     + Vector3.Distance(bones["forearm.l"].position, bones["wrist.l"].position);
        if (rawLen < 1e-5f)
        {
            Destroy(go);
            return false;
        }
        go.transform.localScale = Vector3.one * (armLength / rawLen);

        // 2) 두 어깨를 카메라 X축에 맞추도록 Y회전, 어깨 중점을 shoulderCenter 로 이동
        Vector3 bl = viewRoot.InverseTransformPoint(bones["bicep.l"].position);
        Vector3 br = viewRoot.InverseTransformPoint(bones["bicep.r"].position);
        Vector3 axis = br - bl;
        axis.y = 0f;
        if (axis.sqrMagnitude > 1e-8f)
            go.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.right);
        bl = viewRoot.InverseTransformPoint(bones["bicep.l"].position);
        br = viewRoot.InverseTransformPoint(bones["bicep.r"].position);
        go.transform.localPosition += shoulderCenter - (bl + br) * 0.5f;

        // 3) 화면 왼쪽에 있는 쪽을 왼팔로 취급
        bl = viewRoot.InverseTransformPoint(bones["bicep.l"].position);
        br = viewRoot.InverseTransformPoint(bones["bicep.r"].position);
        string leftSide = bl.x <= br.x ? "l" : "r";
        string rightSide = leftSide == "l" ? "r" : "l";

        armLeft = MakeArm(bones, leftSide, 180f - handAngle, new Vector3(-0.5f, -1f, -0.3f));
        armRight = MakeArm(bones, rightSide, handAngle, new Vector3(0.5f, -1f, -0.3f));
        return true;
    }

    ArmIK MakeArm(Dictionary<string, Transform> bones, string side, float angleDeg, Vector3 pole)
    {
        var a = new ArmIK
        {
            upper = bones["bicep." + side],
            fore = bones["forearm." + side],
            wrist = bones["wrist." + side],
            poleLocal = pole,
            baseAngle = angleDeg,
            gripAngle = angleDeg * Mathf.Deg2Rad,
            // 기준 위치에서 반시계 접선이 핸들 위쪽을 향하면 +1 (오른손 2시 = +1, 왼손 10시 = -1)
            thumbSense = Mathf.Cos(angleDeg * Mathf.Deg2Rad) >= 0f ? 1f : -1f
        };
        bones.TryGetValue("forearm.Twist0." + side, out a.twist0);
        bones.TryGetValue("forearm.Twist1." + side, out a.twist1);
        a.restUpper = a.upper.localRotation;
        a.restFore = a.fore.localRotation;
        a.restWrist = a.wrist.localRotation;
        if (a.twist0 != null) a.restTwist0 = a.twist0.localRotation;
        if (a.twist1 != null) a.restTwist1 = a.twist1.localRotation;
        a.lenU = Vector3.Distance(a.upper.position, a.fore.position);
        a.lenF = Vector3.Distance(a.fore.position, a.wrist.position);

        var tgt = new GameObject("GripTarget." + side).transform;
        tgt.SetParent(wheelSpin, false);
        a.target = tgt;

        // 손 모양 측정 (에셋 기본 자세). 손가락은 기본 자세에서 살짝 굽어 있으므로, 가운뎃손가락 끝이 처진 쪽이 손바닥이다.
        // socket 본 = 무기 손잡이가 놓이는 자리(주먹 안). 여기에 림을 넣으면 주먹이 림을 감싼다.
        bones.TryGetValue("finger_middle1." + side, out var mid1);
        bones.TryGetValue("finger_index1." + side, out var idx1);
        bones.TryGetValue("finger_pinky1." + side, out var pinky1);
        bones.TryGetValue("finger_middle3." + side + "_end", out var midTip);
        bones.TryGetValue("socket." + side, out var socket);
        if (mid1 != null && idx1 != null && pinky1 != null && midTip != null && mid1.IsChildOf(a.wrist))
        {
            Vector3 W = a.wrist.position;
            Vector3 F = (mid1.position - W).normalized;
            Vector3 P = Vector3.ProjectOnPlane(midTip.position - W, F);
            if (P.sqrMagnitude > 1e-10f)
            {
                P.Normalize();
                Vector3 L = Vector3.Cross(F, P);
                a.chir = Vector3.Dot(L, idx1.position - pinky1.position) >= 0f ? 1f : -1f;
                Vector3 off = socket != null && socket.IsChildOf(a.wrist)
                    ? socket.position - W
                    : (mid1.position - W) * 0.9f + P * Vector3.Distance(mid1.position, W) * 0.4f;
                a.socketK = new Vector3(Vector3.Dot(off, F), Vector3.Dot(off, P), Vector3.Dot(off, L));
                a.fwdLocal = a.wrist.InverseTransformDirection(F);
                a.palmLocal = a.wrist.InverseTransformDirection(P);
                a.handOk = true;
            }
        }
        if (!a.handOk)
            Debug.LogWarning("ProtoDrive: 손가락 본으로 손 모양을 재지 못해 손 방향/손가락 쥐기를 건너뜁니다. (" + side + ")");

        // 손가락: 마디 3개 + 끝 본. 굽힘축 = 첫 마디 방향 x 손바닥 방향 (+각도면 손바닥 쪽으로 굽는다)
        string[] names = { "pinky", "ring", "middle", "index", "thumb" };
        foreach (var n in names)
        {
            var f = new Finger { bones = new Transform[3], rest = new Quaternion[3] };
            bool ok = true;
            for (int k = 0; k < 3 && ok; k++)
            {
                string key = "finger_" + n + (k + 1) + "." + side;
                if (bones.TryGetValue(key, out var b)) { f.bones[k] = b; f.rest[k] = b.localRotation; }
                else ok = false;
            }
            string endKey = "finger_" + n + "3." + side + "_end";
            if (!ok || !bones.TryGetValue(endKey, out f.tip)) continue;
            f.thumb = n == "thumb";
            if (a.handOk)
            {
                Vector3 P = a.wrist.TransformDirection(a.palmLocal);
                Vector3 ax = Vector3.Cross(f.bones[1].position - f.bones[0].position, P);
                if (ax.sqrMagnitude < 1e-10f) ax = Vector3.Cross(a.wrist.TransformDirection(a.fwdLocal), P);
                f.axisLocal = a.wrist.InverseTransformDirection(ax.normalized);
            }
            a.fingers.Add(f);
        }
        return a;
    }

    // 손이 잡는 위치와 손 방향 목표.
    // - 핸들이 steerAngle 만큼 돌아도 손은 gripFollow 비율만 따라가고, 나머지는 림 위를 미끄러진다 (핸들은 -steerAngle 만큼 돈다)
    // - 막대를 쥔 주먹: 네 손가락이 림 접선을 따라 쌓이고, 손가락 방향은 림 단면(바깥 R ~ 앞유리 N) 안에서 gripRoll 각도
    // - 손목 위치 = 림 중심선 위의 점 - (손목→socket). 그래서 socket(주먹 안)에 림이 들어간다
    void UpdateGrip(ArmIK a)
    {
        float phi = a.baseAngle + (1f - gripFollow) * steerAngle;
        float r = phi * Mathf.Deg2Rad;
        a.gripAngle = r;
        Vector3 radialL = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f);
        a.target.localPosition = radialL * wheelRadius;
        a.gripPoint = a.target.position;
        if (!a.handOk)
        {
            a.wristGoal = a.gripPoint;
            return;
        }

        Vector3 R = wheelSpin.TransformDirection(radialL);
        Vector3 N = wheelSpin.forward;
        Vector3 thumbDir = wheelSpin.TransformDirection(new Vector3(-Mathf.Sin(r), Mathf.Cos(r), 0f)) * a.thumbSense * (thumbUp ? 1f : -1f);
        float th = gripRoll * Mathf.Deg2Rad;
        a.fwdD = (R * Mathf.Cos(th) + N * Mathf.Sin(th)).normalized;
        a.palmD = a.chir * Vector3.Cross(thumbDir, a.fwdD);
        Vector3 L = Vector3.Cross(a.fwdD, a.palmD);
        Vector3 off = a.fwdD * a.socketK.x + a.palmD * a.socketK.y + L * a.socketK.z;
        Vector3 nudge = a.fwdD * gripNudge.x + a.palmD * gripNudge.y + thumbDir * gripNudge.z;
        a.wristGoal = a.gripPoint - off + nudge;
    }

    // 2본 IK: 어깨→팔꿈치→손목. 매 프레임 에셋 원래 자세에서 시작하므로 회전이 누적되어 틀어지지 않는다.
    // 본 방향 규약을 몰라도 되도록 실제 자식 본 위치를 기준으로 회전시킨다.
    void SolveArm(ArmIK a)
    {
        a.upper.localRotation = a.restUpper;
        a.fore.localRotation = a.restFore;
        a.wrist.localRotation = a.restWrist;
        if (a.twist0 != null) a.twist0.localRotation = a.restTwist0;
        if (a.twist1 != null) a.twist1.localRotation = a.restTwist1;

        Vector3 S = a.upper.position;
        Vector3 T = a.wristGoal;
        float d0 = (T - S).magnitude;
        if (d0 < 1e-5f) return;
        float d = Mathf.Clamp(d0, Mathf.Abs(a.lenU - a.lenF) + 0.01f, a.lenU + a.lenF - 0.005f);
        Vector3 dir = (T - S) / d0;
        T = S + dir * d;

        float along = (a.lenU * a.lenU - a.lenF * a.lenF + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, a.lenU * a.lenU - along * along));
        Vector3 pole = viewRoot.TransformDirection(a.poleLocal.normalized);
        Vector3 perp = pole - Vector3.Dot(pole, dir) * dir;
        perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.down;
        Vector3 elbow = S + dir * along + perp * h;

        a.upper.rotation = Quaternion.FromToRotation(a.fore.position - S, elbow - S) * a.upper.rotation;
        a.fore.rotation = Quaternion.FromToRotation(a.wrist.position - a.fore.position, T - a.fore.position) * a.fore.rotation;
    }

    // 손 방향: 측정해 둔 (손가락 F, 손바닥 P) 를 목표 (fwdD, palmD) 에 그대로 맞춘다. 왼손/오른손은 chir 로 이미 반영됨.
    // 손목이 팔뚝 축으로 돈 만큼을 팔뚝 비틀림 본(Twist0/1)에 1/3 씩 나눠 줘서 팔뚝 피부가 꼬이지 않게 한다.
    void OrientHand(ArmIK a)
    {
        if (!a.handOk) return;
        Vector3 fH = a.wrist.TransformDirection(a.fwdLocal);
        Vector3 pH = a.wrist.TransformDirection(a.palmLocal);
        a.wrist.rotation = Quaternion.LookRotation(a.fwdD, a.palmD) * Quaternion.Inverse(Quaternion.LookRotation(fH, pH)) * a.wrist.rotation;

        if (a.twist0 == null || a.wrist.parent == null) return;
        Vector3 axis = (a.wrist.position - a.fore.position).normalized;
        Vector3 pRest = a.wrist.parent.rotation * (a.restWrist * a.palmLocal);
        Vector3 pNow = a.wrist.TransformDirection(a.palmLocal);
        float tw = Vector3.SignedAngle(Vector3.ProjectOnPlane(pRest, axis), Vector3.ProjectOnPlane(pNow, axis), axis);
        a.twist0.rotation = Quaternion.AngleAxis(tw / 3f, axis) * a.twist0.rotation;
        if (a.twist1 != null) a.twist1.rotation = Quaternion.AngleAxis(tw / 3f, axis) * a.twist1.rotation;
    }

    // 손가락 쥐기: 손바닥 쪽으로 조금씩 굽히다가 손가락 끝이 림 표면에 닿는 순간 멈춘다.
    // 핸들이 돌거나 손 위치가 바뀌어도 매 프레임 다시 찾으므로 림을 파고들거나 허공을 쥐지 않는다.
    void CurlFingers(ArmIK a)
    {
        float surface = RimTube + fingerPad;
        foreach (var f in a.fingers)
        {
            if (!a.handOk)
            {
                for (int k = 0; k < 3; k++) f.bones[k].localRotation = f.rest[k];
                continue;
            }
            Vector3 axis = a.wrist.TransformDirection(f.axisLocal);
            Vector3 maxAng = f.thumb ? thumbCurl : fingerCurl;
            // 최소 minCurl 만큼은 항상 굽히고, 림에 닿지 않는 손가락(림 아래로 삐져나온 새끼/약지 등)은 끝까지 오므린다
            const int steps = 20;
            float best = 1f;
            bool wasOut = false;
            for (int i = 0; i <= steps; i++)
            {
                float s = Mathf.Lerp(minCurl, 1f, i / (float)steps);
                PoseFinger(f, axis, maxAng * s);
                float dist = RimDist(f.tip.position);
                if (dist > surface) wasOut = true;
                else if (wasOut) { best = s; break; }
            }
            PoseFinger(f, axis, maxAng * best);
        }
    }

    static void PoseFinger(Finger f, Vector3 axis, Vector3 ang)
    {
        for (int k = 0; k < 3; k++) f.bones[k].localRotation = f.rest[k];
        for (int k = 0; k < 3; k++)
        {
            float g = k == 0 ? ang.x : (k == 1 ? ang.y : ang.z);
            f.bones[k].rotation = Quaternion.AngleAxis(g, axis) * f.bones[k].rotation;
        }
    }

    // 점에서 림(원형 튜브의 중심선)까지 거리
    float RimDist(Vector3 p)
    {
        Vector3 l = wheelSpin.InverseTransformPoint(p);
        float rho = new Vector2(l.x, l.y).magnitude;
        return new Vector2(rho - wheelRadius, l.z).magnitude;
    }

    static Mesh MakeTorus(float R, float r, int seg, int sides)
    {
        var verts = new Vector3[(seg + 1) * (sides + 1)];
        var tris = new int[seg * sides * 6];
        for (int i = 0; i <= seg; i++)
        {
            float u = i / (float)seg * Mathf.PI * 2f;
            Vector3 c = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f) * R;
            for (int j = 0; j <= sides; j++)
            {
                float v = j / (float)sides * Mathf.PI * 2f;
                Vector3 n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                verts[i * (sides + 1) + j] = c + n * r;
            }
        }
        int t = 0;
        for (int i = 0; i < seg; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
            }
        }
        var m = new Mesh { vertices = verts, triangles = tris };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ---------- 게임 ----------

    void ResetGame()
    {
        foreach (var p in peds) if (p.t != null) Destroy(p.t.gameObject);
        peds.Clear();
        for (int i = 0; i < initialCrowd; i++) SpawnPed(0, Random.Range(6f, SpawnZ));   // 시작부터 인도에 사람이 있다
        carX = lateralVel = steerAngle = 0f;
        speed = baseSpeed;
        dist = elapsed = 0f;
        nextSpawn = 1.2f;
        lives = maxLives;
        score = nearMisses = hits = 0;
        distScore = 0f;
        hurtFlash = invuln = 0f;
        popupUntil = 0f;
        gameOver = false;

        taxi = TaxiState.Cruise;
        caller = null;
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
        camYaw = camYawTarget = 0f;
        fareMul = 1f;
        fareTotal = trips = 0;
        reactUntil = 0f;
        blinker = 0;
        heading = 0;
        gx = 0;
        gy = -1;                                  // 첫 교차로를 지나면 (0, 0)
        citySalt = 0;
        handledInter = -1;
        turnT = -1f;
        fade = 0f;
        foreach (var s in strips) if (s != null) s.lastIdx = int.MinValue;
        if (destMarker != null) destMarker.gameObject.SetActive(false);
    }

    void Update()
    {
        Time.timeScale = (!gameOver && Time.unscaledTime < hitStopUntil) ? 0f : 1f;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (gameOver)
        {
            fade = 0f;                       // 회전 페이드 중에 끝나도 GAME OVER 화면이 어둡게 남지 않게
            turnT = -1f;
            if (kb.rKey.wasPressedThisFrame) ResetGame();
            UpdateWorldVisuals();
            return;
        }

        // 손님과 대화 중: 월드는 멈춰 있고 Space 로 태우거나 X 로 거절한다
        if (taxi == TaxiState.Talking)
        {
            hurtFlash = Mathf.Max(0f, hurtFlash - Time.deltaTime);   // 피격 직후 정차해도 붉은 화면이 남지 않게
            invuln = Mathf.Max(0f, invuln - Time.deltaTime);
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
        lateralVel = Mathf.MoveTowards(lateralVel, input * maxLateral, lateralAccel * dt);
        float limit = roadHalfWidth - CarHalfWidth;
        carX += lateralVel * dt;
        if (Mathf.Abs(carX) > limit)
        {
            carX = Mathf.Clamp(carX, -limit, limit);
            lateralVel = 0f;
        }
        steerAngle = Mathf.Lerp(steerAngle, lateralVel / maxLateral * wheelMaxAngle, dt * 10f);

        // 속도
        bool brake = kb.sKey.isPressed || kb.downArrowKey.isPressed;
        float target = Mathf.Min(maxSpeed, baseSpeed + elapsed * speedRamp) * (brake ? brakeFactor : 1f);
        speed = Mathf.MoveTowards(speed, target, 20f * dt);
        dist += speed * dt;
        distScore += speed * dt * 0.1f;

        // 깜빡이: Q 좌 / E 우 (다시 누르면 해제)
        if (kb.qKey.wasPressedThisFrame) blinker = blinker == -1 ? 0 : -1;
        if (kb.eKey.wasPressedThisFrame) blinker = blinker == 1 ? 0 : 1;

        invuln = Mathf.Max(0f, invuln - dt);
        hurtFlash = Mathf.Max(0f, hurtFlash - dt);

        // 스폰
        if (elapsed >= nextSpawn && peds.Count < maxPeds)
        {
            SpawnPed();
            float interval = Mathf.Max(minSpawnInterval, spawnInterval - elapsed * intervalRamp);
            nextSpawn = elapsed + interval * Random.Range(0.6f, 1.4f);
        }

        UpdatePeds(dt);
        if (!gameOver) UpdateTaxi(dt);       // 같은 프레임에 게임오버가 났으면 대화/정산을 시작하지 않는다
        if (!gameOver) UpdateIntersection(dt);
        UpdateWorldVisuals();
    }

    // ---------- 교차로 / 회전 ----------

    bool IsIntersection(int idx)
    {
        int e = Mathf.Max(intersectionLen + 1, intersectionEvery);
        int m = idx % e;
        if (m < 0) m += e;
        return m < intersectionLen;
    }

    // 교차로를 지나는 순간 깜빡이가 켜져 있고 속도가 충분히 낮으면 회전(페이드 컷)한다.
    void UpdateIntersection(float dt)
    {
        // 손님과 대화를 시작하면 진행 중이던 회전/페이드는 취소한다 (대화 상자가 검은 화면에 가려지지 않게)
        if (taxi == TaxiState.Talking)
        {
            turnT = -1f;
            fade = 0f;
            blinker = 0;
            return;
        }

        // 페이드 컷 진행: 히트스톱(timeScale 0)과 무관하게 실시간으로 센다
        if (turnT >= 0f)
        {
            turnT += Time.unscaledDeltaTime;
            if (!turnApplied && turnT >= TurnFadeOut)
            {
                turnApplied = true;
                ApplyTurn();
            }
            fade = turnT < TurnFadeOut ? turnT / TurnFadeOut : 1f - (turnT - TurnFadeOut) / TurnFadeIn;
            if (turnT >= TurnFadeOut + TurnFadeIn)
            {
                turnT = -1f;
                fade = 0f;
            }
        }

        float period = Mathf.Max(intersectionLen + 1, intersectionEvery) * StripLen;
        float len = intersectionLen * StripLen;
        float roadPos = dist + 8f;                                   // 카메라(z=0)가 놓인 도로 좌표
        int k = Mathf.RoundToInt((roadPos - len * 0.5f) / period);   // 가장 가까운 교차로 번호
        float center = k * period + len * 0.5f;
        float diff = roadPos - center;
        nextInterDist = diff < 0f ? -diff : center + period - roadPos;

        if (turnT < 0f && diff >= -2f && diff < 6f && handledInter != k)
        {
            handledInter = k;
            gx += HeadX[heading];                                    // 이 교차로까지 한 블록 왔다
            gy += HeadY[heading];
            if (blinker != 0)
            {
                if (speed <= turnSpeed)
                {
                    pendingTurn = blinker;
                    turnT = 0f;
                    turnApplied = false;
                    Popup(blinker < 0 ? "좌회전" : "우회전", new Color(0.6f, 0.9f, 1f));
                }
                else
                {
                    Popup("너무 빨라요! 속도를 줄이세요", new Color(1f, 0.6f, 0.3f));
                }
                blinker = 0;
            }
        }
    }

    // 화면이 완전히 어두워진 순간에 방향과 주변을 바꾼다. dist 는 이어서 가므로 새 도로는 그냥 계속되는 직선 도로다.
    void ApplyTurn()
    {
        heading = (heading + pendingTurn + 4) % 4;
        citySalt++;                                                  // 새 거리는 건물 배치가 달라진다
        foreach (var s in strips) s.lastIdx = int.MinValue;          // 건물 다시 구성
        carX = lateralVel = 0f;
        invuln = Mathf.Max(invuln, 0.7f);                            // 페이드 인 동안 어두운 화면에서 맞지 않게

        caller = null;                                               // 사람들은 모두 새로 깐다
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
        foreach (var p in peds) if (p.t != null) Destroy(p.t.gameObject);
        peds.Clear();
        for (int i = 0; i < initialCrowd; i++) SpawnPed(0, Random.Range(6f, SpawnZ));
    }

    // ---------- 택시: 손님 호출 / 정차 / 대화 / 탑승 / 도착 ----------

    void UpdateTaxi(float dt)
    {
        // 손님이 사라졌다(지나쳐서 제거됨)
        if (caller != null && (caller.t == null || !peds.Contains(caller)))
        {
            caller = null;
            callerTimer = Random.Range(callerDelay.x, callerDelay.y);
            if (taxi == TaxiState.Cruise) Popup("손님을 놓쳤다", new Color(0.8f, 0.8f, 0.8f));
        }

        if (taxi == TaxiState.Cruise)
        {
            if (caller == null)
            {
                callerTimer -= dt;
                if (callerTimer <= 0f) MakeCaller();
            }
            else if (speed <= pickupSpeed && caller.z > -2.5f && caller.z < 9f && Mathf.Abs(caller.x - carX) < pickupLateral)
            {
                BeginTalk();
            }
        }
        else if (taxi == TaxiState.Carrying)
        {
            float rem = destDist - dist;
            if (speed <= pickupSpeed && Mathf.Abs(rem) < 14f)
            {
                Arrive();
            }
            else if (rem < -45f)
            {
                // 지나쳤다: 목적지를 앞쪽으로 다시 잡고 요금을 깎는다 (이번 단계는 U턴 없음)
                Popup("목적지를 지나쳤어요!", new Color(1f, 0.5f, 0.4f));
                destDist = dist + 140f;
                fareMul = Mathf.Max(0.2f, fareMul - 0.2f);
            }
        }
    }

    void MakeCaller()
    {
        var p = SpawnPed(0, callerDistance);
        p.caller = true;
        p.walkZ = 0f;                                        // 연석에서 제자리
        p.x = p.side * (roadHalfWidth + 1.2f);
        caller = p;
    }

    void BeginTalk()
    {
        taxi = TaxiState.Talking;
        speed = 0f;
        paxName = PaxNames[Random.Range(0, PaxNames.Length)];
        destName = DestNames[Random.Range(0, DestNames.Length)];
        paxLine = string.Format(HelloLines[Random.Range(0, HelloLines.Length)], destName);
        paxSkin = SkinTones[Random.Range(0, SkinTones.Length)];
        paxHair = HairTones[Random.Range(0, HairTones.Length)];
        paxShirt = caller.baseColor;
        tripMeters = Random.Range(tripDistance.x, tripDistance.y);

        // 손님 쪽(왼쪽/오른쪽)으로 시점을 돌린다
        float dx = caller.x - (carX - 0.4f);
        camYawTarget = Mathf.Atan2(dx, Mathf.Max(0.5f, caller.z)) * Mathf.Rad2Deg;
    }

    void AcceptPassenger()
    {
        taxi = TaxiState.Carrying;
        fareMul = 1f;
        destDist = dist + tripMeters;
        int idx = peds.IndexOf(caller);
        if (idx >= 0) Remove(idx);                           // 손님은 차 안으로
        caller = null;
        camYawTarget = 0f;
        Popup("출발!", new Color(1f, 0.9f, 0.4f));
    }

    void DeclinePassenger()
    {
        if (caller != null)
        {
            caller.caller = false;
            caller.walkZ = Random.Range(sidewalkWalkSpeed.x, sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        }
        caller = null;
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
        taxi = TaxiState.Cruise;
        camYawTarget = 0f;
    }

    void Arrive()
    {
        int fare = Mathf.RoundToInt((baseFare + farePerMeter * tripMeters) * fareMul);
        fareTotal += fare;
        trips++;
        score += Mathf.RoundToInt(fare / 100f);
        Popup("도착! +" + fare + "원", new Color(1f, 0.85f, 0.3f));
        taxi = TaxiState.Cruise;
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
    }

    // 새 사람 한 명을 인도에 만든다. kind 를 -1 로 주면 비율대로 무작위 종류를 고른다.
    Ped SpawnPed(int kind = -1, float z = -1f)
    {
        if (kind < 0)
        {
            float r = Random.value;
            kind = r < sidewalkerRatio ? 0 : (r < sidewalkerRatio + farCrossRatio ? 1 : 2);
        }
        if (z < 0f) z = SpawnZ + Random.Range(0f, 10f);

        float side = Random.value < 0.5f ? -1f : 1f;
        var go = new GameObject("Ped");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pedSprite;
        Color c = Random.ColorHSV(0f, 1f, 0.5f, 0.7f, 0.8f, 1f);
        sr.color = c;

        float offMax = kind == 2 ? Mathf.Min(sidewalkOffset.y, 2f) : sidewalkOffset.y;   // 튀어나올 사람은 연석 가까이
        float off = Random.Range(Mathf.Min(sidewalkOffset.x, offMax), offMax);
        var p = new Ped
        {
            t = go.transform,
            sr = sr,
            kind = kind,
            side = side,
            dir = -side,
            x = side * (roadHalfWidth + off),
            z = z,
            curbX = side * (roadHalfWidth + 0.35f),
            walkZ = Random.Range(sidewalkWalkSpeed.x, sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f),
            walkSpeed = kind == 2 ? Random.Range(dartSpeed.x, dartSpeed.y) : Random.Range(crossSpeed.x, crossSpeed.y),
            triggerZ = kind == 1 ? Random.Range(farTrigger.x, farTrigger.y) : Random.Range(dartTrigger.x, dartTrigger.y),
            waitTime = kind == 2 ? Random.Range(curbWait.x, curbWait.y) : 0f,
            baseColor = c,
            phase = Random.value * 6f
        };
        peds.Add(p);
        return p;
    }

    void UpdatePeds(float dt)
    {
        for (int i = peds.Count - 1; i >= 0; i--)
        {
            var p = peds[i];
            p.z -= speed * dt;

            if (p.fallT >= 0f)
            {
                p.fallT += dt;
                p.x += p.dir * 3f * dt;
                if (p.fallT > 1.2f || p.z < -6f)
                {
                    Remove(i);
                    continue;
                }
            }
            else
            {
                UpdatePedBehavior(p, dt);

                bool inZone = p.z > -0.6f && p.z < 2.4f;
                float lat = Mathf.Abs(p.x - carX);
                // 충돌과 니어미스는 도로를 건너는 중(st == 3)인 사람만. 인도를 걷는 사람은 가까이 지나가도 점수/충돌 없음
                if (inZone)
                {
                    if (p.st == 3)
                    {
                        p.minLat = Mathf.Min(p.minLat, lat);
                        if (lat < CarHalfWidth + PedHalfWidth)
                        {
                            OnHit(p);
                            continue;
                        }
                    }
                }
                else if (p.z <= -0.6f && !p.scored)
                {
                    p.scored = true;
                    if (p.minLat < nearMissDist)
                    {
                        nearMisses++;
                        score += nearMissBonus;
                        Popup("NEAR MISS +" + nearMissBonus, new Color(0.4f, 1f, 0.6f));
                        shakeUntil = Time.unscaledTime + 0.08f;
                    }
                }

            }

            if (p.z < -6f || p.z > SpawnZ + 40f) Remove(i);
        }
    }

    // 인도 걷기 → (트리거 거리 안이면) 연석으로 이동 → 멈칫 → 도로 건너기 → 건너편 인도에서 다시 걷기
    void UpdatePedBehavior(Ped p, float dt)
    {
        switch (p.st)
        {
            case 0: // 인도를 따라 걷는다
                p.z += p.walkZ * dt;
                if (p.kind != 0 && p.z < p.triggerZ && p.z > 0f) p.st = 1;
                break;

            case 1: // 연석으로 다가간다 (튀어나올 사람은 빠르게)
                p.x = Mathf.MoveTowards(p.x, p.curbX, (p.kind == 2 ? p.walkSpeed : 2.2f) * dt);
                if (Mathf.Approximately(p.x, p.curbX))
                {
                    p.st = p.waitTime > 0f ? 2 : 3;
                    p.pauseLeft = p.waitTime;
                }
                break;

            case 2: // 연석에서 멈칫 (밝아져서 예고가 된다)
                p.pauseLeft -= dt;
                if (p.pauseLeft <= 0f)
                {
                    p.pauseLeft = -1f;
                    p.st = 3;
                }
                break;

            default: // 도로를 건넌다
                p.crossed = true;
                p.x += p.dir * p.walkSpeed * dt;
                if (p.dir * p.x > roadHalfWidth + 0.6f)
                {
                    // 건너편 인도에 올라서면 그쪽 인도 보행자가 된다
                    p.st = 0;
                    p.kind = 0;
                    p.side = p.dir;
                    p.dir = -p.side;
                    p.walkZ = Random.Range(sidewalkWalkSpeed.x, sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
                }
                break;
        }
    }

    void Remove(int i)
    {
        if (peds[i].t != null) Destroy(peds[i].t.gameObject);
        peds.RemoveAt(i);
    }

    void OnHit(Ped p)
    {
        p.fallT = 0f;
        p.sr.color = new Color(1f, 0.4f, 0.4f);
        hits++;
        speed *= hitSpeedFactor;
        hitStopUntil = Time.unscaledTime + hitStop;
        shakeUntil = Time.unscaledTime + 0.3f;

        // 손님을 태운 채 사람을 치면 손님이 반응하고 요금이 깎인다
        if (taxi == TaxiState.Carrying)
        {
            reactText = ReactLines[Random.Range(0, ReactLines.Length)];
            reactUntil = Time.unscaledTime + 1.6f;
            fareMul = Mathf.Max(0.2f, fareMul * hitFareMul);
        }
        if (invuln > 0f) return;

        lives--;
        invuln = invulnTime;
        hurtFlash = 0.45f;
        Popup("BONK!", new Color(1f, 0.4f, 0.4f));
        if (lives <= 0)
        {
            lives = 0;
            gameOver = true;
        }
    }

    void Popup(string text, Color color)
    {
        popupText = text;
        popupColor = color;
        popupUntil = Time.unscaledTime + 0.8f;
    }

    // ---------- 월드 표현 ----------

    void UpdateWorldVisuals()
    {
        float frac = dist % StripLen;
        int baseIdx = Mathf.FloorToInt(dist / StripLen);
        float rw = roadHalfWidth;

        for (int i = 0; i < StripCount; i++)
        {
            var s = strips[i];
            int idx = baseIdx + i;
            float zc = i * StripLen - 8f - frac + StripLen * 0.5f;
            float drop = Drop(zc);
            float pitch = Mathf.Atan(2f * curvature * zc) * Mathf.Rad2Deg;
            var pitchRot = Quaternion.Euler(pitch, 0f, 0f);
            float len = StripLen * 1.08f;
            bool even = (idx & 1) == 0;
            bool inter = IsIntersection(idx);

            Place(s.road, new Vector3(0f, drop - 0.1f, zc), pitchRot, new Vector3(rw * 2f, 0.2f, len));
            if (inter)
            {
                // 교차로: 인도 박스를 가로 도로 아스팔트로 쓰고, 중앙선 대신 횡단보도 줄을 하나 둔다
                float cx = rw + crossStreetLength * 0.5f;
                Place(s.swL, new Vector3(-cx, drop - 0.1f, zc), pitchRot, new Vector3(crossStreetLength, 0.2f, len));
                Place(s.swR, new Vector3(cx, drop - 0.1f, zc), pitchRot, new Vector3(crossStreetLength, 0.2f, len));
                s.dash.gameObject.SetActive(true);
                Place(s.dash, new Vector3(0f, drop + 0.03f, zc - StripLen * 0.5f + 0.5f), pitchRot, new Vector3(rw * 2f, 0.02f, 0.5f));
            }
            else
            {
                Place(s.swL, new Vector3(-(rw + 2.5f), drop + 0.05f, zc), pitchRot, new Vector3(5f, 0.3f, len));
                Place(s.swR, new Vector3(rw + 2.5f, drop + 0.05f, zc), pitchRot, new Vector3(5f, 0.3f, len));
                s.dash.gameObject.SetActive(even);
                Place(s.dash, new Vector3(0f, drop + 0.02f, zc), pitchRot, new Vector3(0.18f, 0.02f, StripLen * 0.5f));
            }

            for (int side = 0; side < 2; side++) UpdateBuilding(s, s.sides[side], side, idx, zc, drop, pitchRot, rw);

            if (s.lastIdx != idx)
            {
                s.lastIdx = idx;
                s.road.GetComponent<Renderer>().sharedMaterial = even ? roadA : roadB;
                var sm = inter ? roadA : (even ? swA : swB);
                s.swL.GetComponent<Renderer>().sharedMaterial = sm;
                s.swR.GetComponent<Renderer>().sharedMaterial = sm;
            }
        }

        // 목적지 표지 기둥
        if (destMarker != null)
        {
            float mz = destDist - dist;
            bool show = taxi == TaxiState.Carrying && mz > -25f && mz < SpawnZ;
            if (destMarker.gameObject.activeSelf != show) destMarker.gameObject.SetActive(show);
            if (show) Place(destMarker, new Vector3(roadHalfWidth + 0.8f, Drop(mz) + 3f, mz), Quaternion.identity, new Vector3(0.4f, 6f, 0.4f));
        }

        foreach (var p in peds)
        {
            bool fallen = p.fallT >= 0f;
            float bob = (!fallen && p.pauseLeft <= 0f) ? Mathf.Sin((elapsed + p.phase) * 10f) * 0.05f : 0f;
            float h = fallen ? 0.35f : 0.875f + bob;
            p.t.position = new Vector3(p.x, Drop(p.z) + h, p.z);
            // 옆 시점으로 손님을 볼 때는 손님이 카메라를 향하게 돌린다 (평소 보행자는 정면 고정)
            float faceYaw = (taxi == TaxiState.Talking && p == caller) ? camYaw : 0f;
            p.t.rotation = Quaternion.Euler(0f, faceYaw, fallen ? -p.dir * 85f : 0f);
            if (p.pauseLeft > 0f) p.sr.color = Color.Lerp(p.baseColor, Color.white, 0.5f);
            else if (!fallen) p.sr.color = p.baseColor;
        }
    }

    // 건물 = 상자 몸통 + 각 면에 붙인 스프라이트. 도로 쪽 정면과 다가오는 쪽(-z) 면은 월드에 고정(카메라를 쳐다보지 않음).
    void UpdateBuilding(Strip s, Side sd, int sideIdx, int idx, float zc, float drop, Quaternion pitchRot, float rw)
    {
        float sgn = sideIdx == 0 ? -1f : 1f;
        float ts = TileScale;
        const float depth = 6f;

        bool changed = s.lastIdx != idx;
        bool inter = IsIntersection(idx);
        if (changed)
        {
            // 교차로 구간에는 건물이 없다 (구간이 바뀔 때만 켜고 끈다)
            s.bldg[sideIdx].gameObject.SetActive(!inter);
            if (hasCity)
            {
                sd.upper.gameObject.SetActive(!inter);
                sd.ground.gameObject.SetActive(!inter);
                if (inter)
                {
                    sd.cornice.gameObject.SetActive(false);
                    sd.fUpper.gameObject.SetActive(false);
                    sd.fGround.gameObject.SetActive(false);
                    sd.fCornice.gameObject.SetActive(false);
                    sd.door.gameObject.SetActive(false);
                    sd.prop.gameObject.SetActive(false);
                }
            }
        }
        if (inter) return;

        int hidx = idx + citySalt * 1013;            // 회전할 때마다 건물 배치가 달라지게 하는 해시용 번호
        if (changed)
        {
            int run = hidx / 3;                      // 3구간(12m)이 한 건물
            int seed = run * 2 + sideIdx;
            sd.pal = Mathf.Min(3, Mathf.FloorToInt(Hash(seed * 13 + 1) * 4f));
            sd.floors = 3 + Mathf.FloorToInt(Hash(seed * 17 + 2) * 5f);
            sd.setback = Hash(seed * 29 + 5) * 1.5f;  // 건물마다 앞뒤로 들쑥날쑥 -> 옆면이 보여서 입체감
            s.bldg[sideIdx].GetComponent<Renderer>().sharedMaterial = bodyMats[sd.pal];

            if (hasCity)
            {
                int shop = Mathf.Min(2, Mathf.FloorToInt(Hash(hidx * 19 + sideIdx) * 3f));
                sd.upper.sprite = winTiles[sd.pal];
                sd.upper.size = new Vector2(TileCols, sd.floors * 2f);
                sd.ground.sprite = shopTiles[shop];
                sd.ground.size = new Vector2(TileCols, 2f);
                sd.cornice.sprite = corniceTile;
                sd.cornice.size = new Vector2(TileCols, 1f);

                int fc = Mathf.RoundToInt(depth / (ts * TileW));
                sd.fUpper.sprite = winTiles[sd.pal];
                sd.fUpper.size = new Vector2(fc, sd.floors * 2f);
                sd.fGround.sprite = shopTiles[shop];
                sd.fGround.size = new Vector2(fc, 2f);
                sd.fCornice.sprite = corniceTile;
                sd.fCornice.size = new Vector2(fc, 1f);

                sd.door.sprite = doorTiles[sd.pal];
                sd.wantDoor = ((hidx % 3) + 3) % 3 == 1;

                int kind = 0;
                if (((hidx + sideIdx * 2) % 4 + 4) % 4 == 0) kind = 1;
                else if (Hash(hidx * 3 + sideIdx * 7) < 0.12f) kind = 2;
                else if (Hash(hidx * 5 + sideIdx * 11) < 0.15f) kind = 3;
                else if (Hash(hidx * 7 + sideIdx * 13) < 0.10f) kind = 4;
                sd.wantProp = kind != 0;
                sd.prop.flipX = sideIdx == 0;   // 가로등 머리가 도로 쪽을 향하게
                switch (kind)
                {
                    case 1: sd.prop.sprite = lampSprite; sd.propScale = 2f; break;
                    case 2: sd.prop.sprite = Hash(hidx * 23 + sideIdx) < 0.5f ? lightRedSprite : lightGreenSprite; sd.propScale = 1.6f; break;
                    case 3: sd.prop.sprite = trashSprite; sd.propScale = 1.2f; break;
                    case 4: sd.prop.sprite = mailSprite; sd.propScale = 1.2f; break;
                }
            }
        }

        float H = (3f + sd.floors * 2f) * ts;                 // 1층(2) + 윗층(2*층수) + 지붕 장식(1)
        float faceX = sgn * (rw + 5f + sd.setback);
        float cx = faceX + sgn * depth * 0.5f;
        float baseY = drop + 0.2f;

        Place(s.bldg[sideIdx], new Vector3(cx, baseY, zc) + pitchRot * new Vector3(0f, H * 0.5f, 0f), pitchRot, new Vector3(depth, H, StripLen));
        if (!hasCity) return;

        // 원거리 단순화: 멀리 있는 건물은 벽 두 장(윗층/1층)만 남기고 나머지는 끈다. 상태가 바뀔 때만 SetActive.
        bool far = lodDistance > 0f && zc > lodDistance;
        if (changed || sd.far != far)
        {
            sd.far = far;
            bool near = !far;
            sd.cornice.gameObject.SetActive(near);
            sd.fUpper.gameObject.SetActive(near);
            sd.fGround.gameObject.SetActive(near);
            sd.fCornice.gameObject.SetActive(near);
            sd.door.gameObject.SetActive(near && sd.wantDoor);
            sd.prop.gameObject.SetActive(near && sd.wantProp);
        }

        // 도로 쪽 정면 (법선이 +-x)
        var faceRot = pitchRot * Quaternion.Euler(0f, sgn * 90f, 0f);
        Vector3 basePos = new Vector3(faceX - sgn * 0.02f, baseY, zc);
        Place(sd.ground.transform, basePos, faceRot, Vector3.one * ts);
        Place(sd.upper.transform, basePos + faceRot * new Vector3(0f, 2f * ts, 0f), faceRot, Vector3.one * ts);
        if (far) return;
        Place(sd.cornice.transform, basePos + faceRot * new Vector3(0f, (2f + sd.floors * 2f) * ts, 0f), faceRot, Vector3.one * ts);
        if (sd.door.gameObject.activeSelf)
            Place(sd.door.transform, basePos + faceRot * new Vector3(-(TileCols - 1) * 0.5f * ts * TileW, 0f, -0.02f), faceRot, Vector3.one * ts);

        // 다가오는 쪽(-z) 면: 옆 건물보다 높거나 튀어나온 부분의 단면이 보인다
        Vector3 fBase = new Vector3(cx, baseY, zc) + pitchRot * new Vector3(0f, 0f, -StripLen * 0.5f - 0.02f);
        Place(sd.fGround.transform, fBase, pitchRot, Vector3.one * ts);
        Place(sd.fUpper.transform, fBase + pitchRot * new Vector3(0f, 2f * ts, 0f), pitchRot, Vector3.one * ts);
        Place(sd.fCornice.transform, fBase + pitchRot * new Vector3(0f, (2f + sd.floors * 2f) * ts, 0f), pitchRot, Vector3.one * ts);

        if (sd.prop.gameObject.activeSelf)
        {
            // 소품은 비스듬히 세워서 정면에서도 보이게 (그래도 플레이어를 쳐다보진 않음)
            var propRot = pitchRot * Quaternion.Euler(0f, sgn * 50f, 0f);
            Place(sd.prop.transform, new Vector3(sgn * (rw + 0.7f), drop + 0.2f, zc), propRot, Vector3.one * sd.propScale);
        }
    }

    static void Place(Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        t.SetPositionAndRotation(pos, rot);
        t.localScale = scale;
    }

    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xFFFF) / 65535f;
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 off = Time.unscaledTime < shakeUntil ? (Vector3)(Random.insideUnitCircle * shakeHit) : Vector3.zero;
        cam.transform.position = new Vector3(carX - 0.4f, camHeight, 0f) + off;
        float roll = -lateralVel / maxLateral * 2.5f;
        camYaw = Mathf.MoveTowardsAngle(camYaw, camYawTarget, yawSpeed * Time.unscaledDeltaTime);
        cam.transform.rotation = Quaternion.Euler(0f, camYaw, roll);
        float fovTarget = taxi == TaxiState.Talking ? sideFov : 70f;
        cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, fovTarget, 120f * Time.unscaledDeltaTime);

        // 옆 시점(손님 대화) 중에는 손/핸들/대시보드를 숨긴다
        cabinVisible = taxi != TaxiState.Talking && Mathf.Abs(camYaw) < 4f;
        if (viewRoot != null && viewRoot.gameObject.activeSelf != cabinVisible) viewRoot.gameObject.SetActive(cabinVisible);

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

    // ---------- 텍스처 ----------

    static Texture2D MakeTexture(bool circle)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - n / 2f) / (n / 2f);
                float dy = (y + 0.5f - n / 2f) / (n / 2f);
                float a = (!circle || dx * dx + dy * dy <= 1f) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    // 32x56 도트 느낌의 단순 사람 모양 (흰색 바탕, SpriteRenderer.color 로 색칠)
    static Sprite MakePedSprite()
    {
        const int w = 32, h = 56;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0f, 0f, 0f, 0f);
                float hx = x - 15.5f, hy = y - 46f;
                bool head = hx * hx + hy * hy <= 64f;                  // 머리: 반지름 8
                bool torso = x >= 8 && x <= 23 && y >= 18 && y <= 36;
                bool legs = (x >= 9 && x <= 14 || x >= 17 && x <= 22) && y >= 1 && y < 18;
                bool arms = (x >= 4 && x <= 7 || x >= 24 && x <= 27) && y >= 20 && y <= 36;
                if (head || torso || legs || arms) c = Color.white;
                bool eye = y >= 46 && y <= 49 && (x >= 11 && x <= 13 || x >= 18 && x <= 20);
                if (eye && head) c = Color.black;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    // ---------- 차 내부 오버레이 / HUD (IMGUI) ----------

    GUIStyle style;

    // HUD 문자열 캐시
    int hudLives = -1, hudMaxLives = -1, hudTotal = -1, hudKmh = -1, hudNear = -1, hudHits = -1, hudPeds = -1, hudSec = -1;
    string hudHearts = "", hudScore = "", hudStats = "";

    void Label(Rect r, string text, int size, Color color, TextAnchor anchor)
    {
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
        style.fontSize = Mathf.RoundToInt(size * Screen.height / 720f);
        style.alignment = anchor;
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, whiteTex);
        GUI.color = Color.white;
    }

    void Dot(Vector2 c, float size, Color col)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), circleTex);
        GUI.color = Color.white;
    }

    // 얼굴 하나를 도형으로 그린다 (머리카락 + 피부 + 눈 + 입). shocked 이면 눈이 커지고 입이 벌어진다.
    void DrawFace(Vector2 c, float s, Color skin, Color hair, bool shocked)
    {
        Dot(c + new Vector2(0f, -s * 0.10f), s * 1.10f, hair);
        Dot(c + new Vector2(0f, s * 0.06f), s * 0.92f, skin);
        float ex = s * 0.17f, ey = c.y - s * 0.02f;
        float eye = shocked ? s * 0.20f : s * 0.11f;
        if (shocked)
        {
            Dot(new Vector2(c.x - ex, ey), eye, Color.white);
            Dot(new Vector2(c.x + ex, ey), eye, Color.white);
        }
        Dot(new Vector2(c.x - ex, ey), eye * 0.55f, Color.black);
        Dot(new Vector2(c.x + ex, ey), eye * 0.55f, Color.black);
        if (shocked) Dot(new Vector2(c.x, c.y + s * 0.30f), s * 0.20f, new Color(0.25f, 0.05f, 0.05f));
        else Fill(new Rect(c.x - s * 0.12f, c.y + s * 0.27f, s * 0.24f, Mathf.Max(2f, s * 0.04f)), new Color(0.35f, 0.12f, 0.1f));
    }

    // 룸미러: 평소엔 빈 거울, 손님을 태우면 기사 얼굴 뒤로 손님 얼굴이 보인다
    void DrawRearMirror(float W, float H)
    {
        bool carrying = taxi == TaxiState.Carrying;
        float mw = carrying ? W * 0.16f : W * 0.106f;
        float mh = carrying ? H * 0.085f : H * 0.04f;
        var r = new Rect(W * 0.5f - mw * 0.5f, H * 0.098f, mw, mh);
        Fill(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(0.1f, 0.1f, 0.12f));
        Fill(r, new Color(0.35f, 0.4f, 0.45f));
        if (!carrying) return;

        float s = mh * 0.62f;
        bool shocked = Time.unscaledTime < reactUntil;
        DrawFace(new Vector2(r.center.x + mw * 0.16f, r.center.y - mh * 0.02f), s * 0.85f, paxSkin, paxHair, shocked);   // 뒤쪽 손님
        DrawFace(new Vector2(r.center.x - mw * 0.14f, r.center.y + mh * 0.06f), s, SkinTones[0], new Color(0.12f, 0.09f, 0.07f), false);   // 기사
    }

    void DrawCabin()
    {
        float W = Screen.width, H = Screen.height;
        var frame = new Color(0.1f, 0.1f, 0.12f);

        // 옆 시점(손님 대화): 창틀과 문 안쪽만 보인다
        if (!cabinVisible)
        {
            Fill(new Rect(0, 0, W, H * 0.06f), frame);
            Fill(new Rect(0, H * 0.90f, W, H * 0.10f + 2f), frame);
            return;
        }

        // 앞유리 프레임: 지붕, A필러, 룸미러 (대시보드/핸들/팔은 3D)
        Fill(new Rect(0, 0, W, H * 0.09f), frame);
        Fill(new Rect(0, 0, W * 0.055f, H), frame);
        Fill(new Rect(W * 0.945f, 0, W * 0.055f, H), frame);
        Fill(new Rect(W * 0.44f, H * 0.09f, W * 0.12f, H * 0.055f), frame);
        DrawRearMirror(W, H);

        if (arms3D) return;

        // 3D 팔을 못 쓸 때의 대체: 2D 대시보드/핸들/왼팔
        Fill(new Rect(0, H * 0.80f, W, H * 0.20f + 2f), new Color(0.16f, 0.16f, 0.19f));

        Vector2 c = new Vector2(W * 0.34f, H * 0.90f);
        float R = H * 0.27f;
        float rim = H * 0.05f;
        var wheelCol = new Color(0.05f, 0.05f, 0.06f);

        const int n = 48;
        for (int k = 0; k < n; k++)
        {
            float a = k / (float)n * Mathf.PI * 2f;
            Dot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R, rim, wheelCol);
        }

        float ang2d = steerAngle / wheelMaxAngle * 100f;
        var saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang2d, c);
        Fill(new Rect(c.x - R, c.y - H * 0.014f, R * 2f, H * 0.028f), wheelCol);
        Fill(new Rect(c.x - H * 0.014f, c.y, H * 0.028f, R), wheelCol);
        Dot(c, H * 0.09f, new Color(0.12f, 0.12f, 0.14f));
        GUI.matrix = saved;

        float th = (180f + ang2d) * Mathf.Deg2Rad;
        Vector2 hand = c + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * R;
        Vector2 shoulder = new Vector2(-W * 0.03f, H * 1.08f);
        Vector2 d = hand - shoulder;
        float len = d.magnitude;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float thick = H * 0.13f;

        saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, shoulder);
        Fill(new Rect(shoulder.x, shoulder.y - thick / 2f, len, thick), new Color(0.25f, 0.3f, 0.45f));
        Fill(new Rect(shoulder.x + len * 0.78f, shoulder.y - thick * 0.4f, len * 0.22f, thick * 0.8f), new Color(0.95f, 0.75f, 0.6f));
        GUI.matrix = saved;
        Dot(hand, H * 0.13f, new Color(0.95f, 0.75f, 0.6f));
        Dot(hand + new Vector2(H * 0.01f, -H * 0.045f), H * 0.05f, new Color(0.85f, 0.65f, 0.5f));
    }

    // 택시 UI: 손님 머리 위 '!', 목적지 표시, 탑승 정보, 대화 상자, 사고 시 손님 리액션
    void DrawTaxiGui(float W, float H, float u)
    {
        if (cam == null) return;

        // 손님 머리 위 느낌표
        if (caller != null && caller.t != null && taxi != TaxiState.Carrying)
        {
            Vector3 sp = cam.WorldToScreenPoint(caller.t.position + Vector3.up * 1.2f);
            if (sp.z > 0f)
            {
                float size = Mathf.Clamp(1100f / sp.z, 26f, 90f);
                Label(new Rect(sp.x - 40 * u, H - sp.y - size * u, 80 * u, size * u * 1.2f), "!", Mathf.RoundToInt(size), new Color(1f, 0.85f, 0.1f), TextAnchor.MiddleCenter);
            }
        }

        // 목적지 표지 위 글자
        if (taxi == TaxiState.Carrying && destMarker != null && destMarker.gameObject.activeSelf)
        {
            Vector3 sp = cam.WorldToScreenPoint(destMarker.position + Vector3.up * 3.4f);
            if (sp.z > 0f)
            {
                float size = Mathf.Clamp(1400f / sp.z, 20f, 60f);
                Label(new Rect(sp.x - 120 * u, H - sp.y - size * u, 240 * u, size * u * 1.3f), destName, Mathf.RoundToInt(size), new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter);
            }
        }

        // 탑승 정보 (상단 가운데)
        if (taxi == TaxiState.Carrying)
        {
            int rem = Mathf.Max(0, Mathf.RoundToInt(destDist - dist));
            int fareNow = Mathf.RoundToInt((baseFare + farePerMeter * tripMeters) * fareMul);
            Label(new Rect(0, H * 0.20f, W, 30 * u), paxName + " → " + destName + "   남은 거리 " + rem + " m   요금 " + fareNow + "원", 20, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.20f + 28 * u, W, 24 * u), "목적지 근처에서 브레이크로 정차하면 내려 드립니다", 15, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
        }
        else if (taxi == TaxiState.Cruise)
        {
            string hint = caller != null
                ? "손님이 기다린다! 손님 쪽 인도에 붙어서 브레이크로 멈추세요"
                : "손님을 찾는 중...";
            Label(new Rect(0, H * 0.20f, W, 28 * u), hint, 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
        }

        // 깜빡이와 교차로 안내
        bool blinkOn = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.35f;
        if (blinker != 0 && blinkOn)
            Label(new Rect(blinker < 0 ? W * 0.5f - 230 * u : W * 0.5f + 90 * u, H * 0.145f, 140 * u, 40 * u),
                  blinker < 0 ? "< 좌" : "우 >", 30, new Color(0.4f, 1f, 0.4f), blinker < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);

        int dm = Mathf.RoundToInt(nextInterDist);
        if (dm != hudInterDist || blinker != hudInterBlinker || heading != hudInterHeading || gx != hudInterGx || gy != hudInterGy)
        {
            hudInterGx = gx;
            hudInterGy = gy;
            hudInterDist = dm;
            hudInterBlinker = blinker;
            hudInterHeading = heading;
            string bl = blinker < 0 ? "좌회전 예약" : (blinker > 0 ? "우회전 예약" : "직진 (Q 좌 / E 우 깜빡이)");
            hudInterText = "방향 " + HeadNames[heading] + " (" + gx + "," + gy + ")   다음 교차로 " + dm + " m   " + bl
                         + (blinker != 0 ? "   — " + Mathf.RoundToInt(turnSpeed * 3.6f) + " km/h 이하로" : "");
        }
        Label(new Rect(0, H * 0.34f, W, 26 * u), hudInterText, 16, new Color(0.8f, 0.95f, 1f, 0.9f), TextAnchor.MiddleCenter);

        // 누적 요금 (좌측 상단 아래)
        Label(new Rect(20 * u, 84 * u, 500 * u, 30 * u), "요금 " + fareTotal + "원   (" + trips + "명)", 18, new Color(1f, 0.92f, 0.5f), TextAnchor.UpperLeft);

        // 손님 대화 상자
        if (taxi == TaxiState.Talking && Mathf.Abs(Mathf.DeltaAngle(camYaw, camYawTarget)) < 3f)
        {
            var box = new Rect(W * 0.10f, H * 0.62f, W * 0.80f, H * 0.30f);
            Fill(box, new Color(0.05f, 0.05f, 0.08f, 0.88f));
            DrawFace(new Vector2(box.x + box.height * 0.55f, box.center.y), box.height * 0.62f, paxSkin, paxHair, false);
            Dot(new Vector2(box.x + box.height * 0.55f, box.yMax - box.height * 0.10f), box.height * 0.36f, paxShirt);
            float tx = box.x + box.height * 1.15f;
            Label(new Rect(tx, box.y + 12 * u, box.width - box.height * 1.2f, 36 * u), paxName, 24, new Color(1f, 0.85f, 0.4f), TextAnchor.UpperLeft);
            style.wordWrap = true;
            Label(new Rect(tx, box.y + 52 * u, box.width - box.height * 1.3f, box.height - 90 * u), paxLine, 24, Color.white, TextAnchor.UpperLeft);
            style.wordWrap = false;
            Label(new Rect(tx, box.yMax - 38 * u, box.width - box.height * 1.2f, 30 * u), "[Space] 태우기    [X] 거절", 18, new Color(0.7f, 0.9f, 1f), TextAnchor.LowerLeft);
        }

        // 사고 시 손님 리액션
        if (taxi == TaxiState.Carrying && Time.unscaledTime < reactUntil)
        {
            Vector2 c = new Vector2(W * 0.5f, H * 0.42f);
            Fill(new Rect(c.x - 230 * u, c.y - 120 * u, 460 * u, 240 * u), new Color(0f, 0f, 0f, 0.55f));
            DrawFace(new Vector2(c.x - 130 * u, c.y), 150 * u, paxSkin, paxHair, true);
            Label(new Rect(c.x - 40 * u, c.y - 40 * u, 260 * u, 80 * u), reactText, 30, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft);
        }
    }

    void OnGUI()
    {
        float W = Screen.width, H = Screen.height, u = H / 720f;

        if (hurtFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, hurtFlash * 0.6f));

        DrawCabin();

        // 문자열은 값이 바뀔 때만 새로 만든다 (OnGUI 는 프레임당 여러 번 불려서 매번 만들면 GC 쓰레기가 쌓인다)
        if (lives != hudLives || maxLives != hudMaxLives)
        {
            hudLives = lives;
            hudMaxLives = maxLives;
            hudHearts = new string('♥', Mathf.Max(0, lives)) + new string('♡', Mathf.Max(0, maxLives - lives));
        }
        Label(new Rect(20 * u, 14 * u, 500 * u, 40 * u), hudHearts, 30, new Color(1f, 0.4f, 0.5f), TextAnchor.UpperLeft);
        int total = score + Mathf.FloorToInt(distScore);
        if (total != hudTotal)
        {
            hudTotal = total;
            hudScore = "SCORE  " + total;
        }
        Label(new Rect(20 * u, 54 * u, 500 * u, 40 * u), hudScore, 22, Color.white, TextAnchor.UpperLeft);

        int kmh = Mathf.RoundToInt(speed * 3.6f), sec = Mathf.RoundToInt(elapsed);
        if (kmh != hudKmh || nearMisses != hudNear || hits != hudHits || peds.Count != hudPeds || sec != hudSec)
        {
            hudKmh = kmh; hudNear = nearMisses; hudHits = hits; hudPeds = peds.Count; hudSec = sec;
            hudStats = kmh + " km/h"
                     + "\nnear miss " + nearMisses + "   hit " + hits
                     + "\npeds " + peds.Count + "   t " + sec + "s";
        }
        Label(new Rect(W - 420 * u, 14 * u, 400 * u, 100 * u), hudStats, 18, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperRight);

        Label(new Rect(0, H - 30 * u, W, 26 * u), "A/D 조향   S 브레이크   Q/E 좌/우 깜빡이   Space 손님 태우기   X 거절   R 재시작(게임오버 후)", 15, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerCenter);

        DrawTaxiGui(W, H, u);

        if (Time.unscaledTime < popupUntil)
            Label(new Rect(0, H * 0.28f, W, 50 * u), popupText, 34, popupColor, TextAnchor.MiddleCenter);

        // 회전 페이드 컷
        if (fade > 0f) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, Mathf.Clamp01(fade)));

        if (gameOver)
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            Label(new Rect(0, H * 0.33f, W, 80 * u), "GAME OVER", 56, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.33f + 80 * u, W, 40 * u), "score " + total + "   near miss " + nearMisses + "   time " + elapsed.ToString("0") + "s   — R 로 재시작", 24, Color.white, TextAnchor.MiddleCenter);
        }
    }
}
