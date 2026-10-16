using UnityEngine;

public enum DayType { Normal, Crowded, Kaiju, Hero }

// 날짜/사건 매니저: 하루(= 한 판)마다 날의 종류를 무작위로 정한다.
//  평범한 날 / 사람 많은 날(처음부터 보행자와 손님이 많음) / 괴수 침공(갑자기) / 히어로와 빌런의 전투(갑자기).
// 괴수와 히어로 날은 시작할 때는 평범해 보이다가, 하루 중 무작위 시각에 "긴급 속보"와 함께 사건이 터진다.
// 괴수와 레이저로 실제 장애물을 만드는 건 TrafficManager, 하늘의 히어로 연출은 DriveGame 이 한다. 이 클래스는 일정과 글자만 안다.
public class EventManager
{
    const string DayKey = "DontHitMe.Day";

    readonly DriveGame cfg;
    float startAt, endAt;

    public int Day { get; private set; }
    public DayType Type { get; private set; }
    public bool Active { get; private set; }                       // 괴수/히어로 사건이 진행 중
    public bool KaijuActive { get { return Active && Type == DayType.Kaiju; } }
    public bool HeroActive { get { return Active && Type == DayType.Hero; } }
    public string DayLabel { get; private set; }                   // 화면 오른쪽에 보이는 "Day 3 · 괴수 침공!"
    public string AlertText { get; private set; }                  // 화면에 붉게 깜빡이는 긴급 속보
    public float AlertUntil { get; private set; }

    // 사람 많은 날의 배율 (기본값은 영향 없음)
    public float PedIntervalMul = 1f, MaxPedsMul = 1f, CrowdMul = 1f, CallerDelayMul = 1f;

    /// <summary>괴수/히어로 사건이 시작됐다.</summary>
    public event System.Action<DayType> Started;

    public EventManager(DriveGame cfg)
    {
        this.cfg = cfg;
        Day = Mathf.Max(1, PlayerPrefs.GetInt(DayKey, 1));
        DayLabel = "Day " + Day;
        AlertText = "";
    }

    /// <summary>새 하루 시작: 날의 종류와 사건 시각을 정한다.</summary>
    public void Reset()
    {
        Active = false;
        AlertText = "";
        AlertUntil = 0f;
        Type = PickType();

        PedIntervalMul = MaxPedsMul = CrowdMul = CallerDelayMul = 1f;
        if (Type == DayType.Crowded)
        {
            PedIntervalMul = 0.5f;
            MaxPedsMul = 1.6f;
            CrowdMul = 1.6f;
            CallerDelayMul = 0.6f;
        }

        float window = Mathf.Max(60f, cfg.shiftSeconds);
        startAt = Random.Range(0.25f, 0.55f) * window;
        endAt = startAt + (Type == DayType.Kaiju ? cfg.kaijuDuration : cfg.heroDuration);
        RefreshLabel();
    }

    DayType PickType()
    {
        if (cfg.forceDayType >= 0) return (DayType)Mathf.Clamp(cfg.forceDayType, 0, 3);
        if (Day <= 1) return DayType.Normal;                       // 첫날은 평범하게
        float r = Random.value;
        if (r < 0.40f) return DayType.Normal;
        if (r < 0.60f) return DayType.Crowded;
        if (r < 0.80f) return DayType.Kaiju;
        return DayType.Hero;
    }

    /// <summary>판이 시작된 직후: 사람 많은 날이면 알려 준다.</summary>
    public void Announce()
    {
        if (Type != DayType.Crowded) return;
        AlertText = "오늘은 사람 많은 날! 길도 손님도 북적입니다";
        AlertUntil = Time.unscaledTime + 5f;
    }

    public void Tick(float elapsed)
    {
        if (Type != DayType.Kaiju && Type != DayType.Hero) return;
        if (!Active && elapsed >= startAt && elapsed < endAt) Begin();
        else if (Active && elapsed >= endAt)
        {
            Active = false;
            RefreshLabel();
        }
    }

    void Begin()
    {
        Active = true;
        AlertText = Type == DayType.Kaiju
            ? "긴급 속보! 괴수가 나타났습니다 — 다음 교차로에서 돌아가세요!"
            : "긴급 속보! 히어로와 빌런이 하늘에서 전투 중 — 레이저를 피하세요!";
        AlertUntil = Time.unscaledTime + 6f;
        RefreshLabel();
        if (Started != null) Started(Type);
    }

    /// <summary>시험용: 지금 바로 이 사건을 일으킨다 (F2 괴수, F3 히어로).</summary>
    public void DebugStart(DayType t, float elapsed)
    {
        DebugForce(t, elapsed);
    }

    /// <summary>시험용: 오늘의 날 종류를 지금 바로 바꾼다. 괴수/히어로는 다음 프레임에 사건이 시작되고, 평범한 날은 진행 중인 사건이 끝난다.</summary>
    public void DebugForce(DayType t, float elapsed)
    {
        Active = false;
        Type = t;
        AlertUntil = 0f;
        PedIntervalMul = MaxPedsMul = CrowdMul = CallerDelayMul = 1f;
        if (t == DayType.Crowded)
        {
            PedIntervalMul = 0.5f;
            MaxPedsMul = 1.6f;
            CrowdMul = 1.6f;
            CallerDelayMul = 0.6f;
        }
        startAt = elapsed;
        endAt = elapsed + (t == DayType.Kaiju ? cfg.kaijuDuration : cfg.heroDuration);
        RefreshLabel();
    }

    /// <summary>하루가 끝났다: 다음 날 번호로.</summary>
    public void CompleteDay()
    {
        Day++;
        PlayerPrefs.SetInt(DayKey, Day);
        PlayerPrefs.Save();
        DayLabel = "Day " + Day;
    }

    void RefreshLabel()
    {
        string extra = "";
        if (Type == DayType.Crowded) extra = "  ·  사람 많은 날";
        else if (KaijuActive) extra = "  ·  괴수 침공!";
        else if (HeroActive) extra = "  ·  히어로 vs 빌런";
        DayLabel = "Day " + Day + extra;
    }
}
