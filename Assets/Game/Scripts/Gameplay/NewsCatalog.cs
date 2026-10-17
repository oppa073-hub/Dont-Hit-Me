using UnityEngine;

// 내비 뉴스의 데이터: 날 종류(DayType)마다 "뉴스 한 벌"(배경, 머리 띠, 앵커 연기, 원고 풀, 하단 자막)을 한 표에 모아 둔다.
// 새 날 종류(새 이벤트)를 만들면 Kits 표에 한 줄(그리고 필요하면 풀)만 추가하면 뉴스 연출이 같이 생긴다.
// 원고의 | 는 줄바꿈이다. 한 줄은 12글자 안팎, 최대 4줄(내비 화면이 작다).
public enum NewsBg { City, Fire, Storm, Festival, Zombie }
public enum NewsMood { Sleepy, Panic, Dazzled, Squeezed }

/// <summary>뉴스 한 토막. kind: 0 앵커, 1 현장 연결, 2 광고, 3 긴급 속보(운전 중 끼어들기).</summary>
public struct NewsBeat
{
    public int kind;
    public string text;
    public NewsBeat(int kind, string text) { this.kind = kind; this.text = text; }

    /// <summary>이 토막이 화면에 머무는 시간(초): 글자가 나오는 시간 + 읽을 시간.</summary>
    public float Seconds
    {
        get
        {
            float t = Mathf.Max(3.2f, text.Length / 22f + 1.9f);
            return kind == 1 ? t + 1.1f : (kind == 2 ? Mathf.Max(3.4f, t - 0.4f) : t);       // 현장 연결은 끝에 지직거리는 시간이 붙는다
        }
    }
}

public class NewsKit
{
    public string badge = "시티뉴스  LIVE";
    public Color badgeColor = new Color(0.78f, 0.1f, 0.1f);
    public bool blink;                       // 머리 띠가 점멸하는가 (사건 날)
    public NewsMood mood = NewsMood.Sleepy;
    public NewsBg bg = NewsBg.City;
    public Color studio = new Color(0.08f, 0.16f, 0.38f);
    public string[] headline, field, closing, ticker, bulletin;
    public string[] forecast, over;          // forecast: 아침 주의보(사건이 곧 터질 수 있음), over: 사건이 끝난 뒤 후일담
}

public static class NewsCatalog
{
    static string Pick(string[] a) { return a[Random.Range(0, a.Length)]; }

