using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 로비 연결부: 타이틀 -> 로비(2D 횡스크롤) -> NPC 와 대화하면 상점 / 택시에 타면 영업 시작.
// 걷기와 대사는 LobbyManager 가 하고, 여기서는 키 입력과 흐름(상점 열기, 영업 시작, 택시 개조 대사, 엔딩)을 이어 준다. 그리는 건 UI/DriveGame.LobbyDraw.cs.
public partial class DriveGame
{
    LobbyManager lobby;

    const string IntroKey = "DontHitMe.IntroSeen";
    const int TagIntro = 1, TagGreet = 2, TagProject = 3, TagChat = 4, TagSettings = 5;

    // 로비에 서 있는 사람들. 0 은 상점의 사장(안락의자), 나머지는 말만 걸 수 있다. voice = 말소리(AudioManager.VoiceBase 번호)
    class LobbyNpcDef { public string name, cat, set, prompt; public float x, scale; public int voice; }
    static readonly LobbyNpcDef[] LobbyNpcs =
    {
        new LobbyNpcDef { name = "토니 사장‖Boss Tony", cat = "boss", set = null, x = 625f, scale = 1.1f, voice = 0, prompt = "{accept}  대화 / 상점‖{accept}  Talk / Shop" },
        new LobbyNpcDef { name = "제시카 비서‖Jessica", cat = "stand", set = "Lady with handbag", x = 725f, scale = 1.0f, voice = 4, prompt = "{accept}  말 걸기‖{accept}  Talk" },
        new LobbyNpcDef { name = "세차 알바 매니‖Manny", cat = "npc", set = "Dealer", x = 130f, scale = 1.0f, voice = 2, prompt = "{accept}  말 걸기‖{accept}  Talk" },
        new LobbyNpcDef { name = "프랭키 기사‖Frankie", cat = "npc", set = "Businessman", x = 990f, scale = 1.0f, voice = 1, prompt = "{accept}  말 걸기‖{accept}  Talk" },
        new LobbyNpcDef { name = "비니 기사‖Vinnie", cat = "npc", set = "Photographer", x = 1190f, scale = 1.0f, voice = 2, prompt = "{accept}  말 걸기‖{accept}  Talk" },
        new LobbyNpcDef { name = "빅 마이크 기사‖Big Mike", cat = "stand", set = "Bodybuilder", x = 1405f, scale = 1.0f, voice = 0, prompt = "{accept}  말 걸기‖{accept}  Talk" },
    };

    string lobbySpeakerName = StoryCatalog.NpcName;
    PixelSet lobbySpeakerArt;
    int lobbySpeakerVoice;

    PixelSet LobbyNpcArt(int i)
    {
        var d = LobbyNpcs[i];
        return d.set == null ? PixelArt.Get(d.cat) : PixelArt.GetByName(d.cat, d.set);
    }

    void SetLobbySpeaker(int i)
    {
        lobbySpeakerName = Loc.S(LobbyNpcs[i].name);
        lobbySpeakerArt = LobbyNpcArt(i);
        lobbySpeakerVoice = LobbyNpcs[i].voice;
    }

    string shopNpcLine = "";                 // 상점에서 정비사가 하는 말
    string lobbyDayText = "";
    int pendingProject = -1;                 // 방금 끝낸 택시 개조 (대사가 끝나면 이어서 처리)

    void CreateLobby()
    {
        lobby = new LobbyManager();
        lobby.NpcXs = new float[LobbyNpcs.Length];
        for (int i = 0; i < LobbyNpcs.Length; i++) lobby.NpcXs[i] = LobbyNpcs[i].x;
        lobby.NpcInteract += OnNpcInteract;
        lobby.DialogueFinished += OnLobbyDialogueFinished;
        lobby.BoardRequested += OnLobbyBoard;
    }

