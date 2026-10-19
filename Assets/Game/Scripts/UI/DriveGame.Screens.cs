using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 화면: 타이틀, 일시정지, 게임오버 결과 (IMGUI). 색은 DriveGame.Gui.cs 의 Ui* 팔레트, 상자는 Panel() 로 통일한다.
public partial class DriveGame
{
    // 키 하나를 키보드 키캡 모양으로 (둥근 느낌의 밝은 판 + 아래 그림자 + 글자). 가로 폭은 글자 수에 맞춘다
    float KeyCap(float x, float y, string key, float u)
    {
        float w = Mathf.Max(30f, 16f + key.Length * 13f) * u, h = 28f * u;
        Fill(new Rect(x, y + 3 * u, w, h), new Color(0f, 0f, 0f, 0.5f));                                   // 아래 그림자
        Fill(new Rect(x, y, w, h), new Color(0.9f, 0.92f, 0.96f, 0.96f));
        Fill(new Rect(x + 2 * u, y + 2 * u, w - 4 * u, h * 0.45f), new Color(1f, 1f, 1f, 0.5f));            // 윗면 광택
        Label(new Rect(x, y, w, h), key, 16, new Color(0.1f, 0.12f, 0.18f), TextAnchor.MiddleCenter);
        return w;
    }

    // 조작 한 줄: 키캡들 + 설명. 다음 줄을 그릴 때 쓸 키캡 폭 합계는 필요 없어서 돌려주지 않는다
    void ControlRow(float x, float y, string[] keys, string what, float u)
    {
        float cx = x;
        foreach (var k in keys) cx += KeyCap(cx, y, k, u) + 6 * u;
        Label(new Rect(cx + 6 * u, y, 290 * u, 28 * u), what, 18, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleLeft);
    }

    void DrawTitle(float W, float H, float u)
    {
        // 화면 전체를 어둡게 + 위아래 가장자리는 더 어둡게 (가운데 글자에 눈이 가게)
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
        for (int i = 0; i < 8; i++)
        {
            float a = 0.28f * (1f - i / 8f);
            Fill(new Rect(0, H * 0.05f * i * 0.5f, W, H * 0.025f + 1f), new Color(0f, 0f, 0f, a));
            Fill(new Rect(0, H - H * 0.025f * (i + 1), W, H * 0.025f + 1f), new Color(0f, 0f, 0f, a));
        }

        // 로고 + 택시 체커 띠 + 한 줄 소개
        float ly = H * 0.09f;
        Label(new Rect(0, ly, W, 100 * u), "DON'T HIT ME", 84, UiGold, TextAnchor.MiddleCenter);
        float cw = 14 * u, cx0 = W * 0.5f - 22 * cw, cy = ly + 104 * u;
        for (int i = 0; i < 44; i++)                                                                       // 택시 지붕의 체커 무늬
            for (int j = 0; j < 2; j++)
                Fill(new Rect(cx0 + i * cw, cy + j * cw * 0.6f, cw, cw * 0.6f), ((i + j) & 1) == 0 ? new Color(0.05f, 0.05f, 0.06f, 0.9f) : new Color(UiGold.r, UiGold.g, UiGold.b, 0.9f));
        Label(new Rect(0, cy + 26 * u, W, 34 * u), Loc.T("택시 기사가 되어 무단횡단자를 피하며 손님을 모셔다 드리세요", "Be a taxi driver: dodge jaywalkers and get your passengers to their stops"), 21, new Color(1f, 1f, 1f, 0.88f), TextAnchor.MiddleCenter);

        // 조작 안내: 키캡으로 세 줄 x 두 칸 (상자는 가운데 하나)
        var cp = new Rect(W * 0.5f - 400 * u, 292 * u, 800 * u, 144 * u);
        Panel(cp, 0.5f);
        float c1 = cp.x + 36 * u, c2 = cp.x + 420 * u, r1 = cp.y + 14 * u, step = 40 * u;
        var km = Keybinds.Map;
        ControlRow(c1, r1, new[] { Keybinds.KeyName(km[Keybinds.SteerLeft]), Keybinds.KeyName(km[Keybinds.SteerRight]) }, Loc.T("조향", "Steer"), u);
        ControlRow(c1, r1 + step, new[] { Keybinds.KeyName(km[Keybinds.Brake]) }, Loc.T("브레이크 (손님 앞에서 멈추기)", "Brake (stop at the curb)"), u);
        ControlRow(c1, r1 + step * 2, new[] { Keybinds.KeyName(km[Keybinds.BlinkLeft]), Keybinds.KeyName(km[Keybinds.BlinkRight]) }, Loc.T("좌 / 우 깜빡이 (교차로 회전)", "Left / Right blinker"), u);
        ControlRow(c2, r1, new[] { Keybinds.KeyName(km[Keybinds.Accept]) }, Loc.T("손님 태우기", "Pick up passenger"), u);
        ControlRow(c2, r1 + step, new[] { Keybinds.KeyName(km[Keybinds.Decline]) }, Loc.T("손님 거절", "Decline passenger"), u);
        ControlRow(c2, r1 + step * 2, new[] { "Esc" }, Loc.T("일시정지", "Pause"), u);

        // 시작 안내: 상자 없이 큰 글자 하나만, 부드럽게 숨 쉰다
        float breathe = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
        Label(new Rect(0, 486 * u, W, 56 * u), Loc.T("Space  /  Enter  로비에 입장", "Space  /  Enter  to start"), 38, new Color(UiGreen.r, UiGreen.g, UiGreen.b, breathe), TextAnchor.MiddleCenter);

        // 맨 아래: 모드와 코인 한 줄씩, 조용하게
        Label(new Rect(0, H - 76 * u, W, 26 * u), modeText, 17, new Color(UiGold.r, UiGold.g, UiGold.b, 0.85f), TextAnchor.MiddleCenter);
        Label(new Rect(0, H - 50 * u, W, 26 * u), shopTitleText, 16, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
    }

    void DrawPause(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Panel(new Rect(W * 0.5f - 320 * u, H * 0.5f - 184 * u, 640 * u, 288 * u), 0.7f);
        Label(new Rect(0, H * 0.5f - 170 * u, W, 70 * u), Loc.T("일시정지", "Paused"), 56, Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.5f - 96 * u, W, 34 * u), Loc.T("Esc 또는 P  계속      R  처음부터 다시      O  설정", "Esc or P  Resume      R  Restart      O  Settings"), 22, UiCyan, TextAnchor.MiddleCenter);
        Label(new Rect(0, H * 0.5f - 44 * u, W, 26 * u), Loc.T("오늘의 목표", "Today's Goals"), 19, UiGold, TextAnchor.MiddleCenter);
        DrawGoals(0, H * 0.5f - 16 * u, W, u, 17, TextAnchor.UpperCenter);
    }

