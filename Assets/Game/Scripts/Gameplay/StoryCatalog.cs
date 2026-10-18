using UnityEngine;

// 스토리 대사 모음: 택시 회사 사장 NPC(박 사장)의 대사, 택시 개조 대사. 분위기는 병맛 막장.
// 대사를 고치거나 늘리려면 이 파일만 열면 된다. 뉴스 원고의 줄바꿈은 | 로 적는다 (한 줄은 13글자 안쪽, 한 항목은 4줄 안쪽).
public static class StoryCatalog
{
    public static string NpcName { get { return Loc.T("토니 사장", "Boss Tony"); } }

    // 비서에게 말을 걸면 이 대사 뒤에 설정(언어, 소리, 조작 키)이 열린다
    public static readonly string[] SecretarySettings =
    {
        "어서 오세요! 설정을 바꾸시겠어요? 언어, 소리, 조작 키까지 제가 다 봐 드려요.‖Welcome! Want to change your settings? Language, sound, even key bindings — I handle it all.",
        "잠깐만요, 서류철 좀… 네, 여기 있어요.‖One moment, let me grab the file… here you go.",
    };

    // ---- 로비의 다른 사람들 (인덱스는 DriveGame.Lobby.cs 의 LobbyNpcs 와 같다: 1 비서, 2 세차 알바, 3~5 동료 택시 기사). 말을 걸 때마다 두세 줄을 무작위로 ----
    static readonly string[][] LobbyPool =
    {
        null,
        new[] { "사장님 일정이요? 오전엔 낮잠, 오후엔 낮잠 후 라면입니다.‖The boss's schedule? Morning: nap. Afternoon: nap, then ramen.", "택시 월급 말씀이세요? 그건 사장님께… 아, 사장님도 모르시더라고요.‖Your paycheck? Ask the boss… oh, he doesn't know either.", "핸드백 안에 뭐가 있냐고요? 사장님 비상금과 사장님 몰래 모은 제 비상금입니다.‖What's in my handbag? The boss's emergency cash, and my own emergency cash he doesn't know about.", "오늘 날씨요? 사장님 기분이랑 같아요. 오락가락.‖Today's weather? Same as the boss's mood. All over the place." },
        new[] { "아 지금 사장님 차 세차 중이잖아요. 말 걸지 마요, 거품 마르면 얼룩 져요.‖Hey, I'm washing the boss's car here. Don't talk to me — the foam dries and it streaks.", "투덜투덜… 사장님 차는 하루에 네 번 닦으라면서 정작 한 번도 안 타신다니까요.‖*grumble grumble* He makes me wash it four times a day and he never even drives it.", "에휴, 이 차 한 대에 비누가 세 통이에요. 월급은 비누 한 통 값이고요. 투덜투덜.‖Ugh, three bottles of soap for one car. And my pay is the price of one bottle. *grumble*", "사장님 차 세차 중이에요! 투덜… 아니 뭐, 불만은 없어요. 아주 쬐끔 있을 뿐이죠.‖Washing the boss's car! *grumble* …no, no complaints. Maybe one tiny little one." },
        new[] { "자네가 신입이군. 나는 이 회사 최고참 최 기사야. 사고 횟수도 최고참이지.‖You're the new guy. I'm Frankie, longest-serving driver here. Longest crash record, too.", "손님이 '빨리 가 주세요' 하면 천천히 가. 요금은 미터기가 알아서 올려 주거든.‖When a passenger says 'hurry,' go slow. The meter takes care of the rest.", "내 택시는 저쪽이야. 겹쳐 세운 게 아니라 그냥 그렇게 생긴 주차장이야.‖My cab's in the line over there. First come, first served. Roughly.", "괴수 나오는 날은 쉬어. 쉬라고 했는데 자네는 어차피 안 쉬겠지.‖Take monster days off. I'm telling you to rest, but you won't anyway." },
        new[] { "어이, 신입. 난 오 기사야. 사진 찍는 게 취미라 손님 얼굴 다 찍어 뒀어. 이상한 뜻은 없어.‖Hey, rookie. I'm Vinnie. Photography's my hobby — I've got every passenger's face on file. Nothing weird.", "히어로랑 빌런 싸움 구경하는 명당이 있어. 지붕 위인데… 내려오는 길이 없더라.‖There's a great spot to watch hero-villain fights. On a rooftop… but there's no way down.", "택시 계기판은 내가 직접 개조했어. 속도계는 거짓말을 못 해서 떼어 냈지.‖I modded my own dashboard. Removed the speedometer — it can't lie, so it had to go.", "우리 사장님? 말씀은 무서운데 라면은 잘 끓이셔. 근데 항상 자기가 먹어.‖The boss? Scary talker, great ramen cook. But he always eats it himself." },
        new[] { "난 마 기사. 근육은 운전에도 도움이 돼. 핸들을 한 손으로 돌릴 수 있거든. 가끔 부러뜨리기도 하지만.‖I'm Big Mike. Muscles help with driving — I can turn the wheel one-handed. Sometimes I snap it off, though.", "택시 타이어? 내가 직접 들어서 갈아. 정비사는 구경만 하더라.‖Tires? I lift the cab and swap them myself. The mechanic just watches.", "손님이 무거우면 요금을 더 받아야 하는 거 아니냐고? 가끔 생각은 해 봤지.‖Shouldn't heavy passengers pay extra? I've thought about it, sometimes.", "단백질이 부족하면 괴수한테 못 이겨. 그래서 오늘도 닭가슴살 세 근이야.‖Without protein you can't beat a monster. That's why it's three pounds of chicken breast today." },
    };

