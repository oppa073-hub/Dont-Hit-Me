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
        AddBlood();                                         // 앞유리에 피가 튄다 (분노 중이든 무적 중이든)
        if (rage.Active) { RageSmash(); return; }          // 분노 중에는 목숨/요금이 그대로
        speed *= hitSpeedFactor;
        hitStopUntil = Time.unscaledTime + hitStop;
        shakeUntil = Time.unscaledTime + 0.3f;

        sound.PlayBonk();
        taxi.OnPedHit();                      // 손님을 태운 채 사람을 치면 손님이 반응하고 요금이 깎인다
        if (session.RegisterHit())            // 무적이 아니면 목숨이 줄고, 마지막 목숨이면 게임오버
            Popup("쾅!", new Color(1f, 0.4f, 0.4f));
    }

    // 아슬아슬하게 피했다: 니어미스 보너스
    void OnPedNearMiss(PedestrianManager.Ped p)
    {
        session.RegisterNearMiss(nearMissBonus);
        sound.PlayNearMiss();
        Popup("아슬아슬!  +" + nearMissBonus, new Color(0.4f, 1f, 0.6f));
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.08f);
        rage.AddNearMiss();                   // 팝업 뒤에 불러야 분노가 발동할 때 RAGE 글자가 가려지지 않는다
    }
}
