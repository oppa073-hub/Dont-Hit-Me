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

    // 결혼식 커플이 흔들림에 부케를 던졌다
    void OnBouquetThrown()
    {
        AddBouquet();
        sound.PlayNearMiss();
    }
}
