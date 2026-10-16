using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 3D 운전석: 대시보드, 필러, 천장, 핸들, 계기판 배치
public partial class DriveGame
{
    // 3D 차 내부
    Transform viewRoot, wheelSpin;

    // 운전석 부품 (LayoutCabin 이 public 값으로 위치를 맞춘다)
    Transform dashBody, dashLip, hood, clusterRoot, clusterFace, navStand, navScreen, navBezel, navBg, navBar, navCar, navDest, pillarL, pillarR, roof;

    Shader unlitShader;

    Font cabinFont;

    bool cabinVisible = true;

    // 조명을 안 받는 단색 재질 (내비/계기판 화면처럼 스스로 빛나 보이게)
    Material Unlit(Color c)
    {
        if (unlitShader == null) return Mat(c);
        var m = new Material(unlitShader);
        m.SetColor("_BaseColor", c);
        return m;
    }

    // 운전석 부품 하나 (그림자 없음: 카메라에 붙은 물체라 월드에 그림자를 떨어뜨리면 안 된다)
    Transform CabinPart(string name, PrimitiveType type, Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        var rd = go.GetComponent<Renderer>();
        rd.sharedMaterial = mat;
        rd.shadowCastingMode = ShadowCastingMode.Off;
        rd.receiveShadows = false;
        return go.transform;
    }

    // 3D 글자. height 는 한 줄 높이(m). TextMesh 는 글꼴 크기 10 = 1m 기준이라 characterSize 로 맞춘다
    TextMesh CabinText(string name, Transform parent, float height, TextAnchor anchor, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<TextMesh>();
        tm.font = cabinFont;
        tm.fontSize = 32;                                            // 작게 보이는 글자라 텍스처를 키우면 오히려 거칠어진다
        tm.characterSize = height * 10f / 32f;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = c;
        var rd = go.GetComponent<MeshRenderer>();
        if (cabinFont != null) rd.sharedMaterial = cabinFont.material;
        rd.shadowCastingMode = ShadowCastingMode.Off;
        return tm;
    }

    void Pillar(Transform t, Vector3 b, Vector3 top)
    {
        Vector3 d = top - b;
        t.localPosition = (b + top) * 0.5f;
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        t.localScale = new Vector3(pillarThickness, d.magnitude, pillarThickness * 1.4f);
    }

    // 운전석 부품 배치. 매 프레임 불러서 인스펙터 값을 바꾸면 플레이 중에도 바로 보인다
    void LayoutCabin()
    {
        float dy = dashTopY;
        dashBody.localPosition = new Vector3(0.3f, dy - 0.08f, 0.85f);
        dashBody.localScale = new Vector3(3.2f, 0.16f, 1.1f);
        dashLip.localPosition = new Vector3(0.3f, dy + 0.008f, 1.37f);          // 앞유리와 만나는 끝선
        dashLip.localScale = new Vector3(3.2f, 0.016f, 0.06f);

        float hoodH = Mathf.Max(0.02f, hoodTopY - dy);
        hood.localPosition = new Vector3(0f, dy + hoodH * 0.5f, 0.78f);
        hood.localScale = new Vector3(0.42f, hoodH, 0.22f);
        Vector3 cp = new Vector3(0f, hoodTopY - Mathf.Min(0.07f, hoodH * 0.5f), 0.64f);   // 후드 운전자 쪽 면 바로 앞
        clusterRoot.localPosition = cp;
        clusterRoot.localRotation = Quaternion.LookRotation(cp);
        clusterFace.localScale = new Vector3(0.30f, Mathf.Min(0.10f, hoodH * 0.8f), 1f);
        clusterText.transform.localPosition = new Vector3(0.085f, 0f, -0.002f);   // 핸들 세로 스포크/허브에 가리지 않게 오른쪽으로 비킨다

        Pillar(pillarL, pillarLBase, pillarLTop);
        Pillar(pillarR, pillarRBase, pillarRTop);
        roof.localPosition = new Vector3(0.4f, roofY + 0.05f, 0.6f);
        roof.localScale = new Vector3(3.4f, 0.1f, 0.7f);

        float w = Mathf.Max(0.05f, navSize.x), h = Mathf.Max(0.04f, navSize.y);
        navScreen.localPosition = navPos;
        navScreen.localRotation = Quaternion.LookRotation(navPos);               // 화면이 운전자 눈을 향한다
        navBezel.localPosition = new Vector3(0f, 0f, 0.012f);
        navBezel.localScale = new Vector3(w + 0.03f, h + 0.03f, 0.02f);
        navBg.localScale = new Vector3(w, h, 1f);
        navBar.localPosition = new Vector3(0f, -h * 0.41f, -0.0028f);
        navBar.localScale = new Vector3(w, h * 0.18f, 1f);
        navHeadText.transform.localPosition = new Vector3(-w * 0.5f + 0.008f, h * 0.5f - 0.004f, -0.003f);
        navInfoText.transform.localPosition = new Vector3(0f, -h * 0.41f, -0.0032f);
        float standH = Mathf.Max(0.01f, navPos.y - h * 0.5f - dy + 0.03f);
        navStand.localPosition = new Vector3(navPos.x, dy - 0.03f + standH * 0.5f, navPos.z + 0.04f);
        navStand.localScale = new Vector3(w * 0.5f, standH, 0.05f);
    }

