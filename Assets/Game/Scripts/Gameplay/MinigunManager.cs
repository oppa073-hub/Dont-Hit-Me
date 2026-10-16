using UnityEngine;

// 미니건 매니저: 분노 폭발 중에 지붕 양옆의 미니건이 튀어나와 눈앞의 것들을 쓸어버린다.
// 맞는 것: 보행자(손님 제외), 다른 차, 콘, 공, 레이저 불길. 맞지 않는 것: 괴수, 하늘의 히어로/빌런, 손님(길가에서 기다리는 사람).
// 펼쳐지는 정도(Deploy)와 쏘는 중인지(Firing)를 알려 주고, 화면 표현(미니건 3D 부품, 총구 불꽃, 예광탄)은 DriveGame.Minigun.cs 가 한다.
// 설정 값(초당 처치 수, 사거리)은 DriveGame 의 public 필드를 읽는다.
public class MinigunManager
{
    readonly DriveGame cfg;
    readonly PedestrianManager peds;
    readonly TrafficManager traffic;
    float killT;

    /// <summary>접힘 0 ~ 완전히 튀어나옴 1.</summary>
    public float Deploy { get; private set; }
    public bool Firing { get; private set; }

    /// <summary>하나를 쓰러뜨렸다 (점수 처리용).</summary>
    public event System.Action Killed;

    public MinigunManager(DriveGame cfg, PedestrianManager peds, TrafficManager traffic)
    {
        this.cfg = cfg;
        this.peds = peds;
        this.traffic = traffic;
    }

    public void Reset()
    {
        Deploy = 0f;
        Firing = false;
        killT = 0f;
    }

    /// <summary>active = 지금 미니건을 써야 하는가(분노 중 + 개조 완료), canShoot = 쏠 수 있는 상태(교차로 회전 중이 아님).</summary>
    public void Tick(float dt, bool active, bool canShoot)
    {
        Deploy = Mathf.MoveTowards(Deploy, active ? 1f : 0f, dt * (active ? 4f : 3f));
        Firing = active && canShoot && Deploy > 0.95f;
        if (!Firing)
        {
            killT = 0f;
            return;
        }
        float period = 1f / Mathf.Max(1f, cfg.gunKillsPerSec);
        killT -= dt;
        int guard = 0;
        while (killT <= 0f && guard++ < 3)
        {
            killT += period;
            if (!KillOne()) { killT = 0f; break; }
        }
    }

    // 가장 가까운 대상 하나를 쓰러뜨린다. 대상이 없으면 false.
    bool KillOne()
    {
        const float MinZ = 3f;
        float maxZ = cfg.gunRange;
        float bestZ = float.MaxValue;
        PedestrianManager.Ped bestPed = null;
        TrafficManager.Obj bestObj = null;

        for (int i = 0; i < peds.Count; i++)
        {
            var p = peds[i];
            if (p.caller || p.fallT >= 0f || p.z < MinZ || p.z > maxZ) continue;       // 손님(기다리는 사람)은 쏘지 않는다
            if (p.z < bestZ) { bestZ = p.z; bestPed = p; bestObj = null; }
        }
        for (int i = 0; i < traffic.Count; i++)
        {
            var o = traffic[i];
            int k = o.kind;
            if (k == TrafficManager.Kaiju || k == TrafficManager.Smoke || k == TrafficManager.Boom) continue;     // 괴수와 장식은 제외
            if (o.fallT >= 0f || o.z < MinZ || o.z > maxZ) continue;
            if (o.z < bestZ) { bestZ = o.z; bestObj = o; bestPed = null; }
        }

        if (bestPed != null)
        {
            peds.Shoot(bestPed);
            traffic.SpawnBoom(bestPed.x, bestPed.z);
        }
        else if (bestObj != null) traffic.Shoot(bestObj);
        else return false;

        if (Killed != null) Killed();
        return true;
    }
}
