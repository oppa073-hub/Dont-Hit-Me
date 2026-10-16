using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 로비 연결부: 타이틀 -> 로비(2D 횡스크롤) -> NPC 와 대화하면 상점 / 택시에 타면 영업 시작.
// 걷기와 대사는 LobbyManager 가 하고, 여기서는 키 입력과 흐름(상점 열기, 영업 시작)을 이어 준다. 그리는 건 UI/DriveGame.LobbyDraw.cs.
public partial class DriveGame
{
    LobbyManager lobby;

    const string IntroKey = "DontHitMe.IntroSeen";
    const int TagIntro = 1, TagGreet = 2;

    string shopNpcLine = "";                 // 상점에서 정비사가 하는 말
    string lobbyDayText = "";

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
        RefreshShop();
        if (PlayerPrefs.GetInt(IntroKey, 0) == 0) lobby.StartDialogue(StoryCatalog.Intro, TagIntro);
    }

    void UpdateLobby(Keyboard kb)
    {
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
        lobby.StartDialogue(seen ? StoryCatalog.Greeting(dayEvents.Day, dayEvents.LastType) : StoryCatalog.Intro, TagGreet);
    }

    void OnLobbyDialogueFinished(int tag)
    {
        PlayerPrefs.SetInt(IntroKey, 1);
        PlayerPrefs.Save();
        if (tag == TagGreet) OpenShop();            // 인사가 끝나면 상점이 열린다
    }

    void OnLobbyBoard()
    {
        StartGame();
    }
}
