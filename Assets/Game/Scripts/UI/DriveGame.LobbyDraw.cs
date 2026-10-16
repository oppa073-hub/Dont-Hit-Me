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

    void LTaxi(float tx, float dark)
    {
        Color yellow = new Color(1f, 0.8f, 0.1f);
        float f = GroundY;
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
    }

    void DrawLobbyScene(float W, float H, float u, bool interactive)
    {
        lbU = u;
        float viewW = W / u;
        lbCam = Mathf.Clamp(lobby.PlayerX - viewW * 0.5f, 0f, Mathf.Max(0f, LobbyManager.WorldW - viewW));
        float t = Time.unscaledTime;
        float dark = weather.Darkness;

        Fill(new Rect(0, 0, W, H), weather.SkyColor);

        // 먼 건물 (천천히 움직이는 배경)
        Color far = Color.Lerp(new Color(0.50f, 0.60f, 0.70f), new Color(0.08f, 0.10f, 0.18f), dark);
        for (int i = 0; i < 16; i++)
        {
            float bh = 120f + Frac(Mathf.Sin(i * 7.13f) * 1000f) * 230f;
            float bw = 110f + Frac(Mathf.Sin(i * 3.7f) * 500f) * 60f;
            Fill(new Rect((i * 150f - lbCam * 0.4f) * u, (560f - bh) * u, bw * u, (bh + 40f) * u), far);
        }

        // 정비소 건물
        Color wall = Color.Lerp(new Color(0.60f, 0.52f, 0.45f), new Color(0.22f, 0.2f, 0.24f), dark * 0.7f);
        LR(180f, 330f, 580f, 260f, wall);
        LR(165f, 312f, 610f, 22f, new Color(0.25f, 0.25f, 0.3f));
        LR(330f, 262f, 280f, 50f, new Color(0.1f, 0.12f, 0.18f));
        Label(new Rect((330f - lbCam) * u, 262f * u, 280f * u, 50f * u), "박 정비소", 30, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
        LR(250f, 430f, 190f, 160f, new Color(0.08f, 0.08f, 0.1f));                     // 열린 차고
        LR(250f, 430f, 190f, 66f, new Color(0.5f, 0.53f, 0.58f));                       // 반쯤 내려온 셔터
        for (int k = 0; k < 4; k++) LR(250f, 438f + k * 16f, 190f, 3f, new Color(0.4f, 0.43f, 0.48f));
        Color win = Color.Lerp(new Color(0.7f, 0.85f, 0.95f), new Color(1f, 0.88f, 0.5f), dark);
        LR(540f, 380f, 170f, 100f, win);
        LR(540f, 427f, 170f, 5f, Mul(wall, 0.8f));
        LR(623f, 380f, 5f, 100f, Mul(wall, 0.8f));

        // 인도와 도로
        LR(0f, 560f, LobbyManager.WorldW, 44f, Color.Lerp(new Color(0.65f, 0.63f, 0.58f), new Color(0.25f, 0.25f, 0.3f), dark * 0.7f));
        LR(0f, 604f, LobbyManager.WorldW, 120f, Color.Lerp(new Color(0.24f, 0.24f, 0.27f), new Color(0.1f, 0.1f, 0.13f), dark * 0.7f));
        for (int i = 0; i < 14; i++) LR(i * 170f + 30f, 664f, 80f, 6f, new Color(0.85f, 0.85f, 0.75f, 0.8f));

        // 가로등과 상자
        foreach (float lx in LampXs)
        {
            LR(lx, 380f, 8f, 190f, new Color(0.25f, 0.27f, 0.3f));
            LR(lx - 22f, 372f, 52f, 10f, new Color(0.25f, 0.27f, 0.3f));
            LD(lx + 4f, 390f, 40f + 20f * dark, new Color(1f, 0.95f, 0.65f, 0.25f + 0.4f * dark));
        }
        LR(790f, 540f, 62f, 50f, new Color(0.55f, 0.38f, 0.2f));
        LR(852f, 562f, 40f, 28f, new Color(0.5f, 0.34f, 0.18f));

        // 정비사, 택시, 주인공
        LPerson(LobbyManager.NpcX, 1.18f, lobby.PlayerX < LobbyManager.NpcX ? 1 : -1, 0f, false, NpcShirt, new Color(0.18f, 0.22f, 0.35f), new Color(0.75f, 0.2f, 0.18f), NpcSkin, true);
        LTaxi(LobbyManager.TaxiX, dark);
        LPerson(lobby.PlayerX, 1f, lobby.Facing, lobby.WalkPhase, lobby.Moving, new Color(0.2f, 0.35f, 0.72f), new Color(0.22f, 0.22f, 0.28f), new Color(0.1f, 0.1f, 0.12f), new Color(0.96f, 0.8f, 0.66f), false);

        if (!interactive) return;

        // 말풍선 안내
        if (!lobby.InDialogue && lobby.BoardFade < 0f)
        {
            float bounce = Mathf.Sin(t * 4f) * 4f;
            if (lobby.NearNpc) LobbyPrompt(LobbyManager.NpcX, 398f + bounce, "Space  대화 / 상점", 220f);
            if (lobby.NearTaxi) LobbyPrompt(LobbyManager.TaxiX, 395f + bounce, "Space  택시에 타고 영업 시작", 290f);
        }

        // 위쪽 정보와 아래쪽 조작 안내
        Label(new Rect(20 * u, 14 * u, 400 * u, 40 * u), lobbyDayText, 30, new Color(1f, 0.92f, 0.5f), TextAnchor.UpperLeft);
        Label(new Rect(20 * u, 54 * u, 500 * u, 30 * u), shopCoinsText, 20, Color.white, TextAnchor.UpperLeft);
        Label(new Rect(W - 760 * u, 18 * u, 740 * u, 28 * u), modeText, 18, new Color(1f, 0.92f, 0.5f), TextAnchor.UpperRight);
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
        Fill(r, new Color(0f, 0f, 0f, 0.65f));
        Label(r, text, 18, new Color(0.7f, 1f, 0.8f), TextAnchor.MiddleCenter);
    }

    void DrawLobbyDialogue(float W, float H, float u)
    {
        var box = new Rect(W * 0.08f, H * 0.05f, W * 0.84f, H * 0.26f);       // 위쪽에 둬서 캐릭터를 가리지 않는다
        Fill(box, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        var fc = new Vector2(box.x + box.height * 0.55f, box.center.y);
        DrawFace(fc, box.height * 0.62f, NpcSkin, NpcHair, false);
        Dot(new Vector2(fc.x, box.yMax - box.height * 0.10f), box.height * 0.36f, NpcShirt);
        float tx = box.x + box.height * 1.15f;
        Label(new Rect(tx, box.y + 12 * u, box.width - box.height * 1.2f, 36 * u), StoryCatalog.NpcName, 24, new Color(1f, 0.85f, 0.4f), TextAnchor.UpperLeft);
        style.wordWrap = true;
        Label(new Rect(tx, box.y + 52 * u, box.width - box.height * 1.3f, box.height - 90 * u), lobby.Shown, 24, Color.white, TextAnchor.UpperLeft);
        style.wordWrap = false;
        Label(new Rect(tx, box.yMax - 38 * u, box.width - box.height * 1.2f, 30 * u), lobby.LineDone ? "[Space] 계속" : "[Space] 빨리 보기", 18, new Color(0.7f, 0.9f, 1f), TextAnchor.LowerLeft);
    }
}
