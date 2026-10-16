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
    string[] settingsLines = new string[6];

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

    // 계산된 하늘색, 조명, 도시 그림 색을 실제로 칠한다
    void ApplyWeather()
    {
        if (cam != null) cam.backgroundColor = weather.SkyColor;
        RenderSettings.ambientLight = weather.AmbientColor;
        if (sunLight != null)
        {
            sunLight.intensity = weather.SunIntensity;
            sunLight.color = weather.SunColor;
        }
        for (int i = 0; i < backdropSrs.Count; i++) backdropSrs[i].color = backdropBase[i] * weather.BackdropTint;
    }

    // ---- 설정 ----

    void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(SetKey + "Master", masterVolume);
        bgmVolume = PlayerPrefs.GetFloat(SetKey + "Music", bgmVolume);
        engineVolume = PlayerPrefs.GetFloat(SetKey + "Engine", engineVolume);
        shakeEnabled = PlayerPrefs.GetInt(SetKey + "Shake", shakeEnabled ? 1 : 0) == 1;
        weatherOn = PlayerPrefs.GetInt(SetKey + "Weather", weatherOn ? 1 : 0) == 1;
    }

    void SaveSettings()
    {
        PlayerPrefs.SetFloat(SetKey + "Master", masterVolume);
        PlayerPrefs.SetFloat(SetKey + "Music", bgmVolume);
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

    static string OnOff(string name, bool on) { return name + "   " + (on ? "켜짐" : "꺼짐"); }

    void RefreshSettings()
    {
        settingsLines[0] = VolumeText("마스터 볼륨", masterVolume);
        settingsLines[1] = VolumeText("음악 볼륨", bgmVolume);
        settingsLines[2] = VolumeText("엔진 소리", engineVolume);
        settingsLines[3] = OnOff("화면 흔들림", shakeEnabled);
        settingsLines[4] = OnOff("낮/밤, 비 효과", weatherOn);
        settingsLines[5] = OnOff("튜토리얼 안내", tutorial.Enabled);
    }

    void OpenSettings(GameFlow back)
    {
        settingsReturn = back;
        settingsSel = 0;
        flow = GameFlow.Settings;
        RefreshSettings();
    }

    static float StepVolume(float v, int dir) { return Mathf.Clamp01(Mathf.Round((v + dir * 0.1f) * 10f) / 10f); }

    void AdjustSetting(int i, int dir)
    {
        switch (i)
        {
            case 0: masterVolume = StepVolume(masterVolume, dir); break;
            case 1: bgmVolume = StepVolume(bgmVolume, dir); break;
            case 2: engineVolume = StepVolume(engineVolume, dir); break;
            case 3: shakeEnabled = !shakeEnabled; break;
            case 4:
                weatherOn = !weatherOn;
                weather.Tick(0f);
                ApplyWeather();
                break;
            default: tutorial.SetEnabled(!tutorial.Enabled); break;
        }
        SaveSettings();
        RefreshSettings();
    }

    void UpdateSettings(Keyboard kb)
    {
        int n = settingsLines.Length;
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) settingsSel = (settingsSel + n - 1) % n;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) settingsSel = (settingsSel + 1) % n;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) AdjustSetting(settingsSel, -1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            AdjustSetting(settingsSel, 1);
        if (kb.escapeKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            flow = settingsReturn;
    }
}
