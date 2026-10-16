using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 게임 흐름: 타이틀 -> 플레이 <-> 일시정지, 게임오버(또는 영업 종료) 때 결과 글자와 최고 점수 저장.
// 모드가 두 가지: 하루 영업(정해진 시간 동안 일하고 결산, DriveGame.Shift.cs) / 무한(목숨이 다할 때까지).
public partial class DriveGame
{
    enum GameFlow { Title, Playing, Paused, Shop, Settings }

    GameFlow flow = GameFlow.Title;       // 실행하면 타이틀에서 시작한다

    const string BestKey = "DontHitMe.Best";             // 무한 모드 최고 점수
    const string BestShiftKey = "DontHitMe.BestShift";   // 하루 영업 모드 최고 점수

    int best, bestShift;                  // 지금까지의 최고 점수 (PlayerPrefs 에 저장)
    bool bestSaved, newBest;              // 이번 판의 결과를 이미 저장했는지 / 이번 판이 최고 기록인지
    string modeText = "", resultText = "";

    static readonly string NL = ((char)10).ToString();

    void LoadBest()
    {
        best = PlayerPrefs.GetInt(BestKey, 0);
        bestShift = PlayerPrefs.GetInt(BestShiftKey, 0);
        shiftMode = PlayerPrefs.GetInt(ModeKey, 1) == 1;
        RefreshModeText();
    }

    // 타이틀에 보이는 모드 안내: 지금 모드, 그 모드의 최고 점수, 바꾸는 키
    void RefreshModeText()
    {
        int b = shiftMode ? bestShift : best;
        modeText = (shiftMode ? "하루 영업 모드 (" + Mathf.RoundToInt(shiftSeconds) + "초)" : "무한 모드")
                 + (b > 0 ? "   최고 점수 " + b : "")
                 + "      Tab  모드 변경";
    }

    // 타이틀에서 시작/게임오버 뒤 재시작: 판을 새로 시작하고 플레이 상태로
    void StartGame()
    {
        flow = GameFlow.Playing;
        ResetGame();
    }

    void ResetFlowState()
    {
        bestSaved = newBest = false;
        resultText = "";
    }

    // 게임오버가 된 첫 프레임에 한 번: 최고 점수를 저장하고 결과 글자를 만든다 (OnGUI 가 매 프레임 문자열을 만들지 않게)
    void SaveResultOnce()
    {
        if (bestSaved) return;
        bestSaved = true;
        int total = session.TotalScore;
        int modeBest = shiftMode ? bestShift : best;
        if (total > modeBest)
        {
            newBest = true;
            if (shiftMode) { bestShift = total; PlayerPrefs.SetInt(BestShiftKey, bestShift); }
            else { best = total; PlayerPrefs.SetInt(BestKey, best); }
            PlayerPrefs.Save();
        }
        RefreshModeText();

        string head = "점수 " + total + (newBest ? "   NEW BEST!" : "") + NL;
        string tail = "니어미스 " + nearMisses + "   플레이 시간 " + Mathf.RoundToInt(elapsed) + "초   레벨 " + difficulty.MaxLevelReached + NL;
        if (shiftMode)
        {
            int fine = hits * accidentFine;
            resultText = head
                       + "손님 " + taxi.Trips + "명   요금 합계 " + taxi.FareTotal + "원" + NL
                       + "사고 " + hits + "건   수리비 -" + fine + "원" + NL
                       + "오늘 순수익 " + ShiftNet() + "원   평점 " + ShiftStars() + " / 3" + NL
                       + tail
                       + GrantRunCoins();
        }
        else
        {
            resultText = head
                       + "손님 " + taxi.Trips + "명   요금 " + taxi.FareTotal + "원" + NL
                       + "충돌 " + hits + "   " + tail
                       + GrantRunCoins();
        }
    }
}
