using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 미니건 연결부: 택시 개조 3단계(지붕 양옆 미니건)를 사면, 분노 폭발 때 운전석 시점 양옆으로 미니건이 튀어나와 돌면서 불을 뿜는다.
// 쓰러뜨리는 계산은 MinigunManager 가 하고, 여기서는 3D 부품 만들기/움직이기, 소리, 점수를 맡는다.
public partial class DriveGame
{
    MinigunManager gun;
    Transform gunRootL, gunRootR, gunSpinL, gunSpinR, gunFlashL, gunFlashR, gunTracerL, gunTracerR;
    int gunKills, gunScoreSum;
    float gunSoundT;

    bool gunsOwned { get { return projects.Done(ProjectManager.Minigun); } }

    void CreateMinigun()
    {
        gun = new MinigunManager(this, peds, traffic);
        gun.Killed += OnGunKill;
    }

    // 미니건 한 정: 몸통 + 돌아가는 총열 세 개 + 총구 불꽃
    Transform BuildGun(string gunName, out Transform spin, out Transform flash)
    {
        var root = new GameObject(gunName).transform;
        root.SetParent(viewRoot, false);
        var metal = Mat(new Color(0.18f, 0.19f, 0.22f));
        var metal2 = Mat(new Color(0.40f, 0.42f, 0.47f));
        var housing = CabinPart("Housing", PrimitiveType.Cube, root, metal);
        housing.localPosition = new Vector3(0f, 0f, 0.10f);
        housing.localScale = new Vector3(0.11f, 0.10f, 0.30f);

        spin = new GameObject("Spin").transform;
        spin.SetParent(root, false);
        spin.localPosition = new Vector3(0f, 0f, 0.27f);
        for (int k = 0; k < 3; k++)
        {
            float a = k * 120f * Mathf.Deg2Rad;
            var b = CabinPart("Barrel", PrimitiveType.Cylinder, spin, metal2);
            b.localPosition = new Vector3(Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.03f, 0.18f);
            b.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.localScale = new Vector3(0.034f, 0.2f, 0.034f);
        }
        flash = CabinPart("Flash", PrimitiveType.Sphere, root, Unlit(new Color(1f, 0.85f, 0.3f)));
        flash.localPosition = new Vector3(0f, 0f, 0.68f);
        flash.localScale = Vector3.one * 0.1f;
        flash.gameObject.SetActive(false);
        root.gameObject.SetActive(false);
        return root;
    }

    void BuildMinigun()
    {
        gunRootL = BuildGun("GunL", out gunSpinL, out gunFlashL);
        gunRootR = BuildGun("GunR", out gunSpinR, out gunFlashR);
        gunTracerL = CabinPart("TracerL", PrimitiveType.Cube, viewRoot, Unlit(new Color(1f, 0.92f, 0.45f)));
        gunTracerR = CabinPart("TracerR", PrimitiveType.Cube, viewRoot, Unlit(new Color(1f, 0.92f, 0.45f)));
        gunTracerL.gameObject.SetActive(false);
        gunTracerR.gameObject.SetActive(false);
    }

    // 메인 루프에서 매 프레임: 분노 중이고 미니건을 달았으면 펼쳐서 쏜다
    void UpdateMinigun(float dt)
    {
        bool on = gunsOwned && rage.Active;
        bool wasOut = gun.Deploy > 0.02f;
        gun.Tick(dt, on, !turning);
        if (!wasOut && gun.Deploy > 0.02f) sound.PlayDeploy();
        if (gun.Firing)
        {
            gunSoundT -= dt;
            if (gunSoundT <= 0f)
            {
                gunSoundT = 0.06f;
                sound.PlayGun();
            }
        }
    }

    void OnGunKill()
    {
        session.AddScore(gunKillScore);
        gunKills++;
        gunScoreSum += gunKillScore;
    }

    // 운전석 시점에서 미니건의 위치와 움직임 (LateUpdate, 운전석이 보일 때)
    void UpdateMinigunView()
    {
        float d = gun.Deploy;
        bool show = d > 0.01f && gunsOwned;
        if (gunRootL.gameObject.activeSelf != show)
        {
            gunRootL.gameObject.SetActive(show);
            gunRootR.gameObject.SetActive(show);
        }
        if (!show)
        {
            if (gunTracerL.gameObject.activeSelf) gunTracerL.gameObject.SetActive(false);
            if (gunTracerR.gameObject.activeSelf) gunTracerR.gameObject.SetActive(false);
            return;
        }

        float e = d * d * (3f - 2f * d);                                          // 부드럽게 튀어나온다
        bool fire = gun.Firing && flow == GameFlow.Playing && !gameOver;        // 일시정지/게임오버에서는 불꽃이 남지 않게
        Vector3 recoil = fire ? new Vector3(Random.Range(-0.004f, 0.004f), Random.Range(-0.004f, 0.004f), -Random.Range(0f, 0.015f)) : Vector3.zero;
        gunRootL.localPosition = Vector3.Lerp(new Vector3(-0.40f, -0.62f, 0.75f), new Vector3(-0.58f, -0.26f, 0.80f), e) + recoil;
        gunRootR.localPosition = Vector3.Lerp(new Vector3(0.68f, -0.62f, 0.75f), new Vector3(0.86f, -0.26f, 0.80f), e) + recoil;
        gunRootL.localRotation = Quaternion.Euler(0f, 3f, 0f);
        gunRootR.localRotation = Quaternion.Euler(0f, -3f, 0f);
        float spinSpeed = fire ? 1100f : 200f * e;
        gunSpinL.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * spinSpeed);
        gunSpinR.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * spinSpeed);

        bool flashL = fire && Random.value > 0.35f, flashR = fire && Random.value > 0.35f;
        if (gunFlashL.gameObject.activeSelf != flashL) gunFlashL.gameObject.SetActive(flashL);
        if (gunFlashR.gameObject.activeSelf != flashR) gunFlashR.gameObject.SetActive(flashR);
        if (flashL) gunFlashL.localScale = Vector3.one * Random.Range(0.07f, 0.14f);
        if (flashR) gunFlashR.localScale = Vector3.one * Random.Range(0.07f, 0.14f);

        // 예광탄: 총구에서 멀리 앞쪽으로 가는 가는 빛줄기
        Transform ct = cam.transform;
        bool trL = fire && Random.value > 0.3f, trR = fire && Random.value > 0.3f;
        if (gunTracerL.gameObject.activeSelf != trL && !trL) gunTracerL.gameObject.SetActive(false);
        if (gunTracerR.gameObject.activeSelf != trR && !trR) gunTracerR.gameObject.SetActive(false);
        if (trL) PlaceBeam(gunTracerL, gunFlashL.position, ct.position + ct.forward * 40f - ct.right * Random.Range(0.5f, 3f) + ct.up * Random.Range(-0.5f, 1f), 0.03f);
        if (trR) PlaceBeam(gunTracerR, gunFlashR.position, ct.position + ct.forward * 40f + ct.right * Random.Range(0.5f, 3f) + ct.up * Random.Range(-0.5f, 1f), 0.03f);
    }
}
