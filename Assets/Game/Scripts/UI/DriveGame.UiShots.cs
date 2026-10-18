#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 화면 점검 도구 (에디터/개발 빌드 전용). 명령줄에 -uishots 폴더 가 있으면 배치모드에서 화면을 차례로 만들어
//  1) 글자가 상자 밖으로 나오는지(OVERFLOW), 영어 모드인데 한국어가 남았는지(KOREAN)를 점검해 audit.txt 에 적고
//  2) 주요 화면의 스크린샷(PNG)을 남긴다.
// 실행은 Tools/run-uishots.ps1 (에디터를 먼저 꺼야 한다). -uilang 0|1 로 언어를 고른다 (0 영어, 1 한국어).
// 평소 플레이에서 같은 점검을 켜려면 명령줄에 -uiaudit 만 넣으면 된다 (글자를 그릴 때마다 검사해서 파일에 쌓는다).
public partial class DriveGame
{
    static string[] cmdArgs { get { return System.Environment.GetCommandLineArgs(); } }

    static string ArgAfter(string key)
    {
        var a = cmdArgs;
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    static bool HasArg(string key) { return System.Array.IndexOf(cmdArgs, key) >= 0; }

    public static bool UiShotsRequested { get { return HasArg("-uishots"); } }

    bool uiAuditOn;
    string uiAuditPath;
    readonly HashSet<string> auditSeen = new HashSet<string>();
    int auditCount;

    void InitUiAudit()
    {
        if (!HasArg("-uiaudit") && !UiShotsRequested) return;
        uiAuditOn = true;
        string dir = ArgAfter("-uishots") ?? ArgAfter("-uiaudit-dir") ?? Path.Combine(Application.dataPath, "..", "SimReports");
        Directory.CreateDirectory(dir);
        uiAuditPath = Path.Combine(dir, "audit-" + (Loc.Ko ? "ko" : "en") + ".txt");
        File.WriteAllText(uiAuditPath, "UI audit (" + (Loc.Ko ? "ko" : "en") + ")\n");
    }

    static bool HasHangul(string s)
    {
        foreach (char c in s) if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3130 && c <= 0x318F)) return true;
        return false;
    }

    void AuditLog(string kind, string text, string detail)
    {
        string key = kind + "|" + text + "|" + detail;
        if (!auditSeen.Add(key)) return;
        auditCount++;
        File.AppendAllText(uiAuditPath, kind + "  [" + detail + "]  " + text.Replace("\n", "\\n") + "\n");
    }

    // 글자 하나를 상자(r)에 그릴 때 점검 (Label 이 부른다). style 은 이미 글자 크기가 맞춰져 있다.
    int auditLabels;

    void AuditLabel(Rect r, string text, int size)
    {
        if (++auditLabels % 50000 == 0) File.AppendAllText(uiAuditPath, "(글자 " + auditLabels + "개 점검함)\n");
        if (!Loc.Ko && HasHangul(text)) AuditLog("KOREAN", text, "size " + size);
        var content = new GUIContent(text);
        if (style.wordWrap)
        {
            float h = style.CalcHeight(content, r.width);
            if (h > r.height * 1.04f + 2f) AuditLog("OVERFLOW-H", text, "box " + Mathf.RoundToInt(r.width) + "x" + Mathf.RoundToInt(r.height) + " need " + Mathf.RoundToInt(h) + " size " + size);
        }
        else
        {
            float w = style.CalcSize(content).x;
            if (w > r.width * 1.02f + 2f) AuditLog("OVERFLOW-W", text, "box " + Mathf.RoundToInt(r.width) + " need " + Mathf.RoundToInt(w) + " size " + size);
        }
    }

    // 상자 크기를 아는 글자를 그리지 않고 직접 점검 (w, h 는 720p 기준 px)
    void AuditText(string tag, string text, float w, float h, int size, bool wrap)
    {
        if (!uiAuditOn || string.IsNullOrEmpty(text)) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
        float k = Screen.height / 720f;
        style.fontSize = Mathf.RoundToInt(size * k);
        bool old = style.wordWrap;
        style.wordWrap = wrap;
        if (!Loc.Ko && HasHangul(text)) AuditLog("KOREAN", text, tag);
        var content = new GUIContent(text);
        if (wrap)
        {
            float need = style.CalcHeight(content, w * k);
            if (need > h * k * 1.04f + 2f) AuditLog("OVERFLOW-H", text, tag + " box " + Mathf.RoundToInt(w) + "x" + Mathf.RoundToInt(h) + " need " + Mathf.RoundToInt(need / k));
        }
        else
        {
            float need = style.CalcSize(content).x;
            if (need > w * k * 1.02f + 2f) AuditLog("OVERFLOW-W", text, tag + " box " + Mathf.RoundToInt(w) + " need " + Mathf.RoundToInt(need / k));
        }
        style.wordWrap = old;
    }

    // ---------------- 스크린샷 순서 ----------------

    string shotDir;

    IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }

    IEnumerator UiShotsRoutine()
    {
        shotDir = ArgAfter("-uishots");
        Directory.CreateDirectory(shotDir);
        int lang;
        if (!int.TryParse(ArgAfter("-uilang"), out lang)) lang = 0;
        Loc.Set(lang);
        OnLanguageChanged();
        InitUiAudit();
        string tag = lang == 1 ? "ko-" : "en-";
        yield return Frames(5);

        // 1. 타이틀
        flow = GameFlow.Title;
        yield return Frames(3);
        yield return Shot(tag + "01-title");

        // 2. 로비와 대화
        EnterLobby();
        lobby.Reset(580f);
        yield return Frames(3);
        yield return Shot(tag + "02-lobby");
        var shotsDone = new HashSet<int>();
        foreach (var pair in StoryCatalog.AllLobbyLines())
        {
            SetLobbySpeaker(pair.Key);
            lobby.StartDialogue(new[] { pair.Value }, TagChat);
            lobby.Tick(0f, 0f, true);                     // 한 줄을 끝까지 보이게
            yield return Frames(2);
            if (shotsDone.Add(pair.Key)) yield return Shot(tag + "03-dialogue-" + pair.Key);
            lobby.Tick(0f, 0f, true);                     // 대화 끝
        }
        flow = GameFlow.Lobby;

        // 3. 상점
        OpenShop();
        shopPage = 0;
        yield return Frames(3);
        yield return Shot(tag + "04-shop-upgrades");
        shopPage = 1;
        RefreshShop();
        yield return Frames(3);
        yield return Shot(tag + "05-shop-mods");
        flow = GameFlow.Lobby;

        // 4. 설정
        OpenSettings(GameFlow.Lobby);
        yield return Frames(3);
        yield return Shot(tag + "06-settings");
        settingsKeys = true;
        settingsSel = 0;
        RefreshSettings();
        yield return Frames(2);
        yield return Shot(tag + "07-keys");
        settingsKeyWait = 2;
        RefreshSettings();
        yield return Frames(2);
        yield return Shot(tag + "08-keys-wait");
        settingsKeys = false;
        settingsKeyWait = -1;
        flow = GameFlow.Lobby;

        // 5. 영업 시작: 뉴스, 손님 대화들
        StartGame();
        yield return new WaitForSecondsRealtime(3f);
        yield return Shot(tag + "09-news");
        EndNews();

        int pi = 0;
        foreach (var t in PassengerCatalog.PaxTypes)
        {
            pi++;
            string pname = Loc.S(t.names[0]);
            string dest = Loc.S(t.dests[0]);
            int hi = 0;
            foreach (var hello in t.hello)
            {
                taxi.DebugSet(TaxiState.Talking, t, pname, dest, string.Format(Loc.S(hello), dest));
                yield return Frames(2);
                if (hi++ == 0) yield return Shot(tag + "10-talk-" + pi);
            }
            // 타고 가는 중: 탑승 정보 상자 + 말풍선들 (반응, 수다)
            taxi.DebugSet(TaxiState.Talking, t, pname, dest, "");
            taxi.Accept(gx, gy, heading, Period, 20f);
            yield return Frames(2);
            yield return Shot(tag + "11-carry-" + pi);
            var texts = new List<string>();
            if (t.react != null) texts.AddRange(t.react);
            if (t.chat != null) texts.AddRange(t.chat);
            int ri = 0;
            foreach (var tx in texts)
            {
                taxi.Say(Loc.S(tx), 3f);
                yield return Frames(2);
                if (ri++ == 0) yield return Shot(tag + "12-say-" + pi);
            }
            foreach (var th in t.thanks)
            {
                taxi.DebugThanks(pname + ": \"" + Loc.S(th) + "\"");
                yield return Frames(2);
            }
        }
        foreach (var s in PassengerCatalog.HeistReturnLines) { taxi.DebugSet(TaxiState.Talking, PassengerCatalog.Heist, Loc.S("Sunglasses Guy"), Loc.S(PassengerCatalog.HeistHideouts[0]), string.Format(Loc.S(s), Loc.S(PassengerCatalog.HeistHideouts[0]))); yield return Frames(2); }
        foreach (var s in PassengerCatalog.LateThanks) { taxi.DebugThanks("Pat: \"" + Loc.S(s) + "\""); yield return Frames(2); }
        foreach (var s in PassengerCatalog.HeistWaitLines) { taxi.DebugThanks("Pat: \"" + Loc.S(s) + "\""); yield return Frames(2); }
        taxi.DebugSet(TaxiState.Cruise, null, "", "", "");

        // 6. 뉴스 글: 내비 화면(줄바꿈 4줄 이내)과 앞유리 자막 상자에 들어가는지
        foreach (var b in NewsCatalog.AllBeats())
        {
            string text = Loc.S(b);
            string wrapped = StoryCatalog.Wrap(text, NewsEm);
            int lines = 1;
            foreach (char c in wrapped) if (c == '\n') lines++;
            if (lines > 4) AuditLog("NEWS-LINES", text, lines + " lines");
            AuditText("news-subtitle", text.Replace('|', ' '), 480f, 58f, 17, true);
        }

        // 7. 엔딩 대사
        StartEnding(false);
        var times = StoryCatalog.EndingTimes;
        for (int i = 0; i < times.Length; i++)
        {
            endT = times[i] + 0.4f;
            yield return Frames(2);
            if (i == 2 || i == 9) yield return Shot(tag + "13-ending-" + i);
        }
        endT = endingLandAt + 12f;
        yield return Frames(2);
        yield return Shot(tag + "14-ending-end");
        flow = GameFlow.Lobby;

        // 8. 일시정지
        StartGame();
        yield return Frames(5);
        flow = GameFlow.Paused;
        yield return Frames(3);
        yield return Shot(tag + "15-pause");

        File.AppendAllText(uiAuditPath, "\n== 점검 끝: " + auditCount + "건\n");
        BotExitCode = 0;
        BotFinished = true;
    }
}
#endif
