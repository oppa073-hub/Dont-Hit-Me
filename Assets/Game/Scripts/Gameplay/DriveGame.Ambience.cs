using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 분위기 연결부: 날씨/낮밤(WeatherManager)을 하늘색·조명·배경에 칠하고, 튜토리얼(TutorialManager)을 돌리고, 설정 화면 입력과 저장을 맡는다.
// 화면에 얹는 어두운 막, 비 줄기, 와이퍼, 설정 화면 그리기는 UI/DriveGame.Overlays.cs 에 있다.
public partial class DriveGame
{
    WeatherManager weather;
    TutorialManager tutorial;
    Light sunLight;                                   // Awake 에서 만든 해 (Directional Light)

    // 멀리 보이는 도시 그림 (밤에 어둡게 칠하려고 모아 둔다)
    readonly List<SpriteRenderer> backdropSrs = new List<SpriteRenderer>();
    readonly List<Color> backdropBase = new List<Color>();

    // 설정 화면
    const string SetKey = "DontHitMe.Set.";
    GameFlow settingsReturn = GameFlow.Title;         // 설정을 닫으면 돌아갈 화면
    int settingsSel;
    string[] settingsLines = new string[9];
    bool settingsKeys;                                // true 면 조작 키 설정 쪽
    int settingsKeyWait = -1;                         // 키를 바꾸는 중이면 그 동작 번호 (다음에 누른 키가 들어간다)

    void CreateAmbience()
    {
        weather = new WeatherManager(this);
        tutorial = new TutorialManager();
        LoadSettings();
        RefreshSettings();
    }

    // 판을 시작할 때: 시각을 처음으로, 튜토리얼도 처음 단계부터
    void ResetAmbience()
    {
        weather.Reset();
        tutorial.Reset();
        ApplyWeather();
    }

    void UpdateAmbience(float dt)
    {
        weather.Tick(dt);
        ApplyWeather();
        tutorial.Tick(dt, carX, taxi.State);
    }

    // ---- 밤의 헤드라이트 ----

    Light headLightL, headLightR, cabinLight;

    // 밤의 깊이 (한밤 1, 낮 0). 정전도 밤처럼 어둡다
    float NightK { get { return enc != null ? Mathf.Max(weather.Darkness, enc.Blackout01) : weather.Darkness; } }

    // 차 앞을 비추는 진짜 스포트라이트와, 운전석 안(핸들, 팔)을 은은하게 비추는 작은 조명. 카메라(차)에 붙는다
    void CreateHeadlights()
    {
        headLightL = MakeHeadlight("HeadlightL");
        headLightR = MakeHeadlight("HeadlightR");

        var cgo = new GameObject("CabinLight");
        cgo.transform.SetParent(cam.transform, false);
        cgo.transform.localPosition = new Vector3(0.05f, -0.15f, 0.25f);
        cabinLight = cgo.AddComponent<Light>();
        cabinLight.type = LightType.Point;
        cabinLight.color = new Color(1f, 0.85f, 0.6f);
        cabinLight.shadows = LightShadows.None;
        cabinLight.range = 1.2f;
        cabinLight.enabled = false;
    }

    // 실제 자동차처럼 양쪽에서 나오는 헤드라이트 한 개. 대시보드보다 앞에 둬서 운전석(손, 핸들)을 비추지 않게 하고, 살짝 아래로 향해 도로를 비춘다
    const int SideLayer = 30;                       // 인도와 건물 몸통 상자의 레이어: 헤드라이트가 비추지 않는다 (도로만 보이게)

    Light MakeHeadlight(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);                              // 카메라가 아니라 월드에 둔다: 손님 쪽으로 카메라가 돌아가도 차의 불빛은 앞을 향해 고정
        go.transform.position = new Vector3(carX, camHeight - 0.35f, 1.0f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(1f, 0.95f, 0.75f);
        l.shadows = LightShadows.None;
        l.cullingMask = ~(1 << SideLayer);
        l.enabled = false;
        return l;
    }