    // ---- 날 종류별 뉴스 한 벌 (새 이벤트는 여기에 한 줄 추가) ----
    static readonly NewsKit[] Kits =
    {
        // 평범한 날
        new NewsKit
        {
            badge = "시티뉴스  LIVE", mood = NewsMood.Sleepy, bg = NewsBg.City,
            headline = new[]
            {
                "오늘은 특별한|사건이 없습니다.|평범해서 무섭네요.",
                "도시는 평화롭습니다.|제 마음만 빼고요.|(월요병)",
                "오늘의 사건 사고:|없음! 이럴 때가|제일 불안합니다.",
                "택시 기사 여러분|오늘도 안전 운전!|(안 지킬 거잖아요)",
            },
            closing = new[]
            {
                "오늘의 운세:|운전대를 잡는 자|행운이 따르리라.",
                "내일은 뭐가|터질지 모르니|오늘 많이 버세요.",
                "이상 평화로운|뉴스였습니다.|…진짜 평화네?",
            },
            ticker = new[]
            {
                "시민 김모씨(42) '택시 타고 싶다'", "편의점 도시락 오늘도 맛있음", "가로등 하나 깜빡임 (원인 불명)",
                "비둘기 3마리 횡단보도 무단횡단", "정비사 박씨 '미니건은 합법 아님'", "오늘의 점심 메뉴: 라면",
            },
        },
        // 사람 많은 날
        new NewsKit
        {
            badge = "시티뉴스  축제 특보", badgeColor = new Color(0.9f, 0.45f, 0.1f), mood = NewsMood.Squeezed, bg = NewsBg.Festival,
            studio = new Color(0.35f, 0.15f, 0.4f),
            headline = new[]
            {
                "오늘은 도시 축제!|사람이 너무 많아|땅이 안 보입니다.",
                "전국에서 모인 인파!|횡단보도가|터질 것 같아요.",
                "오늘 인구 밀도|세계 1위 도시!|발 밟지 마세요.",
            },
            field = new[]
            {
                "현장의 박 기자!|여기 사람이 너무|많아서…|(밀려남)",
                "축제 현장입니다!|제 뒤로 사람이|끝없이…|으아 밀지 마!",
            },
            closing = new[]
            {
                "참고로 저도 지금|인파에 밀려|방송 중입니다.",
                "운전자 여러분|사람이 튀어나옵니다.|진심입니다.",
                "택시는 오늘|사람 반 길 반!|조심 또 조심.",
            },
            ticker = new[]
            {
                "시민 한 명 풍선 따라 도로 진입", "푸드트럭 앞 줄 3km", "미아 보호소 포화 상태",
                "축제 폭죽 값 인상 예고", "오늘 횡단보도 신기록 예상", "택시비도 오릅니다? 아니요",
            },
        },
        // 괴수 침공
        new NewsKit
        {
            badge = "긴급 속보  괴수 출현", badgeColor = new Color(0.85f, 0.08f, 0.05f), blink = true, mood = NewsMood.Panic, bg = NewsBg.Fire,
            studio = new Color(0.28f, 0.1f, 0.08f),
            headline = new[]
            {
                "연안에서 괴수가|편의점 도시락을|털고 있습니다!",
                "거대 괴수 상륙!|목적은 불명,|배고픈 건 확실.",
                "괴수가 도시에!|이유는 묻지 마세요|저도 모릅니다.",
            },
            field = new[]
            {
                "현장의 박 기자!|제 뒤에 보이는 게|괴수인가요?|…꺄아아악!",
                "괴수 앞입니다!|숨소리가 닿을…|지지직 …연결이|…끊겼습니다.",
                "박 기자입니다.|괴수가 제 카메라를|물었…|뚝.",
            },
            closing = new[]
            {
                "괴수가 길을 막으면|교차로에서|꺾으세요!",
                "도망치세요!|저는 방송 중이라|못 도망갑니다.",
                "괴수 보험은|아직 없습니다.|내일 나올 예정.",
            },
            forecast = new[]
            {
                "오늘 괴수 출몰이|예상됩니다.|도시락을 사수하세요.",
                "괴수 주의보 발령!|언제 올지는 몰라요|저도 떨립니다.",
            },
            over = new[]
            {
                "괴수가 바다로|돌아갔습니다.|편의점은 폐업 위기.",
                "괴수 사태 종료!|발자국은 관광지로|지정될 예정입니다.",
            },
            bulletin = new[]
            {
                "긴급 속보!|괴수 출현!|방금 편의점에서|나왔습니다!",
                "속보입니다!|괴수가 도로를|막고 있어요!",
            },
            ticker = new[]
            {
                "괴수 입에서 불이 나옴", "편의점 도시락 품절 대란", "시민 '괴수보다 월세가 무섭다'",
                "정부 '대책 회의 중(점심 포함)'", "괴수 발자국이 도로를 막음", "연기 기둥 다수 목격",
            },
        },
        // 히어로와 빌런
        new NewsKit
        {
            badge = "긴급 속보  하늘 전투", badgeColor = new Color(0.55f, 0.15f, 0.85f), blink = true, mood = NewsMood.Dazzled, bg = NewsBg.Storm,
            studio = new Color(0.1f, 0.08f, 0.24f),
            headline = new[]
            {
                "하늘에서 히어로와|빌런이 부부싸움|중입니다.",
                "히어로 vs 빌런!|원인은 층간소음|이라고 합니다.",
                "도시 상공 대결!|눈에서 레이저가|나옵니다!",
            },
            field = new[]
            {
                "현장의 박 기자!|하늘에서 번쩍번쩍!|눈이 부…|아 안 보여요!",
                "박 기자입니다.|레이저가 지나가서|제 머리카락이|…탔습니다.",
                "결투 현장입니다!|히어로가 이쪽을|보고…|지직 …뚝.",
            },
            closing = new[]
            {
                "눈에서 나오는|레이저를 조심!|빨래가 탑니다.",
                "한쪽 차선을 쓸면|반대 차선으로!|침착하게.",
                "싸움은 둘이 하는데|피해는 우리가.|이게 도시입니다.",
            },
            forecast = new[]
            {
                "오늘 하늘 전투가|예상됩니다.|빨래는 실내에서.",
                "히어로 빌런 주의보|부부싸움 소리가|들린다는 제보.",
            },
            over = new[]
            {
                "하늘 전투 종료!|둘은 같이 저녁|먹으러 갔습니다.",
                "전투가 끝났습니다.|피해 복구비는|보험으로… 안 됨.",
            },
            bulletin = new[]
            {
                "긴급 속보!|하늘에서 전투가|시작됐습니다!",
                "속보입니다!|레이저 주의!|반대 차선으로!",
            },
            ticker = new[]
            {
                "히어로 '미안, 합의금은 보험으로'", "빌런 '먼저 때린 건 쟤'", "도로 불길 다수 발생",
                "층간소음 분쟁 하늘로 확대", "시민 '레이저로 라면 끓이고 싶다'", "하늘 전투 중계권 협상 중",
            },
        },
        // 좀비 사태
        new NewsKit
        {
            badge = "긴급 속보  좀비 출현", badgeColor = new Color(0.2f, 0.65f, 0.15f), blink = true, mood = NewsMood.Panic, bg = NewsBg.Zombie,
            studio = new Color(0.03f, 0.09f, 0.06f),
            headline = new[]
            {
                "도심에 좀비 출현!|원인은 불명,|월요일일 가능성.",
                "좀비 떼가 도로로|쏟아지고 있습니다!|초록색을 보면 쓸어!",
                "시민 여러분 침착!|좀비는 걷는 게|느립니다.(운전 중엔)",
            },
            field = new[]
            {
                "현장의 박 기자!|좀비들이 몰려…|으어… 어? 저도|배가 고픈데…",
                "박 기자입니다.|좀비가 제 마이크를|뺏어…|으어어어어.",
                "좀비 앞입니다!|사람처럼 보이지만|초록이 좀비…|…제 손이 초록?",
            },
            closing = new[]
            {
                "초록색은 쳐도 되고|주황색 생존자는|절대 치지 마세요!",
                "좀비는 쓸어버리고|생존자는 피하세요.|구분이 어렵다고요?",
                "분노가 차면 미니건!|좀비 보너스|맛있게 드세요.",
            },
            forecast = new[]
            {
                "오늘 좀비 출현이|예상됩니다.|초록색을 조심!",
                "좀비 주의보 발령!|초록은 쓸어버리고|주황은 피하세요.",
            },
            over = new[]
            {
                "좀비 사태 종료!|좀비들은 다시|출근했습니다.",
                "상황 종료입니다.|좀비도 월요일엔|힘들었을 겁니다.",
            },
            bulletin = new[]
            {
                "긴급 속보!|좀비 출현!|초록색은 쓸어버리세요|생존자는 주황색!",
                "속보입니다!|좀비가 도로로!|침착하게 밟으세요.",
            },
            ticker = new[]
            {
                "좀비 '으어' 한 마디 발언", "생존자 '살려주세요 주황색이에요'", "병원 앞 좀비 줄 3km",
                "정부 '좀비는 월세를 안 냅니다'", "편의점 '뇌 모양 젤리' 품절", "시민 '택시 타고 도망가고 싶다'",
            },
        },
    };

