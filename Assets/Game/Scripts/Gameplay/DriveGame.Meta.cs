using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 성장 연결부: 코인과 업그레이드(UpgradeManager), 오늘의 목표(GoalManager)를 게임에 반영한다.
// 업그레이드 효과는 판을 시작할 때 목숨/무적/택시 요금에 적용하고, 코인은 판이 끝날 때와 목표를 달성할 때 지급한다.
public partial class DriveGame
{
    UpgradeManager upgrades;
    GoalManager goals;

    // 상점 화면 글자 (열 때와 살 때만 새로 만든다)
    string[] shopLines = new string[0];
    string shopCoinsText = "", shopTitleText = "", shopMsg = "";
    float shopMsgUntil;

    static readonly Key[] ShopKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

    void CreateMeta()
    {
        upgrades = new UpgradeManager();
        goals = new GoalManager();
        goals.Completed += OnGoalCompleted;
        RefreshShop();                       // 타이틀에 코인 글자가 보이도록
    }

    // 판을 시작할 때: 산 업그레이드를 목숨/무적/택시에 적용 (session.Reset 보다 먼저 불러야 목숨에 반영된다)
    void ApplyUpgrades()
    {
        session.ExtraLives = upgrades.ExtraLives;
        session.InvulnBonus = upgrades.InvulnBonus;
        taxi.FareBonusMul = upgrades.FareMul;
        taxi.TipBonusMul = upgrades.TipMul;
        taxi.TimeBonus = upgrades.TimeBonus;
    }

    void UpdateGoals()
    {
        goals.Tick(new RunStats
        {
            trips = taxi.Trips,
            nearMisses = nearMisses,
            hits = hits,
            level = level,
            score = session.TotalScore,
            fareTotal = taxi.FareTotal,
            elapsed = elapsed
        });
    }

    void OnGoalCompleted(GoalManager.Goal g)
    {
        upgrades.AddCoins(g.reward);
        Popup("목표 달성!  +" + g.reward + " 코인", new Color(1f, 0.9f, 0.3f));
        sound.PlayArrive();
    }

    // 판이 끝났다: 번 돈과 니어미스로 코인을 받는다. 결과 화면에 붙일 글자를 돌려준다.
    string GrantRunCoins()
    {
        int earned = RunIncome() / 10 + nearMisses * 5;
        upgrades.AddCoins(earned);
        return "획득 코인 +" + earned + "   보유 " + upgrades.Coins + NL
             + "오늘의 목표 " + goals.DoneTodayCount + "/" + goals.Count + " 달성";
    }

    // ---- 상점 ----

    void OpenShop()
    {
        flow = GameFlow.Shop;
        RefreshShop();
    }

    void RefreshShop()
    {
        shopCoinsText = "보유 코인  " + upgrades.Coins;
        shopTitleText = shopCoinsText + "      U  업그레이드 상점      O  설정";
        if (shopLines.Length != upgrades.Count) shopLines = new string[upgrades.Count];
        for (int i = 0; i < upgrades.Count; i++)
        {
            var d = UpgradeManager.Defs[i];
            string cost = upgrades.IsMax(i) ? "최고 단계" : "비용 " + upgrades.Cost(i);
            shopLines[i] = "[" + (i + 1) + "] " + d.name + "  Lv " + upgrades.Level(i) + "/" + d.maxLevel + "   " + d.desc + "   " + cost;
        }
    }

    void UpdateShop(Keyboard kb)
    {
        for (int i = 0; i < upgrades.Count && i < ShopKeys.Length; i++)
        {
            if (!kb[ShopKeys[i]].wasPressedThisFrame) continue;
            if (upgrades.IsMax(i)) shopMsg = "이미 최고 단계입니다";
            else if (upgrades.TryBuy(i)) shopMsg = UpgradeManager.Defs[i].name + " 구매!";
            else shopMsg = "코인이 모자랍니다";
            shopMsgUntil = Time.unscaledTime + 1.5f;
            RefreshShop();
        }
        if (kb.escapeKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            flow = GameFlow.Title;
    }
}
