using System.Collections.Generic;
using UnityEngine;

// 보행자 매니저: 생성 시점, 인도 걷기, 도로 횡단, 충돌/니어미스 판정을 맡는다.
// 점수, 목숨, 손님 리액션 같은 "게임 결과"는 모른다. 치였을 때(Hit)와 아슬아슬하게 피했을 때(NearMiss)를 이벤트로 알리고 DriveGame 이 처리한다.
// 설정 값(생성 간격, 속도, 거리 등)은 씬에 저장된 DriveGame 의 public 필드를 그대로 읽는다.
public class PedestrianManager
{
    public class Ped
    {
        public Transform t;
        public SpriteRenderer sr;
        public float x, z, dir, walkSpeed, pauseLeft = -1f, minLat = 999f, fallT = -1f, phase;
        public bool scored, crossed;      // crossed: 도로에 나온 적이 있는 사람만 충돌/니어미스 점수 대상
        public Color baseColor;
        public int kind;                  // 0 인도 보행자, 1 멀리서 이미 건너는 사람, 2 가까이서 갑자기 튀어나오는 사람
        public int st;                    // 0 인도 걷기, 1 연석으로 이동, 2 연석에서 멈칫, 3 도로 건너는 중
        public float side;                // 인도 쪽 (-1 왼쪽, +1 오른쪽)
        public float walkZ;               // 인도를 따라 걷는 속도(월드 기준, +는 멀어지는 쪽)
        public float triggerZ, waitTime, curbX;
        public float triggerT;            // 건너기 시작하는 시점: 차가 도착하기 이만큼(초) 전에 (속도가 빨라도 같은 여유 시간). 0 이면 triggerZ(m) 를 쓴다
        public bool bypassCap;            // 동시 횡단 인원 제한을 받지 않는다 (팬 습격, 분노 때 몰려든 사람)
        public bool caller;               // 택시를 부르는 손님 (머리 위에 !)
        public bool zombie;               // 좀비: 치면 보상 (좀비의 날)
        public bool survivor;             // 생존자: 좀비의 날에 섞여 나오는, 치면 안 되는 사람
    }

    const float PedHalfWidth = 0.3f;

    readonly DriveGame cfg;
    readonly Transform parent;
    readonly Sprite sprite;
    readonly List<Ped> peds = new List<Ped>();
    float nextSpawn;

    // 난이도 배율 (DriveGame 이 레벨에 맞게 정해 준다. 기본값은 영향 없음)
    public float IntervalMul = 1f;       // 생성 간격에 곱함 (작을수록 사람이 자주 나옴)
    public float DartBonus = 0f;         // 갑자기 튀어나오는 사람 비율을 이만큼 늘림
    public float DartSpeedMul = 1f;      // 튀어나오는 속도 배율
    public float CrowdMul = 1f;          // 시작할 때 깔아 두는 사람 수 배율 (사람 많은 날)
    public float MaxMul = 1f;            // 동시에 있을 수 있는 사람 수 배율 (사람 많은 날)
    public bool FanRush;                 // 팬 습격 중: 차 앞으로 튀어나오는 사람이 빠르게 계속 생긴다
    float nextFan;
    public bool RageSurge;               // 분노 폭발 중: 박살 낼 사람들이 더 많이 몰려든다 (분노는 보상이라서)
    float nextSurge;
    public bool ZombieMode;              // 좀비 사태 중: 새 보행자가 좀비/생존자가 되고 좀비가 추가로 몰려나온다
    float nextZombie;
    public float PlayerSpeed = 20f;      // 지금 차의 속도 (건너기 시작하는 거리를 속도에 맞추는 데 쓴다)
    int crossersNow;                     // 지금 건너는 중이거나 건너려고 연석에 나온 사람 수

    /// <summary>차에 치였다. 쓰러지는 연출은 매니저가 하고, 점수/목숨/요금은 받는 쪽이 처리한다.</summary>
    public event System.Action<Ped> Hit;
    /// <summary>아슬아슬하게 피하고 지나갔다.</summary>
    public event System.Action<Ped> NearMiss;

    public PedestrianManager(DriveGame cfg, Transform parent, Sprite sprite)
    {
        this.cfg = cfg;
        this.parent = parent;
        this.sprite = sprite;
    }

