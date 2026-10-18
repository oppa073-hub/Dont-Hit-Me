using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 화면에 얹는 것들 (IMGUI): 밤/노을/비의 어두운 막, 헤드라이트 빛, 빗줄기와 와이퍼, 설정 화면, 튜토리얼 안내
public partial class DriveGame
{
    static float Frac(float x) { return x - Mathf.Floor(x); }

    void DrawWeatherOverlay(float W, float H, float u)
    {
        // 어두운 막은 앞유리(화면 위 80%)에만 깔아서, 아래쪽 계기판과 내비는 밤에도 또렷하게 둔다
        float playH = H * 0.80f;
        var full = new Rect(0, 0, W, playH);
        var no = weather.NightOverlay;
        if (no.a > 0.005f) Fill(full, no);
        var wo = weather.WarmOverlay;
        if (wo.a > 0.005f) Fill(full, wo);
        var ro = weather.RainOverlay;
        if (ro.a > 0.005f) Fill(full, ro);

        // 정전: 도시가 암흑에 빠진다 (아래 대시보드는 그대로). 팬 습격: 파파라치 플래시가 터진다.
        float bk = enc.Blackout01;
        bool live = flow == GameFlow.Playing && !gameOver;          // 일시정지/결과 화면에는 사건 연출을 남기지 않는다
        if (!live) bk = 0f;
        if (bk > 0.01f) Fill(full, new Color(0f, 0f, 0.03f, 0.2f * bk * (1f - 0.5f * weather.Darkness)));       // 정전의 어둠은 대부분 실제 조명이 하고, 이 막은 분위기만 더한다
        if (live && enc.Active == EncounterType.Fans && Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.05f) Fill(full, new Color(1f, 1f, 1f, 0.22f));

        if (weather.Rain <= 0.02f) return;

        // 빗줄기 (그리기 이벤트에서만): 앞유리 안에서만 (기둥/천장/계기판 위에는 안 내림)
        if (Event.current.type == EventType.Repaint)
        {
            ComputeGlass(W, H, u);
            UpdateRainGlass(W, H, u);
            int n = Mathf.RoundToInt(160f * weather.Rain);
            Color streak = new Color(0.8f, 0.86f, 0.95f, 0.28f);
            float t = Mathf.Repeat(Time.unscaledTime, 600f);
            float gy0 = glassOk ? gTop : 0f, gh = glassOk ? gBot - gTop : H;
            for (int i = 0; i < n; i++)
            {
                float h1 = Frac(Mathf.Sin(i * 12.9898f) * 43758.5453f);
                float h2 = Frac(Mathf.Sin((i + 0.5f) * 78.233f) * 12345.678f);
                float y = gy0 + Mathf.Repeat(h2 * gh + t * (700f + h1 * 400f) * u, gh);
                float x = h1 * W;
                if (!InGlass(x, y) || !InGlass(x, y + 22f * u)) continue;
                Fill(new Rect(x, y, 2f * u, 22f * u), streak);
            }
            DrawRainDrops(u);
        }

        // 와이퍼: 앞유리 아래쪽 대시보드 뒤에서 올라와 쓸고 내려간다 (화면 위 80% 안에서만 보이게 잘라 그린다)
        if (cabinVisible && (weather.Rain > 0.15f || wiperSweepT >= 0f))
        {
            float clipH = H * 0.80f;
            GUI.BeginClip(new Rect(0, 0, W, clipH));
            float sweep = WiperSweep01();                      // 평소엔 아래에 접혀 있다가 일정 시간마다 한 번 쓸고 간다 (UpdateRainGlass)
            float ang = Mathf.Lerp(-78f, -8f, sweep);
            float len = H * 0.55f;
            for (int k = 0; k < 2; k++)
            {
                var pivot = new Vector2(W * (k == 0 ? 0.28f : 0.62f), clipH + 10f * u);
                GUIUtility.RotateAroundPivot(ang, pivot);
                Fill(new Rect(pivot.x - 3f * u, pivot.y - len, 6f * u, len), new Color(0.05f, 0.05f, 0.06f, 0.9f));
                GUI.matrix = Matrix4x4.identity;
            }
            GUI.EndClip();
        }
    }

    void DrawSettings(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.72f));
        Label(new Rect(0, 40 * u, W, 70 * u), settingsKeys ? Loc.T("조작 키 설정", "Key Bindings") : Loc.T("설정", "Settings"), 50, UiGold, TextAnchor.MiddleCenter);
        for (int i = 0; i < settingsLines.Length; i++)
        {
            bool sel = i == settingsSel;
            Color c = sel ? new Color(1f, 0.92f, 0.4f) : new Color(1f, 1f, 1f, 0.8f);
            float ry = (128 + i * 46) * u;
            Panel(new Rect(W * 0.20f, ry, W * 0.60f, 40 * u), sel ? 0.8f : 0.45f);
            if (sel) Fill(new Rect(W * 0.20f - 3 * u, ry, 7 * u, 40 * u), UiGold);
            Label(new Rect(W * 0.22f, ry, W * 0.56f, 40 * u), settingsLines[i], 24, c, TextAnchor.MiddleLeft);
        }
        string hint = settingsKeys && settingsKeyWait >= 0 ? Loc.T("바꿀 키를 누르세요      Esc 취소", "Press the new key      Esc to cancel")
                    : (settingsKeys ? Loc.T("W / S 선택      Enter 키 바꾸기      Esc 뒤로", "W / S select      Enter change key      Esc back")
                                    : Loc.T("W / S 선택      A / D 또는 Enter 조절      Esc 또는 O 돌아가기", "W / S select      A / D or Enter change      Esc or O close"));
        Label(new Rect(0, 640 * u, W, 36 * u), hint, 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
    }

    void DrawTutorial(float W, float H, float u)
    {
        if (!tutorial.Active) return;
        // 왼쪽 목표 목록 아래 빈 자리에 둔다 (가운데 룸미러, 팝업, 교차로 안내와 겹치지 않게)
        Panel(new Rect(14f * u, 202f * u, 340f * u, 112f * u), 0.6f);
        Label(new Rect(24f * u, 206f * u, 340f * u, 20f * u), Loc.T("안내", "Tip"), 14, UiGold, TextAnchor.UpperLeft);
        style.wordWrap = true;
        Label(new Rect(24f * u, 226f * u, 320f * u, 84f * u), tutorial.Text, 16, UiGreen, TextAnchor.UpperLeft);
        style.wordWrap = false;
    }
}
