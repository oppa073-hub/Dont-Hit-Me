using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 월드 표현: 도로 구간과 건물을 매 프레임 배치하고 회전 연출을 그린다
public partial class DriveGame
{
    class Side
    {
        public SpriteRenderer upper, ground, cornice, door, prop;          // 도로 쪽 정면
        public SpriteRenderer fUpper, fGround, fCornice;                   // 다가오는 쪽(-z) 면
        public int floors = 3, pal;
        public float setback;
        public float propScale = 1f;
        public bool wantDoor, wantProp;   // 이 건물이 문/소품을 가졌는지 (가까울 때만 실제로 켠다)
        public bool far;                  // 마지막으로 적용한 원거리 단순화 상태
    }

    class Strip
    {
        public Transform road, swL, swR, dash;
        public Transform[] bldg = new Transform[2];   // 건물 몸통(상자). 면마다 스프라이트를 붙인다
        public Side[] sides = { new Side(), new Side() };
        public int lastIdx = int.MinValue;
    }

    readonly Strip[] strips = new Strip[StripCount];

    Material roadA, roadB, swA, swB, dashMat, darkMat;

    Material[] bodyMats;

    // 도시 타일 스프라이트
    Sprite[] winTiles = new Sprite[4], doorTiles = new Sprite[4], shopTiles = new Sprite[3];

    Sprite corniceTile, lampSprite, lightRedSprite, lightGreenSprite, trashSprite, mailSprite;

    bool hasCity;

    // 구간을 그릴 때 Place 가 적용하는 회전 (UpdateWorldVisuals 가 구간마다 설정)
    bool rendRot;

    float rendTheta, rendPivotZ;

    // 건물 해시용: 지금 그리는 구간이 속한 도로의 월드 기준. (rendNodeX,Y)는 교차로 번호 rendNodeK 의 격자 좌표, rendHead 는 그 도로의 진행 방향.
    int rendNodeX, rendNodeY, rendNodeK, rendHead;

    float SpawnZ => Mathf.Min(StripCount * StripLen - 12f, Mathf.Sqrt(camHeight / curvature) + Mathf.Sqrt(1.75f / curvature) + 8f);

    float Drop(float z) => -curvature * z * z;

    // ---------- 월드 표현 ----------

