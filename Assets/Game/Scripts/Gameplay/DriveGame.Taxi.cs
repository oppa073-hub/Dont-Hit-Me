using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 택시 연결부: 손님/요금/제한 시간 흐름은 TaxiManager 가 하고, 여기서는 그 결과(차 세우기, 시점, 점수, 팝업)를 게임에 반영한다.
public partial class DriveGame
{
    TaxiManager taxi;

    void CreateTaxi()
    {
        taxi = new TaxiManager(this, peds, DestRel);
        taxi.PopupRequested += Popup;
        taxi.TalkStarted += OnTalkStarted;
        taxi.Arrived += OnTaxiArrived;
    }

    void UpdateTaxi(float dt)
    {
        taxi.Tick(dt, speed, carX, turning, SpawnZ);
    }

    // 대화가 시작됐다: 차를 세우고 손님 쪽으로 시점을 돌린다
    void OnTalkStarted(float yaw)
    {
        speed = 0f;
        camYawTarget = yaw;
    }

    void AcceptPassenger()
    {
        float cruise = Mathf.Min(maxSpeed, baseSpeed + elapsed * speedRamp);
        taxi.Accept(gx, gy, heading, Period, cruise);
        hintF = int.MinValue;                                // 안내 글자를 새 목적지로 다시 만든다
        camYawTarget = 0f;
    }

    void DeclinePassenger()
    {
        taxi.Decline();
        camYawTarget = 0f;
    }

    // 정산이 끝났다: 받은 돈의 일부를 점수에 더한다
    void OnTaxiArrived(int income)
    {
        session.AddScore(Mathf.RoundToInt(income / 100f));
        sound.PlayArrive();
    }
}
