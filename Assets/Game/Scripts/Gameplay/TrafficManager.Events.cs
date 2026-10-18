using System.Collections.Generic;
using UnityEngine;

// 교통 매니저의 사건 부분: 괴수가 길을 막는 것과 히어로의 레이저가 한쪽 차선을 불태우는 것.
//  괴수: 다음 교차로 바로 뒤에 서서 도로 전체를 막는다 (교차로에서 돌아가야 한다). 부딪히면 목숨 -1 하고 물러난다. 길가에는 연기 기둥(무너진 건물)이 피어오른다.
//  레이저: 경고(얇은 빛) 뒤 한쪽 차선을 멀리서부터 쓸어 오며 불길을 남긴다. 반대쪽 차선으로 비켜 가면 안전하다.
public partial class TrafficManager
{
    float nextKaiju, nextStrike, strikeT, flameAcc, nextBlock, nextPig;
    PedestrianManager.Ped rivalTarget;
    Obj ambulance;

    /// <summary>구급차가 플레이어를 앞질렀다. true = 길을 막고 있어서 들이받았다, false = 비켜 줬다.</summary>
    public event System.Action<bool> AmbulancePassed;

    /// <summary>구급차가 뒤에서 접근 중이면 뒤쪽 거리(m), 아니면 -1.</summary>
    public float AmbulanceDist { get { return ambulance != null && ambulance.z < 0f ? -ambulance.z : -1f; } }

    /// <summary>구급차의 가로 위치 (시험 봇이 비켜 가는 데 쓴다).</summary>
    public float AmbulanceX { get { return ambulance != null ? ambulance.x : 0f; } }

    /// <summary>라이벌 택시가 손님을 태웠다 (손님을 가로챈다).</summary>
    public event System.Action<PedestrianManager.Ped> RivalArrived;
    /// <summary>플레이어가 라이벌 택시에 박치기했다 (남은 체력, 라이벌이 있는 쪽 -1/+1).</summary>
    public event System.Action<int, float> RivalHit;
    /// <summary>라이벌 택시를 격파했다 (보상).</summary>
    public event System.Action RivalDestroyed;

    /// <summary>레이저 상태: 0 없음, 1 경고, 2 쓸어 오는 중.</summary>
    public int StrikeState;
    public float StrikeSide;       // 불태우는 차선 쪽 (-1 왼쪽, +1 오른쪽)
    public float StrikeZ;          // 레이저 끝이 닿은 앞쪽 거리(m)

    public event System.Action KaijuSpawned, LaserFired;

    public bool HasKaiju
    {
        get
        {
            for (int i = 0; i < objs.Count; i++) if (objs[i].kind == Kaiju) return true;
            return false;
        }
    }

    /// <summary>괴수를 치운다 (사건 시간이 끝났는데 길을 막고 서 있으면 영영 못 지나가므로).</summary>
    public void RemoveKaiju()
    {
        for (int i = objs.Count - 1; i >= 0; i--) if (objs[i].kind == Kaiju) RemoveAt(i);
    }

    /// <summary>괴수, 불길, 연기를 모두 치운다 (사건이 끝났거나 시험용으로 바꿀 때).</summary>
    public void ClearEventObjects()
    {
        for (int i = objs.Count - 1; i >= 0; i--)
        {
            int k = objs[i].kind;
            if (k == Kaiju || k == Flame || k == Smoke || k == Boom || k == Police || k == Pig || k == Rival || k == Ambulance) RemoveAt(i);
        }
        ambulance = null;
        StrikeState = 0;
    }

    // ---- 구급차 (랜덤 인카운트) ----

