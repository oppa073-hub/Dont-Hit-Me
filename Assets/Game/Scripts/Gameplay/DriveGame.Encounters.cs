using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 랜덤 인카운트 연결부: 길거리 사건(정전, 라이벌 택시, 팬 습격, 돼지 떼)을 때에 맞춰 일으키고, 손님 유별 사건(폭탄 터짐, 부케)의 결과를 게임에 반영한다.
// 일정은 EncounterManager, 손님 쪽 규칙은 TaxiManager, 돼지와 라이벌 택시는 TrafficManager, 팬은 PedestrianManager 가 맡는다.
public partial class DriveGame
{
    EncounterManager enc;
    Material alienBeamMat;                    // 외계인 손님의 UFO 빛줄기

    void CreateEncounters()
    {
        enc = new EncounterManager(this);
        enc.Started += OnEncounterStarted;
        traffic.RivalArrived += OnRivalArrived;
        traffic.AmbulancePassed += OnAmbulancePassed;
    }

    void ResetEncounters()
    {
        enc.Reset(0f);
        peds.FanRush = false;
    }

    // 메인 루프에서 매 프레임
    void UpdateEncounters(float dt)
    {
        // 괴수/히어로 사건, 경찰 추격, 은행 대기, 영업이 끝나 가는 때에는 새 사건을 시작하지 않는다
        bool allow = level >= 2 && !turning && flow == GameFlow.Playing && !dayEvents.Active && !taxi.HeistFlee
                     && taxi.State != TaxiState.Waiting && !(shiftMode && shiftLeft < 25f);
        bool rivalOk = taxi.State == TaxiState.Cruise && taxi.Caller != null && taxi.Caller.z > 20f && taxi.Caller.z < SpawnZ - 6f;
        enc.Tick(dt, elapsed, allow, rivalOk);
        peds.FanRush = enc.Active == EncounterType.Fans;
        traffic.TickPigs(elapsed, enc.Active == EncounterType.Pigs, turning, SpawnZ);

        // 구급차가 가까워질수록 사이렌이 커진다
        float ad = traffic.AmbulanceDist;
        if (ad >= 0f)
        {
            sirenWailT -= dt;
            if (sirenWailT <= 0f)
            {
                sirenWailT = 1.5f;
                sound.PlaySiren(Mathf.Clamp01(1f - ad / 75f) * 0.9f + 0.1f);
            }
        }
    }

    float sirenWailT;

    // 룸미러와 화면 가장자리의 붉은/푸른 빛으로 구급차가 접근 중임을 알린다
    void DrawAmbulanceOverlay(float W, float H)
    {
        float ad = traffic.AmbulanceDist;
        if (ad < 0f || ad > 70f || gameOver || flow != GameFlow.Playing) return;
        float k = Mathf.Clamp01(1f - ad / 70f);
        bool a = Mathf.Repeat(Time.unscaledTime, 0.3f) < 0.15f;
        Color red = new Color(1f, 0.05f, 0.05f, 0.05f + 0.18f * k), blue = new Color(0.1f, 0.3f, 1f, 0.05f + 0.18f * k);
        Fill(new Rect(0, 0, W * 0.08f, H * 0.80f), a ? red : blue);
        Fill(new Rect(W * 0.92f, 0, W * 0.08f, H * 0.80f), a ? blue : red);
    }

    void OnEncounterStarted(EncounterType t)
    {
        switch (t)
        {
            case EncounterType.Blackout:
                dayEvents.RaiseAlert("정전! 도시가 암흑에 빠졌습니다 — 헤드라이트에 의지하세요!", 5f);
                sound.PlayPowerDown();
                break;
            case EncounterType.Fans:
                dayEvents.RaiseAlert("연예인 출몰! 팬들이 차 앞으로 몰려듭니다!", 5f);
                sound.PlayAlarm();
                break;
            case EncounterType.Pigs:
                dayEvents.RaiseAlert("트럭 전복! 돼지 떼가 도로를 건넙니다!", 5f);
                sound.PlayOink();
                break;
            case EncounterType.Emergency:
                dayEvents.RaiseAlert("구급차가 뒤에서 달려옵니다! 길을 비켜 주세요! (A / D)", 5f);
                traffic.SpawnAmbulance(carX, speed);
                sirenWailT = 0f;
                break;
            case EncounterType.Rival:
                dayEvents.RaiseAlert("라이벌 택시 접근! 먼저 손님을 태우세요!", 5f);
                traffic.SpawnRival(taxi.Caller, SpawnZ, speed);
                sound.PlayAlarm();
                break;
        }
    }

    // 라이벌 택시가 손님 앞에 먼저 도착했다
    void OnRivalArrived(PedestrianManager.Ped p)
    {
        if (!taxi.StealCaller(p)) return;
        Popup("라이벌 택시가 손님을 가로챘다!", new Color(1f, 0.5f, 0.4f));
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.2f);
        sound.PlayBonk();
    }

    // 구급차가 앞질러 갔다: 비켜 줬으면 보상, 길을 막고 있었으면 들이받힌다
    void OnAmbulancePassed(bool blocked)
    {
        if (!blocked)
        {
            session.AddScore(100);
            rage.AddNearMiss();
            Popup("양보 성공!  +100", UiGreen);
            sound.PlayChime();
            return;
        }
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.4f);
        sound.PlayBonk();
        if (rage.Active)
        {
            RageSmash();                                     // 분노 중에는 오히려 구급차를 박살
            return;
        }
        speed *= 0.4f;
        hitStopUntil = Time.unscaledTime + hitStop;
        taxi.OnPedHit();
        if (session.RegisterHit()) Popup("구급차를 막았다!  들이받혔다", UiRed);
    }

    // 폭탄 손님의 폭탄이 터졌다 (속도가 너무 오래 느렸다)
    void OnBombExploded()
    {
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.5f);
        sound.PlayBonk();
        if (rage.Active) return;                              // 분노 중에는 무적: 폭발해도 멀쩡하다
        speed *= 0.5f;
        hitStopUntil = Time.unscaledTime + hitStop;
        session.RegisterHit();
    }

    // 욱한 손님이 덤볐다: 화면이 흔들리고 휙 소리가 난다
    void OnWheelGrabbed()
    {
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.35f);
        sound.PlayNearMiss();
    }

    // 결혼식 커플이 흔들림에 부케를 던졌다
    void OnBouquetThrown()
    {
        AddBouquet();
        sound.PlayNearMiss();
    }
}
