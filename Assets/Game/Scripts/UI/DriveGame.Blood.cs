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
        public float x, y, size, born, life, seed;      // 위치/크기는 화면에 대한 비율 (x 는 폭, y/size 는 높이 기준)
    }

    const int MaxSplats = 48;
    const float BloodChainWindow = 5f;                  // 이 시간 안에 또 치면 연쇄

    readonly List<Splat> splats = new List<Splat>();
    int bloodChain;
    float lastBloodT = -100f;

    static readonly Color BloodDark = new Color(0.50f, 0.02f, 0.03f), BloodLight = new Color(0.82f, 0.1f, 0.1f);

    void ClearBlood()
    {
        splats.Clear();
        bloodChain = 0;
        lastBloodT = -100f;
    }

    // 사람을 쳤다: 연쇄 횟수에 따라 얼룩 수와 크기가 늘어난다
    void AddBlood()
    {
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

            Color dark = new Color(BloodDark.r, BloodDark.g, BloodDark.b, a);
            Color light = new Color(BloodLight.r, BloodLight.g, BloodLight.b, a * 0.85f);

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
