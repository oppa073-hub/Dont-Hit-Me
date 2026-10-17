using UnityEngine;

// 스토리 대사 모음: 정비소 NPC(박 정비사)의 대사, 택시 개조 대사, 아침 뉴스 원고. 분위기는 병맛 막장.
// 대사를 고치거나 늘리려면 이 파일만 열면 된다. 뉴스 원고의 줄바꿈은 | 로 적는다 (한 줄은 13글자 안쪽, 한 항목은 4줄 안쪽).
public static class StoryCatalog
{
    public const string NpcName = "박 정비사";

    // 처음 로비에 왔을 때 (한 번만)
    public static readonly string[] Intro =
    {
        "어서 와, 신입! 저 택시? 삼촌이 노름빚 대신 두고 간 거야. 브레이크는 의지로 밟게.",
        "이 동네는 말이야, 괴수가 편의점 도시락을 털고 히어로랑 빌런은 하늘에서 눈에서 레이저 쏘며 부부싸움을 한다네.",
        "손님은 태워야 월세가 나와. 근데 사람은 치지 말게. 치면… 앞유리가 좀 지저분해지니까.",
        "아, 저 택시 말이야. 화나면 손이 빨개지는데 그게 폭주 신호야. 손님은 놀라겠지만 팁은 후하지. 허허.",
        "자, 시동 걸어! 아, 안전벨트는 없어. 걱정 마, 운 좋으면 살아.",
    };

    static readonly string[] GreetNormal =
    {
        "왔어? 오늘 점심은 라면이야. 자네 것도 끓였는데… 내가 먹었네. 미안.",
        "어서 와. 방금 쥐가 와이퍼를 갉아 먹었어. 근데 괜찮아, 어차피 앞은 잘 안 보여.",
        "오늘도 무사히 다녀오게. 못 오면 택시는 내가 팔아 줄게.",
    };

    static readonly string[] GreetAfterKaiju =
    {
        "어제 괴수가 우리 가게 간판을 먹고 갔어. 소화는 잘 되나 몰라.",
        "괴수한테 명함 건넸다며? 다음엔 같이 한잔하자고 전해 줘.",
    };

    static readonly string[] GreetAfterHero =
    {
        "히어로가 어제 레이저로 내 라면을 데워 줬어. 고맙다고 해야 하나…",
        "하늘에서 싸우는 건 좋은데, 빨래는 왜 태우는지 모르겠어.",
    };

    static readonly string[] GreetAfterCrowded =
    {
        "어제 사람이 너무 많아서 내 낮잠 자리에도 사람이 있더라고.",
        "축제였다던데 난 초대장 못 받았어. 서운해.",
    };

    // 택시 개조를 끝낸 횟수별 인사 (1~4)
    static readonly string[] GreetProject =
    {
        "증축한 차고 쓰는 재미가 쏠쏠해. 어제 거기서 쥐들이 파티했어.",
        "자네 택시, 햇빛 받으면 내 눈이 멀어. 선글라스 값 좀 줘.",
        "그 미니건… 오늘은 얌전하지? 아니면 말고.",
        "하늘 나는 택시라니. 이제 세금은 누구한테 내라는 거야?",
    };

    static readonly string[] RageHints =
    {
        "손이 빨개지면 곧 폭주야. 근데 폭주하면 오히려 안전하다니, 세상 참 이상하지?",
        "아슬아슬하게 피할수록 택시가 흥분해. 연애랑 비슷하지.",
    };

    static readonly string[] ShopTalk =
    {
        "뭘 손봐 줄까? 현금만 받아. 아, 코인이라고? …그래, 그것도.",
        "싸게 해 줄게. 어차피 품질 보증은 없어.",
        "부품은 넉넉해. 출처는 묻지 말게.",
    };

