using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 다국어(로컬라이징): 영어(기본) / 한국어. 글자는 두 가지 방법으로 쓴다.
//  1) Loc.T("한국어", "English")        : 코드 안의 문장 (UI 글자 등)
//  2) "한국어‖English" 한 줄 + Loc.S(...) : 대사/이름 표(카탈로그)처럼 배열에 쌓아 두는 글. ‖ 앞이 한국어, 뒤가 영어.
//     ‖ 가 없는 글은 어느 언어든 그대로 나온다 (아직 번역하지 않은 글이 비지 않게).
// 언어는 PlayerPrefs "DontHitMe.Lang" (0 영어, 1 한국어)에 저장되고, 로비에서 비서에게 말을 걸면 열리는 설정에서 바꾼다.
public static class Loc
{
    public const char Sep = '‖';
    const string PrefKey = "DontHitMe.Lang";

    public static int Lang = 0;                          // 0 English, 1 한국어
    public static bool Ko { get { return Lang == 1; } }

    static readonly Dictionary<string, string[]> cache = new Dictionary<string, string[]>();

    public static void Load() { Lang = PlayerPrefs.GetInt(PrefKey, 0) == 1 ? 1 : 0; }

    public static void Set(int lang)
    {
        Lang = lang == 1 ? 1 : 0;
        PlayerPrefs.SetInt(PrefKey, Lang);
        PlayerPrefs.Save();
    }

    /// <summary>요금 표시: 한국어는 "1200원", 영어는 뉴욕이 배경이라 "$12.00" (게임 안의 금액을 100 으로 나눈다).</summary>
    public static string Money(int won)
    {
        string sign = won < 0 ? "-" : "";
        int a = Mathf.Abs(won);
        return Lang == 1 ? sign + a + "원" : sign + "$" + (a / 100f).ToString("0.00");
    }

    public static string T(string ko, string en) { return Lang == 1 ? ko : en; }

    /// <summary>"한국어‖English" 에서 지금 언어의 글을 고른다. ‖ 가 없으면 그대로.</summary>
    public static string S(string packed)
    {
        if (string.IsNullOrEmpty(packed)) return packed;
        string[] parts;
        if (!cache.TryGetValue(packed, out parts))
        {
            int i = packed.IndexOf(Sep);
            parts = i < 0 ? new[] { packed, packed } : new[] { packed.Substring(0, i), packed.Substring(i + 1) };
            cache[packed] = parts;
        }
        return parts[Lang == 1 ? 0 : 1];                  // parts[0] = 한국어, parts[1] = English
    }

    /// <summary>글 배열 전체를 지금 언어로 (대화 시작 때 한 번).</summary>
    public static string[] S(string[] packed)
    {
        var r = new string[packed.Length];
        for (int i = 0; i < r.Length; i++) r[i] = S(packed[i]);
        return r;
    }
}

// 조작 키 설정: 조향/브레이크/깜빡이/태우기/거절을 바꿀 수 있다. 방향키(조향, 브레이크)와 Enter(태우기)는 늘 같이 먹는다.
public static class Keybinds
{
    public const int SteerLeft = 0, SteerRight = 1, Brake = 2, BlinkLeft = 3, BlinkRight = 4, Accept = 5, Decline = 6, Count = 7;
    const string PrefKey = "DontHitMe.Key.";

    public static readonly Key[] Defaults = { Key.A, Key.D, Key.S, Key.Q, Key.E, Key.Space, Key.X };
    public static readonly Key[] Map = (Key[])Defaults.Clone();
    public static int Version;                            // 키가 바뀔 때마다 올라간다 (키 안내 글자를 다시 만들 때 쓴다)

    public static string ActionName(int a)
    {
        switch (a)
        {
            case SteerLeft: return Loc.T("왼쪽 조향", "Steer left");
            case SteerRight: return Loc.T("오른쪽 조향", "Steer right");
            case Brake: return Loc.T("브레이크", "Brake");
            case BlinkLeft: return Loc.T("왼쪽 깜빡이", "Left blinker");
            case BlinkRight: return Loc.T("오른쪽 깜빡이", "Right blinker");
            case Accept: return Loc.T("손님 태우기", "Pick up passenger");
            default: return Loc.T("손님 거절", "Decline passenger");
        }
    }

    public static string KeyName(Key k)
    {
        string n = k.ToString();
        if (n.StartsWith("Digit")) n = n.Substring(5);
        return n;
    }

    public static void Load()
    {
        for (int i = 0; i < Count; i++) Map[i] = (Key)PlayerPrefs.GetInt(PrefKey + i, (int)Defaults[i]);
        Version++;
    }

    public static void Save()
    {
        for (int i = 0; i < Count; i++) PlayerPrefs.SetInt(PrefKey + i, (int)Map[i]);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        for (int i = 0; i < Count; i++) Map[i] = Defaults[i];
        Version++;
        Save();
    }

    /// <summary>action 의 키를 바꾼다. 이미 다른 동작이 쓰는 키면 서로 바꿔 치기.</summary>
    public static void Rebind(int action, Key k)
    {
        for (int i = 0; i < Count; i++)
            if (i != action && Map[i] == k) { Map[i] = Map[action]; break; }
        Map[action] = k;
        Version++;
        Save();
    }

    public static bool Held(Keyboard kb, int a) { return kb[Map[a]].isPressed; }
    public static bool Pressed(Keyboard kb, int a) { return kb[Map[a]].wasPressedThisFrame; }
}
