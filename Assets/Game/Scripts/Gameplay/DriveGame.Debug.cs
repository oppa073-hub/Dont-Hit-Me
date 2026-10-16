using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 디버그 패널 (에디터와 개발 빌드에서만): F1 로 열고 숫자키로 사건/날씨/레벨을 바로 바꿔 본다.
//   1 괴수 침공   2 히어로와 빌런   3 사람 많은 날   4 평범한 날(진행 중인 사건 끝)   5 날 종류 무작위로 되돌리기
//   6 비 켜기/끄기   7 시각 +3시간   8 레벨 +1   G 무적
// 1~4 는 "다음 판부터의 날 종류"도 함께 고정한다 (인스펙터 Force Day Type 과 같은 값). 플레이 중이면 지금 바로 적용한다.
public partial class DriveGame
{
    bool debugOpen, debugGod;
    string debugText = "";

    static readonly string[] DebugDayNames = { "무작위", "평범한 날", "사람 많은 날", "괴수 침공", "히어로와 빌런" };

    static bool DebugAllowed { get { return Application.isEditor || Debug.isDebugBuild; } }

    void HandleDebugKeys(Keyboard kb)
    {
        if (!DebugAllowed) return;
        if (kb.f1Key.wasPressedThisFrame)
        {
            debugOpen = !debugOpen;
            RefreshDebug();
        }
        if (!debugOpen || flow == GameFlow.Shop || flow == GameFlow.Settings) return;

        bool changed = true;
        if (kb.digit1Key.wasPressedThisFrame) DebugSetDay(DayType.Kaiju);
        else if (kb.digit2Key.wasPressedThisFrame) DebugSetDay(DayType.Hero);
        else if (kb.digit3Key.wasPressedThisFrame) DebugSetDay(DayType.Crowded);
        else if (kb.digit4Key.wasPressedThisFrame) DebugSetDay(DayType.Normal);
        else if (kb.digit5Key.wasPressedThisFrame) forceDayType = -1;
        else if (kb.digit6Key.wasPressedThisFrame)
        {
            if (!weatherOn) weatherOn = true;
            weather.DebugRain(weather.Rain < 0.5f);
        }
        else if (kb.digit7Key.wasPressedThisFrame)
        {
            if (!weatherOn) weatherOn = true;
            weather.DebugAddHours(3f);
            ApplyWeather();
        }
        else if (kb.digit8Key.wasPressedThisFrame) elapsed += levelSeconds;          // 다음 프레임에 레벨이 한 단계 오른다
        else if (kb.digit9Key.wasPressedThisFrame) rage.DebugFill();
        else if (kb.gKey.wasPressedThisFrame) debugGod = !debugGod;
        else changed = false;
        if (changed) RefreshDebug();
    }

    // 날 종류를 고정하고, 플레이 중이면 지금 바로 바꾼다
    void DebugSetDay(DayType t)
    {
        forceDayType = (int)t;
        if (flow != GameFlow.Playing || gameOver) return;
        dayEvents.DebugForce(t, elapsed);
        traffic.ClearEventObjects();                          // 이전 사건의 괴수/불길/연기를 치운다
        peds.CrowdMul = dayEvents.CrowdMul;
        peds.MaxMul = dayEvents.MaxPedsMul;
        taxi.CallerDelayMul = dayEvents.CallerDelayMul;
        ApplyDifficulty();
    }

    void RefreshDebug()
    {
        debugText = "디버그 (F1 닫기)" + NL
                  + "1  괴수 침공" + NL
                  + "2  히어로와 빌런" + NL
                  + "3  사람 많은 날" + NL
                  + "4  평범한 날 (사건 끝)" + NL
                  + "5  날 종류 무작위로" + NL
                  + "6  비 켜기 / 끄기" + NL
                  + "7  시각 +3시간" + NL
                  + "8  레벨 +1" + NL
                  + "9  분노 바로 발동" + NL
                  + "G  무적 " + (debugGod ? "켜짐" : "꺼짐") + NL
                  + "다음 판 날: " + DebugDayNames[Mathf.Clamp(forceDayType + 1, 0, 4)];
    }

    void DrawDebug(float W, float H, float u)
    {
        if (!debugOpen) return;
        Fill(new Rect(14 * u, 326 * u, 300 * u, 284 * u), new Color(0f, 0f, 0f, 0.72f));
        Label(new Rect(24 * u, 330 * u, 286 * u, 276 * u), debugText, 17, new Color(0.7f, 1f, 0.8f), TextAnchor.UpperLeft);
    }
}
