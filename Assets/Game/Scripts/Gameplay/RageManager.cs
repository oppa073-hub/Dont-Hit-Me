using UnityEngine;

// 분노 매니저: 시간이 지나면 분노 게이지가 차오르고(아슬아슬하게 피하면 더 빨리 참), 가득 차면 자동으로 분노 상태가 된다.
// 단, 분노는 손님을 태우고 있을 때만 발동한다. 게이지가 가득 차도 손님이 없으면 대기하다가 손님을 태우는 순간 발동한다.
// 분노 상태에서는 사람이나 차에 부딪혀도 목숨이 안 닳고 더 빨리 달린다. 끝나면 게이지가 0 으로 돌아가 다시 모은다.
// 이 클래스는 게이지와 상태만 안다. 무적/속도/팔 색 같은 실제 효과는 DriveGame 연결부(DriveGame.Rage.cs)가 적용한다.
// 설정 값(채우는 시간, 지속 시간, 속도 배율)은 DriveGame 의 public 필드를 읽는다.
public class RageManager
{
    readonly DriveGame cfg;

    /// <summary>분노 게이지 (0 ~ 1).</summary>
    public float Gauge { get; private set; }
    public bool Active { get; private set; }
    public float TimeLeft { get; private set; }

    /// <summary>게이지는 가득 찼는데 손님이 없어서 발동을 기다리는 중.</summary>
    public bool Ready { get { return Gauge >= 1f && !Active; } }

    bool canActivate;                   // 마지막 Tick 에서 받은 값: 지금 발동해도 되는가 (손님을 태운 중)

    public event System.Action Started, Ended;

    public RageManager(DriveGame cfg)
    {
        this.cfg = cfg;
    }

    public void Reset()
    {
        Gauge = 0f;
        Active = false;
        TimeLeft = 0f;
    }

    /// <summary>분노가 얼마나 남았는가 (발동 직후 1 → 끝날 때 0). 분노 중이 아니면 0.</summary>
    public float Remaining01 { get { return Active ? Mathf.Clamp01(TimeLeft / Mathf.Max(1f, cfg.rageDuration)) : 0f; } }

    /// <summary>분노 상태에서 곱할 속도 배율 (아니면 1).</summary>
    public float SpeedMul { get { return Active ? Mathf.Max(1f, cfg.rageSpeedMul) : 1f; } }

    public void Tick(float dt, bool canActivate)
    {
        this.canActivate = canActivate;
        if (!cfg.rageOn)
        {
            if (Active || Gauge > 0f) Reset();
            return;
        }
        if (Active)
        {
            TimeLeft -= dt;
            if (TimeLeft <= 0f)
            {
                Active = false;
                Gauge = 0f;
                if (Ended != null) Ended();
            }
            return;
        }
        Gauge = Mathf.Min(1f, Gauge + dt / Mathf.Max(5f, cfg.rageFillSeconds));
        if (Gauge >= 1f && canActivate) Begin();
    }

    /// <summary>아슬아슬하게 피했다: 게이지가 더 찬다 (쿨타임이 줄어드는 효과).</summary>
    public void AddNearMiss()
    {
        if (!cfg.rageOn || Active) return;
        Gauge = Mathf.Min(1f, Gauge + cfg.rageNearMissGain);
        if (Gauge >= 1f && canActivate) Begin();
    }

    /// <summary>시험용: 게이지를 가득 채워 바로 발동한다.</summary>
    public void DebugFill()
    {
        if (!Active && cfg.rageOn) Begin();
    }

    void Begin()
    {
        Gauge = 1f;
        Active = true;
        TimeLeft = Mathf.Max(1f, cfg.rageDuration);
        if (Started != null) Started();
    }
}
