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
        trafficSprites = MakeTrafficSprites();
        traffic = new TrafficManager(this, transform, peds, trafficSprites);
        traffic.Hit += OnTrafficHit;
        traffic.NearMiss += OnTrafficNearMiss;
    }

    // 레벨에 맞게 보행자/교통의 세기를 정한다 (판 시작과 레벨이 바뀔 때)
    void ApplyDifficulty()
    {
        peds.IntervalMul = difficulty.PedIntervalMul * dayEvents.PedIntervalMul;
        peds.DartBonus = difficulty.DartBonus;
        peds.DartSpeedMul = difficulty.DartSpeedMul;
        peds.CrossersBonus = difficulty.CrossersBonus;
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
        Popup(Loc.T("레벨 " + lv + " 상승!", "Level " + lv + "!"), new Color(1f, 0.9f, 0.3f));
        sound.PlayChime();
    }

    // 차나 장애물에 부딪혔다
    void OnTrafficHit(TrafficManager.Obj o)
    {
        if (rage.Active)
        {
            if (o.kind != TrafficManager.Flame) RageSmash();       // 불길은 연달아 있어서 점수/소리를 반복하지 않는다
            return;
        }
        if (o.kind == TrafficManager.Flame && invuln > 0f) return;      // 불길 차선을 달릴 때 불길마다 사고가 겹쳐 쌓이지 않게
        if (o.kind == TrafficManager.Sedan || o.kind == TrafficManager.Truck || o.kind == TrafficManager.Broken) { sound.PlayCrash(); sound.PlayBoom(); }
        else sound.PlayBonk();
        if (o.kind == TrafficManager.Cones)
        {
            // 콘은 목숨은 안 깎고 속도만 잃는다
            speed *= 0.6f;
            shakeUntil = Time.unscaledTime + 0.2f;
            Popup(Loc.T("덜컹!", "Bump!"), new Color(1f, 0.7f, 0.3f));
            return;
        }
        if (o.kind == TrafficManager.Pig)
        {
            // 돼지는 목숨은 안 깎지만 확실히 느껴지게: 속도가 크게 줄고, 흔들리고, 앞유리에 분홍 얼룩이 튀고, 손님이 놀란다
            speed *= 0.55f;
            hitStopUntil = Time.unscaledTime + hitStop;
            shakeUntil = Time.unscaledTime + 0.3f;
            sound.PlayOink();
            AddMud();
            taxi.OnPedHit();
            Popup(Loc.T("꿀꿀!  돼지를 쳤다", "Oink!  You hit a pig"), new Color(1f, 0.65f, 0.75f));
            return;
        }
        speed *= o.kind == TrafficManager.Kaiju ? 0.2f : (o.kind == TrafficManager.Flame ? 0.5f : hitSpeedFactor);
        hitStopUntil = Time.unscaledTime + hitStop;
        shakeUntil = Time.unscaledTime + (o.kind == TrafficManager.Kaiju ? 0.6f : 0.3f);
        taxi.OnPedHit();                      // 손님을 태운 채 사고가 나면 손님이 반응하고 요금이 깎인다
        string what = o.kind == TrafficManager.Kaiju ? Loc.T("괴수의 불길!", "Monster fire!") : (o.kind == TrafficManager.Flame ? Loc.T("레이저 불길!", "Laser fire!") : Loc.T("충돌!", "Crash!"));
        if (session.RegisterHit())
            Popup(what, new Color(1f, 0.4f, 0.4f));
    }

    // 다른 차 옆을 아슬아슬하게 지나갔다
    void OnTrafficNearMiss(TrafficManager.Obj o)
    {
        session.RegisterNearMiss(vehicleNearMissBonus);
        sound.PlayNearMiss();
        Popup(Loc.T("스치듯 통과!  +", "Squeaked by!  +") + vehicleNearMissBonus, new Color(0.4f, 1f, 0.6f));
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.06f);
        rage.AddNearMiss();
    }
}
