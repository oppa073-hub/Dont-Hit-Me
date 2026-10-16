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
        return s;
    }
}
