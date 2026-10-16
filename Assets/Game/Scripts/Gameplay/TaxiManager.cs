using UnityEngine;

public enum TaxiState { Cruise, Talking, Carrying }

// 택시 매니저: 손님 호출 → 정차 → 대화 → 탑승 → 도착(정산)까지의 흐름과 요금/팁/제한 시간/목적지를 맡는다.
// 차의 속도, 시점, 점수, 팝업 같은 "게임 쪽" 일은 이벤트로 DriveGame 에 알리고, 필요한 값은 Tick/Accept 인자로 받는다.
// 목적지를 앞/옆 칸수로 바꾸는 계산(격자 좌표)은 DriveGame 이 가진 함수(DestRelFn)를 빌려 쓴다.
public class TaxiManager
{
    /// <summary>목적지 격자 좌표(destX, destY)가 지금 차 기준으로 앞 f칸, 오른쪽 r칸, 앞쪽 거리 sd(m)에 있는지 알려 주는 함수.</summary>
    public delegate void DestRelFn(int destX, int destY, out int f, out int r, out float sd);

    readonly DriveGame cfg;
    readonly PedestrianManager peds;
    readonly DestRelFn destRel;
    float callerTimer;
    float carX;                           // 마지막 Tick 에서 받은 차의 좌우 위치 (BeginTalk 이 쓴다)
    float prevCarX, sick, sickCooldown, chatTimer;     // 멀미 게이지, 수다 타이머

    // 업그레이드 효과 (DriveGame 이 판 시작 때 정해 준다. 기본값은 영향 없음)
    public float FareBonusMul = 1f;       // 요금에 곱함
    public float TipBonusMul = 1f;        // 팁에 곱함
    public float TimeBonus = 0f;          // 제한 시간에 더함(초)
    public float CallerDelayMul = 1f;     // 손님이 나타나는 간격 배율 (사람 많은 날은 짧게)
    public bool AcceptCallers = true;     // false 면 새 손님이 길가에 나타나지 않는다 (영업 시간이 끝났을 때)

    // ---- 상태 (읽기는 누구나, 쓰기는 이 클래스만) ----
    public TaxiState State { get; private set; }
    public PedestrianManager.Ped Caller { get; private set; }     // 손을 흔들며 기다리는 손님 (머리 위에 !)
    public PaxType Pax { get; private set; }                       // 지금 태운/대화 중인 손님의 유형
    public string PaxName { get; private set; }
    public string PaxLine { get; private set; }
    public string DestName { get; private set; }
    public Color PaxSkin { get; private set; }
    public Color PaxHair { get; private set; }
    public Color PaxShirt { get; private set; }
    public int DestX { get; private set; }                         // 목적지 교차로의 격자 좌표
    public int DestY { get; private set; }
    public float TripMeters { get; private set; }                  // 맨해튼 칸수 × 교차로 간격. 요금/남은 거리의 기준
    public float FareMul { get; private set; }                     // 사고/지나침으로 깎이는 요금 배율
    public float TripLimit { get; private set; }                   // 이번 트립의 제한 시간(초)
    public float TripT { get; private set; }                       // 탑승 후 지난 시간(초)
    public bool Overshot { get; private set; }                     // 이번 트립에서 이미 '지나침' 감액을 했는지
    public bool LateWarned { get; private set; }
    public int FareTotal { get; private set; }
    public int Trips { get; private set; }
    public bool ReactIsChat { get; private set; }                  // 한마디가 사고 반응이 아니라 수다인지
    public string ReactText { get; private set; }                  // 사람을 쳤을 때 손님의 한마디
    public float ReactUntil { get; private set; }
    public string ThanksText { get; private set; }                 // 도착했을 때 손님의 한마디
    public float ThanksUntil { get; private set; }

    // ---- 게임에 알리는 이벤트 ----
    public event System.Action<string, Color> PopupRequested;
    /// <summary>대화가 시작됐다. 인자는 시점을 돌릴 각도(도). 받는 쪽은 차를 세우고 시점을 돌린다.</summary>
    public event System.Action<float> TalkStarted;
    /// <summary>정산이 끝났다. 인자는 받은 돈(요금 + 팁). 받는 쪽은 점수에 반영한다.</summary>
    public event System.Action<int> Arrived;
    /// <summary>길가에 손님이 나타났다 (머리 위에 !).</summary>
    public event System.Action CallerAppeared;

