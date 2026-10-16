using UnityEngine;

// 교통 스프라이트 만들기: 외부 에셋 없이 코드로 그린 도트 차/콘/공. (몸통은 흰색이라 SpriteRenderer.color 로 차마다 색을 칠한다)
public partial class DriveGame
{
    static Texture2D NewPixelTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var clear = new Color32[w * h];
        tex.SetPixels32(clear);
        return tex;
    }

    static Sprite PixelSprite(Texture2D tex, float pixelsPerUnit)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    static TrafficSprites MakeTrafficSprites()
    {
        var s = new TrafficSprites();

        // 승용차 뒷모습 72x48 (40픽셀 = 1m 이니 폭 1.8m, 높이 1.2m). 몸통(흰색)과 세부(유리, 후미등, 바퀴)를 따로 그린다.
        const int w = 72, h = 48;
        var body = NewPixelTex(w, h);
        var detail = NewPixelTex(w, h);
        Color glass = new Color(0.13f, 0.17f, 0.22f);
        Color light = new Color(0.95f, 0.12f, 0.12f);
        Color tire = new Color(0.06f, 0.06f, 0.07f);
        Color bumper = new Color(0.2f, 0.2f, 0.22f);
        Color plate = new Color(0.92f, 0.92f, 0.85f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool lower = x >= 2 && x <= 69 && y >= 8 && y <= 27;
                float cl = 10f + (y - 28) * 0.4f, cr = 61f - (y - 28) * 0.4f;
                bool cabin = y >= 28 && y <= 44 && x >= cl && x <= cr;
                if (lower || cabin) body.SetPixel(x, y, Color.white);

                if ((x >= 5 && x <= 16 || x >= 55 && x <= 66) && y <= 9) detail.SetPixel(x, y, tire);
                else if (x >= 2 && x <= 69 && y >= 8 && y <= 11) detail.SetPixel(x, y, bumper);
                else if ((x >= 3 && x <= 14 || x >= 57 && x <= 68) && y >= 15 && y <= 21) detail.SetPixel(x, y, light);
                else if (x >= 30 && x <= 41 && y >= 12 && y <= 17) detail.SetPixel(x, y, plate);
                else if (y >= 31 && y <= 42 && x >= cl + 4f && x <= cr - 4f) detail.SetPixel(x, y, glass);
            }
        }
        s.carBody = PixelSprite(body, 40f);
        s.carDetail = PixelSprite(detail, 40f);

        // 공사 콘 세 개 48x24 (30픽셀 = 1m 이니 폭 1.6m, 높이 0.8m)
        var cone = NewPixelTex(48, 24);
        int[] centers = { 9, 24, 39 };
        foreach (int cx in centers)
        {
            for (int y = 0; y < 22; y++)
            {
                float half = 7f * (1f - y / 22f) + 0.5f;
                for (int x = cx - 8; x <= cx + 8; x++)
                {
                    bool baseRow = y <= 2 && Mathf.Abs(x - cx) <= 8;
                    bool inCone = Mathf.Abs(x - cx) <= half;
                    if (!baseRow && !inCone) continue;
                    bool stripe = (y >= 8 && y <= 11) || (y >= 15 && y <= 16);
                    Color c = baseRow ? new Color(0.85f, 0.32f, 0.05f) : (stripe ? Color.white : new Color(1f, 0.45f, 0.1f));
                    cone.SetPixel(x, y, c);
                }
            }
        }
        s.cone = PixelSprite(cone, 30f);

        // 공 16x16 (32픽셀 = 1m 이니 지름 0.5m): 빨강/흰색 네 조각이라 구르면 돈다
        var ball = NewPixelTex(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dx = x - 7.5f, dy = y - 7.5f;
                if (dx * dx + dy * dy > 7.5f * 7.5f) continue;
                bool red = (x < 8) == (y < 8);
                ball.SetPixel(x, y, red ? new Color(0.9f, 0.15f, 0.15f) : Color.white);
            }
        }
        s.ball = PixelSprite(ball, 32f);

        // 비상등 8x8 (흰색 원, 색은 SpriteRenderer.color)
        var lamp = NewPixelTex(8, 8);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                float dx = x - 3.5f, dy = y - 3.5f;
                if (dx * dx + dy * dy <= 16f) lamp.SetPixel(x, y, Color.white);
            }
        }
        s.lamp = PixelSprite(lamp, 24f);

        // ---- 날짜 사건용 그림 (모두 코드로 그린 임시 그림. 인스펙터에서 진짜 스프라이트로 바꿀 수 있다) ----
        s.kaiju = PixelSprite(MakeKaijuTex(), 5f);          // 64x80 -> 12.8m x 16m
        s.flame = PixelSprite(MakeFlameTex(), 11f);         // 16x24 -> 1.5m x 2.2m
        s.smoke = PixelSprite(MakeSmokeTex(), 5f);          // 16x48 -> 3.2m x 9.6m
        s.hero = PixelSprite(MakeFigureTex(true), 3f);      // 24x32 -> 8m x 10.7m
        s.villain = PixelSprite(MakeFigureTex(false), 3f);
        return s;
    }

    static void PxRect(Texture2D t, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(t.height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(t.width - 1, x1); x++)
                t.SetPixel(x, y, c);
    }

    static void PxDisc(Texture2D t, float cx, float cy, float r, Color c)
    {
        for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - r)); y <= Mathf.Min(t.height - 1, Mathf.CeilToInt(cy + r)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - r)); x <= Mathf.Min(t.width - 1, Mathf.CeilToInt(cx + r)); x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) t.SetPixel(x, y, c);
    }

    // 정면을 보고 걸어오는 괴수: 초록 몸, 밝은 배, 뿔, 노란 눈, 크게 벌린 입, 등의 가시
    static Texture2D MakeKaijuTex()
    {
        var t = NewPixelTex(64, 80);
        Color body = new Color(0.22f, 0.42f, 0.28f), belly = new Color(0.58f, 0.68f, 0.42f), dark = new Color(0.12f, 0.05f, 0.05f);
        Color horn = new Color(0.88f, 0.84f, 0.68f), spike = new Color(0.62f, 0.26f, 0.2f), eye = new Color(1f, 0.92f, 0.2f);
        PxRect(t, 14, 0, 26, 16, body);                      // 다리
        PxRect(t, 38, 0, 50, 16, body);
        PxRect(t, 12, 0, 28, 3, dark);                       // 발
        PxRect(t, 36, 0, 52, 3, dark);
        for (int y = 14; y <= 56; y++)                       // 몸통
        {
            float k = 1f - Mathf.Abs(y - 34f) / 30f;
            int half = Mathf.RoundToInt(10f + 14f * Mathf.Clamp01(k));
            PxRect(t, 32 - half, y, 31 + half, y, body);
            PxRect(t, 32 - half / 2, y, 31 + half / 2, y, belly);
        }
        PxRect(t, 4, 28, 12, 48, body);                      // 팔
        PxRect(t, 52, 28, 60, 48, body);
        PxRect(t, 3, 26, 13, 30, dark);                      // 발톱
        PxRect(t, 51, 26, 61, 30, dark);
        PxDisc(t, 32f, 64f, 13f, body);                      // 머리
        PxRect(t, 20, 76, 24, 79, horn);                     // 뿔
        PxRect(t, 40, 76, 44, 79, horn);
        PxRect(t, 22, 73, 26, 75, horn);
        PxRect(t, 38, 73, 42, 75, horn);
        PxRect(t, 24, 58, 40, 62, dark);                     // 입
        PxRect(t, 27, 59, 37, 61, new Color(1f, 0.55f, 0.1f));
        PxRect(t, 24, 66, 28, 69, eye);                      // 눈
        PxRect(t, 36, 66, 40, 69, eye);
        PxRect(t, 26, 67, 27, 68, dark);
        PxRect(t, 38, 67, 39, 68, dark);
        for (int i = 0; i < 4; i++) PxRect(t, 14 + i * 12, 54 + (i % 2) * 2, 17 + i * 12, 60 + (i % 2) * 2, spike);     // 등의 가시
        return t;
    }

    // 불꽃: 바깥 주황, 안쪽 노랑, 가운데 연노랑
    static Texture2D MakeFlameTex()
    {
        var t = NewPixelTex(16, 24);
        for (int y = 0; y < 24; y++)
        {
            float k = y / 23f;
            float half = 7f * Mathf.Sin(Mathf.Clamp01(1f - k * 0.9f) * Mathf.PI * 0.5f) * (0.35f + 0.65f * Mathf.Sin(k * Mathf.PI * 0.9f + 0.4f));
            half = Mathf.Max(half, y < 3 ? 5f : 0f);
            for (int x = 0; x < 16; x++)
            {
                float dx = Mathf.Abs(x - 7.5f);
                if (dx > half) continue;
                Color c = new Color(1f, 0.42f, 0.05f);
                if (dx < half * 0.62f && y < 17) c = new Color(1f, 0.78f, 0.15f);
                if (dx < half * 0.30f && y < 11) c = new Color(1f, 0.97f, 0.7f);
                t.SetPixel(x, y, c);
            }
        }
        return t;
    }

    // 무너진 건물에서 피어오르는 연기 기둥: 아래는 불빛, 위로 갈수록 넓고 옅어지는 회색
    static Texture2D MakeSmokeTex()
    {
        var t = NewPixelTex(16, 48);
        var rnd = new System.Random(5);
        for (int y = 0; y < 48; y++)
        {
            float k = y / 47f;
            int half = Mathf.RoundToInt(3f + 4.5f * k);
            for (int x = 0; x < 16; x++)
            {
                if (Mathf.Abs(x - 7.5f) > half) continue;
                float g = 0.20f + 0.25f * k + (float)rnd.NextDouble() * 0.06f;
                Color c = new Color(g, g, g + 0.02f, Mathf.Lerp(0.9f, 0.4f, k));
                if (y < 7) c = Color.Lerp(new Color(1f, 0.5f, 0.1f, 0.95f), c, y / 7f);       // 바닥의 불
                t.SetPixel(x, y, c);
            }
        }
        return t;
    }

    // 하늘을 나는 히어로(빨간 망토, 파란 옷, 빨간 눈)와 빌런(검은 망토, 보라 옷, 뿔, 초록 눈)
    static Texture2D MakeFigureTex(bool hero)
    {
        var t = NewPixelTex(24, 32);
        Color cape = hero ? new Color(0.85f, 0.12f, 0.12f) : new Color(0.08f, 0.05f, 0.1f);
        Color suit = hero ? new Color(0.12f, 0.32f, 0.88f) : new Color(0.48f, 0.12f, 0.58f);
        Color skin = new Color(1f, 0.8f, 0.65f);
        Color eye = hero ? new Color(1f, 0.15f, 0.1f) : new Color(0.3f, 1f, 0.35f);
        PxRect(t, 3, 6, 20, 22, cape);                       // 망토 (뒤)
        PxRect(t, 7, 12, 16, 23, suit);                      // 몸
        PxRect(t, 8, 2, 11, 12, suit);                       // 다리
        PxRect(t, 13, 2, 16, 12, suit);
        PxRect(t, 1, 18, 7, 20, suit);                       // 팔
        PxRect(t, 17, 18, 23, 20, suit);
        PxRect(t, 0, 18, 1, 20, skin);                       // 주먹
        PxRect(t, 22, 18, 23, 20, skin);
        PxDisc(t, 11.5f, 26f, 4.2f, skin);                   // 머리
        PxRect(t, 9, 26, 10, 27, eye);                       // 눈
        PxRect(t, 13, 26, 14, 27, eye);
        if (!hero)
        {
            PxRect(t, 8, 30, 9, 31, cape);                   // 뿔
            PxRect(t, 14, 30, 15, 31, cape);
        }
        return t;
    }
}
