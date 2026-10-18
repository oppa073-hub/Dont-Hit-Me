using UnityEngine;

public enum TaxiState { Cruise, Talking, Carrying, Waiting }

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
    public bool PaxMale { get; private set; }                      // 손님이 남자인지 (비명 목소리)
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

    // ---- 은행 강도 인카운트: 1 은행으로 가는 중, 2 은행 앞에서 손님을 기다리는 중(떠나면 실패), 3 손님을 태우고 경찰에게서 도망치는 중 ----
    public int HeistStage { get; private set; }
    public float WaitT { get; private set; }
    public float TalkDist { get; private set; }            // 대화를 시작할 때 손님까지의 거리 (카메라 확대/기울기에 쓴다)
    float awayT;                            // 은행 자리를 벗어나 있던 시간 (잠깐은 봐준다)
    public bool HeistAllowed;               // DriveGame 이 정한다 (영업 시간이 모자라면 안 나온다)
    public bool ForceHeistNext;             // 시험용: 다음 손님을 은행 강도로
    public bool HeistFlee { get { return HeistStage == 3 && State == TaxiState.Carrying; } }
    public bool HeistLocked { get { return HeistStage == 3 && State == TaxiState.Talking; } }       // 도망 손님은 거절할 수 없다
    public event System.Action HeistFleeStarted, HeistWon;

    // ---- 손님 유별 사건 (폭탄, 결혼식, 외계인, 레이서, 폭주) ----
    public float Fuse { get; private set; }                // 폭탄 도화선 (1 안전 ~ 0 폭발)
    public float Happy { get; private set; }               // 결혼식 행복도 (1 ~ 0)
    public bool SteerFlip { get; private set; }            // 욱한 손님이 핸들을 잡고 있다 (조향 반대)
    public int SteerYank { get; private set; }             // 욱한 손님이 핸들을 확 잡아챈다 (-1 왼쪽, +1 오른쪽, 0 아님). 이때는 플레이어가 막을 수 없다
    int fightPhase;                                        // 0 평소, 1 소리치며 덤빔, 2 핸들을 잡힘(A/D 를 연타해 게이지를 채워야 풀림)
    public float MashL, MashR;                             // 핸들을 뿌리치는 게이지 (가운데에서 양쪽으로 찬다, 각각 0~1)
    public bool Grabbed { get { return fightPhase == 2; } }
    /// <summary>핸들을 잡힌 동안 A(-1)/D(+1) 를 한 번 눌렀다: 그쪽 게이지가 찬다.</summary>
    public void Mash(int dir)
    {
        if (fightPhase != 2) return;
        if (dir < 0) MashL = Mathf.Min(1f, MashL + cfg.fighterMashStep);
        else MashR = Mathf.Min(1f, MashR + cfg.fighterMashStep);
    }
    float fightT;
    public float AvgSpeed { get { return TripT > 1f ? speedSum / TripT : 0f; } }
    public bool AlienTrip { get { return State == TaxiState.Carrying && Pax != null && Pax.quirk == Quirk.Alien; } }
    public bool NoPenalty;                                 // 분노 중: 폭탄/부케로 요금이 깎이지 않는다 (DriveGame 이 매 프레임 알려 준다)
    public System.Func<float, bool> InIntersection;       // 앞쪽 z(m) 가 교차로 안인가 (DriveGame 이 알려 준다): 교차로 한가운데서 손님이 부르지 않게
    PaxType rolledPax;                                     // 손님이 나타날 때 미리 정한 유형 (외계인/강도는 처음부터 그 모습으로 나온다)
    public PaxType ForcePaxType;                           // 시험용: 다음 손님 유형을 고정
    public event System.Action BombExploded, BouquetThrown, WheelGrabbed;
    float speedSum, prevSpeed, ruffCool, flipEventT;
    int yankDir;

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
        HeistStage = 0;
        WaitT = 0f;
        ForceHeistNext = false;
        ClearQuirk();
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
        else if (State == TaxiState.Waiting)
        {
            // 은행 앞에서 손님을 기다린다: 이 자리를 벗어나면 실패, 시간이 차면 손님이 뛰어온다
            WaitT += dt;
            destRel(DestX, DestY, out int wf, out int wr, out float wsd);
            bool away = wr != 0 || Mathf.Abs(wsd) > Mathf.Max(1f, cfg.arriveRadius) * 2.2f;
            awayT = away ? awayT + dt : 0f;
            if (away && awayT - dt <= 0f) Popup("자리를 지키세요! 손님이 아직 안 나왔어요", new Color(1f, 0.7f, 0.3f));      // 벗어나는 순간 경고
            if (!turning && awayT > 1.5f) HeistFail();                                                           // 1.5초 넘게 벗어나 있으면 실패
            else if (WaitT >= cfg.heistWaitSeconds && !turning) BeginHeistReturn(spawnZ);
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
            bool nearDest = r == 0 && Mathf.Abs(sd) <= Mathf.Max(1f, cfg.arriveRadius) * 2.5f;
            float decel = dt > 0.0001f ? (prevSpeed - speed) / dt : 0f;
            prevSpeed = speed;
            speedSum += speed * dt;
            if (Pax != null && Pax.quirk != Quirk.None) TickQuirk(dt, speed, latSpeed, decel, turning, nearDest);
            if (!turning && r == 0)
            {
                float radius = Mathf.Max(1f, cfg.arriveRadius);
                if (speed <= cfg.pickupSpeed && Mathf.Abs(sd) <= radius)
                {
                    if (HeistStage == 1) BeginHeistWait();       // 은행에 도착: 손님은 내려서 들어가고, 기다려야 한다
                    else Arrive();
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

    void ClearQuirk()
    {
        Fuse = 1f;
        Happy = 1f;
        SteerFlip = false;
        SteerYank = 0;
        fightPhase = 0;
        MashL = MashR = 0f;
        ruffCool = 0f;
        speedSum = 0f;
        prevSpeed = 0f;
        flipEventT = Random.Range(6f, 10f);
    }

    // 손님 유별 사건 한 프레임 (태우고 가는 동안). 목적지 근처와 교차로 회전 중에는 봐준다.
    void TickQuirk(float dt, float speed, float latSpeed, float decel, bool turning, bool nearDest)
    {
        switch (Pax.quirk)
        {
            case Quirk.Bomb:
                if (turning || nearDest) break;
                if (speed < cfg.bombMinSpeed)
                {
                    Fuse -= dt * cfg.bombDrain;
                    if (Fuse <= 0f)
                    {
                        Fuse = 1f;
                        if (!NoPenalty) FareMul = Mathf.Max(0.2f, FareMul * 0.6f);
                        Popup("쾅!  폭탄이 터졌다…", new Color(1f, 0.4f, 0.3f));
                        if (BombExploded != null) BombExploded();
                    }
                }
                else Fuse = Mathf.Min(1f, Fuse + dt * 0.15f);
                break;

            case Quirk.Wedding:
                if (ruffCool > 0f) { ruffCool -= dt; break; }
                if (!turning && !nearDest && !NoPenalty && (latSpeed > 5.2f || decel > 30f))       // 거의 최대로 꺾거나 부딪혀서 확 서면 (평소 브레이크는 감속 20)
                {
                    ruffCool = 1.6f;
                    Happy = Mathf.Max(0f, Happy - 0.25f);
                    ReactText = PassengerCatalog.PickOne(Pax.react);
                    ReactIsChat = false;
                    ReactUntil = Time.unscaledTime + 1.6f;
                    Popup("부케가 날아갔다!", new Color(1f, 0.6f, 0.8f));
                    if (BouquetThrown != null) BouquetThrown();
                }
                break;

            case Quirk.Fighter:
                // 설명 없이 갑자기: 소리치며 덤빔(0.4초) -> 핸들을 확 잡아챔(0.7초, 막을 수 없음) -> 핸들을 쥔 채 반대로 돌림(몇 초)
                if (turning) break;
                if (fightPhase == 0)
                {
                    flipEventT -= dt;
                    if (flipEventT <= 0f)
                    {
                        fightPhase = 1;
                        fightT = 0.4f;
                        yankDir = Random.value < 0.5f ? -1 : 1;
                        ReactText = PassengerCatalog.PickOne(Pax.react);          // 손님의 고함 (말풍선)
                        ReactIsChat = true;
                        ReactUntil = Time.unscaledTime + 1.8f;
                        if (WheelGrabbed != null) WheelGrabbed();
                    }
                }
                else if (fightPhase == 1)
                {
                    fightT -= dt;
                    if (fightT <= 0f)
                    {
                        fightPhase = 2;                                           // 핸들을 잡혔다: 차는 한쪽으로 쏠리고, A/D 를 연타해 게이지를 채워야 풀린다
                        fightT = Mathf.Max(2f, cfg.fighterYankMax);
                        SteerYank = yankDir;
                        MashL = MashR = 0f;
                    }
                }
                else
                {
                    fightT -= dt;
                    // 채우던 쪽은 서서히 줄어들어서 계속 눌러야 한다 (다 찬 쪽은 그대로)
                    if (MashL < 1f) MashL = Mathf.Max(0f, MashL - cfg.fighterMashLeak * dt);
                    if (MashR < 1f) MashR = Mathf.Max(0f, MashR - cfg.fighterMashLeak * dt);
                    bool freed = MashL >= 1f && MashR >= 1f;
                    if (freed || fightT <= 0f)
                    {
                        fightPhase = 0;
                        SteerYank = 0;
                        MashL = MashR = 0f;
                        flipEventT = Random.Range(6f, 10f);
                        if (freed) Popup("뿌리쳤다!", new Color(0.5f, 1f, 0.6f));
                    }
                }
                break;
        }
    }

    // 손님 유별 요금 배율: 폭탄 x2(장난감), 외계인 x1.5, 레이서는 평균 속도가 빠를수록 최대 x2
    float QuirkFareMul()
    {
        if (Pax == null) return 1f;
        switch (Pax.quirk)
        {
            case Quirk.Bomb: return 2f;
            case Quirk.Alien: return 1.5f;
            case Quirk.Racer: return 1f + Mathf.Clamp01((AvgSpeed - 14f) / 16f);
            default: return 1f;
        }
    }

    /// <summary>보너스 요금(라이벌 택시 격파 등)을 받는다.</summary>
    public void AddBonusFare(int amount)
    {
        FareTotal += Mathf.Max(0, amount);
    }

    /// <summary>라이벌 택시가 이 손님을 가로챘다. 아직 태우기 전이면 손님이 사라지고 true.</summary>

    public bool StealCaller(PedestrianManager.Ped p)
    {
        if (State != TaxiState.Cruise || Caller == null || Caller != p) return false;
        peds.Remove(p);
        Caller = null;
        ResetCallerTimer();
        return true;
    }

    void MakeCaller(float spawnZ)
    {
        var p = peds.Spawn(spawnZ, 0, ClearOfIntersection(cfg.callerDistance, spawnZ));
        p.caller = true;
        p.walkZ = 0f;                                        // 연석에서 제자리
        p.x = p.side * (cfg.roadHalfWidth + 1.2f);
        rolledPax = RollPax();
        GiveCallerArt(p, rolledPax);
        Caller = p;
        if (CallerAppeared != null) CallerAppeared();
    }

    // 손님의 모습: 외계인 손님은 처음부터 외계인, 은행 강도는 처음부터 강도 그림, 나머지는 걷는 사람과 서 있는 사람 중 아무나 (그림이 없으면 기존 그림 그대로)
    static void GiveCallerArt(PedestrianManager.Ped p, PaxType pax)
    {
        PixelSet a = null;
        if (pax != null && pax.heist) a = PixelArt.Get("robber");
        else if (pax != null && pax.quirk == Quirk.Alien) a = PixelArt.Get("alien");
        if (a == null) a = PixelArt.RandomMix("npc", "stand");
        if (a == null) return;
        p.art = a;
        p.baseColor = Color.white;
        p.sr.sprite = a.First;
        p.sr.color = Color.white;
    }

    // 손님 유형을 정한다 (가끔 수상한 손님=은행 강도). 시험용 고정 값도 여기서 쓴다.
    PaxType RollPax()
    {
        bool heist = HeistAllowed && HeistStage == 0 && (ForceHeistNext || Random.value < cfg.heistChance);
        ForceHeistNext = false;
        var t = heist ? PassengerCatalog.Heist : PassengerCatalog.PickType();
        if (ForcePaxType != null)
        {
            t = ForcePaxType;                                // 시험용 고정
            ForcePaxType = null;
        }
        return t;
    }

    // 교차로 안(옆길 한가운데)이 아닌 자리로 손님 위치를 옮긴다: 교차로 구간의 인도 상자는 옆길 아스팔트라서 거기서 부르면 도로 한복판이다
    float ClearOfIntersection(float z0, float spawnZ)
    {
        if (InIntersection == null) return z0;
        const float Strip = 4f;
        for (int k = 0; k < 16; k++)
        {
            float z = z0 + ((k + 1) / 2) * Strip * (k % 2 == 1 ? 1f : -1f);
            if (z < 18f || z > spawnZ + 25f) continue;
            if (!InIntersection(z - Strip) && !InIntersection(z) && !InIntersection(z + Strip)) return z;
        }
        return z0;
    }

    void BeginTalk()
    {
        State = TaxiState.Talking;
        Pax = rolledPax ?? RollPax();                        // 손님이 나타날 때 이미 정해 둔 유형
        rolledPax = null;
        PaxName = PassengerCatalog.NameFor(Caller.art, Pax);
        PaxMale = PassengerCatalog.IsMale(Caller.art);
        DestName = PassengerCatalog.PickOne(Pax.dests);
        PaxLine = string.Format(PassengerCatalog.PickOne(Pax.hello), DestName);
        PaxSkin = PassengerCatalog.SkinTones[Random.Range(0, PassengerCatalog.SkinTones.Length)];
        PaxHair = PassengerCatalog.HairTones[Pax.hair >= 0 ? Pax.hair % PassengerCatalog.HairTones.Length : Random.Range(0, PassengerCatalog.HairTones.Length)];
        PaxShirt = Caller.art != null ? Random.ColorHSV(0f, 1f, 0.5f, 0.7f, 0.8f, 1f) : Caller.baseColor;       // 룸미러 얼굴의 셔츠 색
        FaceCaller();
    }

    // 손님 쪽(왼쪽/오른쪽)으로 시점을 돌린다
    void FaceCaller()
    {
        // 손님이 정확히 시야 가운데 오도록: 차보다 뒤에 있어도(z < 0) 그쪽으로 몸을 돌린다. 거리는 확대 정도를 정하는 데 쓴다.
        float dx = Caller.x - (carX - 0.4f);
        float yaw = Mathf.Atan2(dx, Caller.z) * Mathf.Rad2Deg;
        TalkDist = Mathf.Sqrt(dx * dx + Caller.z * Caller.z);
        if (TalkStarted != null) TalkStarted(yaw);
    }

    // ---- 은행 강도 ----

    void BeginHeistWait()
    {
        HeistStage = 2;
        State = TaxiState.Waiting;
        WaitT = 0f;
        awayT = 0f;
        ThanksText = PaxName + ": \"" + PassengerCatalog.PickOne(PassengerCatalog.HeistWaitLines) + "\"";
        ThanksUntil = Time.unscaledTime + 3f;
        Popup("손님이 은행으로 들어갔다…  기다리세요!", new Color(1f, 0.85f, 0.3f));
    }

    void HeistFail()
    {
        HeistStage = 0;
        State = TaxiState.Cruise;
        Pax = null;
        ThanksUntil = 0f;                                    // 기다리라던 대사가 남지 않게
        ResetCallerTimer();
        Popup("손님을 버리고 떠났다…  의뢰 실패!", new Color(1f, 0.5f, 0.4f));
    }

    // 시간이 찼다: 손님이 가방을 끌어안고 뛰어와 차에 매달린다 (옆 시점 대화, 거절 불가)
    void BeginHeistReturn(float spawnZ)
    {
        float side = Random.value < 0.5f ? -1f : 1f;
        var p = peds.Spawn(spawnZ, 0, 1.5f, side);
        p.caller = true;
        p.walkZ = 0f;
        p.x = side * (cfg.roadHalfWidth + 1.2f);
        GiveCallerArt(p, PassengerCatalog.Heist);
        if (p.art == null)
        {
            p.baseColor = PaxShirt;
            p.sr.color = PaxShirt;
        }
        Caller = p;
        DestName = PassengerCatalog.PickOne(PassengerCatalog.HeistHideouts);
        PaxLine = string.Format(PassengerCatalog.PickOne(PassengerCatalog.HeistReturnLines), DestName);
        HeistStage = 3;
        State = TaxiState.Talking;
        FaceCaller();
    }

    /// <summary>손님을 태운다. 목적지를 정하고 제한 시간을 계산한다. (gx, gy) = 방금 지난 교차로, heading = 진행 방향, period = 교차로 간격(m), cruiseSpeed = 지금 순항 속도.</summary>
    public void Accept(int gx, int gy, int heading, float period, float cruiseSpeed)
    {
        State = TaxiState.Carrying;
        FareMul = 1f;

        // 목적지: 방금 지난 교차로에서 tripMinBlocks ~ tripMaxBlocks 칸(1칸 = 80m) 떨어진 곳. 앞으로 간 칸과 옆으로 간 칸으로 나눈다.
        // (다음 교차로 N 이 이미 앞으로 1칸이라 앞 칸수 f = 전체 - 1 - 옆 칸수)
        int h = heading;                                     // 대화는 회전 중에 시작되지 않는다
        int rh = (h + 1) & 3;
        bool alienTrip = Pax != null && Pax.quirk == Quirk.Alien;
        int minB = Mathf.Max(2, cfg.tripMinBlocks);
        int maxB = Mathf.Max(minB, cfg.tripMaxBlocks);
        int total = Random.Range(minB, maxB + 1);
        int sideBlocks = alienTrip ? 0 : Random.Range(0, Mathf.Min(total - 1, Mathf.Max(0, cfg.tripSideBlocks)) + 1);
        int f = total - 1 - sideBlocks;
        int r = sideBlocks == 0 ? 0 : (Random.value < 0.5f ? -sideBlocks : sideBlocks);
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
        ClearQuirk();
        if (Pax != null && Pax.heist && HeistStage == 0) HeistStage = 1;              // 수상한 손님을 태웠다: 먼저 은행으로
        bool fleeing = HeistStage == 3;
        if (fleeing) TripLimit *= Mathf.Max(0.2f, cfg.heistTimeMul);                  // 도망 중이라 시간이 빠듯하다

        peds.Remove(Caller);                                 // 손님은 차 안으로
        Caller = null;
        Popup(fleeing ? "출발!  경찰이 쫓아온다!" : "출발!", new Color(1f, 0.9f, 0.4f));
        if (fleeing && HeistFleeStarted != null) HeistFleeStarted();
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
        bool escaped = HeistStage == 3;                       // 도주 성공 (요금이 몇 배)
        int fare = CurrentFare();
        if (Pax != null)
        {
            ThanksText = PaxName + ": \"" + PassengerCatalog.PickOne(TripT > TripLimit ? PassengerCatalog.LateThanks : Pax.thanks) + "\"";
            ThanksUntil = Time.unscaledTime + 3f;
        }
        int tip = CurrentTip();
        FareTotal += fare + tip;
        Trips++;
        Popup(escaped ? "도주 성공!  +" + (fare + tip) + "원" : (tip > 0 ? "도착! +" + fare + "원  (팁 +" + tip + ")" : "도착! +" + fare + "원"), new Color(1f, 0.85f, 0.3f));
        State = TaxiState.Cruise;
        HeistStage = 0;
        ClearQuirk();
        ResetCallerTimer();
        if (escaped && HeistWon != null) HeistWon();
        if (Arrived != null) Arrived(fare + tip);
    }

    /// <summary>태운 손님이 한마디 한다 (미니건이 튀어나올 때 같은 상황).</summary>
    public void Say(string text, float seconds)
    {
        if (State != TaxiState.Carrying) return;
        ReactText = text;
        ReactIsChat = true;
        ReactUntil = Time.unscaledTime + seconds;
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
        float heistMul = HeistStage == 3 ? Mathf.Max(1f, cfg.heistFareMul) : 1f;       // 도망 중인 손님은 요금이 몇 배
        return Mathf.RoundToInt((cfg.baseFare + cfg.farePerMeter * TripMeters) * mul * typeMul * heistMul * QuirkFareMul() * FareBonusMul / 10f) * 10;
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
        int tip = Mathf.RoundToInt(Mathf.Max(0, cfg.tipMax) * k * (Pax != null ? Pax.tipMul : 1f) * TipBonusMul / 10f) * 10;
        if (Pax != null && Pax.quirk == Quirk.Wedding) tip += Mathf.RoundToInt(1200f * Happy / 10f) * 10;       // 행복하게 도착하면 축의금
        return tip;
    }
}