    public TaxiManager(DriveGame cfg, PedestrianManager peds, DestRelFn destRel)
    {
        this.cfg = cfg;
        this.peds = peds;
        this.destRel = destRel;
    }

    void Popup(string text, Color c)
    {
        if (PopupRequested != null) PopupRequested(text, c);
    }

    void ResetCallerTimer()
    {
        callerTimer = Random.Range(cfg.callerDelay.x, cfg.callerDelay.y) * CallerDelayMul;
    }

    /// <summary>판을 새로 시작.</summary>
    public void Reset()
    {
        State = TaxiState.Cruise;
        Caller = null;
        Pax = null;
        PaxName = PaxLine = DestName = ReactText = ThanksText = "";
        ResetCallerTimer();
        DestX = DestY = 0;
        TripMeters = 0f;
        FareMul = 1f;
        TripLimit = TripT = 0f;
        Overshot = false;
        LateWarned = false;
        FareTotal = Trips = 0;
        AcceptCallers = true;
        ReactUntil = ThanksUntil = 0f;
        ReactIsChat = false;
        sick = sickCooldown = 0f;
    }

    /// <summary>회전이 끝나 사람들을 다시 깔았다: 기다리던 손님도 사라졌으니 잊고 다음 손님을 기다린다.</summary>
    public void CallerLost()
    {
        Caller = null;
        ResetCallerTimer();
    }

    /// <summary>
    /// 한 프레임 진행. speed = 차의 속도, carX = 차의 좌우 위치, turning = 교차로 회전 중, spawnZ = 사람이 나타나는 거리.
    /// </summary>
    public void Tick(float dt, float speed, float carX, bool turning, float spawnZ)
    {
        this.carX = carX;
        float latSpeed = dt > 0.0001f ? Mathf.Min(10f, Mathf.Abs(carX - prevCarX) / dt) : 0f;     // 좌우로 움직이는 빠르기
        prevCarX = carX;

        // 손님이 사라졌다(지나쳐서 제거됨)
        if (Caller != null && (Caller.t == null || !peds.Contains(Caller)))
        {
            Caller = null;
            ResetCallerTimer();
            if (State == TaxiState.Cruise) Popup("손님을 놓쳤다", new Color(0.8f, 0.8f, 0.8f));
        }

        if (State == TaxiState.Cruise)
        {
            if (Caller == null)
            {
                if (AcceptCallers) callerTimer -= dt;
                if (callerTimer <= 0f && AcceptCallers) MakeCaller(spawnZ);
            }
            else if (!turning && speed <= cfg.pickupSpeed && Caller.z > -2.5f && Caller.z < 9f && Mathf.Abs(Caller.x - carX) < cfg.pickupLateral)
            {
                BeginTalk();
            }
        }
        else if (State == TaxiState.Carrying)
        {
            // 제한 시간 (대화/히트스톱 중에는 dt 가 0 이거나 이 함수가 안 불려서 흐르지 않는다)
            TripT += dt;
            if (!LateWarned && TripT > TripLimit)
            {
                LateWarned = true;
                Popup("늦었어요! 요금이 깎입니다", new Color(1f, 0.6f, 0.4f));
            }

            // 수다쟁이 손님은 가끔 말을 건다
            if (Pax != null && Pax.chat != null && Pax.chat.Length > 0)
            {
                chatTimer -= dt;
                if (chatTimer <= 0f && Time.unscaledTime >= ReactUntil)
                {
                    ReactText = PassengerCatalog.PickOne(Pax.chat);
                    ReactIsChat = true;
                    ReactUntil = Time.unscaledTime + 2.5f;
                    chatTimer = Random.Range(10f, 16f);
                }
            }

            // 멀미하는 손님: 핸들을 급하게 꺾는 시간이 쌓이면 요금이 깎인다
            if (Pax != null && Pax.swerveSense > 0f && !turning)
            {
                if (sickCooldown > 0f) sickCooldown -= dt;
                else
                {
                    sick = Mathf.Max(0f, sick + Mathf.Max(0f, latSpeed - 2.5f) * Pax.swerveSense * dt - 0.3f * dt);
                    if (sick >= 1f)
                    {
                        sick = 0f;
                        sickCooldown = 3f;
                        FareMul = Mathf.Max(0.2f, FareMul - 0.1f);
                        ReactText = PassengerCatalog.PickOne(Pax.react);
                        ReactIsChat = false;
                        ReactUntil = Time.unscaledTime + 1.6f;
                        Popup("멀미! 요금 -10%", new Color(0.7f, 0.9f, 0.5f));
                    }
                }
            }

            // 회전 중에는 판정하지 않는다. 목적지 줄(r==0) 위에서만 도착/지나침을 본다.
            destRel(DestX, DestY, out int f, out int r, out float sd);
            if (!turning && r == 0)
            {
                float radius = Mathf.Max(1f, cfg.arriveRadius);
                if (speed <= cfg.pickupSpeed && Mathf.Abs(sd) <= radius)
                {
                    Arrive();
                }
                else if (sd < -radius && !Overshot)
                {
                    // 지나쳤다: 트립당 한 번만 요금을 깎는다. 목적지는 그대로 두고 옆길로 돌아와 다시 접근한다.
                    Overshot = true;
                    Popup("목적지를 지나쳤어요!", new Color(1f, 0.5f, 0.4f));
                    FareMul = Mathf.Max(0.2f, FareMul - 0.2f);
                }
            }
        }
    }