    // 헤드라이트가 비추는 정도 (0 어둠 ~ 1 환함). x = 가로 위치, z = 앞쪽 거리. 스포트라이트와 같은 모양(원뿔 + 거리 감쇠)이라서,
    // 빛을 받지 못하는 스프라이트(사람, 차, 건물 그림)의 색을 이 값으로 어둡게 칠하면 진짜 빛과 맞는다.
    float NightLight(float x, float z)
    {
        float nk = NightK;
        if (nk <= 0.003f) return 1f;
        float lit = 0f;
        if (headlightOn)
        {
            // 왼쪽/오른쪽 헤드라이트 두 개: 각각 원뿔 + 거리 감쇠를 구하고 더 밝은 쪽을 쓴다
            float half = Mathf.Max(5f, headlightAngle * 0.5f);
            float beam = 0f, nearest = 99f;
            float conv = Mathf.Max(5f, headlightConverge);
            for (int side = -1; side <= 1; side += 2)
            {
                float dx = x - (carX + side * headlightSpacing * (1f - Mathf.Max(0f, z) / conv));       // 두 불빛은 안쪽으로 기울어 앞쪽 conv 거리에서 한곳으로 모인다
                float dist = Mathf.Sqrt(dx * dx + z * z);
                nearest = Mathf.Min(nearest, dist);
                float ang = Mathf.Atan2(Mathf.Abs(dx), Mathf.Max(0.5f, z)) * Mathf.Rad2Deg;
                float cone = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.35f, half, ang));
                float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(headlightRange * 0.2f, headlightRange, dist));
                beam = Mathf.Max(beam, cone * fall);
            }
            beam *= beam;                                                                  // 가장자리를 더 날카롭게: 빛 안과 밖의 차이를 크게
            lit = Mathf.Max(beam, Mathf.Clamp01(1f - nearest / 5f) * 0.12f);               // 아주 가까운 곳은 차 불빛이 번져서 조금 보인다
            // 도로 밖(인도, 건물)에는 불빛이 닿지 않는다: 인도에서 튀어나오는 사람은 도로에 발을 디뎌야 보인다
            float road = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roadHalfWidth - 0.2f, roadHalfWidth + 0.8f, Mathf.Abs(x)));
            lit *= Mathf.Lerp(road, 1f, headlightSideLight);
        }
        return Mathf.Lerp(1f, Mathf.Max(nightMinLight, lit), nk);
    }

    // 계산된 하늘색, 조명, 도시 그림 색을 실제로 칠한다
    void ApplyWeather()
    {
        float bk = enc != null ? enc.Blackout01 : 0f;                    // 정전: 낮이어도 밤처럼 어두워진다
        float nk = NightK;
        if (cam != null) cam.backgroundColor = Color.Lerp(weather.SkyColor, new Color(0.008f, 0.012f, 0.035f), bk * 0.85f);
        RenderSettings.ambientLight = Color.Lerp(weather.AmbientColor, new Color(0.012f, 0.014f, 0.03f), bk);
        if (sunLight != null)
        {
            sunLight.intensity = weather.SunIntensity * (1f - 0.9f * bk);
            sunLight.color = weather.SunColor;
        }
        for (int i = 0; i < backdropSrs.Count; i++) backdropSrs[i].color = backdropBase[i] * weather.BackdropTint * (1f - 0.8f * bk);

        if (headLightL != null)
        {
            bool on = headlightOn && nk > 0.02f;
            if (headLightL.enabled != on) { headLightL.enabled = on; headLightR.enabled = on; }
            if (on)
            {
                for (int side = 0; side < 2; side++)
                {
                    var l = side == 0 ? headLightL : headLightR;
                    l.transform.rotation = Quaternion.Euler(3f, (side == 0 ? 1f : -1f) * Mathf.Atan2(headlightSpacing, Mathf.Max(5f, headlightConverge)) * Mathf.Rad2Deg, 0f);
                    l.transform.position = new Vector3(carX + (side == 0 ? -headlightSpacing : headlightSpacing), camHeight - 0.35f, 1.0f);
                    l.range = headlightRange;
                    l.spotAngle = headlightAngle;
                    l.innerSpotAngle = headlightAngle * 0.5f;
                    l.intensity = headlightIntensity * 0.6f * nk;                // 두 개가 겹치는 가운데는 더 밝아진다
                }
            }
        }
        if (cabinLight != null)
        {
            bool on = cabinGlow > 0.001f && nk > 0.02f;
            if (cabinLight.enabled != on) cabinLight.enabled = on;
            if (on) cabinLight.intensity = cabinGlow * nk;
        }
    }

    // ---- 설정 ----
    // 로비에서 비서에게 말을 걸면 열린다. 언어, 소리(전체/음악/효과음/엔진), 화면, 튜토리얼, 조작 키.

    void LoadSettings()
    {
        Loc.Load();
        Keybinds.Load();
        masterVolume = PlayerPrefs.GetFloat(SetKey + "Master", masterVolume);
        bgmVolume = PlayerPrefs.GetFloat(SetKey + "Music", bgmVolume);
        sfxVolume = PlayerPrefs.GetFloat(SetKey + "Sfx", sfxVolume);
        engineVolume = PlayerPrefs.GetFloat(SetKey + "Engine", engineVolume);
        shakeEnabled = PlayerPrefs.GetInt(SetKey + "Shake", shakeEnabled ? 1 : 0) == 1;
        weatherOn = PlayerPrefs.GetInt(SetKey + "Weather", weatherOn ? 1 : 0) == 1;
    }

    void SaveSettings()
    {
        PlayerPrefs.SetFloat(SetKey + "Master", masterVolume);
        PlayerPrefs.SetFloat(SetKey + "Music", bgmVolume);
        PlayerPrefs.SetFloat(SetKey + "Sfx", sfxVolume);
        PlayerPrefs.SetFloat(SetKey + "Engine", engineVolume);
        PlayerPrefs.SetInt(SetKey + "Shake", shakeEnabled ? 1 : 0);
        PlayerPrefs.SetInt(SetKey + "Weather", weatherOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    static string VolumeText(string name, float v)
    {
        int k = Mathf.Clamp(Mathf.RoundToInt(v * 10f), 0, 10);
        return name + "   [" + new string('|', k) + new string('.', 10 - k) + "] " + (k * 10) + "%";
    }

    static string OnOff(string name, bool on) { return name + "   " + (on ? Loc.T("켜짐", "On") : Loc.T("꺼짐", "Off")); }

    void RefreshSettings()
    {
        if (settingsKeys)
        {
            if (settingsLines.Length != Keybinds.Count + 2) settingsLines = new string[Keybinds.Count + 2];
            for (int i = 0; i < Keybinds.Count; i++)
                settingsLines[i] = Keybinds.ActionName(i) + "   [ " + (settingsKeyWait == i ? Loc.T("키를 누르세요…", "press a key…") : Keybinds.KeyName(Keybinds.Map[i])) + " ]";
            settingsLines[Keybinds.Count] = Loc.T("기본값으로 되돌리기", "Reset to defaults");
            settingsLines[Keybinds.Count + 1] = Loc.T("뒤로", "Back");
            return;
        }
        if (settingsLines.Length != 9) settingsLines = new string[9];
        settingsLines[0] = Loc.T("언어   ", "Language   ") + (Loc.Ko ? "한국어" : "English");
        settingsLines[1] = VolumeText(Loc.T("전체 볼륨", "Master volume"), masterVolume);
        settingsLines[2] = VolumeText(Loc.T("노래 (음악)", "Music"), bgmVolume);
        settingsLines[3] = VolumeText(Loc.T("효과음", "Sound effects"), sfxVolume);
        settingsLines[4] = VolumeText(Loc.T("엔진 소리", "Engine sound"), engineVolume);
        settingsLines[5] = OnOff(Loc.T("화면 흔들림", "Screen shake"), shakeEnabled);
        settingsLines[6] = OnOff(Loc.T("낮/밤, 비 효과", "Day/night & rain"), weatherOn);
        settingsLines[7] = OnOff(Loc.T("튜토리얼 안내", "Tutorial tips"), tutorial.Enabled);
        settingsLines[8] = Loc.T("조작 키 설정  >", "Key bindings  >");
    }

    void OpenSettings(GameFlow back)
    {
        settingsReturn = back;
        settingsSel = 0;
        settingsKeys = false;
        settingsKeyWait = -1;
        flow = GameFlow.Settings;
        RefreshSettings();
    }

    static float StepVolume(float v, int dir) { return Mathf.Clamp01(Mathf.Round((v + dir * 0.1f) * 10f) / 10f); }

    // 언어를 바꿨다: 글자를 미리 만들어 두는 곳(상점, 로비, 목표, HUD 등)을 새로 만든다
    void OnLanguageChanged()
    {
        RefreshShop();
        RefreshModeText();
        goals.Refresh();
        hudRem = hudFareNow = hudTimeLeft = hudTip = -99999;          // 탑승 정보를 다시 만든다
        hudPaxName = hudDestName = null;
        hintF = int.MinValue;
        hudInterDist = int.MinValue;
        navHead = -1;
        navRem = -2;
        hudKmh = -1; hudLevel = -1; hudNear = -1; hudHits = -1; hudTotal = -1; hudFareTotal = -1;
        hudLives = -999;
        lobbyModeCached = null;
    }

    void AdjustSetting(int i, int dir)
    {
        if (settingsKeys)
        {
            if (i < Keybinds.Count) settingsKeyWait = i;                           // 다음에 누르는 키를 받는다
            else if (i == Keybinds.Count) Keybinds.ResetAll();
            else { settingsKeys = false; settingsSel = 8; }
            RefreshSettings();
            return;
        }
        switch (i)
        {
            case 0:
                Loc.Set(Loc.Ko ? 0 : 1);
                OnLanguageChanged();
                break;
            case 1: masterVolume = StepVolume(masterVolume, dir); break;
            case 2: bgmVolume = StepVolume(bgmVolume, dir); break;
            case 3: sfxVolume = StepVolume(sfxVolume, dir); break;
            case 4: engineVolume = StepVolume(engineVolume, dir); break;
            case 5: shakeEnabled = !shakeEnabled; break;
            case 6:
                weatherOn = !weatherOn;
                weather.Tick(0f);
                ApplyWeather();
                break;
            case 7: tutorial.SetEnabled(!tutorial.Enabled); break;
            default:
                settingsKeys = true;
                settingsSel = 0;
                break;
        }
        SaveSettings();
        RefreshSettings();
    }

    void UpdateSettings(Keyboard kb)
    {
        // 키를 바꾸는 중: 다음에 눌린 키를 받는다 (Esc 는 취소)
        if (settingsKeys && settingsKeyWait >= 0)
        {
            if (kb.escapeKey.wasPressedThisFrame) { settingsKeyWait = -1; RefreshSettings(); return; }
            foreach (var kc in kb.allKeys)
            {
                if (!kc.wasPressedThisFrame || kc.keyCode == Key.None || kc.keyCode == Key.IMESelected) continue;
                Keybinds.Rebind(settingsKeyWait, kc.keyCode);
                settingsKeyWait = -1;
                RefreshSettings();
                return;
            }
            return;
        }
        int n = settingsLines.Length;
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) settingsSel = (settingsSel + n - 1) % n;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) settingsSel = (settingsSel + 1) % n;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) AdjustSetting(settingsSel, -1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            AdjustSetting(settingsSel, 1);
        if (kb.escapeKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
        {
            if (settingsKeys) { settingsKeys = false; settingsSel = 8; RefreshSettings(); }
            else flow = settingsReturn;
        }
    }
}
