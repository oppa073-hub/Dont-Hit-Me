using System.Collections.Generic;
using UnityEngine;

// 교통 매니저의 사건 부분: 괴수가 길을 막는 것과 히어로의 레이저가 한쪽 차선을 불태우는 것.
//  괴수: 다음 교차로 바로 뒤에 서서 도로 전체를 막는다 (교차로에서 돌아가야 한다). 부딪히면 목숨 -1 하고 물러난다. 길가에는 연기 기둥(무너진 건물)이 피어오른다.
//  레이저: 경고(얇은 빛) 뒤 한쪽 차선을 멀리서부터 쓸어 오며 불길을 남긴다. 반대쪽 차선으로 비켜 가면 안전하다.
public partial class TrafficManager
{
    float nextKaiju, nextStrike, strikeT, flameAcc, nextBlock;

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

    /// <summary>괴수, 불길, 연기를 모두 치운다 (사건이 끝났거나 시험용으로 바꿀 때).</summary>
    public void ClearEventObjects()
    {
        for (int i = objs.Count - 1; i >= 0; i--)
        {
            int k = objs[i].kind;
            if (k == Kaiju || k == Flame || k == Smoke || k == Boom || k == Police) RemoveAt(i);
        }
        StrikeState = 0;
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
        o.body.sprite = bodySprite;
        o.body.color = useCustom ? Color.white : new Color(0.14f, 0.22f, 0.6f);
        if (!useCustom)
        {
            var dgo = new GameObject("Detail");
            dgo.transform.SetParent(go.transform, false);
            dgo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            o.detail = dgo.AddComponent<SpriteRenderer>();
            o.detail.sprite = sprites.carDetail;
        }
        o.lamps = new SpriteRenderer[2];
        for (int k = 0; k < 2; k++)
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
        if (nextInterDist < Mathf.Max(28f, speed * 2.2f)) return;     // 교차로가 너무 가까우면(약 2초 안) 돌 시간이 없으니 다음 교차로를 기다린다
        float zk = nextInterDist + 16f;
        if (zk > spawnZ - 4f) return;                           // 아직 도로가 보이는 거리 밖이다

        var o = MakeSpriteObj("Kaiju", Kaiju, cfg.kaijuSprite != null ? cfg.kaijuSprite : sprites.kaiju);
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