    void MakeCaller(float spawnZ)
    {
        var p = peds.Spawn(spawnZ, 0, cfg.callerDistance);
        p.caller = true;
        p.walkZ = 0f;                                        // 연석에서 제자리
        p.x = p.side * (cfg.roadHalfWidth + 1.2f);
        Caller = p;
        if (CallerAppeared != null) CallerAppeared();
    }

    void BeginTalk()
    {
        State = TaxiState.Talking;
        Pax = PassengerCatalog.PickType();
        PaxName = PassengerCatalog.PickOne(Pax.names);
        DestName = PassengerCatalog.PickOne(Pax.dests);
        PaxLine = string.Format(PassengerCatalog.PickOne(Pax.hello), DestName);
        PaxSkin = PassengerCatalog.SkinTones[Random.Range(0, PassengerCatalog.SkinTones.Length)];
        PaxHair = PassengerCatalog.HairTones[Pax.hair >= 0 ? Pax.hair % PassengerCatalog.HairTones.Length : Random.Range(0, PassengerCatalog.HairTones.Length)];
        PaxShirt = Caller.baseColor;

        // 손님 쪽(왼쪽/오른쪽)으로 시점을 돌린다
        float dx = Caller.x - (carX - 0.4f);
        float yaw = Mathf.Atan2(dx, Mathf.Max(0.5f, Caller.z)) * Mathf.Rad2Deg;
        if (TalkStarted != null) TalkStarted(yaw);
    }

    /// <summary>손님을 태운다. 목적지를 정하고 제한 시간을 계산한다. (gx, gy) = 방금 지난 교차로, heading = 진행 방향, period = 교차로 간격(m), cruiseSpeed = 지금 순항 속도.</summary>
    public void Accept(int gx, int gy, int heading, float period, float cruiseSpeed)
    {
        State = TaxiState.Carrying;
        FareMul = 1f;

        // 목적지: 다음 교차로 N 기준 앞 0..destMaxAhead칸, 옆 -destMaxSide..+destMaxSide칸 (N 자체는 제외)
        int h = heading;                                     // 대화는 회전 중에 시작되지 않는다
        int rh = (h + 1) & 3;
        int f = Random.Range(0, Mathf.Max(0, cfg.destMaxAhead) + 1);
        int r = Random.Range(-Mathf.Max(0, cfg.destMaxSide), Mathf.Max(0, cfg.destMaxSide) + 1);
        if (f == 0 && r == 0) f = 1;
        DestX = gx + DriveGame.HeadX[h] + f * DriveGame.HeadX[h] + r * DriveGame.HeadX[rh];
        DestY = gy + DriveGame.HeadY[h] + f * DriveGame.HeadY[h] + r * DriveGame.HeadY[rh];
        TripMeters = (Mathf.Abs(DestX - gx) + Mathf.Abs(DestY - gy)) * period;   // 맨해튼 칸수 × 80m. 요금/남은 거리 모두 이 기준
        Overshot = false;

        // 제한 시간: 거리 ÷ (지금 순항 속도 × 평균 비율) × 손님 급함 + 여유. 속도가 오른 판 후반에도 시간이 맞게 짧아진다
        float avgSpeed = Mathf.Max(4f, cruiseSpeed * Mathf.Clamp(cfg.limitSpeedRatio, 0.2f, 1.5f));
        TripLimit = TripMeters / avgSpeed * (Pax != null ? Pax.timeMul : 1f) + Mathf.Max(0f, cfg.timeBuffer) + TimeBonus;
        TripT = 0f;
        sick = sickCooldown = 0f;
        chatTimer = Random.Range(5f, 9f);
        LateWarned = false;

        peds.Remove(Caller);                                 // 손님은 차 안으로
        Caller = null;
        Popup("출발!", new Color(1f, 0.9f, 0.4f));
    }

