using UnityEngine;

public class ProjectDef
{
    public string name, desc;
    public int cost;
}

// 택시 개조 매니저: 번 코인을 크게 쓰는 장기 목표 4단계. 앞 단계를 끝내야 다음 단계가 열리고, 한 번 끝낸 건 저장된다.
// 끝낼 때마다 로비의 모습이 바뀌고, 정비사의 병맛 대사가 나오고, 영구 효과가 붙는다. 마지막 단계가 엔딩이다.
// 이 클래스는 진행과 효과 값만 안다. 코인은 UpgradeManager 에서 쓰고, 대사/화면은 DriveGame 쪽이 맡는다.
public class ProjectManager
{
    public const int Minigun = 2;           // 세 번째 개조: 양옆 미니건
    public const int Wings = 3;             // 마지막 개조: 하늘을 나는 택시 (엔딩)

    public static readonly ProjectDef[] Defs =
    {
        new ProjectDef { name = "정비소 증축 + 네온간판",   desc = "차고를 넓힌다  —  모든 업그레이드 20% 할인",                       cost = 4000 },
        new ProjectDef { name = "황금 도색 + 언더네온",     desc = "눈이 부신 전설의 택시  —  분노 1.5배 오래, 요금 +10%",              cost = 9000 },
        new ProjectDef { name = "지붕 양옆 미니건",         desc = "분노 폭발 때 튀어나와 앞을 쓸어버린다 (괴수, 히어로, 손님은 제외)", cost = 24000 },
        new ProjectDef { name = "하늘을 나는 택시",         desc = "모든 기사의 꿈  —  분노 충전 1.5배, 요금 +15%, 엔딩",              cost = 60000 },
    };

    const string Key = "DontHitMe.Proj.";
    readonly bool[] done = new bool[Defs.Length];

    public int Count { get { return Defs.Length; } }

    public ProjectManager()
    {
        for (int i = 0; i < done.Length; i++) done[i] = PlayerPrefs.GetInt(Key + i, 0) == 1;
    }

    public bool Done(int i) { return done[i]; }

    /// <summary>앞 단계를 끝냈는가 (첫 단계는 항상 열려 있다).</summary>
    public bool Unlocked(int i) { return i == 0 || done[i - 1]; }

    public int DoneCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < done.Length; i++) if (done[i]) n++;
            return n;
        }
    }

    /// <summary>코인을 내고 개조를 끝낸다. 이미 끝냈거나 잠겼거나 코인이 모자라면 false.</summary>
    public bool Buy(int i, UpgradeManager coins)
    {
        if (done[i] || !Unlocked(i)) return false;
        if (!coins.Spend(Defs[i].cost)) return false;
        done[i] = true;
        PlayerPrefs.SetInt(Key + i, 1);
        PlayerPrefs.Save();
        return true;
    }

    // ---- 영구 효과 ----
    public float UpgradeCostMul { get { return done[0] ? 0.8f : 1f; } }
    public float FareMul { get { return 1f + (done[1] ? 0.10f : 0f) + (done[3] ? 0.15f : 0f); } }
    public float RageDurationMul { get { return done[1] ? 1.5f : 1f; } }
    public float RageFillMul { get { return done[3] ? 1.5f : 1f; } }
}
