using UnityEngine;

// 게임 진행 기록 매니저: 목숨, 점수, 니어미스/충돌 횟수, 무적 시간, 피격 붉은 화면, 게임오버 여부를 맡는다.
// 차의 속도나 화면 흔들림 같은 "연출"은 모른다. 설정 값(최대 목숨, 무적 시간)은 DriveGame 의 public 필드를 읽는다.
public class GameSession
{
    readonly DriveGame cfg;

    public int Lives { get; private set; }
    public int Score { get; private set; }               // 니어미스 보너스, 요금 환산 점수 등 (달린 거리 점수는 따로)
    public int NearMisses { get; private set; }
    public int Hits { get; private set; }
    public float DistScore { get; private set; }         // 달린 거리에서 얻는 점수
    public float Invuln { get; private set; }            // 남은 무적 시간(초)
    public float HurtFlash { get; private set; }         // 피격 붉은 화면의 남은 세기(초)
    public bool GameOver { get; private set; }
    public bool TimeUp { get; private set; }

    // 무사고로 달리면 하트가 하나씩 차오른다
    public float SafeTime { get; private set; }          // 마지막 사고 뒤 지난 시간
    public float HealTimeMul = 1f;                       // 회복 시간 배율 (업그레이드로 줄어든다)
    public event System.Action Healed;

    float HealNeeded { get { return Mathf.Max(10f, cfg.healSeconds * HealTimeMul); } }

    /// <summary>다음 하트가 차기까지의 진행도 (0 ~ 1). 하트가 가득이면 0.</summary>
    public float HealProgress { get { return Lives >= MaxLives || GameOver ? 0f : Mathf.Clamp01(SafeTime / HealNeeded); } }             // 목숨이 아니라 영업 시간이 끝나서 끝난 판

    /// <summary>화면에 보이는 총점.</summary>
    public int TotalScore { get { return Score + Mathf.FloorToInt(DistScore); } }

    // 업그레이드 효과 (DriveGame 이 판 시작 때 정해 준다)
    public int ExtraLives;
    public float InvulnBonus;

    /// <summary>이번 판의 최대 목숨 (기본 + 업그레이드).</summary>
    public int MaxLives { get { return cfg.maxLives + ExtraLives; } }

    public GameSession(DriveGame cfg)
    {
        this.cfg = cfg;
    }

    /// <summary>판을 새로 시작.</summary>
    public void Reset()
    {
        Lives = MaxLives;
        Score = NearMisses = Hits = 0;
        DistScore = 0f;
        Invuln = HurtFlash = 0f;
        GameOver = false;
        TimeUp = false;
        SafeTime = 0f;
    }

    /// <summary>플레이 중 매 프레임: 사고 없이 healSeconds 가 지나면 하트를 하나 채운다.</summary>
    public void TickHeal(float dt)
    {
        if (GameOver || Lives >= MaxLives) { SafeTime = 0f; return; }
        SafeTime += dt;
        if (SafeTime >= HealNeeded)
        {
            SafeTime = 0f;
            Lives++;
            if (Healed != null) Healed();
        }
    }

    /// <summary>영업 시간이 끝나 판을 마친다 (목숨은 그대로).</summary>
    public void ForceEnd()
    {
        if (GameOver) return;
        TimeUp = true;
        GameOver = true;
    }

    /// <summary>무적 시간과 피격 붉은 화면을 흘려보낸다.</summary>
    public void Tick(float dt)
    {
        Invuln = Mathf.Max(0f, Invuln - dt);
        HurtFlash = Mathf.Max(0f, HurtFlash - dt);
    }

    public void AddDistance(float meters)
    {
        DistScore += meters * 0.1f;
    }

    public void AddScore(int points)
    {
        Score += points;
    }

    public void RegisterNearMiss(int bonus)
    {
        NearMisses++;
        Score += bonus;
    }

    /// <summary>
    /// 사람을 쳤다. 충돌 횟수는 항상 늘고, 무적이 아닐 때만 목숨이 줄어든다.
    /// 목숨이 줄었으면 true, 무적이라 그냥 넘어갔으면 false.
    /// </summary>
    public bool RegisterHit()
    {
        Hits++;
        SafeTime = 0f;                                    // 사고가 나면 회복 시간이 처음부터 다시
        if (Invuln > 0f) return false;

        Lives--;
        Invuln = cfg.invulnTime + InvulnBonus;
        HurtFlash = 0.45f;
        if (Lives <= 0)
        {
            Lives = 0;
            GameOver = true;
        }
        return true;
    }

    /// <summary>무적 시간을 적어도 이만큼은 유지한다 (회전 직후 등).</summary>
    public void ExtendInvuln(float seconds)
    {
        Invuln = Mathf.Max(Invuln, seconds);
    }
}
