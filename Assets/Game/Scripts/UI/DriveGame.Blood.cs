using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 앞유리에 튀는 피 연출 (IMGUI, 도형으로 그린 얼룩): 사람을 칠 때마다 얼룩이 생기고,
// 연속으로 칠수록(5초 안에 또 치면 연쇄) 더 많이, 더 크게 튀어 화면을 점점 가린다.
// 치지 않고 있으면 얼룩이 아래로 흘러내리며 몇 초 안에 금방 지워진다.
public partial class DriveGame
{
    struct Splat
    {
        public float x, y, size, born, life, seed;
        public int type;                                // 0 피, 1 부케, 2 돼지 얼룩      // 위치/크기는 화면에 대한 비율 (x 는 폭, y/size 는 높이 기준)
    }

    const int MaxSplats = 48;
    const float BloodChainWindow = 5f;                  // 이 시간 안에 또 치면 연쇄

    readonly List<Splat> splats = new List<Splat>();
    int bloodChain;
    float lastBloodT = -100f;

    static readonly Color BloodDark = new Color(0.50f, 0.02f, 0.03f), BloodLight = new Color(0.82f, 0.1f, 0.1f);
    static readonly Color ZombieDark = new Color(0.16f, 0.45f, 0.1f), ZombieLight = new Color(0.6f, 0.9f, 0.3f);
    static readonly Color MudDark = new Color(0.82f, 0.38f, 0.5f), MudLight = new Color(1f, 0.76f, 0.82f);

    void ClearBlood()
    {
        wipeStart = -100f;
        splats.Clear();
        bloodChain = 0;
        lastBloodT = -100f;
    }

    // ---- 분노가 끝나면 와이퍼가 한 번 왕복하며 앞유리의 피를 닦는다 ----
    const float WipeSeconds = 1.3f;
    float wipeStart = -100f;
    bool wipeCleared;

    void StartBloodWipe()
    {
        if (splats.Count == 0 && bloodChain == 0) return;       // 닦을 게 없으면 그냥 둔다
        wipeStart = Time.unscaledTime;
        wipeCleared = false;
        sound.PlayWipe();                                        // 쓰윽
    }

    // 와이퍼 두 개가 앞유리를 쓸고 지나간다. 올라가는 중간쯤에 얼룩이 한꺼번에 지워진다. (비 오는 날의 와이퍼와는 별개)
    void DrawBloodWiper(float W, float H, float u)
    {
        float k = (Time.unscaledTime - wipeStart) / WipeSeconds;
        if (k < 0f || k > 1f) return;
        if (!wipeCleared && k >= 0.45f)
        {
            wipeCleared = true;
            splats.Clear();
            bloodChain = 0;
        }
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        float sweep = k < 0.5f ? k * 2f : (1f - k) * 2f;                    // 올라갔다가 내려온다
        sweep = sweep * sweep * (3f - 2f * sweep);
        float ang = Mathf.Lerp(-78f, -8f, sweep);
        float clipH = H * 0.80f, len = H * 0.55f;
        GUI.BeginClip(new Rect(0, 0, W, clipH));
        for (int i = 0; i < 2; i++)
        {
            var pivot = new Vector2(W * (i == 0 ? 0.28f : 0.62f), clipH + 10f * u);
            GUIUtility.RotateAroundPivot(ang, pivot);
            Fill(new Rect(pivot.x - 3f * u, pivot.y - len, 6f * u, len), new Color(0.05f, 0.05f, 0.06f, 0.9f));
            GUI.matrix = Matrix4x4.identity;
        }
        GUI.EndClip();
    }

    // 사람을 쳤다: 연쇄 횟수에 따라 얼룩 수와 크기가 늘어난다
    void AddBlood()
    {
        sound.PlayBlood();
        float now = Time.unscaledTime;
        if (now - lastBloodT > BloodChainWindow) bloodChain = 0;
        bloodChain = Mathf.Min(8, bloodChain + 1);
        lastBloodT = now;

        int n = Mathf.Min(1 + bloodChain, 7);
        float scale = 1f + 0.22f * bloodChain;
        for (int i = 0; i < n; i++)
        {
            var s = new Splat
            {
                // 첫 얼룩은 가운데쯤, 나머지는 앞유리 곳곳에 (대시보드를 덮지 않게 위쪽 70% 안)
                x = i == 0 ? Random.Range(0.35f, 0.65f) : Random.Range(0.06f, 0.94f),
                y = i == 0 ? Random.Range(0.28f, 0.5f) : Random.Range(0.08f, 0.66f),
                size = Random.Range(0.06f, 0.11f) * scale,
                born = now,
                life = 3.2f,
                seed = Random.value * 10f
            };
            if (splats.Count >= MaxSplats) splats.RemoveAt(0);
            splats.Add(s);
        }
    }

