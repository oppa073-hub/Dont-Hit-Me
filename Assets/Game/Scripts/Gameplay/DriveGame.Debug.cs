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
    int debugQuirk;
    static readonly string[] DebugQuirkNames = { "무작위", "폭탄", "결혼식", "외계인", "레이서", "욱한" };
    string debugText = "";
    float debugResetUntil;                                 // Del 을 한 번 눌렀다: 이 시각 안에 한 번 더 누르면 저장 데이터를 지운다

    static readonly string[] DebugDayNames = { "무작위", "평범한 날", "사람 많은 날", "괴수 침공", "히어로와 빌런", "좀비 사태" };

    static bool DebugAllowed { get { return Application.isEditor || Debug.isDebugBuild; } }

    // 반환값 true = 저장 데이터를 지우고 장면을 다시 불러오는 중이라 이번 프레임은 더 진행하지 않는다
    bool HandleDebugKeys(Keyboard kb)
    {
        if (!DebugAllowed) return false;
        if (kb.f1Key.wasPressedThisFrame)
        {
            debugOpen = !debugOpen;
            RefreshDebug();
        }
        if (!debugOpen || flow == GameFlow.Shop || flow == GameFlow.Settings) return false;

        // Del 두 번(3초 안): 저장 데이터를 전부 지우고 처음 실행한 것처럼 다시 시작한다
        if (kb.deleteKey.wasPressedThisFrame)
        {
            if (Time.unscaledTime < debugResetUntil)
            {
                DebugResetSave();
                return true;
            }
            debugResetUntil = Time.unscaledTime + 3f;
        }

        bool changed = true;
        if (kb.digit1Key.wasPressedThisFrame) DebugSetDay(DayType.Kaiju);
        else if (kb.digit2Key.wasPressedThisFrame) DebugSetDay(DayType.Hero);
        else if (kb.digit3Key.wasPressedThisFrame) DebugSetDay(DayType.Crowded);
        else if (kb.digit4Key.wasPressedThisFrame) DebugSetDay(DayType.Normal);
        else if (kb.digit5Key.wasPressedThisFrame) forceDayType = -1;
        else if (kb.digit0Key.wasPressedThisFrame) DebugSetDay(DayType.Zombie);
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
        else if (kb.cKey.wasPressedThisFrame) { upgrades.AddCoins(5000); RefreshShop(); }
        else if (kb.vKey.wasPressedThisFrame) taxi.ForceHeistNext = true;
        else if (kb.yKey.wasPressedThisFrame) enc.Force(EncounterType.Blackout, elapsed);
        else if (kb.kKey.wasPressedThisFrame) enc.Force(EncounterType.Rival, elapsed);
        else if (kb.lKey.wasPressedThisFrame) enc.Force(EncounterType.Fans, elapsed);
        else if (kb.jKey.wasPressedThisFrame) enc.Force(EncounterType.Pigs, elapsed);
        else if (kb.mKey.wasPressedThisFrame) enc.Force(EncounterType.Emergency, elapsed);
        else if (kb.bKey.wasPressedThisFrame)
        {
            debugQuirk = (debugQuirk + 1) % DebugQuirkNames.Length;                  // 다음 손님 유형을 고정 (0 = 무작위)
            taxi.ForcePaxType = debugQuirk == 0 ? null : PassengerCatalog.ByQuirk(debugQuirk);
        }
        else if (kb.gKey.wasPressedThisFrame) debugGod = !debugGod;
        else if (kb.f4Key.wasPressedThisFrame && flow == GameFlow.Playing && !gameOver) StartEnding(false);           // 엔딩 바로 보기 (본 것으로 기록하지 않는다)
        else if (kb.f6Key.wasPressedThisFrame) { PlayerPrefs.DeleteKey(EndingSeenKey); PlayerPrefs.Save(); Popup("엔딩 기록을 지웠다 (다음 손님에서 다시 나옴)", UiGold); }
        else changed = false;
        if (changed) RefreshDebug();
        return false;
    }

    // 최고 점수, 코인, 업그레이드, 택시 개조, 오늘의 목표, Day, 튜토리얼/인트로 기록, 설정을 전부 지우고 장면을 다시 불러온다
    void DebugResetSave()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
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
                  + "0  좀비 사태" + NL
                  + "6  비 켜기 / 끄기" + NL
                  + "7  시각 +3시간" + NL
                  + "8  레벨 +1" + NL
                  + "9  분노 바로 발동" + NL
                  + "C  코인 +5000" + NL
                  + "V  다음 손님을 은행 강도로" + NL
                  + "Y 정전   K 라이벌   L 팬   J 돼지   M 구급차" + NL
                  + "B  다음 손님: " + DebugQuirkNames[debugQuirk] + NL
                  + "F4  엔딩 바로 보기   F6  엔딩 기록 지우기" + NL
                  + "G  무적 " + (debugGod ? "켜짐" : "꺼짐") + NL
                  + "Del 두 번  저장 데이터 전부 초기화" + NL
                  + "다음 판 날: " + DebugDayNames[Mathf.Clamp(forceDayType + 1, 0, 5)];
    }

    void DrawDebug(float W, float H, float u)
    {
        if (!debugOpen) return;
        Panel(new Rect(14 * u, 276 * u, 320 * u, 438 * u), 0.78f);
        Label(new Rect(24 * u, 280 * u, 306 * u, 430 * u), debugText, 17, new Color(0.7f, 1f, 0.8f), TextAnchor.UpperLeft);
        if (Time.unscaledTime < debugResetUntil)
            Label(new Rect(0, H * 0.6f, W, 36 * u), "한 번 더 Del 을 누르면 저장 데이터가 모두 지워집니다!", 24, UiRed, TextAnchor.MiddleCenter);
    }
}
