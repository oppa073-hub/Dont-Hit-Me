using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 교차로, 깜빡이 회전, 격자 좌표(gx, gy)와 방향(heading) 추적
public partial class DriveGame
{
    // 교차로 / 회전 상태
    int blinker;                         // -1 좌, 0 없음, +1 우

    int heading, gx, gy;                 // (gx,gy) = 방금 지난 교차로, heading = 진행 방향 (0 북, 1 동, 2 남, 3 서)

    float fade, nextInterDist;

    // 회전 연출: 월드를 교차로 중심 둘레로 회전시키고, 중간에 새 도로 구간으로 교체한 뒤, 끝나면 좌표를 다시 잡는다
    bool turning, retargeted;

    int turnDir, turnInterIdx, lastKNext = int.MinValue;

    float turnAlpha, turnCenterRoad, turnT, worldMove, retargetFadeT = -1f;

    static readonly int[] HeadX = { 0, 1, 0, -1 };

    static readonly int[] HeadY = { 1, 0, -1, 0 };

    static readonly string[] HeadNames = { "북", "동", "남", "서" };

    // ---------- 교차로 / 회전 ----------

    bool IsIntersection(int idx)
    {
        int e = Mathf.Max(intersectionLen + 1, intersectionEvery);
        int m = idx % e;
        if (m < 0) m += e;
        return m < intersectionLen;
    }

    float Period { get { return Mathf.Max(intersectionLen + 1, intersectionEvery) * StripLen; } }   // 교차로 간격(m)

    // 달린 거리 d 에서 앞으로 만날 교차로 번호. 교차로 갱신과 건물 해시가 같은 식을 쓰도록 한 곳에 둔다.
    int KNextAt(float d)
    {
        float len = intersectionLen * StripLen;
        return Mathf.CeilToInt(((d + 8f) - len * 0.5f) / Period);
    }

    // 회전: 깜빡이를 켜고 교차로 중심에 도착하면(turnCenterTolerance 안) 그 자리에서 turnDuration 동안 자동으로 돈다.
    // 월드를 교차로 중심 둘레로 0→90도 회전시키고(UpdateWorldVisuals), 중간에 짧게 어두워지는 순간 옛 도로 대신
    // 새 도로 구간으로 교체하고, 중심에 도착하면 좌표를 다시 잡아(CompleteTurn) 새 도로가 그냥 앞으로 이어지게 한다.
    void UpdateIntersection(float dt)
    {
        int e = Mathf.Max(intersectionLen + 1, intersectionEvery);
        float period = e * StripLen;
        float len = intersectionLen * StripLen;
        float roadPos = dist + 8f;                                   // 카메라(z=0)가 놓인 도로 좌표
        int kNext = KNextAt(dist);                                    // 앞으로 만날 교차로 번호
        float zC = kNext * period + len * 0.5f - roadPos;             // 그 교차로 중심까지 남은 거리
        nextInterDist = zC;

        if (!turning)
        {
            if (kNext != lastKNext)
            {
                // 교차로를 막 지났다
                if (lastKNext != int.MinValue)
                {
                    gx += HeadX[heading];
                    gy += HeadY[heading];
                    if (blinker != 0)
                    {
                        Popup("깜빡이를 더 일찍 켜세요", new Color(1f, 0.7f, 0.4f));
                        blinker = 0;
                    }
                }
                lastKNext = kNext;
            }

            // 프레임이 튀어도 중심을 건너뛰지 않도록, 이번 프레임에 달리는 거리의 2배까지는 시작 범위로 본다
            float startWindow = Mathf.Max(turnCenterTolerance, speed * dt * 2f);
            if (blinker != 0 && zC <= startWindow && zC > 0f && taxi != TaxiState.Talking)
            {
                turning = true;
                turnT = 0f;
                retargeted = false;
                retargetFadeT = -1f;
                turnDir = blinker;
                turnCenterRoad = roadPos + zC;
                turnInterIdx = kNext * e;                             // 이 교차로의 첫 구간 번호
                turnAlpha = 0f;
                blinker = 0;
                Popup(turnDir < 0 ? "좌회전" : "우회전", new Color(0.6f, 0.9f, 1f));
            }
            return;
        }

        // 회전 진행: 차가 교차로 중심에 도착한 뒤, turnDuration 동안 시간에 맞춰 0→90도 돈다
        turnT += dt;
        float p = Mathf.Clamp01(turnT / Mathf.Max(0.1f, turnDuration));
        turnAlpha = 90f * (p * p * (3f - 2f * p));

        // 장면 교체는 짧게 어두워지는 순간에 한다 (옛 도로의 가까운 건물과 새 도로의 건물이 겹치는 부분을 가린다)
        if (!retargeted && retargetFadeT == -1f && turnAlpha >= turnSwapAngle) retargetFadeT = 0f;
        if (retargetFadeT >= 0f)
        {
            float fadeTime = Mathf.Max(0.05f, swapFadeTime);
            retargetFadeT += Time.unscaledDeltaTime;
            fade = swapFadeDarkness * Mathf.Sin(Mathf.PI * Mathf.Clamp01(retargetFadeT / fadeTime));
            if (!retargeted && retargetFadeT >= fadeTime * 0.5f)
            {
                retargeted = true;
                foreach (var s in strips) s.lastIdx = int.MinValue;   // 새 도로 구간으로 다시 구성
            }
            if (retargetFadeT >= fadeTime)
            {
                retargetFadeT = -2f;
                fade = 0f;
            }
        }

        if (p >= 1f) CompleteTurn();
    }

    // 회전이 끝나는 순간: 새 도로가 앞으로 이어지도록 좌표를 옮기고, 방향/도시 해시를 바꾼다.
    void CompleteTurn()
    {
        int e = Mathf.Max(intersectionLen + 1, intersectionEvery);
        float period = e * StripLen;
        float len = intersectionLen * StripLen;

        gx += HeadX[heading];                                        // 이 교차로에 도착
        gy += HeadY[heading];
        heading = (heading + turnDir + 4) % 4;

        // 새 도로의 교차로 중심 = RebaseBlocks 번째 교차로 (장면 교체 때 새 도로 구간에 쓴 번호와 같다)
        float rebaseRoad = e * RebaseBlocks * StripLen;
        // 중심 '직후'에 놓는다: 그러면 다음 교차로가 곧바로 kNext 가 되어, (gx,gy)는 항상 "방금 지난 교차로"다
        dist = rebaseRoad + len * 0.5f + 0.01f - 8f;
        carX = lateralVel = 0f;

        turning = false;
        retargeted = false;
        turnAlpha = 0f;
        retargetFadeT = -1f;
        fade = 0f;
        lastKNext = Mathf.CeilToInt(((dist + 8f) - len * 0.5f) / period);
        nextInterDist = period - 0.01f;                              // 다음 프레임 UpdateTaxi 가 낡은 값(≈0)을 쓰지 않게 새 좌표 기준으로 맞춘다
        foreach (var s in strips) s.lastIdx = int.MinValue;
        invuln = Mathf.Max(invuln, postTurnInvuln);

        // 사람들은 새 도로 기준으로 다시 깐다 (가까운 곳은 비워서 멀리서부터 자연스럽게 나타나게)
        caller = null;
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
        peds.Respawn(SpawnZ);
    }
}
