using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 엔딩: 하늘을 나는 택시(마지막 택시 개조)를 산 뒤, 처음 태우는 일반 손님과 함께 달까지 날아간다.
// 시점은 택시의 뒷모습. 날개가 펴지고 로켓처럼 치솟으며 카메라가 점점 멀어지고, 도시가 작아지고 하늘이 어두워지고 별이 쏟아지고 달이 커진다.
// 손님은 내내 소리 지르며 애원한다(대사: StoryCatalog.EndingTimes/EndingTexts). 모두 도형으로 그린 임시 그림이고 월드(3D)는 쓰지 않는다.
// 타이밍은 인스펙터 Ending 헤더(endingLaunchAt, endingLandAt, endingShake). F4(디버그)로 바로 볼 수 있다.
public partial class DriveGame
{
    const string EndingSeenKey = "DontHitMe.EndingSeen";

    float endT, edScroll, edRocketT, edU, edVw;
    Vector2 edOff;                                  // 화면 흔들림 (논리 좌표)
    bool edLaunched, edLanded, edWingsSound;
    int edLineIdx;
    Color edSkin = new Color(0.96f, 0.8f, 0.66f), edHair = new Color(0.15f, 0.1f, 0.08f);
    string edName = "손님";

    // 날개를 단 뒤 처음 태우는 일반 손님이면 엔딩 (은행 강도 같은 특수 손님은 건너뛴다)
    bool ShouldStartEnding()
    {
        if (!projects.Done(ProjectManager.Wings)) return false;
        if (PlayerPrefs.GetInt(EndingSeenKey, 0) == 1) return false;
        return taxi.Pax != null && !taxi.Pax.heist;
    }

    // natural = false 면(디버그 F4 로 미리 보기) "엔딩을 봤다"고 기록하지 않아서, 나중에 진짜 조건(날개 개조 뒤 첫 손님)에 다시 나온다
    void StartEnding(bool natural = true)
    {
        flow = GameFlow.Ending;
        endT = 0f;
        edScroll = 0f;
        edRocketT = 0f;
        edLaunched = edLanded = edWingsSound = false;
        edLineIdx = -1;
        edOff = Vector2.zero;
        if (taxi.Pax != null)
        {
            edSkin = taxi.PaxSkin;
            edHair = taxi.PaxHair;
            edName = string.IsNullOrEmpty(taxi.PaxName) ? "손님" : taxi.PaxName;
        }
        if (natural)
        {
            PlayerPrefs.SetInt(EndingSeenKey, 1);
            PlayerPrefs.Save();
        }
        camYawTarget = 0f;
        camPitchTarget = 0f;
        EndNews();
    }

    static float EndSm(float a, float b, float x)
    {
        x = Mathf.Clamp01((x - a) / (b - a));
        return x * x * (3f - 2f * x);
    }

    // 달리는 느낌의 세기 (0 정지 ~ 1 최고 속도). 달에 가까워지면 다시 줄어든다
    float EndSpeed()
    {
        float L = endingLaunchAt, A = endingLandAt;
        if (endT < L || endT >= A) return 0f;
        return EndSm(L, L + 1.4f, endT) * (1f - 0.85f * EndSm(A - 5f, A - 0.3f, endT));
    }