    /// <summary>구급차를 뒤쪽 멀리에 만든다. 지금 차 위치 근처의 차선으로 달려오니, 그 차선에서 비켜야 한다.</summary>
    public void SpawnAmbulance(float carX, float playerSpeed)
    {
        if (ambulance != null) { int old = objs.IndexOf(ambulance); if (old >= 0) RemoveAt(old); ambulance = null; }
        bool custom = cfg.ambulanceSprite != null;
        var o = MakeSpriteObj("Ambulance", Ambulance, custom ? cfg.ambulanceSprite : sprites.carBody);
        o.life = 16f;
        o.halfW = 1.1f;
        o.x = Mathf.Clamp(carX, -1.2f, 1.2f);                 // 플레이어가 서 있는 차선으로 달려온다
        o.z = -60f;
        o.speed = Mathf.Max(8f, playerSpeed) + cfg.ambulanceSpeedAdd;     // 플레이어보다 빠르게 앞지른다
        o.body.color = custom ? Color.white : new Color(0.97f, 0.97f, 1f);
        if (!custom)
        {
            o.t.localScale = new Vector3(1.15f, 1.25f, 1f);
            o.centerY = -o.body.sprite.bounds.min.y * 1.25f;
            var dgo = new GameObject("Detail");
            dgo.transform.SetParent(o.t, false);
            dgo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            o.detail = dgo.AddComponent<SpriteRenderer>();
            o.detail.sprite = sprites.carDetail;
            var cgo = new GameObject("Cross");               // 붉은 십자
            cgo.transform.SetParent(o.t, false);
            cgo.transform.localPosition = new Vector3(0f, 0.22f, -0.03f);
            var csr = cgo.AddComponent<SpriteRenderer>();
            csr.sprite = sprites.cross;
        }
        Bounds sb = o.body.sprite.bounds;
        o.lamps = new SpriteRenderer[2];
        for (int k = 0; k < 2; k++)
        {
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(o.t, false);
            lgo.transform.localPosition = new Vector3((k == 0 ? -0.3f : 0.3f) * sb.extents.x, sb.max.y - 0.04f, -0.04f);
            lgo.transform.localScale = Vector3.one * 1.5f;
            var lsr = lgo.AddComponent<SpriteRenderer>();
            lsr.sprite = sprites.lamp;
            lsr.color = k == 0 ? new Color(1f, 0.1f, 0.1f) : new Color(0.25f, 0.45f, 1f);
            o.lamps[k] = lsr;
        }
        ambulance = o;
    }

    // ---- 돼지 떼 / 라이벌 택시 (랜덤 인카운트) ----

    /// <summary>돼지 떼 사건 중이면 돼지가 줄줄이 도로를 건넌다.</summary>
    public void TickPigs(float elapsed, bool active, bool turning, float spawnZ)
    {
        if (!active || turning || elapsed < nextPig) return;
        nextPig = elapsed + 0.6f;
        float side = Random.value < 0.5f ? -1f : 1f;
        var o = MakeSpriteObj("Pig", Pig, cfg.pigSprite != null ? cfg.pigSprite : sprites.pig);
        o.halfW = 0.45f;
        o.dir = -side;
        o.rollSpeed = Random.Range(2.5f, 4.5f);
        o.x = side * (cfg.roadHalfWidth + 0.4f);
        o.z = Mathf.Min(spawnZ - 6f, Random.Range(26f, 60f));
        o.body.flipX = o.dir < 0f;                           // 그림은 오른쪽을 본다
        var pigArt = cfg.pigSprite == null ? PixelArt.Random("pig") : null;
        if (pigArt != null)
        {
            o.art = pigArt;
            o.body.sprite = pigArt.First;
            o.centerY = -o.body.sprite.bounds.min.y;
        }
    }

    void SetRivalAlpha(Obj o, float a)
    {
        var c = o.baseColor;
        c.a = a;
        o.body.color = c;
        if (o.detail != null) o.detail.color = new Color(1f, 1f, 1f, a);
        if (o.lamps != null)
            for (int k = 0; k < o.lamps.Length; k++)
            {
                var lc = o.lamps[k].color;
                lc.a = a;
                o.lamps[k].color = lc;
            }
    }

