using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 로비 그리기 (IMGUI): 정비소 앞 거리를 옆에서 본 2D 장면. 모두 도형으로 그린 임시 그림이다 (나중에 스프라이트로 바꾸기 쉽게 장면 요소별 함수로 나눠 둠).
public partial class DriveGame
{
    float lbU, lbCam;                    // 지금 그리는 화면의 배율과 카메라 위치 (로비 단위: 화면 높이 720 기준)

    const float GroundY = 590f;          // 캐릭터 발이 닿는 높이

    static readonly Color NpcSkin = new Color(0.9f, 0.72f, 0.58f), NpcHair = new Color(0.75f, 0.72f, 0.7f), NpcShirt = new Color(0.2f, 0.3f, 0.55f);

    static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }       // 알파는 그대로 두고 어둡게

    static readonly float[] LampXs = { 880f, 1250f, 2000f };

    void LR(float x, float y, float w, float h, Color c)
    {
        Fill(new Rect((x - lbCam) * lbU, y * lbU, w * lbU, h * lbU), c);
    }

    void LD(float cx, float cy, float size, Color c)
    {
        Dot(new Vector2((cx - lbCam) * lbU, cy * lbU), size * lbU, c);
    }

    // 스프라이트 하나를 IMGUI 로 그린다. footX/footY = 발(피벗) 위치(화면 px), k = 스프라이트 한 픽셀이 화면 몇 px 인가, flip = 좌우 뒤집기
    void DrawSpriteGui(Sprite sp, float footX, float footY, float k, bool flip, Color tint)
    {
        if (sp == null || Event.current == null || Event.current.type != EventType.Repaint) return;
        var tex = sp.texture;
        var r = sp.textureRect;
        float w = r.width * k, h = r.height * k;
        float pivX = sp.pivot.x * k;
        float left = flip ? footX - (w - pivX) : footX - pivX;
        float top = footY + sp.pivot.y * k - h;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        if (flip) { uv.x += uv.width; uv.width = -uv.width; }
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(new Rect(left, top, w, h), tex, uv, true);
        GUI.color = Color.white;
    }

    // 로비 장면의 사람 그림 (로비 좌표). scale 1 = 키 약 112 로비 px
    void DrawLobbySprite(Sprite sp, float x, float scale, bool flip, float dark)
    {
        float k = 64f / sp.pixelsPerUnit * scale * lbU;                  // 로비는 1m = 64 로비 px: 스프라이트 한 픽셀이 화면 몇 px 인가
        float shade = 1f - 0.5f * dark;
        DrawSpriteGui(sp, (x - lbCam) * lbU, GroundY * lbU, k, flip, new Color(shade, shade, shade * 1.05f, 1f));
    }

    // 옆모습 사람 하나. 걸을 때 다리와 팔이 번갈아 움직인다.
    void LPerson(float x, float s, int facing, float phase, bool moving, Color jacket, Color pants, Color hat, Color skin, bool mustache)
    {
        float f = GroundY;
        float swing = moving ? Mathf.Sin(phase) * 10f * s : 0f;
        LR(x - 11f * s + swing, f - 36f * s, 10f * s, 36f * s, pants);                // 다리
        LR(x + 1f * s - swing, f - 36f * s, 10f * s, 36f * s, pants);
        LR(x - 12f * s + swing, f - 5f * s, 14f * s, 5f * s, new Color(0.08f, 0.08f, 0.1f));    // 신발
        LR(x - 2f * s - swing, f - 5f * s, 14f * s, 5f * s, new Color(0.08f, 0.08f, 0.1f));
        LR(x - 15f * s, f - 78f * s, 30f * s, 44f * s, jacket);                       // 몸통
        LR(x - 4f * s - swing * 0.7f, f - 74f * s, 8f * s, 32f * s, Mul(jacket, 0.85f));   // 팔
        LD(x, f - 96f * s, 30f * s, skin);                                            // 머리
        LR(x - 16f * s, f - 116f * s, 32f * s, 12f * s, hat);                         // 모자
        LR(x + (facing > 0 ? 6f : -22f) * s, f - 106f * s, 16f * s, 4f * s, hat);     // 모자챙
        LD(x + facing * 7f * s, f - 98f * s, 4f * s, Color.black);                    // 눈
        if (mustache) LR(x + (facing > 0 ? -2f : -14f) * s, f - 88f * s, 16f * s, 4f * s, new Color(0.35f, 0.33f, 0.32f));
    }

    // lift = 엔딩에서 하늘로 떠오른 높이. 개조한 만큼 택시가 달라진다 (황금 도색, 지붕 양옆 미니건, 날개)
    // 주인공 택시를 Retro car 그림으로 그리고, 택시 개조(황금 도색, 미니건, 날개)는 그 위에 도형으로 얹는다
    void LTaxiSprite(PixelSet car, float tx, float dark)
    {
        bool gold = projects.Done(1), guns = projects.Done(ProjectManager.Minigun), wings = projects.Done(ProjectManager.Wings);
        float f = GroundY;
        float shade = 1f - 0.5f * dark;
        if (wings)
        {
            Color wc = new Color(0.95f, 0.97f, 1f);
            LR(tx - 230f, f - 130f, 190f, 14f, wc);                                  // 날개 (뒤로 젖혀진 세 겹)
            LR(tx - 200f, f - 116f, 160f, 10f, Mul(wc, 0.92f));
            LR(tx - 170f, f - 106f, 130f, 8f, Mul(wc, 0.85f));
        }
        var clip = lobby.BoardFade >= 0f ? car.walk : car.idle;
        var sp = clip.At(Time.unscaledTime);
        Color tint = gold ? new Color(1f, 0.85f, 0.4f) : Color.white;
        tint = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
        DrawSpriteGui(sp, (tx - lbCam) * lbU, f * lbU, 64f / sp.pixelsPerUnit * lbU, false, tint);
        if (gold) LR(tx - 150f, f - 6f, 300f, 6f, new Color(0.3f, 0.95f, 1f, 0.6f));      // 언더네온
        LR(tx - 22f, f - 112f, 44f, 15f, Color.white);                                    // 지붕 TAXI 표시등
        Label(new Rect((tx - 22f - lbCam) * lbU, (f - 112f) * lbU, 44f * lbU, 15f * lbU), "TAXI", 11, Color.black, TextAnchor.MiddleCenter);
        if (guns)
        {
            Color gm = new Color(0.2f, 0.21f, 0.25f), gb = new Color(0.46f, 0.48f, 0.53f);
            LR(tx + 20f, f - 126f, 40f, 16f, gm);
            for (int k = 0; k < 3; k++) LR(tx + 60f, f - 124f + k * 5f, 80f, 3f, gb);
            LR(tx - 90f, f - 124f, 34f, 14f, Mul(gm, 0.8f));
            for (int k = 0; k < 3; k++) LR(tx - 56f, f - 122f + k * 5f, 66f, 3f, Mul(gb, 0.8f));
        }
    }

    void LTaxi(float tx, float dark, float lift)
    {
        var carArt = PixelArt.Get("lobbycar");
        if (carArt != null) { LTaxiSprite(carArt, tx, dark); return; }
        bool gold = projects.Done(1), guns = projects.Done(ProjectManager.Minigun), wings = projects.Done(ProjectManager.Wings);
        Color yellow = gold ? new Color(0.98f, 0.72f, 0.1f) : new Color(1f, 0.8f, 0.1f);
        float f = GroundY - lift;
        if (wings)
        {
            Color wc = new Color(0.95f, 0.97f, 1f);
            LR(tx - 250f, f - 165f, 200f, 14f, wc);                      // 날개 (뒤로 젖혀진 세 겹)
            LR(tx - 215f, f - 151f, 165f, 10f, Mul(wc, 0.92f));
            LR(tx - 180f, f - 141f, 130f, 8f, Mul(wc, 0.85f));
        }
        if (gold) LR(tx - 150f, f - 14f, 300f, 8f, new Color(0.3f, 0.95f, 1f, 0.6f));      // 언더네온
        LR(tx - 160f, f - 85f, 320f, 62f, yellow);                                    // 아래 차체
        LR(tx - 95f, f - 135f, 185f, 52f, yellow);                                    // 지붕 쪽
        Color glass = Color.Lerp(new Color(0.62f, 0.85f, 0.95f), new Color(0.25f, 0.3f, 0.4f), dark);
        LR(tx - 85f, f - 128f, 80f, 38f, glass);                                      // 창문
        LR(tx + 2f, f - 128f, 80f, 38f, glass);
        LR(tx - 28f, f - 154f, 56f, 20f, Color.white);                                // 지붕 TAXI 표시등
        Label(new Rect((tx - 28f - lbCam) * lbU, (f - 154f) * lbU, 56f * lbU, 20f * lbU), "TAXI", 13, Color.black, TextAnchor.MiddleCenter);
        LR(tx - 160f, f - 45f, 320f, 6f, new Color(0.1f, 0.1f, 0.12f));               // 범퍼 라인
        LR(tx - 160f, f - 62f, 320f, 5f, new Color(0.15f, 0.15f, 0.18f));             // 체커 띠
        LD(tx - 100f, f - 22f, 56f, new Color(0.05f, 0.05f, 0.06f));                  // 바퀴
        LD(tx + 100f, f - 22f, 56f, new Color(0.05f, 0.05f, 0.06f));
        LD(tx - 100f, f - 22f, 24f, new Color(0.6f, 0.6f, 0.65f));
        LD(tx + 100f, f - 22f, 24f, new Color(0.6f, 0.6f, 0.65f));
        LD(tx + 155f, f - 62f, 14f, Color.Lerp(new Color(1f, 0.95f, 0.7f), Color.white, dark));    // 앞 라이트
        if (gold) LR(tx - 160f, f - 52f, 320f, 3f, new Color(1f, 0.95f, 0.6f, 0.9f));              // 번쩍이는 띠
        if (guns)
        {
            // 지붕 양옆 미니건: 앞쪽 한 정과 반대쪽(뒤쪽, 살짝 어둡게) 한 정
            Color gm = new Color(0.2f, 0.21f, 0.25f), gb = new Color(0.46f, 0.48f, 0.53f);
            LR(tx + 40f, f - 152f, 42f, 17f, gm);
            for (int k = 0; k < 3; k++) LR(tx + 82f, f - 150f + k * 5f, 84f, 3f, gb);
            LR(tx - 90f, f - 150f, 36f, 15f, Mul(gm, 0.8f));
            for (int k = 0; k < 3; k++) LR(tx - 54f, f - 148f + k * 5f, 70f, 3f, Mul(gb, 0.8f));
        }
    }

    // 텍스처 한 장을 IMGUI 로 (점 필터 그대로)
    void DrawTexGui(Texture2D tex, Rect r, Color tint)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        tex.filterMode = FilterMode.Point;
        GUI.color = tint;
        GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    // 타일 스프라이트 한 칸을 로비 좌표(lx, ly, lw, lh)에 그린다. 화면 밖이면 건너뛴다
    void DrawTile(Sprite sp, float lx, float ly, float lw, float lh, Color tint)
    {
        if (sp == null || Event.current == null || Event.current.type != EventType.Repaint) return;
        float vw = Screen.width / lbU;
        if (lx - lbCam > vw || lx + lw - lbCam < 0f) return;
        var tex = sp.texture;
        var r = sp.textureRect;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(new Rect((lx - lbCam) * lbU, ly * lbU, lw * lbU, lh * lbU), tex, uv, true);
        GUI.color = Color.white;
    }

    // 타일 건물 하나: 1층 상점 + 윗층 창문 + 지붕 장식. 타일 한 칸 = 로비 52 x 128 (원본 26 x 64 의 두 배)
    // x = 왼쪽 위치, cols = 가로 칸, floors = 윗층 수, pal = 창문 색(0~3), shop = 1층 상점 모양(0~2), doorCol = 문이 있는 칸 (-1 이면 없음)
    void LTileBuilding(float x, int cols, int floors, int pal, int shop, int doorCol, float shade)
    {
        const float cw = 52f, rh = 128f, baseY = 560f;
        Color tint = new Color(shade, shade, shade * 1.06f, 1f);
        for (int c = 0; c < cols; c++)
        {
            float cx = x + c * cw;
            if (c == doorCol) DrawTile(doorTiles[pal], cx - 6f, baseY - rh, 64f, rh, tint);
            else DrawTile(shopTiles[shop], cx, baseY - rh, cw, rh, tint);
            for (int r = 0; r < floors; r++) DrawTile(winTiles[pal], cx, baseY - rh * (2 + r), cw, rh, tint);
            DrawTile(corniceTile, cx, baseY - rh * (1 + floors) - 64f, cw, 64f, tint);
        }
    }

    void DrawLobbyBuildings(float dark)
    {
        float shade = 1f - 0.62f * dark;
        // 택시 회사 건물 (왼쪽, 사장이 앉은 자리 뒤): 차고 + 간판
        LTileBuilding(150f, 10, 2, 1, 0, 7, shade);
        Color navy = Color.Lerp(new Color(0.1f, 0.12f, 0.18f), new Color(0.04f, 0.05f, 0.09f), dark);
        LR(150f + 52f * 5f, 560f - 128f - 96f, 52f * 5f, 64f, navy);
        Label(new Rect((150f + 52f * 5f - lbCam) * lbU, (560f - 128f - 96f) * lbU, 52f * 5f * lbU, 64f * lbU), "박 택시", 34, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
        LR(150f + 52f * 1f, 560f - 128f, 52f * 3f, 128f, new Color(0.08f, 0.08f, 0.1f));                      // 열린 차고
        LR(150f + 52f * 1f, 560f - 128f, 52f * 3f, 56f, new Color(0.5f, 0.53f, 0.58f) * shade);              // 반쯤 내려온 셔터
        for (int k = 0; k < 4; k++) LR(150f + 52f * 1f, 560f - 120f + k * 14f, 52f * 3f, 3f, new Color(0.4f, 0.43f, 0.48f) * shade);
        if (projects.Done(0))
        {
            // 차고 증축 + 네온간판: 오른쪽에 라면 휴게실을 붙인다
            LTileBuilding(150f + 52f * 10f + 8f, 3, 1, 2, 2, 1, shade);
            Color neon = new Color(1f, 0.4f, 0.8f, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f));
            LR(150f + 52f * 10f + 8f, 560f - 128f - 150f, 52f * 3f, 3f, neon);
            LR(150f + 52f * 10f + 8f, 560f - 128f - 100f, 52f * 3f, 3f, neon);
            Label(new Rect((150f + 52f * 10f + 8f - lbCam) * lbU, (560f - 128f - 146f) * lbU, 52f * 3f * lbU, 44f * lbU), "라면 휴게실", 17, new Color(0.5f, 1f, 1f), TextAnchor.MiddleCenter);
        }
        // 이웃 건물들
        LTileBuilding(900f, 5, 3, 0, 0, 1, shade);
        LTileBuilding(1210f, 6, 2, 2, 1, 4, shade);
        LTileBuilding(1850f, 6, 4, 3, 2, 1, shade);
    }

    void DrawLobbyProps(float dark)
    {
        float shade = 1f - 0.55f * dark;
        Color tint = new Color(shade, shade, shade * 1.05f, 1f);
        foreach (float lx in LampXs)
        {
            DrawTile(lampSprite, lx - 28f, 592f - 192f, 64f, 192f, Color.white);                               // 가로등은 스스로 빛난다
            LD(lx + 18f, 592f - 178f, 50f + 28f * dark, new Color(1f, 0.95f, 0.65f, 0.2f + 0.45f * dark));
        }
        DrawTile(trashSprite, 800f, 592f - 64f, 64f, 64f, tint);
        DrawTile(mailSprite, 1560f, 592f - 64f, 64f, 64f, tint);
    }

    void DrawLobbyScene(float W, float H, float u, bool interactive)
    {
        lbU = u;
        float viewW = W / u;
        // 엔딩 때는 카메라가 택시를 따라가서 떠오르는 장면이 보인다
        float focusX = lobby.PlayerX;
        lbCam = Mathf.Clamp(focusX - viewW * 0.5f, 0f, Mathf.Max(0f, LobbyManager.WorldW - viewW));
        float t = Time.unscaledTime;
        float dark = weather.Darkness;

        Fill(new Rect(0, 0, W, H), weather.SkyColor);

        // 먼 스카이라인: 도시 배경 그림이 있으면 그것을 천천히 움직이게 (없으면 도형)
        if (lobbyCityTiles && cityBackground != null)
        {
            float sc = 360f / cityBackground.height;
            float bw = cityBackground.width * sc;
            float sx = -Mathf.Repeat(lbCam * 0.35f, bw);
            Color bt = weather.BackdropTint;
            for (float x = sx; x < viewW; x += bw)
                DrawTexGui(cityBackground, new Rect(x * u, (560f - 360f) * u, bw * u, 360f * u), bt);
        }
        else
        {
            Color far = Color.Lerp(new Color(0.50f, 0.60f, 0.70f), new Color(0.08f, 0.10f, 0.18f), dark);
            for (int i = 0; i < 16; i++)
            {
                float bh = 120f + Frac(Mathf.Sin(i * 7.13f) * 1000f) * 230f;
                float bw = 110f + Frac(Mathf.Sin(i * 3.7f) * 500f) * 60f;
                Fill(new Rect((i * 150f - lbCam * 0.4f) * u, (560f - bh) * u, bw * u, (bh + 40f) * u), far);
            }

        }

        // 건물: 도시 타일 그림으로 (없으면 도형)
        if (lobbyCityTiles && hasCity) DrawLobbyBuildings(dark);
        else
        {
            // 정비소 건물
            Color wall = Color.Lerp(new Color(0.60f, 0.52f, 0.45f), new Color(0.22f, 0.2f, 0.24f), dark * 0.7f);
            LR(180f, 330f, 580f, 260f, wall);
            if (projects.Done(0))
            {
                // 정비소 증축: 오른쪽에 휴게실을 붙이고 간판에 네온을 단다
                LR(770f, 400f, 170f, 190f, Mul(wall, 0.95f));
                LR(760f, 384f, 190f, 18f, new Color(0.25f, 0.25f, 0.3f));
                LR(790f, 424f, 130f, 64f, new Color(0.08f, 0.1f, 0.18f));
                Label(new Rect((790f - lbCam) * u, 424f * u, 130f * u, 64f * u), "라면 휴게실", 18, new Color(0.5f, 1f, 1f), TextAnchor.MiddleCenter);
                Color neon = new Color(1f, 0.4f, 0.8f, 0.7f + 0.3f * Mathf.Sin(t * 5f));
                LR(326f, 258f, 288f, 3f, neon);
                LR(326f, 312f, 288f, 3f, neon);
                LR(326f, 258f, 3f, 57f, neon);
                LR(611f, 258f, 3f, 57f, neon);
            }
            LR(165f, 312f, 610f, 22f, new Color(0.25f, 0.25f, 0.3f));
            LR(330f, 262f, 280f, 50f, new Color(0.1f, 0.12f, 0.18f));
            Label(new Rect((330f - lbCam) * u, 262f * u, 280f * u, 50f * u), "박 택시", 30, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
            LR(250f, 430f, 190f, 160f, new Color(0.08f, 0.08f, 0.1f));                     // 열린 차고
            LR(250f, 430f, 190f, 66f, new Color(0.5f, 0.53f, 0.58f));                       // 반쯤 내려온 셔터
            for (int k = 0; k < 4; k++) LR(250f, 438f + k * 16f, 190f, 3f, new Color(0.4f, 0.43f, 0.48f));
            Color win = Color.Lerp(new Color(0.7f, 0.85f, 0.95f), new Color(1f, 0.88f, 0.5f), dark);
            LR(540f, 380f, 170f, 100f, win);
            LR(540f, 427f, 170f, 5f, Mul(wall, 0.8f));
            LR(623f, 380f, 5f, 100f, Mul(wall, 0.8f));

        }

        // 인도와 도로
        LR(0f, 560f, LobbyManager.WorldW, 44f, Color.Lerp(new Color(0.65f, 0.63f, 0.58f), new Color(0.25f, 0.25f, 0.3f), dark * 0.7f));
        LR(0f, 604f, LobbyManager.WorldW, 120f, Color.Lerp(new Color(0.24f, 0.24f, 0.27f), new Color(0.1f, 0.1f, 0.13f), dark * 0.7f));
        for (int i = 0; i < 14; i++) LR(i * 170f + 30f, 664f, 80f, 6f, new Color(0.85f, 0.85f, 0.75f, 0.8f));

        // 가로등과 소품
        if (lobbyCityTiles && hasCity) DrawLobbyProps(dark);
        else
        {
            foreach (float lx in LampXs)
            {
                LR(lx, 380f, 8f, 190f, new Color(0.25f, 0.27f, 0.3f));
                LR(lx - 22f, 372f, 52f, 10f, new Color(0.25f, 0.27f, 0.3f));
                LD(lx + 4f, 390f, 40f + 20f * dark, new Color(1f, 0.95f, 0.65f, 0.25f + 0.4f * dark));
            }
            LR(790f, 540f, 62f, 50f, new Color(0.55f, 0.38f, 0.2f));
            LR(852f, 562f, 40f, 28f, new Color(0.5f, 0.34f, 0.18f));

        }

        // 정비사, 택시, 주인공
        var bossArt = PixelArt.Get("boss");                                  // 택시 회사 사장 (안락의자에 앉아 있다)
        if (bossArt != null) DrawLobbySprite(bossArt.idle.At(t), LobbyManager.NpcX, 1.1f, bossArt.Flip(lobby.PlayerX > LobbyManager.NpcX ? 1f : -1f), dark);
        else LPerson(LobbyManager.NpcX, 1.18f, lobby.PlayerX < LobbyManager.NpcX ? 1 : -1, 0f, false, NpcShirt, new Color(0.18f, 0.22f, 0.35f), new Color(0.75f, 0.2f, 0.18f), NpcSkin, true);
        LTaxi(LobbyManager.TaxiX, dark, 0f);
        var driverArt = PixelArt.GetByName("npc", "Japanese guy");           // 주인공 (남자 택시 기사)
        if (driverArt != null) DrawLobbySprite((lobby.Moving ? driverArt.walk : driverArt.idle).At(t), lobby.PlayerX, 1f, driverArt.Flip(lobby.Facing), dark);
        else LPerson(lobby.PlayerX, 1f, lobby.Facing, lobby.WalkPhase, lobby.Moving, new Color(0.2f, 0.35f, 0.72f), new Color(0.22f, 0.22f, 0.28f), new Color(0.1f, 0.1f, 0.12f), new Color(0.96f, 0.8f, 0.66f), false);

        if (!interactive) return;

        // 말풍선 안내
        if (!lobby.InDialogue && lobby.BoardFade < 0f)
        {
            float bounce = Mathf.Sin(t * 4f) * 4f;
            if (lobby.NearNpc) LobbyPrompt(LobbyManager.NpcX, 398f + bounce, "Space  대화 / 상점", 220f);
            if (lobby.NearTaxi) LobbyPrompt(LobbyManager.TaxiX, 395f + bounce, "Space  택시에 타고 영업 시작", 290f);
        }

        // 위쪽 정보와 아래쪽 조작 안내
        Label(new Rect(20 * u, 14 * u, 400 * u, 40 * u), lobbyDayText, 30, UiGold, TextAnchor.UpperLeft);
        Label(new Rect(20 * u, 54 * u, 500 * u, 30 * u), shopCoinsText, 20, Color.white, TextAnchor.UpperLeft);
        Label(new Rect(20 * u, 80 * u, 500 * u, 28 * u), lobbyProjText, 17, UiCyan, TextAnchor.UpperLeft);
        Label(new Rect(20 * u, 104 * u, 500 * u, 28 * u), lobbyHeatText, 17, new Color(1f, 0.62f, 0.45f), TextAnchor.UpperLeft);
        Label(new Rect(W - 760 * u, 18 * u, 740 * u, 28 * u), modeText, 18, UiGold, TextAnchor.UpperRight);
        Label(new Rect(0, H - 34 * u, W, 28 * u), "A / D 이동      Space 말 걸기 / 타기      O 설정      Esc 타이틀", 16, new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerCenter);

        if (lobby.InDialogue) DrawLobbyDialogue(W, H, u);

        // 택시에 타면 화면이 어두워진다
        if (lobby.BoardFade >= 0f)
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, lobby.BoardFade));
            if (lobby.BoardFade > 0.5f) Label(new Rect(0, H * 0.45f, W, 60 * u), "출발!", 46, new Color(1f, 0.9f, 0.4f, (lobby.BoardFade - 0.5f) * 2f), TextAnchor.MiddleCenter);
        }
    }

    void LobbyPrompt(float wx, float y, string text, float w)
    {
        var r = new Rect((wx - w * 0.5f - lbCam) * lbU, y * lbU, w * lbU, 34f * lbU);
        Panel(r, 0.75f);
        Label(r, text, 18, UiGreen, TextAnchor.MiddleCenter);
    }

    void DrawLobbyDialogue(float W, float H, float u)
    {
        var box = new Rect(W * 0.08f, H * 0.05f, W * 0.84f, H * 0.26f);       // 위쪽에 둬서 캐릭터를 가리지 않는다
        Panel(box, 0.92f);
        var fc = new Vector2(box.x + box.height * 0.55f, box.center.y);
        if (!DrawArtFace(fc, box.height * 0.80f, PixelArt.Get("boss"), false, false))          // 택시 회사 사장의 얼굴
        {
            DrawFace(fc, box.height * 0.62f, NpcSkin, NpcHair, false);
            Dot(new Vector2(fc.x, box.yMax - box.height * 0.10f), box.height * 0.36f, NpcShirt);
        }
        float tx = box.x + box.height * 1.15f;
        Label(new Rect(tx, box.y + 12 * u, box.width - box.height * 1.2f, 36 * u), StoryCatalog.NpcName, 24, UiGold, TextAnchor.UpperLeft);
        style.wordWrap = true;
        Label(new Rect(tx, box.y + 52 * u, box.width - box.height * 1.3f, box.height - 90 * u), lobby.Shown, 24, Color.white, TextAnchor.UpperLeft);
        style.wordWrap = false;
        Label(new Rect(tx, box.yMax - 38 * u, box.width - box.height * 1.2f, 30 * u), lobby.LineDone ? "[Space] 계속" : "[Space] 빨리 보기", 18, UiCyan, TextAnchor.LowerLeft);
    }
}