    // 택시 개조를 끝냈을 때의 대사 (개조 순서대로)
    static readonly string[][] ProjectDone =
    {
        new[]
        {
            "오오, 차고가 넓어졌어! 이제 라면 끓일 자리도 생겼네. 할인? 해 주지, 기분이다!",
            "간판에 네온도 달았어. 눈은 좀 아프지만 있어 보이잖아.",
        },
        new[]
        {
            "이게 그 낡은 택시라고? 눈부셔서 못 쳐다보겠네. 손님들이 선글라스 쓰고 탈 걸세.",
            "분노도 더 오래 간다네. 성질이 더 더러워졌어, 택시가.",
        },
        new[]
        {
            "자, 이게 뭐냐고? 지붕 양옆에 미니건이야. 합법이냐고? 그런 건 내가 알아서 하지.",
            "분노가 터지면 양옆에서 튀어나와서 앞에 있는 건 전부 쓸어버릴 걸세. 괴물이랑 히어로, 손님은 빼고. 걔네는 클레임이 무서워서.",
            "한 가지만 약속해 줘. 쏘고 나서 나한테 영수증은 가져오지 말게.",
        },
        new[]
        {
            "마지막 부품이야. 날개… 달아 볼까? 잠깐, 이거 어디서 났더라. 알아서 날겠지.",
            "자, 달았네! 시동은 걸지 말게. 아직 시험 비행은 안 해 봤거든. 아니, 못 해 봤지.",
            "첫 손님을 태워서 한번 나가 보게. 손님한테는… 비밀로 하고. 안전 운전! 아니, 안전 비행!",
        },
    };

    // 엔딩: 날개 단 택시가 첫 손님을 태우고 달까지 날아간다. 시각(초)과 손님의 말. 시간은 엔딩 시작부터, 날개가 펴지는 순간이 약 2.5초, 달 도착이 약 24초.
    public static readonly float[] EndingTimes = { 0.4f, 2.2f, 3.4f, 5.6f, 8.0f, 10.5f, 13.0f, 15.5f, 18.0f, 20.5f, 22.8f, 25.5f, 28.5f };
    public static readonly string[] EndingTexts =
    {
        "기사님, 어? 땅이 왜 이렇게 멀어지죠?",
        "…기사님? 방금 뭐가 펴지는 소리가…",
        "으아아아아아악!!!",
        "기사님!! 기사님!! 여기 하늘이에요!!!",
        "내려주세요!! 제발 내려주세요!!!",
        "저 내일 출근해야 돼요!! 부장님한테 죽어요!!",
        "신고할 거예요!! 아니 신고할 곳이 없어!!",
        "저기 별이… 별이 왜 이렇게 가까워요?!",
        "살려주세요… 엄마… 엄마아아아!!",
        "소원이 뭐예요!! 돈이면 드릴게요!! 전 재산이에요!!",
        "…저거 달이에요? 달이에요?? 진짜 가는 거예요???",
        "………",
        "…우주에서도 요금은 받으시죠?",
    };

    // 미니건이 튀어나올 때 손님이 하는 말
    static readonly string[] GunPassengerLines =
    {
        "기사님 이거 합법이에요?!",
        "저 여기서 내릴래요!! …아니 그냥 가요!",
        "와 미쳤다 ㅋㅋㅋㅋ",
        "요금 깎지 마세요 제발!",
        "이거 택시 맞아요? 탱크 아니에요?",
    };

    static string Pick(string[] a) { return a[Random.Range(0, a.Length)]; }

    /// <summary>평소 로비에서 말을 걸었을 때의 대사 (어제 어떤 날이었는지, 택시를 얼마나 개조했는지에 따라 다르다).</summary>
    public static string[] Greeting(int day, DayType lastDay, int projectsDone)
    {
        string first;
        if (projectsDone > 0 && Random.value < 0.4f) first = GreetProject[Mathf.Min(projectsDone, GreetProject.Length) - 1];
        else
        {
            switch (lastDay)
            {
                case DayType.Kaiju: first = Pick(GreetAfterKaiju); break;
                case DayType.Hero: first = Pick(GreetAfterHero); break;
                case DayType.Crowded: first = Pick(GreetAfterCrowded); break;
                default: first = Pick(GreetNormal); break;
            }
        }
        if (day >= 3 && Random.value < 0.5f) return new[] { first, Pick(RageHints), "필요한 거 있으면 골라 보게." };
        return new[] { first, "필요한 거 있으면 골라 보게." };
    }

    public static string ShopLine() { return Pick(ShopTalk); }

    public static string[] ProjectLines(int i) { return ProjectDone[Mathf.Clamp(i, 0, ProjectDone.Length - 1)]; }

    public static string GunPassengerLine() { return Pick(GunPassengerLines); }

    /// <summary>줄바꿈 표시(|)를 실제 줄바꿈으로.</summary>
    public static string Wrap(string s) { return s.Replace('|', '\n'); }
}