    /// <summary>라이벌 택시: 길가의 손님(target) 바로 앞에 갑자기 소환되어 손님을 가로채고 도망친다.</summary>
    public void SpawnRival(PedestrianManager.Ped target)
    {
        if (target == null) return;
        rivalTarget = target;
        float side = target.x >= 0f ? 1f : -1f;
        bool useCustom = cfg.carSprite != null;
        var o = MakeSpriteObj("RivalTaxi", Rival, useCustom ? cfg.carSprite : sprites.carBody);
        o.x = side * 2.6f;
        o.z = Mathf.Clamp(target.z - 2f, 10f, 38f);           // 손님 바로 앞(플레이어 쪽)에 나타난다
        o.halfW = useCustom ? Mathf.Max(0.5f, cfg.carSprite.bounds.extents.x * 0.85f) : 1.0f;
        o.hp = 2;
        o.stage = 0;
        o.speed = 0f;
        o.baseColor = useCustom ? Color.white : new Color(1f, 0.82f, 0.1f);
        o.body.color = new Color(o.baseColor.r, o.baseColor.g, o.baseColor.b, 0f);
        SpawnBoom(o.x, o.z);                                  // 소환 연기
        if (!useCustom)
        {
            var dgo = new GameObject("Detail");
            dgo.transform.SetParent(o.t, false);
            dgo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            o.detail = dgo.AddComponent<SpriteRenderer>();
            o.detail.sprite = sprites.carDetail;
        }
        var lgo = new GameObject("Sign");                    // 지붕 TAXI 표시등
        lgo.transform.SetParent(o.t, false);
        lgo.transform.localPosition = new Vector3(0f, o.body.sprite.bounds.max.y - 0.04f, -0.04f);
        lgo.transform.localScale = Vector3.one * 1.6f;
        var lsr = lgo.AddComponent<SpriteRenderer>();
        lsr.sprite = sprites.lamp;
        lsr.color = new Color(1f, 1f, 0.8f, 0f);
        o.lamps = new[] { lsr };
        SetRivalAlpha(o, 0f);
    }

    // ---- 경찰 바리케이드 (은행 강도 손님을 태우고 도망칠 때) ----

    public void StartPolice(float elapsed) { nextBlock = elapsed + 3f; }

    public void ClearPolice()
    {
        for (int i = objs.Count - 1; i >= 0; i--) if (objs[i].kind == Police) RemoveAt(i);
    }

    /// <summary>도망 중이면 가끔 경찰차 두 대가 길의 한쪽을 막는다 (반대쪽 차선으로 빠져나가야 한다).</summary>
    public void TickPolice(float elapsed, bool active, bool turning, float spawnZ)
    {
        if (!active || turning || elapsed < nextBlock) return;
        if (!SlotFree(spawnZ)) { nextBlock = elapsed + 0.6f; return; }
        float side = Random.value < 0.5f ? -1f : 1f;
        float z = spawnZ - 4f;
        SpawnPolice(side * 1.2f, z);
        SpawnPolice(side * 3.0f, z + 0.5f);
        nextBlock = elapsed + Mathf.Max(2f, cfg.roadblockInterval) * Random.Range(0.8f, 1.3f);
    }

