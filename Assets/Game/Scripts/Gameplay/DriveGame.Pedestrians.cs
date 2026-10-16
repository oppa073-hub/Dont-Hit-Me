using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 보행자 연결부: 보행자 자체(생성, 이동, 횡단, 판정)는 PedestrianManager 가 하고, 여기서는 그 결과를 게임에 반영한다.
public partial class DriveGame
{
    PedestrianManager peds;

    // 차에 치였다: 속도 감소, 히트스톱, 흔들림, 손님 리액션/요금 감액, 목숨. (쓰러지는 연출은 매니저가 한다)
    void OnPedHit(PedestrianManager.Ped p)
    {
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

    // 아슬아슬하게 피했다: 니어미스 보너스
    void OnPedNearMiss(PedestrianManager.Ped p)
    {
        nearMisses++;
        score += nearMissBonus;
        Popup("NEAR MISS +" + nearMissBonus, new Color(0.4f, 1f, 0.6f));
        shakeUntil = Time.unscaledTime + 0.08f;
    }
}