    /// <summary>로비의 npc(1 이상)에게 말을 걸었을 때의 대사 두세 줄.</summary>
    public static string[] LobbyChat(int npc)
    {
        if (npc <= 0 || npc >= LobbyPool.Length) return new[] { "…" };
        var pool = LobbyPool[npc];
        int a = Random.Range(0, pool.Length), b = (a + 1 + Random.Range(0, pool.Length - 1)) % pool.Length;
        return new[] { pool[a], pool[b] };
    }

    // 처음 로비에 왔을 때 (한 번만)
    public static readonly string[] Intro =
    {
        "어서 와, 신입! 내가 이 택시 회사 사장이야. 직원은 자네 하나지만. 저 택시? 삼촌이 노름빚 대신 두고 간 거야. 브레이크는 의지로 밟게.‖Welcome, rookie! I'm the boss of this cab company. You're my only employee. That cab? My uncle left it to cover a gambling debt. Brake with willpower.",
        "이 동네는 말이야, 괴수가 편의점 도시락을 털고 히어로랑 빌런은 하늘에서 눈에서 레이저 쏘며 부부싸움을 한다네.‖In this town, monsters raid the deli for sandwiches, and heroes and villains have marital spats in the sky, shooting lasers out of their eyes.",
        "손님은 태워야 월세가 나와. 근데 사람은 치지 말게. 치면… 앞유리가 좀 지저분해지니까.‖You've got to pick up fares to pay the rent. But don't hit people. If you do… the windshield gets a little messy.",
        "아, 저 택시 말이야. 화나면 손이 빨개지는데 그게 폭주 신호야. 손님은 놀라겠지만 팁은 후하지. 허허.‖Oh, about that cab: when it gets angry, your hands turn red — that's the rage signal. Passengers freak out, but the tips are generous. Heh heh.",
        "자, 시동 걸어! 아, 안전벨트는 없어. 걱정 마, 운 좋으면 살아.‖Alright, start her up! Oh, there are no seatbelts. Don't worry — if you're lucky, you'll live.",
    };

    static readonly string[] GreetNormal =
    {
        "왔어? 오늘 점심은 라면이야. 자네 것도 끓였는데… 내가 먹었네. 미안.‖You're here? Lunch is ramen today. I made yours too… and ate it. Sorry.",
        "어서 와. 방금 쥐가 와이퍼를 갉아 먹었어. 근데 괜찮아, 어차피 앞은 잘 안 보여.‖Welcome back. A rat just chewed up your wipers. It's fine — you can't see ahead anyway.",
        "오늘도 무사히 다녀오게. 못 오면 택시는 내가 팔아 줄게.‖Come back safe today. If you don't, I'll sell the cab for you.",
    };