    // 결과 화면의 숫자 칸 하나: 위에 작은 이름, 아래에 큰 값
    void StatCard(Rect r, string label, string value, Color valueColor, float u)
    {
        Panel(r, 0.58f);
        Label(new Rect(r.x, r.y + 5 * u, r.width, 20 * u), label, 14, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
        Label(new Rect(r.x, r.y + 26 * u, r.width, 36 * u), value, 26, valueColor, TextAnchor.MiddleCenter);
    }

    static string ClockText(int sec) { return (sec / 60) + ":" + (sec % 60).ToString("00"); }

    // 영업 종료 / 게임 오버: 제목 -> 점수(+최고 기록) -> 평점 별 -> 숫자 칸 -> 코인과 목표 -> 다음 행동. 한 상자에 몰아 쓰지 않고 칸마다 나눈다.
    void DrawGameOver(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.84f));          // 룸미러/내비/계기판 글자가 비쳐 보이지 않게 진하게
        float cx = W * 0.5f;

        // 제목과 부제
        if (timeUp) Label(new Rect(0, 26 * u, W, 70 * u), Loc.T("영업 종료", "Shift Over"), 58, UiGold, TextAnchor.MiddleCenter);
        else Label(new Rect(0, 26 * u, W, 70 * u), resShift ? Loc.T("사고로 영업 중단", "Shift Ended by Crash") : Loc.T("게임 오버", "Game Over"), 58, UiRed, TextAnchor.MiddleCenter);
        Label(new Rect(0, 94 * u, W, 26 * u), "Day " + resDay + "   ·   " + (resShift ? Loc.T("하루 영업 모드", "Daily Shift") : Loc.T("무한 모드", "Endless Mode")), 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);

