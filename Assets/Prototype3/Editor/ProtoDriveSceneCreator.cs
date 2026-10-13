#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProtoDriveSceneCreator
{
    [MenuItem("Prototype/Create Drive Scene")]
    static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var drive = new GameObject("ProtoDrive").AddComponent<ProtoDrive>();
        drive.buildingTiles = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_External/GandalfHardcore City Tiles/Building Tiles 32x32.png");
        drive.decoTiles = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_External/GandalfHardcore City Tiles/Decoration 32x32.png");
        drive.cityBackground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_External/GandalfHardcore City Tiles/City background.png");
        drive.armAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_External/팔/arm_albedo_pale.png");
        drive.armsModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_External/팔/arms.fbx");
        EditorSceneManager.SaveScene(scene, "Assets/Prototype3/DriveScene.unity");
        Debug.Log("DriveScene 생성 완료. Play 를 누르세요.");
    }
}
#endif
