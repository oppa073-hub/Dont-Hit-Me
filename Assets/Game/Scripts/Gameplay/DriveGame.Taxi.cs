using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 택시: 손님 호출, 정차, 대화, 탑승, 도착, 요금/팁/제한 시간, 손님 유형 표
public partial class DriveGame
{
    enum TaxiState { Cruise, Talking, Carrying }

    // 택시 상태
    TaxiState taxi;

    Ped caller;

    float callerTimer, camYaw, camYawTarget, tripMeters, fareMul = 1f, reactUntil;

    int fareTotal, trips;

    bool overshot;                       // 이번 트립에서 이미 '지나침' 감액을 했는지

    string paxName = "", paxLine = "", destName = "", reactText = "", thanksText = "";

    PaxType pax;                         // 지금 태운/대화 중인 손님의 유형

    float thanksUntil;

    float tripLimit, tripT;              // 이번 트립의 제한 시간 / 탑승 후 지난 시간(초)

    bool lateWarned;                     // 지각 안내를 이미 했는지

    static readonly string[] LateThanks = { "많이 늦었네요… 다음엔 좀 서둘러 주세요.", "약속 시간에 늦었잖아요. 아쉽네요.", "늦었지만 데려다줘서 고마워요." };

    Color paxSkin, paxHair, paxShirt;

    Transform destMarker;

    // 손님 유형 표: 여기만 고치면 이름/대사/목적지/요금이 바뀐다. hello 의 {0} 은 목적지 이름.
    // 조사 때문에 {0} 뒤에는 "까지"처럼 받침과 상관없는 말만 쓴다 (예: "{0}까지 가 주세요").
    class PaxType
    {
        public string[] names, dests, hello, react, thanks;
        public float fareMul = 1f;       // 이 유형의 기본 요금 배율
        public float hitSense = 1f;      // 사람을 쳤을 때 감액이 얼마나 커지는지 (1 = hitFareMul 그대로, 클수록 더 깎음)
        public float timeMul = 1f;       // 허용 시간 배율 (작을수록 급한 손님)
        public float tipMul = 1f;        // 팁 배율
        public int hair = -1;            // HairTones 번호. -1 이면 무작위
        public int weight = 2;           // 나타날 확률 가중치
    }