    // 돼지를 쳤다: 앞유리에 분홍 얼룩이 튄다 (피와 같은 방식, 색만 다르다)
    void AddMud()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            var s = new Splat { x = i == 0 ? Random.Range(0.35f, 0.65f) : Random.Range(0.1f, 0.9f), y = Random.Range(0.2f, 0.6f), size = Random.Range(0.06f, 0.1f), born = now, life = 2.8f, seed = Random.value * 10f, type = 2 };
            if (splats.Count >= MaxSplats) splats.RemoveAt(0);
            splats.Add(s);
        }
    }

    // 좀비를 쳤다: 초록 점액이 앞유리에 튄다
    void AddZombieSplat()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            var s = new Splat { x = i == 0 ? Random.Range(0.3f, 0.7f) : Random.Range(0.08f, 0.92f), y = Random.Range(0.15f, 0.62f), size = Random.Range(0.06f, 0.11f), born = now, life = 2.6f, seed = Random.value * 10f, type = 3 };
            if (splats.Count >= MaxSplats) splats.RemoveAt(0);
            splats.Add(s);
        }
    }

    // 결혼식 커플이 던진 부케가 앞유리에 철썩 붙는다
    void AddBouquet()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < 4; i++)
        {
            var s = new Splat { x = Random.Range(0.2f, 0.8f), y = Random.Range(0.15f, 0.6f), size = Random.Range(0.05f, 0.09f), born = now, life = 2.6f, seed = Random.value * 10f, type = 1 };
            if (splats.Count >= MaxSplats) splats.RemoveAt(0);
            splats.Add(s);
        }
    }

    // 매 프레임 (OnGUI, 앞유리 위에 그린다). 오래된 얼룩은 흐려지며 아래로 흘러내리고 사라진다.
    void DrawBlood(float W, float H, float u)
    {
        if (splats.Count == 0) return;
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        float now = Time.unscaledTime;

        // 연쇄가 길수록 화면 가장자리부터 붉게 번진다
        float chainK = Mathf.Clamp01(bloodChain / 8f) * Mathf.Clamp01(1f - (now - lastBloodT) / BloodChainWindow);
        if (chainK > 0.01f) Fill(new Rect(0, 0, W, H * 0.80f), new Color(0.45f, 0f, 0f, 0.22f * chainK));

        for (int i = splats.Count - 1; i >= 0; i--)
        {
            var s = splats[i];
            float age = now - s.born;
            float left = s.life - age;
            if (left <= 0f)
            {
                splats.RemoveAt(i);
                continue;
            }
            float a = Mathf.Clamp01(left / 1.2f) * 0.92f;                       // 사라지기 직전 1.2초 동안 흐려진다
            float pop = 1f + 0.5f * Mathf.Clamp01(1f - age / 0.1f);              // 튀는 순간 크게 퍼졌다가 자리 잡는다
            float cx = s.x * W;
            float cy = (s.y + age * 0.02f) * H;                                  // 천천히 흘러내린다
            float r = s.size * H * pop;

            if (s.type == 1)
            {
                // 부케: 분홍/흰 꽃잎 여섯 장과 노란 중심, 초록 줄기
                Color pet = new Color(1f, 0.55f, 0.75f, a), pet2 = new Color(1f, 0.95f, 0.95f, a);
                for (int p = 0; p < 6; p++)
                {
                    float pa = p * 60f * Mathf.Deg2Rad + s.seed;
                    Dot(new Vector2(cx + Mathf.Cos(pa) * r * 0.55f, cy + Mathf.Sin(pa) * r * 0.55f), r * 0.55f, p % 2 == 0 ? pet : pet2);
                }
                Dot(new Vector2(cx, cy), r * 0.4f, new Color(1f, 0.85f, 0.3f, a));
                Fill(new Rect(cx - r * 0.06f, cy + r * 0.4f, r * 0.12f, r * 0.9f), new Color(0.3f, 0.75f, 0.35f, a));
                continue;
            }
            Color bd = s.type == 3 ? ZombieDark : (s.type == 2 ? MudDark : BloodDark), bl = s.type == 3 ? ZombieLight : (s.type == 2 ? MudLight : BloodLight);
            Color dark = new Color(bd.r, bd.g, bd.b, a);
            Color light = new Color(bl.r, bl.g, bl.b, a * 0.85f);

            // 중심 얼룩과 튄 방울들
            Dot(new Vector2(cx, cy), r, dark);
            for (int k = 0; k < 4; k++)
            {
                float ang = (s.seed + k * 1.7f) * 1.9f;
                float dist = r * (0.62f + 0.3f * Mathf.Abs(Mathf.Sin(s.seed * 3f + k)));
                float ds = r * (0.22f + 0.14f * Mathf.Abs(Mathf.Cos(s.seed + k * 2.3f)));
                Dot(new Vector2(cx + Mathf.Cos(ang) * dist, cy + Mathf.Sin(ang) * dist), ds, dark);
            }
            Dot(new Vector2(cx - r * 0.18f, cy - r * 0.2f), r * 0.32f, light);       // 번들거리는 하이라이트

            // 흘러내린 자국 (시간이 갈수록 길어진다)
            float dripLen = r * (0.5f + Mathf.Min(age, 2.5f) * 0.9f);
            float top = cy + r * 0.3f;
            dripLen = Mathf.Min(dripLen, H * 0.78f - top);                      // 대시보드까지는 흘러내리지 않는다
            if (dripLen > 2f) Fill(new Rect(cx - r * 0.07f, top, r * 0.14f, dripLen), dark);
        }
    }
}