    void SpawnPolice(float x, float z)
    {
        var o = new Obj { kind = Police, halfW = 0.95f, x = x, z = z, speed = 0f, phase = Random.value * 6f };
        bool useCustom = cfg.carSprite != null;
        Sprite bodySprite = useCustom ? cfg.carSprite : sprites.carBody;
        Bounds sb = bodySprite.bounds;
        o.centerY = -sb.min.y;
        if (useCustom) o.halfW = Mathf.Max(0.5f, sb.extents.x * 0.85f);       // 그림 폭에 맞춘다
        var go = new GameObject("Police");
        go.transform.SetParent(parent, false);
        o.t = go.transform;
        o.body = go.AddComponent<SpriteRenderer>();
        var policeArt = useCustom ? null : PixelArt.Get("police");
        if (policeArt != null)
        {
            // 옆으로 선 경찰차(경광등이 번갈아 켜진다)로 길을 막는다
            o.art = policeArt;
            bodySprite = policeArt.First;
            sb = bodySprite.bounds;
            o.centerY = -sb.min.y;
            o.halfW = Mathf.Max(0.5f, sb.extents.x * 0.8f);
            o.body.color = Color.white;
            useCustom = true;
        }
        o.body.sprite = bodySprite;
        if (policeArt == null) o.body.color = useCustom ? Color.white : new Color(0.14f, 0.22f, 0.6f);
        if (!useCustom)
        {
            var dgo = new GameObject("Detail");
            dgo.transform.SetParent(go.transform, false);
            dgo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            o.detail = dgo.AddComponent<SpriteRenderer>();
            o.detail.sprite = sprites.carDetail;
        }
        o.lamps = new SpriteRenderer[policeArt != null ? 0 : 2];
        for (int k = 0; k < o.lamps.Length; k++)
        {
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3((k == 0 ? -0.3f : 0.3f) * sb.extents.x, sb.max.y - 0.06f, -0.04f);
            var lsr = lgo.AddComponent<SpriteRenderer>();
            lsr.sprite = sprites.lamp;
            lsr.color = k == 0 ? new Color(1f, 0.1f, 0.1f) : new Color(0.25f, 0.45f, 1f);
            lgo.transform.localScale = Vector3.one * 1.4f;
            o.lamps[k] = lsr;
        }
        objs.Add(o);
    }

    // ---- 미니건 ----

    /// <summary>작은 폭발 (총에 맞은 자리).</summary>
    public void SpawnBoom(float x, float z)
    {
        var o = MakeSpriteObj("Boom", Boom, cfg.flameSprite != null ? cfg.flameSprite : sprites.flame);
        o.x = x;
        o.z = z;
        o.life = 0.35f;
    }

    /// <summary>총에 맞았다: 차/콘/공은 날아가 쓰러지고 불길은 꺼진다. (괴수와 장식은 맞지 않는다)</summary>
    public void Shoot(Obj o)
    {
        SpawnBoom(o.x, o.z);
        if (o.kind == Flame) { o.life = 0f; o.fallT = 0f; o.hitDone = true; return; }       // 같은 불길을 두 번 처치하지 않게
        o.hitDone = true;
        o.fallT = 0f;
        o.kickDir = Random.value < 0.5f ? -1f : 1f;
    }

    // 스프라이트 하나짜리 물체를 만들어 목록에 넣는다 (바닥이 도로에 닿게 높이를 맞춘다)
    Obj MakeSpriteObj(string name, int kind, Sprite sp)
    {
        var o = new Obj { kind = kind, phase = Random.value * 6f };
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        o.t = go.transform;
        o.body = go.AddComponent<SpriteRenderer>();
        o.body.sprite = sp;
        o.centerY = -sp.bounds.min.y;
        objs.Add(o);
        return o;
    }

    // ---- 괴수 ----

