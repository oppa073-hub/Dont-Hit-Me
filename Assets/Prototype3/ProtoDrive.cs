using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 프로토타입 3: "앞유리 너머로 무단횡단자를 먼저 알아보고 핸들로 피하는 게 재미있는가?"
// 버릴 코드. 빈 씬에 이 컴포넌트 하나만 붙이면 도로/보행자/차 내부/HUD 전부 코드로 생성된다.
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
    public float camHeight = 1.2f;

    [Header("Car")]
    public float baseSpeed = 12f;          // m/s
    public float maxSpeed = 30f;
    public float speedRamp = 0.2f;         // 초당 증가
    public float brakeFactor = 0.5f;
    public float maxLateral = 6f;          // 최대 횡이동 속도
    public float lateralAccel = 25f;       // 조향 관성 (작을수록 묵직)

    [Header("Pedestrians")]
    public float spawnInterval = 1.6f;
    public float minSpawnInterval = 0.55f;
    public float intervalRamp = 0.012f;
    public float walkSpeedMin = 1.2f;
    public float walkSpeedMax = 3.8f;
    [Range(0f, 1f)]
    public float pauseChance = 0.35f;      // 건너다 멈칫하는 비율
    public int maxPeds = 10;
    public float nearMissDist = 2.2f;      // 중심 간 횡거리 (2.3 이상이면 안전)
    public int nearMissBonus = 50;

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
    public float wristToHand = 0.07f;
    public float handAngle = 30f;          // 핸들 3시/9시 기준 위로 몇 도 (10시-2시 = 30)

    class Ped
    {
        public Transform t;
        public SpriteRenderer sr;
        public float x, z, dir, walkSpeed, pauseAt, pauseLeft = -1f, minLat = 999f, fallT = -1f, phase;
        public bool willPause, pausedDone, scored;
        public Color baseColor;
    }

    class Side
    {
        public SpriteRenderer upper, ground, cornice, door, prop;          // 도로 쪽 정면
        public SpriteRenderer fUpper, fGround, fCornice;                   // 다가오는 쪽(-z) 면
        public int floors = 3, pal;
        public float setback;
        public float propScale = 1f;
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
        public float[] angles;         // 마디별 최대 굽힘각
    }

    class ArmIK
    {
        public Transform upper, fore, wrist;
        public float lenU, lenF;
        public Transform target;
        public float tangentAngle;     // 림 접선 계산용 (도)
        public Vector3 poleLocal;
        public List<Finger> fingers = new List<Finger>();
    }

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
        ring.AddComponent<MeshFilter>().sharedMesh = MakeTorus(wheelRadius, 0.018f, 36, 10);
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
            poleLocal = pole
        };
        a.lenU = Vector3.Distance(a.upper.position, a.fore.position);
        a.lenF = Vector3.Distance(a.fore.position, a.wrist.position);

        var tgt = new GameObject("GripTarget." + side).transform;
        tgt.SetParent(wheelSpin, false);
        float r = angleDeg * Mathf.Deg2Rad;
        tgt.localPosition = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f) * wheelRadius;
        a.target = tgt;
        a.tangentAngle = angleDeg + 90f;

        // 손가락: 마디 3개 + 끝 본. 림을 감싸 쥐도록 매 프레임 굽힌다.
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
            f.angles = n == "thumb" ? new[] { 35f, 45f, 35f } : new[] { 75f, 85f, 60f };
            a.fingers.Add(f);
        }
        return a;
    }

    // 2본 IK: 어깨→팔꿈치→손목. 본 방향 규약을 몰라도 되도록 실제 자식 본 위치를 기준으로 회전시킨다.
    void SolveArm(ArmIK a)
    {
        Vector3 S = a.upper.position;
        Vector3 T = a.target.position;
        Vector3 dirST = (T - S);
        float d0 = dirST.magnitude;
        if (d0 < 1e-5f) return;
        dirST /= d0;
        T -= dirST * wristToHand;           // 손바닥이 림에 오도록 손목은 조금 앞에서 멈춤
        float d = Mathf.Clamp((T - S).magnitude, Mathf.Abs(a.lenU - a.lenF) + 0.01f, a.lenU + a.lenF - 0.005f);
        Vector3 dir = (T - S).normalized;
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

    // 손가락을 림 접선축으로 굽혀 주먹 쥔 모양으로 만든다. 굽힘 방향은 끝 본이 림 중심에 가까워지는 쪽을 고른다.
    void CurlFingers(ArmIK a)
    {
        Vector3 grip = a.target.position;
        float t = a.tangentAngle * Mathf.Deg2Rad;
        Vector3 axis = wheelSpin.TransformDirection(new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f)).normalized;

        foreach (var f in a.fingers)
        {
            for (int k = 0; k < 3; k++) f.bones[k].localRotation = f.rest[k];
            for (int k = 0; k < 3; k++)
            {
                var b = f.bones[k];
                Quaternion r0 = b.rotation;
                b.rotation = Quaternion.AngleAxis(f.angles[k], axis) * r0;
                float dPlus = (f.tip.position - grip).sqrMagnitude;
                b.rotation = Quaternion.AngleAxis(-f.angles[k], axis) * r0;
                float dMinus = (f.tip.position - grip).sqrMagnitude;
                b.rotation = Quaternion.AngleAxis(dPlus < dMinus ? f.angles[k] : -f.angles[k], axis) * r0;
            }
        }
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
    }

    void Update()
    {
        Time.timeScale = (!gameOver && Time.unscaledTime < hitStopUntil) ? 0f : 1f;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (gameOver)
        {
            if (kb.rKey.wasPressedThisFrame) ResetGame();
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
        UpdateWorldVisuals();
    }

    void SpawnPed()
    {
        float sx = roadHalfWidth + 1.2f;
        float dir = Random.value < 0.5f ? 1f : -1f;
        var go = new GameObject("Ped");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pedSprite;
        Color c = Random.ColorHSV(0f, 1f, 0.5f, 0.7f, 0.8f, 1f);
        sr.color = c;

        var p = new Ped
        {
            t = go.transform,
            sr = sr,
            x = -dir * sx,
            z = SpawnZ + Random.Range(0f, 10f),
            dir = dir,
            walkSpeed = Random.Range(walkSpeedMin, walkSpeedMax),
            baseColor = c,
            phase = Random.value * 6f,
            willPause = Random.value < pauseChance,
            pauseAt = Random.Range(-2.5f, 2.5f)
        };
        peds.Add(p);
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
                if (p.pauseLeft > 0f)
                {
                    p.pauseLeft -= dt;
                }
                else
                {
                    p.x += p.dir * p.walkSpeed * dt;
                    bool reached = p.dir > 0f ? p.x >= p.pauseAt : p.x <= p.pauseAt;
                    if (p.willPause && !p.pausedDone && reached)
                    {
                        p.pausedDone = true;
                        p.pauseLeft = Random.Range(0.6f, 1.6f);
                    }
                }

                bool inZone = p.z > -0.6f && p.z < 2.4f;
                float lat = Mathf.Abs(p.x - carX);
                if (inZone)
                {
                    p.minLat = Mathf.Min(p.minLat, lat);
                    if (lat < CarHalfWidth + PedHalfWidth)
                    {
                        OnHit(p);
                        continue;
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

                if (p.dir * p.x > roadHalfWidth + 3f)
                {
                    Remove(i);
                    continue;
                }
            }

            if (p.z < -6f) Remove(i);
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

            Place(s.road, new Vector3(0f, drop - 0.1f, zc), pitchRot, new Vector3(rw * 2f, 0.2f, len));
            Place(s.swL, new Vector3(-(rw + 2.5f), drop + 0.05f, zc), pitchRot, new Vector3(5f, 0.3f, len));
            Place(s.swR, new Vector3(rw + 2.5f, drop + 0.05f, zc), pitchRot, new Vector3(5f, 0.3f, len));

            s.dash.gameObject.SetActive(even);
            Place(s.dash, new Vector3(0f, drop + 0.02f, zc), pitchRot, new Vector3(0.18f, 0.02f, StripLen * 0.5f));

            for (int side = 0; side < 2; side++) UpdateBuilding(s, s.sides[side], side, idx, zc, drop, pitchRot, rw);

            if (s.lastIdx != idx)
            {
                s.lastIdx = idx;
                s.road.GetComponent<Renderer>().sharedMaterial = even ? roadA : roadB;
                var sm = even ? swA : swB;
                s.swL.GetComponent<Renderer>().sharedMaterial = sm;
                s.swR.GetComponent<Renderer>().sharedMaterial = sm;
            }
        }

        foreach (var p in peds)
        {
            bool fallen = p.fallT >= 0f;
            float bob = (!fallen && p.pauseLeft <= 0f) ? Mathf.Sin((elapsed + p.phase) * 10f) * 0.05f : 0f;
            float h = fallen ? 0.35f : 0.875f + bob;
            p.t.position = new Vector3(p.x, Drop(p.z) + h, p.z);
            p.t.rotation = Quaternion.Euler(0f, 0f, fallen ? -p.dir * 85f : 0f);
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

        if (s.lastIdx != idx)
        {
            int run = idx / 3;                       // 3구간(12m)이 한 건물
            int seed = run * 2 + sideIdx;
            sd.pal = Mathf.Min(3, Mathf.FloorToInt(Hash(seed * 13 + 1) * 4f));
            sd.floors = 3 + Mathf.FloorToInt(Hash(seed * 17 + 2) * 5f);
            sd.setback = Hash(seed * 29 + 5) * 1.5f;  // 건물마다 앞뒤로 들쑥날쑥 -> 옆면이 보여서 입체감
            s.bldg[sideIdx].GetComponent<Renderer>().sharedMaterial = bodyMats[sd.pal];

            if (hasCity)
            {
                int shop = Mathf.Min(2, Mathf.FloorToInt(Hash(idx * 19 + sideIdx) * 3f));
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
                sd.door.gameObject.SetActive(idx % 3 == 1);

                int kind = 0;
                if ((idx + sideIdx * 2) % 4 == 0) kind = 1;
                else if (Hash(idx * 3 + sideIdx * 7) < 0.12f) kind = 2;
                else if (Hash(idx * 5 + sideIdx * 11) < 0.15f) kind = 3;
                else if (Hash(idx * 7 + sideIdx * 13) < 0.10f) kind = 4;
                sd.prop.gameObject.SetActive(kind != 0);
                sd.prop.flipX = sideIdx == 0;   // 가로등 머리가 도로 쪽을 향하게
                switch (kind)
                {
                    case 1: sd.prop.sprite = lampSprite; sd.propScale = 2f; break;
                    case 2: sd.prop.sprite = Hash(idx * 23 + sideIdx) < 0.5f ? lightRedSprite : lightGreenSprite; sd.propScale = 1.6f; break;
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

        // 도로 쪽 정면 (법선이 +-x)
        var faceRot = pitchRot * Quaternion.Euler(0f, sgn * 90f, 0f);
        Vector3 basePos = new Vector3(faceX - sgn * 0.02f, baseY, zc);
        Place(sd.ground.transform, basePos, faceRot, Vector3.one * ts);
        Place(sd.upper.transform, basePos + faceRot * new Vector3(0f, 2f * ts, 0f), faceRot, Vector3.one * ts);
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
        cam.transform.rotation = Quaternion.Euler(0f, 0f, roll);

        // 핸들 회전 → 손 위치가 따라 움직이므로 IK 는 그 뒤에 푼다
        if (wheelSpin != null) wheelSpin.localRotation = Quaternion.Euler(0f, 0f, -steerAngle);
        if (arms3D)
        {
            SolveArm(armLeft);
            SolveArm(armRight);
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

    void DrawCabin()
    {
        float W = Screen.width, H = Screen.height;
        var frame = new Color(0.1f, 0.1f, 0.12f);

        // 앞유리 프레임: 지붕, A필러, 룸미러 (대시보드/핸들/팔은 3D)
        Fill(new Rect(0, 0, W, H * 0.09f), frame);
        Fill(new Rect(0, 0, W * 0.055f, H), frame);
        Fill(new Rect(W * 0.945f, 0, W * 0.055f, H), frame);
        Fill(new Rect(W * 0.44f, H * 0.09f, W * 0.12f, H * 0.055f), frame);
        Fill(new Rect(W * 0.447f, H * 0.098f, W * 0.106f, H * 0.04f), new Color(0.35f, 0.4f, 0.45f));

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

    void OnGUI()
    {
        float W = Screen.width, H = Screen.height, u = H / 720f;

        if (hurtFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, hurtFlash * 0.6f));

        DrawCabin();

        string hearts = new string('♥', Mathf.Max(0, lives)) + new string('♡', Mathf.Max(0, maxLives - lives));
        Label(new Rect(20 * u, 14 * u, 500 * u, 40 * u), hearts, 30, new Color(1f, 0.4f, 0.5f), TextAnchor.UpperLeft);
        int total = score + Mathf.FloorToInt(distScore);
        Label(new Rect(20 * u, 54 * u, 500 * u, 40 * u), "SCORE  " + total, 22, Color.white, TextAnchor.UpperLeft);

        string stats = (speed * 3.6f).ToString("0") + " km/h"
                     + "\nnear miss " + nearMisses + "   hit " + hits
                     + "\npeds " + peds.Count + "   t " + elapsed.ToString("0") + "s";
        Label(new Rect(W - 420 * u, 14 * u, 400 * u, 100 * u), stats, 18, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperRight);

        Label(new Rect(0, H - 30 * u, W, 26 * u), "A/D 또는 ←/→ 조향   S/↓ 브레이크   R 재시작(게임오버 후)", 15, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerCenter);

        if (Time.unscaledTime < popupUntil)
            Label(new Rect(0, H * 0.28f, W, 50 * u), popupText, 34, popupColor, TextAnchor.MiddleCenter);

        if (gameOver)
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            Label(new Rect(0, H * 0.33f, W, 80 * u), "GAME OVER", 56, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.33f + 80 * u, W, 40 * u), "score " + total + "   near miss " + nearMisses + "   time " + elapsed.ToString("0") + "s   — R 로 재시작", 24, Color.white, TextAnchor.MiddleCenter);
        }
    }
}
