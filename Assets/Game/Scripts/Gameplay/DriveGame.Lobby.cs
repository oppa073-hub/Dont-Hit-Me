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
    const int TagIntro = 1, TagGreet = 2, TagProject = 3;

    string shopNpcLine = "";                 // 상점에서 정비사가 하는 말
    string lobbyDayText = "";
    int pendingProject = -1;                 // 방금 끝낸 택시 개조 (대사가 끝나면 이어서 처리)
    float endingT = -1f;                     // 엔딩 연출 진행 시간 (-1 이면 아님)

    void CreateLobby()
    {
        lobby = new LobbyManager();
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
        endingT = -1f;
        RefreshShop();
        if (PlayerPrefs.GetInt(IntroKey, 0) == 0) lobby.StartDialogue(StoryCatalog.Intro, TagIntro);
    }

    void UpdateLobby(Keyboard kb)
    {
        // 엔딩 연출 중에는 조작을 받지 않는다
        if (endingT >= 0f)
        {
            endingT += Time.unscaledDeltaTime;
            if (endingT >= 9f)
            {
                endingT = -1f;
                shopPage = 1;
            }
            return;
        }

        float mv = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        bool act = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
        if (!lobby.InDialogue && !lobby.Boarding)
        {
            if (kb.oKey.wasPressedThisFrame) { OpenSettings(GameFlow.Lobby); return; }
            if (kb.tabKey.wasPressedThisFrame) ToggleMode();          // 영업 모드 / 무한 모드 바꾸기
            if (kb.escapeKey.wasPressedThisFrame) { flow = GameFlow.Title; return; }
        }
        lobby.Tick(Time.unscaledDeltaTime, mv, act);
    }

    void OnNpcInteract()
    {
        bool seen = PlayerPrefs.GetInt(IntroKey, 0) == 1;
        lobby.StartDialogue(seen ? StoryCatalog.Greeting(dayEvents.Day, dayEvents.LastType, projects.DoneCount) : StoryCatalog.Intro, TagGreet);
    }

    // 택시 개조를 끝냈다: 정비사가 한마디 한다 (상점에서 로비로 나와 대사를 보여 준다)
    void StartProjectDialogue(int i)
    {
        pendingProject = i;
        flow = GameFlow.Lobby;
        lobby.StartDialogue(StoryCatalog.ProjectLines(i), TagProject);
    }

    void OnLobbyDialogueFinished(int tag)
    {
        PlayerPrefs.SetInt(IntroKey, 1);
        PlayerPrefs.Save();
        if (tag == TagGreet)
        {
            shopPage = 0;
            OpenShop();                          // 인사가 끝나면 상점이 열린다
        }
        else if (tag == TagProject)
        {
            if (pendingProject == ProjectManager.Wings)
            {
                endingT = 0f;                    // 마지막 개조: 하늘을 나는 택시 엔딩
                sound.PlayRoar();
            }
            else
            {
                shopPage = 1;
                OpenShop();                      // 다음 개조를 보러 상점으로
            }
        }
    }

    void OnLobbyBoard()
    {
        StartGame();
    }
}
