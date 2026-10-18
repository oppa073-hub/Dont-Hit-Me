using UnityEngine;

// 튜토리얼 매니저: 처음 플레이할 때 화면 위에 한 단계씩 안내 글자를 보여 준다.
// 게임을 직접 조작하지 않고 Tick 인자(차 위치, 택시 상태, 태운 손님 수)만 보고 다음 단계로 넘어간다.
// 끝까지 보면 PlayerPrefs 에 기록해서 다시 안 보여 주고, 설정 화면에서 다시 켤 수 있다.
public class TutorialManager
{
    const string DoneKey = "DontHitMe.TutorialDone";

    static readonly string[] Steps =
    {
        "A / D 로 좌우 조향 — 길을 건너는 사람을 피하세요‖Steer left and right with A / D — dodge people crossing the road",
        "머리 위 느낌표 `!` 손님 옆에서 S(브레이크)로 완전히 멈추고, Space 로 태우세요‖Stop completely next to a passenger with the exclamation mark `!` (S = brake), then press Space to pick them up",
        "화면 위 내비 글자와 대시보드 지도를 보며 목적지 교차로로 가세요. 돌 때는 교차로 전에 Q / E 로 깜빡이를 켜 두세요‖Follow the nav text and the dashboard map to the destination intersection. To turn, switch on the blinker (Q / E) before the intersection",
        "목적지 근처에서 S 로 멈추면 요금 정산! 게임오버 뒤 U 키로 코인 상점을 열 수 있어요‖Stop near the destination with S to collect the fare! After a game over, press U for the coin shop",
    };

    int step;
    float stepT;
    bool seenCarrying;

    /// <summary>지금 안내를 보여 주는 중인가.</summary>
    public bool Active { get; private set; }

    public string Text { get { return Active ? Loc.S(Steps[step]) : ""; } }

    public TutorialManager()
    {
        Active = PlayerPrefs.GetInt(DoneKey, 0) == 0;
    }

    /// <summary>설정 화면에서 안내를 다시 켜거나 끈다.</summary>
    public void SetEnabled(bool on)
    {
        PlayerPrefs.SetInt(DoneKey, on ? 0 : 1);
        PlayerPrefs.Save();
        Active = on;
        step = 0;
        stepT = 0f;
        seenCarrying = false;
    }

    public bool Enabled { get { return PlayerPrefs.GetInt(DoneKey, 0) == 0; } }

    /// <summary>새 판 시작: 켜져 있으면 처음 단계부터.</summary>
    public void Reset()
    {
        Active = Enabled;
        step = 0;
        stepT = 0f;
        seenCarrying = false;
    }

    public void Tick(float dt, float carX, TaxiState taxi)
    {
        if (!Active) return;
        stepT += dt;
        switch (step)
        {
            case 0:     // 조향: 옆으로 한 번 움직이거나 12초가 지나면 다음
                if (Mathf.Abs(carX) > 1.0f || stepT > 12f) Next();
                break;
            case 1:     // 손님: 태우면 다음
                if (taxi == TaxiState.Carrying) Next();
                break;
            case 2:     // 내비/깜빡이: 정산이 끝나거나 손님이 없어지면 다음
                if (taxi == TaxiState.Carrying) seenCarrying = true;
                else if (seenCarrying && taxi == TaxiState.Cruise) Next();
                break;
            default:    // 마무리 안내는 8초 보여 주고 끝
                if (stepT > 8f)
                {
                    PlayerPrefs.SetInt(DoneKey, 1);
                    PlayerPrefs.Save();
                    Active = false;
                }
                break;
        }
    }

    void Next()
    {
        step++;
        stepT = 0f;
        seenCarrying = false;
    }
}