    static readonly PaxType[] PaxTypes =
    {
        new PaxType   // 회사원: 요금 높고 사고에 엄격하다
        {
            names = new[] { "김 사장님", "정 부장님", "박 과장님" },
            dests = new[] { "시청", "공항", "호텔", "중앙역" },
            hello = new[] { "{0}까지 가 주세요. 미팅에 늦었어요.", "기사님, {0}까지. 급합니다.", "{0}까지요. 최대한 빨리, 하지만 안전하게." },
            react = new[] { "이봐요, 운전 똑바로 합시다!", "지금 뭐 하시는 겁니까!", "사람을 칠 뻔했잖아요!" },
            thanks = new[] { "수고했어요. 잔돈은 됐습니다.", "시간 맞춰 왔군요. 좋아요." },
            fareMul = 1.2f, hitSense = 1.3f, timeMul = 0.85f, tipMul = 1.5f
        },
        new PaxType   // 할머니: 느긋하고 놀람이 크다
        {
            names = new[] { "박 할머니", "최 할머니", "윤 할머니" },
            dests = new[] { "대학병원", "시장", "공원 입구" },
            hello = new[] { "아이고, 택시 잡았네. {0}까지 천천히 가 주오.", "기사 양반, {0}까지 가 주시게.", "{0}에 가요. 서두르지 말아요." },
            react = new[] { "아이고 깜짝이야!", "아이구, 이게 무슨 일이래!", "아이고 놀래라!!" },
            thanks = new[] { "고마워요, 젊은이. 복 받을 거야.", "안전하게 데려다줘서 고맙소." },
            fareMul = 1.0f, hitSense = 1.0f, hair = 2, timeMul = 1.3f, tipMul = 0.8f
        },
        new PaxType   // 학생: 요금은 조금 낮고, 리액션은 크지만 사고 감액은 작다
        {
            names = new[] { "이 학생", "한 학생", "서 학생" },
            dests = new[] { "도서관", "중앙역", "시청" },
            hello = new[] { "기사님, {0}까지요! 빨리빨리요!", "{0}까지 가 주세요! 수업 늦었어요!", "{0}! 지각이에요 지각!" },
            react = new[] { "헐 뭐야!! 방금 뭐 쳤어요?", "꺄악!! 기사님!!", "으아악!! 대박!!" },
            thanks = new[] { "감사합니다! 별점 5점 드릴게요!", "덕분에 안 늦었어요! 감사합니다!" },
            fareMul = 0.9f, hitSense = 0.8f, timeMul = 0.85f, tipMul = 0.5f
        },
        new PaxType   // 아저씨
        {
            names = new[] { "최 아저씨", "오 아저씨", "강 아저씨" },
            dests = new[] { "시장", "중앙역", "공원 입구", "시청" },
            hello = new[] { "어이, {0}까지 갑시다.", "{0}까지 가요. 막히지 않게 잘 가 봅시다.", "택시! {0}까지 부탁해요." },
            react = new[] { "어허, 운전 조심해!", "에이, 이 사람아!", "거 운전 좀 살살 합시다!" },
            thanks = new[] { "수고했소.", "잘 왔네. 고마워요." },
            fareMul = 1.0f, hitSense = 1.0f
        },
        new PaxType   // 아주머니
        {
            names = new[] { "한 아주머니", "송 아주머니", "조 아주머니" },
            dests = new[] { "시장", "대학병원", "도서관", "공원 입구" },
            hello = new[] { "기사님, {0}까지요. 애들 데리러 가야 해요.", "{0}까지 부탁드려요. 장 본 게 무거워서요.", "어휴, 잡았다. {0}요, 천천히 안전하게요." },
            react = new[] { "꺄악!! 기사님!!", "어머, 어머! 조심하세요!!", "방금 사람 쳤어요!?" },
            thanks = new[] { "안전하게 와서 고마워요.", "고마워요, 기사님. 수고하세요." },
            fareMul = 1.0f, hitSense = 1.0f
        },
        new PaxType   // VIP: 요금이 매우 높지만 사고에 아주 민감하다
        {
            names = new[] { "VIP 손님", "이사장님", "관광객 스미스" },
            dests = new[] { "공항", "호텔", "시청" },
            hello = new[] { "{0}까지 부탁합니다. 아주 부드럽게요.", "{0}까지. 승차감이 중요합니다.", "{0}! Please, smooth ride." },
            react = new[] { "오 마이 갓!!", "이게 무슨 무례한 운전입니까!", "Stop! 방금 뭐였죠!?" },
            thanks = new[] { "Excellent! 훌륭한 운전이었어요.", "만족합니다. 수고했어요." },
            fareMul = 1.5f, hitSense = 1.7f, weight = 1, timeMul = 0.9f, tipMul = 2f
        },
    };

    static readonly Color[] SkinTones =
    {
        new Color(0.96f, 0.80f, 0.66f), new Color(0.88f, 0.68f, 0.52f), new Color(0.72f, 0.52f, 0.38f), new Color(0.98f, 0.86f, 0.76f)
    };

    static readonly Color[] HairTones =
    {
        new Color(0.10f, 0.08f, 0.07f), new Color(0.32f, 0.20f, 0.12f), new Color(0.75f, 0.72f, 0.70f), new Color(0.55f, 0.35f, 0.15f)
    };

    // ---------- 택시: 손님 호출 / 정차 / 대화 / 탑승 / 도착 ----------