    public Ped this[int i] { get { return peds[i]; } }      // 매 프레임 순회해도 메모리가 생기지 않게 인덱스로 읽는다
    public int Count { get { return peds.Count; } }
    public bool Contains(Ped p) { return peds.Contains(p); }

    /// <summary>판을 새로 시작: 모두 치우고 인도에 사람을 깔아 둔다.</summary>
    public void Reset(float spawnZ)
    {
        Clear();
        for (int i = 0, n = Mathf.RoundToInt(cfg.initialCrowd * CrowdMul); i < n; i++) Spawn(spawnZ, 0, Random.Range(6f, spawnZ));
        nextSpawn = 1.2f;
        nextFan = 0f;
        nextSurge = 0f;
        nextZombie = 0f;
        ZombieMode = false;
    }

    /// <summary>회전이 끝난 뒤: 새 도로 기준으로 다시 깐다 (가까운 곳은 비워서 멀리서부터 자연스럽게 나타나게).</summary>
    public void Respawn(float spawnZ)
    {
        Clear();
        for (int i = 0, n = Mathf.RoundToInt(cfg.initialCrowd * CrowdMul); i < n; i++) Spawn(spawnZ, 0, Random.Range(20f, Mathf.Max(30f, spawnZ)));
    }

    public void Clear()
    {
        foreach (var p in peds) if (p.t != null) Object.Destroy(p.t.gameObject);
        peds.Clear();
    }

    /// <summary>총에 맞았다: 쓰러지는 연출을 시작한다 (충돌/니어미스 판정은 하지 않는다).</summary>
    public void Shoot(Ped p)
    {
        if (p.fallT >= 0f) return;
        p.fallT = 0f;
        p.dir = Random.value < 0.5f ? -1f : 1f;
        p.sr.color = new Color(1f, 0.4f, 0.4f);
    }

    public void Remove(Ped p)
    {
        int i = peds.IndexOf(p);
        if (i >= 0) RemoveAt(i);
    }

    void RemoveAt(int i)
    {
        if (peds[i].t != null) Object.Destroy(peds[i].t.gameObject);
        peds.RemoveAt(i);
    }

    // 새 사람 한 명을 인도에 만든다. kind 를 -1 로 주면 비율대로 무작위 종류를 고른다.
    // forceSide 를 -1/1 로 주면 그쪽 인도에 만든다 (0 이면 무작위).
    public Ped Spawn(float spawnZ, int kind = -1, float z = -1f, float forceSide = 0f)
    {
        if (kind < 0)
        {
            float r = Random.value;
            float walkers = Mathf.Max(0.2f, cfg.sidewalkerRatio - DartBonus);
            kind = r < walkers ? 0 : (r < walkers + cfg.farCrossRatio ? 1 : 2);
        }
        if (z < 0f) z = spawnZ + Random.Range(0f, 10f);

        float side = forceSide != 0f ? Mathf.Sign(forceSide) : (Random.value < 0.5f ? -1f : 1f);
        var go = new GameObject("Ped");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        Color c = Random.ColorHSV(0f, 1f, 0.5f, 0.7f, 0.8f, 1f);
        sr.color = c;

        float offMax = kind == 2 ? Mathf.Min(cfg.sidewalkOffset.y, 2f) : cfg.sidewalkOffset.y;   // 튀어나올 사람은 연석 가까이
        float off = Random.Range(Mathf.Min(cfg.sidewalkOffset.x, offMax), offMax);
        var p = new Ped
        {
            t = go.transform,
            sr = sr,
            kind = kind,
            side = side,
            dir = -side,
            x = side * (cfg.roadHalfWidth + off),
            z = z,
            curbX = side * (cfg.roadHalfWidth + 0.35f),
            walkZ = Random.Range(cfg.sidewalkWalkSpeed.x, cfg.sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f),
            walkSpeed = kind == 2 ? Random.Range(cfg.dartSpeed.x, cfg.dartSpeed.y) * DartSpeedMul : Random.Range(cfg.crossSpeed.x, cfg.crossSpeed.y),
            triggerZ = kind == 1 ? Random.Range(cfg.farTrigger.x, cfg.farTrigger.y) : Random.Range(cfg.dartTrigger.x, cfg.dartTrigger.y),
            triggerT = kind == 1 ? Random.Range(cfg.farLeadTime.x, cfg.farLeadTime.y) : (kind == 2 ? Random.Range(cfg.dartLeadTime.x, cfg.dartLeadTime.y) : 0f),
            waitTime = kind == 2 ? Random.Range(cfg.curbWait.x, cfg.curbWait.y) : 0f,
            baseColor = c,
            phase = Random.value * 6f
        };
        peds.Add(p);
        if (ZombieMode) Infect(p, false);
        return p;
    }

