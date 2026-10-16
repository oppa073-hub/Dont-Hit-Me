using System.Collections.Generic;
using UnityEngine;

// 히어로와 빌런의 하늘 전투 연출: 하늘에 떠서 싸우는 두 인물, 히어로의 눈에서 나오는 레이저(경고 빛 -> 굵은 빛으로 차선을 쓸어 옴), 빌런이 쏘는 보라색 빛.
// 불길이라는 실제 장애물은 TrafficManager(TickHero)가 만들고, 여기서는 보이는 것만 맡는다.
public partial class DriveGame
{
    SpriteRenderer heroSr, villainSr;
    Transform beamHero, beamVillain;
    float heroX;

    void BuildBattle()
    {
        heroSr = MakeSR("Hero", 6);
        heroSr.sprite = heroSprite != null ? heroSprite : trafficSprites.hero;
        villainSr = MakeSR("Villain", 6);
        villainSr.sprite = villainSprite != null ? villainSprite : trafficSprites.villain;
        villainSr.flipX = true;
        heroSr.gameObject.SetActive(false);
        villainSr.gameObject.SetActive(false);

        beamHero = Block("HeroBeam");
        beamHero.GetComponent<Renderer>().sharedMaterial = Unlit(new Color(1f, 0.25f, 0.2f));
        beamHero.gameObject.SetActive(false);
        beamVillain = Block("VillainBeam");
        beamVillain.GetComponent<Renderer>().sharedMaterial = Unlit(new Color(0.75f, 0.3f, 1f));
        alienBeamMat = Unlit(new Color(0.45f, 1f, 0.55f));         // 외계인 손님의 UFO 빛줄기
        beamVillain.gameObject.SetActive(false);
    }

    static void PlaceBeam(Transform beam, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) { beam.gameObject.SetActive(false); return; }
        if (!beam.gameObject.activeSelf) beam.gameObject.SetActive(true);
        beam.position = a + d * 0.5f;
        beam.rotation = Quaternion.LookRotation(d);
        beam.localScale = new Vector3(thickness, thickness, len);
    }

    // 매 프레임 (UpdateWorldVisuals 끝에서): 전투 중에만 보인다. 교차로를 돌 때는 숨긴다.
    void UpdateBattleVisuals()
    {
        bool show = dayEvents.HeroActive && !turning;
        if (heroSr.gameObject.activeSelf != show)
        {
            heroSr.gameObject.SetActive(show);
            villainSr.gameObject.SetActive(show);
        }
        if (!show)
        {
            if (beamHero.gameObject.activeSelf) beamHero.gameObject.SetActive(false);
            if (beamVillain.gameObject.activeSelf) beamVillain.gameObject.SetActive(false);
            return;
        }

        float t = elapsed;
        int st = traffic.StrikeState;
        float wantX = st != 0 ? traffic.StrikeSide * 5f : -9f + 4f * Mathf.Sin(t * 0.9f);
        heroX = Mathf.MoveTowards(heroX, wantX, 14f * Time.deltaTime);
        Vector3 hp = new Vector3(heroX, 26f + 2f * Mathf.Sin(t * 1.7f), 78f);
        Vector3 vp = new Vector3(9f + 4f * Mathf.Sin(t * 0.9f + 2f), 29f + 2f * Mathf.Sin(t * 1.3f + 1f), 84f + 5f * Mathf.Cos(t * 0.7f));
        heroSr.transform.position = hp;
        villainSr.transform.position = vp;

        // 히어로의 눈 레이저: 경고 때는 얇고, 쓸어 올 때는 굵다
        if (st != 0)
        {
            float sz = Mathf.Max(5f, traffic.StrikeZ);
            Vector3 target = new Vector3(traffic.StrikeSide * 2f, Drop(sz) + 0.3f, sz);
            PlaceBeam(beamHero, hp + new Vector3(0f, 1.3f, 0f), target, st == 1 ? 0.08f : 0.5f);
        }
        else if (beamHero.gameObject.activeSelf) beamHero.gameObject.SetActive(false);

        // 빌런이 히어로에게 쏘는 보라색 빛 (2.4초마다 잠깐)
        if (Mathf.Repeat(t, 2.4f) < 0.35f) PlaceBeam(beamVillain, vp + new Vector3(0f, 1.3f, 0f), hp, 0.35f);
        else if (beamVillain.gameObject.activeSelf) beamVillain.gameObject.SetActive(false);
    }
}
