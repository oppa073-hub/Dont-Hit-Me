using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 날짜/사건 연결부: 날의 종류(EventManager)에 따라 보행자 수를 늘리고, 괴수와 레이저 사건을 TrafficManager 에 시키고, 소리와 흔들림을 붙인다.
// 시험용 키: F2 = 괴수 침공 바로 시작, F3 = 히어로와 빌런 전투 바로 시작 (플레이 중에).
public partial class DriveGame
{
    EventManager dayEvents;
    TrafficSprites trafficSprites;
    float rumbleT;

    void CreateEvents()
    {
        dayEvents = new EventManager(this);
        dayEvents.Started += OnEventStarted;
        traffic.KaijuSpawned += OnKaijuSpawned;
        traffic.LaserFired += OnLaserFired;
    }

    // 판을 시작할 때 맨 먼저: 오늘이 어떤 날인지 정하고 보행자/손님 배율을 알려 준다
    void ResetEvents()
    {
        dayEvents.Reset();
        peds.CrowdMul = dayEvents.CrowdMul;
        peds.MaxMul = dayEvents.MaxPedsMul;
        taxi.CallerDelayMul = dayEvents.CallerDelayMul;
        rumbleT = 0f;
    }

    // 메인 루프에서 매 프레임
    void UpdateEvents(float dt, float advance)
    {
        if (debugGod) session.ExtendInvuln(1f);
        var kb = Keyboard.current;
        if (kb != null && (Application.isEditor || Debug.isDebugBuild))      // 시험용 키는 에디터/개발 빌드에서만
        {
            if (kb.f2Key.wasPressedThisFrame) dayEvents.DebugStart(DayType.Kaiju, elapsed);
            if (kb.f3Key.wasPressedThisFrame) dayEvents.DebugStart(DayType.Hero, elapsed);
        }
        dayEvents.Tick(elapsed);
        traffic.TickKaiju(elapsed, speed, dayEvents.KaijuActive, nextInterDist, Period, turning, SpawnZ);
        traffic.TickHero(dt, elapsed, advance, dayEvents.HeroActive, turning, SpawnZ);

        // 괴수가 있는 동안 발걸음 소리처럼 화면이 가끔 울린다
        if (traffic.HasKaiju)
        {
            rumbleT -= dt;
            if (rumbleT <= 0f)
            {
                rumbleT = 2.2f;
                shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.15f);
            }
        }
    }

    void OnEventStarted(DayType t)
    {
        sound.PlayAlarm();
    }

    void OnKaijuSpawned() { sound.PlayRoar(); }

    void OnLaserFired() { sound.PlayLaser(); }
}