    static readonly Color SurvivorColor = new Color(1f, 0.62f, 0.12f);

    // 좀비 사태: 보행자를 좀비(초록, 느리게 비틀거림)나 생존자(주황, 치면 안 됨)로 바꾼다. preferZombie 면 생존자가 아니면 거의 좀비
    void Infect(Ped p, bool preferZombie)
    {
        float roll = Random.value;
        if (p.kind >= 1 && roll < cfg.zombieSurvivorRatio)
        {
            p.survivor = true;
            p.baseColor = SurvivorColor;
        }
        else if (preferZombie || roll < cfg.zombieSurvivorRatio + cfg.zombieRatio)
        {
            p.zombie = true;
            p.baseColor = new Color(Random.Range(0.12f, 0.28f), Random.Range(0.6f, 0.8f), Random.Range(0.08f, 0.22f));       // 진한 초록
            p.walkSpeed *= p.kind == 2 ? Mathf.Lerp(1f, cfg.zombieSlow, 0.6f) : cfg.zombieSlow;
        }
        p.sr.color = p.baseColor;
    }

    /// <summary>좀비 사태가 끝났다: 남은 좀비와 생존자는 평범한 시민으로 돌아간다.</summary>
    public void EndZombies()
    {
        foreach (var p in peds)
        {
            if (!p.zombie && !p.survivor) continue;
            p.zombie = p.survivor = false;
            p.baseColor = Random.ColorHSV(0f, 1f, 0.5f, 0.7f, 0.8f, 1f);
            if (p.sr != null) p.sr.color = p.baseColor;
        }
    }

    /// <summary>
    /// 한 프레임 진행. advance = 이번 프레임에 월드가 차 쪽으로 다가온 거리(m), carX = 차의 좌우 위치,
    /// turning = 교차로 회전 중(충돌/니어미스 판정을 끈다), spawnZ = 사람이 새로 나타나는 거리.
    /// </summary>
    public void Tick(float dt, float elapsed, float advance, float carX, bool turning, float spawnZ)
    {
        // 지금 건너는(또는 건너려고 나온) 사람 수. 너무 많이 한꺼번에 건너지 않게 새로 건너기 시작하는 사람을 제한한다.
        crossersNow = 0;
        for (int ci = 0; ci < peds.Count; ci++)
            if (peds[ci].st >= 1 && peds[ci].st <= 3 && peds[ci].fallT < 0f) crossersNow++;

        // 팬 습격: 연예인을 보고 몰려드는 사람들
        if (FanRush && elapsed >= nextFan && peds.Count < Mathf.RoundToInt(cfg.maxPeds * MaxMul) + 25)
        {
            var fp = Spawn(spawnZ, 2);
            fp.bypassCap = true;
            nextFan = elapsed + 0.45f;
        }

        // 분노 폭발 중: 박살 낼 사람들이 계속 몰려든다 (멀리서 건너는 사람과 튀어나오는 사람)
        if (RageSurge && elapsed >= nextSurge && peds.Count < Mathf.RoundToInt(cfg.maxPeds * MaxMul) + 40)
        {
            var sp = Spawn(spawnZ, Random.value < 0.5f ? 1 : 2);
            sp.bypassCap = true;
            nextSurge = elapsed + Mathf.Max(0.08f, cfg.rageSurgeInterval);
        }

        // 좀비 사태: 도로로 나오는 좀비가 계속 추가로 생긴다 (치면 보상)
        if (ZombieMode && elapsed >= nextZombie && peds.Count < Mathf.RoundToInt(cfg.maxPeds * MaxMul) + 30)
        {
            var zp = Spawn(spawnZ, Random.value < 0.6f ? 1 : 2);
            if (!zp.zombie && !zp.survivor) Infect(zp, true);
            zp.bypassCap = true;
            nextZombie = elapsed + Mathf.Max(0.15f, cfg.zombieInterval) * Random.Range(0.7f, 1.3f);
        }

        // 스폰
        if (elapsed >= nextSpawn && peds.Count < Mathf.RoundToInt(cfg.maxPeds * MaxMul))
        {
            Spawn(spawnZ);
            float interval = Mathf.Max(cfg.minSpawnInterval, cfg.spawnInterval - elapsed * cfg.intervalRamp) * IntervalMul;
            nextSpawn = elapsed + interval * Random.Range(0.6f, 1.4f);
        }

        for (int i = peds.Count - 1; i >= 0; i--)
        {
            var p = peds[i];
            p.z -= advance;      // 회전 중에는 도로가 멈춰 있으니 사람만 앞으로 미끄러지지 않게 DriveGame 이 월드가 실제로 다가온 만큼만 준다

            if (p.fallT >= 0f)
            {
                p.fallT += dt;
                p.x += p.dir * 3f * dt;
                if (p.fallT > 1.2f || p.z < -6f)
                {
                    RemoveAt(i);
                    continue;
                }
            }
            else
            {
                UpdateBehavior(p, dt);

                bool inZone = p.z > -0.6f && p.z < 2.4f;
                float lat = Mathf.Abs(p.x - carX);
                // 충돌과 니어미스는 도로를 건너는 중(st == 3)인 사람만. 인도를 걷는 사람은 가까이 지나가도 점수/충돌 없음
                if (inZone)
                {
                    if (p.st == 3 && !turning)       // 회전 중에는 화면과 판정 좌표가 달라서 충돌/니어미스를 끈다
                    {
                        p.minLat = Mathf.Min(p.minLat, lat);
                        if (lat < DriveGame.CarHalfWidth + PedHalfWidth)
                        {
                            p.fallT = 0f;
                            p.sr.color = new Color(1f, 0.4f, 0.4f);
                            if (Hit != null) Hit(p);
                            continue;
                        }
                    }
                }
                else if (p.z <= -0.6f && !p.scored)
                {
                    p.scored = true;
                    if (p.minLat < cfg.nearMissDist && !p.zombie && NearMiss != null) NearMiss(p);
                }
            }

            if (p.z < -6f || p.z > spawnZ + 40f) RemoveAt(i);
        }
    }