    void UpdateEnding(Keyboard kb)
    {
        float dt = Time.unscaledDeltaTime;
        endT += dt;
        float L = endingLaunchAt, A = endingLandAt;

        if (!edWingsSound && endT >= L - 1.7f)
        {
            edWingsSound = true;
            sound.PlayWingsOpen();                 // 날개가 철컹 펴지는 소리와 점점 커지는 휘이잉
        }
        if (!edLaunched && endT >= L)
        {
            edLaunched = true;
            sound.PlayRocket();
            sound.PlayStomp(1f);                   // 쾅 하고 발사
        }
        if (!edLanded && endT >= A)
        {
            edLanded = true;
            sound.PlayStomp(0.6f);                 // 달에 사뿐히(쿵)
            sound.PlayChime();
        }

        // 손님의 말: 소리치는 대사(!!)는 비명, 나머지는 웅얼
        var lt = StoryCatalog.EndingTimes;
        while (edLineIdx + 1 < lt.Length && endT >= lt[edLineIdx + 1])
        {
            edLineIdx++;
            string line = StoryCatalog.EndingTexts[edLineIdx];
            if (line.Contains("!!") || line.Contains("아아")) sound.PlayScream(taxi.PaxMale);
            else sound.Speak(line, PaxVoice(), voicePaxVolume);
        }

        // 반복 소리: 발사 전에는 저음 진동이 커지고, 날아가는 동안 바람, 우주로 나가면 바람이 줄고 낮은 울림
        float space = EndSm(A * 0.55f, A * 0.85f, endT);
        float windV = EndSpeed() * (1f - space) + (endT >= L ? 0.1f * (1f - space) : 0f);
        float rumbleV = endT < L ? 0.7f * EndSm(0f, L, endT) : 0.5f * EndSpeed() * (1f - space);
        float droneV = 0.55f * space * (endT >= A ? 0.6f : 1f);
        sound.EndingAmbience(windV, rumbleV, droneV);

        float sf = EndSpeed();
        edScroll += sf * dt;

        // 화면 흔들림: 날개가 펴지기 전부터 떨리기 시작해서 날아가는 내내 심하게, 도착 직전에 가장 크게
        float amp = 0f;
        if (endT < L) amp = 4f * EndSm(L - 1.6f, L, endT);
        else if (endT < A) amp = 7f + 7f * sf + 8f * EndSm(A - 3f, A - 0.2f, endT);
        edOff = amp > 0f ? Random.insideUnitCircle * amp * endingShake : Vector2.zero;

        bool skip = kb.escapeKey.wasPressedThisFrame && endT > 6f;
        bool done = endT >= A + 12f && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
        if (skip || done) FinishEnding();
    }

    // 엔딩이 끝났다: 이번 영업을 결산하고 로비로 돌아간다
    void FinishEnding()
    {
        sound.StopEndingAmbience();
        sound.StopSpeech();
        SaveResultOnce();
        EnterLobby();
    }

    // ---- 그리기 ----

    void ER(float x, float y, float w, float h, Color c)
    {
        Fill(new Rect((x + edOff.x) * edU, (y + edOff.y) * edU, w * edU, h * edU), c);
    }

    void ED(float cx, float cy, float size, Color c)
    {
        Dot(new Vector2((cx + edOff.x) * edU, (cy + edOff.y) * edU), size * edU, c);
    }

