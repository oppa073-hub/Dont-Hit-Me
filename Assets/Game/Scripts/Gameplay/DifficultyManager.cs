using UnityEngine;

// 난이도 매니저: 플레이 시간이 지나면 레벨이 오르고, 레벨마다 보행자/교통/돌발 상황이 점점 거칠어진다.
// 이 클래스는 "지금 몇 레벨인가"와 레벨별 배율만 알려 준다. 실제로 적용하는 건 DriveGame 연결부(DriveGame.Traffic.cs)가 한다.
// 설정 값(레벨 길이, 최대 레벨)은 DriveGame 의 public 필드를 읽는다.
public class DifficultyManager
{
    readonly DriveGame cfg;

    public int Level { get; private set; }
    public int Heat;                         // 도시 위험도 0~5 (DriveGame 이 판을 시작할 때 알려 준다)
    public int MaxLevelReached { get; private set; }

    /// <summary>레벨이 바뀌었다 (새 레벨 번호).</summary>
    public event System.Action<int> LevelChanged;

    public DifficultyManager(DriveGame cfg)
    {
        this.cfg = cfg;
        Reset();
    }

    public void Reset()
    {
        Level = LevelAt(0f);
        MaxLevelReached = Level;
    }

    // 위험도가 높으면 판을 높은 레벨에서 시작한다
    int LevelAt(float elapsed)
    {
        float shift = Heat * cfg.heatLevelPerStep * Mathf.Max(5f, cfg.levelSeconds);
        return Mathf.Clamp(1 + Mathf.FloorToInt((elapsed + shift) / Mathf.Max(5f, cfg.levelSeconds)), 1, Mathf.Max(1, cfg.maxLevel));
    }

    /// <summary>위험도에 따라 늘어나는 동시 횡단 인원.</summary>
    public int CrossersBonus { get { return Heat / Mathf.Max(1, cfg.heatCrossersStep); } }

    public void Tick(float elapsed)
    {
        int lv = LevelAt(elapsed);
        if (lv == Level) return;
        Level = lv;
        if (lv > MaxLevelReached) MaxLevelReached = lv;
        if (LevelChanged != null) LevelChanged(lv);
    }

    // 레벨이 오를수록 보행자가 더 자주 나온다 (생성 간격에 곱함)
    public float PedIntervalMul { get { return Mathf.Max(0.4f, Mathf.Max(0.65f, 1f - 0.035f * (Level - 1)) - cfg.heatPedCut * Heat); } }

    // 가까이서 튀어나오는 사람의 비율이 늘어난다
    public float DartBonus { get { return Mathf.Min(0.12f, 0.015f * (Level - 1)) + cfg.heatDartBonus * Heat; } }

    // 튀어나오는 속도도 빨라진다
    public float DartSpeedMul { get { return 1f + 0.02f * (Level - 1); } }
}
