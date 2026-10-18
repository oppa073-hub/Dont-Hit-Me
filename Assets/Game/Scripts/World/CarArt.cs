using System.Collections.Generic;
using UnityEngine;

// 외부 도트 자동차 그림(Free Pixel Pack Car Sprites): Tools 가 잘라 둔 Resources/Cars/carNN_rear.png, carNN_front.png 를 읽는다.
// 같은 방향으로 달리는 차는 뒷모습(rear), 마주 오는 차/룸미러에 비치는 차는 앞모습(front). 파일이 없으면 null (예전 도형 그림을 쓴다).
// 모든 차의 폭을 같게(2m) 맞춘다. 발(바퀴 바닥)이 기준점.
public static class CarArt
{
    const float CarWidth = 2f;                     // 어떤 그림이든 차 폭은 2m 로 맞춘다 (차종마다 폭이 달라 홀쭉/뚱뚱해 보이지 않게)

    static bool loaded;
    static readonly List<Sprite> rear = new List<Sprite>();
    static readonly List<Sprite> front = new List<Sprite>();

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        for (int i = 1; i < 100; i++)
        {
            var r = Resources.Load<Texture2D>("Cars/car" + i.ToString("00") + "_rear");
            var f = Resources.Load<Texture2D>("Cars/car" + i.ToString("00") + "_front");
            if (r == null || f == null) break;
            if (r.height > r.width * 0.95f) continue;       // 위에서 본 트럭처럼 홀쭉한 그림은 뺀다
            rear.Add(Make(r));
            front.Add(Make(f));
        }
    }

    static Sprite Make(Texture2D t)
    {
        t.filterMode = FilterMode.Point;           // 도트가 번지지 않게
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0f), t.width / CarWidth, 0, SpriteMeshType.FullRect);
    }

    public static bool Available { get { Load(); return rear.Count > 0; } }

    /// <summary>무작위 차 하나의 (뒷모습, 앞모습) 번호. 없으면 -1.</summary>
    public static int RandomIndex() { Load(); return rear.Count > 0 ? Random.Range(0, rear.Count) : -1; }
    public static Sprite Rear(int i) { Load(); return i >= 0 && i < rear.Count ? rear[i] : null; }
    public static Sprite Front(int i) { Load(); return i >= 0 && i < front.Count ? front[i] : null; }
}