    static readonly string[] GreetAfterKaiju =
    {
        "어제 괴수가 우리 회사 간판을 먹고 갔어. 소화는 잘 되나 몰라.‖Yesterday a monster ate our company sign. Hope it digests well.",
        "괴수한테 명함 건넸다며? 다음엔 같이 한잔하자고 전해 줘.‖I heard you handed the monster a business card? Tell it we should grab a drink next time.",
    };

    static readonly string[] GreetAfterHero =
    {
        "히어로가 어제 레이저로 내 라면을 데워 줬어. 고맙다고 해야 하나…‖The hero heated up my ramen with his laser yesterday. Should I thank him…?",
        "하늘에서 싸우는 건 좋은데, 빨래는 왜 태우는지 모르겠어.‖Fighting in the sky is fine, but why do they have to burn my laundry?",
    };

    static readonly string[] GreetAfterCrowded =
    {
        "어제 사람이 너무 많아서 내 낮잠 자리에도 사람이 있더라고.‖It was so crowded yesterday there was someone in my nap spot.",
        "축제였다던데 난 초대장 못 받았어. 서운해.‖They say it was a festival and I never got an invitation. I'm hurt.",
    };

    static readonly string[] GreetAfterZombie =
    {
        "어제 좀비 나왔다며? 우리 사무실에도 하나 들어왔길래 결재판으로 돌려보냈지.‖Zombies yesterday, huh? One wandered into my office, so I sent it away with a clipboard.",
        "좀비 때문에 어제 타이어 주문이 폭주했어. 장사는 잘 됐네, 기분은 별로지만.‖Tire orders went through the roof because of the zombies. Business is good — I just feel weird about it.",
    };

    static readonly string[] ZombieCheers =
    {
        "와 좀비다!! 더 밟아요!!‖Zombies!! Step on it!!",
        "기사님 좀비는 쳐도 돼요!!‖Driver, you're allowed to hit zombies!!",
        "으어어 소리 들려요! 철퍽!‖I can hear them groaning! Splat!",
        "저 오늘 이 택시 타길 잘했어요!‖I'm so glad I took this cab today!",
        "생존자는 치지 마세요! 생존자는요!‖Don't hit the survivors! The survivors!",
    };

    public static string ZombieCheer() { return Pick(ZombieCheers); }

    // 택시 개조를 끝낸 횟수별 인사 (1~4)
    static readonly string[] GreetProject =
    {
        "증축한 차고 쓰는 재미가 쏠쏠해. 어제 거기서 쥐들이 파티했어.‖That expanded garage is fun. The rats threw a party there yesterday.",
        "자네 택시, 햇빛 받으면 내 눈이 멀어. 선글라스 값 좀 줘.‖Your cab blinds me in the sunlight. Pay for my sunglasses.",
        "그 미니건… 오늘은 얌전하지? 아니면 말고.‖Those miniguns… behaving today? And if not, never mind.",
        "하늘 나는 택시라니. 이제 세금은 누구한테 내라는 거야?‖A flying taxi. Now who am I supposed to pay taxes to?",
    };

    static readonly string[] RageHints =
    {
        "손이 빨개지면 곧 폭주야. 근데 폭주하면 오히려 안전하다니, 세상 참 이상하지?‖When your hands turn red, rage is coming. And going berserk is actually safer — strange world, huh?",
        "아슬아슬하게 피할수록 택시가 흥분해. 연애랑 비슷하지.‖The closer your near misses, the more excited the cab gets. Kind of like dating.",
    };

    static readonly string[] ShopTalk =
    {
        "뭘 지원해 줄까? 법인카드는 없어. 현금만 받아. 아, 코인이라고? …그래, 그것도.‖What'll it be? No corporate card — cash only. Oh, coins? …Fine, those too.",
        "싸게 해 줄게. 어차피 품질 보증은 없어.‖I'll give you a deal. No warranty anyway.",
        "부품은 넉넉해. 출처는 묻지 말게.‖Plenty of parts. Don't ask where they came from.",
    };

