using UnityEngine;

// 이번 판의 진행 상황 (목표 판정에 쓴다)
public struct RunStats
{
    public int trips, nearMisses, hits, level, score, fareTotal;
    public float elapsed;
}

// 오늘의 목표 매니저: 날짜가 같으면 같은 목표 3개가 나오고, 한 판 안에서 달성하면 보상 코인을 준다.
// 오늘 이미 달성한 목표는 PlayerPrefs 에 기록되어 다시 보상을 주지 않는다. 코인 지급은 받는 쪽(DriveGame)이 한다.
public class GoalManager
{
    public class Goal
    {
        public int kind, target, reward;
        public string key;
        public string Line = "";          // 화면에 그대로 쓰는 글자 (값이 바뀔 때만 새로 만든다)
        public bool doneToday;
        public int lastShown = -1;
    }

    // 종류별 문구와 목표값 후보(쉬움 → 어려움)
    static readonly string[] Names = { "손님 {0}명 태우기", "아슬아슬하게 {0}번 피하기", "레벨 {0} 도달", "충돌 없이 {0}초 버티기", "점수 {0}점 달성", "요금 합계 {0}원 벌기" };
    static readonly int[][] Targets =
    {
        new[] { 2, 3, 4 }, new[] { 5, 8, 12 }, new[] { 3, 4, 5 }, new[] { 45, 60, 90 }, new[] { 500, 1000, 1500 }, new[] { 5000, 8000, 12000 }
    };

    readonly Goal[] goals = new Goal[3];

    /// <summary>오늘 처음 달성했다 (보상 지급 대상).</summary>
    public event System.Action<Goal> Completed;

    public int Count { get { return goals.Length; } }
    public Goal this[int i] { get { return goals[i]; } }

    public GoalManager()
    {
        var now = System.DateTime.Now;
        int seed = now.Year * 10000 + now.Month * 100 + now.Day;
        var rng = new System.Random(seed);

        int[] kinds = { 0, 1, 2, 3, 4, 5 };
        for (int i = kinds.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = kinds[i]; kinds[i] = kinds[j]; kinds[j] = tmp;
        }
        for (int i = 0; i < goals.Length; i++)
        {
            int kind = kinds[i];
            int variant = rng.Next(Targets[kind].Length);
            var g = new Goal
            {
                kind = kind,
                target = Targets[kind][variant],
                reward = 200 + variant * 100,
                key = "DontHitMe.Goal." + seed + "." + i
            };
            g.doneToday = PlayerPrefs.GetInt(g.key, 0) == 1;
            goals[i] = g;
        }
        Reset();
    }

    /// <summary>새 판 시작: 진행 글자를 0 부터 다시.</summary>
    public void Reset()
    {
        foreach (var g in goals)
        {
            g.lastShown = -1;
            Refresh(g, 0);
        }
    }

    public int DoneTodayCount
    {
        get
        {
            int n = 0;
            foreach (var g in goals) if (g.doneToday) n++;
            return n;
        }
    }

    static int Value(int kind, RunStats s)
    {
        switch (kind)
        {
            case 0: return s.trips;
            case 1: return s.nearMisses;
            case 2: return s.level;
            case 3: return s.hits == 0 ? Mathf.FloorToInt(s.elapsed) : 0;
            case 4: return s.score;
            default: return s.fareTotal;
        }
    }

    void Refresh(Goal g, int cur)
    {
        string name = string.Format(Names[g.kind], g.target);
        if (g.doneToday) g.Line = "[완료] " + name;
        else g.Line = "[ ] " + name + " (" + Mathf.Min(cur, g.target) + "/" + g.target + ")  +" + g.reward;
        g.lastShown = cur;
    }

    public void Tick(RunStats s)
    {
        for (int i = 0; i < goals.Length; i++)
        {
            var g = goals[i];
            if (g.doneToday) continue;
            int cur = Value(g.kind, s);
            if (cur >= g.target)
            {
                g.doneToday = true;
                PlayerPrefs.SetInt(g.key, 1);
                PlayerPrefs.Save();
                Refresh(g, cur);
                if (Completed != null) Completed(g);
            }
            else if (cur != g.lastShown)
            {
                Refresh(g, cur);
            }
        }
    }
}
