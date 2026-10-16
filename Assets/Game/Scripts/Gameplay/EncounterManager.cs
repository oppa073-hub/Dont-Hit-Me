using UnityEngine;

public enum EncounterType { None, Blackout, Rival, Fans, Pigs, Emergency }

// 랜덤 인카운트 스케줄러: 운전 중 가끔 길거리 사건이 하나씩 터진다. (괴수/히어로 같은 '날'과는 별개)
//  정전(도시가 어두워짐) / 라이벌 택시(내 손님을 가로채려 함) / 팬 습격(연예인 출몰, 사람들이 차 앞으로 몰려듦) / 돼지 떼(트럭 전복, 돼지가 도로를 건넘)
//  / 긴급 차량(구급차가 뒤에서 사이렌을 울리며 달려옴, 길을 비켜 줘야 함)
// 사건이 하나 끝나기 전에는 다음 사건이 시작되지 않는다. 이 클래스는 일정과 정전의 밝기만 알고, 실제 사람/돼지/택시는 각 매니저가 만든다.
// 설정 값(켜기, 첫 사건까지의 시간, 사건 간격)은 DriveGame 의 public 필드를 읽는다.
public class EncounterManager
{
    readonly DriveGame cfg;
    float nextAt, endAt;

    public EncounterType Active { get; private set; }
    /// <summary>정전의 어두운 정도 (0 ~ 1, 천천히 어두워졌다 밝아진다).</summary>
    public float Blackout01 { get; private set; }

    /// <summary>사건이 시작됐다.</summary>
    public event System.Action<EncounterType> Started;

    public EncounterManager(DriveGame cfg)
    {
        this.cfg = cfg;
    }

    public void Reset(float elapsed)
    {
        Active = EncounterType.None;
        Blackout01 = 0f;
        endAt = 0f;
        nextAt = elapsed + Mathf.Max(5f, cfg.encounterFirstDelay);
    }

    static float Duration(EncounterType t)
    {
        switch (t)
        {
            case EncounterType.Blackout: return 18f;
            case EncounterType.Fans: return 10f;
            case EncounterType.Pigs: return 8f;
            case EncounterType.Emergency: return 9f;
            default: return 12f;
        }
    }

    /// <summary>allow = 지금 새 사건을 시작해도 되는 상태인가, rivalPossible = 라이벌 택시가 노릴 손님이 마침 길가에 있는가.</summary>
    public void Tick(float dt, float elapsed, bool allow, bool rivalPossible)
    {
        Blackout01 = Mathf.MoveTowards(Blackout01, Active == EncounterType.Blackout ? 1f : 0f, dt / 1.2f);
        if (!cfg.encountersOn)
        {
            Active = EncounterType.None;
            return;
        }
        if (Active != EncounterType.None)
        {
            if (elapsed >= endAt)
            {
                Active = EncounterType.None;
                nextAt = elapsed + Random.Range(cfg.encounterInterval.x, cfg.encounterInterval.y);
            }
            return;
        }
        if (elapsed < nextAt || !allow) return;

        int n = rivalPossible ? 5 : 4;
        int pick = Random.Range(0, n);
        EncounterType t = pick == 0 ? EncounterType.Blackout : (pick == 1 ? EncounterType.Fans : (pick == 2 ? EncounterType.Pigs : (pick == 3 ? EncounterType.Emergency : EncounterType.Rival)));
        Begin(t, elapsed);
    }

    /// <summary>시험용: 지금 바로 이 사건을 일으킨다.</summary>
    public void Force(EncounterType t, float elapsed)
    {
        Begin(t, elapsed);
    }

    void Begin(EncounterType t, float elapsed)
    {
        Active = t;
        endAt = elapsed + Duration(t);
        if (Started != null) Started(t);
    }
}