    void UpdateTaxi(float dt)
    {
        // 손님이 사라졌다(지나쳐서 제거됨)
        if (caller != null && (caller.t == null || !peds.Contains(caller)))
        {
            caller = null;
            callerTimer = Random.Range(callerDelay.x, callerDelay.y);
            if (taxi == TaxiState.Cruise) Popup("손님을 놓쳤다", new Color(0.8f, 0.8f, 0.8f));
        }

        if (taxi == TaxiState.Cruise)
        {
            if (caller == null)
            {
                callerTimer -= dt;
                if (callerTimer <= 0f) MakeCaller();
            }
            else if (!turning && speed <= pickupSpeed && caller.z > -2.5f && caller.z < 9f && Mathf.Abs(caller.x - carX) < pickupLateral)
            {
                BeginTalk();
            }
        }
        else if (taxi == TaxiState.Carrying)
        {
            // 제한 시간 (대화/히트스톱 중에는 dt 가 0 이거나 이 함수가 안 불려서 흐르지 않는다)
            tripT += dt;
            if (!lateWarned && tripT > tripLimit)
            {
                lateWarned = true;
                Popup("늦었어요! 요금이 깎입니다", new Color(1f, 0.6f, 0.4f));
            }

            // 회전 중에는 판정하지 않는다. 목적지 줄(r==0) 위에서만 도착/지나침을 본다.
            DestRel(out int f, out int r, out float sd);
            if (!turning && r == 0)
            {
                float radius = Mathf.Max(1f, arriveRadius);
                if (speed <= pickupSpeed && Mathf.Abs(sd) <= radius)
                {
                    Arrive();
                }
                else if (sd < -radius && !overshot)
                {
                    // 지나쳤다: 트립당 한 번만 요금을 깎는다. 목적지는 그대로 두고 옆길로 돌아와 다시 접근한다.
                    overshot = true;
                    Popup("목적지를 지나쳤어요!", new Color(1f, 0.5f, 0.4f));
                    fareMul = Mathf.Max(0.2f, fareMul - 0.2f);
                }
            }
        }
    }

    void MakeCaller()
    {
        var p = SpawnPed(0, callerDistance);
        p.caller = true;
        p.walkZ = 0f;                                        // 연석에서 제자리
        p.x = p.side * (roadHalfWidth + 1.2f);
        caller = p;
    }

    static string PickOne(string[] a)
    {
        return a[Random.Range(0, a.Length)];
    }

    // 가중치에 따라 손님 유형을 하나 뽑는다
    static PaxType PickPaxType()
    {
        int total = 0;
        foreach (var t in PaxTypes) total += Mathf.Max(0, t.weight);
        int roll = Random.Range(0, Mathf.Max(1, total));
        foreach (var t in PaxTypes)
        {
            roll -= Mathf.Max(0, t.weight);
            if (roll < 0) return t;
        }
        return PaxTypes[0];
    }

    // 지금 정산하면 받을 요금 (HUD 와 정산이 같은 값을 쓴다)
    int CurrentFare()
    {
        float typeMul = pax != null ? pax.fareMul : 1f;
        float mul = Mathf.Max(0.2f, fareMul * LateMul());                  // 사고/지나침/지각 감액을 합쳐도 20% 밑으로는 안 내려간다
        return Mathf.RoundToInt((baseFare + farePerMeter * tripMeters) * mul * typeMul / 10f) * 10;
    }

    // 제한 시간을 넘긴 만큼 요금에 곱하는 값 (제한 안이면 1)
    float LateMul()
    {
        float late = Mathf.Max(0f, tripT - tripLimit);
        return Mathf.Max(Mathf.Clamp01(lateMinMul), 1f - Mathf.Max(0f, lateDropPerSec) * late);
    }

    // 제한 시간을 일찍 마칠수록 큰 팁 (남은 시간이 제한의 절반 이상이면 최대)
    int CurrentTip()
    {
        float left = tripLimit - tripT;
        if (left <= 0f || tripLimit <= 0f) return 0;
        float k = Mathf.Clamp01(left / (tripLimit * 0.5f));
        return Mathf.RoundToInt(Mathf.Max(0, tipMax) * k * (pax != null ? pax.tipMul : 1f) / 10f) * 10;
    }

