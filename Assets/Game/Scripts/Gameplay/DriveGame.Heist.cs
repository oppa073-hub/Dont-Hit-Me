using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 은행 강도 인카운트 연결부: 손님을 태웠더니 수상한 손님이라 은행까지 가서 기다리게 하고, 나오면 경찰이 길을 막는 가운데 도망친다.
// 손님과 단계(은행으로 가기 / 기다리기 / 도망치기)는 TaxiManager 가 하고, 경찰 바리케이드는 TrafficManager 가 만든다.
// 여기서는 도망이 시작되면 경찰 바리케이드를 켜고, 사이렌 소리와 화면 가장자리의 붉은/푸른 경광을 붙이고, 성공하면 보너스를 준다.
public partial class DriveGame
{
    float sirenT;

    void OnHeistFlee()
    {
        traffic.StartPolice(elapsed);
        sirenT = 0f;                                         // 첫 사이렌은 UpdatePolice 가 바로 울린다
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.2f);
    }

    void OnHeistWon()
    {
        traffic.ClearPolice();
        session.AddScore(300);                               // 도착 종소리는 정산(OnTaxiArrived)에서 난다
    }

    // 메인 루프에서 매 프레임
    void UpdatePolice(float dt)
    {
        bool flee = taxi.HeistFlee;
        traffic.TickPolice(elapsed, flee, turning, SpawnZ);
        if (flee)
        {
            sirenT -= dt;
            if (sirenT <= 0f)
            {
                sirenT = 3.4f;
                sound.PlayAlarm();
            }
        }
    }

    // 도망 중에는 화면 양옆이 붉은색/푸른색으로 번갈아 번쩍인다
    void DrawPoliceOverlay(float W, float H)
    {
        if (!taxi.HeistFlee || gameOver || flow != GameFlow.Playing) return;
        bool a = Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
        Color red = new Color(1f, 0.05f, 0.05f, 0.20f), blue = new Color(0.1f, 0.3f, 1f, 0.20f);
        Fill(new Rect(0, 0, W * 0.10f, H * 0.80f), a ? red : blue);
        Fill(new Rect(W * 0.90f, 0, W * 0.10f, H * 0.80f), a ? blue : red);
    }
}
