using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 게임 흐름: 타이틀 -> 플레이 <-> 일시정지, 게임오버(또는 영업 종료) 때 결과 글자와 최고 점수 저장.
// 모드가 두 가지: 하루 영업(정해진 시간 동안 일하고 결산, DriveGame.Shift.cs) / 무한(목숨이 다할 때까지).
public partial class DriveGame
{
    enum GameFlow { Title, Lobby, Playing, Paused, Shop, Settings, Ending }

    GameFlow flow = GameFlow.Title;       // 실행하면 타이틀에서 시작한다

    const string BestKey = "DontHitMe.Best";             // 무한 모드 최고 점수
    const string BestShiftKey = "DontHitMe.BestShift";   // 하루 영업 모드 최고 점수

    int best, bestShift;                  // 지금까지의 최고 점수 (PlayerPrefs 에 저장)
    bool bestSaved, newBest;              // 이번 판의 결과를 이미 저장했는지 / 이번 판이 최고 기록인지
    string modeText = "", resultText = "";

    // 결과 화면용 숫자들 (게임오버가 된 첫 프레임에 한 번 채운다. 화면은 DriveGame.Screens.cs 의 DrawGameOver)
    bool resShift;
    int resDay, resScore, resTrips, resFare, resRepairs, resNet, resStars, resNear, resHits, resSeconds, resLevel, resEarned, resCoins, resGoalsDone, resGoalsMax;

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
        modeText = "Day " + dayEvents.Day + "      " + (shiftMode ? Loc.T("하루 영업 모드 (" + Mathf.RoundToInt(shiftSeconds) + "초)", "Daily Shift (" + Mathf.RoundToInt(shiftSeconds) + "s)") : Loc.T("무한 모드", "Endless Mode"))
                 + (b > 0 ? Loc.T("   최고 점수 ", "   Best score ") + b : "")
                 + Loc.T("      Tab  모드 변경", "      Tab  Switch mode");
    }

    // 타이틀에서 시작/게임오버 뒤 재시작: 판을 새로 시작하고 플레이 상태로
    void StartGame()
    {
        flow = GameFlow.Playing;
        ResetGame();
        dayEvents.Announce();
        StartNews();                         // 운전을 시작하면 내비 화면에 아침 뉴스가 나온다
    }

    // 지금 틀 음악: 타이틀/로비/상점은 로비 곡, 운전 중에는 시간대(낮/노을/밤)에 따라
    int MusicTrack()
    {
        if (flow == GameFlow.Ending) return -1;           // 엔딩은 음악 없이 효과음만
        if (flow == GameFlow.Title || flow == GameFlow.Lobby || flow == GameFlow.Shop) return AudioManager.TrackLobby;
        if (rage.Active && !gameOver) return AudioManager.TrackRage;
        if (weather.Darkness > 0.5f) return AudioManager.TrackNight;
        if (weather.Warm > 0.25f && weather.DayT > 0.5f) return AudioManager.TrackDusk;
        return AudioManager.TrackDay;
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
        sound.PlayStinger(lives > 0);          // 하루를 무사히 마쳤으면 승리, 목숨이 다했으면 패배 효과음
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

        // 결과 숫자들: 화면이 칸을 나눠 그린다
        resShift = shiftMode;
        resDay = dayEvents.Day;
        resScore = total;
        resTrips = taxi.Trips;
        resFare = taxi.FareTotal;
        resRepairs = hits * accidentFine;
        resNet = ShiftNet();
        resStars = ShiftStars();
        resNear = nearMisses;
        resHits = hits;
        resSeconds = Mathf.RoundToInt(elapsed);
        resLevel = difficulty.MaxLevelReached;
        GrantRunCoins();                  // 코인을 주고 resEarned/resCoins/resGoals* 를 채운다
        dayEvents.CompleteDay();          // 하루가 끝났다: 다음 날 번호로
        RefreshModeText();
    }
}
