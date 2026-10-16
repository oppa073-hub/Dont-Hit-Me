using UnityEngine;

// 스토리 대사 모음: 정비소 NPC(박 정비사)의 대사와 아침 뉴스 원고. 대사를 고치거나 늘리려면 이 파일만 열면 된다.
// 뉴스 원고의 줄바꿈은 | 로 적는다 (한 줄은 13글자 안쪽으로, 한 항목은 4줄 안쪽으로).
public static class StoryCatalog
{
    public const string NpcName = "박 정비사";

    // 처음 로비에 왔을 때 (한 번만)
    public static readonly string[] Intro =
    {
        "오, 자네가 새로 온 기사로군. 저 낡은 택시, 내 오랜 친구가 남기고 간 걸세.",
        "요즘 이 도시는 심상치 않아. 바닷가에 괴수가 나타났다는 소문에, 하늘에선 히어로랑 빌런이 싸운다지 뭔가.",
        "그래도 택시는 굴러가야지. 손님을 안전하게 모시는 게 우리 일 아니겠나.",
        "번 돈으로 여기서 택시를 손봐 줄 수 있네. 그리고 말이지… 저 택시, 가끔 화를 낼 때가 있으니 조심하게. 허허.",
        "자, 준비됐으면 택시에 타게. 오늘도 안전 운전!",
    };

    static readonly string[] GreetNormal =
    {
        "어서 오게. 오늘도 무사히 다녀오게나.",
        "왔는가. 커피 한 잔 하겠나? …아, 영업 나가야지.",
        "택시 상태는 내가 봐 뒀네. 믿고 나가 보게.",
    };

    static readonly string[] GreetAfterKaiju =
    {
        "어제 괴수 때문에 난리였지? 무사히 돌아와서 다행이야.",
        "괴수 소동 뉴스 봤네. 그놈, 오늘은 안 나오면 좋으련만.",
    };

    static readonly string[] GreetAfterHero =
    {
        "어제 하늘에서 히어로랑 빌런이 한바탕 했다며? 레이저 조심하게나.",
        "하늘이 번쩍번쩍하더구먼. 밑에서 운전하는 사람만 고생이야.",
    };

    static readonly string[] GreetAfterCrowded =
    {
        "어제는 사람이 정말 많았지. 오늘은 좀 한적하려나.",
        "어제 거리가 북적였더군. 손님은 많았나?",
    };

    static readonly string[] RageHints =
    {
        "그나저나, 손님 태우고 달리다 보면 속에서 뭔가 끓어오르는 느낌 안 드나? 그게 이 택시의 비밀이야.",
        "아슬아슬하게 피할수록 저 택시 화가 빨리 차오른다네. 손이 빨개지면 곧 터진다는 신호지.",
    };

    static readonly string[] ShopTalk =
    {
        "뭘 손봐 줄까? 번 만큼 해 주겠네.",
        "돈 들인 만큼 튼튼해지는 법이지.",
        "부품은 넉넉하니 천천히 골라 보게.",
    };

    static string Pick(string[] a) { return a[Random.Range(0, a.Length)]; }

    /// <summary>평소 로비에서 말을 걸었을 때의 대사 (어제 어떤 날이었는지에 따라 다르다).</summary>
    public static string[] Greeting(int day, DayType lastDay)
    {
        string first;
        switch (lastDay)
        {
            case DayType.Kaiju: first = Pick(GreetAfterKaiju); break;
            case DayType.Hero: first = Pick(GreetAfterHero); break;
            case DayType.Crowded: first = Pick(GreetAfterCrowded); break;
            default: first = Pick(GreetNormal); break;
        }
        if (day >= 3 && Random.value < 0.5f) return new[] { first, Pick(RageHints), "필요한 게 있으면 골라 보게." };
        return new[] { first, "필요한 게 있으면 골라 보게." };
    }

    public static string ShopLine() { return Pick(ShopTalk); }

    /// <summary>운전을 시작할 때 내비 화면에 나오는 아침 뉴스 원고. 오늘이 어떤 날인지 알려 준다.</summary>
    public static string[] News(DayType type, int day, bool shift, int shiftSeconds)
    {
        var list = new System.Collections.Generic.List<string>();
        list.Add("안녕하세요,|시티뉴스|김 앵커입니다.");
        switch (type)
        {
            case DayType.Crowded:
                list.Add("오늘은 도시 축제!|거리마다 인파가|가득합니다.");
                list.Add("보행자를 조심하세요.|손님도 많을|예정입니다!");
                break;
            case DayType.Kaiju:
                list.Add("연안에서 거대|생명체 목격담이|이어지고 있습니다.");
                list.Add("괴수가 나타나면|길이 막힙니다.|우회하세요!");
                break;
            case DayType.Hero:
                list.Add("하늘에서 히어로와|빌런의 대치 소식이|들어왔습니다.");
                list.Add("눈에서 나오는|레이저를 조심하세요!");
                break;
            default:
                list.Add("오늘 Day " + day + ".|큰 사건 없는|평범한 하루네요.");
                list.Add("안전 운전하시고|손님 많이|태우세요!");
                break;
        }
        if (shift) list.Add("오늘 영업은|" + shiftSeconds + "초입니다.|힘내세요!");
        return list.ToArray();
    }

    /// <summary>줄바꿈 표시(|)를 실제 줄바꿈으로.</summary>
    public static string Wrap(string s) { return s.Replace('|', '\n'); }
}
