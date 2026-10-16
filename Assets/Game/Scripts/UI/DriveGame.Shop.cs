using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 상점(정비소) 화면과 오늘의 목표 표시 (IMGUI). 상점은 두 쪽: 0 = 업그레이드, 1 = 택시 개조 (Tab 으로 넘긴다).
public partial class DriveGame
{
    void DrawShop(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.72f));
        Label(new Rect(0, 56 * u, W, 70 * u), shopPage == 0 ? "박 정비소" : "택시 개조", 52, UiGold, TextAnchor.MiddleCenter);
        Label(new Rect(0, 124 * u, W, 36 * u), shopCoinsText, 28, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 160 * u, W, 26 * u), shopPage == 0 ? "Tab  택시 개조 보기" : "Tab  업그레이드 보기", 17, UiCyan, TextAnchor.MiddleCenter);

        // 정비사 초상과 말풍선
        Panel(new Rect(W * 0.02f, 196 * u, W * 0.23f, 420 * u), 0.5f);
        var fc = new Vector2(W * 0.135f, 300 * u);
        DrawFace(fc, 190 * u, NpcSkin, NpcHair, false);
        Dot(new Vector2(fc.x, 410 * u), 110 * u, NpcShirt);
        Label(new Rect(W * 0.03f, 450 * u, W * 0.2f, 30 * u), StoryCatalog.NpcName, 22, UiGold, TextAnchor.MiddleCenter);
        style.wordWrap = true;
        Label(new Rect(W * 0.03f, 482 * u, W * 0.2f, 120 * u), shopNpcLine, 18, Color.white, TextAnchor.UpperCenter);
        style.wordWrap = false;

        if (shopPage == 0)
        {
            for (int i = 0; i < shopLines.Length; i++)
            {
                bool maxed = upgrades.IsMax(i);
                bool canBuy = !maxed && upgrades.Coins >= upgrades.Cost(i);
                Color c = maxed ? new Color(0.6f, 0.9f, 0.6f) : (canBuy ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                Panel(new Rect(W * 0.27f, (216 + i * 56) * u, W * 0.68f, 46 * u), canBuy ? 0.65f : 0.45f);
                Label(new Rect(W * 0.285f, (219 + i * 56) * u, W * 0.65f, 40 * u), shopLines[i], 24, c, TextAnchor.MiddleLeft);
            }
        }
        else
        {
            for (int i = 0; i < shopProjNames.Length; i++)
            {
                bool done = projects.Done(i);
                bool open = projects.Unlocked(i);
                bool canBuy = !done && open && upgrades.Coins >= ProjectManager.Defs[i].cost;
                Color c = done ? UiGreen : (canBuy ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                Panel(new Rect(W * 0.27f, (206 + i * 78) * u, W * 0.68f, 68 * u), canBuy ? 0.7f : 0.45f);
                if (canBuy) Fill(new Rect(W * 0.27f - 3 * u, (206 + i * 78) * u, 6 * u, 68 * u), UiGold);
                Label(new Rect(W * 0.285f, (210 + i * 78) * u, W * 0.65f, 34 * u), shopProjNames[i], 25, c, TextAnchor.MiddleLeft);
                Label(new Rect(W * 0.285f, (240 + i * 78) * u, W * 0.65f, 28 * u), shopProjDescs[i], 16, done ? UiGreen : new Color(1f, 1f, 1f, canBuy ? 0.8f : 0.45f), TextAnchor.MiddleLeft);
            }
        }

        if (Time.unscaledTime < shopMsgUntil)
            Label(new Rect(0, 530 * u, W, 40 * u), shopMsg, 26, UiGreen, TextAnchor.MiddleCenter);
        Label(new Rect(0, 600 * u, W, 36 * u), "숫자키로 구매     Tab 쪽 넘기기     Esc 로 로비로 돌아가기", 21, UiCyan, TextAnchor.MiddleCenter);
    }

    // 오늘의 목표 목록 (HUD, 타이틀, 일시정지에서 쓴다). 완료한 목표는 초록색.
    void DrawGoals(float x, float y, float w, float u, int size, TextAnchor anchor)
    {
        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            Color c = g.doneToday ? UiGreen : new Color(1f, 1f, 1f, 0.88f);
            Label(new Rect(x, y + i * (size + 8) * u, w, (size + 6) * u), g.Line, size, c, anchor);
        }
    }
}