    void UpdateWorldVisuals()
    {
        float frac = dist % StripLen;
        int baseIdx = Mathf.FloorToInt(dist / StripLen);
        float rw = roadHalfWidth;

        // 회전 중: 옛 도로(A)는 -turnDir * 각도, 새 도로(B)는 turnDir * (90 - 각도) 만큼 교차로 중심 둘레로 돌린다.
        // 두 회전이 끝에서 만나면(각도 90) 새 도로가 정면을 향하고 옛 도로는 옆 도로가 된다.
        int e = Mathf.Max(intersectionLen + 1, intersectionEvery);
        float thetaA = -turnDir * turnAlpha;
        float thetaB = turnDir * (90f - turnAlpha);
        float pivotZ = turnCenterRoad - (dist + 8f);                       // 교차로 중심이 차 앞 몇 m 에 있는가
        int farStartIdx = turnInterIdx + intersectionLen;                  // 옛 도로에서 교차로를 벗어난 첫 구간
        int rebaseIdx = e * RebaseBlocks;
        float rebaseCenter = rebaseIdx * StripLen + intersectionLen * StripLen * 0.5f;
        float distB = rebaseCenter - pivotZ - 8f;                          // 새 도로 쪽 dist (중심이 pivotZ 앞에 오도록)

        // 건물 해시의 월드 기준: 앞으로 만날 교차로 N 의 격자 좌표와 번호 (회전 중에는 회전하는 교차로)
        int kNextCur = turning ? turnInterIdx / e : KNextAt(dist);
        int nodeX = gx + HeadX[heading], nodeY = gy + HeadY[heading];
        int headB = (heading + turnDir + 4) & 3;                           // 회전 뒤 새 도로의 방향

        for (int i = 0; i < StripCount; i++)
        {
            var s = strips[i];
            int idxA = baseIdx + i;
            int idx = idxA;
            float zc = i * StripLen - 8f - frac + StripLen * 0.5f;

            bool roleB = false;
            int hideSide = -1;
            if (turning && retargeted)
            {
                if (idxA >= farStartIdx)
                {
                    // 교차로 너머의 구간은 새 도로의 구간으로 쓴다 (번호는 회전이 끝난 뒤 도로 번호와 같다)
                    roleB = true;
                    idx = rebaseIdx + intersectionLen + (idxA - farStartIdx);
                    zc = idx * StripLen + StripLen * 0.5f - distB - 8f;
                }
                else if (idxA < turnInterIdx)
                {
                    // 옛 도로의 접근 구간은 땅으로 남기되, 새 도로 건물과 겹치는 회전 쪽 건물만 끈다
                    hideSide = turnDir > 0 ? 1 : 0;
                }
            }
            rendRot = turning;
            rendTheta = roleB ? thetaB : thetaA;
            rendPivotZ = pivotZ;
            rendNodeX = nodeX;
            rendNodeY = nodeY;
            rendNodeK = roleB ? RebaseBlocks : kNextCur;                   // 새 도로의 회전 교차로 번호는 RebaseBlocks (idx 도 그 기준으로 옮겨 있다)
            rendHead = roleB ? headB : heading;

            float drop = Drop(zc);
            float pitch = Mathf.Atan(2f * curvature * zc) * Mathf.Rad2Deg;
            var pitchRot = Quaternion.Euler(pitch, 0f, 0f);
            float len = StripLen * 1.08f;
            bool even = (idx & 1) == 0;
            bool inter = IsIntersection(idx);

            Place(s.road, new Vector3(0f, drop - 0.1f, zc), pitchRot, new Vector3(rw * 2f, 0.2f, len));
            if (inter)
            {
                // 교차로: 인도 박스를 가로 도로 아스팔트로 쓰고, 중앙선 대신 횡단보도 줄을 하나 둔다
                float cx = rw + crossStreetLength * 0.5f;
                // 장면 교체 뒤에는 새 도로 쪽 가로 도로 팔을 끈다 (새 도로 구간과 같은 자리라 겹쳐 깜빡인다)
                bool armOff = turning && retargeted && !roleB;
                s.swL.gameObject.SetActive(!(armOff && turnDir < 0));
                s.swR.gameObject.SetActive(!(armOff && turnDir > 0));
                Place(s.swL, new Vector3(-cx, drop - 0.1f, zc), pitchRot, new Vector3(crossStreetLength, 0.2f, len));
                Place(s.swR, new Vector3(cx, drop - 0.1f, zc), pitchRot, new Vector3(crossStreetLength, 0.2f, len));
                s.dash.gameObject.SetActive(true);
                Place(s.dash, new Vector3(0f, drop + 0.03f, zc - StripLen * 0.5f + 0.5f), pitchRot, new Vector3(rw * 2f, 0.02f, 0.5f));
            }
            else
            {
                s.swL.gameObject.SetActive(true);
                s.swR.gameObject.SetActive(true);
                Place(s.swL, new Vector3(-(rw + 2.5f), drop + 0.05f, zc), pitchRot, new Vector3(5f, 0.3f, len));
                Place(s.swR, new Vector3(rw + 2.5f, drop + 0.05f, zc), pitchRot, new Vector3(5f, 0.3f, len));
                s.dash.gameObject.SetActive(even);
                Place(s.dash, new Vector3(0f, drop + 0.02f, zc), pitchRot, new Vector3(0.18f, 0.02f, StripLen * 0.5f));
            }

            for (int side = 0; side < 2; side++)
            {
                if (side == hideSide) HideSide(s, side);
                else UpdateBuilding(s, s.sides[side], side, idx, zc, drop, pitchRot, rw);
            }

            if (s.lastIdx != idx)
            {
                s.lastIdx = idx;
                s.road.GetComponent<Renderer>().sharedMaterial = even ? roadA : roadB;
                var sm = inter ? roadA : (even ? swA : swB);
                s.swL.GetComponent<Renderer>().sharedMaterial = sm;
                s.swR.GetComponent<Renderer>().sharedMaterial = sm;
            }
        }

        // 목적지 표지 기둥
        if (destMarker != null)
        {
            // 목적지가 지금 달리는 도로 위(r==0)에 있을 때만 보인다. 회전 중에는 옛 도로 위에 있는 경우(회전하는 교차로 자체)만, 장면 교체 전까지.
            DestRel(out int mf, out int mr, out float mz);
            bool show = taxi.State == TaxiState.Carrying && mr == 0 && mz > -25f && mz < SpawnZ
                        && (!turning || (!retargeted && mf < 0));
            if (destMarker.gameObject.activeSelf != show) destMarker.gameObject.SetActive(show);
            rendRot = turning;
            rendTheta = thetaA;
            rendPivotZ = pivotZ;
            if (show) Place(destMarker, new Vector3(roadHalfWidth + 0.8f, Drop(mz) + 3f, mz), Quaternion.identity, new Vector3(0.4f, 6f, 0.4f));
        }

        for (int pi = 0; pi < peds.Count; pi++)
        {
            var p = peds[pi];
            bool fallen = p.fallT >= 0f;
            float bob = (!fallen && p.pauseLeft <= 0f) ? Mathf.Sin((elapsed + p.phase) * 10f) * 0.05f : 0f;
            float h = fallen ? 0.35f : 0.875f + bob;
            float px = p.x, pz = p.z;
            if (turning)
            {
                // 보행자도 옛 도로와 함께 돈다 (장면 교체 뒤에는 새 도로와 맞지 않으므로 숨긴다)
                float rad = thetaA * Mathf.Deg2Rad;
                float dz = pz - pivotZ;
                float nx = px * Mathf.Cos(rad) + dz * Mathf.Sin(rad);
                float nz = -px * Mathf.Sin(rad) + dz * Mathf.Cos(rad);
                px = nx;
                pz = pivotZ + nz;
            }
            p.sr.enabled = !(turning && retargeted);
            p.t.position = new Vector3(px, Drop(pz) + h, pz);
            // 옆 시점으로 손님을 볼 때는 손님이 카메라를 향하게 돌린다 (평소 보행자는 정면 고정)
            float faceYaw = (taxi.State == TaxiState.Talking && p == taxi.Caller) ? camYaw : 0f;
            p.t.rotation = Quaternion.Euler(0f, faceYaw, fallen ? -p.dir * 85f : 0f);
            if (p.pauseLeft > 0f) p.sr.color = Color.Lerp(p.baseColor, Color.white, 0.5f);
            else if (!fallen) p.sr.color = p.baseColor;
        }

        // 다른 차와 돌발 장애물 (사람처럼 정면 고정 스프라이트)
        bool trafficVisible = !(turning && retargeted);
        for (int ti = 0; ti < traffic.Count; ti++)
        {
            var o = traffic[ti];
            if (o.t.gameObject.activeSelf != trafficVisible) o.t.gameObject.SetActive(trafficVisible);
            if (!trafficVisible) continue;
            float ox = o.x, oz = o.z;
            if (turning)
            {
                // 옛 도로와 함께 돈다
                float rad = thetaA * Mathf.Deg2Rad;
                float dz = oz - pivotZ;
                float nx = ox * Mathf.Cos(rad) + dz * Mathf.Sin(rad);
                float nz = -ox * Mathf.Sin(rad) + dz * Mathf.Cos(rad);
                ox = nx;
                oz = pivotZ + nz;
            }
            float sy = 1f;
            if (o.kind == TrafficManager.Kaiju)
            {
                // 괴수는 숨 쉬듯 크기가 조금씩 변한다
                sy = 1f + 0.04f * Mathf.Sin(elapsed * 4f + o.phase);
                o.t.localScale = new Vector3(1f + 0.03f * Mathf.Sin(elapsed * 3f + o.phase), sy, 1f);
            }
            else if (o.kind == TrafficManager.Flame)
            {
                // 불길은 일렁이고 꺼지기 직전에 작아진다
                sy = (1f + 0.2f * Mathf.Sin(elapsed * 17f + o.phase)) * Mathf.Min(1f, o.life);
                o.t.localScale = new Vector3(0.9f + 0.15f * Mathf.Sin(elapsed * 13f + o.phase), sy, 1f);
            }
            else if (o.kind == TrafficManager.Smoke)
            {
                sy = 1f + 0.05f * Mathf.Sin(elapsed * 3f + o.phase);
                o.t.localScale = new Vector3(1f, sy, 1f);
            }
            o.t.position = new Vector3(ox, Drop(oz) + o.centerY * sy, oz);
            float tilt = o.angle;                                           // 공: 구르는 각도
            if (o.fallT >= 0f) tilt = -o.kickDir * Mathf.Min(o.fallT * 4f, 1f) * 35f;   // 부딪힌 차/콘: 옆으로 기울며 밀려난다
            o.t.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        UpdateBattleVisuals();
    }

    // 건물 = 상자 몸통 + 각 면에 붙인 스프라이트. 도로 쪽 정면과 다가오는 쪽(-z) 면은 월드에 고정(카메라를 쳐다보지 않음).
    void UpdateBuilding(Strip s, Side sd, int sideIdx, int idx, float zc, float drop, Quaternion pitchRot, float rw)
    {
        float sgn = sideIdx == 0 ? -1f : 1f;
        float ts = TileScale;
        const float depth = 6f;

        bool changed = s.lastIdx != idx;
        bool inter = IsIntersection(idx);
        if (changed)
        {
            // 교차로 구간에는 건물이 없다 (구간이 바뀔 때만 켜고 끈다)
            s.bldg[sideIdx].gameObject.SetActive(!inter);
            if (hasCity)
            {
                sd.upper.gameObject.SetActive(!inter);
                sd.ground.gameObject.SetActive(!inter);
                if (inter)
                {
                    sd.cornice.gameObject.SetActive(false);
                    sd.fUpper.gameObject.SetActive(false);
                    sd.fGround.gameObject.SetActive(false);
                    sd.fCornice.gameObject.SetActive(false);
                    sd.door.gameObject.SetActive(false);
                    sd.prop.gameObject.SetActive(false);
                }
            }
        }
        if (inter) return;

        // 해시용 번호는 도로 구간의 월드 좌표로 정한다: 같은 길은 어느 방향/어느 때 지나도 같은 건물이 보인다.
        // 구간(두 교차로 사이)을 양의 방향(북/동) 기준으로 통일하고, 반대 방향으로 달릴 때는 구간 안 위치와 좌우를 뒤집는다.
        int e = Mathf.Max(intersectionLen + 1, intersectionEvery);
        int k = idx / e;                                              // idx 는 항상 0 이상
        int j = idx - k * e;                                          // 이 구간이 교차로에서 몇 번째 조각인지
        int dk = k - rendNodeK;
        int cx0 = rendNodeX + HeadX[rendHead] * dk;                   // 이 도로 구간이 시작하는 교차로(진행 방향 기준)
        int cy0 = rendNodeY + HeadY[rendHead] * dk;
        int jc = j, sideC = sideIdx;
        if (rendHead >= 2)
        {
            cx0 += HeadX[rendHead];                                   // 반대편 교차로가 양의 방향 기준 시작점이다
            cy0 += HeadY[rendHead];
            jc = e + intersectionLen - 1 - j;
            sideC = 1 - sideIdx;
        }
        int segKey = unchecked((cx0 * 73856093) ^ (cy0 * 19349663) ^ ((rendHead & 1) * 83492791)) & 0xFFFFF;
        int stride = (e + 2) / 3 * 3;                                 // e 이상인 3의 배수 (기본 20 → 21). 구간끼리 번호가 겹치지 않고 3구간 묶음이 맞게 정렬
        int hidx = segKey * stride + jc;
        if (changed)
        {
            int run = hidx / 3;                      // 3구간(12m)이 한 건물
            int seed = run * 2 + sideC;
            sd.pal = Mathf.Min(3, Mathf.FloorToInt(Hash(seed * 13 + 1) * 4f));
            sd.floors = 3 + Mathf.FloorToInt(Hash(seed * 17 + 2) * 5f);
            sd.setback = Hash(seed * 29 + 5) * 1.5f;  // 건물마다 앞뒤로 들쑥날쑥 -> 옆면이 보여서 입체감
            s.bldg[sideIdx].GetComponent<Renderer>().sharedMaterial = bodyMats[sd.pal];

            if (hasCity)
            {
                int shop = Mathf.Min(2, Mathf.FloorToInt(Hash(hidx * 19 + sideC) * 3f));
                sd.upper.sprite = winTiles[sd.pal];
                sd.upper.size = new Vector2(TileCols, sd.floors * 2f);
                sd.ground.sprite = shopTiles[shop];
                sd.ground.size = new Vector2(TileCols, 2f);
                sd.cornice.sprite = corniceTile;
                sd.cornice.size = new Vector2(TileCols, 1f);

                int fc = Mathf.RoundToInt(depth / (ts * TileW));
                sd.fUpper.sprite = winTiles[sd.pal];
                sd.fUpper.size = new Vector2(fc, sd.floors * 2f);
                sd.fGround.sprite = shopTiles[shop];
                sd.fGround.size = new Vector2(fc, 2f);
                sd.fCornice.sprite = corniceTile;
                sd.fCornice.size = new Vector2(fc, 1f);

                sd.door.sprite = doorTiles[sd.pal];
                sd.wantDoor = ((hidx % 3) + 3) % 3 == 1;

                int kind = 0;
                if (((hidx + sideC * 2) % 4 + 4) % 4 == 0) kind = 1;
                else if (Hash(hidx * 3 + sideC * 7) < 0.12f) kind = 2;
                else if (Hash(hidx * 5 + sideC * 11) < 0.15f) kind = 3;
                else if (Hash(hidx * 7 + sideC * 13) < 0.10f) kind = 4;
                sd.wantProp = kind != 0;
                sd.prop.flipX = sideIdx == 0;   // 가로등 머리가 도로 쪽을 향하게
                switch (kind)
                {
                    case 1: sd.prop.sprite = lampSprite; sd.propScale = 2f; break;
                    case 2: sd.prop.sprite = Hash(hidx * 23 + sideC) < 0.5f ? lightRedSprite : lightGreenSprite; sd.propScale = 1.6f; break;
                    case 3: sd.prop.sprite = trashSprite; sd.propScale = 1.2f; break;
                    case 4: sd.prop.sprite = mailSprite; sd.propScale = 1.2f; break;
                }
            }
        }

        float H = (3f + sd.floors * 2f) * ts;                 // 1층(2) + 윗층(2*층수) + 지붕 장식(1)
        float faceX = sgn * (rw + 5f + sd.setback);
        float cx = faceX + sgn * depth * 0.5f;
        float baseY = drop + 0.2f;

        Place(s.bldg[sideIdx], new Vector3(cx, baseY, zc) + pitchRot * new Vector3(0f, H * 0.5f, 0f), pitchRot, new Vector3(depth, H, StripLen));
        if (!hasCity) return;

        // 원거리 단순화: 멀리 있는 건물은 벽 두 장(윗층/1층)만 남기고 나머지는 끈다. 상태가 바뀔 때만 SetActive.
        bool far = lodDistance > 0f && zc > lodDistance;
        if (changed || sd.far != far)
        {
            sd.far = far;
            bool near = !far;
            sd.cornice.gameObject.SetActive(near);
            sd.fUpper.gameObject.SetActive(near);
            sd.fGround.gameObject.SetActive(near);
            sd.fCornice.gameObject.SetActive(near);
            sd.door.gameObject.SetActive(near && sd.wantDoor);
            sd.prop.gameObject.SetActive(near && sd.wantProp);
        }

        // 도로 쪽 정면 (법선이 +-x)
        var faceRot = pitchRot * Quaternion.Euler(0f, sgn * 90f, 0f);
        Vector3 basePos = new Vector3(faceX - sgn * 0.02f, baseY, zc);
        Place(sd.ground.transform, basePos, faceRot, Vector3.one * ts);
        Place(sd.upper.transform, basePos + faceRot * new Vector3(0f, 2f * ts, 0f), faceRot, Vector3.one * ts);
        if (far) return;
        Place(sd.cornice.transform, basePos + faceRot * new Vector3(0f, (2f + sd.floors * 2f) * ts, 0f), faceRot, Vector3.one * ts);
        if (sd.door.gameObject.activeSelf)
            Place(sd.door.transform, basePos + faceRot * new Vector3(-(TileCols - 1) * 0.5f * ts * TileW, 0f, -0.02f), faceRot, Vector3.one * ts);

        // 다가오는 쪽(-z) 면: 옆 건물보다 높거나 튀어나온 부분의 단면이 보인다
        Vector3 fBase = new Vector3(cx, baseY, zc) + pitchRot * new Vector3(0f, 0f, -StripLen * 0.5f - 0.02f);
        Place(sd.fGround.transform, fBase, pitchRot, Vector3.one * ts);
        Place(sd.fUpper.transform, fBase + pitchRot * new Vector3(0f, 2f * ts, 0f), pitchRot, Vector3.one * ts);
        Place(sd.fCornice.transform, fBase + pitchRot * new Vector3(0f, (2f + sd.floors * 2f) * ts, 0f), pitchRot, Vector3.one * ts);

        if (sd.prop.gameObject.activeSelf)
        {
            // 소품은 비스듬히 세워서 정면에서도 보이게 (그래도 플레이어를 쳐다보진 않음)
            var propRot = pitchRot * Quaternion.Euler(0f, sgn * 50f, 0f);
            Place(sd.prop.transform, new Vector3(sgn * (rw + 0.7f), drop + 0.2f, zc), propRot, Vector3.one * sd.propScale);
        }
    }

    // 월드 오브젝트를 놓는다. 회전 중이면(rendRot) 교차로 중심 둘레로 rendTheta 만큼 돌리고,
    // 도로 휨(y = -curvature z^2)은 돌린 뒤의 앞쪽 거리 기준으로 다시 계산해서 도로가 항상 앞으로 휘어 내려가게 한다.
    void Place(Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        if (rendRot)
        {
            float zOld = pos.z;
            float rad = rendTheta * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            float dz = zOld - rendPivotZ;
            float nx = pos.x * c + dz * sn;
            float nz = -pos.x * sn + dz * c;
            float zNew = rendPivotZ + nz;
            pos = new Vector3(nx, pos.y - Drop(zOld) + Drop(zNew), zNew);
            rot = PitchQ(zNew) * Quaternion.Euler(0f, rendTheta, 0f) * Quaternion.Inverse(PitchQ(zOld)) * rot;
        }
        t.SetPositionAndRotation(pos, rot);
        t.localScale = scale;
    }

    Quaternion PitchQ(float z)
    {
        return Quaternion.Euler(Mathf.Atan(2f * curvature * z) * Mathf.Rad2Deg, 0f, 0f);
    }

    // 한쪽 건물(몸통 + 스프라이트)을 모두 끈다. 회전 중 새 도로 건물과 겹치는 옛 건물에 쓴다.
    // 다시 켜는 건 구간이 다시 구성될 때(lastIdx 초기화) UpdateBuilding 이 한다.
    void HideSide(Strip s, int side)
    {
        s.bldg[side].gameObject.SetActive(false);
        if (!hasCity) return;
        var sd = s.sides[side];
        sd.upper.gameObject.SetActive(false);
        sd.ground.gameObject.SetActive(false);
        sd.cornice.gameObject.SetActive(false);
        sd.fUpper.gameObject.SetActive(false);
        sd.fGround.gameObject.SetActive(false);
        sd.fCornice.gameObject.SetActive(false);
        sd.door.gameObject.SetActive(false);
        sd.prop.gameObject.SetActive(false);
    }

    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xFFFF) / 65535f;
        }
    }
}
