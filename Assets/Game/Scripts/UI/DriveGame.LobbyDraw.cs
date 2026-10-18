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

    static readonly float[] LampXs = { 748f, 1215f, 1945f };          // 건물/상점 사이 빈틈에 세운다 (간판을 가리지 않게)

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

    // ---- 도형 건물 더 좋게: 시차(멀수록 천천히 지나감) 중간 건물, 창문 격자, 상점가, 가로수 ----

    void LRp(float x, float y, float w, float h, Color c, float par)
    {
        Fill(new Rect((x - lbCam * par) * lbU, y * lbU, w * lbU, h * lbU), c);
    }

    // 창문 격자: 밤에는 일부 창이 노랗게 켜진다 (seed 로 어느 창이 켜지는지 정함)
    void LWindows(float x, float y, float w, float h, int cols, int rows, float dark, int seed, float par)
    {
        float cw = w / cols, rh = h / rows;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool lit = dark > 0.25f && Frac(Mathf.Sin((seed * 31 + r * 7 + c * 13) * 12.9898f) * 43758f) < 0.55f;
                Color wc = lit ? new Color(1f, 0.86f, 0.45f) : Color.Lerp(new Color(0.62f, 0.78f, 0.9f), new Color(0.12f, 0.15f, 0.24f), dark);
                LRp(x + c * cw + cw * 0.18f, y + r * rh + rh * 0.2f, cw * 0.64f, rh * 0.58f, wc, par);
            }
        }
    }

    void DrawLobbyMidCity(float dark)
    {
        const float par = 0.6f;
        for (int i = 0; i < 16; i++)
        {
            float h01 = Frac(Mathf.Sin(i * 5.71f) * 800f);
            float bw = 150f + Frac(Mathf.Sin(i * 2.9f) * 300f) * 90f;
            float bh = 190f + h01 * 150f;
            float bx = i * 210f - 100f;
            Color wall = Color.Lerp(new Color(0.46f + h01 * 0.1f, 0.5f, 0.58f), new Color(0.13f, 0.15f, 0.22f), dark * 0.85f);
            LRp(bx, 560f - bh, bw, bh, wall, par);
            LRp(bx - 4f, 560f - bh - 8f, bw + 8f, 8f, Mul(wall, 0.8f), par);                       // 지붕 테두리
            if (i % 3 == 0) LRp(bx + bw * 0.6f, 560f - bh - 40f, 6f, 32f, Mul(wall, 0.7f), par);      // 안테나
            if (i % 4 == 1) LRp(bx + bw * 0.2f, 560f - bh - 26f, 38f, 18f, Mul(wall, 0.75f), par);    // 옥상 물탱크
            LWindows(bx + 10f, 560f - bh + 14f, bw - 20f, bh - 30f, Mathf.Max(2, (int)(bw / 34f)), Mathf.Max(3, (int)(bh / 40f)), dark, i, par);
        }
    }

    // 길가 상점 하나: 윗층 창문, 1층 쇼윈도와 문, 줄무늬 차양, 간판
    void LShop(float x, float w, float h, Color wall, string name, Color awning, float dark, int seed)
    {
        const float baseY = 560f;
        float k = 1f - 0.65f * dark;
        Color c = Mul(wall, k);
        LR(x, baseY - h, w, h, c);
        LR(x - 6f, baseY - h - 10f, w + 12f, 12f, Mul(c, 0.78f));                                    // 지붕 테두리
        LWindows(x + 14f, baseY - h + 18f, w - 28f, h - 190f, Mathf.Max(2, (int)(w / 52f)), Mathf.Max(1, (int)((h - 190f) / 50f)), dark, seed, 1f);
        float fy = baseY - 118f;
        Color glass = Color.Lerp(new Color(0.66f, 0.82f, 0.9f), new Color(1f, 0.9f, 0.6f), Mathf.Clamp01(dark * 1.6f));   // 밤에는 안쪽 불빛
        LR(x + 12f, fy + 22f, w - 24f, 96f, Mul(c, 0.5f));
        LR(x + 18f, fy + 28f, w * 0.52f, 84f, glass);                                                // 쇼윈도
        LR(x + 18f + w * 0.26f, fy + 28f, 3f, 84f, Mul(c, 0.5f));                                    // 창틀
        LR(x + w * 0.62f, fy + 28f, 44f, 90f, Mul(c, 0.55f));                                        // 문
        LR(x + w * 0.62f + 6f, fy + 36f, 32f, 62f, glass);
        LR(x - 6f, fy, w + 12f, 24f, Mul(awning, k));                                                // 차양
        for (int s = 0; s < (int)((w + 12f) / 28f); s++) LR(x - 6f + s * 28f, fy, 14f, 24f, Mul(Color.white, k * 0.95f));
        LR(x + 10f, fy - 38f, w - 20f, 32f, Color.Lerp(new Color(0.1f, 0.12f, 0.18f), new Color(0.03f, 0.04f, 0.08f), dark));      // 간판
        Label(new Rect((x + 10f - lbCam) * lbU, (fy - 38f) * lbU, (w - 20f) * lbU, 32f * lbU), name, 18, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
    }

    static readonly float[] TreeXs = { 940f, 1722f, 2150f };          // 간판을 가리지 않는 자리 (라면 휴게실/편의점 사이, 약국/사무실 사이)

    // 아파트: 층마다 창문과 발코니(난간, 에어컨 실외기, 빨래), 1층 현관에는 간판 없이 차양과 우편함
    void LApartment(float x, float w, float h, Color wall, float dark, int seed)
    {
        const float baseY = 560f;
        float k = 1f - 0.65f * dark;
        Color c = Mul(wall, k);
        LR(x, baseY - h, w, h, c);
        LR(x - 5f, baseY - h - 10f, w + 10f, 12f, Mul(c, 0.75f));
        LR(x + w * 0.3f, baseY - h - 34f, 40f, 24f, Mul(c, 0.7f));                                     // 옥상 기계실
        LR(x + w - 40f, baseY - h - 22f, 4f, 12f, Mul(c, 0.6f));
        int floors = Mathf.Max(3, (int)((h - 110f) / 62f));
        for (int r = 0; r < floors; r++)
        {
            float fy = baseY - h + 18f + r * 62f;
            for (int cc = 0; cc < 2; cc++)
            {
                float wx = x + 16f + cc * (w * 0.5f);
                bool lit = dark > 0.25f && Frac(Mathf.Sin((seed * 17 + r * 5 + cc * 11) * 12.9898f) * 43758f) < 0.55f;
                Color wc = lit ? new Color(1f, 0.86f, 0.45f) : Color.Lerp(new Color(0.62f, 0.78f, 0.9f), new Color(0.12f, 0.15f, 0.24f), dark);
                LR(wx, fy, w * 0.34f, 34f, wc);                                                          // 창문
                LR(wx - 4f, fy + 36f, w * 0.34f + 8f, 5f, Mul(c, 0.6f));                                 // 발코니 바닥
                for (int b = 0; b <= 4; b++) LR(wx - 3f + b * (w * 0.34f + 6f) / 4f, fy + 26f, 2f, 12f, Mul(c, 0.55f));    // 난간
                int kind = (int)(Frac(Mathf.Sin((seed * 3 + r * 7 + cc * 5) * 4.1f) * 999f) * 4f);
                if (kind == 0) LR(wx + 4f, fy + 38f - 14f, 22f, 14f, Mul(new Color(0.8f, 0.82f, 0.85f), k));     // 에어컨 실외기
                else if (kind == 1) { LR(wx + 2f, fy + 18f, w * 0.3f, 2f, Mul(c, 0.5f)); LR(wx + 6f, fy + 20f, 12f, 16f, Mul(new Color(0.9f, 0.4f, 0.45f), k)); LR(wx + 22f, fy + 20f, 12f, 16f, Mul(new Color(0.95f, 0.95f, 0.9f), k)); }   // 빨래
            }
        }
        LR(x + w * 0.5f - 22f, baseY - 76f, 44f, 76f, Mul(c, 0.5f));                                     // 현관
        LR(x + w * 0.5f - 17f, baseY - 70f, 34f, 70f, Color.Lerp(new Color(0.66f, 0.82f, 0.9f), new Color(1f, 0.9f, 0.6f), Mathf.Clamp01(dark * 1.6f)));
        LR(x + w * 0.5f - 34f, baseY - 84f, 68f, 8f, Mul(new Color(0.4f, 0.4f, 0.46f), k));              // 현관 차양
        LR(x + 12f, baseY - 48f, 20f, 28f, Mul(new Color(0.3f, 0.4f, 0.6f), k));                         // 우편함
    }

    // 사무실 빌딩: 파란 유리창이 격자로, 유리에 비스듬한 반사, 옥상 안테나와 헬리패드, 1층 회전문
    void LOffice(float x, float w, float h, float dark, int seed)
    {
        const float baseY = 560f;
        Color frame = Color.Lerp(new Color(0.34f, 0.4f, 0.48f), new Color(0.1f, 0.12f, 0.18f), dark);
        LR(x, baseY - h, w, h, frame);
        int cols = 5, rows = Mathf.Max(5, (int)((h - 90f) / 34f));
        float cw = (w - 16f) / cols, rh = (h - 110f) / rows;
        for (int r = 0; r < rows; r++)
        {
            for (int cc = 0; cc < cols; cc++)
            {
                bool lit = dark > 0.25f && Frac(Mathf.Sin((seed * 29 + r * 7 + cc * 13) * 12.9898f) * 43758f) < 0.5f;
                Color gc = lit ? new Color(1f, 0.9f, 0.55f) : Color.Lerp(new Color(0.45f, 0.65f, 0.82f), new Color(0.1f, 0.14f, 0.26f), dark);
                LR(x + 8f + cc * cw + 1f, baseY - h + 12f + r * rh + 1f, cw - 2f, rh - 2f, gc);
                if (!lit && dark < 0.5f) LR(x + 8f + cc * cw + 1f, baseY - h + 12f + r * rh + 1f, (cw - 2f) * 0.35f, rh - 2f, new Color(1f, 1f, 1f, 0.12f));      // 유리 반사
            }
        }
        LR(x - 6f, baseY - h - 8f, w + 12f, 10f, Mul(frame, 0.7f));                                      // 옥상 테두리
        LR(x + w * 0.5f - 1.5f, baseY - h - 60f, 3f, 52f, Mul(frame, 0.6f));                            // 안테나
        LD(x + w * 0.5f, baseY - h - 62f, 8f, new Color(1f, 0.2f, 0.2f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f)));       // 깜빡이는 경고등
        LR(x, baseY - 92f, w, 6f, Mul(frame, 0.7f));                                                     // 1층 띠
        LR(x + w * 0.5f - 34f, baseY - 84f, 68f, 84f, Mul(frame, 0.55f));                                // 1층 입구
        LR(x + w * 0.5f - 28f, baseY - 78f, 56f, 78f, Color.Lerp(new Color(0.7f, 0.85f, 0.95f), new Color(1f, 0.92f, 0.65f), Mathf.Clamp01(dark * 1.6f)));
        LR(x + w * 0.5f - 1f, baseY - 78f, 2f, 78f, Mul(frame, 0.6f));
        LR(x + w * 0.5f - 44f, baseY - 98f, 88f, 8f, Mul(new Color(0.22f, 0.24f, 0.3f), 1f - 0.5f * dark));       // 입구 차양
    }

    // 벽돌 연립주택: 경사 지붕과 굴뚝, 덧창이 달린 창문, 현관 계단과 문
    void LTownhouse(float x, float w, float h, Color brick, float dark, int seed)
    {
        const float baseY = 560f;
        float k = 1f - 0.65f * dark;
        Color c = Mul(brick, k);
        LR(x, baseY - h, w, h, c);
        for (int r = 0; r < (int)(h / 14f); r++) LR(x, baseY - h + r * 14f, w, 1.5f, Mul(c, 0.82f));      // 벽돌 줄눈
        int steps = 9;
        for (int s = 0; s < steps; s++)                                                                    // 경사 지붕을 계단 모양으로
        {
            float inset = s * (w * 0.5f / steps);
            LR(x - 8f + inset, baseY - h - (s + 1) * 8f, w + 16f - inset * 2f, 8f, Mul(new Color(0.28f, 0.24f, 0.26f), 1f - 0.4f * dark));
        }
        LR(x + w * 0.72f, baseY - h - 90f, 22f, 60f, Mul(brick, 0.7f * k));                                // 굴뚝
        for (int r = 0; r < 2; r++)
        {
            for (int cc = 0; cc < 2; cc++)
            {
                float wx = x + 26f + cc * (w - 100f), wy = baseY - h + 26f + r * 82f;
                bool lit = dark > 0.25f && Frac(Mathf.Sin((seed * 13 + r * 5 + cc * 7) * 12.9898f) * 43758f) < 0.6f;
                LR(wx - 10f, wy - 2f, 12f, 52f, Mul(new Color(0.22f, 0.4f, 0.32f), k));                    // 덧창
                LR(wx + 48f, wy - 2f, 12f, 52f, Mul(new Color(0.22f, 0.4f, 0.32f), k));
                LR(wx, wy, 48f, 48f, lit ? new Color(1f, 0.86f, 0.45f) : Color.Lerp(new Color(0.62f, 0.78f, 0.9f), new Color(0.12f, 0.15f, 0.24f), dark));
                LR(wx + 23f, wy, 2f, 48f, Mul(c, 0.55f));
                LR(wx, wy + 23f, 48f, 2f, Mul(c, 0.55f));
                LR(wx - 6f, wy + 50f, 60f, 5f, Mul(new Color(0.8f, 0.78f, 0.72f), k));                     // 창턱
            }
        }
        LR(x + w * 0.5f - 22f, baseY - 84f, 44f, 84f, Mul(new Color(0.35f, 0.22f, 0.14f), k));            // 현관문
        LD(x + w * 0.5f + 12f, baseY - 42f, 5f, new Color(0.95f, 0.8f, 0.3f));
        for (int s = 0; s < 3; s++) LR(x + w * 0.5f - 30f + s * 3f, baseY - 8f + s * 0f - s * 0f, 60f - s * 6f, 5f, Mul(new Color(0.7f, 0.68f, 0.64f), k));     // 계단
    }

    void DrawLobbyShops(float dark)
    {
        // 상가만 늘어서면 밋밋하니까 종류를 섞는다: 상점, 아파트(발코니), 약국, 사무실 빌딩(유리), 벽돌 연립주택
        LShop(980f, 230f, 310f, new Color(0.78f, 0.7f, 0.58f), Loc.T("24시 편의점", "24h Deli"), new Color(0.2f, 0.55f, 0.8f), dark, 1);
        LApartment(1224f, 200f, 360f, new Color(0.72f, 0.66f, 0.6f), dark, 2);
        LShop(1440f, 210f, 270f, new Color(0.55f, 0.68f, 0.66f), Loc.T("약국", "Pharmacy"), new Color(0.25f, 0.65f, 0.4f), dark, 3);
        LOffice(1668f, 250f, 430f, dark, 4);
        LTownhouse(1950f, 230f, 300f, new Color(0.62f, 0.36f, 0.3f), dark, 5);
        // 가로수 (인도 위)
        float foliage = 1f - 0.55f * dark;
        foreach (float tx in TreeXs)
        {
            LR(tx - 5f, 470f, 10f, 92f, new Color(0.35f, 0.25f, 0.18f) * 1f);
            LD(tx, 450f, 96f, new Color(0.2f * foliage, 0.5f * foliage, 0.25f * foliage));
            LD(tx - 28f, 468f, 66f, new Color(0.24f * foliage, 0.56f * foliage, 0.28f * foliage));
            LD(tx + 28f, 466f, 70f, new Color(0.18f * foliage, 0.46f * foliage, 0.24f * foliage));
        }
    }

    // ---- 거리의 차와 사람들 ----

    // 로비 좌표로 차 하나. footDy = 발 높이 보정(뒷줄은 위로), scale = 크기, taxiSign = 지붕 TAXI 표시등
    void DrawParkedCar(PixelSet car, float x, float scale, float footDy, Color tint, float dark, bool taxiSign, bool flip)
    {
        if (car == null) return;
        var sp = car.idle.At(Time.unscaledTime + x * 0.01f);
        float shade = 1f - 0.5f * dark;
        var c = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
        float foot = GroundY + footDy;
        DrawSpriteGui(sp, (x - lbCam) * lbU, foot * lbU, 64f / sp.pixelsPerUnit * scale * lbU, flip, c);
        if (taxiSign)
        {
            float sy = foot - 112f * scale;
            LR(x - 18f * scale, sy, 36f * scale, 13f * scale, Mul(Color.white, shade));
            Label(new Rect((x - 18f * scale - lbCam) * lbU, sy * lbU, 36f * scale * lbU, 13f * scale * lbU), "TAXI", 9, Color.black, TextAnchor.MiddleCenter);
        }
    }

    void DrawLobbyStreet(float t, float dark)
    {
        float shade = 1f - 0.5f * dark;
        var taxiYellow = new Color(1f, 0.88f, 0.28f);

        // 맨 왼쪽: 사장님 차 세차 (하얀 고급차 + 거품 + 스펀지질하는 알바)
        var luxury = PixelArt.GetByName("parkcar", "LuxuryCar");
        DrawParkedCar(luxury, 330f, 1f, 0f, Color.white, dark, false, false);
        if (luxury != null)
        {
            for (int i = 0; i < 18; i++)                                                              // 거품: 차 위에서 부풀었다 터진다
            {
                float ph = Frac(i * 0.37f + t * 0.3f);
                float bx = 190f + Frac(Mathf.Sin(i * 12.3f) * 99f) * 280f;
                float by = GroundY - 24f - Frac(Mathf.Sin(i * 7.7f) * 55f) * 70f - ph * 12f;
                float sz = (8f + Frac(i * 0.61f) * 12f) * (0.6f + 0.6f * Mathf.Sin(ph * Mathf.PI));
                LD(bx, by, sz, new Color(1f, 1f, 1f, 0.9f * Mathf.Sin(ph * Mathf.PI)));
            }
            for (int i = 0; i < 6; i++)                                                               // 바닥에 흐르는 물방울
            {
                float ph = Frac(i * 0.43f + t * 0.9f);
                LD(200f + i * 50f, GroundY - 40f + ph * 40f, 4f, new Color(0.7f, 0.85f, 1f, 0.8f * (1f - ph)));
            }
        }
        LR(150f, GroundY - 16f, 26f, 16f, new Color(0.55f, 0.3f, 0.65f) * shade);                   // 양동이
        LD(163f, GroundY - 18f, 20f, new Color(1f, 1f, 1f, 0.9f));

        // 택시 승강장: 동료 택시들이 한 줄로 나란히 서 있다 (살짝 겹침). 모두 오른쪽을 본다
        LR(930f, 608f, 760f, 4f, new Color(1f, 0.85f, 0.2f, 0.85f));                                   // 승강장 노란 선
        var oldMuscle = PixelArt.GetByName("parkcar", "OldMuscleCar");
        var muscle = PixelArt.GetByName("parkcar", "MuscleCar");
        var retro = PixelArt.Get("lobbycar");
        DrawParkedCar(oldMuscle, 1080f, 1f, 0f, taxiYellow, dark, true, false);
        DrawParkedCar(muscle, 1295f, 1f, 0f, Color.white, dark, true, false);
        DrawParkedCar(retro, 1510f, 1f, 0f, taxiYellow, dark, true, false);

        // 사람들: 가까이 가면 이쪽을 본다
        for (int i = 0; i < LobbyNpcs.Length; i++)
        {
            var d = LobbyNpcs[i];
            var art = LobbyNpcArt(i);
            if (art == null)
            {
                if (i == 0) LPerson(d.x, 1.18f, lobby.PlayerX < d.x ? 1 : -1, 0f, false, NpcShirt, new Color(0.18f, 0.22f, 0.35f), new Color(0.75f, 0.2f, 0.18f), NpcSkin, true);
                continue;
            }
            float x = d.x;
            bool near = Mathf.Abs(lobby.PlayerX - x) < 380f;
            bool flip = art.Flip(near ? (lobby.PlayerX > x ? 1f : -1f) : (i % 2 == 0 ? 1f : -1f));
            if (i == 2)                                                                                 // 세차 알바: 차를 보고 왔다 갔다 닦는다
            {
                x += Mathf.Sin(t * 5f) * 16f;
                flip = art.Flip(1f);
            }
            DrawLobbySprite(art.idle.At(t + i * 0.7f), x, d.scale, flip, dark);
            if (i <= 1)                                                                                 // 사장과 비서는 머리 위에 이름이 뜬다
            {
                float ny = GroundY - (i == 0 ? 158f : 150f);
                var nr = new Rect((x - 48f - lbCam) * lbU, ny * lbU, 96f * lbU, 24f * lbU);
                Panel(nr, 0.65f);
                Label(nr, Loc.S(d.name), 14, UiGold, TextAnchor.MiddleCenter);
            }
            if (i == 2) LR(x + 26f, GroundY - 62f + Mathf.Sin(t * 5f + 1f) * 7f, 15f, 10f, new Color(1f, 0.9f, 0.2f));      // 스펀지
        }
    }

    void DrawLobbyBuildings(float dark)
    {
        float shade = 1f - 0.62f * dark;
        // 택시 회사 건물 (왼쪽, 사장이 앉은 자리 뒤): 차고 + 간판
        LTileBuilding(150f, 10, 2, 1, 0, 7, shade);
        Color navy = Color.Lerp(new Color(0.1f, 0.12f, 0.18f), new Color(0.04f, 0.05f, 0.09f), dark);
        LR(150f + 52f * 5f, 560f - 128f - 96f, 52f * 5f, 64f, navy);
        Label(new Rect((150f + 52f * 5f - lbCam) * lbU, (560f - 128f - 96f) * lbU, 52f * 5f * lbU, 64f * lbU), Loc.T("박 택시", "Big Apple Taxi"), 30, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
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
            Label(new Rect((150f + 52f * 10f + 8f - lbCam) * lbU, (560f - 128f - 146f) * lbU, 52f * 3f * lbU, 44f * lbU), Loc.T("라면 휴게실", "Ramen Lounge"), 15, new Color(0.5f, 1f, 1f), TextAnchor.MiddleCenter);
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
            DrawLobbyMidCity(dark);                               // 중간 거리 건물(창문 격자, 밤에는 불이 켜짐)
            DrawLobbyShops(dark);                                 // 길가 상점가 (편의점, 치킨집, 약국 ...)
            // 정비소 건물
            Color wall = Color.Lerp(new Color(0.60f, 0.52f, 0.45f), new Color(0.22f, 0.2f, 0.24f), dark * 0.7f);
            LR(180f, 330f, 580f, 260f, wall);
            if (projects.Done(0))
            {
                // 정비소 증축: 오른쪽에 휴게실을 붙이고 간판에 네온을 단다
                LR(770f, 320f, 170f, 270f, Mul(wall, 0.95f));
                LR(760f, 304f, 190f, 18f, new Color(0.25f, 0.25f, 0.3f));
                LR(790f, 340f, 130f, 64f, new Color(0.08f, 0.1f, 0.18f));
                Label(new Rect((790f - lbCam) * u, 340f * u, 130f * u, 64f * u), Loc.T("라면 휴게실", "Ramen Lounge"), 15, new Color(0.5f, 1f, 1f), TextAnchor.MiddleCenter);
                Color neon = new Color(1f, 0.4f, 0.8f, 0.7f + 0.3f * Mathf.Sin(t * 5f));
                LR(326f, 258f, 288f, 3f, neon);
                LR(326f, 312f, 288f, 3f, neon);
                LR(326f, 258f, 3f, 57f, neon);
                LR(611f, 258f, 3f, 57f, neon);
            }
            LR(165f, 312f, 610f, 22f, new Color(0.25f, 0.25f, 0.3f));
            LR(330f, 262f, 280f, 50f, new Color(0.1f, 0.12f, 0.18f));
            Label(new Rect((330f - lbCam) * u, 262f * u, 280f * u, 50f * u), Loc.T("박 택시", "Big Apple Taxi"), 26, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
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

        // 거리: 사장님 차 세차, 동료 택시들, 사람들, 주인공 택시
        DrawLobbyStreet(t, dark);
        LTaxi(LobbyManager.TaxiX, dark, 0f);
        var driverArt = PixelArt.GetByName("npc", "Japanese guy");           // 주인공 (남자 택시 기사)
        if (driverArt != null) DrawLobbySprite((lobby.Moving ? driverArt.walk : driverArt.idle).At(t), lobby.PlayerX, 1f, driverArt.Flip(lobby.Facing), dark);
        else LPerson(lobby.PlayerX, 1f, lobby.Facing, lobby.WalkPhase, lobby.Moving, new Color(0.2f, 0.35f, 0.72f), new Color(0.22f, 0.22f, 0.28f), new Color(0.1f, 0.1f, 0.12f), new Color(0.96f, 0.8f, 0.66f), false);

        if (!interactive) return;

        // 말풍선 안내
        if (!lobby.InDialogue && lobby.BoardFade < 0f)
        {
            float bounce = Mathf.Sin(t * 4f) * 4f;
            if (lobby.NearNpc) LobbyPrompt(LobbyNpcs[lobby.NearNpcIdx].x, 398f + bounce - (lobby.NearNpcIdx <= 1 || lobby.NearNpcIdx == 5 ? 52f : 0f), Loc.S(LobbyNpcs[lobby.NearNpcIdx].prompt), 220f);
            if (lobby.NearTaxi) LobbyPrompt(LobbyManager.TaxiX, 395f + bounce, Loc.T("Space  택시에 타고 영업 시작", "Space  Get in the cab and start the shift"), 290f);
        }

        // 위쪽 정보와 아래쪽 조작 안내: 대화 중에는 대화 상자가 위를 쓰니까 숨긴다
        if (!lobby.InDialogue)
        {
            // 왼쪽 위: 날짜, 코인, 개조, 위험도를 한 상자에
            Panel(new Rect(14 * u, 12 * u, 330 * u, 124 * u), 0.5f);
            Label(new Rect(28 * u, 16 * u, 300 * u, 36 * u), lobbyDayText, 28, UiGold, TextAnchor.UpperLeft);
            Label(new Rect(28 * u, 52 * u, 300 * u, 26 * u), shopCoinsText, 19, Color.white, TextAnchor.UpperLeft);
            Label(new Rect(28 * u, 78 * u, 300 * u, 24 * u), lobbyProjText, 16, UiCyan, TextAnchor.UpperLeft);
            Label(new Rect(28 * u, 102 * u, 300 * u, 24 * u), lobbyHeatText, 16, new Color(1f, 0.62f, 0.45f), TextAnchor.UpperLeft);

            // 오른쪽 위: 영업 모드와 최고 점수, 모드 바꾸는 키
            if (!ReferenceEquals(modeText, lobbyModeCached))
            {
                lobbyModeCached = modeText;
                lobbyModeLine1 = shiftMode ? Loc.T("하루 영업 모드 (" + Mathf.RoundToInt(shiftSeconds) + "초)", "Daily Shift (" + Mathf.RoundToInt(shiftSeconds) + "s)") : Loc.T("무한 모드", "Endless Mode");
                int bb = shiftMode ? bestShift : best;
                lobbyModeLine2 = (bb > 0 ? Loc.T("최고 점수 ", "Best score ") + bb + "      " : "") + Loc.T("Tab  모드 변경", "Tab  Switch mode");
            }
            Panel(new Rect(W - 344 * u, 12 * u, 330 * u, 62 * u), 0.5f);
            Label(new Rect(W - 336 * u, 16 * u, 314 * u, 28 * u), lobbyModeLine1, 20, UiGold, TextAnchor.UpperRight);
            Label(new Rect(W - 336 * u, 44 * u, 314 * u, 24 * u), lobbyModeLine2, 15, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperRight);

            // 아래: 조작 안내
            Panel(new Rect(W * 0.5f - 300 * u, H - 44 * u, 600 * u, 30 * u), 0.45f);
            Label(new Rect(W * 0.5f - 300 * u, H - 44 * u, 600 * u, 30 * u), Loc.T("A / D 이동      Space 말 걸기 / 타기      O 설정      Esc 타이틀", "A / D Move      Space Talk / Ride      O Settings      Esc Title"), 15, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
        }

        if (lobby.InDialogue) DrawLobbyDialogue(W, H, u);

        // 택시에 타면 화면이 어두워진다
        if (lobby.BoardFade >= 0f)
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, lobby.BoardFade));
            if (lobby.BoardFade > 0.5f) Label(new Rect(0, H * 0.45f, W, 60 * u), Loc.T("출발!", "Let's go!"), 46, new Color(1f, 0.9f, 0.4f, (lobby.BoardFade - 0.5f) * 2f), TextAnchor.MiddleCenter);
        }
    }

    string lobbyModeCached, lobbyModeLine1 = "", lobbyModeLine2 = "";

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
        if (!DrawArtFace(fc, box.height * 0.80f, lobbySpeakerArt, false, false))          // 말하는 사람의 얼굴 (사장, 비서, 동료 기사 ...)
        {
            DrawFace(fc, box.height * 0.62f, NpcSkin, NpcHair, false);
            Dot(new Vector2(fc.x, box.yMax - box.height * 0.10f), box.height * 0.36f, NpcShirt);
        }
        float tx = box.x + box.height * 1.15f;
        Label(new Rect(tx, box.y + 12 * u, box.width - box.height * 1.2f, 36 * u), lobbySpeakerName, 24, UiGold, TextAnchor.UpperLeft);
        style.wordWrap = true;
        Label(new Rect(tx, box.y + 52 * u, box.width - box.height * 1.3f, box.height - 90 * u), lobby.Shown, 24, Color.white, TextAnchor.UpperLeft);
        style.wordWrap = false;
        Label(new Rect(tx, box.yMax - 38 * u, box.width - box.height * 1.2f, 30 * u), lobby.LineDone ? Loc.T("[Space] 계속", "[Space] Continue") : Loc.T("[Space] 빨리 보기", "[Space] Skip"), 18, UiCyan, TextAnchor.LowerLeft);
    }
}
