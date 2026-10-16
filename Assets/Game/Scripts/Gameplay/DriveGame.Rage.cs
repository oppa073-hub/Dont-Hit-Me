using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 분노 연결부: 게이지는 RageManager 가 하고, 여기서는 그 결과(팔이 붉어짐, 무적으로 들이받기, 속도 증가, 화면 붉은 막)를 게임에 반영한다.
public partial class DriveGame
{
    RageManager rage;
    Material armMaterial;               // 팔 재질 (분노 게이지에 따라 붉게 칠한다)

    static readonly Color RageRed = new Color(1f, 0.12f, 0.08f);

    void CreateRage()
    {
        rage = new RageManager(this);
        rage.Started += OnRageStarted;
        rage.Ended += OnRageEnded;
    }

    void UpdateRage(float dt)
    {
        if (!gameOver) rage.Tick(dt, taxi.State == TaxiState.Carrying);
        peds.RageSurge = rage.Active && !gameOver;                  // 분노 중에는 시민이 더 몰려든다 (박살 낼 보상)       // 분노는 손님을 태웠을 때만 발동한다
    }

    void OnRageStarted()
    {
        Popup(gunsOwned ? "분노 폭발!  미니건 전개!" : "분노 폭발!  무적 + 가속", new Color(1f, 0.3f, 0.2f));
        gunKills = 0;
        gunScoreSum = 0;
        if (gunsOwned) taxi.Say(StoryCatalog.GunPassengerLine(), 2.6f);
        sound.PlayRoar();
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.25f);
    }

    void OnRageEnded()
    {
        if (gunsOwned && gunKills > 0) Popup("난사 " + gunKills + "마리 처치!  +" + gunScoreSum, UiGold);
        else Popup("분노가 가라앉았다", new Color(0.9f, 0.9f, 0.9f));
        session.ExtendInvuln(0.6f);                 // 겹친 채 끝나도 바로 다치지 않게 잠깐 더 무적
    }

    // 분노 중 부딪혔다: 목숨/요금/속도는 그대로, 점수만 얻고 시원하게 날려 버린다
    void RageSmash()
    {
        sound.PlayBonk();
        shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + 0.15f);
        session.AddScore(rageSmashScore);
        Popup("박살!  +" + rageSmashScore, new Color(1f, 0.45f, 0.3f));
    }

    // 팔 색: 게이지가 찰수록 붉어지고, 분노 상태에서는 선명하게 깜빡인다 (매 프레임, 팔이 보일 때)
    void UpdateRageTint()
    {
        if (armMaterial == null) return;
        Color c;
        if (rage.Active)
        {
            // 남은 시간만큼 붉다: 발동 직후는 선명한 빨강으로 깜빡이다가, 끝에 다가갈수록 맥동이 줄고 원래 피부색으로 자연스럽게 돌아온다
            float rem = rage.Remaining01;
            Color baseC = Color.Lerp(Color.white, RageRed, rem);
            Color glow = Color.Lerp(baseC, new Color(1f, 0.45f, 0.3f), 0.5f * rem);
            c = Color.Lerp(baseC, glow, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f));
        }
        else if (rage.Ready) c = Color.Lerp(RageRed, new Color(0.7f, 0.04f, 0.04f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));     // 가득 찼고 손님을 기다리는 중: 느리게 맥동
        else c = Color.Lerp(Color.white, RageRed, rage.Gauge);
        armMaterial.SetColor("_BaseColor", c);
    }

    // 분노 상태일 때 화면 가장자리까지 붉은 막
    void DrawRageOverlay(float W, float H)
    {
        if (!rage.Active || gameOver || flow != GameFlow.Playing) return;       // 결과/일시정지 화면에는 붉은 막을 남기지 않는다
        float a = (0.10f + 0.06f * Mathf.Sin(Time.unscaledTime * 10f)) * (0.35f + 0.65f * rage.Remaining01);     // 끝나 갈수록 붉은 막도 옅어진다
        Fill(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, a));
    }
}
