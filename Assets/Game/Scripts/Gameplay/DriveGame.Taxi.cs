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
        taxi.InIntersection = z => IsIntersection(Mathf.FloorToInt((dist + z + 8f) / StripLen));       // 교차로 한가운데서 손님이 부르지 않게
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
        TickBombBeep();
        UpdateVoice();
        TickKaijuStomp();
    }

    // 폭탄 손님: 도화선이 타는 동안 삐 삐 삐. 터질 때가 가까울수록 간격이 짧아지고 소리가 커진다
    float nextBeepAt;

    void TickBombBeep()
    {
        bool burning = taxi.State == TaxiState.Carrying && taxi.Pax != null && taxi.Pax.quirk == Quirk.Bomb && taxi.Fuse < 0.98f && !gameOver && flow == GameFlow.Playing;
        if (!burning) return;
        if (Time.unscaledTime < nextBeepAt) return;
        float danger = 1f - Mathf.Clamp01(taxi.Fuse);
        nextBeepAt = Time.unscaledTime + Mathf.Lerp(0.8f, 0.12f, danger);
        sound.PlayBeep(Mathf.Lerp(0.5f, 1f, danger));
    }

    // 괴수가 서 있는 동안 쿵 쿵 쿵: 가까울수록 크고 땅이 흔들린다
    float nextStompAt;

    void TickKaijuStomp()
    {
        float d = traffic.KaijuDist;
        if (d < 0f || gameOver || flow != GameFlow.Playing) return;
        if (Time.unscaledTime < nextStompAt) return;
        nextStompAt = Time.unscaledTime + 1.1f;
        float near = 1f - Mathf.Clamp01(d / 140f);
        sound.PlayStomp(Mathf.Lerp(0.35f, 1f, near));
        if (d < 60f) shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.15f);
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
        sound.PlayDoorOpen();                                // 손님이 타는 소리: 문 열고, 앉고, 닫는다
        sound.PlayLater("doorclose", 0.7f, 0.9f);
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
        sound.PlayLater("dooropen", 0.5f, 0.9f);              // 손님이 내리는 소리
        sound.PlayLater("doorclose", 1.3f, 0.9f);
    }
}
