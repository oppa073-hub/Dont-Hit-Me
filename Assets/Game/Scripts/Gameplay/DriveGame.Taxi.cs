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
        camPitchTarget = TalkPitch();
    }

    // 대화 카메라: 손님의 "얼굴"이 화면 위쪽 3분의 1쯤(대화 상자 위)에 오게 한다. 예전에는 몸 가운데를 향해서 차가 가까우면 발만 보였다.
    // 손님 그림의 머리 높이는 약 1.45m. 유니티에서 x 회전이 양수면 아래를 본다.
    const float TalkFaceY = 1.45f;

    // 손님이 멀수록 더 확대해서(최소 14도) 얼굴이 크게 보이게 한다. 가까우면 sideFov 까지만 넓힌다
    float TalkFov()
    {
        float d = Mathf.Max(2f, taxi.TalkDist);
        return Mathf.Clamp(2f * Mathf.Atan(1.05f / d) * Mathf.Rad2Deg, 14f, sideFov);
    }

    // 화면 높이의 20%만큼 얼굴보다 아래를 화면 가운데로 잡으면 얼굴이 위에서 30% 지점에 온다
    float TalkPitch()
    {
        float d = Mathf.Max(2f, taxi.TalkDist);
        float view = 2f * d * Mathf.Tan(TalkFov() * 0.5f * Mathf.Deg2Rad);
        float aimY = TalkFaceY - 0.2f * view;
        return Mathf.Atan2(camHeight - aimY, d) * Mathf.Rad2Deg;
    }

    void AcceptPassenger()
    {
        // 하늘을 나는 택시를 산 뒤 첫 일반 손님: 태우는 순간 엔딩이 시작된다
        if (ShouldStartEnding()) { StartEnding(); return; }
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
