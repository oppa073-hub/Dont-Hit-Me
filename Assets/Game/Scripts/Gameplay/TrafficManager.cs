using System.Collections.Generic;
using UnityEngine;

// 교통/돌발 상황 매니저: 도로 위의 차(승용차, 트럭, 고장 난 차)와 장애물(공사 콘, 굴러오는 공)을 만들고 움직이고 충돌/아슬아슬 판정을 한다.
// 점수, 목숨, 연출 같은 "게임 결과"는 모른다. 부딪혔을 때(Hit)와 아슬아슬하게 지나갔을 때(NearMiss)를 이벤트로 알리고 DriveGame 이 처리한다.
// 굴러오는 공은 닿아도 아무 일 없지만, 잠시 뒤 같은 쪽 인도에서 아이가 튀어나온다 (보행자 매니저에 부탁해서 만든다).
// 설정 값은 씬에 저장된 DriveGame 의 public 필드를 읽는다.
public class TrafficSprites
{
    public Sprite carBody, carDetail, cone, ball, lamp;
    public Sprite pig, cross;
    public Sprite kaiju, flame, smoke, hero, villain;       // 날짜 사건용 (괴수, 불길, 연기 기둥, 히어로, 빌런)
}

public partial class TrafficManager
{
    public const int Sedan = 0, Truck = 1, Broken = 2, Cones = 3, Ball = 4, Kaiju = 5, Flame = 6, Smoke = 7, Boom = 8, Police = 9, Rival = 10, Pig = 11, Ambulance = 12;

    public class Obj
    {
        public Transform t;
        public SpriteRenderer body, detail;
        public SpriteRenderer[] lamps;      // 고장 난 차의 비상등
        public int kind;
        public float x, z;
        public float speed;                 // 스스로 앞으로 가는 속도 (m/s)
        public float halfW;                 // 좌우 반폭 (충돌 판정)
        public float centerY;               // 스프라이트 중심 높이
        public float minLat = 999f;
        public bool scored, hitDone;
        public float fallT = -1f, kickDir;  // 부딪힌 뒤 옆으로 밀려나는 연출
        public float angle;                 // 공이 구르는 각도
        public float dir, rollSpeed, side;  // 공: 굴러가는 방향/속도, 출발한 인도 쪽
        public float childAt = -1f;         // 공: 이 시각(elapsed)에 아이가 튀어나온다
        public float life = -1f;            // 불길/연기: 남은 시간(초)
        public int hp, stage;               // 라이벌 택시: 체력(2), 단계(0 손님을 태우는 중, 1 도망 중, 2 격파됨)
        public float age, hitCool;          // 라이벌 택시: 나타난 뒤 시간, 박치기 재사용 대기
        public Color baseColor = Color.white;
        public float phase;                 // 흔들림/깜빡임이 물체마다 다르게 보이게 하는 값
        public PixelSet art;                // 외부 픽셀 아트 애니메이션 (돼지, 경찰차, 괴수). 없으면 예전 그림
    }

    readonly DriveGame cfg;
    readonly Transform parent;
    readonly TrafficSprites sprites;
    readonly PedestrianManager peds;
    readonly List<Obj> objs = new List<Obj>();
    float nextVehicle, nextHazard, lastElapsed;

    /// <summary>현재 난이도 레벨 (DriveGame 이 알려 준다). 레벨이 낮으면 아무것도 안 나온다.</summary>
    public int Level = 1;

    /// <summary>부딪혔다. 차는 옆으로 밀려나는 연출을 매니저가 하고, 목숨/속도는 받는 쪽이 처리한다.</summary>
    public event System.Action<Obj> Hit;
    /// <summary>아슬아슬하게 지나갔다 (차만).</summary>
    public event System.Action<Obj> NearMiss;

    public TrafficManager(DriveGame cfg, Transform parent, PedestrianManager peds, TrafficSprites sprites)
    {
        this.cfg = cfg;
        this.parent = parent;
        this.peds = peds;
        this.sprites = sprites;
    }

    public Obj this[int i] { get { return objs[i]; } }
    public int Count { get { return objs.Count; } }

    public void Reset()
    {
        Clear();
        lastElapsed = 0f;
        nextVehicle = 4f;
        nextHazard = 6f;
        nextKaiju = 0f;
        nextStrike = 3f;
        nextPig = 0f;
        rivalTarget = null;
    }

