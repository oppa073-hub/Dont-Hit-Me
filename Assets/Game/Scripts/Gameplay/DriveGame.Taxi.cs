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
        taxi.HeistFleeStarted += OnHeistFlee;
        taxi.HeistWon += OnHeistWon;
        taxi.BombExploded += OnBombExploded;
        taxi.BouquetThrown += OnBouquetThrown;
        taxi.WheelGrabbed += OnWheelGrabbed;
    }

    void UpdateTaxi(float dt)
    {
        taxi.HeistAllowed = heistOn && (!shiftMode || shiftLeft > 90f);        // 영업 시간이 모자라면 수상한 손님은 안 나온다
        taxi.Tick(dt, speed, carX, turning, SpawnZ);
    }

    // 대화가 시작됐다: 차를 세우고 손님 쪽으로 시점을 돌린다
    void OnTalkStarted(float yaw)
    {
        speed = 0f;
        camYawTarget = yaw;
        camPitchTarget = Mathf.Atan2(1.0f, Mathf.Max(2f, taxi.TalkDist)) * Mathf.Rad2Deg;       // 유니티에서 x 회전이 양수면 아래를 본다: 눈높이보다 낮은 곳을 보면 손님이 화면 위쪽(대화 상자 위)에 온다
    }

    void AcceptPassenger()
    {
        float cruise = Mathf.Min(Mathf.Min(maxSpeed, cruiseCap), baseSpeed + elapsed * speedRamp);
        taxi.Accept(gx, gy, heading, Period, cruise);
        EndNews();                                           // 손님을 태웠으니 내비는 지도로
        hintF = int.MinValue;                                // 안내 글자를 새 목적지로 다시 만든다
        camYawTarget = 0f;
        camPitchTarget = 0f;
    }

    void DeclinePassenger()
    {
        taxi.Decline();
        camYawTarget = 0f;
        camPitchTarget = 0f;
    }

    // 정산이 끝났다: 받은 돈의 일부를 점수에 더한다
    void OnTaxiArrived(int income)
    {
        session.AddScore(Mathf.RoundToInt(income / 100f));
        sound.PlayArrive();
    }
}