    // 택시 개조를 끝냈을 때의 대사 (개조 순서대로)
    static readonly string[][] ProjectDone =
    {
        new[]
        {
            "오오, 차고가 넓어졌어! 이제 라면 끓일 자리도 생겼네. 할인? 해 주지, 기분이다!‖Ooh, the garage got bigger! Now I have room to cook ramen. A discount? Sure, I'm feeling generous!",
            "간판에 네온도 달았어. 눈은 좀 아프지만 있어 보이잖아.‖We put neon on the sign too. Hurts the eyes, but it looks classy.",
        },
        new[]
        {
            "이게 그 낡은 택시라고? 눈부셔서 못 쳐다보겠네. 손님들이 선글라스 쓰고 탈 걸세.‖This is that old cab? Too bright to look at. Passengers will ride wearing sunglasses.",
            "분노도 더 오래 간다네. 성질이 더 더러워졌어, 택시가.‖The rage lasts longer now, too. The cab's temper got even worse.",
        },
        new[]
        {
            "자, 이게 뭐냐고? 지붕 양옆에 미니건이야. 합법이냐고? 그런 건 내가 알아서 하지.‖So what's this? Miniguns on both sides of the roof. Legal? I'll handle that.",
            "분노가 터지면 양옆에서 튀어나와서 앞에 있는 건 전부 쓸어버릴 걸세. 괴물이랑 히어로, 손님은 빼고. 걔네는 클레임이 무서워서.‖When rage hits, they pop out and sweep away everything ahead. Except monsters, heroes and passengers — the complaints scare me.",
            "한 가지만 약속해 줘. 쏘고 나서 나한테 영수증은 가져오지 말게.‖Promise me one thing: after you shoot, don't bring me the receipts.",
        },
        new[]
        {
            "마지막 부품이야. 날개… 달아 볼까? 잠깐, 이거 어디서 났더라. 알아서 날겠지.‖Last part. Wings… shall we? Wait, where did I get these? They'll fly themselves.",
            "자, 달았네! 시동은 걸지 말게. 아직 시험 비행은 안 해 봤거든. 아니, 못 해 봤지.‖There, installed! Don't start it yet. I haven't test-flown it. Okay, I couldn't.",
            "첫 손님을 태워서 한번 나가 보게. 손님한테는… 비밀로 하고. 안전 운전! 아니, 안전 비행!‖Take your first passenger for a spin. Don't tell them… Drive safe! I mean, fly safe!",
        },
    };

    // 엔딩: 날개 단 택시가 첫 손님을 태우고 달까지 날아간다. 시각(초)과 손님의 말. 시간은 엔딩 시작부터, 날개가 펴지는 순간이 약 2.5초, 달 도착이 약 24초.
    public static readonly float[] EndingTimes = { 0.4f, 2.2f, 3.4f, 5.6f, 8.0f, 10.5f, 13.0f, 15.5f, 18.0f, 20.5f, 22.8f, 25.5f, 28.5f };
    public static readonly string[] EndingTexts =
    {
        "기사님, 어? 땅이 왜 이렇게 멀어지죠?‖Driver, huh? Why is the ground getting so far away?",
        "…기사님? 방금 뭐가 펴지는 소리가…‖…Driver? What was that unfolding sound just now…",
        "으아아아아아악!!!‖AAAAAAAAAAH!!!",
        "기사님!! 기사님!! 여기 하늘이에요!!!‖Driver!! Driver!! We're in the sky!!!",
        "내려주세요!! 제발 내려주세요!!!‖Let me out!! Please let me out!!!",
        "저 내일 출근해야 돼요!! 부장님한테 죽어요!!‖I have work tomorrow!! My manager will kill me!!",
        "신고할 거예요!! 아니 신고할 곳이 없어!!‖I'm reporting you!! Wait, there's nobody to report to!!",
        "저기 별이… 별이 왜 이렇게 가까워요?!‖Those stars… why are the stars so close?!",
        "살려주세요… 엄마… 엄마아아아!!‖Help me… Mom… MOMMMMM!!",
        "소원이 뭐예요!! 돈이면 드릴게요!! 전 재산이에요!!‖What do you want!! I'll give you money!! It's everything I have!!",
        "…저거 달이에요? 달이에요?? 진짜 가는 거예요???‖…Is that the Moon? Is that the MOON?? Are we really going?!?",
        "………",
        "…우주에서도 요금은 받으시죠?‖…You'll still charge me the fare in space, right?",
    };