    // 광고 (평범한 날에 끼어든다): 병맛
    static readonly string[] Ads =
    {
        "박 정비소|지붕에 미니건|달아드립니다|(합법 아님)",
        "라면 휴게실|오픈 기념|국물 무한 리필|(면은 유한)",
        "하늘을 나는 택시|곧 출시!|날개는 옵션|비행은 자기 책임",
        "분노 조절 학원|수강생 모집|…아 줄 서 있는데|새치기 하지 마!",
        "중고 택시 판매|사고 이력 없음|(사고가 나서|이력이 날아감)",
    };

    // 어제의 후일담
    static readonly string[] AfterKaiju = { "어제 괴수가 남긴|발자국이 관광지가|됐습니다. 3천 원!", "괴수가 떠난 자리에|편의점이 새로|문을 열었습니다." };
    static readonly string[] AfterHero = { "어제 하늘 전투는|무승부! 합의금|협상 중입니다.", "히어로와 빌런이|화해했습니다.|도시는 그대로." };
    static readonly string[] AfterZombie = { "어제 좀비 사태의|후유증으로 시내|목 보호대가 품절.", "좀비들이 사라졌습니다.|…어디로 갔을까요?|(출근했을지도)" };
    static readonly string[] AfterCrowded = { "어제 축제의 쓰레기|산이 아직도|남아 있습니다.", "어제 축제 인파|기록 갱신!|발은 괜찮으신가요." };

