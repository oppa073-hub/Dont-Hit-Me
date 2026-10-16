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
        Label(new Rect(0, 60 * u, W, 70 * u), "박 정비소", 52, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 130 * u, W, 36 * u), shopCoinsText, 28, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);

        // 정비사 초상과 말풍선
        var fc = new Vector2(W * 0.13f, 300 * u);
        DrawFace(fc, 190 * u, NpcSkin, NpcHair, false);
        Dot(new Vector2(fc.x, 410 * u), 110 * u, NpcShirt);
        Label(new Rect(W * 0.03f, 450 * u, W * 0.2f, 30 * u), StoryCatalog.NpcName, 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        style.wordWrap = true;
        Label(new Rect(W * 0.03f, 482 * u, W * 0.2f, 120 * u), shopNpcLine, 18, Color.white, TextAnchor.UpperCenter);
        style.wordWrap = false;
        for (int i = 0; i < shopLines.Length; i++)
        {
            bool maxed = upgrades.IsMax(i);
            bool canBuy = !maxed && upgrades.Coins >= upgrades.Cost(i);
            Color c = maxed ? new Color(0.6f, 0.9f, 0.6f) : (canBuy ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            Label(new Rect(W * 0.28f, (220 + i * 56) * u, W * 0.66f, 40 * u), shopLines[i], 26, c, TextAnchor.MiddleLeft);
        }
        if (Time.unscaledTime < shopMsgUntil)
            Label(new Rect(0, 510 * u, W, 40 * u), shopMsg, 26, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);
        Label(new Rect(0, 600 * u, W, 36 * u), "숫자키로 구매     Esc 로 로비로 돌아가기", 22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
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