    void BeginTalk()
    {
        taxi = TaxiState.Talking;
        speed = 0f;
        pax = PickPaxType();
        paxName = PickOne(pax.names);
        destName = PickOne(pax.dests);
        paxLine = string.Format(PickOne(pax.hello), destName);
        paxSkin = SkinTones[Random.Range(0, SkinTones.Length)];
        paxHair = HairTones[pax.hair >= 0 ? pax.hair % HairTones.Length : Random.Range(0, HairTones.Length)];
        paxShirt = caller.baseColor;

        // 손님 쪽(왼쪽/오른쪽)으로 시점을 돌린다
        float dx = caller.x - (carX - 0.4f);
        camYawTarget = Mathf.Atan2(dx, Mathf.Max(0.5f, caller.z)) * Mathf.Rad2Deg;
    }

    void AcceptPassenger()
    {
        taxi = TaxiState.Carrying;
        fareMul = 1f;

        // 목적지: 다음 교차로 N 기준 앞 0..destMaxAhead칸, 옆 -destMaxSide..+destMaxSide칸 (N 자체는 제외)
        int h = heading;                                     // 대화는 회전 중에 시작되지 않는다
        int rh = (h + 1) & 3;
        int f = Random.Range(0, Mathf.Max(0, destMaxAhead) + 1);
        int r = Random.Range(-Mathf.Max(0, destMaxSide), Mathf.Max(0, destMaxSide) + 1);
        if (f == 0 && r == 0) f = 1;
        destX = gx + HeadX[h] + f * HeadX[h] + r * HeadX[rh];
        destY = gy + HeadY[h] + f * HeadY[h] + r * HeadY[rh];
        tripMeters = (Mathf.Abs(destX - gx) + Mathf.Abs(destY - gy)) * Period;   // 맨해튼 칸수 × 80m. 요금/남은 거리 모두 이 기준
        overshot = false;
        hintF = int.MinValue;

        // 제한 시간: 거리 ÷ (지금 순항 속도 × 평균 비율) × 손님 급함 + 여유. 속도가 오른 판 후반에도 시간이 맞게 짧아진다
        float cruise = Mathf.Min(maxSpeed, baseSpeed + elapsed * speedRamp);
        float avgSpeed = Mathf.Max(4f, cruise * Mathf.Clamp(limitSpeedRatio, 0.2f, 1.5f));
        tripLimit = tripMeters / avgSpeed * (pax != null ? pax.timeMul : 1f) + Mathf.Max(0f, timeBuffer);
        tripT = 0f;
        lateWarned = false;

        int idx = peds.IndexOf(caller);
        if (idx >= 0) Remove(idx);                           // 손님은 차 안으로
        caller = null;
        camYawTarget = 0f;
        Popup("출발!", new Color(1f, 0.9f, 0.4f));
    }

    void DeclinePassenger()
    {
        if (caller != null)
        {
            caller.caller = false;
            caller.walkZ = Random.Range(sidewalkWalkSpeed.x, sidewalkWalkSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        }
        caller = null;
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
        taxi = TaxiState.Cruise;
        camYawTarget = 0f;
    }

    void Arrive()
    {
        int fare = CurrentFare();
        if (pax != null)
        {
            thanksText = paxName + ": \"" + PickOne(tripT > tripLimit ? LateThanks : pax.thanks) + "\"";
            thanksUntil = Time.unscaledTime + 3f;
        }
        int tip = CurrentTip();
        fareTotal += fare + tip;
        trips++;
        score += Mathf.RoundToInt((fare + tip) / 100f);
        Popup(tip > 0 ? "도착! +" + fare + "원  (팁 +" + tip + ")" : "도착! +" + fare + "원", new Color(1f, 0.85f, 0.3f));
        taxi = TaxiState.Cruise;
        callerTimer = Random.Range(callerDelay.x, callerDelay.y);
    }
}
