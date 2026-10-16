using UnityEngine;

// 사운드 매니저: 외부 에셋 없이 코드로 만든 효과음(엔진, 깜빡이 틱톡, 충돌, 니어미스, 도착 종소리)을 재생한다.
// 게임 상태는 모른다. DriveGame 이 Tick 인자로 속도/깜빡이를 주고, 사건(충돌, 도착 등)이 생기면 Play 함수를 부른다.
// 설정 값(켜기/끄기, 볼륨)은 DriveGame 의 public 필드를 읽는다. 오디오 리스너가 없으면 카메라에 붙인다.
public class AudioManager
{
    const int Rate = 22050;

    readonly DriveGame cfg;
    readonly AudioSource engine;
    readonly AudioSource sfx;
    readonly AudioClip tick, tock, bonk, whoosh, ding, chime;
    bool lastBlinkOn;

    public AudioManager(DriveGame cfg, GameObject host, Camera cam)
    {
        this.cfg = cfg;
        if (cam != null && cam.GetComponent<AudioListener>() == null && Object.FindFirstObjectByType<AudioListener>() == null)
            cam.gameObject.AddComponent<AudioListener>();

        engine = host.AddComponent<AudioSource>();
        engine.clip = MakeEngine();
        engine.loop = true;
        engine.playOnAwake = false;
        engine.spatialBlend = 0f;
        sfx = host.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        tick = Click(1800f, 0.012f, 0.9f);
        tock = Click(1100f, 0.014f, 0.8f);
        bonk = MakeBonk();
        whoosh = MakeWhoosh();
        ding = MakeBell(new[] { 880f, 1320f }, 0.7f);
        chime = MakeBell(new[] { 660f }, 0.25f);
    }

    // ---- 사건 효과음 ----
    public void PlayBonk() { Play(bonk, 1f); }
    public void PlayNearMiss() { Play(whoosh, 0.7f); }
    public void PlayArrive() { Play(ding, 0.8f); }
    public void PlayChime() { Play(chime, 0.6f); }

    void Play(AudioClip clip, float volume)
    {
        if (!cfg.soundOn || clip == null) return;
        sfx.PlayOneShot(clip, volume * cfg.masterVolume);
    }

    /// <summary>
    /// 매 프레임: 엔진 소리(속도에 따라 높낮이/크기)와 깜빡이 틱톡. silent = 게임오버처럼 엔진을 꺼야 할 때.
    /// 깜빡이 틱톡은 화면의 깜빡이 글자(0.6초 주기, 앞 0.35초 켜짐)와 같은 박자다.
    /// </summary>
    public void Tick(float speed, float maxSpeed, int blinker, bool silent)
    {
        if (!cfg.soundOn || silent)
        {
            if (engine.isPlaying) engine.Pause();
            return;
        }
        if (!engine.isPlaying) engine.Play();
        float k = Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed));
        engine.pitch = Mathf.Lerp(0.7f, 1.9f, k);
        engine.volume = cfg.masterVolume * cfg.engineVolume * Mathf.Lerp(0.5f, 1f, k);

        if (blinker == 0)
        {
            lastBlinkOn = false;
            return;
        }
        bool on = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.35f;
        if (on != lastBlinkOn)
        {
            lastBlinkOn = on;
            sfx.PlayOneShot(on ? tick : tock, 0.5f * cfg.masterVolume);
        }
    }

    // ---- 소리 만들기 (모두 코드로 합성) ----

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 엔진: 55Hz 기본음 + 배음을 정확히 55번 반복(1초)해서 이음매 없이 이어지게 한다.
    static AudioClip MakeEngine()
    {
        var d = new float[Rate];
        for (int i = 0; i < d.Length; i++)
        {
            float ph = (float)i / Rate * 55f * Mathf.PI * 2f;
            float v = Mathf.Sin(ph) * 0.55f + Mathf.Sin(ph * 2f) * 0.3f + Mathf.Sin(ph * 3f) * 0.18f + Mathf.Sin(ph * 5f) * 0.08f;
            v += Mathf.Sin(ph * 0.5f + Mathf.Sin(ph * 4f) * 0.6f) * 0.1f;     // 약간의 울렁임
            d[i] = v * 0.5f;
        }
        return Make("engine", d);
    }

    // 짧은 클릭음 (깜빡이 릴레이)
    static AudioClip Click(float freq, float seconds, float gain)
    {
        var d = new float[Mathf.CeilToInt(Rate * seconds)];
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            d[i] = Mathf.Sin((float)i / Rate * freq * Mathf.PI * 2f) * Mathf.Pow(1f - t, 3f) * gain;
        }
        return Make("click", d);
    }

    // 충돌: 낮은 음이 뚝 떨어지는 쿵 + 짧은 잡음
    static AudioClip MakeBonk()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.35f)];
        var rnd = new System.Random(7);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float f = Mathf.Lerp(140f, 38f, Mathf.Sqrt(t));
            phase += f / Rate * Mathf.PI * 2f;
            float noise = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Pow(1f - t, 8f) * 0.5f;
            d[i] = (Mathf.Sin(phase) * Mathf.Pow(1f - t, 2f) + noise) * 0.9f;
        }
        return Make("bonk", d);
    }

    // 니어미스: 잡음이 커졌다 사라지는 쉬익
    static AudioClip MakeWhoosh()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.4f)];
        var rnd = new System.Random(11);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float env = Mathf.Sin(t * Mathf.PI);
            float n = (float)rnd.NextDouble() * 2f - 1f;
            lp += (n - lp) * Mathf.Lerp(0.05f, 0.35f, t);                    // 뒤로 갈수록 밝아지는 잡음
            d[i] = lp * env * 0.9f;
        }
        return Make("whoosh", d);
    }

    // 종소리: 여러 음을 겹쳐 서서히 줄어들게
    static AudioClip MakeBell(float[] freqs, float seconds)
    {
        var d = new float[Mathf.CeilToInt(Rate * seconds)];
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float v = 0f;
            foreach (float f in freqs) v += Mathf.Sin((float)i / Rate * f * Mathf.PI * 2f);
            d[i] = v / freqs.Length * Mathf.Pow(1f - t, 2.5f) * 0.6f;
        }
        return Make("bell", d);
    }
}