    public void Clear()
    {
        foreach (var o in objs) if (o.t != null) Object.Destroy(o.t.gameObject);
        objs.Clear();
        StrikeState = 0;
        nextKaiju = Mathf.Max(nextKaiju, lastElapsed + 6f);
        nextStrike = Mathf.Max(nextStrike, lastElapsed + 3f);
        nextBlock = Mathf.Max(nextBlock, lastElapsed + 2.5f);
        nextPig = Mathf.Max(nextPig, lastElapsed + 1f);
        rivalTarget = null;
        ambulance = null;          // 회전 직후에는 경찰 바리케이드도 잠깐 쉰다
        nextVehicle = Mathf.Max(nextVehicle, lastElapsed + 3f);       // 회전 직후에는 잠깐 비워 둔다
        nextHazard = Mathf.Max(nextHazard, lastElapsed + 4f);
    }

    void RemoveAt(int i)
    {
        if (objs[i].t != null) Object.Destroy(objs[i].t.gameObject);
        objs.RemoveAt(i);
    }

    float Interval(float baseInterval, int startLevel)
    {
        return Mathf.Max(cfg.minTrafficInterval, baseInterval - 0.6f * (Level - startLevel));
    }

    // 길을 막는 물건(공 말고)이 너무 가깝게 연달아 나오지 않게, 새로 나올 자리가 비었는지 본다
    bool SlotFree(float spawnZ)
    {
        int blockers = 0;
        for (int i = 0; i < objs.Count; i++)
        {
            if (objs[i].kind == Ball || objs[i].kind == Smoke || objs[i].kind == Boom || objs[i].kind == Pig || objs[i].kind == Rival || objs[i].kind == Ambulance) continue;
            blockers++;
            if (objs[i].z > spawnZ - 35f) return false;
        }
        return blockers < cfg.maxTraffic;
    }

