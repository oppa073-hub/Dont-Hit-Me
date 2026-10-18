using System.Collections.Generic;
using UnityEngine;

// 외부 픽셀 아트(사람, 좀비, 외계인, 돼지, UFO, 경찰차, 괴수)를 아틀라스에서 읽어 애니메이션 스프라이트로 돌려준다.
// 아틀라스는 Tools/build-pixel-atlas.py 가 Assets/_External/_Generated/Resources 에 만든다(_External 은 .gitignore). 파일이 없으면 Available 이 false 가 되고
// 게임은 예전 도형 그림으로 그대로 돌아간다. 스프라이트는 처음 쓸 때 한 번만 만든다.
[System.Serializable]
public class PixelClipData { public string name; public float fps; public bool loop; public int[] rects; }

[System.Serializable]
public class PixelSetData { public string cat, name; public float ppu, px, py, mx, my; public bool faceLeft; public List<PixelClipData> clips; }

[System.Serializable]
public class PixelAtlasData { public int w, h; public List<PixelSetData> sets; }

public sealed class PixelClip
{
    public Sprite[] frames;
    public float fps;
    public bool loop;

    /// <summary>시각 t(초)의 그림. 반복이 아니면 마지막 그림에서 멈춘다.</summary>
    public Sprite At(float t)
    {
        int n = frames.Length;
        int i = Mathf.FloorToInt(Mathf.Max(0f, t) * fps);
        return frames[loop ? i % n : Mathf.Min(i, n - 1)];
    }

    public float Length { get { return frames.Length / Mathf.Max(1f, fps); } }
}

public sealed class PixelSet
{
    public string cat, name;
    public bool faceLeft;
    public PixelClip idle, walk, death, attack;
    public Vector2 muzzle;                                 // 포신 끝(미터, 기준점에서). UFO 가 레이저를 쏘는 자리

    public Sprite First { get { return (idle ?? walk).frames[0]; } }

    /// <summary>그림이 원래 보는 쪽과 가고 싶은 쪽(dir &lt; 0 왼쪽, &gt; 0 오른쪽)이 다르면 true (SpriteRenderer.flipX 에 넣는다).</summary>
    public bool Flip(float dir) { return faceLeft ? dir > 0f : dir < 0f; }
}

public static class PixelArt
{
    static bool loaded, available;
    static readonly Dictionary<string, List<PixelSet>> byCat = new Dictionary<string, List<PixelSet>>();

    public static bool Available
    {
        get
        {
            if (!loaded) Load();
            return available;
        }
    }

    static void Load()
    {
        loaded = true;
        var tex = Resources.Load<Texture2D>("PixelAtlas");
        var json = Resources.Load<TextAsset>("PixelAtlasData");
        if (tex == null || json == null) return;
        PixelAtlasData data;
        try { data = JsonUtility.FromJson<PixelAtlasData>(json.text); }
        catch (System.Exception) { return; }
        if (data == null || data.sets == null) return;
        tex.filterMode = FilterMode.Point;                    // 도트가 번지지 않게
        tex.wrapMode = TextureWrapMode.Clamp;

        foreach (var sd in data.sets)
        {
            var set = new PixelSet { cat = sd.cat, name = sd.name, faceLeft = sd.faceLeft, muzzle = new Vector2(sd.mx, sd.my) / Mathf.Max(1f, sd.ppu) };
            foreach (var cd in sd.clips)
            {
                int n = cd.rects.Length / 4;
                var clip = new PixelClip { frames = new Sprite[n], fps = cd.fps, loop = cd.loop };
                for (int i = 0; i < n; i++)
                {
                    int x = cd.rects[i * 4], y = cd.rects[i * 4 + 1], w = cd.rects[i * 4 + 2], h = cd.rects[i * 4 + 3];
                    var rect = new Rect(x, tex.height - y - h, w, h);                  // 아틀라스는 위에서부터 센 좌표
                    clip.frames[i] = Sprite.Create(tex, rect, new Vector2(sd.px / w, sd.py / h), sd.ppu, 0, SpriteMeshType.FullRect);
                }
                if (cd.name == "idle") set.idle = clip;
                else if (cd.name == "walk") set.walk = clip;
                else if (cd.name == "death") set.death = clip;
                else if (cd.name == "attack") set.attack = clip;
            }
            if (set.idle == null && set.walk == null) continue;
            if (set.idle == null) set.idle = set.walk;
            if (set.walk == null) set.walk = set.idle;
            List<PixelSet> list;
            if (!byCat.TryGetValue(sd.cat, out list)) byCat[sd.cat] = list = new List<PixelSet>();
            list.Add(set);
        }
        available = byCat.Count > 0;
    }

    /// <summary>종류(npc, zombie, alien, pig, ufo, police, kaiju) 중 무작위 하나. 없으면 null.</summary>
    public static PixelSet Random(string cat)
    {
        if (!Available) return null;
        List<PixelSet> list;
        if (!byCat.TryGetValue(cat, out list) || list.Count == 0) return null;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    /// <summary>두 종류를 합친 것 중 무작위 하나 (예: 걷는 사람 + 서 있는 사람).</summary>
    public static PixelSet RandomMix(string catA, string catB)
    {
        if (!Available) return null;
        List<PixelSet> a, b;
        byCat.TryGetValue(catA, out a);
        byCat.TryGetValue(catB, out b);
        int na = a != null ? a.Count : 0, nb = b != null ? b.Count : 0;
        if (na + nb == 0) return null;
        int i = UnityEngine.Random.Range(0, na + nb);
        return i < na ? a[i] : b[i - na];
    }

    /// <summary>종류 안에서 이름으로 찾는다 (예: ("npc", "Japanese guy")). 없으면 null.</summary>
    public static PixelSet GetByName(string cat, string name)
    {
        if (!Available) return null;
        List<PixelSet> list;
        if (!byCat.TryGetValue(cat, out list)) return null;
        foreach (var s in list) if (s.name == name) return s;
        return null;
    }

    public static PixelSet Get(string cat)
    {
        if (!Available) return null;
        List<PixelSet> list;
        return byCat.TryGetValue(cat, out list) && list.Count > 0 ? list[0] : null;
    }
}