    // 인도 걷기 → (트리거 거리 안이면) 연석으로 이동 → 멈칫 → 도로 건너기 → 건너편 인도에서 다시 걷기
    void UpdateBehavior(Ped p, float dt)
    {
        switch (p.st)
        {
            case 0: // 인도를 따라 걷는다
                p.z += p.walkZ * dt;
                if (p.kind != 0 && p.z > 0f)
                {
                    // 차가 도착하기 몇 초 전에 건너기 시작한다 (빠를수록 더 멀리서). 동시에 건너는 사람 수에는 상한이 있다.
                    float trig = p.triggerT > 0f ? p.triggerT * Mathf.Max(PlayerSpeed, 12f) : p.triggerZ;
                    if (p.z < trig && (p.bypassCap || crossersNow < cfg.maxCrossers))
                    {
                        p.st = 1;
                        crossersNow++;
                    }
                }
                break;

            case 1: // 연석으로 다가간다 (튀어나올 사람은 빠르게)
                p.x = Mathf.MoveTowards(p.x, p.curbX, (p.kind == 2 ? p.walkSpeed : 2.2f) * dt);
                if (Mathf.Approximately(p.x, p.curbX))
                {
                    p.st = p.waitTime > 0f ? 2 : 3;
                    p.pauseLeft = p.waitTime;
                }
                break;

            case 2: // 연석에서 멈칫 (밝아져서 예고가 된다)
                p.pauseLeft -= dt;
                if (p.pauseLeft <= 0f)
                {
                    p.pauseLeft = -1f;
                    p.st = 3;
                }
                break;

            default: // 도로를 건넌다
                p.crossed = true;
                p.x += p.dir * p.walkSpeed * dt;
                if (p.dir * p.x > cfg.roadHalfWidth + 0.6f)
                {
                    // 건너편 인도에 올라서면 그쪽 인도 보행자가 된다
                    p.st = 0;
                    p.kind = 0;
                    p.side = p.dir;
                    p.dir = -p.side;
                    p.walkZ = Random.Range(cfg.sidewalkWalkSpeed.x, cfg.sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
                }
                break;
        }
    }
}
