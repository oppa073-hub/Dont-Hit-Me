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
        "횡단하는 사람이나 차에 부딪히면 목숨이 줄고, 모두 잃으면 게임 오버";

    void DrawTitle(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.62f));
        Label(new Rect(0, H * 0.20f, W, 90 * u), "DON'T HIT ME", 72, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.20f + 86 * u, W, 36 * u), "택시 기사가 되어 무단횡단자를 피하며 손님을 모셔다 드리세요", 22, Color.white, TextAnchor.MiddleCenter);

        style.wordWrap = true;
        Label(new Rect(W * 0.2f, H * 0.46f, W * 0.6f, 130 * u), ControlsText, 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter);
        style.wordWrap = false;

        Label(new Rect(0, 466 * u, W, 32 * u), modeText, 22, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 498 * u, W, 32 * u), shopTitleText, 22, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
        if (Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f)
            Label(new Rect(0, 540 * u, W, 40 * u), "Space 또는 Enter 로 로비에 입장", 30, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);

        Label(new Rect(0, 590 * u, W, 28 * u), "오늘의 목표", 20, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        DrawGoals(0, 618 * u, W, u, 17, TextAnchor.UpperCenter);
    }

    void DrawPause(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Label(new Rect(0, H * 0.36f, W, 70 * u), "일시정지", 56, Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.36f + 72 * u, W, 36 * u), "Esc 또는 P  계속      R  처음부터 다시      O  설정", 24, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        DrawGoals(0, H * 0.36f + 130 * u, W, u, 18, TextAnchor.UpperCenter);
    }

    void DrawGameOver(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        if (timeUp) Label(new Rect(0, H * 0.22f, W, 80 * u), "영업 종료", 60, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        else Label(new Rect(0, H * 0.22f, W, 80 * u), shiftMode ? "사고로 영업 중단" : "GAME OVER", 60, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.22f + 84 * u, W, 250 * u), resultText, 26, Color.white, TextAnchor.UpperCenter);
        if (Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f)
            Label(new Rect(0, H * 0.84f, W, 40 * u), "R 바로 다시 영업      U 로비로 (정비소)", 28, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.84f + 42 * u, W, 30 * u), modeText, 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
    }
}