    void DrawEnding(float W, float H, float u)
    {
        edU = u;
        edVw = W / u;
        float vw = edVw, cx = vw * 0.5f;
        float t = endT, L = endingLaunchAt, A = endingLandAt;
        float flight = Mathf.Max(0f, t - L);
        float prog = Mathf.Clamp01((t - L) / Mathf.Max(1f, A - L));
        float sf = EndSpeed();
        bool landed = t >= A + 0.1f;

        if (!landed) DrawEndFlight(vw, cx, t, L, A, flight, prog, sf);
        else DrawEndMoon(vw, cx, t, A);

        // 날아오르는 순간과 달에 닿는 순간의 번쩍임
        if (t >= L && t < L + 0.35f) Fill(new Rect(0, 0, W, H), new Color(1f, 1f, 1f, 1f - (t - L) / 0.35f));
        if (t >= A - 0.25f && t < A + 1.8f)
            Fill(new Rect(0, 0, W, H), new Color(1f, 1f, 1f, t < A ? (t - (A - 0.25f)) / 0.25f : 1f - (t - A) / 1.8f));

        // 손님의 말
        float ov = EndSm(A + 6.5f, A + 8.5f, t);
        DrawEndSpeech(W, H, u, t, A, ov);

        // 끝: 어두워지며 마무리 글자
        if (ov > 0f)
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, ov));
            float a1 = EndSm(A + 8f, A + 9.5f, t), a2 = EndSm(A + 9f, A + 10.5f, t), a3 = EndSm(A + 11f, A + 12f, t);
            Label(new Rect(0, H * 0.28f, W, 80 * u), "하늘을 나는 택시", 60, new Color(UiGold.r, UiGold.g, UiGold.b, a1), TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.28f + 90 * u, W, 36 * u), "…그리고 택시는 달에 도착했다. 손님은 내렸고, 요금은 못 받았다.", 24, new Color(1f, 1f, 1f, a2), TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.28f + 130 * u, W, 36 * u), "(월세는 아직 안 냈다)", 20, new Color(1f, 1f, 1f, a2 * 0.8f), TextAnchor.MiddleCenter);
            Label(new Rect(0, H * 0.28f + 200 * u, W, 36 * u), "THE END?   영업은 계속된다", 24, new Color(UiGreen.r, UiGreen.g, UiGreen.b, a3), TextAnchor.MiddleCenter);
            if (t >= A + 12f && Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f)
                Label(new Rect(0, H - 70 * u, W, 30 * u), "Space  로비로", 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        }
        else if (t > 6f)
        {
            Label(new Rect(W - 260 * u, H - 34 * u, 250 * u, 26 * u), "Esc  건너뛰기", 15, new Color(1f, 1f, 1f, 0.45f), TextAnchor.LowerRight);
        }
    }

    // 지상에서 이륙해 달 쪽으로 날아가는 장면
    void DrawEndFlight(float vw, float cx, float t, float L, float A, float flight, float prog, float sf)
    {
        float dark = EndSm(0.08f, 0.5f, prog);
        Color sky = Color.Lerp(new Color(0.50f, 0.76f, 0.88f), new Color(0.01f, 0.01f, 0.06f), dark);
        ER(-20f, -20f, vw + 40f, 760f, sky);

        // 별: 하늘이 어두워지면 나타나 아래로 흘러내리는 줄이 된다
        if (dark > 0.12f)
        {
            Color sc = new Color(1f, 1f, 1f, Mathf.Clamp01((dark - 0.12f) * 1.5f));
            for (int i = 0; i < 110; i++)
            {
                float hx = Frac(Mathf.Sin(i * 12.9898f) * 43758.5453f), hy = Frac(Mathf.Sin(i * 78.233f) * 12345.678f), hs = Frac(Mathf.Sin(i * 3.17f) * 999.1f);
                float y = Mathf.Repeat(hy * 760f + edScroll * (260f + hs * 520f), 760f) - 20f;
                ER(hx * vw, y, 1.5f + hs * 1.5f, 2f + 50f * sf * hs, sc);
            }
        }

        // 구름: 하늘이 파란 동안 아래로 흘러간다
        if (dark < 0.9f)
        {
            Color cc = new Color(1f, 1f, 1f, 0.85f * (1f - dark));
            for (int i = 0; i < 8; i++)
            {
                float hx = Frac(Mathf.Sin(i * 5.31f) * 777.7f), hy = Frac(Mathf.Sin(i * 9.7f) * 333.3f);
                float y = Mathf.Repeat(hy * 1000f + edScroll * 700f, 1000f) - 160f, x = hx * vw;
                ED(x, y, 120f, cc); ED(x + 55f, y + 10f, 90f, cc); ED(x - 55f, y + 12f, 80f, cc);
            }
        }

        // 달: 도착이 가까워지면 위쪽에서 점점 커진다
        float M = A - 11f;
        if (t > M)
        {
            float mp = EndSm(M, A, t);
            float r = Mathf.Lerp(26f, 470f, mp * mp);
            float my = Mathf.Lerp(120f, 250f, mp);
            Color moon = new Color(0.88f, 0.88f, 0.84f);
            ED(cx, my, r * 2f, moon);
            Color cr = new Color(0.72f, 0.72f, 0.69f);
            ED(cx - r * 0.35f, my - r * 0.25f, r * 0.42f, cr);
            ED(cx + r * 0.30f, my + r * 0.20f, r * 0.56f, cr);
            ED(cx + r * 0.12f, my - r * 0.52f, r * 0.24f, cr);
            ED(cx - r * 0.40f, my + r * 0.42f, r * 0.20f, cr);
        }

        // 도시와 길: 날아오르면 아래로 사라진다
        float groundY = 540f + Mathf.Pow(flight, 2.4f) * 38f;
        if (groundY < 760f)
        {
            for (int i = 0; i < 22; i++)
            {
                float bh = 70f + Frac(Mathf.Sin(i * 7.13f) * 1000f) * 240f;
                float bx = i * (vw / 21f) - 20f;
                ER(bx, groundY - bh, vw / 21f - 6f, bh + 2f, Color.Lerp(new Color(0.38f, 0.46f, 0.55f), new Color(0.08f, 0.1f, 0.18f), dark));
            }
            ER(-20f, groundY, vw + 40f, 760f - groundY + 40f, new Color(0.26f, 0.26f, 0.29f));
            ER(-20f, groundY, vw + 40f, 16f, new Color(0.62f, 0.6f, 0.55f));
            for (int k = 0; k < 6; k++)
            {
                float w = 5f + k * 3f;
                ER(cx - w * 0.5f, groundY + 22f + k * k * 3.6f, w, 9f + k * 3f, new Color(0.9f, 0.9f, 0.75f, 0.85f));
            }
        }

        // 택시 (뒷모습)
        float spread = EndSm(L - 1.4f, L, t);                          // 날개가 펴지는 정도
        float s = 1f / (1f + 0.5f * flight);
        float ty = Mathf.Lerp(470f, 400f, EndSm(L, A, t)) + Mathf.Sin(t * 2.1f) * 4f * (flight > 0f ? 1f : 0f);
        float tx = cx + Mathf.Sin(t * 1.3f) * 14f * s * (flight > 0f ? 1f : 0f);
        DrawEndTaxi(tx, ty, s, spread, flight > 0f, t);

        // 속도선 (화면 가장자리에서 아래로 흐르는 하얀 줄)
        if (sf > 0.05f)
        {
            Color lc = new Color(1f, 1f, 1f, 0.32f * sf);
            for (int i = 0; i < 26; i++)
            {
                float hx = Frac(Mathf.Sin(i * 21.3f) * 555.5f), hy = Frac(Mathf.Sin(i * 4.9f) * 888.8f);
                float x = hx * vw;
                if (Mathf.Abs(x - cx) < 90f) x += (x < cx ? -1f : 1f) * 180f;                     // 택시 위는 비운다
                float y = Mathf.Repeat(hy * 900f + edScroll * 1500f, 900f) - 100f;
                ER(x, y, 2f, 50f + 150f * sf * hy, lc);
            }
        }
    }

    // 택시를 뒤에서 본 모습 (s = 크기, spread = 날개가 펴진 정도 0~1)
    void DrawEndTaxi(float cx, float cy, float s, float spread, bool flying, float t)
    {
        Color yellow = projects.Done(1) ? new Color(0.98f, 0.72f, 0.1f) : new Color(1f, 0.8f, 0.1f);
        Color wc = new Color(0.96f, 0.98f, 1f);
        float flap = flying ? 1f + 0.08f * Mathf.Sin(t * 14f) : 1f;

        // 날개: 몸통 양옆으로 깃털 다섯 겹이 부채꼴로 펴진다
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k < 5; k++)
            {
                float len = (80f + k * 32f) * (0.15f + 0.85f * spread) * flap;
                float x = side < 0 ? -140f - len : 140f;
                ER(cx + x * s, cy + (-34f - k * 17f) * s, len * s, 14f * s, Mul(wc, 1f - k * 0.05f));
            }
        }

        ER(cx - 140f * s, cy - 10f * s, 280f * s, 70f * s, yellow);                          // 아래 차체
        ER(cx - 95f * s, cy - 62f * s, 190f * s, 56f * s, yellow);                           // 지붕 쪽
        ER(cx - 80f * s, cy - 54f * s, 160f * s, 36f * s, new Color(0.18f, 0.24f, 0.32f));   // 뒷유리
        ER(cx - 30f * s, cy - 82f * s, 60f * s, 20f * s, Color.white);                       // TAXI 표시등
        if (s > 0.35f) Label(new Rect((cx - 30f + edOff.x) * edU * 1f, (cy - 82f + edOff.y) * edU, 60f * s * edU, 20f * s * edU), "TAXI", Mathf.Max(6, Mathf.RoundToInt(13f * s)), Color.black, TextAnchor.MiddleCenter);
        ER(cx - 135f * s, cy + 2f * s, 34f * s, 18f * s, new Color(0.9f, 0.1f, 0.1f));         // 브레이크등
        ER(cx + 101f * s, cy + 2f * s, 34f * s, 18f * s, new Color(0.9f, 0.1f, 0.1f));
        ER(cx - 28f * s, cy + 20f * s, 56f * s, 18f * s, Color.white);                        // 번호판
        ER(cx - 145f * s, cy + 42f * s, 290f * s, 12f * s, new Color(0.1f, 0.1f, 0.12f));       // 범퍼
        ER(cx - 110f * s, cy + 50f * s, 50f * s, 26f * s, new Color(0.05f, 0.05f, 0.06f));       // 바퀴
        ER(cx + 60f * s, cy + 50f * s, 50f * s, 26f * s, new Color(0.05f, 0.05f, 0.06f));
        if (projects.Done(1)) ER(cx - 150f * s, cy + 58f * s, 300f * s, 6f * s, new Color(0.3f, 0.95f, 1f, 0.6f));     // 언더네온

        // 분사 불꽃: 뒤에서 보면 배기구에서 눈부신 빛이 뿜어져 나온다
        if (flying)
        {
            float fl = 1f + 0.25f * Mathf.Sin(t * 40f);
            ED(cx - 70f * s, cy + 44f * s, 70f * s * fl, new Color(1f, 0.45f, 0.1f, 0.55f));
            ED(cx + 70f * s, cy + 44f * s, 70f * s * fl, new Color(1f, 0.45f, 0.1f, 0.55f));
            ED(cx - 70f * s, cy + 44f * s, 38f * s * fl, new Color(1f, 0.9f, 0.4f, 0.9f));
            ED(cx + 70f * s, cy + 44f * s, 38f * s * fl, new Color(1f, 0.9f, 0.4f, 0.9f));
        }
    }

    // 도착한 달 표면
    void DrawEndMoon(float vw, float cx, float t, float A)
    {
        ER(-20f, -20f, vw + 40f, 760f, new Color(0.01f, 0.01f, 0.05f));
        for (int i = 0; i < 90; i++)
        {
            float hx = Frac(Mathf.Sin(i * 12.9898f) * 43758.5453f), hy = Frac(Mathf.Sin(i * 78.233f) * 12345.678f), hs = Frac(Mathf.Sin(i * 3.17f) * 999.1f);
            float tw = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * (1.5f + hs * 3f) + i);
            ER(hx * vw, hy * 440f, 1.5f + hs * 1.5f, 1.5f + hs * 1.5f, new Color(1f, 1f, 1f, tw));
        }
        ED(vw * 0.82f, 120f, 78f, new Color(0.25f, 0.5f, 0.95f));                              // 저 멀리 지구
        ED(vw * 0.82f - 14f, 108f, 34f, new Color(0.35f, 0.75f, 0.4f));
        ER(-20f, 470f, vw + 40f, 300f, new Color(0.64f, 0.64f, 0.61f));                        // 달 표면
        Color cr = new Color(0.5f, 0.5f, 0.48f);
        ED(vw * 0.18f, 560f, 150f, cr); ED(vw * 0.78f, 600f, 190f, cr); ED(vw * 0.55f, 650f, 110f, cr); ED(vw * 0.35f, 690f, 120f, cr);
        float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * 2f;
        DrawEndTaxi(cx, 440f + bob, 0.75f, 0.25f, false, t);
    }

    // 손님의 말풍선 (얼굴 + 이름 + 대사). 소리칠수록 글자가 커지고 떨린다
    void DrawEndSpeech(float W, float H, float u, float t, float A, float ov)
    {
        if (ov > 0.6f) return;
        var times = StoryCatalog.EndingTimes;
        var texts = StoryCatalog.EndingTexts;
        int idx = -1;
        for (int i = 0; i < times.Length; i++) if (times[i] <= t) idx = i;
        if (idx < 0) return;
        if (idx < times.Length - 1 && t - times[idx] > 3.6f) return;
        if (idx == times.Length - 1 && t - times[idx] > 4f) return;

        string text = texts[idx];
        bool shout = text.Contains("!!");
        var r = new Rect(W * 0.5f - 450 * u, H - 150 * u, 900 * u, 108 * u);
        Panel(r, 0.72f);
        DrawFace(new Vector2(r.x + 62 * u, r.y + 56 * u), 74 * u, edSkin, edHair, idx >= 2 && !text.StartsWith("…"));
        Label(new Rect(r.x + 120 * u, r.y + 6 * u, 400 * u, 24 * u), edName, 17, UiGold, TextAnchor.UpperLeft);
        float jx = 0f, jy = 0f;
        if (shout && t < A) { jx = Random.Range(-3f, 3f) * u; jy = Random.Range(-3f, 3f) * u; }
        Label(new Rect(r.x + 120 * u + jx, r.y + 34 * u + jy, 760 * u, 60 * u), text, shout ? 30 : 26, shout ? new Color(1f, 0.85f, 0.8f) : Color.white, TextAnchor.UpperLeft);
    }
}
