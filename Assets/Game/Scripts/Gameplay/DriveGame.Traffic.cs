using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 난이도/교통 연결부: 레벨은 DifficultyManager, 차와 돌발 장애물은 TrafficManager 가 하고, 여기서는 그 결과(속도, 목숨, 점수, 팝업)를 게임에 반영한다.
public partial class DriveGame
{
    DifficultyManager difficulty;
    TrafficManager traffic;

    int level { get { return difficulty.Level; } }

    void CreateTraffic()
    {
        difficulty = new DifficultyManager(this);
        difficulty.LevelChanged += OnLevelChanged;
        traffic = new TrafficManager(this, transform, peds, MakeTrafficSprites());
        traffic.Hit += OnTrafficHit;
        traffic.NearMiss += OnTrafficNearMiss;
    }

    // 레벨에 맞게 보행자/교통의 세기를 정한다 (판 시작과 레벨이 바뀔 때)
    void ApplyDifficulty()
    {
        peds.IntervalMul = difficulty.PedIntervalMul;
        peds.DartBonus = difficulty.DartBonus;
        peds.DartSpeedMul = difficulty.DartSpeedMul;
        traffic.Level = difficulty.Level;
    }

    void ResetTraffic()
    {
        difficulty.Reset();
        ApplyDifficulty();
        traffic.Reset();
    }

    void UpdateTraffic(float dt, float advance)
    {
        difficulty.Tick(elapsed);
        traffic.Tick(dt, elapsed, advance, carX, turning, SpawnZ);
    }

    void OnLevelChanged(int lv)
    {
        ApplyDifficulty();
        Popup("LEVEL " + lv, new Color(1f, 0.9f, 0.3f));
        sound.PlayChime();
    }

    // 차나 장애물에 부딪혔다
    void OnTrafficHit(TrafficManager.Obj o)
    {
        sound.PlayBonk();
        if (o.kind == TrafficManager.Cones)
        {
            // 콘은 목숨은 안 깎고 속도만 잃는다
            speed *= 0.6f;
            shakeUntil = Time.unscaledTime + 0.2f;
            Popup("BUMP!", new Color(1f, 0.7f, 0.3f));
            return;
        }
        speed *= hitSpeedFactor;
        hitStopUntil = Time.unscaledTime + hitStop;
        shakeUntil = Time.unscaledTime + 0.3f;
        taxi.OnPedHit();                      // 손님을 태운 채 사고가 나면 손님이 반응하고 요금이 깎인다
        if (session.RegisterHit())
            Popup("CRASH!", new Color(1f, 0.4f, 0.4f));
    }

    // 다른 차 옆을 아슬아슬하게 지나갔다
    void OnTrafficNearMiss(TrafficManager.Obj o)
    {
        session.RegisterNearMiss(vehicleNearMissBonus);
        sound.PlayNearMiss();
        Popup("CLOSE PASS +" + vehicleNearMissBonus, new Color(0.4f, 1f, 0.6f));
        shakeUntil = Time.unscaledTime + 0.06f;
    }
}
