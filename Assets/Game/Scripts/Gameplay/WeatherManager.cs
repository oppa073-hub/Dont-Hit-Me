using UnityEngine;

// 날씨 매니저: 하루 시계(아침-낮-노을-밤)와 비를 맡는다. 화면에 어떻게 칠할지(하늘색, 조명, 어두운 막, 비 줄기)는 모르고,
// 계산한 값을 읽기 전용 속성으로 알려 주면 DriveGame 연결부(DriveGame.Ambience.cs)가 적용한다.
// 설정 값(하루 길이, 시작 시각, 비 확률, 켜기/끄기)은 DriveGame 의 public 필드를 읽는다.
public class WeatherManager
{
    readonly DriveGame cfg;
    float nextChange, rainTarget;

    /// <summary>하루 중 시각 (0 = 자정, 0.25 = 아침 6시, 0.5 = 낮 12시, 0.75 = 저녁 6시).</summary>
    public float DayT { get; private set; }
    /// <summary>비의 세기 (0 맑음 ~ 1 폭우).</summary>
    public float Rain { get; private set; }
    /// <summary>밤의 깊이 (0 낮 ~ 1 한밤).</summary>
    public float Darkness { get; private set; }
    /// <summary>해 뜰 때/질 때의 노을 정도 (0 ~ 1).</summary>
    public float Warm { get; private set; }

    public Color SkyColor { get; private set; }
    public Color AmbientColor { get; private set; }
    public Color SunColor { get; private set; }
    public float SunIntensity { get; private set; }
    public Color NightOverlay { get; private set; }     // 화면 전체에 깔 어두운 파란 막 (알파 = 세기)
    public Color RainOverlay { get; private set; }
    public Color WarmOverlay { get; private set; }
    public Color BackdropTint { get; private set; }     // 멀리 보이는 도시 그림에 곱할 색

    public WeatherManager(DriveGame cfg)
    {
        this.cfg = cfg;
        Reset();
    }

    public void Reset()
    {
        DayT = Mathf.Repeat(cfg.startTimeOfDay, 1f);
        Rain = rainTarget = 0f;
        nextChange = Random.Range(50f, 90f);
        Recalc();
    }

    public void Tick(float dt)
    {
        if (cfg.weatherOn)
        {
            DayT = Mathf.Repeat(DayT + dt / Mathf.Max(30f, cfg.dayLength), 1f);
            nextChange -= dt;
            if (nextChange <= 0f)
            {
                nextChange = Random.Range(40f, 80f);
                rainTarget = (rainTarget <= 0f && Random.value < cfg.rainChance) ? 1f : 0f;
            }
            Rain = Mathf.MoveTowards(Rain, rainTarget, dt / 6f);          // 6초에 걸쳐 서서히 내리고 그친다
        }
        else
        {
            Rain = 0f;                                                    // 끄면 비도 바로 멈춘다
            rainTarget = 0f;
        }
        Recalc();
    }

    /// <summary>시험용: 비를 바로 내리거나 그친다.</summary>
    public void DebugRain(bool on)
    {
        rainTarget = on ? 1f : 0f;
        nextChange = 120f;
        if (!on) Rain = Mathf.Min(Rain, 0.5f);
    }

    /// <summary>시험용: 시각을 앞으로 돌린다.</summary>
    public void DebugAddHours(float hours)
    {
        DayT = Mathf.Repeat(DayT + hours / 24f, 1f);
        Recalc();
    }

    /// <summary>화면에 보일 시계 글자 만들기용: 하루를 10분 단위로 센 번호 (바뀔 때만 글자를 새로 만들면 된다).</summary>
    public int ClockStep { get { return Mathf.FloorToInt(DayT * 144f) % 144; } }

    public string ClockText()
    {
        int m = ClockStep * 10;
        return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
    }

    void Recalc()
    {
        bool on = cfg.weatherOn;
        float sun = -Mathf.Cos(DayT * Mathf.PI * 2f);                              // +1 한낮, -1 한밤
        float dark = on ? Mathf.Clamp01(Mathf.InverseLerp(0.25f, -0.1f, sun)) : 0f;
        dark = dark * dark * (3f - 2f * dark);
        Darkness = dark;
        Warm = on ? Mathf.Clamp01(1f - Mathf.Abs(sun) / 0.35f) * (1f - dark * 0.5f) : 0f;
        float rain = on ? Rain : 0f;

        var daySky = new Color(128f / 255f, 194f / 255f, 206f / 255f);
        var sky = Color.Lerp(daySky, new Color(0.008f, 0.012f, 0.035f), dark);
        sky = Color.Lerp(sky, new Color(0.9f, 0.55f, 0.35f), Warm * 0.5f);
        SkyColor = Color.Lerp(sky, Dim(new Color(0.45f, 0.5f, 0.55f), 1f - dark * 0.8f), rain * 0.5f);
        SkyColor = Dim(SkyColor, 1f);

        var dayAmbient = new Color(0.68f, 0.68f, 0.72f);
        AmbientColor = Color.Lerp(dayAmbient, new Color(0.012f, 0.014f, 0.03f), dark) * (1f - 0.2f * rain);
        SunColor = Color.Lerp(Color.white, new Color(1f, 0.7f, 0.45f), Warm * 0.6f);
        SunIntensity = Mathf.Lerp(0.8f, 0.006f, dark) * (1f - 0.3f * rain);

        NightOverlay = new Color(0.01f, 0.015f, 0.08f, 0.28f * dark);              // 어둡게 만드는 건 대부분 실제 조명과 색 칠하기가 하고, 이 막은 전체 분위기만 잡는다
        RainOverlay = new Color(0.10f, 0.12f, 0.15f, 0.18f * rain);
        WarmOverlay = new Color(1f, 0.6f, 0.25f, 0.14f * Warm);

        var tint = Color.Lerp(Color.white, new Color(0.025f, 0.03f, 0.07f), dark * 0.97f);
        tint = Color.Lerp(tint, new Color(1f, 0.8f, 0.65f), Warm * 0.5f);
        BackdropTint = Color.Lerp(tint, Dim(tint, 0.8f), rain);
    }

    // 색을 어둡게 하되 알파는 1 로 둔다 (Color * float 는 알파까지 줄여 버린다)
    static Color Dim(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }
}
