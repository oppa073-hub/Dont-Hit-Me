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
    readonly AudioSource bgm;
    readonly AudioSource rainSrc;
    readonly AudioClip tick, tock, bonk, whoosh, ding, chime, alarm, roar, laser;
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

        // 배경음악: 인스펙터 bgmClip 이 있으면 그걸, 없으면 코드로 만든 임시 곡을 반복 재생한다
        bgm = host.AddComponent<AudioSource>();
        bgm.clip = cfg.bgmClip != null ? cfg.bgmClip : MakeBgm();
        bgm.loop = true;
        bgm.playOnAwake = false;
        bgm.spatialBlend = 0f;
        rainSrc = host.AddComponent<AudioSource>();
        rainSrc.clip = MakeRain();
        rainSrc.loop = true;
        rainSrc.playOnAwake = false;
        rainSrc.spatialBlend = 0f;

        tick = Click(1800f, 0.012f, 0.9f);
        tock = Click(1100f, 0.014f, 0.8f);
        bonk = MakeBonk();
        whoosh = MakeWhoosh();
        ding = MakeBell(new[] { 880f, 1320f }, 0.7f);
        chime = MakeBell(new[] { 660f }, 0.25f);
        alarm = MakeAlarm();
        roar = MakeRoar();
        laser = MakeLaser();
    }

    // ---- 사건 효과음 ----
    public void PlayBonk() { Play(bonk, 1f); }
    public void PlayNearMiss() { Play(whoosh, 0.7f); }
    public void PlayArrive() { Play(ding, 0.8f); }
    public void PlayChime() { Play(chime, 0.6f); }
    public void PlayAlarm() { Play(alarm, 0.7f); }       // 긴급 속보 사이렌
    public void PlayRoar() { Play(roar, 1f); }           // 괴수 울음
    public void PlayLaser() { Play(laser, 0.8f); }       // 히어로의 눈 레이저

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

    /// <summary>
    /// 매 프레임: 배경음악과 빗소리. rain = 비의 세기(0~1), duck = 음악을 줄이는 배율(일시정지 등에서 작게).
    /// 효과음/엔진과 달리 타이틀, 일시정지, 상점에서도 계속 흐른다.
    /// </summary>
    public void TickMusic(float rain, float duck)
    {
        if (!cfg.soundOn)
        {
            if (bgm.isPlaying) bgm.Pause();
            if (rainSrc.isPlaying) rainSrc.Pause();
            return;
        }
        if (!bgm.isPlaying) bgm.Play();
        bgm.volume = cfg.masterVolume * cfg.bgmVolume * duck;
        if (rain > 0.02f)
        {
            if (!rainSrc.isPlaying) rainSrc.Play();
            rainSrc.volume = cfg.masterVolume * 0.4f * rain;
        }
        else if (rainSrc.isPlaying) rainSrc.Pause();
    }

    // ---- 소리 만들기 (모두 코드로 합성) ----

    // 사이렌: 두 음을 번갈아
    static AudioClip MakeAlarm()
    {
        var d = new float[Mathf.CeilToInt(Rate * 1.4f)];
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float tt = (float)i / Rate;
            float f = ((int)(tt / 0.35f) & 1) == 0 ? 760f : 980f;
            phase += f / Rate * Mathf.PI * 2f;
            float sq = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 3f);
            float env = Mathf.Min(1f, tt * 40f) * Mathf.Min(1f, (1.4f - tt) * 12f);
            d[i] = sq * env * 0.35f;
        }
        return Make("alarm", d);
    }

    // 괴수 울음: 낮은 톱니파가 떨어지며 떨리고 거친 숨소리가 섞인다
    static AudioClip MakeRoar()
    {
        var d = new float[Mathf.CeilToInt(Rate * 1.4f)];
        var rnd = new System.Random(31);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float f = Mathf.Lerp(120f, 42f, t);
            phase += f / Rate;
            float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
            float noise = (float)rnd.NextDouble() * 2f - 1f;
            float v = (saw * 0.8f + noise * 0.4f) * (1f + 0.4f * Mathf.Sin(t * 60f));
            lp += (v - lp) * 0.18f;
            float env = Mathf.Min(1f, t * 10f) * Mathf.Pow(1f - t, 1.2f);
            d[i] = lp * env * 0.9f;
        }
        return Make("roar", d);
    }

    // 레이저: 높은 음이 쭉 떨어지는 찌잉
    static AudioClip MakeLaser()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.55f)];
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float f = Mathf.Lerp(2200f, 260f, Mathf.Sqrt(t));
            phase += f / Rate * Mathf.PI * 2f;
            float env = Mathf.Min(1f, t * 30f) * Mathf.Pow(1f - t, 1.5f);
            d[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * env * 0.45f;
        }
        return Make("laser", d);
    }

    // 임시 배경음악 16초: Am - F - C - G 코드를 부드러운 패드와 짧은 아르페지오로. 끝과 처음이 이어지도록 소리가 넘치면 앞으로 감아 넣는다.
    // 진짜 곡을 넣으려면 DriveGame 인스펙터의 Bgm Clip 에 AudioClip 을 끌어다 놓으면 이 곡 대신 재생된다.
    static AudioClip MakeBgm()
    {
        const float bar = 4f;
        int n = Mathf.RoundToInt(Rate * bar * 4f);
        var d = new float[n];
        float[][] chords =
        {
            new[] { 220.00f, 261.63f, 329.63f },
            new[] { 174.61f, 220.00f, 261.63f },
            new[] { 261.63f, 329.63f, 392.00f },
            new[] { 196.00f, 246.94f, 293.66f },
        };
        float[] roots = { 110.00f, 87.31f, 130.81f, 98.00f };
        for (int b = 0; b < 4; b++)
        {
            int start = Mathf.RoundToInt(Rate * bar * b);
            int len = Mathf.RoundToInt(Rate * bar);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float env = Mathf.Sin(t * Mathf.PI);
                env *= env;                                                   // 마디 양끝에서 0 이 되는 부드러운 패드
                float v = 0f;
                foreach (float f in chords[b]) v += Mathf.Sin((float)i / Rate * f * Mathf.PI * 2f);
                v = v / 3f * env * 0.16f;
                v += Mathf.Sin((float)i / Rate * roots[b] * Mathf.PI * 2f) * env * 0.12f;
                d[(start + i) % n] += v;
            }
            // 0.5초마다 코드 음 하나씩 짧게 퉁기는 아르페지오
            int[] order = { 0, 1, 2, 1, 2, 1, 0, 1 };
            for (int s = 0; s < 8; s++)
            {
                float f = chords[b][order[s]] * 2f;
                int noteStart = start + Mathf.RoundToInt(Rate * 0.5f * s);
                int noteLen = Mathf.RoundToInt(Rate * 0.6f);
                for (int i = 0; i < noteLen; i++)
                {
                    float tt = (float)i / Rate;
                    float v = Mathf.Sin(tt * f * Mathf.PI * 2f) * Mathf.Exp(-5f * tt) * Mathf.Min(1f, tt * 80f) * 0.10f;
                    d[(noteStart + i) % n] += v;
                }
            }
        }
        return Make("bgm", d);
    }

    // 빗소리: 부드러운 잡음. 끝과 처음을 섞어 이음매가 안 들리게 한다.
    static AudioClip MakeRain()
    {
        const int fade = 2000;
        int len = Rate * 2;
        var rnd = new System.Random(23);
        var raw = new float[len + fade];
        float lp = 0f, hp = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float x = (float)rnd.NextDouble() * 2f - 1f;
            lp += (x - lp) * 0.45f;
            hp += (lp - hp) * 0.02f;
            raw[i] = (lp - hp) * 0.8f;
        }
        var d = new float[len];
        for (int i = 0; i < len; i++)
        {
            if (i < fade)
            {
                float k = (float)i / fade;
                d[i] = raw[i] * k + raw[len + i] * (1f - k);
            }
            else d[i] = raw[i];
        }
        return Make("rain", d);
    }

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
