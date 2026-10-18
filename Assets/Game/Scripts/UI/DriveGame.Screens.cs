using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 화면: 타이틀, 일시정지, 게임오버 결과 (IMGUI). 색은 DriveGame.Gui.cs 의 Ui* 팔레트, 상자는 Panel() 로 통일한다.
public partial class DriveGame
{
    static readonly string ControlsText =
        "A / D  조향      S  브레이크      Q / E  좌 / 우 깜빡이" + ((char)10) +
        "손님 `!` 옆에 정차 →  Space 태우기  /  X 거절" + ((char)10) +
        "목적지 교차로에서 브레이크로 정차하면 요금 정산" + ((char)10) +
        "횡단하는 사람이나 차에 부딪히면 목숨이 줄고, 모두 잃으면 끝";

    void DrawTitle(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.58f));

        // 로고
        float ly = H * 0.12f;
        Label(new Rect(0, ly, W, 100 * u), "DON'T HIT ME", 82, UiGold, TextAnchor.MiddleCenter);
        Fill(new Rect(W * 0.5f - 270 * u, ly + 104 * u, 540 * u, 3 * u), new Color(UiGold.r, UiGold.g, UiGold.b, 0.85f));
        Label(new Rect(0, ly + 114 * u, W, 34 * u), "택시 기사가 되어 무단횡단자를 피하며 손님을 모셔다 드리세요", 22, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

        // 조작 안내
        Panel(new Rect(W * 0.5f - 380 * u, 292 * u, 760 * u, 142 * u), 0.55f);
        style.wordWrap = true;
        Label(new Rect(W * 0.5f - 360 * u, 302 * u, 720 * u, 124 * u), ControlsText, 20, new Color(1f, 1f, 1f, 0.92f), TextAnchor.UpperCenter);
        style.wordWrap = false;

        // 모드, 코인
        Label(new Rect(0, 452 * u, W, 30 * u), modeText, 20, UiGold, TextAnchor.MiddleCenter);
        Label(new Rect(0, 482 * u, W, 30 * u), shopTitleText, 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);

        // 시작 안내는 깜빡이지 않고 부드럽게 숨 쉰다
        float breathe = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
        Label(new Rect(0, 520 * u, W, 44 * u), "Space 또는 Enter 로 로비에 입장", 32, new Color(UiGreen.r, UiGreen.g, UiGreen.b, breathe), TextAnchor.MiddleCenter);

        // 오늘의 목표
        Panel(new Rect(W * 0.5f - 300 * u, 582 * u, 600 * u, 118 * u), 0.5f);
        Label(new Rect(0, 586 * u, W, 26 * u), "오늘의 목표", 19, UiGold, TextAnchor.MiddleCenter);
        DrawGoals(0, 614 * u, W, u, 16, TextAnchor.UpperCenter);
    }

    void DrawPause(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Panel(new Rect(W * 0.5f - 320 * u, H * 0.5f - 184 * u, 640 * u, 288 * u), 0.7f);
        Label(new Rect(0, H * 0.5f - 170 * u, W, 70 * u), "일시정지", 56, Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.5f - 96 * u, W, 34 * u), "Esc 또는 P  계속      R  처음부터 다시      O  설정", 22, UiCyan, TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.5f - 44 * u, W, 26 * u), "오늘의 목표", 19, UiGold, TextAnchor.MiddleCenter);
        DrawGoals(0, H * 0.5f - 16 * u, W, u, 17, TextAnchor.UpperCenter);
    }

    void DrawGameOver(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.6f));
        if (timeUp) Label(new Rect(0, H * 0.14f, W, 80 * u), "영업 종료", 62, UiGold, TextAnchor.MiddleCenter);
        else Label(new Rect(0, H * 0.14f, W, 80 * u), shiftMode ? "사고로 영업 중단" : "게임 오버", 62, UiRed, TextAnchor.MiddleCenter);

        Panel(new Rect(W * 0.5f - 390 * u, H * 0.14f + 96 * u, 780 * u, 290 * u), 0.62f);
        Label(new Rect(W * 0.5f - 370 * u, H * 0.14f + 108 * u, 740 * u, 270 * u), resultText, 24, Color.white, TextAnchor.UpperCenter);

        float breathe = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
        Label(new Rect(0, H * 0.84f, W, 40 * u), "R 바로 다시 영업      U 로비로 (사장님께)", 28, new Color(UiGreen.r, UiGreen.g, UiGreen.b, breathe), TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.84f + 42 * u, W, 30 * u), modeText, 17, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
    }
}
