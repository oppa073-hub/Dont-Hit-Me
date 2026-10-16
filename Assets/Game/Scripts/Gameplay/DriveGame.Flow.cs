using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 게임 흐름: 타이틀 -> 플레이 <-> 일시정지, 게임오버 때 결과와 최고 점수 저장.
public partial class DriveGame
{
    enum GameFlow { Title, Playing, Paused }

    GameFlow flow = GameFlow.Title;       // 실행하면 타이틀에서 시작한다

    const string BestKey = "DontHitMe.Best";

    int best;                             // 지금까지의 최고 점수 (PlayerPrefs 에 저장)
    bool bestSaved, newBest;              // 이번 판의 결과를 이미 저장했는지 / 이번 판이 최고 기록인지
    string bestText = "", resultText = "";

    static readonly string NL = ((char)10).ToString();

    void LoadBest()
    {
        best = PlayerPrefs.GetInt(BestKey, 0);
        bestText = best > 0 ? "최고 점수 " + best : "";
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
        if (total > best)
        {
            best = total;
            newBest = true;
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
            bestText = "최고 점수 " + best;
        }
        resultText = "점수 " + total + (newBest ? "   NEW BEST!" : "") + NL
                   + "손님 " + taxi.Trips + "명   요금 " + taxi.FareTotal + "원" + NL
                   + "니어미스 " + nearMisses + "   충돌 " + hits + "   플레이 시간 " + Mathf.RoundToInt(elapsed) + "초" + NL
                   + bestText;
    }
}