    // ---------- 차 내부 (3D 핸들 + 3D 팔 IK) ----------

    void BuildCabin()
    {
        var rootGo = new GameObject("CabinView");
        rootGo.transform.SetParent(cam.transform, false);
        viewRoot = rootGo.transform;

        // 운전석: 대시보드, 계기판 후드, 내비, A필러, 천장 (위치/크기는 LayoutCabin 이 매 프레임 public 값으로 맞춘다)
        unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        cabinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var dashMatIn = Mat(new Color(0.15f, 0.15f, 0.17f));
        var trimMat = Mat(new Color(0.58f, 0.56f, 0.53f));       // 필러/천장 내장재 (밝은 회베이지)
        dashBody = CabinPart("Dashboard", PrimitiveType.Cube, viewRoot, dashMatIn);
        dashLip = CabinPart("DashLip", PrimitiveType.Cube, viewRoot, Mat(new Color(0.06f, 0.06f, 0.07f)));
        hood = CabinPart("ClusterHood", PrimitiveType.Cube, viewRoot, dashMatIn);
        pillarL = CabinPart("PillarL", PrimitiveType.Cube, viewRoot, trimMat);
        pillarR = CabinPart("PillarR", PrimitiveType.Cube, viewRoot, trimMat);
        roof = CabinPart("Roof", PrimitiveType.Cube, viewRoot, trimMat);

        // 계기판: 핸들 너머로 보이는 속도 표시
        clusterRoot = new GameObject("Cluster").transform;
        clusterRoot.SetParent(viewRoot, false);
        clusterFace = CabinPart("ClusterFace", PrimitiveType.Quad, clusterRoot, Unlit(new Color(0.02f, 0.03f, 0.04f)));
        clusterText = CabinText("Speed", clusterRoot, 0.032f, TextAnchor.MiddleCenter, new Color(0.55f, 0.9f, 1f));

        // 내비: 대시보드 위 받침대 + 테두리 + 화면. 지도는 화면 안의 얇은 판(쿼드)들로 그린다 (UpdateNav)
        navStand = CabinPart("NavStand", PrimitiveType.Cube, viewRoot, dashMatIn);
        navScreen = new GameObject("Nav").transform;
        navScreen.SetParent(viewRoot, false);
        navBezel = CabinPart("NavBezel", PrimitiveType.Cube, navScreen, Mat(new Color(0.04f, 0.04f, 0.05f)));
        navBg = CabinPart("NavBg", PrimitiveType.Quad, navScreen, Unlit(new Color(0.07f, 0.13f, 0.16f)));
        navBar = CabinPart("NavBar", PrimitiveType.Quad, navScreen, Unlit(new Color(0.02f, 0.05f, 0.07f)));
        navRoadMat = Unlit(new Color(0.30f, 0.36f, 0.42f));
        navRoadHiMat = Unlit(new Color(0.78f, 0.82f, 0.86f));
        navRouteMat = Unlit(new Color(0.30f, 0.95f, 0.45f));
        navDest = CabinPart("NavDest", PrimitiveType.Quad, navScreen, Unlit(new Color(1f, 0.82f, 0.2f)));
        navCar = CabinPart("NavCar", PrimitiveType.Quad, navScreen, Unlit(Color.white));
        navCar.GetComponent<MeshFilter>().sharedMesh = ArrowMesh();
        navHeadText = CabinText("NavHead", navScreen, 0.016f, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
        navInfoText = CabinText("NavInfo", navScreen, 0.014f, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
        LayoutCabin();

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
}
