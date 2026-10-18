using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 내비 화면의 뉴스: 운전을 시작하면 지도 대신 앵커가 오늘이 어떤 날인지 방송하고, 사건이 터지면 긴급 속보가 끼어든다.
// 화면은 "토막(NewsBeat)"의 연속이다: 앵커 / 현장 연결(지직거리며 끊김) / 광고 / 긴급 속보. 배경 그림, 머리 띠, 앵커 연기는 날 종류별 뉴스 한 벌(NewsCatalog.Kits)을 따른다.
// 새 날 종류(이벤트)를 만들면 NewsCatalog.Kits 에 한 줄 추가하고, 배경이 새로 필요하면 아래 DrawNewsBg 의 switch 에 한 가지 더 그리면 된다.
// 손님을 태우거나 N 키를 누르면 바로 지도로 돌아온다. 모두 내비 화면 안의 사각형과 3D 글자로 그린다.
public partial class DriveGame
{
    Transform newsRoot, newsStudio, newsHeader, newsTicker, newsHead, newsHair, newsEyeL, newsEyeR, newsBody, newsTie, newsMouth, newsSweat, newsFlash, newsPanel;
    TextMesh newsBadgeText, newsBodyText, newsTickerText, newsZzText;
    Transform[] newsAnchorParts;
    readonly List<Transform> newsDeco = new List<Transform>();
    readonly Dictionary<int, Material> newsMats = new Dictionary<int, Material>();

    bool newsActive, newsShown;          // newsActive = 뉴스 시간, newsShown = 화면이 지금 뉴스 모습인지
    float newsT;
    NewsBeat[] newsBeats = new NewsBeat[0];
    float[] newsEnds = new float[0];
    DayType newsType;
    bool newsBulletin, newsOver, newsAnchorOn = true, newsCentered;
    int newsBeatIdx = -1, newsShownChars = -1, newsTickIdx = -1;
    string newsBadgeShown = "";
    string newsSubSpeaker = "", newsSubText = "";       // 앞유리 아래 큰 자막 (내비 화면이 작아서 같은 대사를 크게 한 번 더 보여 준다)
    Color newsSubColor = Color.white;
    int newsSubChars = -1, newsSubBeat = -1;
    float newsW, newsH;                  // 화면 크기(m): 모든 장식은 이 안으로 잘라서 화면 밖으로 튀어나오지 않게 한다
    Vector2 newsShake;                   // 장식 흔들림