    /// <summary>손님을 거절한다. 손님은 다시 인도를 걷는다.</summary>
    public void Decline()
    {
        if (Caller != null)
        {
            Caller.caller = false;
            Caller.walkZ = Random.Range(cfg.sidewalkWalkSpeed.x, cfg.sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        }
        Caller = null;
        ResetCallerTimer();
        State = TaxiState.Cruise;
    }

    void Arrive()
    {
        int fare = CurrentFare();
        if (Pax != null)
        {
            ThanksText = PaxName + ": \"" + PassengerCatalog.PickOne(TripT > TripLimit ? PassengerCatalog.LateThanks : Pax.thanks) + "\"";
            ThanksUntil = Time.unscaledTime + 3f;
        }
        int tip = CurrentTip();
        FareTotal += fare + tip;
        Trips++;
        Popup(tip > 0 ? "도착! +" + fare + "원  (팁 +" + tip + ")" : "도착! +" + fare + "원", new Color(1f, 0.85f, 0.3f));
        State = TaxiState.Cruise;
        ResetCallerTimer();
        if (Arrived != null) Arrived(fare + tip);
    }

    /// <summary>사람을 쳤다. 태운 손님이 놀라고 요금이 깎인다.</summary>
    public void OnPedHit()
    {
        if (State != TaxiState.Carrying) return;
        ReactText = PassengerCatalog.PickOne(Pax != null ? Pax.react : PassengerCatalog.PaxTypes[0].react);
        ReactIsChat = false;
        ReactUntil = Time.unscaledTime + 1.6f;
        float sense = Pax != null ? Pax.hitSense : 1f;
        float hitMul = Mathf.Clamp01(1f - (1f - cfg.hitFareMul) * sense);    // 유형이 민감할수록 더 깎는다
        FareMul = Mathf.Max(0.2f, FareMul * hitMul);
    }

    // ---- 요금 ----

    /// <summary>지금 정산하면 받을 요금 (HUD 와 정산이 같은 값을 쓴다).</summary>
    public int CurrentFare()
    {
        float typeMul = Pax != null ? Pax.fareMul : 1f;
        float mul = Mathf.Max(0.2f, FareMul * LateMul());                  // 사고/지나침/지각 감액을 합쳐도 20% 밑으로는 안 내려간다
        return Mathf.RoundToInt((cfg.baseFare + cfg.farePerMeter * TripMeters) * mul * typeMul * FareBonusMul / 10f) * 10;
    }

    // 제한 시간을 넘긴 만큼 요금에 곱하는 값 (제한 안이면 1)
    float LateMul()
    {
        float late = Mathf.Max(0f, TripT - TripLimit);
        return Mathf.Max(Mathf.Clamp01(cfg.lateMinMul), 1f - Mathf.Max(0f, cfg.lateDropPerSec) * late);
    }

    /// <summary>제한 시간을 일찍 마칠수록 큰 팁 (남은 시간이 제한의 절반 이상이면 최대).</summary>
    public int CurrentTip()
    {
        float left = TripLimit - TripT;
        if (left <= 0f || TripLimit <= 0f) return 0;
        float k = Mathf.Clamp01(left / (TripLimit * 0.5f));
        return Mathf.RoundToInt(Mathf.Max(0, cfg.tipMax) * k * (Pax != null ? Pax.tipMul : 1f) * TipBonusMul / 10f) * 10;
    }
}