    /// <summary>
    /// 한 프레임 진행. advance = 이번 프레임에 월드가 차 쪽으로 다가온 거리(m), carX = 차의 좌우 위치,
    /// turning = 교차로 회전 중(충돌 판정을 끈다), spawnZ = 새로 나타나는 거리.
    /// </summary>
    public void Tick(float dt, float elapsed, float advance, float carX, bool turning, float spawnZ)
    {
        lastElapsed = elapsed;

        if (!turning && Level >= cfg.vehicleStartLevel && elapsed >= nextVehicle)
        {
            if (SlotFree(spawnZ))
            {
                SpawnVehicle(spawnZ);
                nextVehicle = elapsed + Interval(cfg.vehicleInterval, cfg.vehicleStartLevel) * Random.Range(0.7f, 1.3f);
            }
            else nextVehicle = elapsed + 0.7f;
        }
        if (!turning && Level >= cfg.hazardStartLevel && elapsed >= nextHazard)
        {
            SpawnHazard(spawnZ);
            nextHazard = elapsed + Interval(cfg.hazardInterval, cfg.hazardStartLevel) * Random.Range(0.7f, 1.3f);
        }

        bool lampOn = ((int)(elapsed * 3f) & 1) == 0;

        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var o = objs[i];
            o.z += -advance + o.speed * dt;

            if (o.kind == Broken && o.lamps != null)
                for (int k = 0; k < o.lamps.Length; k++) o.lamps[k].enabled = lampOn;
            else if (o.kind == Police && o.lamps != null)
            {
                o.lamps[0].enabled = lampOn;            // 경광등: 빨강/파랑이 번갈아 번쩍인다
                o.lamps[1].enabled = !lampOn;
            }

            if (o.kind == Kaiju)
            {
                o.x = Mathf.Sin(elapsed * 0.7f + o.phase) * 1.2f;            // 괴수가 좌우로 느릿하게 움직인다
                if (o.lamps != null && o.lamps.Length > 0)
                {
                    float fk = 2.6f + 0.6f * Mathf.Sin(elapsed * 11f);       // 입에서 나오는 불이 일렁인다
                    o.lamps[0].transform.localScale = new Vector3(fk, fk * 1.2f, 1f);
                }
            }
            else if (o.kind == Smoke || o.kind == Boom)
            {
                o.life -= dt;
                if (o.life <= 0f || o.z < -6f) RemoveAt(i);
                continue;
            }
            else if (o.kind == Flame)
            {
                o.life -= dt;
                if (o.life <= 0f) { RemoveAt(i); continue; }
            }

            if (o.kind == Ambulance)
            {
                // 구급차: 뒤에서 달려와 플레이어를 앞질러 간다. 지나치는 순간 플레이어가 길을 막고 있었는지 한 번 본다.
                o.life -= dt;
                if (o.life <= 0f) { if (ambulance == o) ambulance = null; RemoveAt(i); continue; }       // 끝내 못 앞지르면 사라진다
                o.speed = (dt > 0.0001f ? advance / dt : 0f) + cfg.ambulanceSpeedAdd;                    // 플레이어 속도가 변해도 항상 같은 빠르기로 따라잡는다
                if (!o.scored && o.z >= -1f)
                {
                    o.scored = true;
                    bool blocked = Mathf.Abs(o.x - carX) < 1.0f + o.halfW + 0.15f;
                    if (!turning && AmbulancePassed != null) AmbulancePassed(blocked);                   // 회전 중에는 판정하지 않는다
                }
                if (o.lamps != null && o.lamps.Length > 1)
                {
                    o.lamps[0].enabled = lampOn;            // 경광등: 빨강/파랑 번갈아
                    o.lamps[1].enabled = !lampOn;
                }
                if (o.z > spawnZ + 40f) { if (ambulance == o) ambulance = null; RemoveAt(i); }
                continue;
            }

            if (o.kind == Rival)
            {
                // 라이벌 택시: 손님 앞에 갑자기 나타나 손님을 가로채고 도망친다. 플레이어가 박치기로 체력 2를 깎으면 격파(보상).
                // 박치기를 해도 플레이어는 다치지 않는다. 도망쳐서 멀어지면 흐려지며 사라진다.
                o.age += dt;
                if (o.stage == 2)
                {
                    o.fallT += dt;
                    o.x += o.kickDir * 5f * dt;                                 // 격파되어 옆으로 날아간다
                    SetRivalAlpha(o, Mathf.Clamp01(1f - o.fallT));
                    if (o.fallT > 1f || o.z < -6f) RemoveAt(i);
                    continue;
                }
                if (o.hitCool > 0f) o.hitCool -= dt;
                float pv = dt > 0.0001f ? advance / dt : 0f;                    // 플레이어(월드)가 다가오는 속도
                float alpha = Mathf.Clamp01(o.age / 0.25f);                     // 소환될 때 순식간에 나타난다
                if (o.stage == 0)
                {
                    o.speed = 0f;                                               // 연석에 서서 손님을 태운다
                    if (o.age >= 0.9f || o.z < -3f)                             // 0.9초가 지났거나 플레이어가 지나쳐 버렸다
                    {
                        o.stage = 1;
                        if (rivalTarget != null && peds.Contains(rivalTarget))
                        {
                            var tgt = rivalTarget;
                            rivalTarget = null;
                            if (RivalArrived != null) RivalArrived(tgt);
                        }
                        rivalTarget = null;
                        o.speed = 5f;
                    }
                }
                else
                {
                    // 라이벌 택시는 아주 느리게 도망친다 (플레이어 속도와 상관없이 5 -> 12 m/s). 박치기할 시간이 충분하다.
                    o.speed = Mathf.Lerp(5f, 12f, Mathf.Clamp01((o.age - 0.9f) / 10f));
                    float far = Mathf.Clamp01((spawnZ + 5f - o.z) / 25f);       // 멀어지면 자연스럽게 흐려져 사라진다
                    alpha = Mathf.Min(alpha, far);
                    if (far <= 0.02f) { RemoveAt(i); continue; }
                }
                SetRivalAlpha(o, alpha);

                if (!turning && o.hitCool <= 0f && o.z > -4f && o.z < 5f && Mathf.Abs(o.x - carX) < DriveGame.CarHalfWidth + o.halfW)
                {
                    o.hitCool = 0.35f;
                    o.hp--;
                    o.z += 4f;                                                  // 맞고 앞으로 튕겨 나간다 (플레이어도 넉백으로 느려져서 두 번째 박치기를 노릴 수 있다)
                    float knockDir = o.x >= carX ? 1f : -1f;
                    if (o.hp <= 0)
                    {
                        o.stage = 2;
                        o.fallT = 0f;
                        o.kickDir = o.x >= carX ? 1f : -1f;
                        SpawnBoom(o.x, o.z);
                        if (RivalDestroyed != null) RivalDestroyed();
                    }
                    else if (RivalHit != null) RivalHit(o.hp, knockDir);
                    continue;
                }
                if (o.z < -6f) RemoveAt(i);
                continue;
            }

            if (o.fallT >= 0f)
            {
                o.fallT += dt;
                o.x += o.kickDir * 5f * dt;
                if (o.fallT > 1f || o.z < -6f) RemoveAt(i);
                continue;
            }

            if (o.kind == Pig)
            {
                // 돼지: 도로를 가로질러 뛴다 (닿으면 덜컹, 목숨은 안 깎임)
                o.x += o.dir * o.rollSpeed * dt;
                if (o.dir * o.x > cfg.roadHalfWidth + 3f) { RemoveAt(i); continue; }
            }
            else if (o.kind == Ball)
            {
                o.x += o.dir * o.rollSpeed * dt;
                o.angle -= o.dir * 420f * dt;
                if (o.childAt >= 0f && elapsed >= o.childAt)
                {
                    o.childAt = -1f;
                    var kid = peds.Spawn(spawnZ, 2, o.z, o.side);     // 공을 쫓아 같은 쪽 인도에서 아이가 튀어나온다
                    kid.triggerZ = o.z + 10f;
                    kid.waitTime = 0.15f;
                    kid.triggerT = 0f;
                }
                if (o.dir * o.x > cfg.roadHalfWidth + 3f || o.z < -6f) RemoveAt(i);
                continue;
            }

            bool inZone = o.z > -0.6f && o.z < 2.4f;
            float hitDist = DriveGame.CarHalfWidth + o.halfW;
            if (inZone)
            {
                if (!turning)
                {
                    float lat = Mathf.Abs(o.x - carX);
                    o.minLat = Mathf.Min(o.minLat, lat);
                    if (lat < hitDist && !o.hitDone)
                    {
                        o.hitDone = true;
                        o.fallT = 0f;
                        o.kickDir = o.x >= carX ? 1f : -1f;
                        if (Hit != null) Hit(o);
                        if (o.kind == Kaiju || o.kind == Flame) RemoveAt(i);       // 괴수는 물러나고 불길은 꺼진다
                        continue;
                    }
                }
            }
            else if (o.z <= -0.6f && !o.scored)
            {
                o.scored = true;
                bool vehicle = o.kind == Sedan || o.kind == Truck || o.kind == Broken || o.kind == Police;
                if (vehicle && o.minLat < hitDist + cfg.vehicleNearMissMargin && NearMiss != null) NearMiss(o);
            }

            if (o.z < -6f || (o.z > spawnZ + 40f && o.kind != Kaiju)) RemoveAt(i);          // 괴수는 먼 곳에 서 있어도 지우지 않는다
        }
    }

    // ---- 만들기 ----

    void SpawnVehicle(float spawnZ)
    {
        int start = cfg.vehicleStartLevel;
        float r = Random.value;
        int kind = Sedan;
        if (Level >= start + 2 && r < 0.2f) kind = Broken;             // 고장 난 차: 서 있고 비상등이 깜빡인다
        else if (Level >= start + 1 && r < 0.5f) kind = Truck;         // 트럭: 더 크고 느리다

        float side = Random.value < 0.5f ? -1f : 1f;
        var o = new Obj { kind = kind, z = spawnZ - Random.Range(0f, 4f) };
        Color color;
        float sx = 1f, sy = 1f;
        switch (kind)
        {
            case Truck:
                o.halfW = 1.2f; sx = 1.35f; sy = 1.9f;
                o.x = side * Random.Range(1.4f, 2.2f);
                o.speed = Random.Range(cfg.vehicleSpeed.x, cfg.vehicleSpeed.y) * 0.8f;
                color = Random.value < 0.5f ? new Color(0.85f, 0.85f, 0.8f) : new Color(0.3f, 0.45f, 0.7f);
                break;
            case Broken:
                o.halfW = 0.9f;
                o.x = side * Random.Range(1.2f, 2.0f);
                o.speed = 0f;
                color = new Color(0.55f, 0.5f, 0.42f);
                break;
            default:
                o.halfW = 0.9f;
                o.x = side * Random.Range(1.2f, 2.0f);
                o.speed = Random.Range(cfg.vehicleSpeed.x, cfg.vehicleSpeed.y);
                color = Random.ColorHSV(0f, 1f, 0.35f, 0.7f, 0.5f, 0.9f);
                break;
        }

        // 인스펙터에 넣은 스프라이트가 있으면 그걸 그대로 쓴다 (색칠/크기 조절 없음). 없으면 임시로 코드로 그린 그림.
        Sprite custom = kind == Truck ? (cfg.truckSprite != null ? cfg.truckSprite : cfg.carSprite) : cfg.carSprite;
        if (custom == null) custom = CarArt.Rear(CarArt.RandomIndex());      // 외부 도트 자동차 그림(뒷모습)이 있으면 그걸 쓴다
        bool useCustom = custom != null;
        if (useCustom)
        {
            // 트럭 전용 그림이 없으면 차 그림을 키워서 쓴다 (찌그러지지 않게 가로세로 같은 비율)
            if (kind == Truck) { sx = sy = cfg.truckSprite != null ? 1f : 1.3f; }
            else { sx = sy = 1f; }
        }
        Sprite bodySprite = useCustom ? custom : sprites.carBody;
        Bounds sb = bodySprite.bounds;
        o.centerY = -sb.min.y * sy;                                        // pivot 이 어디든 바닥이 도로에 닿게
        if (useCustom) o.halfW = Mathf.Max(0.5f, sb.extents.x * sx * 0.85f);     // 충돌 폭은 그림 폭에 맞춘다

        var go = new GameObject(kind == Truck ? "Truck" : "Car");
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(sx, sy, 1f);
        o.t = go.transform;
        o.body = go.AddComponent<SpriteRenderer>();
        o.body.sprite = bodySprite;
        o.body.color = useCustom ? Color.white : color;

        if (!useCustom)
        {
            var dgo = new GameObject("Detail");
            dgo.transform.SetParent(go.transform, false);
            dgo.transform.localPosition = new Vector3(0f, 0f, -0.02f);        // 몸통보다 살짝 카메라 쪽
            o.detail = dgo.AddComponent<SpriteRenderer>();
            o.detail.sprite = sprites.carDetail;
        }

        if (kind == Broken)
        {
            o.lamps = new SpriteRenderer[2];
            for (int k = 0; k < 2; k++)
            {
                var lgo = new GameObject("Lamp");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = new Vector3((k == 0 ? -0.7f : 0.7f) * sb.extents.x, sb.center.y - sb.extents.y * 0.2f, -0.04f);
                var lsr = lgo.AddComponent<SpriteRenderer>();
                lsr.sprite = sprites.lamp;
                lsr.color = new Color(1f, 0.6f, 0.05f);
                o.lamps[k] = lsr;
            }
        }
        objs.Add(o);
    }

    void SpawnHazard(float spawnZ)
    {
        bool cones = Level >= cfg.hazardStartLevel + 1 && Random.value < 0.5f && SlotFree(spawnZ);
        if (cones)
        {
            var o = new Obj { kind = Cones, halfW = 0.8f, centerY = 0.4f, speed = 0f };
            o.x = Random.Range(-2.6f, 2.6f);
            o.z = spawnZ - Random.Range(0f, 4f);
            var go = new GameObject("Cones");
            go.transform.SetParent(parent, false);
            o.t = go.transform;
            o.body = go.AddComponent<SpriteRenderer>();
            o.body.sprite = cfg.coneSprite != null ? cfg.coneSprite : sprites.cone;
            o.centerY = -o.body.sprite.bounds.min.y;
            objs.Add(o);
        }
        else
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            var o = new Obj { kind = Ball, halfW = 0.25f, centerY = 0.25f, speed = 0f };
            o.side = side;
            o.dir = -side;
            o.rollSpeed = Random.Range(4f, 6f);
            o.x = side * (cfg.roadHalfWidth + 0.4f);
            o.z = Mathf.Min(spawnZ - 5f, Random.Range(35f, 55f));
            o.childAt = lastElapsed + 0.35f;                           // 공이 나온 직후 같은 쪽에서 아이가 뒤따라 나온다
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            o.t = go.transform;
            o.body = go.AddComponent<SpriteRenderer>();
            o.body.sprite = cfg.ballSprite != null ? cfg.ballSprite : sprites.ball;
            o.centerY = o.body.sprite.bounds.extents.y;     // 공은 구르니까 pivot 을 Center 로 둘 것
            objs.Add(o);
        }
    }
}