    // 타이틀이나 게임오버에서 로비로: 세계를 새로 시작해 두고(내일의 날씨/날 종류), 처음 온 거면 정비사가 먼저 말을 건다
    void EnterLobby()
    {
        ResetGame();
        flow = GameFlow.Lobby;
        lobby.Reset(1000f);
        RefreshShop();
        if (PlayerPrefs.GetInt(IntroKey, 0) == 0) { SetLobbySpeaker(0); lobby.StartDialogue(StoryCatalog.Intro, TagIntro); }
    }

    void UpdateLobby(Keyboard kb)
    {
        float mv = (Keybinds.Held(kb, Keybinds.SteerRight) || kb.rightArrowKey.isPressed ? 1f : 0f) - (Keybinds.Held(kb, Keybinds.SteerLeft) || kb.leftArrowKey.isPressed ? 1f : 0f);
        bool act = Keybinds.Pressed(kb, Keybinds.Accept) || kb.enterKey.wasPressedThisFrame;
        if (!lobby.InDialogue && !lobby.Boarding)
        {
            if (kb.oKey.wasPressedThisFrame) { OpenSettings(GameFlow.Lobby); return; }
            if (kb.tabKey.wasPressedThisFrame) ToggleMode();          // 영업 모드 / 무한 모드 바꾸기
            if (kb.escapeKey.wasPressedThisFrame) { flow = GameFlow.Title; return; }
        }
        lobby.Tick(Time.unscaledDeltaTime, mv, act);
        BossBabble();
    }

    int bossShownLen;

    // 사장님 대사가 한 글자씩 나올 때마다 웅얼 (낮고 걸걸한 목소리). 두 글자에 한 번
    void BossBabble()
    {
        string sh = lobby.InDialogue ? lobby.Shown : "";
        if (sh.Length < bossShownLen) bossShownLen = 0;           // 새 대사
        for (int i = bossShownLen; i < sh.Length; i++)
            if ((i & 1) == 0) sound.PlayBlip(sh[i], lobbySpeakerVoice, voiceBossVolume);
        bossShownLen = sh.Length;
    }

    void OnNpcInteract(int idx)
    {
        SetLobbySpeaker(idx);
        if (idx == 1)
        {
            lobby.StartDialogue(StoryCatalog.SecretarySettings, TagSettings);     // 비서: 대사가 끝나면 설정이 열린다
            return;
        }
        if (idx > 0)
        {
            lobby.StartDialogue(StoryCatalog.LobbyChat(idx), TagChat);            // 상점과 상관없는 수다
            return;
        }
        bool seen = PlayerPrefs.GetInt(IntroKey, 0) == 1;
        lobby.StartDialogue(seen ? StoryCatalog.Greeting(dayEvents.Day, dayEvents.LastType, projects.DoneCount) : StoryCatalog.Intro, TagGreet);
    }

    // 택시 개조를 끝냈다: 정비사가 한마디 한다 (상점에서 로비로 나와 대사를 보여 준다)
    void StartProjectDialogue(int i)
    {
        pendingProject = i;
        SetLobbySpeaker(0);
        flow = GameFlow.Lobby;
        lobby.StartDialogue(StoryCatalog.ProjectLines(i), TagProject);
    }

    void OnLobbyDialogueFinished(int tag)
    {
        if (tag == TagChat) return;                    // 동료와의 수다는 저장할 것도 열 것도 없다
        if (tag == TagSettings) { OpenSettings(GameFlow.Lobby); return; }
        PlayerPrefs.SetInt(IntroKey, 1);
        PlayerPrefs.Save();
        if (tag == TagGreet)
        {
            shopPage = 0;
            OpenShop();                          // 인사가 끝나면 상점이 열린다
        }
        else if (tag == TagProject)
        {
            shopPage = 1;
            OpenShop();                          // 다음 개조를 보러 상점으로 (마지막 개조의 엔딩은 다음 영업의 첫 손님을 태울 때 시작된다: DriveGame.Ending.cs)
        }
    }

    void OnLobbyBoard()
    {
        StartGame();
    }
}
