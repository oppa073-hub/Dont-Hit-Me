#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 빌드 시험용(개발 빌드). 에디터에서는 괜찮은데 빌드에서만 생기는 문제(분홍색 셰이더 등)를 찾는다.
// 실행: Tools/run-build-test.ps1 (에디터를 먼저 꺼야 한다). 결과 폴더는 저장소 밖(기본 D:/Unity/project/_Builds/DontHitMe-test).
// 메뉴 Game > Ensure URP Shaders Included 로 셰이더 포함만 따로 실행할 수도 있다.
public static class BuildTest
{
    // 코드가 Shader.Find 로 찾는 셰이더. 빌드는 씬이 쓰는 셰이더만 넣으므로, 이름으로 찾는 것은 Always Included 에 넣어 둬야 빌드에서 안 사라진다.
    static readonly string[] NeededShaders =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Unlit",
    };

    static string Arg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    [MenuItem("Game/Ensure URP Shaders Included")]
    public static void EnsureShaders()
    {
        var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (all == null || all.Length == 0) { Debug.LogError("[BuildTest] GraphicsSettings 를 읽지 못했습니다"); return; }
        var so = new SerializedObject(all[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        int added = 0;
        foreach (string name in NeededShaders)
        {
            var sh = Shader.Find(name);
            if (sh == null) { Debug.LogWarning("[BuildTest] 셰이더를 못 찾음: " + name); continue; }
            bool has = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
            if (has) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildTest] Always Included 셰이더 " + added + "개 추가 (총 " + list.arraySize + "개)");
    }

    // 명령줄: -executeMethod BuildTest.Build -buildDir "폴더"
    public static void Build()
    {
        string dir = Arg("-buildDir") ?? "D:/Unity/project/_Builds/DontHitMe-test";
        Directory.CreateDirectory(dir);
        EnsureShaders();
        var opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(dir, "DontHitMe.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,                 // 개발 빌드: 화면 점검 도구(-uishots)와 구간 타이머가 들어 있다
        };
        var report = BuildPipeline.BuildPlayer(opts);
        var sum = report.summary;
        Debug.Log("[BuildTest] 결과 " + sum.result + ", 크기 " + (sum.totalSize / (1024 * 1024)) + "MB, 시간 " + sum.totalTime + ", 오류 " + sum.totalErrors + ", 경고 " + sum.totalWarnings);
        EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }
}
#endif
