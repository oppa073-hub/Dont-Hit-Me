using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 보행자: 생성, 이동, 도로 횡단, 충돌과 니어미스
public partial class DriveGame
{
    class Ped
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
        public bool caller;               // 택시를 부르는 손님 (머리 위에 !)
    }

    readonly List<Ped> peds = new List<Ped>();

    Sprite pedSprite;

    // 새 사람 한 명을 인도에 만든다. kind 를 -1 로 주면 비율대로 무작위 종류를 고른다.
    Ped SpawnPed(int kind = -1, float z = -1f)
    {
        if (kind < 0)
        {
            float r = Random.value;
            kind = r < sidewalkerRatio ? 0 : (r < sidewalkerRatio + farCrossRatio ? 1 : 2);
        }
        if (z < 0f) z = SpawnZ + Random.Range(0f, 10f);

        float side = Random.value < 0.5f ? -1f : 1f;
        var go = new GameObject("Ped");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pedSprite;
        Color c = Random.ColorHSV(0f, 1f, 0.5f, 0.7f, 0.8f, 1f);
        sr.color = c;

        float offMax = kind == 2 ? Mathf.Min(sidewalkOffset.y, 2f) : sidewalkOffset.y;   // 튀어나올 사람은 연석 가까이
        float off = Random.Range(Mathf.Min(sidewalkOffset.x, offMax), offMax);
        var p = new Ped
        {
            t = go.transform,
            sr = sr,
            kind = kind,
            side = side,
            dir = -side,
            x = side * (roadHalfWidth + off),
            z = z,
            curbX = side * (roadHalfWidth + 0.35f),
            walkZ = Random.Range(sidewalkWalkSpeed.x, sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f),
            walkSpeed = kind == 2 ? Random.Range(dartSpeed.x, dartSpeed.y) : Random.Range(crossSpeed.x, crossSpeed.y),
            triggerZ = kind == 1 ? Random.Range(farTrigger.x, farTrigger.y) : Random.Range(dartTrigger.x, dartTrigger.y),
            waitTime = kind == 2 ? Random.Range(curbWait.x, curbWait.y) : 0f,
            baseColor = c,
            phase = Random.value * 6f
        };
        peds.Add(p);
        return p;
    }

    void UpdatePeds(float dt)
    {
        for (int i = peds.Count - 1; i >= 0; i--)
        {
            var p = peds[i];
            p.z -= turning ? worldMove : speed * dt;      // 회전 중에는 도로가 멈춰 있으니 사람만 앞으로 미끄러지지 않게

            if (p.fallT >= 0f)
            {
                p.fallT += dt;
                p.x += p.dir * 3f * dt;
                if (p.fallT > 1.2f || p.z < -6f)
                {
                    Remove(i);
                    continue;
                }
            }
            else
            {
                UpdatePedBehavior(p, dt);

                bool inZone = p.z > -0.6f && p.z < 2.4f;
                float lat = Mathf.Abs(p.x - carX);
                // 충돌과 니어미스는 도로를 건너는 중(st == 3)인 사람만. 인도를 걷는 사람은 가까이 지나가도 점수/충돌 없음
                if (inZone)
                {
                    if (p.st == 3 && !turning)       // 회전 중에는 화면과 판정 좌표가 달라서 충돌/니어미스를 끈다
                    {
                        p.minLat = Mathf.Min(p.minLat, lat);
                        if (lat < CarHalfWidth + PedHalfWidth)
                        {
                            OnHit(p);
                            continue;
                        }
                    }
                }
                else if (p.z <= -0.6f && !p.scored)
                {
                    p.scored = true;
                    if (p.minLat < nearMissDist)
                    {
                        nearMisses++;
                        score += nearMissBonus;
                        Popup("NEAR MISS +" + nearMissBonus, new Color(0.4f, 1f, 0.6f));
                        shakeUntil = Time.unscaledTime + 0.08f;
                    }
                }

            }

            if (p.z < -6f || p.z > SpawnZ + 40f) Remove(i);
        }
    }

    // 인도 걷기 → (트리거 거리 안이면) 연석으로 이동 → 멈칫 → 도로 건너기 → 건너편 인도에서 다시 걷기
    void UpdatePedBehavior(Ped p, float dt)
    {
        switch (p.st)
        {
            case 0: // 인도를 따라 걷는다
                p.z += p.walkZ * dt;
                if (p.kind != 0 && p.z < p.triggerZ && p.z > 0f) p.st = 1;
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
                if (p.dir * p.x > roadHalfWidth + 0.6f)
                {
                    // 건너편 인도에 올라서면 그쪽 인도 보행자가 된다
                    p.st = 0;
                    p.kind = 0;
                    p.side = p.dir;
                    p.dir = -p.side;
                    p.walkZ = Random.Range(sidewalkWalkSpeed.x, sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
                }
                break;
        }
    }

    void Remove(int i)
    {
        if (peds[i].t != null) Destroy(peds[i].t.gameObject);
        peds.RemoveAt(i);
    }

    void OnHit(Ped p)
    {
        p.fallT = 0f;
        p.sr.color = new Color(1f, 0.4f, 0.4f);
        hits++;
        speed *= hitSpeedFactor;
        hitStopUntil = Time.unscaledTime + hitStop;
        shakeUntil = Time.unscaledTime + 0.3f;

        // 손님을 태운 채 사람을 치면 손님이 반응하고 요금이 깎인다
        if (taxi == TaxiState.Carrying)
        {
            reactText = PickOne(pax != null ? pax.react : PaxTypes[0].react);
            reactUntil = Time.unscaledTime + 1.6f;
            float sense = pax != null ? pax.hitSense : 1f;
            float hitMul = Mathf.Clamp01(1f - (1f - hitFareMul) * sense);    // 유형이 민감할수록 더 깎는다
            fareMul = Mathf.Max(0.2f, fareMul * hitMul);
        }
        if (invuln > 0f) return;

        lives--;
        invuln = invulnTime;
        hurtFlash = 0.45f;
        Popup("BONK!", new Color(1f, 0.4f, 0.4f));
        if (lives <= 0)
        {
            lives = 0;
            gameOver = true;
        }
    }
}
