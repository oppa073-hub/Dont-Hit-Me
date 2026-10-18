using UnityEngine;

public enum DayType { Normal, Crowded, Kaiju, Hero, Zombie }

// 날짜/사건 매니저: 하루(= 한 판)마다 날의 종류를 무작위로 정한다.
//  평범한 날 / 사람 많은 날(처음부터 보행자와 손님이 많음) / 괴수 침공(갑자기) / 히어로와 빌런의 전투(갑자기) / 좀비 사태(갑자기: 좀비는 치면 보상, 주황 생존자는 치면 사고).
// 괴수와 히어로 날은 시작할 때는 평범해 보이다가, 하루 중 무작위 시각에 "긴급 속보"와 함께 사건이 터진다.
// 괴수와 레이저로 실제 장애물을 만드는 건 TrafficManager, 하늘의 히어로 연출은 DriveGame 이 한다. 이 클래스는 일정과 글자만 안다.
public class EventManager
{
    const string DayKey = "DontHitMe.Day";

    readonly DriveGame cfg;
    float startAt, endAt;

    public int Day { get; private set; }
    public DayType LastType { get; private set; }                  // 어제는 어떤 날이었나 (정비사의 인사말에 쓴다)
    public DayType Type { get; private set; }
    public bool Active { get; private set; }                       // 괴수/히어로 사건이 진행 중
    public bool KaijuActive { get { return Active && Type == DayType.Kaiju; } }
    public bool HeroActive { get { return Active && Type == DayType.Hero; } }
    public int Heat;                                               // 도시 위험도: 높을수록 사건이 있는 날이 더 자주 나온다
    public bool ZombieActive { get { return Active && Type == DayType.Zombie; } }
    public string DayLabel { get; private set; }                   // 화면 오른쪽에 보이는 "Day 3 · 괴수 침공!"
    public string AlertText { get; private set; }                  // 화면에 붉게 깜빡이는 긴급 속보
    public float AlertUntil { get; private set; }

    // 사람 많은 날의 배율 (기본값은 영향 없음)
    public float PedIntervalMul = 1f, MaxPedsMul = 1f, CrowdMul = 1f, CallerDelayMul = 1f;

    /// <summary>괴수/히어로 사건이 시작됐다.</summary>
    public event System.Action<DayType> Started;
    /// <summary>괴수/히어로/좀비 사건이 끝났다.</summary>
    public event System.Action<DayType> Ended;

    public EventManager(DriveGame cfg)
    {
        this.cfg = cfg;
        Day = Mathf.Max(1, PlayerPrefs.GetInt(DayKey, 1));
        LastType = (DayType)Mathf.Clamp(PlayerPrefs.GetInt(DayKey + ".LastType", 0), 0, 4);
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
        if (cfg.forceDayType >= 0) startAt = 8f;                 // 시험으로 날 종류를 고정했으면 사건이 금방 시작한다
        endAt = startAt + Duration(Type);
        RefreshLabel();
    }

    float Duration(DayType t)
    {
        return t == DayType.Kaiju ? cfg.kaijuDuration : (t == DayType.Zombie ? cfg.zombieDuration : cfg.heroDuration);
    }

    DayType PickType()
    {
        if (cfg.forceDayType >= 0) return (DayType)Mathf.Clamp(cfg.forceDayType, 0, 4);
        if (Day <= 1) return DayType.Normal;                       // 첫날은 평범하게
        float r = Random.value;
        // 사건이 있는 날(괴수/히어로/좀비)은 위험도 단계마다 확률이 늘고, 그만큼 평범한 날이 줄어든다
        float normal = Mathf.Max(0.12f, 0.34f - cfg.heatEventBonus * 3f * Heat);
        float crowded = normal + 0.16f;
        float each = (1f - crowded) / 3f;
        if (r < normal) return DayType.Normal;
        if (r < crowded) return DayType.Crowded;
        if (r < crowded + each) return DayType.Kaiju;
        if (r < crowded + each * 2f) return DayType.Hero;
        return DayType.Zombie;
    }

    /// <summary>화면 위쪽에 붉게 깜빡이는 긴급 속보를 띄운다 (랜덤 인카운트 안내용).</summary>
    public void RaiseAlert(string text, float seconds)
    {
        AlertText = text;
        AlertUntil = Time.unscaledTime + seconds;
    }

    /// <summary>판이 시작된 직후: 사람 많은 날이면 알려 준다.</summary>
    public void Announce()
    {
        if (Type != DayType.Crowded) return;
        AlertText = Loc.T("오늘은 사람 많은 날! 길도 손님도 북적입니다", "Crowded day! Streets and fares are packed");
        AlertUntil = Time.unscaledTime + 5f;
    }

    public void Tick(float elapsed)
    {
        if (Type != DayType.Kaiju && Type != DayType.Hero && Type != DayType.Zombie) return;
        if (!Active && elapsed >= startAt && elapsed < endAt) Begin();
        else if (Active && elapsed >= endAt)
        {
            Active = false;
            RefreshLabel();
            if (Ended != null) Ended(Type);
        }
    }

    void Begin()
    {
        Active = true;
        AlertText = Type == DayType.Kaiju
            ? Loc.T("긴급 속보! 괴수가 나타났습니다 — 다음 교차로에서 돌아가세요!", "BREAKING NEWS! A monster has appeared — turn back at the next intersection!")
            : (Type == DayType.Zombie
                ? Loc.T("긴급 속보! 좀비 출현 — 초록색은 쓸어버리고, 주황색 생존자는 피하세요!", "BREAKING NEWS! Zombies! Plow the green ones, avoid the orange survivors!")
                : Loc.T("긴급 속보! 히어로와 빌런이 하늘에서 전투 중 — 레이저를 피하세요!", "BREAKING NEWS! Hero vs villain in the sky — dodge the lasers!"));
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
        endAt = elapsed + Duration(t);
        RefreshLabel();
    }

    /// <summary>하루가 끝났다: 다음 날 번호로.</summary>
    public void CompleteDay()
    {
        LastType = Type;
        Day++;
        PlayerPrefs.SetInt(DayKey, Day);
        PlayerPrefs.SetInt(DayKey + ".LastType", (int)LastType);
        PlayerPrefs.Save();
        DayLabel = "Day " + Day;
    }

    void RefreshLabel()
    {
        string extra = "";
        if (Type == DayType.Crowded) extra = Loc.T("  ·  사람 많은 날", "  ·  Crowded day");
        else if (KaijuActive) extra = Loc.T("  ·  괴수 침공!", "  ·  Monster attack!");
        else if (HeroActive) extra = Loc.T("  ·  히어로 vs 빌런", "  ·  Hero vs Villain");
        else if (ZombieActive) extra = Loc.T("  ·  좀비 사태!", "  ·  Zombie outbreak!");
        DayLabel = "Day " + Day + extra + (Heat > 0 ? Loc.T("  ·  위험 ", "  ·  Heat ") + Heat : "");
    }
}
