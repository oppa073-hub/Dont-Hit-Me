using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// IMGUI 오버레이: HUD, 앞유리, 룸미러, 택시 UI, 팝업
public partial class DriveGame
{
    Texture2D whiteTex, circleTex;

    string popupText = "";

    Color popupColor = Color.white;

    int hudRem = -1, hudFareNow = -1;

    string hudPaxName = "", hudDestName = "", hudTripText = "", hudTripText2 = "";

    int hudTimeLeft = int.MinValue, hudTip = -1, hudWaitLeft = -1;
    bool hudFlee;
    string hudWaitText = "";

    int hudInterDist = -1, hudInterBlinker = -2, hudInterHeading = -1, hudInterGx = int.MinValue, hudInterGy = int.MinValue;

    string hudInterText = "";

    void Popup(string text, Color color)
    {
        BotOnPopup(text);
        popupText = text;
        popupColor = color;
        popupUntil = Time.unscaledTime + 0.8f;
    }

    // ---------- 차 내부 오버레이 / HUD (IMGUI) ----------

    GUIStyle style;

    // HUD 문자열 캐시
    int hudLives = -1, hudMaxLives = -1, hudTotal = -1, hudKmh = -1, hudNear = -1, hudHits = -1, hudLevel = -1, hudClock = -1;
    bool hudRain;

    string hudHearts = "", hudScore = "", hudSpeedText = "", hudInfoText = "", hudCountText = "";
    int hudFareTotal = -1, hudTrips = -1;
    string hudFareTotalText = "";

    // UI 팔레트: 강조(금색), 좋음(초록), 나쁨(빨강), 안내(하늘색)
    static readonly Color UiGold = new Color(1f, 0.86f, 0.38f), UiGreen = new Color(0.55f, 1f, 0.68f),
                          UiRed = new Color(1f, 0.45f, 0.4f), UiCyan = new Color(0.72f, 0.9f, 1f);