    public static NewsKit Kit(DayType t) { return Kits[Mathf.Clamp((int)t, 0, Kits.Length - 1)]; }

    public static string Tick(DayType t, int i)
    {
        var tk = Kit(t).ticker;
        return "▶ " + tk[((i % tk.Length) + tk.Length) % tk.Length];
    }

    /// <summary>운전을 시작할 때 나오는 아침 뉴스 원고. 인사 -> 헤드라인 -> (사건 날) 현장 연결 -> 곁들이 이야기 -> 마무리.</summary>
    public static NewsBeat[] Script(DayType type, int day, bool shift, int shiftSeconds, DayType lastDay, float rain, float darkness, int projectsDone)
    {
        var kit = Kit(type);
        var list = new System.Collections.Generic.List<NewsBeat>();
        string[] greets =
        {
            "안녕하세요,|시티뉴스|김 앵커입니다.|(졸려요)",
            "좋은 아침입니다.|김 앵커입니다.|…좋은 건 모르겠고.",
            "시티뉴스 시작합니다.|오늘도 월세를|위해 달립니다!",
            "여러분 안녕하세요.|저는 오늘도|출근했습니다.",
        };
        list.Add(new NewsBeat(0, Pick(greets)));
        // 사건이 하루 중 갑자기 터지는 날(괴수/히어로/좀비)의 아침 방송은 "주의보"다. 실제 사건이 터지면 긴급 속보가 따로 끼어든다.
        bool forecastDay = kit.forecast != null;
        list.Add(new NewsBeat(0, Pick(forecastDay ? kit.forecast : kit.headline)));
        if (kit.field != null && !forecastDay) list.Add(new NewsBeat(1, Pick(kit.field)));

        // 곁들이 이야기: 날씨, 어제 일, 택시 개조 소문, 광고 중 하나
        float r = Random.value;
        if (type == DayType.Normal && r < 0.45f) list.Add(new NewsBeat(2, Pick(Ads)));
        else if (rain > 0.5f) list.Add(new NewsBeat(0, "오늘은 비가 옵니다.|와이퍼를 믿으세요.|(믿지 마세요)"));
        else if (darkness > 0.6f) list.Add(new NewsBeat(0, "지금은 한밤중.|헤드라이트를 켜세요.|…이미 켰네요."));
        else if (lastDay != DayType.Normal && day > 1 && r < 0.7f) list.Add(new NewsBeat(0, Pick(lastDay == DayType.Kaiju ? AfterKaiju : (lastDay == DayType.Hero ? AfterHero : (lastDay == DayType.Zombie ? AfterZombie : AfterCrowded)))));
        else if (projectsDone >= 4) list.Add(new NewsBeat(0, "날개 달린 택시 목격!|새다 비행기다|…아니 택시다!"));
        else if (projectsDone >= 3) list.Add(new NewsBeat(0, "속보! 지붕에 미니건|단 택시가 목격|경찰은 못 본 척!"));
        else list.Add(new NewsBeat(2, Pick(Ads)));

        list.Add(new NewsBeat(0, Pick(kit.closing)));
        if (shift) list.Add(new NewsBeat(0, "오늘 영업은|" + shiftSeconds + "초입니다.|월세를 위해!"));
        return list.ToArray();
    }

    /// <summary>사건이 끝난 뒤 끼어드는 후일담 (없으면 null).</summary>
    public static NewsBeat[] Over(DayType type)
    {
        var kit = Kit(type);
        if (kit.over == null) return null;
        return new[] { new NewsBeat(0, Pick(kit.over)) };
    }

    /// <summary>사건이 시작될 때 운전 중에 끼어드는 긴급 속보 (없으면 null).</summary>
    public static NewsBeat[] Bulletin(DayType type)
    {
        var kit = Kit(type);
        if (kit.bulletin == null) return null;
        return new[] { new NewsBeat(3, Pick(kit.bulletin)), new NewsBeat(3, Pick(kit.closing)) };
    }
}
