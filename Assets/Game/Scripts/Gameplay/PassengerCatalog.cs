using UnityEngine;

// 손님 유형 한 종류의 데이터. 값은 PassengerCatalog.PaxTypes 표에서 고친다.
// 손님 유형 표: 여기만 고치면 이름/대사/목적지/요금이 바뀐다. hello 의 {0} 은 목적지 이름.
// 조사 때문에 {0} 뒤에는 "까지"처럼 받침과 상관없는 말만 쓴다 (예: "{0}까지 가 주세요").
public class PaxType
{
    public string[] names, dests, hello, react, thanks;
    public float fareMul = 1f;       // 이 유형의 기본 요금 배율
    public float hitSense = 1f;      // 사람을 쳤을 때 감액이 얼마나 커지는지 (1 = hitFareMul 그대로, 클수록 더 깎음)
    public float timeMul = 1f;       // 허용 시간 배율 (작을수록 급한 손님)
    public float tipMul = 1f;        // 팁 배율
    public int hair = -1;            // HairTones 번호. -1 이면 무작위
    public int weight = 2;           // 나타날 확률 가중치
    public string[] chat;            // 타고 가는 동안 가끔 걸어 오는 말 (없으면 조용한 손님)
    public float swerveSense = 0f;   // 급하게 핸들을 꺾을 때 멀미하는 정도 (0 = 상관없음, 클수록 쉽게 멀미)
    public bool heist;               // 은행 강도 손님 (랜덤 인카운트로만 나온다. weight 는 0)
}

// 손님 데이터 표: 여기만 고치면 이름/대사/목적지/요금이 바뀐다. (대사를 늘리려면 이 파일만 열면 된다)
public static class PassengerCatalog
{
    public static readonly PaxType[] PaxTypes =
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
        new PaxType   // 수다쟁이: 가는 동안 계속 말을 걸고, 대신 팁이 후하다
        {
            names = new[] { "홍 언니", "백 오빠", "마 작가님" },
            dests = new[] { "시장", "도서관", "공원 입구", "호텔" },
            hello = new[] { "기사님 안녕하세요~ {0}까지요! 가면서 얘기나 해요.", "{0}까지 가 주세요! 오늘 날씨 진짜 좋죠?" },
            react = new[] { "헉, 지금 뭐예요!?", "어머 놀라라!", "기사님 괜찮아요!?" },
            thanks = new[] { "얘기 즐거웠어요! 또 타요~", "수다 떠느라 시간 가는 줄 몰랐네요!" },
            chat = new[] { "기사님은 이 일 오래 하셨어요?", "저번에 탄 택시는 말이 없어서요~", "요즘 물가 진짜 올랐죠?", "저기 새로 생긴 가게 아세요?" },
            fareMul = 1.0f, hitSense = 0.9f, tipMul = 1.6f
        },
        new PaxType   // 서두르는 손님: 시간이 아주 짧지만 요금과 팁이 높다
        {
            names = new[] { "급한 김 씨", "박 배달원", "조 대리" },
            dests = new[] { "공항", "중앙역", "시청" },
            hello = new[] { "{0}까지! 제발 빨리요, 비행기 놓쳐요!", "{0}까지요! 최대한 빨리 부탁합니다!" },
            react = new[] { "아 뭐예요! 시간 없다고요!", "으악, 늦겠어요!", "지금 장난해요!?" },
            thanks = new[] { "세이프! 정말 고마워요!", "덕분에 안 늦었어요!" },
            fareMul = 1.3f, hitSense = 1.0f, timeMul = 0.6f, tipMul = 1.5f, weight = 1
        },
        new PaxType   // 멀미하는 손님: 핸들을 급하게 꺾으면 요금이 깎인다
        {
            names = new[] { "창백한 이 씨", "멀미 많은 최 씨" },
            dests = new[] { "대학병원", "공원 입구", "도서관" },
            hello = new[] { "{0}까지요… 저 멀미가 심해서 핸들 살살 돌려 주세요…", "{0}까지 부탁해요. 급하게 틀면 안 돼요… 우웩." },
            react = new[] { "우웩… 살살 좀 몰아요…", "으… 속이 울렁거려요…", "핸들 좀 부드럽게요!" },
            thanks = new[] { "살았다… 감사합니다.", "부드럽게 와줘서 고마워요." },
            fareMul = 1.1f, hitSense = 1.0f, swerveSense = 0.25f, weight = 1
        },
        new PaxType   // 수상한 손님: 은행까지 태워 가서 기다리게 하고, 나오면 경찰에 쫓긴다 (랜덤 인카운트, 평소에는 안 나옴)
        {
            names = new[] { "선글라스 손님", "마스크 손님", "큰 가방 손님" },
            dests = new[] { "은행" },
            hello = new[]
            {
                "{0}까지 가 주세요. 잠깐 볼일만 보고 올 거예요. 복면은… 패션이에요.",
                "{0}까지요! 가방이 좀 큰데 빈 거예요. 진짜로. 나올 땐 무거워지겠지만.",
                "{0}까지 부탁해요. 기사님, 입 무거우시죠? 아, 그냥 물어봤어요.",
            },
            react = new[] { "으악, 눈에 안 띄게 가자니까요!", "경찰 오면 어쩌려고요!", "조용히 좀 몰아요, 조용히!" },
            thanks = new[] { "(헉헉) 고마워요! 이건 비밀 팁이에요. 우리 만난 적 없죠?", "도주 성공! 이 돈은… 음, 세탁해야겠네요." },
            fareMul = 1.2f, hitSense = 1.0f, tipMul = 3f, weight = 0, heist = true
        },
    };

    // 은행 강도 인카운트 대사
    public static readonly string[] HeistHideouts = { "비밀 아지트", "이모네 집", "폐공장", "역 앞 코인락커" };

    public static readonly string[] HeistReturnLines =
    {
        "(헉헉) 출발! 출발해요!! 경찰이 와요!! {0}까지 빨리빨리!!",
        "가자 가자 가자!! 뒤돌아보지 마요!! {0}까지요!!",
        "기사님 사랑해요!! 아무튼 {0}까지 전속력으로!!",
    };

    public static readonly string[] HeistWaitLines =
    {
        "금방 올게요! 여기서 꼼짝 말고 기다려 주세요!",
        "5분이면 돼요. 아, 시동은 끄지 마시고요.",
    };

    /// <summary>은행 강도 손님 유형.</summary>
    public static PaxType Heist
    {
        get
        {
            foreach (var t in PaxTypes) if (t.heist) return t;
            return PaxTypes[0];
        }
    }

    public static readonly Color[] SkinTones =
    {
        new Color(0.96f, 0.80f, 0.66f), new Color(0.88f, 0.68f, 0.52f), new Color(0.72f, 0.52f, 0.38f), new Color(0.98f, 0.86f, 0.76f)
    };

    public static readonly Color[] HairTones =
    {
        new Color(0.10f, 0.08f, 0.07f), new Color(0.32f, 0.20f, 0.12f), new Color(0.75f, 0.72f, 0.70f), new Color(0.55f, 0.35f, 0.15f)
    };

    public static readonly string[] LateThanks = { "많이 늦었네요… 다음엔 좀 서둘러 주세요.", "약속 시간에 늦었잖아요. 아쉽네요.", "늦었지만 데려다줘서 고마워요." };

    public static string PickOne(string[] a)
    {
        return a[Random.Range(0, a.Length)];
    }

    // 가중치에 따라 손님 유형을 하나 뽑는다
    public static PaxType PickType()
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
}
