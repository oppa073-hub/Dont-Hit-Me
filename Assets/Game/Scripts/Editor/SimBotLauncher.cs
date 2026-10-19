#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 명령줄에 -simbot 이 있을 때만 동작: 배치모드로 열린 Unity 가 DriveScene 을 열고 플레이를 시작한다.
// 봇(DriveGame.Bot.cs)이 끝나면 BotFinished 를 세우고, 여기서 그걸 보고 Unity 를 종료한다.
// 실행은 Tools/run-simbot.ps1 로 한다 (에디터를 먼저 꺼야 한다).
[InitializeOnLoad]
public static class SimBotLauncher
{
    const string ScenePath = "Assets/Game/Scenes/DriveScene.unity";

    static SimBotLauncher()
    {
        if (!System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-simbot" || a == "-uishots")) return;
        EditorApplication.update += Poll;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;       // 플레이 진입 뒤 도메인 리로드로 다시 불린 경우
        EditorApplication.delayCall += () =>
        {
            Debug.Log("[SimBot] 장면을 열고 플레이를 시작합니다");
            EditorSceneManager.OpenScene(ScenePath);
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-uishots" || a == "-uiaudit" || a == "-uigfx"))
                PlayModeWindow.SetCustomRenderingResolution(1280, 720, "uishots");        // 스크린샷 해상도를 720p 로 고정
            EditorApplication.EnterPlaymode();
        };
    }

    static void Poll()
    {
        if (!DriveGame.BotFinished) return;
        EditorApplication.update -= Poll;
        Debug.Log("[SimBot] 종료 코드 " + DriveGame.BotExitCode);
        EditorApplication.Exit(DriveGame.BotExitCode);
    }
}
#endif