    // 모든 UI 글자는 여기서 그린다. 어두운 외곽선(큰 글자) 또는 그림자(작은 글자)를 깔아 어떤 배경에서도 읽히게 한다.
    // uiFont 를 넣으면 모든 글자에 그 글꼴이 쓰인다.
    void Label(Rect r, string text, int size, Color color, TextAnchor anchor)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
        if (uiFont != null && style.font != uiFont) style.font = uiFont;
        float px = size * Screen.height / 720f;
        style.fontSize = Mathf.RoundToInt(px);
        style.alignment = anchor;
        if (color.a > 0.05f && Event.current != null && Event.current.type == EventType.Repaint)
        {
            float o = Mathf.Max(1f, px * 0.065f);
            var shadow = new Color(0f, 0f, 0f, Mathf.Min(0.8f, color.a * 0.8f));
            style.normal.textColor = shadow;
            style.hover.textColor = shadow;
            style.active.textColor = shadow;
            style.focused.textColor = shadow;
            style.onNormal.textColor = shadow;
            style.onHover.textColor = shadow;
            style.onActive.textColor = shadow;
            style.onFocused.textColor = shadow;
            var s = r;
            if (size >= 26)
            {
                s.x = r.x - o; s.y = r.y; GUI.Label(s, text, style);
                s.x = r.x + o; GUI.Label(s, text, style);
                s.x = r.x; s.y = r.y - o; GUI.Label(s, text, style);
                s.y = r.y + o; GUI.Label(s, text, style);
            }
            else
            {
                s.x = r.x + o; s.y = r.y + o;
                GUI.Label(s, text, style);
            }
        }
        style.normal.textColor = color;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (uiAuditOn) AuditLabel(r, text, size);
#endif
        style.hover.textColor = color;                       // 마우스를 올려도 글자색이 바뀌지 않게 (IMGUI 기본 스타일은 hover/active 때 색이 달라진다)
        style.active.textColor = color;
        style.focused.textColor = color;
        style.onNormal.textColor = color;
        style.onHover.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
        GUI.Label(r, text, style);
    }

    // 글자가 상자(width, 720p 기준 px)보다 길면 글자 크기를 줄여서(최소 11) 상자 안에 들어오게 한다. 바뀔 때만 한 번 불러 값을 보관해 쓸 것
    int FitFont(string text, float width, int size)
    {
        if (string.IsNullOrEmpty(text)) return size;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
        if (uiFont != null && style.font != uiFont) style.font = uiFont;
        var content = new GUIContent(text);
        float k = Screen.height / 720f;
        for (int sz = size; sz > 11; sz--)
        {
            style.fontSize = Mathf.RoundToInt(sz * k);
            if (style.CalcSize(content).x <= width * k) return sz;
        }
        return 11;
    }

    int hudTripSize1 = 20, hudTripSize2 = 18, hudLine3Size = 17, hudWaitSize = 17, hudHintSize = 17;
    string hudLine3Cached, hudHintCached;

    // 깜빡이 화살표: 한 번 그려 둔 그림(Texture)을 룸미러 양옆에 쓴다. 오른쪽을 가리키는 두툼한 화살표에 안티앨리어싱, 위아래 밝기 차이,
    // 어두운 테두리가 있다. 왼쪽은 좌우를 뒤집어서 쓴다. dir -1 왼쪽, +1 오른쪽
    Texture2D blinkArrowTex;

    Texture2D MakeBlinkArrow()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Vector2[] poly = { new Vector2(18, 50), new Vector2(66, 50), new Vector2(66, 24), new Vector2(112, 64), new Vector2(66, 104), new Vector2(66, 78), new Vector2(18, 78) };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                // 도형까지의 부호 있는 거리 (안쪽이 음수)
                float dmin = 1e9f;
                bool inside = false;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                {
                    var a0 = poly[j]; var a1 = poly[i];
                    var e = a1 - a0;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a0, e) / e.sqrMagnitude);
                    dmin = Mathf.Min(dmin, (p - (a0 + e * t)).magnitude);
                    if ((a1.y > p.y) != (a0.y > p.y) && p.x < (a0.x - a1.x) * (p.y - a1.y) / (a0.y - a1.y) + a1.x) inside = !inside;
                }
                float d = inside ? -dmin : dmin;
                // 본체(조금 둥글게 깎은 안쪽), 테두리, 빛 번짐
                float fill = Mathf.Clamp01(0.5f - (d + 5f));                         // d < -5 이면 1
                float body = Mathf.Clamp01(0.5f - (d + 5f) * 1.0f);
                float rim = Mathf.Clamp01(0.5f - d) * (1f - body);                     // 테두리: 본체 바깥 ~ 도형 가장자리
                float glow = 0f;                                                        // 바깥 빛 번짐은 뺐다
                float grad = Mathf.Clamp01((y - 20f) / 90f);                           // 위(y 큼)로 갈수록 밝게
                var bodyC = Color.Lerp(new Color(0.12f, 0.62f, 0.2f), new Color(0.55f, 1f, 0.55f), grad);
                var rimC = new Color(0.02f, 0.22f, 0.05f);
                var glowC = new Color(0.3f, 1f, 0.4f);
                // 겹치기: 빛 -> 테두리 -> 본체
                float aG = glow * (d > 0f ? 1f : 0f);
                Color c = glowC; float a = aG;
                if (rim > 0f) { c = Color.Lerp(c, rimC, rim / Mathf.Max(0.001f, a + rim)); a = a + rim * (1f - a); }
                if (body > 0f) { c = Color.Lerp(c, bodyC, body / Mathf.Max(0.001f, a + body)); a = a + body * (1f - a); }
                c.a = a;
                px[y * N + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    void DrawBlinkArrow(Vector2 c, float size, int dir)
    {
        if (blinkArrowTex == null) blinkArrowTex = MakeBlinkArrow();
        var r = new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);
        GUI.DrawTextureWithTexCoords(r, blinkArrowTex, dir < 0 ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f), true);
    }

    // 어두운 반투명 상자 + 옅은 테두리와 윗줄 광택
    void Panel(Rect r, float alpha)
    {
        float b = Mathf.Max(1.5f, Screen.height / 720f * 2f);
        Fill(new Rect(r.x - b, r.y - b, r.width + b * 2f, r.height + b * 2f), new Color(1f, 1f, 1f, 0.16f * Mathf.Clamp01(alpha + 0.3f)));
        Fill(r, new Color(0.03f, 0.04f, 0.08f, alpha));
        Fill(new Rect(r.x, r.y, r.width, Mathf.Max(2f, r.height * 0.035f)), new Color(1f, 1f, 1f, 0.07f));
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

    // 도트 그림의 얼굴(머리 부분만 자른 것)을 그린다. 그림이 없으면 false (호출한 쪽이 예전 도형 얼굴을 그린다).
    // 놀라면 얼굴이 떨리고, 화나면 붉게 물든다. 그림은 왼쪽을 보고 있어서 대화 글자 쪽(오른쪽)을 보게 뒤집는다
    bool DrawArtFace(Vector2 c, float size, PixelSet art, bool shocked, bool angry)
    {
        if (art == null || art.Face == null) return false;
        if (Event.current == null || Event.current.type != EventType.Repaint) return true;
        var bg = new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);
        Fill(bg, new Color(0.12f, 0.14f, 0.2f, 0.9f));                                   // 얼굴 뒤 어두운 배경
        DrawArtSprite(art.Face, c, size, art.faceLeft, shocked, angry);
        return true;
    }

    // 스프라이트 하나를 정사각형 칸에 그린다 (flip 이면 좌우 뒤집기)
    void DrawArtSprite(Sprite sp, Vector2 c, float size, bool flip, bool shocked, bool angry)
    {
        if (shocked) c += new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(-2.5f, 2.5f)) * (Screen.height / 720f);
        var tex = sp.texture;
        var r = sp.textureRect;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        if (flip) { uv.x += uv.width; uv.width = -uv.width; }
        GUI.color = angry ? new Color(1f, 0.68f, 0.68f) : Color.white;
        GUI.DrawTextureWithTexCoords(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), tex, uv, true);
        GUI.color = Color.white;
    }

    // 룸미러: 항상 같은 크기. 뒷유리 너머로 지나온 도로가 뒤로 흘러가고(낮/밤/노을 색, 구급차가 오면 여기에 비친다),
    // 앞좌석의 기사(플레이어)와 뒷좌석 손님이 가슴까지 보이는 전용 그림(Bust)으로 앉아 있다. 거울 밖으로는 잘려서 안 나온다.
    // 왼쪽 아래 조작 안내: 지금 키 설정과 언어로 만든다 (바뀔 때만 새로 만든다)
    string controlsHintText = "";
    int controlsHintKey = -1;

    string ControlsHint()
    {
        int key = Loc.Lang * 100000 + Keybinds.Version;
        if (key != controlsHintKey)
        {
            controlsHintKey = key;
            var m = Keybinds.Map;
            string n(int a) { return Keybinds.KeyName(m[a]); }
            controlsHintText = Loc.Ko
                ? n(0) + "/" + n(1) + " 조향   " + n(2) + " 브레이크   " + n(3) + "/" + n(4) + " 좌/우 깜빡이   " + n(5) + " 손님 태우기   " + n(6) + " 거절   R 재시작(게임오버 후)"
                : n(0) + "/" + n(1) + " Steer   " + n(2) + " Brake   " + n(3) + "/" + n(4) + " Blinkers   " + n(5) + " Pick up   " + n(6) + " Decline   R Restart (after game over)";
        }
        return controlsHintText;
    }

    // 룸미러 자리 (화면 위 가운데). 다른 가운데 UI 는 이 아래로 쌓고, 깜빡이 표시는 양옆에 둔다
    Rect MirrorRect(float W, float H)
    {
        float mw = W * 0.19f, mh = H * 0.115f;
        return new Rect(W * 0.5f - mw * 0.5f, H * 0.092f, mw, mh);
    }

    float hudStackY;                                  // 가운데 UI 를 위에서부터 쌓는 다음 자리 (OnGUI 가 룸미러 아래로 초기화하고, 칸마다 내려간다)

    void DrawRearMirror(float W, float H)
    {
        bool carrying = taxi.State == TaxiState.Carrying;
        var r = MirrorRect(W, H);
        float mw = r.width, mh = r.height;
        Fill(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(0.1f, 0.1f, 0.12f));
        Fill(r, new Color(0.16f, 0.18f, 0.22f));                                               // 차 안 천장/벽

        GUI.BeginClip(r);                                    // 거울 안에서만 그린다 (좌표는 거울 왼쪽 위가 0,0)
        var win = new Rect(mw * 0.10f, mh * 0.08f, mw * 0.80f, mh * 0.64f);          // 뒷유리
        DrawMirrorBackdrop(win);

        // 구급차가 뒤에서 오면 뒷유리에 붉은/푸른 빛과 점점 커지는 앞모습이 보인다
        float amb = traffic.AmbulanceDist;
        if (amb >= 0f && !gameOver && flow == GameFlow.Playing)
        {
            float k = Mathf.Clamp01(1f - amb / 70f);
            bool blink = Mathf.Repeat(Time.unscaledTime, 0.25f) < 0.125f;
            Fill(new Rect(0f, 0f, mw, mh), blink ? new Color(1f, 0.1f, 0.1f, 0.30f * k + 0.08f) : new Color(0.15f, 0.3f, 1f, 0.30f * k + 0.08f));
            var front = CarArt.AmbulanceFront();                           // 흰 차의 앞모습에 경광등을 얹어 구급차로
            if (front != null)
            {
                float hh = Mathf.Max(4f, win.height * 0.9f * Mathf.Lerp(0.3f, 1f, k));          // 가까워질수록 커진다 (뒷유리 안에 들어오게 제한)
                float px = hh / front.rect.height;
                float footY = win.yMax - 1f;
                DrawSpriteGui(front, win.center.x, footY, px, false, Color.white);
                float lw = front.rect.width * px * 0.3f, lh = Mathf.Max(2f, hh * 0.14f), ly = footY - hh - lh * 0.4f;
                Fill(new Rect(win.center.x - lw * 1.05f, ly, lw, lh), blink ? new Color(1f, 0.1f, 0.1f) : new Color(0.4f, 0.1f, 0.1f));
                Fill(new Rect(win.center.x + lw * 0.05f, ly, lw, lh), blink ? new Color(0.1f, 0.2f, 0.5f) : new Color(0.2f, 0.4f, 1f));
            }
            else
            {
                float sz = k * mh * 0.5f;
                Fill(new Rect(win.center.x - sz * 0.9f, win.center.y - sz * 0.4f, sz * 1.8f, sz * 0.8f), new Color(1f, 1f, 0.92f, 0.9f));
            }
        }

        // 뒷좌석 손님: 오른쪽, 가슴까지. 어깨 아래는 뒷좌석 등받이에 가려진다
        bool shocked = Time.unscaledTime < taxi.ReactUntil && !taxi.ReactIsChat;
        if (carrying)
        {
            float ps = mh * 1.02f;
            var pc = new Vector2(mw * 0.70f, mh * 0.52f);
            if (taxi.PaxArt != null && taxi.PaxArt.Bust != null) DrawArtSprite(taxi.PaxArt.Bust, pc, ps, false, shocked, false);
            else DrawFace(new Vector2(pc.x, pc.y - ps * 0.1f), ps * 0.6f, taxi.PaxSkin, taxi.PaxHair, shocked);
        }
        Fill(new Rect(0f, mh * 0.80f, mw, mh * 0.20f), new Color(0.07f, 0.08f, 0.1f));    // 뒷좌석 등받이

        // 앞좌석 기사(플레이어): 왼쪽, 가슴까지, 손님보다 가까우니 더 크게. 손님 쪽(오른쪽)을 향한다
        var playerArt = PixelArt.GetByName("npc", "Japanese guy");
        float ds = mh * 1.28f;
        var dc = new Vector2(mw * 0.28f, mh * 0.62f);
        if (playerArt != null && playerArt.Bust != null) DrawArtSprite(playerArt.Bust, dc, ds, true, false, false);
        else DrawFace(new Vector2(dc.x, dc.y - ds * 0.1f), ds * 0.6f, PassengerCatalog.SkinTones[0], new Color(0.12f, 0.09f, 0.07f), false);
        Fill(new Rect(0f, mh * 0.93f, mw, mh * 0.07f), new Color(0.05f, 0.06f, 0.08f));  // 아래 가장자리
        GUI.EndClip();
    }

    // 뒷유리(bounds) 밖으로 삐져나가는 부분은 잘라서 칠한다 (풍경이 차 안쪽 벽 위로 번지지 않게)
    void FillIn(Rect r, Rect bounds, Color c)
    {
        float x0 = Mathf.Max(r.x, bounds.x), y0 = Mathf.Max(r.y, bounds.y), x1 = Mathf.Min(r.xMax, bounds.xMax), y1 = Mathf.Min(r.yMax, bounds.yMax);
        if (x1 <= x0 || y1 <= y0) return;
        Fill(new Rect(x0, y0, x1 - x0, y1 - y0), c);
    }

    static Color Dk(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }       // 알파는 그대로 두고 밝기만 줄인다

    // 뒷유리 너머 풍경: 하늘색(시간대), 지나온 도로가 소실점으로 모이며 중앙선이 뒤로 달아나고, 양옆 건물이 가장자리로 흘러간다
    void DrawMirrorBackdrop(Rect win)
    {
        float dark = Mathf.Lerp(1f, 0.28f, Mathf.Clamp01(weather.Darkness));
        Color sky = weather.SkyColor;
        Fill(win, new Color(sky.r * 0.8f, sky.g * 0.8f, sky.b * 0.85f));
        float vx = win.center.x, vy = win.y + win.height * 0.42f;        // 소실점
        Color fog = Dk(new Color(0.55f, 0.6f, 0.66f), dark);
        // 먼 건물 윤곽
        for (int i = 0; i < 9; i++)
        {
            float bh = (0.12f + 0.3f * Frac(Mathf.Sin((i + 3) * 12.9f) * 4375f)) * win.height;
            float bw = win.width * 0.085f;
            FillIn(new Rect(win.x + (i + 0.3f) * win.width / 9f, vy - bh, bw, bh + 1f), win, Dk(new Color(0.33f, 0.37f, 0.45f), dark));
        }
        Fill(new Rect(win.x, vy, win.width, win.yMax - vy), Dk(new Color(0.26f, 0.28f, 0.3f), dark));      // 땅
        // 도로 (소실점에서 아래로 넓어지는 사다리꼴을 가로 줄로 나눠 칠한다)
        const int Rows = 10;
        for (int i = 0; i < Rows; i++)
        {
            float t0 = i / (float)Rows, t1 = (i + 1) / (float)Rows;
            float y0 = Mathf.Lerp(vy, win.yMax, t0), y1 = Mathf.Lerp(vy, win.yMax, t1);
            float hw = win.width * 0.52f * (t0 + t1) * 0.5f;
            FillIn(new Rect(vx - hw, y0, hw * 2f, y1 - y0 + 1f), win, Dk(new Color(0.2f, 0.21f, 0.24f), dark));
        }
        // 중앙선: 지나온 거리(dist)에 따라 뒤로 달아난다 (소실점 쪽으로 모인다)
        for (int k = 0; k < 6; k++)
        {
            float z = Mathf.Repeat(k / 6f - dist * 0.04f, 1f);               // 0 = 소실점(멀리), 1 = 바로 뒤
            float t = z * z;
            float y = Mathf.Lerp(vy, win.yMax, t);
            float len = Mathf.Max(1.5f, win.height * 0.10f * t), wd = Mathf.Max(1f, win.width * 0.012f * (0.3f + t));
            Fill(new Rect(vx - wd * 0.5f, y, wd, len), Dk(new Color(0.9f, 0.85f, 0.5f), dark));
        }
        // 길가 건물: 양옆에서 가장자리 쪽으로 커지며 흘러간다
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k < 5; k++)
            {
                float z = Mathf.Repeat(k / 5f - dist * 0.025f, 1f);
                float t = z * z;
                float x = vx + side * win.width * (0.16f + 0.44f * t);
                float bh = win.height * (0.18f + 0.34f * Frac(Mathf.Sin((k + side * 7 + Mathf.Floor(dist * 0.025f - k / 5f)) * 78.2f) * 4375f)) * (0.25f + t);
                float bw = win.width * 0.10f * (0.25f + t);
                float by = Mathf.Lerp(vy, win.yMax, t);
                Color bc = Color.Lerp(fog, Dk(new Color(0.3f, 0.34f, 0.42f), dark), t);
                FillIn(new Rect(side < 0 ? x - bw : x, by - bh, bw, bh), win, bc);
            }
        }
        Fill(new Rect(win.x, win.y, win.width, 2f), new Color(0f, 0f, 0f, 0.25f));       // 창틀 그림자
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

        // 앞유리 느낌: 위쪽 선팅 띠 + 옅은 반사 줄 (천장/필러/대시보드/내비는 3D, LayoutCabin)
        for (int i = 0; i < 4; i++)
            Fill(new Rect(0, H * (0.09f + i * 0.03f), W, H * 0.03f + 1f), new Color(0.1f, 0.22f, 0.3f, 0.24f - i * 0.055f));
        var glassM = GUI.matrix;
        GUIUtility.RotateAroundPivot(-24f, new Vector2(W * 0.30f, H * 0.32f));
        Fill(new Rect(W * 0.05f, H * 0.32f - H * 0.012f, W * 0.5f, H * 0.024f), new Color(1f, 1f, 1f, 0.035f));
        Fill(new Rect(W * 0.12f, H * 0.36f, W * 0.4f, H * 0.008f), new Color(1f, 1f, 1f, 0.03f));
        GUI.matrix = glassM;

        // 룸미러 받침 (천장에서 내려온다)
        Fill(new Rect(W * 0.485f, 0, W * 0.03f, H * 0.11f), frame);
        DrawRearMirror(W, H);

        if (arms3D) return;

        // 3D 팔을 못 쓸 때의 대체: 2D 왼팔만 (대시보드/핸들/내비는 항상 3D 로 있으므로 2D 로 다시 덮지 않는다)
        Vector2 c = new Vector2(W * 0.34f, H * 0.90f);
        float R = H * 0.27f;

        float ang2d = steerAngle / wheelMaxAngle * 100f;
        var saved = GUI.matrix;

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
        if (taxi.Caller != null && taxi.Caller.t != null && taxi.State != TaxiState.Carrying && !(turning && retargeted))
        {
            Vector3 sp = cam.WorldToScreenPoint(taxi.Caller.t.position + Vector3.up * (taxi.Caller.art != null ? 2.3f : 1.2f));
            if (sp.z > 0f)
            {
                float size = Mathf.Clamp(1100f / sp.z, 26f, 90f);
                Label(new Rect(sp.x - 40 * u, H - sp.y - size * u, 80 * u, size * u * 1.2f), "!", Mathf.RoundToInt(size), new Color(1f, 0.85f, 0.1f), TextAnchor.MiddleCenter);
            }
        }

        // 목적지 표지 위 글자
        if ((taxi.State == TaxiState.Carrying || taxi.State == TaxiState.Waiting) && destMarker != null && destMarker.gameObject.activeSelf)
        {
            Vector3 sp = cam.WorldToScreenPoint(destMarker.position + Vector3.up * 3.4f);
            if (sp.z > 0f)
            {
                float size = Mathf.Clamp(1400f / sp.z, 20f, 60f);
                Label(new Rect(sp.x - 7f * size * u, H - sp.y - size * u, 14f * size * u, size * u * 1.3f), taxi.DestName, Mathf.RoundToInt(size), new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter);
            }
        }

        // 탑승 정보 (상단 가운데)
        if (taxi.State == TaxiState.Carrying)
        {
            DestRel(out int nf, out int nr, out float nsd);
            int rem = Mathf.RoundToInt(Mathf.Abs(nsd) + Mathf.Abs(nr) * Period);
            bool near = nr == 0 && Mathf.Abs(nsd) <= Mathf.Max(1f, arriveRadius);
            if (nf != hintF || nr != hintR || near != hintNear)
            {
                hintF = nf;
                hintR = nr;
                hintNear = near;
                hintText = NavHint(nf, nr, near);
            }
            int fareNow = taxi.CurrentFare();
            bool late = taxi.TripT > taxi.TripLimit;
            int timeKey = late ? -Mathf.CeilToInt(taxi.TripT - taxi.TripLimit) : Mathf.CeilToInt(taxi.TripLimit - taxi.TripT);   // 음수면 지각한 초
            int tipNow = taxi.CurrentTip();
            if (rem != hudRem || fareNow != hudFareNow || timeKey != hudTimeLeft || tipNow != hudTip || taxi.PaxName != hudPaxName || taxi.DestName != hudDestName || taxi.HeistFlee != hudFlee)
            {
                hudFlee = taxi.HeistFlee;
                hudRem = rem;
                hudFareNow = fareNow;
                hudTimeLeft = timeKey;
                hudTip = tipNow;
                hudPaxName = taxi.PaxName;
                hudDestName = taxi.DestName;
                string timeStr = late ? Loc.T("지각 " + (-timeKey) + "초", "Late " + (-timeKey) + "s") : Loc.T("남은 시간 " + timeKey + "초", "Time left " + timeKey + "s");
                string tipStr = tipNow > 0 ? Loc.T(" (팁 +", " (tip +") + Loc.Money(tipNow) + ")" : "";
                hudTripText = taxi.PaxName + " → " + taxi.DestName + Loc.T("    남은 거리 ", "    Distance ") + (taxi.AlienTrip ? "???" : rem.ToString()) + " m" + (hudFlee ? Loc.T("    경찰 추격!", "    Police chase!") : "");
                hudTripText2 = timeStr + Loc.T("      요금 ", "      Fare ") + Loc.Money(fareNow) + tipStr;
                hudTripSize1 = FitFont(hudTripText, 456f, 20);                      // 목적지 이름이 길면 글자를 줄여서 상자 밖으로 안 나가게
                hudTripSize2 = FitFont(hudTripText2, 440f, 18);
            }
            float py = hudStackY;
            Panel(new Rect(W * 0.5f - 240 * u, py, 480 * u, 96 * u), 0.55f);
            string line3 = taxi.AlienTrip ? (nf < 0 ? Loc.T("신호가 뒤쪽이에요! 다음 교차로에서 돌아가세요", "The signal is behind you! Turn around at the next intersection") : (nr > 0 ? Loc.T("초록 빛줄기가 오른쪽에 보여요  (멈추면 도착)", "The green beam is on your right  (stop to arrive)") : (nr < 0 ? Loc.T("초록 빛줄기가 왼쪽에 보여요  (멈추면 도착)", "The green beam is on your left  (stop to arrive)") : Loc.T("초록 빛줄기를 향해 곧장 가세요  (멈추면 도착)", "Head straight for the green beam  (stop to arrive)")))) : hintText;
            if (!ReferenceEquals(line3, hudLine3Cached)) { hudLine3Cached = line3; hudLine3Size = FitFont(line3, 440f, 17); }
            Label(new Rect(W * 0.5f - 232 * u, py + 3 * u, 464 * u, 30 * u), hudTripText, hudTripSize1, Color.white, TextAnchor.MiddleCenter);
            Label(new Rect(W * 0.5f - 224 * u, py + 33 * u, 448 * u, 28 * u), hudTripText2, hudTripSize2, late ? UiRed : UiGold, TextAnchor.MiddleCenter);
            Label(new Rect(W * 0.5f - 224 * u, py + 63 * u, 448 * u, 28 * u), line3, hudLine3Size, UiGreen, TextAnchor.MiddleCenter);
            hudStackY += 96 * u + 6 * u;
        }
        else if (taxi.State == TaxiState.Waiting)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(heistWaitSeconds - taxi.WaitT));
            if (left != hudWaitLeft)
            {
                hudWaitLeft = left;
                hudWaitText = Loc.T("손님을 기다리는 중…  " + left + "초      브레이크를 밟고 이 자리에 계세요!", "Waiting for your passenger…  " + left + "s      Hold the brake and stay here!");
                hudWaitSize = FitFont(hudWaitText, 448f, 17);
            }
            Panel(new Rect(W * 0.5f - 240 * u, hudStackY, 480 * u, 34 * u), 0.55f);
            Label(new Rect(W * 0.5f - 232 * u, hudStackY + 2 * u, 464 * u, 30 * u), hudWaitText, hudWaitSize, UiGold, TextAnchor.MiddleCenter);
            hudStackY += 34 * u + 6 * u;
        }
        else if (taxi.State == TaxiState.Cruise)
        {
            string hint = taxi.Caller != null
                ? Loc.T("손님이 기다려요!  인도에 붙어서 브레이크로 멈추세요", "A passenger is waiting! Pull up to the curb and brake")
                : Loc.T("손님을 찾는 중...", "Looking for a passenger...");
            if (!ReferenceEquals(hint, hudHintCached)) { hudHintCached = hint; hudHintSize = FitFont(hint, 448f, 17); }
            Panel(new Rect(W * 0.5f - 240 * u, hudStackY, 480 * u, 34 * u), 0.45f);
            Label(new Rect(W * 0.5f - 232 * u, hudStackY + 2 * u, 464 * u, 30 * u), hint, hudHintSize, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            hudStackY += 34 * u + 6 * u;
        }

        // 깜빡이: 룸미러 양옆의 큰 초록 화살표 (깜빡임). "다음 교차로 N m" 는 내비 화면에 있다 (DriveGame.Navigator.cs)
        if (blinker != 0 && Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.35f)
        {
            var mr = MirrorRect(W, H);
            float asz = 74 * u;
            DrawBlinkArrow(new Vector2(blinker < 0 ? mr.x - 6 * u - asz * 0.5f : mr.xMax + 6 * u + asz * 0.5f, mr.center.y), asz, blinker);
        }

        // 손님 대화 상자
        if (taxi.State == TaxiState.Talking && Mathf.Abs(Mathf.DeltaAngle(camYaw, camYawTarget)) < 3f)
        {
            var box = new Rect(W * 0.10f, H * 0.62f, W * 0.80f, H * 0.30f);
            Panel(box, 0.9f);
            if (!DrawArtFace(new Vector2(box.x + box.height * 0.55f, box.center.y), box.height * 0.80f, taxi.PaxArt, false, false))
            {
                DrawFace(new Vector2(box.x + box.height * 0.55f, box.center.y), box.height * 0.62f, taxi.PaxSkin, taxi.PaxHair, false);
                Dot(new Vector2(box.x + box.height * 0.55f, box.yMax - box.height * 0.10f), box.height * 0.36f, taxi.PaxShirt);
            }
            float tx = box.x + box.height * 1.15f;
            Label(new Rect(tx, box.y + 12 * u, box.width - box.height * 1.2f, 36 * u), taxi.PaxName, 24, UiGold, TextAnchor.UpperLeft);
            style.wordWrap = true;
            Label(new Rect(tx, box.y + 52 * u, box.width - box.height * 1.3f, box.height - 90 * u), taxi.PaxLine, 24, Color.white, TextAnchor.UpperLeft);
            style.wordWrap = false;
            Label(new Rect(tx, box.yMax - 38 * u, box.width - box.height * 1.2f, 30 * u), taxi.HeistLocked ? "[" + Keybinds.KeyName(Keybinds.Map[Keybinds.Accept]) + "] " + Loc.T("출발!!", "Go!!") : "[" + Keybinds.KeyName(Keybinds.Map[Keybinds.Accept]) + "] " + Loc.T("태우기", "Pick up") + "    [" + Keybinds.KeyName(Keybinds.Map[Keybinds.Decline]) + "] " + Loc.T("거절", "Decline"), 18, UiCyan, TextAnchor.LowerLeft);
        }

        // 손님의 말풍선: 사고 반응, 수다, 고함, 폭탄/미니건 반응 등 모든 말이 이 상자로 나온다.
        // 수다쟁이 손님만 화면 가운데에 크게(말로 화면을 가리는 것이 그 손님의 특징), 나머지는 오른쪽에 작게.
        if (taxi.State == TaxiState.Carrying && Time.unscaledTime < taxi.ReactUntil)
        {
            bool big = taxi.Pax != null && taxi.Pax.bigTalk;
            bool angry = !taxi.ReactIsChat || (taxi.Pax != null && taxi.Pax.quirk == Quirk.Fighter);
            if (big)
            {
                Vector2 c = new Vector2(W * 0.5f, H * 0.62f);
                Panel(new Rect(c.x - 230 * u, c.y - 120 * u, 460 * u, 240 * u), 0.7f);
                if (!DrawArtFace(new Vector2(c.x - 130 * u, c.y), 170 * u, taxi.PaxArt, angry, angry))
                    DrawFace(new Vector2(c.x - 130 * u, c.y), 150 * u, taxi.PaxSkin, taxi.PaxHair, angry);
                style.wordWrap = true;
                Label(new Rect(c.x - 40 * u, c.y - 60 * u, 260 * u, 120 * u), taxi.ReactText, 28, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft);
                style.wordWrap = false;
            }
            else
            {
                var box = new Rect(W - 340 * u, ((shiftMode ? 160f : 126f) + 24f) * u, 326 * u, 118 * u);
                Panel(box, 0.7f);
                if (!DrawArtFace(new Vector2(box.x + 56 * u, box.center.y), 92 * u, taxi.PaxArt, angry, angry))
                    DrawFace(new Vector2(box.x + 56 * u, box.center.y), 84 * u, taxi.PaxSkin, taxi.PaxHair, angry);
                style.wordWrap = true;
                Label(new Rect(box.x + 108 * u, box.y + 8 * u, box.width - 118 * u, box.height - 16 * u), taxi.ReactText, 18, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleLeft);
                style.wordWrap = false;
            }
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;       // 그리기만 하는 화면이라 Layout/입력 이벤트에서는 건너뛴다 (CPU 절약)
        float W = Screen.width, H = Screen.height, u = H / 720f;

        // 로비와 정비소(상점), 로비에서 연 설정은 로비 장면 위에 그린다
        if (flow == GameFlow.Lobby || flow == GameFlow.Shop || (flow == GameFlow.Settings && settingsReturn == GameFlow.Lobby))
        {
            DrawLobbyScene(W, H, u, flow == GameFlow.Lobby);
            if (flow == GameFlow.Shop) DrawShop(W, H, u);
            else if (flow == GameFlow.Settings) DrawSettings(W, H, u);
            DrawDebug(W, H, u);
            return;
        }

        if (flow == GameFlow.Ending)
        {
            DrawEnding(W, H, u);
            DrawDebug(W, H, u);
            return;
        }

        if (hurtFlash > 0f && flow == GameFlow.Playing && !gameOver) Fill(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, hurtFlash * 0.6f));

        DrawCabin();
        DrawWeatherOverlay(W, H, u);
        DrawRageOverlay(W, H);
        DrawPoliceOverlay(W, H);
        DrawAmbulanceOverlay(W, H);
        DrawBlood(W, H, u);
        DrawBloodWiper(W, H, u);

        if (flow == GameFlow.Title)
        {
            DrawTitle(W, H, u);                       // 타이틀 화면에서는 HUD 를 그리지 않는다
            DrawDebug(W, H, u);
            return;
        }
        if (flow == GameFlow.Shop)
        {
            DrawShop(W, H, u);
            DrawDebug(W, H, u);
            return;
        }
        if (flow == GameFlow.Settings)
        {
            DrawSettings(W, H, u);
            DrawDebug(W, H, u);
            return;
        }

        // 문자열은 값이 바뀔 때만 새로 만든다 (OnGUI 는 프레임당 여러 번 불려서 매번 만들면 GC 쓰레기가 쌓인다)
        if (lives != hudLives || maxLivesNow != hudMaxLives)
        {
            hudLives = lives;
            hudMaxLives = maxLivesNow;
            hudHearts = new string('♥', Mathf.Max(0, lives)) + new string('♡', Mathf.Max(0, maxLivesNow - lives));
        }
        int total = score + Mathf.FloorToInt(distScore);
        if (total != hudTotal)
        {
            hudTotal = total;
            hudScore = Loc.T("점수  ", "Score  ") + total;
        }

        // 왼쪽 위 패널: 목숨, 점수, 누적 요금(DrawTaxiGui), 오늘의 목표
        Panel(new Rect(14 * u, 10 * u, 340 * u, 180 * u), 0.5f);
        Label(new Rect(26 * u, 12 * u, 320 * u, 36 * u), hudHearts, 28, new Color(1f, 0.4f, 0.5f), TextAnchor.UpperLeft);
        if (lives < maxLivesNow && lives > 0)
        {
            // 하트가 비어 있으면 무사고로 달린 진행도가 하트 아래에 차오른다
            Fill(new Rect(26 * u, 46 * u, 120 * u, 3 * u), new Color(1f, 1f, 1f, 0.18f));
            Fill(new Rect(26 * u, 46 * u, 120 * u * session.HealProgress, 3 * u), UiGreen);
        }
        Label(new Rect(26 * u, 48 * u, 320 * u, 30 * u), hudScore, 22, Color.white, TextAnchor.UpperLeft);
        if (taxi.FareTotal != hudFareTotal || taxi.Trips != hudTrips)
        {
            hudFareTotal = taxi.FareTotal;
            hudTrips = taxi.Trips;
            hudFareTotalText = Loc.T("요금 ", "Fare ") + Loc.Money(hudFareTotal) + "   (" + hudTrips + Loc.T("명)", " passengers)");
        }
        Label(new Rect(26 * u, 78 * u, 320 * u, 26 * u), hudFareTotalText, 17, UiGold, TextAnchor.UpperLeft);
        DrawGoals(26 * u, 108 * u, 320 * u, u, 14, TextAnchor.UpperLeft);
        if (!gameOver && flow == GameFlow.Playing) DrawTutorial(W, H, u);

        // 오른쪽 위 패널: 속도, 레벨/시각, 니어미스/충돌, 영업 시간, 날짜
        int kmh = Mathf.RoundToInt(speed * 3.6f);
        bool rainNow = weather.Rain > 0.3f;
        if (kmh != hudKmh)
        {
            hudKmh = kmh;
            hudSpeedText = kmh + " km/h";
        }
        if (level != hudLevel || weather.ClockStep != hudClock || rainNow != hudRain)
        {
            hudLevel = level; hudClock = weather.ClockStep; hudRain = rainNow;
            hudInfoText = "LV " + level + "      " + weather.ClockText() + (rainNow ? Loc.T("   비", "   Rain") : "");
        }
        if (nearMisses != hudNear || hits != hudHits)
        {
            hudNear = nearMisses; hudHits = hits;
            hudCountText = Loc.T("니어미스 ", "Near misses ") + nearMisses + Loc.T("     충돌 ", "     Hits ") + hits;
        }
        Panel(new Rect(W - 346 * u, 10 * u, 332 * u, (shiftMode ? 160f : 126f) * u), 0.5f);
        Label(new Rect(W - 336 * u, 12 * u, 312 * u, 36 * u), hudSpeedText, 28, Color.white, TextAnchor.UpperRight);
        Label(new Rect(W - 336 * u, 50 * u, 312 * u, 24 * u), hudInfoText, 17, UiCyan, TextAnchor.UpperRight);
        Label(new Rect(W - 336 * u, 74 * u, 312 * u, 24 * u), hudCountText, 17, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperRight);
        if (shiftMode) Label(new Rect(W - 336 * u, 98 * u, 312 * u, 34 * u), ShiftHudText(), 26, shiftLeft <= 30f ? UiRed : UiGold, TextAnchor.UpperRight);
        Label(new Rect(W - 336 * u, (shiftMode ? 132f : 98f) * u, 312 * u, 24 * u), dayEvents.DayLabel, 17, dayEvents.Active ? UiRed : new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperRight);

        // 가운데 UI 쌓기 시작: 룸미러 바로 아래. 긴급 속보가 있으면 맨 위 칸을 차지한다
        hudStackY = MirrorRect(W, H).yMax + 14 * u;
        if (!gameOver && Time.unscaledTime < dayEvents.AlertUntil)
        {
            // 긴급 속보: 붉게 깜빡이는 띠
            float pulse = 0.7f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
            float ay = hudStackY;
            Fill(new Rect(W * 0.5f - 242 * u, ay, 484 * u, 56 * u), new Color(1f, 1f, 1f, 0.55f));
            Fill(new Rect(W * 0.5f - 240 * u, ay + 2 * u, 480 * u, 52 * u), new Color(0.6f, 0.05f, 0.05f, pulse));
            style.wordWrap = true;
            Label(new Rect(W * 0.5f - 230 * u, ay + 2 * u, 460 * u, 52 * u), dayEvents.AlertText, 19, Color.white, TextAnchor.MiddleCenter);
            style.wordWrap = false;
            hudStackY += 56 * u + 6 * u;
        }

        Label(new Rect(20 * u, H - 30 * u, W * 0.62f, 26 * u), ControlsHint(), 15, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerLeft);   // 내비(오른쪽 아래)와 겹치지 않게 왼쪽 정렬

        if (!gameOver && flow == GameFlow.Playing) DrawNewsSubtitle(W, H, u);
        if (!gameOver && flow != GameFlow.Paused) DrawTaxiGui(W, H, u);       // 게임오버/일시정지 글자와 겹치지 않게
        if (!gameOver && flow != GameFlow.Paused) DrawQuirkHud(W, H, u);

        // 팝업(쾅!, 아슬아슬! 등)과 도착 인사: 가운데 UI 쌓기 맨 아래의 한 자리에 차례로 (화면 한복판을 가리지 않는다)
        float msgY = hudStackY + 2 * u;
        bool popupOn = Time.unscaledTime < popupUntil && !gameOver;
        if (popupOn)
        {
            float age = Time.unscaledTime - (popupUntil - 0.8f);
            float pop = 1f + 0.3f * Mathf.Clamp01(1f - age / 0.15f);                          // 처음엔 크게 튀어나왔다가 제 크기로
            float fadeP = Mathf.Clamp01((popupUntil - Time.unscaledTime) / 0.25f);           // 끝에서 부드럽게 사라진다
            Color pc = popupColor;
            pc.a *= fadeP;
            Label(new Rect(0, msgY, W, 52 * u), popupText, Mathf.RoundToInt(32f * pop), pc, TextAnchor.MiddleCenter);
        }
        if (!gameOver && (taxi.State == TaxiState.Cruise || taxi.State == TaxiState.Waiting) && Time.unscaledTime < taxi.ThanksUntil)
            Label(new Rect(0, msgY + (popupOn ? 50 * u : 0f), W, 34 * u), taxi.ThanksText, 20, new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleCenter);

        // 회전 중 장면 교체 순간에 잠깐 어두워진다
        if (fade > 0f) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, Mathf.Clamp01(fade)));

        if (gameOver) DrawGameOver(W, H, u);
        else if (flow == GameFlow.Paused) DrawPause(W, H, u);
        DrawDebug(W, H, u);
    }
}
