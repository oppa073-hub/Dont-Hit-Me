using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 손님 유별 사건의 화면 표시 (IMGUI): 폭탄 도화선, 행복도, 외계인 안내, 레이서 평균 속도, 폭주 손님 경고.
public partial class DriveGame
{
    int hudRacerKmh = -1;
    string hudRacerText = "";

    // 가로 막대 하나 (라벨 + 채워진 비율)
    void QuirkBar(Rect r, string label, float k, Color fill)
    {
        Panel(r, 0.55f);
        Label(new Rect(r.x + 8 * Screen.height / 720f, r.y, r.width * 0.36f, r.height), label, 15, Color.white, TextAnchor.MiddleLeft);
        float bx = r.x + r.width * 0.38f, bw = r.width * 0.58f, bh = r.height * 0.42f, by = r.y + (r.height - bh) * 0.5f;
        Fill(new Rect(bx, by, bw, bh), new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(bx, by, bw * Mathf.Clamp01(k), bh), fill);
    }

    void DrawQuirkHud(float W, float H, float u)
    {
        if (taxi.State != TaxiState.Carrying || taxi.Pax == null || taxi.Pax.quirk == Quirk.None) return;
        float y = H * 0.20f + 98f * u;
        var r = new Rect(W * 0.5f - 160 * u, y, 320 * u, 28 * u);
        switch (taxi.Pax.quirk)
        {
            case Quirk.Bomb:
            {
                bool low = taxi.Fuse < 0.35f;
                Color c = low && Mathf.Repeat(Time.unscaledTime, 0.3f) < 0.15f ? Color.white : new Color(1f, 0.3f, 0.2f);
                QuirkBar(r, taxi.Fuse < 0.9f && speed < bombMinSpeed ? "속도 올려!" : "폭탄", taxi.Fuse, c);
                break;
            }
            case Quirk.Wedding:
                QuirkBar(r, "행복도", taxi.Happy, new Color(1f, 0.5f, 0.75f));
                break;
            case Quirk.Alien:
                r = new Rect(W * 0.5f - 230 * u, y, 460 * u, 28 * u);
                Panel(r, 0.55f);
                Label(r, "삐빅… 지구 지도 해독 불가", 17, UiGreen, TextAnchor.MiddleCenter);
                break;
            case Quirk.Racer:
            {
                int kmh = Mathf.RoundToInt(taxi.AvgSpeed * 3.6f / 5f) * 5;
                if (kmh != hudRacerKmh)
                {
                    hudRacerKmh = kmh;
                    hudRacerText = "평균 " + kmh + " km/h    빠를수록 요금이 오른다";
                }
                r = new Rect(W * 0.5f - 230 * u, y, 460 * u, 28 * u);
                Panel(r, 0.55f);
                Label(r, hudRacerText, 17, UiGold, TextAnchor.MiddleCenter);
                break;
            }
            case Quirk.Fighter:
            {
                r = new Rect(W * 0.5f - 230 * u, y, 460 * u, 28 * u);
                Panel(r, 0.55f);
                if (taxi.SteerFlip) Label(r, "조향 반전!!", 20, UiRed, TextAnchor.MiddleCenter);
                else if (taxi.SteerFlipWarn > 0f) Label(r, Mathf.Repeat(Time.unscaledTime, 0.2f) < 0.1f ? "손님이 핸들을 잡으려 한다!!" : "", 20, UiRed, TextAnchor.MiddleCenter);
                else Label(r, "손님이 가끔 핸들을 노린다", 16, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
                break;
            }
        }
    }
}