    void BuildNews()
    {
        newsRoot = new GameObject("News").transform;
        newsRoot.SetParent(navScreen, false);
        newsStudio = CabinPart("NewsStudio", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.08f, 0.16f, 0.38f)));
        newsHeader = CabinPart("NewsHeader", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.78f, 0.1f, 0.1f)));
        newsTicker = CabinPart("NewsTicker", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.08f, 0.08f, 0.12f)));
        newsPanel = CabinPart("NewsPanel", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.04f, 0.06f, 0.13f)));
        newsBody = CabinPart("NewsSuit", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.1f, 0.1f, 0.16f)));
        newsTie = CabinPart("NewsTie", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.85f, 0.15f, 0.15f)));
        newsHead = CabinPart("NewsHead", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.96f, 0.8f, 0.66f)));
        newsHair = CabinPart("NewsHair", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.12f, 0.08f, 0.06f)));
        newsEyeL = CabinPart("NewsEyeL", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.05f, 0.05f, 0.05f)));
        newsEyeR = CabinPart("NewsEyeR", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.05f, 0.05f, 0.05f)));
        newsMouth = CabinPart("NewsMouth", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.45f, 0.08f, 0.08f)));
        newsSweat = CabinPart("NewsSweat", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.5f, 0.8f, 1f)));
        newsFlash = CabinPart("NewsFlash", PrimitiveType.Quad, newsRoot, Unlit(Color.white));
        newsAnchorParts = new[] { newsBody, newsTie, newsHead, newsHair, newsEyeL, newsEyeR, newsMouth };

        newsBadgeText = CabinText("NewsBadge", newsRoot, 0.0090f, TextAnchor.MiddleLeft, Color.white);
        newsBadgeText.alignment = TextAlignment.Left;
        newsBadgeText.text = "시티뉴스  LIVE";
        newsBodyText = CabinText("NewsBody", newsRoot, 0.0100f, TextAnchor.UpperLeft, Color.white);
        newsBodyText.alignment = TextAlignment.Left;
        newsTickerText = CabinText("NewsTickerText", newsRoot, 0.0072f, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
        newsTickerText.text = "N 키: 뉴스 건너뛰기";
        newsZzText = CabinText("NewsZz", newsRoot, 0.012f, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));
        newsZzText.text = "z Z";
        newsRoot.gameObject.SetActive(false);
    }

    // 운전을 시작할 때: 오늘의 뉴스를 방송한다
    void StartNews()
    {
        var beats = NewsCatalog.Script(dayEvents.Type, dayEvents.Day, shiftMode, Mathf.RoundToInt(shiftSeconds), dayEvents.LastType, weather.Rain, weather.Darkness, projects.DoneCount, HeatFor(dayEvents.Day));
        BeginNews(beats, dayEvents.Type, false, false);
        sound.PlayArrive();                  // 손님 호출음(차임)과 다른 소리
    }

    // 사건(괴수, 히어로)이 시작됐다: 손님을 찾는 중이면 내비에 긴급 속보가 끼어든다 (손님을 태웠거나 대화 중이면 지도를 그대로 둔다)
    void StartBulletin(DayType t)
    {
        if (flow != GameFlow.Playing || gameOver || taxi.State != TaxiState.Cruise) return;
        var beats = NewsCatalog.Bulletin(t);
        if (beats == null) return;
        BeginNews(beats, t, true, false);
    }

    // 사건이 끝났다: 손님을 찾는 중이면 내비에 후일담 뉴스가 잠깐 나온다
    void StartOverNews(DayType t)
    {
        if (flow != GameFlow.Playing || gameOver || taxi.State != TaxiState.Cruise) return;
        var beats = NewsCatalog.Over(t);
        if (beats == null) return;
        BeginNews(beats, t, false, true);
        sound.PlayChime();
    }

    void BeginNews(NewsBeat[] beats, DayType type, bool bulletin, bool over)
    {
        newsBeats = beats;
        newsEnds = new float[beats.Length];
        float acc = 0f;
        for (int i = 0; i < beats.Length; i++) { acc += beats[i].Seconds; newsEnds[i] = acc; }
        newsType = type;
        newsBulletin = bulletin;
        newsOver = over;
        newsT = 0f;
        newsActive = true;
        newsBeatIdx = -1;
        newsSubBeat = newsSubChars = -1;
        newsShownChars = newsTickIdx = -1;
        newsBadgeShown = "";
    }

    void EndNews()
    {
        newsActive = false;
    }

    // 메인 루프에서 매 프레임: 시간이 지나면 뉴스를 끝낸다
    void UpdateNews(float dt, Keyboard kb)
    {
        if (!newsActive) return;
        if (kb != null && kb.nKey.wasPressedThisFrame) { EndNews(); return; }
        newsT += dt;
        if (newsEnds.Length == 0 || newsT >= newsEnds[newsEnds.Length - 1] + 0.5f) EndNews();
    }

    // 내비 화면을 뉴스 모습으로: 지도 조각들을 숨기고 스튜디오를 켠다
    void EnterNewsView()
    {
        newsShown = true;
        newsRoot.gameObject.SetActive(true);
        HideFrom(navGridV, 0);
        HideFrom(navGridH, 0);
        HideFrom(navRoute, 0);
        navDest.gameObject.SetActive(false);
        navCaller.gameObject.SetActive(false);
        navCar.gameObject.SetActive(false);
        navBar.gameObject.SetActive(false);
        navHeadText.gameObject.SetActive(false);
        navInfoText.gameObject.SetActive(false);
    }

    // 뉴스가 끝나 지도로 돌아온다
    void ExitNewsView()
    {
        newsShown = false;
        newsRoot.localPosition = Vector3.zero;
        newsRoot.gameObject.SetActive(false);
        navCar.gameObject.SetActive(true);
        navBar.gameObject.SetActive(true);
        navHeadText.gameObject.SetActive(true);
        navInfoText.gameObject.SetActive(true);
        navHead = -1;
        navRem = -3;                         // 아래 정보 글자를 다시 만들게 한다
    }

    // ---- 그리기 도우미 ----

    static void SetCol(Transform t, Color c)
    {
        t.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", c);
    }

    Material NewsMat(Color c)
    {
        int key = (Mathf.RoundToInt(c.r * 255f) << 16) | (Mathf.RoundToInt(c.g * 255f) << 8) | Mathf.RoundToInt(c.b * 255f);
        Material m;
        if (!newsMats.TryGetValue(key, out m)) { m = Unlit(c); newsMats[key] = m; }
        return m;
    }

    // 배경 장식 사각형 하나 (풀에서 꺼내 쓴다). x,y 는 화면 중심 기준(m)
    void NDeco(ref int n, float x, float y, float w, float h, Color c)
    {
        if (n >= newsDeco.Count) newsDeco.Add(CabinPart("NewsDeco", PrimitiveType.Quad, newsRoot, NewsMat(c)));
        var q = newsDeco[n++];
        ClipToScreen(ref x, ref y, ref w, ref h);
        bool visible = w > 0.0002f && h > 0.0002f;
        if (q.gameObject.activeSelf != visible) q.gameObject.SetActive(visible);
        q.localPosition = new Vector3(x, y, -0.0038f);
        q.localScale = new Vector3(Mathf.Max(0.0001f, w), Mathf.Max(0.0001f, h), 1f);
        var rd = q.GetComponent<Renderer>();
        var m = NewsMat(c);
        if (rd.sharedMaterial != m) rd.sharedMaterial = m;
    }

    // 사각형(중심 x,y 크기 w,h)을 화면 안으로 잘라낸다. 흔들림도 여기서 더해 화면 밖으로 삐져나가지 않게 한다
    void ClipToScreen(ref float x, ref float y, ref float w, ref float h)
    {
        x += newsShake.x; y += newsShake.y;
        float x0 = Mathf.Max(x - w * 0.5f, -newsW * 0.5f), x1 = Mathf.Min(x + w * 0.5f, newsW * 0.5f);
        float y0 = Mathf.Max(y - h * 0.5f, -newsH * 0.5f), y1 = Mathf.Min(y + h * 0.5f, newsH * 0.5f);
        w = Mathf.Max(0f, x1 - x0); h = Mathf.Max(0f, y1 - y0);
        x = (x0 + x1) * 0.5f; y = (y0 + y1) * 0.5f;
    }

    // 날 종류별 배경 그림. intense = 현장 연결/긴급 속보 (더 격하게 움직인다)
    void DrawNewsBg(NewsBg bg, float w, float h, bool intense, float t)
    {
        int n = 0;
        float floorY = -h * 0.36f;
        float spd = intense ? 1.7f : 1f;
        switch (bg)
        {
            case NewsBg.City:
            {
                SetCol(newsStudio, new Color(0.42f, 0.68f, 0.88f));
                NDeco(ref n, w * 0.40f, h * 0.20f, h * 0.13f, h * 0.13f, new Color(1f, 0.92f, 0.4f));                 // 해
                float[] bh = { 0.30f, 0.46f, 0.26f, 0.52f, 0.36f, 0.28f, 0.42f, 0.32f };
                for (int i = 0; i < bh.Length; i++)
                {
                    float bw = w * 0.115f, bx = -w * 0.46f + i * w * 0.13f;
                    NDeco(ref n, bx, floorY + h * bh[i] * 0.5f, bw, h * bh[i], new Color(0.2f, 0.3f, 0.45f));
                }
                break;
            }
            case NewsBg.Fire:
            {
                SetCol(newsStudio, new Color(0.30f, 0.09f, 0.06f));
                NDeco(ref n, 0f, floorY + h * 0.12f, w, h * 0.26f, new Color(0.7f, 0.22f, 0.05f));                    // 불타는 노을
                float[] bh = { 0.30f, 0.42f, 0.24f, 0.46f, 0.34f };
                for (int i = 0; i < bh.Length; i++)
                {
                    float bx = -w * 0.46f + i * w * 0.15f;
                    NDeco(ref n, bx, floorY + h * bh[i] * 0.5f, w * 0.13f, h * bh[i], new Color(0.07f, 0.05f, 0.05f));
                    float fl = 0.09f + 0.07f * Mathf.Pow(Mathf.Sin(t * 9f * spd + i * 1.7f), 2f);                       // 건물 위의 불꽃
                    NDeco(ref n, bx, floorY + h * bh[i] + fl * h * 0.5f, w * 0.05f, fl * h, i % 2 == 0 ? new Color(1f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.2f));
                }
                // 건물 뒤로 얼굴을 내민 괴수
                float by = Mathf.Sin(t * 2.4f * spd) * h * 0.02f;
                float kx = w * 0.34f;
                Color kg = new Color(0.18f, 0.42f, 0.2f);
                NDeco(ref n, kx, floorY + h * 0.32f + by, w * 0.20f, h * 0.62f, kg);                                  // 몸
                NDeco(ref n, kx - w * 0.01f, floorY + h * 0.66f + by, w * 0.18f, h * 0.17f, new Color(0.22f, 0.5f, 0.24f));      // 머리
                NDeco(ref n, kx - w * 0.05f, floorY + h * 0.70f + by, w * 0.03f, h * 0.05f, new Color(1f, 0.95f, 0.2f));      // 눈
                NDeco(ref n, kx + w * 0.03f, floorY + h * 0.70f + by, w * 0.03f, h * 0.05f, new Color(1f, 0.95f, 0.2f));
                for (int i = 0; i < 4; i++) NDeco(ref n, kx - w * 0.06f + i * w * 0.035f, floorY + h * 0.61f + by, w * 0.018f, h * 0.04f, Color.white);   // 이빨
                float br = 0.06f + 0.05f * Mathf.Pow(Mathf.Sin(t * 7f * spd), 2f);
                NDeco(ref n, kx - w * 0.14f - br * w * 0.5f, floorY + h * 0.62f + by, br * w, h * 0.05f, new Color(1f, 0.55f, 0.1f));   // 입김 불
                break;
            }
            case NewsBg.Storm:
            {
                SetCol(newsStudio, new Color(0.10f, 0.08f, 0.22f));
                NDeco(ref n, -w * 0.25f, h * 0.30f, w * 0.40f, h * 0.14f, new Color(0.24f, 0.22f, 0.34f));
                NDeco(ref n, w * 0.20f, h * 0.27f, w * 0.46f, h * 0.16f, new Color(0.2f, 0.18f, 0.3f));
                NDeco(ref n, w * 0.0f, floorY + h * 0.12f, w, h * 0.24f, new Color(0.1f, 0.1f, 0.16f));
                float hx = -w * 0.12f + Mathf.Sin(t * 1.6f * spd) * w * 0.03f, hy = h * 0.10f + Mathf.Sin(t * 2.3f * spd) * h * 0.03f;
                float vx = w * 0.36f - Mathf.Sin(t * 1.9f * spd) * w * 0.03f, vy = h * 0.16f + Mathf.Sin(t * 2.7f * spd) * h * 0.03f;
                NDeco(ref n, hx - w * 0.04f, hy - h * 0.03f, w * 0.05f, h * 0.15f, new Color(0.9f, 0.15f, 0.15f));        // 히어로 망토
                NDeco(ref n, hx, hy, w * 0.07f, h * 0.14f, new Color(0.2f, 0.45f, 0.95f));
                NDeco(ref n, hx, hy + h * 0.10f, w * 0.05f, h * 0.06f, new Color(0.96f, 0.8f, 0.66f));
                NDeco(ref n, vx, vy, w * 0.07f, h * 0.14f, new Color(0.45f, 0.1f, 0.55f));                                // 빌런
                NDeco(ref n, vx, vy + h * 0.10f, w * 0.05f, h * 0.06f, new Color(0.55f, 0.7f, 0.5f));
                if (Mathf.Repeat(t * 3f * spd, 1f) < 0.7f)                                                                 // 눈에서 나오는 레이저
                    NDeco(ref n, (hx + vx) * 0.5f, (hy + vy) * 0.5f + h * 0.04f, Mathf.Abs(vx - hx) - w * 0.08f, h * 0.014f, new Color(1f, 0.15f, 0.15f));
                if (Mathf.Repeat(t * 0.9f * spd, 1f) < 0.35f)                                                              // 번개
                {
                    float lx = w * 0.04f + Mathf.Floor(t * 0.9f * spd) % 3 * w * 0.12f;
                    NDeco(ref n, lx, h * 0.18f, w * 0.012f, h * 0.2f, new Color(1f, 0.95f, 0.4f));
                    NDeco(ref n, lx + w * 0.02f, h * 0.04f, w * 0.012f, h * 0.14f, new Color(1f, 0.95f, 0.4f));
                    NDeco(ref n, lx, -h * 0.07f, w * 0.012f, h * 0.12f, new Color(1f, 0.95f, 0.4f));
                }
                break;
            }
            case NewsBg.Zombie:
            {
                SetCol(newsStudio, new Color(0.03f, 0.09f, 0.06f));
                NDeco(ref n, w * 0.38f, h * 0.20f, h * 0.15f, h * 0.15f, new Color(0.78f, 0.95f, 0.55f));                       // 초록빛 달
                float[] bh = { 0.30f, 0.44f, 0.26f, 0.5f, 0.34f, 0.28f, 0.4f };
                for (int i = 0; i < bh.Length; i++)
                    NDeco(ref n, -w * 0.46f + i * w * 0.15f, floorY + h * bh[i] * 0.5f, w * 0.12f, h * bh[i], new Color(0.05f, 0.12f, 0.09f));
                NDeco(ref n, 0f, floorY + h * 0.05f, w, h * 0.14f, new Color(0.1f, 0.28f, 0.16f));                                // 바닥 안개
                for (int i = 0; i < 6; i++)
                {
                    // 비틀거리며 다가오는 좀비 실루엣 (팔을 앞으로 뻗는다)
                    float zx = -w * 0.42f + i * w * 0.17f + Mathf.Sin(t * 1.4f * spd + i * 2.1f) * w * 0.015f;
                    float bob = Mathf.Abs(Mathf.Sin(t * 3.2f * spd + i * 1.7f)) * h * 0.02f;
                    NDeco(ref n, zx, floorY + h * 0.11f + bob, w * 0.04f, h * 0.15f, new Color(0.25f, 0.5f, 0.25f));                  // 몸
                    NDeco(ref n, zx, floorY + h * 0.215f + bob, w * 0.04f, h * 0.06f, new Color(0.45f, 0.78f, 0.4f));                 // 머리
                    NDeco(ref n, zx + w * 0.035f, floorY + h * 0.16f + bob, w * 0.045f, h * 0.018f, new Color(0.45f, 0.78f, 0.4f));   // 뻗은 팔
                }
                break;
            }
            default: // Festival
            {
                SetCol(newsStudio, new Color(0.5f, 0.5f, 0.92f));
                Color[] pal = { new Color(1f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.85f, 0.45f), new Color(0.35f, 0.65f, 1f), new Color(0.95f, 0.45f, 0.9f) };
                for (int i = 0; i < 11; i++) NDeco(ref n, -w * 0.5f + i * w * 0.1f, h * 0.31f - Mathf.Abs(i - 5) * 0f, w * 0.05f, h * 0.06f, pal[i % pal.Length]);   // 깃발 줄
                for (int i = 0; i < 6; i++)
                {
                    float bx = -w * 0.40f + i * w * 0.17f, by = h * 0.12f + Mathf.Sin(t * 1.6f * spd + i * 1.3f) * h * 0.04f;
                    NDeco(ref n, bx, by - h * 0.10f, w * 0.004f, h * 0.14f, Color.white);                                  // 풍선 줄
                    NDeco(ref n, bx, by, w * 0.07f, h * 0.10f, pal[(i + 2) % pal.Length]);                                   // 풍선
                }
                for (int i = 0; i < 14; i++)
                {
                    float bob = Mathf.Abs(Mathf.Sin(t * 4f * spd + i * 2.1f)) * h * 0.03f;
                    float px = -w * 0.47f + i * w * 0.072f;
                    NDeco(ref n, px, floorY + h * 0.06f + bob, w * 0.05f, h * 0.10f, pal[i % pal.Length]);                   // 몸
                    NDeco(ref n, px, floorY + h * 0.14f + bob, w * 0.04f, h * 0.06f, new Color(0.9f, 0.72f, 0.58f));         // 머리
                }
                break;
            }
        }
        HideFrom(newsDeco, n);
    }

    // 광고 화면: 번쩍이는 테두리
    void DrawNewsAd(float w, float h, float t)
    {
        int n = 0;
        SetCol(newsStudio, new Color(0.85f, 0.14f, 0.52f));
        Color c = Mathf.Repeat(t * 4f, 1f) < 0.5f ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 1f, 1f);
        NDeco(ref n, 0f, h * 0.34f, w, h * 0.03f, c);
        NDeco(ref n, 0f, -h * 0.34f, w, h * 0.03f, c);
        NDeco(ref n, -w * 0.49f, 0f, w * 0.02f, h * 0.7f, c);
        NDeco(ref n, w * 0.49f, 0f, w * 0.02f, h * 0.7f, c);
        HideFrom(newsDeco, n);
    }

    // 매 프레임 뉴스 화면 배치와 글자
    void UpdateNewsView()
    {
        float w = Mathf.Max(0.05f, navSize.x), h = Mathf.Max(0.04f, navSize.y);
        float t = newsT;
        float ts = w / 0.26f;                                  // 화면 크기에 비례해 글자 크기도 맞춘다
        newsBadgeText.transform.localScale = Vector3.one * ts;
        newsBodyText.transform.localScale = Vector3.one * ts;
        newsTickerText.transform.localScale = Vector3.one * ts;
        newsZzText.transform.localScale = Vector3.one * ts;
        var kit = NewsCatalog.Kit(newsType);

        // 지금 토막
        int idx = newsEnds.Length - 1;
        for (int i = 0; i < newsEnds.Length; i++) if (t < newsEnds[i]) { idx = i; break; }
        if (idx < 0) return;
        var beat = newsBeats[idx];
        float bt = t - (idx > 0 ? newsEnds[idx - 1] : 0f);
        if (idx != newsBeatIdx)
        {
            newsBeatIdx = idx;
            newsShownChars = -1;
            if (beat.kind == 1) sound.PlayNearMiss();          // 현장 연결: 치직
            else if (beat.kind == 2) sound.PlayChime();        // 광고: 띠링
        }
        bool ad = beat.kind == 2, field = beat.kind == 1;
        bool intense = field || beat.kind == 3;
        bool calm = kit.blink && !newsBulletin;                // 괴수/히어로/좀비 날의 아침 방송(주의보)과 후일담은 차분하다
        NewsMood mood = (newsBulletin || beat.kind == 3) ? NewsMood.Panic : ((calm || newsOver) ? NewsMood.Sleepy : kit.mood);

        // 화면 전체 흔들림 (놀란 뉴스일수록)
        newsW = w; newsH = h;
        Vector2 shake = Vector2.zero;
        if (mood == NewsMood.Panic) shake = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * h * (intense ? 0.014f : 0.007f);
        else if (field) shake = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * h * 0.008f;
        newsShake = shake;                   // 배경 그림만 흔든다 (화면 틀과 글자는 그대로)
        newsRoot.localPosition = Vector3.zero;

        // 배경
        newsStudio.localPosition = new Vector3(0f, 0f, -0.0035f);
        newsStudio.localScale = new Vector3(w, h, 1f);
        if (ad) DrawNewsAd(w, h, t); else DrawNewsBg(newsOver ? NewsBg.City : kit.bg, w, h, intense, t);

        // 머리 띠 (사건이면 점멸) 와 하단 자막
        float hy = h * 0.5f - h * 0.07f;
        newsHeader.localPosition = new Vector3(0f, hy, -0.004f);
        newsHeader.localScale = new Vector3(w, h * 0.14f, 1f);
        newsBadgeText.transform.localPosition = new Vector3(-w * 0.5f + 0.006f, hy, -0.0045f);
        string badge = ad ? "광고" : (field ? "현장 연결  LIVE" : (newsOver ? "시티뉴스  사태 종료" : (calm ? "시티뉴스  오늘의 예보" : kit.badge)));
        Color bc = ad ? new Color(0.55f, 0.15f, 0.7f) : (field ? new Color(0.9f, 0.5f, 0.1f) : (newsOver ? new Color(0.15f, 0.55f, 0.3f) : (calm ? new Color(0.85f, 0.5f, 0.1f) : kit.badgeColor)));
        if ((kit.blink || beat.kind == 3) && !ad && !calm && !newsOver && Mathf.Repeat(t * 2.6f, 1f) < 0.45f) bc = Color.Lerp(bc, new Color(0.12f, 0.02f, 0.02f), 0.8f);
        SetCol(newsHeader, bc);
        if (badge != newsBadgeShown) { newsBadgeShown = badge; newsBadgeText.text = badge; }

        float ty = -h * 0.5f + h * 0.07f;
        newsTicker.localPosition = new Vector3(0f, ty, -0.004f);
        newsTicker.localScale = new Vector3(w, h * 0.14f, 1f);
        newsTickerText.transform.localPosition = new Vector3(0f, ty, -0.0045f);
        int ti = t < 2.6f ? -1 : Mathf.FloorToInt((t - 2.6f) / 3f);
        if (ti != newsTickIdx)
        {
            newsTickIdx = ti;
            newsTickerText.text = ti < 0 ? "N 키: 뉴스 건너뛰기" : (ad ? "▶ 광고 문의: 박 택시 (합법 아님)" : NewsCatalog.Tick(newsType, ti));
        }

        // 앵커 (광고에는 나오지 않는다)
        if (newsAnchorOn == ad)
        {
            newsAnchorOn = !ad;
            foreach (var p in newsAnchorParts) p.gameObject.SetActive(newsAnchorOn);
            if (ad) { newsSweat.gameObject.SetActive(false); newsZzText.gameObject.SetActive(false); }
        }

        float cx = -w * 0.31f;
        if (mood == NewsMood.Squeezed) cx += Mathf.Sin(t * 3f) * w * 0.035f;                // 인파에 밀려 좌우로 흔들림
        if (mood == NewsMood.Panic) cx += Random.Range(-1f, 1f) * h * 0.012f;
        float talk = Mathf.Sin(t * 12f) * h * 0.006f;
        float headY = h * 0.04f + talk;
        // 현장 기자: 주황 조끼와 다른 머리색
        SetCol(newsBody, field ? new Color(0.9f, 0.5f, 0.1f) : new Color(0.1f, 0.1f, 0.16f));
        SetCol(newsHair, field ? new Color(0.55f, 0.28f, 0.1f) : new Color(0.12f, 0.08f, 0.06f));
        if (!ad)
        {
            newsBody.localPosition = new Vector3(cx, headY - h * 0.27f, -0.0045f);
            newsBody.localScale = new Vector3(h * 0.46f, h * 0.24f, 1f);
            newsTie.localPosition = new Vector3(cx, headY - h * 0.21f, -0.0048f);
            newsTie.localScale = field ? Vector3.zero : new Vector3(h * 0.04f, h * 0.12f, 1f);
            newsHead.localPosition = new Vector3(cx, headY, -0.005f);
            newsHead.localScale = new Vector3(h * 0.20f, h * 0.24f, 1f);
            newsHair.localPosition = new Vector3(cx, headY + h * 0.115f, -0.0052f);
            newsHair.localScale = new Vector3(h * 0.22f, h * 0.07f, 1f);

            // 눈: 졸림(거의 감김) / 당황(커다랗게) / 번쩍임에 눈이 부심
            float eh = h * 0.03f, ew = h * 0.025f;
            bool lightning = mood == NewsMood.Dazzled && Mathf.Repeat(t, 1.7f) < 0.35f;
            if (mood == NewsMood.Sleepy) eh = Mathf.Repeat(t, 3.3f) < 0.25f ? h * 0.03f : h * 0.006f;
            else if (mood == NewsMood.Panic) { eh = h * 0.058f; ew = h * 0.04f; }
            else if (lightning) eh = h * 0.004f;
            newsEyeL.localPosition = new Vector3(cx - h * 0.045f, headY + h * 0.02f, -0.0054f);
            newsEyeL.localScale = new Vector3(ew, eh, 1f);
            newsEyeR.localPosition = new Vector3(cx + h * 0.045f, headY + h * 0.02f, -0.0054f);
            newsEyeR.localScale = new Vector3(ew, eh, 1f);

            // 입: 글자가 나오는 동안 벌렸다 닫았다
            bool talking = newsShownChars < 0 || newsShownChars < StoryCatalog.Wrap(beat.text, 11f).Length;
            float open = (talking && Mathf.Sin(t * 18f) > -0.2f) ? h * 0.032f : h * 0.008f;
            if (mood == NewsMood.Panic) open = Mathf.Max(open, h * 0.03f);
            newsMouth.localPosition = new Vector3(cx, headY - h * 0.075f, -0.0055f);
            newsMouth.localScale = new Vector3(h * 0.06f, open, 1f);

            // 땀방울(당황) / zZ(졸림)
            bool sweat = mood == NewsMood.Panic;
            if (newsSweat.gameObject.activeSelf != sweat) newsSweat.gameObject.SetActive(sweat);
            if (sweat)
            {
                newsSweat.localPosition = new Vector3(cx + h * 0.115f, headY + h * 0.09f - Mathf.Repeat(t * 1.3f, 1f) * h * 0.2f, -0.0056f);
                newsSweat.localScale = new Vector3(h * 0.022f, h * 0.034f, 1f);
            }
            bool zz = mood == NewsMood.Sleepy;
            if (newsZzText.gameObject.activeSelf != zz) newsZzText.gameObject.SetActive(zz);
            if (zz) newsZzText.transform.localPosition = new Vector3(cx + h * 0.16f, headY + h * 0.17f + Mathf.Sin(t * 2f) * h * 0.012f, -0.0056f);
        }

        // 번쩍임(히어로 뉴스: 눈이 부실 만큼 하얗게) 과 현장 연결이 끊길 때의 지직 잡음
        bool whiteFlash = mood == NewsMood.Dazzled && !ad && Mathf.Repeat(t, 1.7f) < 0.07f;
        bool noise = field && bt > beat.Seconds - 1.1f;
        if (newsFlash.gameObject.activeSelf != whiteFlash) newsFlash.gameObject.SetActive(whiteFlash);
        if (whiteFlash)
        {
            newsFlash.localPosition = new Vector3(0f, 0f, -0.0049f);
            newsFlash.localScale = new Vector3(w, h, 1f);
        }
        if (noise)
        {
            for (int i = 0; i < 12; i++)
            {
                float g = 0.1f + Random.Range(0, 8) * 0.1f;          // 몇 단계 회색만 써서 재질이 늘어나지 않게
                NDecoOver(i, Random.Range(-h * 0.4f, h * 0.4f), w, Random.Range(h * 0.01f, h * 0.06f), new Color(g, g, g));
            }
        }
        else HideNoise();

        // 글자판과 원고: 한 글자씩 나타난다
        float panelW = ad ? w * 0.92f : w * 0.58f, panelH = ad ? h * 0.6f : h * 0.62f;
        newsPanel.localPosition = new Vector3(ad ? 0f : w * 0.20f, h * 0.0f, -0.0046f);
        newsPanel.localScale = new Vector3(panelW, panelH, 1f);
        if (newsCentered != ad)
        {
            newsCentered = ad;
            newsBodyText.anchor = ad ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            newsBodyText.alignment = ad ? TextAlignment.Center : TextAlignment.Left;
        }
        string full = StoryCatalog.Wrap(beat.text, 11f);
        // 글자 판 안에서 세로 가운데에 놓는다 (전체 줄 수로 계산해서, 글자가 한 글자씩 나와도 위치가 흔들리지 않는다)
        int lineCount = 1;
        foreach (char ch in full) if (ch == '\n') lineCount++;
        newsBodyText.transform.localPosition = ad ? new Vector3(0f, 0f, -0.005f) : new Vector3(-w * 0.075f, lineCount * 0.0125f * ts * 0.5f, -0.005f);
        int chars = Mathf.Clamp(Mathf.FloorToInt(bt * 24f), 0, full.Length);
        if (chars != newsShownChars)
        {
            newsShownChars = chars;
            newsBodyText.text = full.Substring(0, chars);
        }

        // 자막: 같은 대사를 줄바꿈 없이 한 글자씩 (화자 표시는 토막 종류에 따라)
        string raw = beat.text.Replace('|', ' ');
        int sc = Mathf.Clamp(Mathf.FloorToInt(bt * 24f), 0, raw.Length);
        if (idx != newsSubBeat || sc != newsSubChars)
        {
            if (idx != newsSubBeat)
            {
                newsSubBeat = idx;
                newsSubSpeaker = beat.kind == 1 ? "[현장 기자]" : (beat.kind == 2 ? "[광고]" : (beat.kind == 3 ? "[긴급 속보]" : (newsOver ? "[후일담]" : "[김 앵커]")));
                newsSubColor = beat.kind == 1 ? new Color(1f, 0.7f, 0.3f) : (beat.kind == 2 ? new Color(1f, 0.5f, 0.85f) : (beat.kind == 3 ? new Color(1f, 0.4f, 0.35f) : new Color(0.7f, 0.9f, 1f)));
            }
            newsSubChars = sc;
            newsSubText = raw.Substring(0, sc);
        }
    }

    // 앞유리 아래쪽 자막 (IMGUI): 뉴스가 나오는 동안만
    void DrawNewsSubtitle(float W, float H, float u)
    {
        if (!newsActive || !newsShown || newsSubBeat < 0) return;
        var r = new Rect(W * 0.5f - 400f * u, H * 0.585f, 800f * u, 78f * u);
        Panel(r, 0.62f);
        Label(new Rect(r.x + 14f * u, r.y + 4f * u, 400f * u, 22f * u), newsSubSpeaker, 17, newsSubColor, TextAnchor.UpperLeft);
        style.wordWrap = true;
        Label(new Rect(r.x + 14f * u, r.y + 26f * u, r.width - 28f * u, 50f * u), newsSubText, 24, Color.white, TextAnchor.UpperLeft);
        style.wordWrap = false;
    }

    // 지직 잡음 줄 (배경 장식 풀과 별도로 뒤쪽 번호를 쓴다): 화면 위에 덮어 그린다
    readonly List<Transform> newsNoise = new List<Transform>();

    void NDecoOver(int i, float y, float w, float hh, Color c)
    {
        if (i >= newsNoise.Count) newsNoise.Add(CabinPart("NewsNoise", PrimitiveType.Quad, newsRoot, NewsMat(c)));
        var q = newsNoise[i];
        if (!q.gameObject.activeSelf) q.gameObject.SetActive(true);
        float nx = 0f;
        newsShake = Vector2.zero;
        ClipToScreen(ref nx, ref y, ref w, ref hh);
        q.localPosition = new Vector3(nx, y, -0.0047f);
        q.localScale = new Vector3(Mathf.Max(0.0001f, w), Mathf.Max(0.0001f, hh), 1f);
        var m = NewsMat(c);
        var rd = q.GetComponent<Renderer>();
        if (rd.sharedMaterial != m) rd.sharedMaterial = m;
    }

    void HideNoise()
    {
        for (int i = 0; i < newsNoise.Count; i++)
            if (newsNoise[i].gameObject.activeSelf) newsNoise[i].gameObject.SetActive(false);
    }
}
