using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 에셋(타일/배경/텍스처)에서 스프라이트와 재질, 도로/건물 오브젝트를 만든다
public partial class DriveGame
{
    // ---------- 에셋 → 스프라이트 ----------

    Sprite Tile(Texture2D tex, int px, int pyFromTop, int w, int h, float pivotY = 0f)
    {
        tex.filterMode = FilterMode.Point;
        var rect = new Rect(px, tex.height - pyFromTop - h, w, h);
        return Sprite.Create(tex, rect, new Vector2(0.5f, pivotY), 32f, 0, SpriteMeshType.FullRect);
    }

    void BuildCitySprites()
    {
        for (int i = 0; i < 4; i++)
        {
            winTiles[i] = Tile(buildingTiles, 128 + 3, i * 64, 26, 64);    // 창문(회색/베이지/빨강/분홍), 좌우 3px 테두리 제외
            doorTiles[i] = Tile(buildingTiles, 64, i * 64, 32, 64);    // 문
        }
        for (int i = 0; i < 3; i++) shopTiles[i] = Tile(buildingTiles, 128 + i * 32 + 3, 256, 26, 64);   // 1층 상점
        corniceTile = Tile(buildingTiles, 480 + 3, 0, 26, 32);            // 지붕 장식

        lampSprite = Tile(decoTiles, 192, 32, 32, 96);
        lightRedSprite = Tile(decoTiles, 160, 0, 32, 128);
        lightGreenSprite = Tile(decoTiles, 224, 0, 32, 128);
        trashSprite = Tile(decoTiles, 96, 0, 32, 32);
        mailSprite = Tile(decoTiles, 128, 0, 32, 32);
    }

    void BuildBackdrop()
    {
        if (cityBackground == null) return;
        cityBackground.filterMode = FilterMode.Point;
        var sp = Sprite.Create(cityBackground, new Rect(0, 0, cityBackground.width, cityBackground.height), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);

        const float z = 350f;
        float width = 2f * z * Mathf.Tan(32.5f * Mathf.Deg2Rad);          // 한 장이 가로 약 65도
        float scale = width / (cityBackground.width / 100f);
        float bottom = z * Mathf.Tan(-1f * Mathf.Deg2Rad);

        // 굽은 도로 때문에 배경 스프라이트 아래로 비어 보이는 틈을, 스카이라인 바닥색 판으로 메운다
        var fill = new GameObject("BackdropFill");
        fill.transform.SetParent(cam.transform, false);
        fill.transform.localPosition = new Vector3(0f, bottom + 0.5f, z + 1f);
        fill.transform.localScale = new Vector3(width * 4f, 900f, 1f);
        var fsr = fill.AddComponent<SpriteRenderer>();
        fsr.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 1f), whiteTex.width);
        fsr.color = new Color(98f / 255f, 122f / 255f, 163f / 255f);
        fsr.sortingOrder = -11;
        for (int k = -1; k <= 1; k++)
        {
            var go = new GameObject("Backdrop" + k);
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(k * width * 0.998f, bottom, z);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = -10;
        }
    }

    SpriteRenderer MakeSR(string n, int order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }

    SpriteRenderer MakeTiled(string n, int order)
    {
        var sr = MakeSR(n, order);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        return sr;
    }

    Transform Block(string blockName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = blockName;
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    Material Mat(Color c)
    {
        var m = new Material(litShader);
        m.SetColor("_BaseColor", c);
        m.color = c;
        return m;
    }

    // ---------- 텍스처 ----------

    static Texture2D MakeTexture(bool circle)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - n / 2f) / (n / 2f);
                float dy = (y + 0.5f - n / 2f) / (n / 2f);
                float a = (!circle || dx * dx + dy * dy <= 1f) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    // 32x56 도트 느낌의 단순 사람 모양 (흰색 바탕, SpriteRenderer.color 로 색칠)
    static Sprite MakePedSprite()
    {
        const int w = 32, h = 56;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0f, 0f, 0f, 0f);
                float hx = x - 15.5f, hy = y - 46f;
                bool head = hx * hx + hy * hy <= 64f;                  // 머리: 반지름 8
                bool torso = x >= 8 && x <= 23 && y >= 18 && y <= 36;
                bool legs = (x >= 9 && x <= 14 || x >= 17 && x <= 22) && y >= 1 && y < 18;
                bool arms = (x >= 4 && x <= 7 || x >= 24 && x <= 27) && y >= 20 && y <= 36;
                if (head || torso || legs || arms) c = Color.white;
                bool eye = y >= 46 && y <= 49 && (x >= 11 && x <= 13 || x >= 18 && x <= 20);
                if (eye && head) c = Color.black;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }
}