    // 미니건이 튀어나올 때 손님이 하는 말
    static readonly string[] GunPassengerLines =
    {
        "기사님 이거 합법이에요?!‖Driver, is this even legal?!",
        "저 여기서 내릴래요!! …아니 그냥 가요!‖I want out here!! …No, never mind, keep going!",
        "와 미쳤다 ㅋㅋㅋㅋ‖Whoa, this is insane lol",
        "요금 깎지 마세요 제발!‖Please don't cut my fare, please!",
        "이거 택시 맞아요? 탱크 아니에요?‖Is this really a taxi? Isn't it a tank?",
    };

    static string Pick(string[] a) { return Loc.S(a[Random.Range(0, a.Length)]); }

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
                case DayType.Zombie: first = Pick(GreetAfterZombie); break;
                default: first = Pick(GreetNormal); break;
            }
        }
        if (day >= 3 && Random.value < 0.5f) return new[] { first, Pick(RageHints), "필요한 거 있으면 골라 보게.‖Pick whatever you need." };
        return new[] { first, "필요한 거 있으면 골라 보게.‖Pick whatever you need." };
    }

    public static string ShopLine() { return Pick(ShopTalk); }

    public static string[] ProjectLines(int i) { return ProjectDone[Mathf.Clamp(i, 0, ProjectDone.Length - 1)]; }

    public static string GunPassengerLine() { return Pick(GunPassengerLines); }

    /// <summary>시험용(화면 점검 도구): 로비 대사를 전부 (말하는 사람 번호, 묶음 글)로.</summary>
    public static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, string>> AllLobbyLines()
    {
        var r = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, string>>();
        void add(int who, string[] a) { foreach (var s in a) r.Add(new System.Collections.Generic.KeyValuePair<int, string>(who, s)); }
        add(0, Intro); add(0, GreetNormal); add(0, GreetAfterKaiju); add(0, GreetAfterHero); add(0, GreetAfterCrowded); add(0, GreetAfterZombie);
        add(0, GreetProject); add(0, RageHints); add(0, ShopTalk); add(0, ZombieCheers); add(0, GunPassengerLines);
        foreach (var p in ProjectDone) add(0, p);
        add(1, SecretarySettings);
        add(0, new[] { "필요한 거 있으면 골라 보게.‖Pick whatever you need." });
        for (int i = 1; i < LobbyPool.Length; i++) add(i, LobbyPool[i]);
        return r;
    }

    /// <summary>줄바꿈 표시(|)를 실제 줄바꿈으로.</summary>
    public static string Wrap(string s) { return s.Replace('|', '\n'); }

    /// <summary>줄바꿈 표시를 줄바꿈으로 바꾸고, 한 줄이 maxEm(한글 한 글자 = 1) 보다 길면 띄어쓰기에서 더 나눈다. 작은 화면 밖으로 글자가 삐져나가지 않게 한다.</summary>
    public static string Wrap(string s, float maxEm)
    {
        var sb = new System.Text.StringBuilder(s.Length + 4);
        float w = 0f;
        foreach (char c in s)
        {
            if (c == '|' || c == '\n') { sb.Append('\n'); w = 0f; continue; }
            float cw = c == ' ' ? 0.4f : (c < 128 ? 0.6f : 1f);
            if (w + cw > maxEm)
            {
                // 가장 가까운 띄어쓰기에서 나눈다 (없으면 그 자리에서)
                int sp = -1;
                for (int i = sb.Length - 1; i >= 0 && sb[i] != '\n'; i--) if (sb[i] == ' ') { sp = i; break; }
                if (sp >= 0)
                {
                    sb[sp] = '\n';
                    w = 0f;
                    for (int i = sp + 1; i < sb.Length; i++) w += sb[i] < 128 ? 0.6f : 1f;
                }
                else { sb.Append('\n'); w = 0f; }
            }
            sb.Append(c);
            w += cw;
        }
        return sb.ToString();
    }
}