        // 점수와 새 최고 기록
        Label(new Rect(0, 128 * u, W, 20 * u), Loc.T("점수", "SCORE"), 15, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 146 * u, W, 66 * u), resScore.ToString("N0"), 58, Color.white, TextAnchor.MiddleCenter);
        if (newBest)
        {
            var nb = new Rect(cx + 150 * u, 160 * u, 150 * u, 34 * u);
            Fill(nb, UiGold);
            Label(nb, "NEW BEST!", 18, new Color(0.15f, 0.1f, 0f), TextAnchor.MiddleCenter);
        }

        // 평점 별 (하루 영업 모드만)
        float rowY = 232 * u;
        if (resShift)
        {
            for (int i = 0; i < 3; i++)
                Label(new Rect(cx - 90 * u + i * 60 * u, 214 * u, 60 * u, 56 * u), "★", 50, i < resStars ? UiGold : new Color(1f, 1f, 1f, 0.18f), TextAnchor.MiddleCenter);
            rowY = 282 * u;
        }

        // 숫자 칸 (한 줄에 네 칸)
        float cw = 176 * u, ch = 66 * u, gap = 12 * u, x0 = cx - (4 * cw + 3 * gap) * 0.5f;
        string[] labels; string[] values; Color[] colors;
        Color white = Color.white;
        if (resShift)
        {
            labels = new[] { Loc.T("손님", "Passengers"), Loc.T("요금 합계", "Total fares"), Loc.T("수리비", "Repairs"), Loc.T("순수익", "Net profit"),
                             Loc.T("니어미스", "Near misses"), Loc.T("사고", "Crashes"), Loc.T("플레이 시간", "Time played"), Loc.T("레벨", "Level") };
            values = new[] { resTrips.ToString(), Loc.Money(resFare), "-" + Loc.Money(resRepairs), Loc.Money(resNet),
                             resNear.ToString(), resHits.ToString(), ClockText(resSeconds), resLevel.ToString() };
            colors = new[] { white, white, resRepairs > 0 ? UiRed : white, UiGold, white, resHits > 0 ? UiRed : white, white, white };
        }
        else
        {
            labels = new[] { Loc.T("손님", "Passengers"), Loc.T("요금", "Fares"), Loc.T("니어미스", "Near misses"), Loc.T("충돌", "Hits"),
                             Loc.T("플레이 시간", "Time played"), Loc.T("레벨", "Level") };
            values = new[] { resTrips.ToString(), Loc.Money(resFare), resNear.ToString(), resHits.ToString(), ClockText(resSeconds), resLevel.ToString() };
            colors = new[] { white, UiGold, white, resHits > 0 ? UiRed : white, white, white };
        }
        for (int i = 0; i < labels.Length; i++)
        {
            int row = i / 4, col = i % 4;
            int inRow = Mathf.Min(4, labels.Length - row * 4);
            float rx = cx - (inRow * cw + (inRow - 1) * gap) * 0.5f + col * (cw + gap);
            StatCard(new Rect(rx, rowY + row * (ch + gap), cw, ch), labels[i], values[i], colors[i], u);
        }
        float afterCards = rowY + 2 * (ch + gap);

        // 코인과 오늘의 목표
        var bar = new Rect(cx - (4 * cw + 3 * gap) * 0.5f, afterCards + 6 * u, 4 * cw + 3 * gap, 48 * u);
        Panel(bar, 0.5f);
        float third = bar.width / 3f;
        Label(new Rect(bar.x, bar.y, third, bar.height), Loc.T("획득 코인  +", "Coins earned  +") + resEarned, 20, UiGold, TextAnchor.MiddleCenter);
        Label(new Rect(bar.x + third, bar.y, third, bar.height), Loc.T("보유 코인  ", "Total coins  ") + resCoins, 20, Color.white, TextAnchor.MiddleCenter);
        Label(new Rect(bar.x + third * 2, bar.y, third, bar.height), Loc.T("오늘의 목표  ", "Goals  ") + resGoalsDone + " / " + resGoalsMax, 20, resGoalsDone >= resGoalsMax && resGoalsMax > 0 ? UiGreen : Color.white, TextAnchor.MiddleCenter);

        // 다음 행동
        float breathe = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
        Panel(new Rect(cx - 330 * u, H - 90 * u, 660 * u, 56 * u), 0.7f);
        Label(new Rect(0, H - 82 * u, W, 40 * u), Loc.T("R 바로 다시 영업      U 로비로 (사장님께)", "R  Start another shift      U  Back to the lobby (Boss)"), 26, new Color(UiGreen.r, UiGreen.g, UiGreen.b, breathe), TextAnchor.MiddleCenter);
    }
}
