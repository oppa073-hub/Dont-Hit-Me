using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 화면: 타이틀, 일시정지, 게임오버 결과 (IMGUI)
public partial class DriveGame
{
    static readonly string ControlsText =
        "A / D  조향      S  브레이크      Q / E  좌 / 우 깜빡이" + ((char)10) +
        "손님 `!` 옆에 정차 -> Space 태우기 / X 거절" + ((char)10) +
        "목적지 교차로에서 브레이크로 정차하면 요금 정산" + ((char)10) +
        "횡단하는 사람을 치면 목숨이 줄어듭니다 (3개)";

    void DrawTitle(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.62f));
        Label(new Rect(0, H * 0.20f, W, 90 * u), "DON'T HIT ME", 72, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.20f + 86 * u, W, 36 * u), "택시 기사가 되어 무단횡단자를 피하며 손님을 모셔다 드리세요", 22, Color.white, TextAnchor.MiddleCenter);

        style.wordWrap = true;
        Label(new Rect(W * 0.2f, H * 0.46f, W * 0.6f, 130 * u), ControlsText, 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter);
        style.wordWrap = false;

        if (bestText.Length > 0)
            Label(new Rect(0, H * 0.70f, W, 32 * u), bestText, 22, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
        if (Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f)
            Label(new Rect(0, H * 0.78f, W, 40 * u), "Space 또는 Enter 로 시작", 30, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);
    }

    void DrawPause(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Label(new Rect(0, H * 0.36f, W, 70 * u), "일시정지", 56, Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.36f + 72 * u, W, 36 * u), "Esc 또는 P  계속      R  처음부터 다시", 24, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
    }

    void DrawGameOver(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Label(new Rect(0, H * 0.22f, W, 80 * u), "GAME OVER", 60, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.22f + 84 * u, W, 190 * u), resultText, 26, Color.white, TextAnchor.UpperCenter);
        if (Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f)
            Label(new Rect(0, H * 0.78f, W, 40 * u), "R 로 다시 시작", 28, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);
    }
}