    /// <summary>괴수 사건이 진행 중이면, 다음 교차로 뒤에 괴수를 세운다 (한 번에 한 마리).</summary>
    public void TickKaiju(float elapsed, float speed, bool active, float nextInterDist, float period, bool turning, float spawnZ)
    {
        if (!active || turning || elapsed < nextKaiju || HasKaiju) return;
        if (nextInterDist < Mathf.Min(Mathf.Max(28f, speed * 2.2f), 70f)) return;     // 교차로가 너무 가까우면(약 2초 안) 돌 시간이 없으니 다음 교차로를 기다린다
        float zk = nextInterDist + 16f;                         // 교차로 바로 뒤에 선다. 도로가 보이는 거리(spawnZ) 밖이어도 먼 곳에 거대한 모습이 보인다

        var o = MakeSpriteObj("Kaiju", Kaiju, cfg.kaijuSprite != null ? cfg.kaijuSprite : sprites.kaiju);
        var kaijuArt = cfg.kaijuSprite == null ? PixelArt.Get("kaiju") : null;
        if (kaijuArt != null)
        {
            o.art = kaijuArt;
            o.body.sprite = kaijuArt.First;
            o.centerY = -o.body.sprite.bounds.min.y;
        }
        o.halfW = 4.5f;                                         // 도로 전체를 막는다
        o.x = 0f;
        o.z = zk;

        // 입에서 뿜는 불
        var fgo = new GameObject("Breath");
        fgo.transform.SetParent(o.t, false);
        var b = o.body.sprite.bounds;
        fgo.transform.localPosition = new Vector3(0f, b.center.y + b.extents.y * 0.50f, -0.05f);
        var fsr = fgo.AddComponent<SpriteRenderer>();
        fsr.sprite = cfg.flameSprite != null ? cfg.flameSprite : sprites.flame;
        fsr.enabled = kaijuArt == null;                          // 새 그림(지옥의 거인)은 입에서 불을 뿜지 않는다
        o.lamps = new[] { fsr };

        // 무너진 건물에서 피어오르는 연기 기둥
        int n = Random.Range(4, 7);
        for (int i = 0; i < n; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            SpawnSmoke(side * (cfg.roadHalfWidth + Random.Range(1.5f, 4.5f)), zk + Random.Range(-14f, 8f), 40f);
        }
        nextKaiju = elapsed + 8f;
        if (KaijuSpawned != null) KaijuSpawned();
    }

    void SpawnSmoke(float x, float z, float life)
    {
        var o = MakeSpriteObj("Smoke", Smoke, cfg.smokeSprite != null ? cfg.smokeSprite : sprites.smoke);
        o.x = x;
        o.z = z;
        o.life = life;
    }

    // ---- 히어로 레이저 ----

    // 앞쪽에 차나 장애물이 있으면 레이저를 쏘지 않는다 (불길이 한쪽을 막았을 때 반대쪽도 막혀 피할 수 없게 되는 것을 막는다)
    bool RoadAheadClear()
    {
        for (int i = 0; i < objs.Count; i++)
        {
            var o = objs[i];
            if (o.kind == Ball || o.kind == Smoke || o.kind == Flame || o.kind == Boom) continue;
            if (o.z > 4f && o.z < 85f) return false;
        }
        return true;
    }

    /// <summary>히어로 사건이 진행 중이면, 가끔 한쪽 차선을 레이저로 쓸어 불길을 남긴다.</summary>
    public void TickHero(float dt, float elapsed, float advance, bool active, bool turning, float spawnZ)
    {
        if (!active || turning)
        {
            StrikeState = 0;
            return;
        }
        switch (StrikeState)
        {
            case 0:
                if (elapsed < nextStrike) break;
                if (!RoadAheadClear()) { nextStrike = elapsed + 0.5f; break; }
                StrikeSide = Random.value < 0.5f ? -1f : 1f;
                StrikeZ = Mathf.Min(spawnZ - 4f, 62f);
                strikeT = 0.9f;
                StrikeState = 1;
                break;
            case 1:
                strikeT -= dt;                                   // 경고 빛은 앞쪽 한 곳에 머문다 (달리는 속도와 상관없이 같은 반응 시간)
                if (strikeT <= 0f)
                {
                    StrikeState = 2;
                    flameAcc = 0f;
                    if (LaserFired != null) LaserFired();
                }
                break;
            default:
                float step = 48f * dt;
                StrikeZ -= advance + step;
                flameAcc += step;
                while (flameAcc >= 4f)
                {
                    flameAcc -= 4f;
                    SpawnFlame(StrikeSide, StrikeZ + flameAcc);
                }
                if (StrikeZ <= 18f)
                {
                    StrikeState = 0;
                    nextStrike = elapsed + Random.Range(cfg.laserInterval, cfg.laserInterval + 3f);
                }
                break;
        }
    }

    void SpawnFlame(float side, float z)
    {
        var o = MakeSpriteObj("Flame", Flame, cfg.flameSprite != null ? cfg.flameSprite : sprites.flame);
        o.halfW = 1.2f;
        o.x = side * 2f;
        o.z = z;
        o.life = Mathf.Max(2f, cfg.laserFlameLife);
    }
}
