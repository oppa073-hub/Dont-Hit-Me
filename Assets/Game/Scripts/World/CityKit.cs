using System.Collections.Generic;
using UnityEngine;

// 3D 로우폴리 건물(Kenney City Kit)을 Resources/CityKit 에서 읽어 도시에 세운다. 테스트용: 인스펙터 use3DBuildings 로 켜고 끈다.
// 파일은 Tools/stage-citykit.py 가 Assets/_External/_Generated/Resources/CityKit 에 복사한다. 없으면 Available 이 false 라서 예전 스프라이트 건물로 돌아간다.
// 모델은 한 가지 팔레트 그림(colormap.png) 하나로 칠해져 있어서 재질 하나를 모두가 나눠 쓴다. 인스턴스는 모델별로 풀에 모아 다시 쓴다(생성/파괴 없이).
public static class CityKit
{
    public sealed class Model
    {
        public GameObject prefab;
        public float width, depth, height;       // 불러온 그대로의 크기(미터): width = 도로를 따라가는 방향, depth = 도로에서 멀어지는 방향. 정면은 +z
    }

    static bool loaded, available;
    static readonly List<Model> models = new List<Model>();
    static readonly Dictionary<int, Stack<GameObject>> pool = new Dictionary<int, Stack<GameObject>>();
    static Material mat;

    public static bool Available
    {
        get { return loaded ? available : Load(); }
    }

    public static int Count { get { return models.Count; } }
    public static Model Get(int i) { return models[i]; }

    // 이 물체를 동그란 레이어로 옮겨 헤드라이트를 받지 않게 한다 (인도/건물 몸통과 같은 규칙)
    static bool Load()
    {
        loaded = true;
        var tex = Resources.Load<Texture2D>("CityKit/colormap");
        var all = Resources.LoadAll<GameObject>("CityKit");
        if (tex == null || all == null || all.Length == 0) return false;
        tex.filterMode = FilterMode.Point;                               // 팔레트 칸 사이로 색이 번지지 않게
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return false;
        mat = new Material(sh);
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.SetFloat("_Metallic", 0f);

        System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));       // 읽는 순서가 항상 같게 (같은 건물이 같은 모델로 나오게)
        foreach (var p in all)
        {
            var go = Object.Instantiate(p);
            Bounds b = default;
            bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            Object.Destroy(go);
            if (!has || b.size.x < 0.05f) continue;
            models.Add(new Model { prefab = p, width = b.size.x, depth = b.size.z, height = b.size.y });
        }
        available = models.Count > 0;
        return available;
    }

    /// <summary>모델 하나를 풀에서 꺼낸다 (없으면 새로 만든다). layer = 헤드라이트를 받지 않는 레이어.</summary>
    public static GameObject Acquire(int index, Transform parent, int layer)
    {
        Stack<GameObject> st;
        GameObject go = null;
        if (pool.TryGetValue(index, out st))
            while (st.Count > 0 && go == null) go = st.Pop();
        if (go == null)
        {
            go = Object.Instantiate(models[index].prefab, parent, false);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var ms = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SetLayer(go, layer);
        }
        go.SetActive(true);
        return go;
    }

    public static void Release(int index, GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        Stack<GameObject> st;
        if (!pool.TryGetValue(index, out st)) pool[index] = st = new Stack<GameObject>();
        st.Push(go);
    }

    static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }
}
