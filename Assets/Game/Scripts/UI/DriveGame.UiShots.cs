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

    // 교차로 회전 장면 연속 사진: 명령줄에 -turnshots 폴더 가 있으면 회전이 시작될 때마다(최대 3번) 2프레임에 한 장씩 찍는다. Tools/run-simbot.ps1 -Gfx -TurnShots
    string turnShotDir;
    int turnShotFrame, turnShotRun;
    bool turnShotPrev;

    void TurnShotTick()
    {
        if (turnShotDir == null)
        {
            turnShotDir = ArgAfter("-turnshots") ?? "";
            if (turnShotDir.Length > 0) Directory.CreateDirectory(turnShotDir);
        }
        if (turnShotDir.Length == 0) return;
        if (turning)
        {
            if (!turnShotPrev) { turnShotRun++; turnShotFrame = 0; }
            if (turnShotRun <= 3 && turnShotFrame % 2 == 0)
                ScreenCapture.CaptureScreenshot(Path.Combine(turnShotDir, "turn" + turnShotRun + "-" + turnShotFrame.ToString("000") + ".png"));
            turnShotFrame++;
        }
        turnShotPrev = turning;
    }

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
        if (UnityEngine.InputSystem.Keyboard.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("UiShotsKeyboard");   // 배치 모드엔 키보드가 없어 Update 가 일찍 끝나 월드가 배치되지 않는다
        shotDir = ArgAfter("-uishots");
        Directory.CreateDirectory(shotDir);
        int lang;
        if (!int.TryParse(ArgAfter("-uilang"), out lang)) lang = 0;
        Loc.Set(lang);
        OnLanguageChanged();
        InitUiAudit();
        string tag = lang == 1 ? "ko-" : "en-";
        yield return Frames(5);

        // 0. 로딩 화면: 켠 직후와 같이 로딩을 한 번 거친다 (걸린 시간을 audit 에 적는다)
        loadingDone = false; loadT = 0f; loadShown = 0f; warmIdx = 0; fontsWarm = false;
        float loadStart = Time.realtimeSinceStartup;
        yield return Frames(4);
        yield return Shot(tag + "00-loading");
        float loadMaxDt = 0f;
        while (!loadingDone && Time.realtimeSinceStartup - loadStart < 25f) { yield return null; loadMaxDt = Mathf.Max(loadMaxDt, Time.unscaledDeltaTime); }
        File.AppendAllText(uiAuditPath, "로딩 중 가장 긴 프레임: " + (loadMaxDt * 1000f).ToString("0") + "ms, LoadAudioData 호출 최대 " + sound.PreloadMaxCallMs.ToString("0") + "ms\n");
        File.AppendAllText(uiAuditPath, "로딩 화면이 보인 시간: " + (Time.realtimeSinceStartup - loadStart).ToString("0.0") + "초 (미리 읽기 끝 " + sound.PreloadDone + ", 글꼴 " + fontsWarm + ", 소리 데이터 안 올라온 효과음 " + sound.NotLoadedCount() + "개)\n");

        File.AppendAllText(uiAuditPath, "첫 재생 비용(미리 읽은 상태): " + sound.DebugFirstPlayCost(false) + "\n");
        File.AppendAllText(uiAuditPath, "첫 재생 비용(소리 데이터 내린 상태 = 수정 전과 같은 조건): " + sound.DebugFirstPlayCost(true) + "\n");

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
            if (t.quirk == Quirk.Alien)
            {
                dayEvents.RaiseAlert(Loc.T("정전! 도시가 암흑에 빠졌습니다 — 헤드라이트에 의지하세요!", "Blackout! The city goes dark — rely on your headlights!"), 5f);
                yield return Frames(3);
                yield return Shot(tag + "16-alien-alert");
            }
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
        // 룸미러: 손님 표정(화남)과 기사의 분노 연출
        {
            var t0 = PassengerCatalog.PaxTypes[0];
            taxi.DebugSet(TaxiState.Talking, t0, Loc.S(t0.names[0]), Loc.S(t0.dests[0]), "");
            taxi.DebugArt(PixelArt.Random("npc"));
            taxi.Accept(gx, gy, heading, Period, 20f);
            rage.AddBonus(0.55f);
            yield return Frames(3);
            yield return Shot(tag + "17-mirror-gauge");
            taxi.OnPedHit();
            yield return Frames(3);
            yield return Shot(tag + "18-mirror-angry");
            rage.DebugFill();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(tag + "19-mirror-rage-a");
            yield return new WaitForSecondsRealtime(0.13f);
            yield return Shot(tag + "19-mirror-rage-b");
            taxi.DebugSet(TaxiState.Cruise, null, "", "", "");
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

        // 8-1. 결과 화면 (영업 종료 / 사고로 중단 / 무한 모드)
        for (int variant = 0; variant < 3; variant++)
        {
            StartGame();
            yield return Frames(3);
            bestSaved = true;                                // 결과 계산이 우리가 넣은 값을 덮어쓰지 않게
            newBest = variant == 0;
            resShift = variant != 2; resDay = 139; resScore = 18450; resTrips = 7; resFare = 38400; resRepairs = 1000; resNet = 37400; resStars = 2;
            resNear = 24; resHits = 2; resSeconds = 193; resLevel = 6; resEarned = 4120; resCoins = 9263; resGoalsDone = 2; resGoalsMax = 3;
            session.ForceEnd();
            yield return Frames(3);
            yield return Shot(tag + "20-result-" + variant);
        }

        // 7-3. 차 폭발: 앞에 차를 세워 박는다 (부딪힌 직후부터 0.12초 간격 연속 사진)
        StartGame();
        yield return Frames(5);
        traffic.DebugSpawnCar(carX, 4f);
        for (int s = 0; s < 8; s++)
        {
            yield return new WaitForSecondsRealtime(0.12f);
            yield return Shot(tag + "16-boom-" + s);
        }

        // 8. 일시정지
        StartGame();
        yield return Frames(5);
        flow = GameFlow.Paused;
        yield return Frames(3);
        yield return Shot(tag + "15-pause");
        pauseLobbyArmed = true;
        yield return Frames(3);
        yield return Shot(tag + "15-pause-armed");
        pauseLobbyArmed = false;

        File.AppendAllText(uiAuditPath, "\n== 점검 끝: " + auditCount + "건\n");
        BotExitCode = 0;
        BotFinished = true;
#if !UNITY_EDITOR
        Application.Quit(0);                                 // 빌드에서 돌릴 때는 끝나면 스스로 종료 (에디터는 SimBotLauncher 가 끈다)
#endif
    }
}
#endif
