using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 상점 화면과 오늘의 목표 표시 (IMGUI)
public partial class DriveGame
{
    void DrawShop(float W, float H, float u)
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.72f));
        Label(new Rect(0, 70 * u, W, 70 * u), "업그레이드 상점", 52, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 140 * u, W, 36 * u), shopCoinsText, 28, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
        for (int i = 0; i < shopLines.Length; i++)
        {
            bool maxed = upgrades.IsMax(i);
            bool canBuy = !maxed && upgrades.Coins >= upgrades.Cost(i);
            Color c = maxed ? new Color(0.6f, 0.9f, 0.6f) : (canBuy ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            Label(new Rect(W * 0.14f, (220 + i * 56) * u, W * 0.72f, 40 * u), shopLines[i], 26, c, TextAnchor.MiddleLeft);
        }
        if (Time.unscaledTime < shopMsgUntil)
            Label(new Rect(0, 510 * u, W, 40 * u), shopMsg, 26, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 600 * u, W, 36 * u), "숫자키로 구매     Esc 또는 U 로 돌아가기", 22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
    }

    // 오늘의 목표 목록 (HUD, 타이틀, 일시정지에서 쓴다). 완료한 목표는 초록색.
    void DrawGoals(float x, float y, float w, float u, int size, TextAnchor anchor)
    {
        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            Color c = g.doneToday ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.85f);
            Label(new Rect(x, y + i * (size + 8) * u, w, (size + 6) * u), g.Line, size, c, anchor);
        }
    }
}
