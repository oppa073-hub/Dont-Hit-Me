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

    int hudTimeLeft = int.MinValue, hudTip = -1;

    int hudInterDist = -1, hudInterBlinker = -2, hudInterHeading = -1, hudInterGx = int.MinValue, hudInterGy = int.MinValue;

    string hudInterText = "";

    void Popup(string text, Color color)
    {
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
            style.normal.textColor = new Color(0f, 0f, 0f, Mathf.Min(0.8f, color.a * 0.8f));
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
        GUI.Label(r, text, style);
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

    // 룸미러: 평소엔 빈 거울, 손님을 태우면 기사 얼굴 뒤로 손님 얼굴이 보인다
    void DrawRearMirror(float W, float H)
    {
        bool carrying = taxi.State == TaxiState.Carrying;
        float mw = carrying ? W * 0.16f : W * 0.106f;
        float mh = carrying ? H * 0.085f : H * 0.04f;
        var r = new Rect(W * 0.5f - mw * 0.5f, H * 0.098f, mw, mh);
        Fill(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(0.1f, 0.1f, 0.12f));
        Fill(r, new Color(0.35f, 0.4f, 0.45f));
        if (!carrying) return;

        float s = mh * 0.62f;
        bool shocked = Time.unscaledTime < taxi.ReactUntil && !taxi.ReactIsChat;
        DrawFace(new Vector2(r.center.x + mw * 0.16f, r.center.y - mh * 0.02f), s * 0.85f, taxi.PaxSkin, taxi.PaxHair, shocked);   // 뒤쪽 손님
        DrawFace(new Vector2(r.center.x - mw * 0.14f, r.center.y + mh * 0.06f), s, PassengerCatalog.SkinTones[0], new Color(0.12f, 0.09f, 0.07f), false);   // 기사
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
            Vector3 sp = cam.WorldToScreenPoint(taxi.Caller.t.position + Vector3.up * 1.2f);
            if (sp.z > 0f)
            {
                float size = Mathf.Clamp(1100f / sp.z, 26f, 90f);
                Label(new Rect(sp.x - 40 * u, H - sp.y - size * u, 80 * u, size * u * 1.2f), "!", Mathf.RoundToInt(size), new Color(1f, 0.85f, 0.1f), TextAnchor.MiddleCenter);
            }
        }

        // 목적지 표지 위 글자
        if (taxi.State == TaxiState.Carrying && destMarker != null && destMarker.gameObject.activeSelf)
        {
            Vector3 sp = cam.WorldToScreenPoint(destMarker.position + Vector3.up * 3.4f);
            if (sp.z > 0f)
            {
                float size = Mathf.Clamp(1400f / sp.z, 20f, 60f);
                Label(new Rect(sp.x - 120 * u, H - sp.y - size * u, 240 * u, size * u * 1.3f), taxi.DestName, Mathf.RoundToInt(size), new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter);
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
            if (rem != hudRem || fareNow != hudFareNow || timeKey != hudTimeLeft || tipNow != hudTip || taxi.PaxName != hudPaxName || taxi.DestName != hudDestName)
            {
                hudRem = rem;
                hudFareNow = fareNow;
                hudTimeLeft = timeKey;
                hudTip = tipNow;
                hudPaxName = taxi.PaxName;
                hudDestName = taxi.DestName;
                string timeStr = late ? "지각 " + (-timeKey) + "초" : "남은 시간 " + timeKey + "초";
                string tipStr = tipNow > 0 ? " (팁 +" + tipNow + ")" : "";
                hudTripText = taxi.PaxName + " → " + taxi.DestName + "      남은 거리 " + rem + " m";
                hudTripText2 = timeStr + "      요금 " + fareNow + "원" + tipStr;
            }
            Panel(new Rect(W * 0.5f - 270 * u, H * 0.20f - 6 * u, 540 * u, 100 * u), 0.55f);
            Label(new Rect(W * 0.5f - 262 * u, H * 0.20f - 2 * u, 524 * u, 32 * u), hudTripText, 21, Color.white, TextAnchor.MiddleCenter);
            Label(new Rect(W * 0.5f - 262 * u, H * 0.20f + 30 * u, 524 * u, 28 * u), hudTripText2, 18, late ? UiRed : UiGold, TextAnchor.MiddleCenter);
            Label(new Rect(W * 0.5f - 262 * u, H * 0.20f + 62 * u, 524 * u, 28 * u), hintText, 17, UiGreen, TextAnchor.MiddleCenter);
        }
        else if (taxi.State == TaxiState.Cruise)
        {
            string hint = taxi.Caller != null
                ? "손님이 기다린다! 손님 쪽 인도에 붙어서 브레이크로 멈추세요"
                : "손님을 찾는 중...";
            Panel(new Rect(W * 0.5f - 270 * u, H * 0.20f - 4 * u, 540 * u, 34 * u), 0.45f);
            Label(new Rect(W * 0.5f - 262 * u, H * 0.20f - 2 * u, 524 * u, 30 * u), hint, 17, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        }

        // 깜빡이와 교차로 안내
        bool blinkOn = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.35f;
        if (blinker != 0 && blinkOn)
            Label(new Rect(blinker < 0 ? W * 0.5f - 260 * u : W * 0.5f + 120 * u, H * 0.145f, 140 * u, 40 * u),
                  blinker < 0 ? "< 좌" : "우 >", 30, new Color(0.4f, 1f, 0.4f), blinker < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);

        int dm = Mathf.RoundToInt(nextInterDist / 5f) * 5;                        // 5m 단위로 (글자를 자주 새로 만들지 않게)
        if (dm != hudInterDist || blinker != hudInterBlinker || heading != hudInterHeading || gx != hudInterGx || gy != hudInterGy)
        {
            hudInterGx = gx;
            hudInterGy = gy;
            hudInterDist = dm;
            hudInterBlinker = blinker;
            hudInterHeading = heading;
            string bl = blinker < 0 ? "좌회전 예약" : (blinker > 0 ? "우회전 예약" : "직진 (Q / E 깜빡이)");
            hudInterText = "방향 " + HeadNames[heading] + "   ·   다음 교차로 " + dm + " m   ·   " + bl;
        }
        Panel(new Rect(W * 0.5f - 270 * u, H * 0.34f - 1 * u, 540 * u, 26 * u), 0.4f);
        Label(new Rect(0, H * 0.34f, W, 26 * u), hudInterText, 16, UiCyan, TextAnchor.MiddleCenter);


        // 손님 대화 상자
        if (taxi.State == TaxiState.Talking && Mathf.Abs(Mathf.DeltaAngle(camYaw, camYawTarget)) < 3f)
        {
            var box = new Rect(W * 0.10f, H * 0.62f, W * 0.80f, H * 0.30f);
            Panel(box, 0.9f);
            DrawFace(new Vector2(box.x + box.height * 0.55f, box.center.y), box.height * 0.62f, taxi.PaxSkin, taxi.PaxHair, false);
            Dot(new Vector2(box.x + box.height * 0.55f, box.yMax - box.height * 0.10f), box.height * 0.36f, taxi.PaxShirt);
            float tx = box.x + box.height * 1.15f;
            Label(new Rect(tx, box.y + 12 * u, box.width - box.height * 1.2f, 36 * u), taxi.PaxName, 24, UiGold, TextAnchor.UpperLeft);
            style.wordWrap = true;
            Label(new Rect(tx, box.y + 52 * u, box.width - box.height * 1.3f, box.height - 90 * u), taxi.PaxLine, 24, Color.white, TextAnchor.UpperLeft);
            style.wordWrap = false;
            Label(new Rect(tx, box.yMax - 38 * u, box.width - box.height * 1.2f, 30 * u), "[Space] 태우기    [X] 거절", 18, UiCyan, TextAnchor.LowerLeft);
        }

        // 사고 시 손님 리액션
        if (taxi.State == TaxiState.Carrying && Time.unscaledTime < taxi.ReactUntil && taxi.ReactIsChat && Time.unscaledTime >= dayEvents.AlertUntil)
        {
            // 수다: 운전을 방해하지 않게 화면 위쪽 가운데에 작은 말풍선으로
            Panel(new Rect(W * 0.5f - 260 * u, 10 * u, 520 * u, 46 * u), 0.7f);
            Label(new Rect(W * 0.5f - 250 * u, 10 * u, 500 * u, 46 * u), taxi.ReactText, 20, new Color(1f, 0.95f, 0.75f), TextAnchor.MiddleCenter);
        }
        else if (taxi.State == TaxiState.Carrying && Time.unscaledTime < taxi.ReactUntil)
        {
            Vector2 c = new Vector2(W * 0.5f, H * 0.62f);
            Panel(new Rect(c.x - 230 * u, c.y - 120 * u, 460 * u, 240 * u), 0.7f);
            DrawFace(new Vector2(c.x - 130 * u, c.y), 150 * u, taxi.PaxSkin, taxi.PaxHair, !taxi.ReactIsChat);
            Label(new Rect(c.x - 40 * u, c.y - 40 * u, 260 * u, 80 * u), taxi.ReactText, 30, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft);
        }
    }

    void OnGUI()
    {
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

        if (hurtFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, hurtFlash * 0.6f));

        DrawCabin();
        DrawWeatherOverlay(W, H, u);
        DrawRageOverlay(W, H);

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
            hudScore = "점수  " + total;
        }

        // 왼쪽 위 패널: 목숨, 점수, 누적 요금(DrawTaxiGui), 오늘의 목표
        Panel(new Rect(14 * u, 10 * u, 340 * u, 180 * u), 0.5f);
        Label(new Rect(26 * u, 12 * u, 320 * u, 36 * u), hudHearts, 28, new Color(1f, 0.4f, 0.5f), TextAnchor.UpperLeft);
        Label(new Rect(26 * u, 48 * u, 320 * u, 30 * u), hudScore, 22, Color.white, TextAnchor.UpperLeft);
        if (taxi.FareTotal != hudFareTotal || taxi.Trips != hudTrips)
        {
            hudFareTotal = taxi.FareTotal;
            hudTrips = taxi.Trips;
            hudFareTotalText = "요금 " + hudFareTotal + "원   (" + hudTrips + "명)";
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
            hudInfoText = "LV " + level + "      " + weather.ClockText() + (rainNow ? "   비" : "");
        }
        if (nearMisses != hudNear || hits != hudHits)
        {
            hudNear = nearMisses; hudHits = hits;
            hudCountText = "니어미스 " + nearMisses + "     충돌 " + hits;
        }
        Panel(new Rect(W - 346 * u, 10 * u, 332 * u, (shiftMode ? 160f : 126f) * u), 0.5f);
        Label(new Rect(W - 336 * u, 12 * u, 312 * u, 36 * u), hudSpeedText, 28, Color.white, TextAnchor.UpperRight);
        Label(new Rect(W - 336 * u, 50 * u, 312 * u, 24 * u), hudInfoText, 17, UiCyan, TextAnchor.UpperRight);
        Label(new Rect(W - 336 * u, 74 * u, 312 * u, 24 * u), hudCountText, 17, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperRight);
        if (shiftMode) Label(new Rect(W - 336 * u, 98 * u, 312 * u, 34 * u), ShiftHudText(), 26, shiftLeft <= 30f ? UiRed : UiGold, TextAnchor.UpperRight);
        Label(new Rect(W - 336 * u, (shiftMode ? 132f : 98f) * u, 312 * u, 24 * u), dayEvents.DayLabel, 17, dayEvents.Active ? UiRed : new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperRight);

        if (!gameOver && Time.unscaledTime < dayEvents.AlertUntil)
        {
            // 긴급 속보: 붉게 깜빡이는 띠
            float pulse = 0.7f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
            Fill(new Rect(W * 0.5f - 262 * u, 6 * u, 524 * u, 60 * u), new Color(1f, 1f, 1f, 0.55f));
            Fill(new Rect(W * 0.5f - 260 * u, 8 * u, 520 * u, 56 * u), new Color(0.6f, 0.05f, 0.05f, pulse));
            style.wordWrap = true;
            Label(new Rect(W * 0.5f - 250 * u, 8 * u, 500 * u, 56 * u), dayEvents.AlertText, 19, Color.white, TextAnchor.MiddleCenter);
            style.wordWrap = false;
        }

        Label(new Rect(20 * u, H - 30 * u, W * 0.62f, 26 * u), "A/D 조향   S 브레이크   Q/E 좌/우 깜빡이   Space 손님 태우기   X 거절   R 재시작(게임오버 후)", 15, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerLeft);   // 내비(오른쪽 아래)와 겹치지 않게 왼쪽 정렬

        if (!gameOver && flow != GameFlow.Paused) DrawTaxiGui(W, H, u);       // 게임오버/일시정지 글자와 겹치지 않게

        if (Time.unscaledTime < popupUntil && !gameOver)
        {
            float age = Time.unscaledTime - (popupUntil - 0.8f);
            float pop = 1f + 0.45f * Mathf.Clamp01(1f - age / 0.15f);                         // 처음엔 크게 튀어나왔다가 제 크기로
            float fadeP = Mathf.Clamp01((popupUntil - Time.unscaledTime) / 0.25f);           // 끝에서 부드럽게 사라진다
            Color pc = popupColor;
            pc.a *= fadeP;
            Label(new Rect(0, H * 0.47f, W, 64 * u), popupText, Mathf.RoundToInt(38f * pop), pc, TextAnchor.MiddleCenter);
        }
        if (!gameOver && taxi.State == TaxiState.Cruise && Time.unscaledTime < taxi.ThanksUntil)
            Label(new Rect(0, H * 0.34f + 32 * u, W, 34 * u), taxi.ThanksText, 22, new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleCenter);

        // 회전 중 장면 교체 순간에 잠깐 어두워진다
        if (fade > 0f) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, Mathf.Clamp01(fade)));

        if (gameOver) DrawGameOver(W, H, u);
        else if (flow == GameFlow.Paused) DrawPause(W, H, u);
        DrawDebug(W, H, u);
    }
}
