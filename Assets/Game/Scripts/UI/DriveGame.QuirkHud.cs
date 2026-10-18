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
                // 평소에는 아무 표시도 하지 않는다. 핸들을 잡히면 가운데에서 양쪽으로 차오르는 게이지가 나온다 (A 는 왼쪽, D 는 오른쪽)
                if (!taxi.Grabbed) break;
                float gw = 460 * u, gh = 30 * u, gx = W * 0.5f - gw * 0.5f, gy = H * 0.60f;
                float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 24f);
                Label(new Rect(gx, gy - 46 * u, gw, 36 * u), "핸들을 잡혔다!  A  D  연타!!", Mathf.RoundToInt(26 * pulse), UiGold, TextAnchor.MiddleCenter);
                Panel(new Rect(gx - 8 * u, gy - 8 * u, gw + 16 * u, gh + 16 * u), 0.7f);
                Fill(new Rect(gx, gy, gw, gh), new Color(0f, 0f, 0f, 0.65f));
                float half = gw * 0.5f;
                bool lf = taxi.MashL >= 1f, rf = taxi.MashR >= 1f;
                Fill(new Rect(W * 0.5f - half * taxi.MashL, gy, half * taxi.MashL, gh), lf ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.6f, 0.2f));
                Fill(new Rect(W * 0.5f, gy, half * taxi.MashR, gh), rf ? new Color(0.4f, 1f, 0.5f) : new Color(0.3f, 0.8f, 1f));
                Fill(new Rect(W * 0.5f - 1.5f * u, gy - 4 * u, 3f * u, gh + 8 * u), Color.white);        // 가운데 선
                Label(new Rect(gx - 44 * u, gy - 4 * u, 36 * u, gh + 8 * u), "A", 24, Color.white, TextAnchor.MiddleCenter);
                Label(new Rect(gx + gw + 8 * u, gy - 4 * u, 36 * u, gh + 8 * u), "D", 24, Color.white, TextAnchor.MiddleCenter);
                break;
            }
        }
    }
}
