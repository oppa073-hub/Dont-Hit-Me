using UnityEngine;

// 업그레이드 매니저: 코인과 영구 강화 단계를 PlayerPrefs 에 저장한다. 효과는 읽기 전용 속성으로 알려 주고,
// 실제로 적용하는 건 DriveGame 연결부(DriveGame.Meta.cs)가 한다. 새 업그레이드를 늘리려면 Defs 표에 한 줄 추가하고 아래 효과 속성을 만든다.
public class UpgradeDef
{
    public string name, desc;
    public int maxLevel, baseCost;       // 비용 = baseCost * (현재 단계 + 1)
}

public class UpgradeManager
{
    public static readonly UpgradeDef[] Defs =
    {
        new UpgradeDef { name = "여분 목숨",   desc = "시작 목숨 +1",              maxLevel = 2, baseCost = 800 },
        new UpgradeDef { name = "튼튼한 범퍼", desc = "피격 뒤 무적 시간 +0.25초", maxLevel = 4, baseCost = 400 },
        new UpgradeDef { name = "친절한 미소", desc = "팁 +25%",                   maxLevel = 4, baseCost = 500 },
        new UpgradeDef { name = "요금 협상",   desc = "요금 +8%",                  maxLevel = 5, baseCost = 600 },
        new UpgradeDef { name = "시간 여유",   desc = "손님 제한 시간 +3초",       maxLevel = 3, baseCost = 500 },
        new UpgradeDef { name = "자가 수리",   desc = "체력 회복 15% 빨라짐",      maxLevel = 4, baseCost = 450 },
    };

    const string CoinsKey = "DontHitMe.Coins";
    const string LevelKey = "DontHitMe.Up.";

    readonly int[] levels = new int[Defs.Length];

    public int Coins { get; private set; }

    /// <summary>업그레이드 값에 곱하는 할인 배율 (정비소 증축 프로젝트를 끝내면 0.8).</summary>
    public float CostMul = 1f;
    public int Count { get { return Defs.Length; } }

    public UpgradeManager()
    {
        Coins = PlayerPrefs.GetInt(CoinsKey, 0);
        for (int i = 0; i < levels.Length; i++) levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(LevelKey + i, 0), 0, Defs[i].maxLevel);
    }

    public int Level(int id) { return levels[id]; }

    public bool IsMax(int id) { return levels[id] >= Defs[id].maxLevel; }

    /// <summary>다음 단계 비용. 최고 단계면 -1.</summary>
    public int Cost(int id) { return IsMax(id) ? -1 : Mathf.RoundToInt(Defs[id].baseCost * (levels[id] + 1) * CostMul / 10f) * 10; }

    /// <summary>코인을 쓴다 (프로젝트 구매용). 모자라면 false.</summary>
    public bool Spend(int n)
    {
        if (n < 0 || Coins < n) return false;
        Coins -= n;
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.Save();
        return true;
    }

    public void AddCoins(int n)
    {
        if (n <= 0) return;
        Coins += n;
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.Save();
    }

    /// <summary>산다. 코인이 모자라거나 이미 최고 단계면 false.</summary>
    public bool TryBuy(int id)
    {
        int cost = Cost(id);
        if (cost < 0 || Coins < cost) return false;
        Coins -= cost;
        levels[id]++;
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.SetInt(LevelKey + id, levels[id]);
        PlayerPrefs.Save();
        return true;
    }

    // ---- 효과 ----
    public int ExtraLives { get { return levels[0]; } }
    public float InvulnBonus { get { return 0.25f * levels[1]; } }
    public float TipMul { get { return 1f + 0.25f * levels[2]; } }
    public float FareMul { get { return 1f + 0.08f * levels[3]; } }
    public float TimeBonus { get { return 3f * levels[4]; } }
    public float HealMul { get { return Mathf.Max(0.4f, 1f - 0.15f * levels[5]); } }
}
