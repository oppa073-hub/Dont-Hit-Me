using System.Collections.Generic;
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
    AudioClip engineSynth;
    AudioSource windSrc, rumbleSrc, droneSrc;       // 엔딩용 반복 소리: 바람, 저음 진동, 우주의 낮은 울림
    readonly AudioClip tick, tock, bonk, whoosh, ding, chime, alarm, roar, laser, gunShot, deploy, oink, powerDown, rocket, splat;
    bool lastBlinkOn;

    // 진짜 음악(Resources/Audio, 없으면 예전 임시 곡): 0 로비/타이틀, 1 낮, 2 노을, 3 밤. 필요할 때 한 번만 읽는다
    public const int TrackLobby = 0, TrackDay = 1, TrackDusk = 2, TrackNight = 3, TrackRage = 4;
    static readonly string[] TrackFiles = { "bgm_lobby", "bgm_day", "bgm_dusk", "bgm_night", "bgm_rage" };
    readonly AudioClip[] tracks = new AudioClip[5];
    readonly bool[] trackTried = new bool[5];
    readonly AudioSource stingerSrc;
    int curTrack = -1;
    float musicFade = 1f, stingerLeft;

    public AudioManager(DriveGame cfg, GameObject host, Camera cam)
    {
        this.cfg = cfg;
        if (cam != null && cam.GetComponent<AudioListener>() == null && Object.FindFirstObjectByType<AudioListener>() == null)
            cam.gameObject.AddComponent<AudioListener>();

        engine = host.AddComponent<AudioSource>();
        engineSynth = MakeEngine();
        engine.clip = engineSynth;
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
        windSrc = LoopSource(host);
        rumbleSrc = LoopSource(host);
        droneSrc = LoopSource(host);
        stingerSrc = host.AddComponent<AudioSource>();
        stingerSrc.playOnAwake = false;
        stingerSrc.spatialBlend = 0f;
        rainSrc = host.AddComponent<AudioSource>();
        var rainClip = Resources.Load<AudioClip>("Audio/rain");          // 진짜 빗소리가 있으면 그걸, 없으면 코드로 만든 소리
        rainSrc.clip = rainClip != null ? rainClip : MakeRain();
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
        gunShot = MakeGunShot();
        oink = MakeOink();
        powerDown = MakePowerDown();
        deploy = Click(240f, 0.2f, 1f);
        rocket = MakeRocket();
        splat = MakeSplat();
    }

    // ---- 사건 효과음 ----
    public void PlayBonk() { Play("bonk", bonk, 1f); }
    public void PlayNearMiss() { Play("whoosh", whoosh, 0.7f); }
    public void PlayArrive() { Play("ding", ding, 0.8f); }
    public void PlayChime() { Play("chime", chime, 0.6f); }
    public void PlayAlarm() { Play("alarm", alarm, 0.7f); }       // 긴급 속보 사이렌
    public void PlaySiren(float volume) { Play(alarm, volume); }       // 구급차 사이렌 (가까울수록 크게)
    public void PlayRoar() { Play("roar", roar, 1f); }           // 괴수 울음
    public void PlayLaser() { Play("laser", laser, 0.8f); }       // 히어로의 눈 레이저
    public void PlayOink() { Play("oink", oink, 0.8f); }         // 돼지
    public void PlayPowerDown() { Play("power", powerDown, 0.8f); }     // 정전
    public void PlayGun() { Play("gun", gunShot, 0.35f); }      // 미니건 한 발
    public void PlayDeploy() { Play("deploy", deploy, 1f); }    // 미니건이 튀어나오는 철컹
    public void PlaySplat() { Play("splat", splat, 0.9f); }       // 좀비를 칠 때 철퍽
    public void PlayRocket() { Play("rocket", rocket, 1f); }       // 엔딩: 날개 택시가 로켓처럼 치솟는 소리

    static AudioSource LoopSource(GameObject host)
    {
        var s = host.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    // ---- 엔딩 소리: 반복 소리 세 개의 크기(0~1)를 매 프레임 정해 준다 ----
    public void EndingAmbience(float wind, float rumble, float drone)
    {
        SetLoop(windSrc, "wind", wind);
        SetLoop(rumbleSrc, "rumble", rumble);
        SetLoop(droneSrc, "drone", drone);
    }

    public void StopEndingAmbience() { EndingAmbience(0f, 0f, 0f); }

    void SetLoop(AudioSource src, string key, float v)
    {
        if (!cfg.soundOn || v < 0.01f)
        {
            if (src.isPlaying) src.Stop();
            return;
        }
        if (src.clip == null) src.clip = Pick(key, null);
        if (src.clip == null) return;
        if (!src.isPlaying) src.Play();
        src.volume = v * cfg.masterVolume * cfg.sfxVolume;
    }

    public void PlayScream(bool male) { Play(male ? "screamM" : "screamF", null, 0.9f); }
    public void PlayWingsOpen() { Play("whir", null, 0.9f); Play("build", null, 0.8f); }

    // 엔진: 기어 0(정차)~6 루프를 속도 구간(초속 5씩)마다 갈아 끼우고, 한 기어 안에서는 음이 4반음쯤 오른다.
    // 기어가 오르내릴 때 변속음. 파일이 없으면 false.
    int gear = -1;
    float lastShift;

    bool TickGears(float speed)
    {
        if (Pick("eng0", null) == null || Pick("eng6", null) == null) return false;
        int want = speed < 0.3f ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt(speed / 5f), 1, 6);
        if (gear == 0 && speed < 0.8f) want = 0;                         // 출발하기 전까지 공회전 유지
        else if (gear > 0 && want != gear)
        {
            // 경계에서 덜덜 바뀌지 않게 0.4 만큼 여유
            float lo = (gear - 1) * 5f, hi = gear * 5f;
            if (speed > lo - 0.4f && speed < hi + 0.4f && speed >= 0.3f) want = gear;
        }
        if (want != gear)
        {
            if (gear >= 0 && Time.unscaledTime - lastShift > 0.5f && want > 0 && gear > 0) Play("shift", null, 0.35f);
            lastShift = Time.unscaledTime;
            gear = want;
            engine.clip = Pick("eng" + gear, null);
            engine.time = 0f;
            engine.Play();
        }
        float frac = gear == 0 ? 0f : Mathf.Clamp01((speed - (gear - 1) * 5f) / 5f);
        engine.pitch = Mathf.Lerp(0.95f, 1.2f, frac);
        return true;
    }

    public void PlayCrash() { Play("crash", bonk, 0.9f); }       // 차끼리 부딪힘
    public void PlayBrake() { Play("brake", null, 0.5f); }       // 급브레이크: 끼익 대신 부드러운 공기 빠지는 소리
    public void PlayBlood() { Play("blood", splat, 1f); }      // 앞유리에 피가 튐
    public void PlayWipe() { Play("wipe", whoosh, 0.7f); }       // 와이퍼가 피를 닦음
    public void PlaySlap() { Play("slap", bonk, 0.6f); }         // 앞유리에 부케가 철썩
    public void PlayGrab() { Play("grab", whoosh, 0.8f); }       // 손님이 핸들을 잡아챔
    public void PlayBeep(float volume) { Play("beep", tick, volume); }     // 폭탄 도화선 삐
    public void PlayStomp(float volume) { Play("stomp", bonk, volume); }   // 괴수 발소리 쿵
    public void PlayBoom() { Play("boom", bonk, 1f); }           // 폭탄

    // ---- 말소리 (동물의 숲 주민처럼 글자마다 짧은 "웅얼" 소리) ----
    // 글자 하나당 0.07초짜리 소리 하나. 글자 코드로 음 높이(한 옥타브 12단계)와 모음 느낌(5가지)을 정해서
    // 같은 글이면 같은 소리가 나고, 말하는 사람(voice 0~6: 낮은 남자 ~ 높은 여자, 6 외계인)마다 기본 음이 다르다.
    static readonly float[] VoiceBase = { 105f, 125f, 150f, 210f, 250f, 300f, 420f };
    static readonly float[,] Vowels = { { 700f, 1200f }, { 420f, 2000f }, { 320f, 2300f }, { 520f, 900f }, { 360f, 750f } };
    // 진짜 사람 목소리 조각(Resources/Audio/voice, 귀여운 만화 캐릭터 목소리)을 짧게 잘라 음 높이만 바꿔 쓴다.
    // 조각을 못 읽으면(파일 없음) 코드로 만든 소리로 돌아간다.
    static readonly float[] VoicePitch = { 0.8f, 0.95f, 1.1f, 1.0f, 1.12f, 1.25f, 1.8f };
    readonly Dictionary<int, AudioClip> blips = new Dictionary<int, AudioClip>();
    // 목소리 조각 모음: m 남자(음, 어), f 여자, a 외계인(만화 캐릭터). 각 조각은 소리가 시작하는 곳부터 쓴다
    class VoiceSet { public readonly List<float[]> data = new List<float[]>(); public readonly List<int> rate = new List<int>(); }
    VoiceSet[] voiceSets;
    static int VoiceGroup(int voice) { return voice <= 2 ? 0 : (voice <= 5 ? 1 : 2); }

    void LoadVoices()
    {
        voiceSets = new[] { new VoiceSet(), new VoiceSet(), new VoiceSet() };
        foreach (var c in Resources.LoadAll<AudioClip>("Audio/voice"))
        {
            try
            {
                var raw = new float[c.samples * c.channels];
                if (!c.GetData(raw, 0)) continue;
                var mono = new float[c.samples];
                for (int i = 0; i < mono.Length; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < c.channels; ch++) sum += raw[i * c.channels + ch];
                    mono[i] = sum / c.channels;
                }
                float pk = 0f;
                for (int i = 0; i < mono.Length; i++) pk = Mathf.Max(pk, Mathf.Abs(mono[i]));
                int on = 0;
                while (on < mono.Length - 1 && Mathf.Abs(mono[on]) < pk * 0.12f) on++;       // 앞의 조용한 부분은 자른다
                on = Mathf.Max(0, on - c.frequency / 100);
                var trimmed = new float[mono.Length - on];
                System.Array.Copy(mono, on, trimmed, 0, trimmed.Length);
                int g = c.name.StartsWith("m") ? 0 : (c.name.StartsWith("f") ? 1 : 2);
                voiceSets[g].data.Add(trimmed);
                voiceSets[g].rate.Add(c.frequency);
            }
            catch (System.Exception) { }
        }
    }

    AudioClip Blip(int voice, int code)
    {
        int key = voice * 65536 + code;
        AudioClip c;
        if (blips.TryGetValue(key, out c)) return c;
        if (voiceSets == null) LoadVoices();
        c = voiceSets[VoiceGroup(voice)].data.Count > 0 ? VoiceBlip(voice, code) : SynthBlip(voice, (code * 7) % 12, (code / 12) % 5);
        blips[key] = c;
        return c;
    }

    // 목소리 조각 하나: 글자 코드로 어느 파일의 어느 부분인지, 음이 얼마나 높은지를 정한다
    AudioClip VoiceBlip(int voice, int code)
    {
        var set = voiceSets[VoiceGroup(voice)];
        int si = (code * 31 + voice * 7) % set.data.Count;
        var src = set.data[si];
        int rate = set.rate[si];
        float ratio = VoicePitch[voice] * Mathf.Pow(2f, ((code * 7) % 12 - 6) / 24f);       // 글자마다 음이 오르내림
        int segLen = Mathf.Min(src.Length, Mathf.CeilToInt(rate * 0.16f * ratio));
        int start = src.Length > segLen ? (code * 13) % Mathf.Min(src.Length - segLen, rate / 5) : 0;       // 소리가 시작한 뒤 0.2초 안에서
        var d = new float[Mathf.CeilToInt(Rate * 0.16f)];
        float peak = 0.0001f;
        for (int i = 0; i < d.Length; i++)
        {
            float pos = start + i * ratio * rate / Rate;
            int i0 = (int)pos;
            if (i0 + 1 >= src.Length) { d.Initialize(); break; }
            float fr = pos - i0;
            float v = src[i0] * (1f - fr) + src[i0 + 1] * fr;
            float t = (float)i / d.Length;
            v *= Mathf.Min(1f, t * 25f) * Mathf.Min(1f, (1f - t) * 7f);              // 앞뒤를 부드럽게
            d[i] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }
        float g = Mathf.Min(0.9f / peak, 6f);                                                       // 글자마다 같은 크기로
        for (int i = 0; i < d.Length; i++) d[i] *= g;
        return Make("vblip", d);
    }

    AudioClip SynthBlip(int voice, int pitch, int vowel)
    {
        float f0 = VoiceBase[voice] * Mathf.Pow(2f, pitch / 12f);
        var d = new float[Mathf.CeilToInt(Rate * 0.075f)];
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float v = 0f;
            for (int h = 1; h < 24; h++)
            {
                float fr = f0 * h;
                if (fr > 4500f) break;
                float g = Mathf.Exp(-Mathf.Pow((fr - Vowels[vowel, 0]) / 320f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((fr - Vowels[vowel, 1]) / 500f, 2f));
                v += g * Mathf.Sin(2f * Mathf.PI * fr * t) / h;
            }
            float env = Mathf.Min(1f, t * 220f) * Mathf.Exp(-t * 22f);
            d[i] = v * env * 0.95f;
        }
        return Make("blip", d);
    }

    /// <summary>글자 하나의 웅얼 소리를 지금 낸다.</summary>
    public void PlayBlip(char ch, int voice, float volume)
    {
        if (!cfg.soundOn || char.IsWhiteSpace(ch) || char.IsPunctuation(ch)) return;
        voice = Mathf.Clamp(voice, 0, VoiceBase.Length - 1);
        int code = ch;
        sfx.PlayOneShot(Blip(voice, code), volume * cfg.masterVolume * cfg.sfxVolume);
    }

    struct Babble { public float at; public char ch; public int voice; public float vol; }
    readonly List<Babble> babbles = new List<Babble>();

    /// <summary>문장 하나를 글자 속도(초당 14자쯤)로 웅얼거린다. 새 말이 시작되면 하던 말은 끊긴다.</summary>
    public void Speak(string text, int voice, float volume)
    {
        babbles.Clear();
        if (string.IsNullOrEmpty(text)) return;
        float t = Time.unscaledTime + 0.05f;
        int n = 0;
        foreach (char ch in text)
        {
            if (n++ > 60) break;
            if (char.IsWhiteSpace(ch)) { t += 0.05f; continue; }
            if (char.IsPunctuation(ch)) { t += 0.16f; continue; }
            babbles.Add(new Babble { at = t, ch = ch, voice = voice, vol = volume });
            t += 0.07f;
        }
    }

    public void StopSpeech() { babbles.Clear(); }

    void TickBabble()
    {
        int played = 0;
        while (babbles.Count > 0 && Time.unscaledTime >= babbles[0].at)
        {
            var b = babbles[0];
            babbles.RemoveAt(0);
            if (played++ < 2) PlayBlip(b.ch, b.voice, b.vol);
        }
    }

    // 사람 비명: 그림의 성별에 맞는 목소리. 좀비는 신음
    public void PlayHurt(bool male) { Play(male ? "hurtM" : "hurtF", null, 0.9f); }
    public void PlayZombie() { Play("zomb", null, 0.5f); }
    public void PlayDoorOpen() { Play("dooropen", null, 0.9f); }
    public void PlayDoorClose() { Play("doorclose", null, 0.9f); }

    // 조금 뒤에 한 번 울리는 소리 (문 열고 닫기, 손님의 뒤늦은 비명)
    struct Later { public float at; public string key; public float vol; }
    readonly List<Later> later = new List<Later>();

    public void PlayLater(string key, float delay, float volume)
    {
        later.Add(new Later { at = Time.unscaledTime + delay, key = key, vol = volume });
    }

    void TickLater()
    {
        for (int i = later.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime < later[i].at) continue;
            Play(later[i].key, null, later[i].vol);
            later.RemoveAt(i);
        }
    }

    // Resources/Audio/sfx 의 진짜 효과음 (이름_번호.wav). 같은 이름이 여러 개면 매번 하나를 무작위로 골라 단조롭지 않게 한다.
    // 파일이 없으면 코드로 합성한 소리(fallback)를 쓴다.
    Dictionary<string, List<AudioClip>> bank;

    AudioClip Pick(string key, AudioClip fallback)
    {
        if (bank == null)
        {
            bank = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/sfx"))
            {
                int us = c.name.LastIndexOf('_');
                string k = us > 0 ? c.name.Substring(0, us) : c.name;
                List<AudioClip> l;
                if (!bank.TryGetValue(k, out l)) bank[k] = l = new List<AudioClip>();
                l.Add(c);
            }
        }
        List<AudioClip> list;
        if (bank.TryGetValue(key, out list) && list.Count > 0) return list[Random.Range(0, list.Count)];
        return fallback;
    }

    void Play(string key, AudioClip fallback, float volume) { Play(Pick(key, fallback), volume); }

    void Play(AudioClip clip, float volume)
    {
        if (!cfg.soundOn || clip == null) return;
        sfx.PlayOneShot(clip, volume * cfg.masterVolume * cfg.sfxVolume);
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
        float k = Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed));
        if (!TickGears(speed))
        {
            // 진짜 엔진 소리가 없으면 코드로 만든 소리를 속도에 따라 높낮이만 바꿔 쓴다
            if (engine.clip != engineSynth) { engine.clip = engineSynth; engine.Play(); }
            engine.pitch = Mathf.Lerp(0.7f, 1.9f, k);
        }
        if (!engine.isPlaying) engine.Play();
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
            sfx.PlayOneShot(on ? tick : tock, 0.5f * cfg.masterVolume * cfg.sfxVolume);
        }
    }

    /// <summary>
    /// 매 프레임: 배경음악과 빗소리. rain = 비의 세기(0~1), duck = 음악을 줄이는 배율(일시정지 등에서 작게).
    /// 효과음/엔진과 달리 타이틀, 일시정지, 상점에서도 계속 흐른다.
    /// </summary>
    public void TickMusic(float rain, float duck, int track)
    {
        if (!cfg.soundOn)
        {
            if (bgm.isPlaying) bgm.Pause();
            if (stingerSrc.isPlaying) stingerSrc.Pause();
            if (rainSrc.isPlaying) rainSrc.Pause();
            return;
        }
        TickLater();
        TickBabble();
        float dt = Time.unscaledDeltaTime;
        var want = LoadTrack(track);
        if (track < 0) musicFade = Mathf.MoveTowards(musicFade, 0f, dt / 0.4f);        // 음악 없는 장면(엔딩): 빠르게 끈다
        if (want != null)
        {
            // 곡이 바뀌면 0.8초에 걸쳐 줄였다가 새 곡을 처음부터 틀고 다시 키운다
            float fadeTime = (track == TrackRage || curTrack == TrackRage) ? 0.25f : 0.8f;     // 분노 곡은 짧게 끊고 들어가고 나온다
            if (curTrack != track)
            {
                musicFade = Mathf.MoveTowards(musicFade, 0f, dt / fadeTime);
                if (musicFade <= 0f || !bgm.isPlaying)
                {
                    bgm.clip = want;
                    bgm.time = 0f;
                    bgm.Play();
                    curTrack = track;
                }
            }
            else musicFade = Mathf.MoveTowards(musicFade, 1f, dt / fadeTime);
        }
        if (!bgm.isPlaying) bgm.Play();
        // 결과 효과음이 울리는 동안은 음악을 끈다
        stingerLeft = stingerSrc.isPlaying ? stingerLeft : 0f;
        float stingerMute = stingerSrc.isPlaying ? 0f : 1f;
        bgm.volume = cfg.masterVolume * cfg.bgmVolume * duck * musicFade * stingerMute;
        if (rain > 0.02f)
        {
            if (!rainSrc.isPlaying) rainSrc.Play();
            rainSrc.volume = cfg.masterVolume * cfg.sfxVolume * 0.35f * rain;
        }
        else if (rainSrc.isPlaying) rainSrc.Pause();
    }

    AudioClip LoadTrack(int i)
    {
        if (i < 0 || i >= tracks.Length) return null;
        if (!trackTried[i])
        {
            trackTried[i] = true;
            tracks[i] = Resources.Load<AudioClip>("Audio/" + TrackFiles[i]);
        }
        return tracks[i];
    }

    /// <summary>결과 효과음을 바로 끊고, 지금 틀어야 할 곡을 기다림 없이 처음부터 튼다 (재시작, 로비 이동).</summary>
    public void StopStinger()
    {
        if (!stingerSrc.isPlaying) return;
        stingerSrc.Stop();
        bgm.Stop();
        curTrack = -1;
        musicFade = 1f;
    }

    /// <summary>하루 영업을 마쳤을 때(win) / 목숨이 다했을 때 한 번 울리는 짧은 곡. 파일이 없으면 아무것도 안 한다.</summary>
    public void PlayStinger(bool win)
    {
        if (!cfg.soundOn) return;
        var clip = Resources.Load<AudioClip>("Audio/" + (win ? "stinger_win" : "stinger_lose"));
        if (clip == null) return;
        stingerSrc.clip = clip;
        stingerSrc.volume = cfg.masterVolume * cfg.bgmVolume * 2f;
        stingerSrc.Play();
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

    // 꿀꿀: 짧게 떨리며 올랐다 내려오는 소리
    static AudioClip MakeOink()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.3f)];
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float f = 260f + 140f * Mathf.Sin(t * Mathf.PI) + 30f * Mathf.Sin(t * 90f);
            phase += f / Rate * Mathf.PI * 2f;
            d[i] = (Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f)) * Mathf.Sin(t * Mathf.PI) * 0.5f;
        }
        return Make("oink", d);
    }

    // 정전: 윙 하며 전원이 꺼지는 소리
    static AudioClip MakePowerDown()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.9f)];
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            phase += Mathf.Lerp(700f, 45f, Mathf.Sqrt(t)) / Rate * Mathf.PI * 2f;
            d[i] = Mathf.Sin(phase) * Mathf.Pow(1f - t, 1.5f) * Mathf.Min(1f, t * 30f) * 0.5f;
        }
        return Make("powerdown", d);
    }

    // 미니건 한 발: 짧은 잡음에 낮은 쿵이 섞인 소리
    static AudioClip MakeGunShot()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.07f)];
        var rnd = new System.Random(41);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            phase += 170f / Rate * Mathf.PI * 2f;
            float env = Mathf.Pow(1f - t, 3f);
            d[i] = (((float)rnd.NextDouble() * 2f - 1f) * 0.8f + Mathf.Sin(phase) * 0.6f) * env * 0.7f;
        }
        return Make("gun", d);
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

    // 철퍽: 낮은 음이 툭 떨어지고 젖은 잡음이 짧게 번진다
    static AudioClip MakeSplat()
    {
        var d = new float[Mathf.CeilToInt(Rate * 0.28f)];
        var rnd = new System.Random(31);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            phase += Mathf.Lerp(190f, 55f, Mathf.Sqrt(t)) / Rate * Mathf.PI * 2f;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            lp += (n - lp) * 0.18f;
            d[i] = (Mathf.Sin(phase) * 0.5f + lp * 0.9f) * Mathf.Pow(1f - t, 3f);
        }
        return Make("splat", d);
    }

    // 엔딩 로켓: 낮은 울림이 점점 높아지는 소리 + 거친 잡음 (3.5초)
    static AudioClip MakeRocket()
    {
        var d = new float[Mathf.CeilToInt(Rate * 3.5f)];
        var rnd = new System.Random(23);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / d.Length;
            float env = Mathf.Clamp01(t * 8f) * Mathf.Pow(1f - t, 0.6f);
            phase += Mathf.Lerp(60f, 520f, t * t) / Rate * Mathf.PI * 2f;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            lp += (n - lp) * Mathf.Lerp(0.08f, 0.5f, t);
            d[i] = (Mathf.Sin(phase) * 0.45f + lp * 0.55f) * env * 0.9f;
        }
        return Make("rocket", d);
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
