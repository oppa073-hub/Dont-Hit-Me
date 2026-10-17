#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

// 자동 운전 시험 봇 (에디터/개발 빌드 전용). 명령줄에 -simbot 이 있을 때만 켜진다. 사람이 하는 것처럼 키 입력을 흘려 넣어
// 며칠 치 영업을 빠르게 돌려 보고, 오류/예외, 사고 원인, 수입과 업그레이드 진행, 프레임 시간, 메모리 증가를 보고서 파일로 남긴다.
//   -simdays N   며칠 돌릴지 (기본 5)           -simskill 0|1|2   0 완벽 / 1 능숙 / 2 평범 (기본 1)
//   -simmode shift|endless                      -simcover   사건을 번갈아 강제로 일으키고 무적으로 돌린다(예외 찾기용)
//   -simnoshop  번 코인을 쓰지 않는다            -simseed N   -simout 파일경로   -simmaxsec N  (실제 시간 상한, 기본 1500)
// 실행은 Tools/run-simbot.ps1 이 한다. 플레이어 저장 데이터는 시작할 때 비우므로(PlayerPrefs) 실행 스크립트가 레지스트리를 백업/복구한다.
public partial class DriveGame
{
    public static bool BotFinished;
    public static int BotExitCode;
    static bool botWiped;

    SimBot bot;

    void BotInit()
    {
        if (!SimBot.Requested) return;
        if (!botWiped)
        {
            // 저장 데이터를 비운 뒤 장면을 다시 불러와서, 모든 매니저가 처음 설치한 것처럼 시작하게 한다
            botWiped = true;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("DontHitMe.Day", SimBot.StartDayArg);          // -simday: 후반 난이도를 바로 시험하려고 Day 번호를 정해 둔다
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }
        bot = new SimBot(this);
    }

    void BotUpdate() { if (bot != null) bot.Update(); }

    void BotOnPopup(string text) { if (bot != null) bot.OnPopup(text); }

    sealed class SimBot
    {
        // ---- 설정 ----
        readonly DriveGame g;
        readonly int days, skill, seed;
        readonly bool cover, shop, shiftMode;
        readonly string outPath;
        readonly float maxWallSec;

        // 능숙도: 시야(m), 판단 주기(초)
        static readonly float[] Sight = { 80f, 45f, 30f };
        static readonly float[] Interval = { 0.04f, 0.14f, 0.28f };

        // ---- 입력 ----
        readonly Keyboard kb;
        readonly HashSet<Key> held = new HashSet<Key>();
        readonly HashSet<Key> sent = new HashSet<Key>();

        // ---- 진행 ----
        enum Phase { Boot, Playing, Done }
        Phase phase = Phase.Boot;
        int day, frame, dayFrames;
        readonly Stopwatch wall = Stopwatch.StartNew();
        bool finished;
        readonly StringBuilder rep = new StringBuilder(8192);

        // ---- 판단 ----
        float targetX, replanT, blinkCool, unavoidableT;
        bool unavoidable;
        struct Th { public float t0, tm, t1, x0, xm, x1, clear, w; }
        readonly List<Th> ths = new List<Th>(64);

        // ---- 통계 (하루 단위) ----
        TaxiState prevState;
        float[] stateTime = new float[4];
        int prevHits, prevLives, healCount;
        float lastHitSrcTime = -99f, lastPopupTime = -99f;
        string lastHitSrc = "", lastPopup = "", hitDetail = "", hist = "", planDump = "";
        readonly Queue<string> planRing = new Queue<string>();
        static bool Dbg { get { return Has("-simdbg"); } }
        static bool NoUnav { get { return Has("-simnounav"); } }
        readonly Dictionary<string, int> causes = new Dictionary<string, int>();
        readonly Dictionary<string, int> popups = new Dictionary<string, int>();
        readonly List<string> timeline = new List<string>();
        readonly List<string> trips = new List<string>();
        int tripFareBefore; float tripStartT; int tripIdx;
        int stallLogged; float lastProgressT; int lastProgressKey;
        int dayStartCoins;
        readonly List<float> frameMs = new List<float>(16384);
        readonly List<long> frameAlloc = new List<long>(16384);
        long lastTs, lastAlloc;
        int gcStart;
        string memStart = "";

        // 전체
        readonly Dictionary<string, int> allCauses = new Dictionary<string, int>();
        readonly Dictionary<string, int> allPopups = new Dictionary<string, int>();
        readonly List<string> dayLines = new List<string>();
        readonly List<string> buyLog = new List<string>();
        readonly Dictionary<string, int> errors = new Dictionary<string, int>();
        readonly Dictionary<string, string> errorStack = new Dictionary<string, string>();
        readonly Dictionary<string, int> warns = new Dictionary<string, int>();
        int errorTotal;
        readonly int dayType;                                   // -simdaytype: 모든 날을 같은 종류로 (-1 이면 게임이 정함)
        string setApplied = "";
        // 날별 결과 (요약용)
        readonly List<float> sumElapsed = new List<float>(), sumFare = new List<float>(), sumNet = new List<float>(), sumTrips = new List<float>(), sumHits = new List<float>(), sumNear = new List<float>(), sumCoins = new List<float>();
        readonly List<bool> sumDied = new List<bool>();

        // 강제 사건 (cover)
        float nextForce;
        int forceIdx, quirkIdx;

        public static bool Requested { get { return Has("-simbot"); } }

        public static int StartDayArg { get { return Mathf.Max(1, ArgInt("-simday", 1)); } }

        static bool Has(string name)
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a == name) return true;
            return false;
        }

        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        static int ArgInt(string name, int def)
        {
            int v;
            return int.TryParse(Arg(name, ""), out v) ? v : def;
        }

        public SimBot(DriveGame game)
        {
            g = game;
            days = Mathf.Max(1, ArgInt("-simdays", 5));
            skill = Mathf.Clamp(ArgInt("-simskill", 1), 0, 2);
            seed = ArgInt("-simseed", 1234);
            cover = Has("-simcover");
            shop = !Has("-simnoshop");
            shiftMode = Arg("-simmode", "shift") != "endless";
            maxWallSec = ArgInt("-simmaxsec", 1500);
            dayType = ArgInt("-simdaytype", -1);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SimReports"));
            Directory.CreateDirectory(dir);
            outPath = Arg("-simout", Path.Combine(dir, "simbot-" + stamp + ".txt"));

            // 창이 포커스를 잃은(배치모드) 상태에서도 가상 키보드 입력을 받도록, 설정 복사본을 만들어 쓴다 (프로젝트의 설정 파일은 건드리지 않는다)
            var st = UnityEngine.Object.Instantiate(InputSystem.settings);
            st.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            st.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = st;
            kb = InputSystem.AddDevice<Keyboard>("SimBotKeyboard");
            Application.logMessageReceived += OnLog;
            ApplySettings(Arg("-simset", ""));
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureFramerate = 60;                  // 한 프레임이 항상 1/60초: 실제 시간과 상관없이 최대한 빨리 돌린다

            g.peds.Hit += p =>
            {
                SetSrc(p.kind == 2 ? "보행자(튀어나옴)" : "보행자(횡단)");
                hitDetail = "ped x=" + p.x.ToString("F1") + " dir=" + p.dir + " ws=" + p.walkSpeed.ToString("F1") + " z=" + p.z.ToString("F1") + " | car x=" + g.carX.ToString("F1")
                          + " lv=" + g.lateralVel.ToString("F1") + " tgt=" + targetX.ToString("F1") + " spd=" + g.speed.ToString("F0") + " thr=" + ths.Count + " unav=" + unavoidable + " hist=" + hist + (Dbg ? " || " + planDump : "");
            };
            g.traffic.Hit += o => SetSrc("차량:" + KindName(o.kind));
            g.traffic.AmbulancePassed += b => { if (b) SetSrc("구급차를 막음"); };

            rep.AppendLine("SimBot 보고서  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            rep.AppendLine("days=" + days + " skill=" + skill + " mode=" + (shiftMode ? "shift" : "endless") + " cover=" + cover + " shop=" + shop + " seed=" + seed);
            rep.AppendLine("shiftSeconds=" + g.shiftSeconds + " baseFare=" + g.baseFare + " farePerMeter=" + g.farePerMeter + " cruiseCap=" + g.cruiseCap + " healSeconds=" + g.healSeconds);
            rep.AppendLine();
        }

        // -simset "필드=값;필드=값": 인스펙터 값(public 필드)을 바꿔서 돌린다 (밸런스 비교용). 범위(Vector2)는 "a:b".
        void ApplySettings(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;
            foreach (var part in spec.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string name = part.Substring(0, eq).Trim(), val = part.Substring(eq + 1).Trim();
                var f = typeof(DriveGame).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (f == null) { setApplied += name + "=(없는 필드) "; continue; }
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                try
                {
                    if (f.FieldType == typeof(float)) f.SetValue(g, float.Parse(val, ci));
                    else if (f.FieldType == typeof(int)) f.SetValue(g, int.Parse(val, ci));
                    else if (f.FieldType == typeof(bool)) f.SetValue(g, val == "1" || val.ToLowerInvariant() == "true");
                    else if (f.FieldType == typeof(Vector2)) { var xy = val.Split(':'); f.SetValue(g, new Vector2(float.Parse(xy[0], ci), float.Parse(xy[1], ci))); }
                    else { setApplied += name + "=(지원 안 하는 형식) "; continue; }
                    setApplied += name + "=" + val + " ";
                }
                catch (Exception e) { setApplied += name + "=(오류:" + e.Message + ") "; }
            }
            rep.AppendLine("설정 바꿈: " + setApplied);
        }

        static float Avg(List<float> l) { if (l.Count == 0) return 0f; float s = 0f; foreach (var v in l) s += v; return s / l.Count; }

        // 기계가 읽기 쉬운 한 줄 요약 (스윕 비교용)
        string SummaryLine()
        {
            int died = 0; foreach (var d in sumDied) if (d) died++;
            float totalSec = 0f, totalHits = 0f, totalNear = 0f;
            for (int i = 0; i < sumElapsed.Count; i++) { totalSec += sumElapsed[i]; totalHits += sumHits[i]; totalNear += sumNear[i]; }
            float perMin = totalSec > 0f ? totalHits / (totalSec / 60f) : 0f, nearMin = totalSec > 0f ? totalNear / (totalSec / 60f) : 0f;
            return "SUMMARY days=" + sumElapsed.Count + " diedPct=" + Mathf.RoundToInt(100f * died / Mathf.Max(1, sumDied.Count)) + " avgSec=" + Avg(sumElapsed).ToString("F0")
                 + " hitsPerMin=" + perMin.ToString("F2") + " nearPerMin=" + nearMin.ToString("F1") + " trips=" + Avg(sumTrips).ToString("F1") + " fare=" + Avg(sumFare).ToString("F0")
                 + " net=" + Avg(sumNet).ToString("F0") + " coins=" + Avg(sumCoins).ToString("F0") + " set=[" + setApplied.Trim() + "] skill=" + skill;
        }

        // ================= 매 프레임 =================

        public void Update()
        {
            if (finished) return;
            frame++;
            if (wall.Elapsed.TotalSeconds > maxWallSec)
            {
                rep.AppendLine("!! 실제 시간 상한(" + maxWallSec + "초)에 도달해 중단");
                Finish(3);
                return;
            }

            held.Clear();
            switch (phase)
            {
                case Phase.Boot:
                    StartDay();
                    break;
                case Phase.Playing:
                    PlayFrame();
                    break;
            }
            Flush();
        }

        void StartDay()
        {
            day++;
            g.shiftMode = shiftMode;
            g.forceDayType = cover ? CoverDay(day) : dayType;
            g.debugGod = cover;
            Random.InitState(seed * 1000 + day);
            if (day == 1 && Has("-simfull"))
            {
                // 후반 시험: 업그레이드를 모두 끝까지 사고 개조도 날개 전까지(엔딩 제외) 끝낸 채로 시작한다
                g.upgrades.AddCoins(300000);
                for (int pass = 0; pass < 8; pass++) for (int id = 0; id < g.upgrades.Count; id++) g.upgrades.TryBuy(id);
                for (int i = 0; i < g.projects.Count - 1; i++) g.projects.Buy(i, g.upgrades);
                g.ApplyProjectPerks();
            }
            if (day == 1 && Has("-simending"))
            {
                // 엔딩 시험: 코인을 잔뜩 주고 택시 개조를 모두 끝낸 채로 시작한다
                g.upgrades.AddCoins(200000);
                for (int i = 0; i < g.projects.Count; i++) g.projects.Buy(i, g.upgrades);
                g.ApplyProjectPerks();
            }
            dayStartCoins = g.upgrades.Coins;
            g.StartGame();

            for (int i = 0; i < stateTime.Length; i++) stateTime[i] = 0f;
            causes.Clear(); popups.Clear(); timeline.Clear(); trips.Clear();
            frameMs.Clear(); frameAlloc.Clear();
            prevState = TaxiState.Cruise;
            prevHits = g.hits;
            prevLives = g.session.Lives;
            healCount = 0;
            tripFareBefore = 0; tripStartT = 0f; tripIdx = 0;
            stallLogged = 0; lastProgressT = 0f; lastProgressKey = -1;
            nextForce = 6f; forceIdx = 0; quirkIdx = 0;
            replanT = 0f; targetX = 0f; blinkCool = 0f;
            dayFrames = 0;
            lastTs = 0; lastAlloc = 0;
            gcStart = GC.CollectionCount(0);
            memStart = MemSnapshot();
            phase = Phase.Playing;
        }

        static int CoverDay(int d)
        {
            switch ((d - 1) % 5)
            {
                case 0: return (int)DayType.Kaiju;
                case 1: return (int)DayType.Hero;
                case 4: return (int)DayType.Zombie;
                case 2: return (int)DayType.Crowded;
                default: return (int)DayType.Normal;
            }
        }

        void PlayFrame()
        {
            if (g.gameOver)
            {
                if (g.bestSaved) EndDay();               // 결과(코인 지급 포함)가 저장된 다음 프레임에 정리한다
                return;
            }
            dayFrames++;
            float cap = g.shiftMode ? g.shiftSeconds + g.shiftOvertimeMax + 90f : 240f;
            if (g.elapsed > cap)
            {
                rep.AppendLine("!! Day " + day + ": 시간 상한을 넘겨 강제 종료 (shiftLeft=" + g.shiftLeft.ToString("F1") + " state=" + g.taxi.State + ")");
                g.session.ForceEnd();
                return;
            }
            if (g.flow == GameFlow.Ending)
            {
                if (frame % 4 == 0) held.Add(Key.Space);               // 엔딩이 끝나면 Space 로 로비로 (날개 택시 개조를 산 뒤에만 나온다)
                return;
            }
            if (g.flow == GameFlow.Lobby) { EndDay(); return; }       // 엔딩 뒤에는 로비로 돌아온다: 그 날을 마친 것으로 센다
            if (g.flow != GameFlow.Playing) return;

            SampleFrame();
            Track();
            if (cover) CoverForce();
            Drive();
        }

        // ================= 통계 =================

        void SampleFrame()
        {
            long ts = Stopwatch.GetTimestamp();
            long alloc = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            if (lastTs != 0 && dayFrames > 30)
            {
                frameMs.Add((ts - lastTs) * 1000f / Stopwatch.Frequency);
                frameAlloc.Add(Math.Max(0L, alloc - lastAlloc));
            }
            lastTs = ts;
            lastAlloc = alloc;
        }

        void Track()
        {
            float dt = Time.deltaTime;
            var taxi = g.taxi;
            if (dt > 0f) stateTime[(int)taxi.State] += dt;

            // 손님 한 명 분 기록: 탑승 시작 -> 도착
            if (prevState != TaxiState.Carrying && taxi.State == TaxiState.Carrying)
            {
                tripFareBefore = taxi.FareTotal;
                tripStartT = g.elapsed;
            }
            else if (prevState == TaxiState.Carrying && taxi.State != TaxiState.Carrying)
            {
                tripIdx++;
                int gain = taxi.FareTotal - tripFareBefore;
                trips.Add("  #" + tripIdx + "  t=" + tripStartT.ToString("F0") + "s  " + (g.elapsed - tripStartT).ToString("F0") + "초  요금 +" + gain
                          + "  (제한 " + taxi.TripLimit.ToString("F0") + "초)  " + (taxi.Pax != null ? taxi.Pax.quirk + (taxi.Pax.heist ? "/강도" : "") + "/" + taxi.PaxName : "?"));
            }
            prevState = taxi.State;

            // 사고 원인
            if (g.hits > prevHits)
            {
                string cause = g.elapsed - lastHitSrcTime < 0.5f ? lastHitSrc
                             : (g.elapsed - lastPopupTime < 1.5f ? "기타:" + lastPopup : "원인불명");
                cause += (g.speed < 6f ? " [저속]" : " [주행]") + " " + taxi.State;
                Add(causes, cause); Add(allCauses, cause);
                timeline.Add("[" + g.elapsed.ToString("F1") + "s] 사고 -> " + cause + " (목숨 " + g.session.Lives + ") " + hitDetail);
            }
            if (g.session.Lives > prevLives) healCount++;
            prevHits = g.hits;
            prevLives = g.session.Lives;

            // 소프트락 감시: 70초 동안 요금/손님/점수가 하나도 안 변하면 기록
            int key = taxi.FareTotal + taxi.Trips * 100003 + (int)taxi.State * 7 + g.session.TotalScore / 50;
            if (key != lastProgressKey) { lastProgressKey = key; lastProgressT = g.elapsed; }
            else if (g.elapsed - lastProgressT > 70f && stallLogged < 3)
            {
                stallLogged++;
                lastProgressT = g.elapsed;
                string msg = "[" + g.elapsed.ToString("F0") + "s] 정체 의심: state=" + taxi.State + " caller=" + (taxi.Caller != null) + " speed=" + g.speed.ToString("F1")
                           + " turning=" + g.turning + " nextInter=" + g.nextInterDist.ToString("F0") + " shiftLeft=" + g.shiftLeft.ToString("F0");
                timeline.Add(msg);
                rep.AppendLine("!! Day " + day + " " + msg);
            }
        }

        public void OnPopup(string text)
        {
            if (phase != Phase.Playing) return;
            string norm = Normalize(text);
            Add(popups, norm); Add(allPopups, norm);
            lastPopup = norm;
            lastPopupTime = g.elapsed;
            if (timeline.Count < 120) timeline.Add("[" + g.elapsed.ToString("F0") + "s] " + text);
        }

        void SetSrc(string s) { lastHitSrc = s; lastHitSrcTime = g.elapsed; }

        static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (c < '0' || c > '9') sb.Append(c);
            return sb.ToString().Trim();
        }

        static void Add(Dictionary<string, int> d, string k)
        {
            int v;
            d.TryGetValue(k, out v);
            d[k] = v + 1;
        }

        static string KindName(int k)
        {
            switch (k)
            {
                case TrafficManager.Sedan: return "승용차";
                case TrafficManager.Truck: return "트럭";
                case TrafficManager.Broken: return "고장차";
                case TrafficManager.Cones: return "콘";
                case TrafficManager.Ball: return "공";
                case TrafficManager.Kaiju: return "괴수";
                case TrafficManager.Flame: return "불길";
                case TrafficManager.Smoke: return "연기";
                case TrafficManager.Police: return "경찰";
                case TrafficManager.Rival: return "라이벌택시";
                case TrafficManager.Pig: return "돼지";
                case TrafficManager.Ambulance: return "구급차";
                default: return "종류" + k;
            }
        }

        // ================= 강제 사건 (cover) =================

        void CoverForce()
        {
            if (g.elapsed < nextForce) return;
            nextForce = g.elapsed + 11f;
            switch (forceIdx++ % 9)
            {
                case 0: g.elapsed += g.levelSeconds; break;                                  // 레벨 +1 (인카운트 조건)
                case 1: g.taxi.ForcePaxType = PassengerCatalog.ByQuirk(1 + (quirkIdx++ % 5)); break;
                case 2: g.enc.Force(EncounterType.Emergency, g.elapsed); break;
                case 3: g.taxi.ForceHeistNext = true; break;
                case 4: g.enc.Force(EncounterType.Rival, g.elapsed); break;
                case 5: g.rage.DebugFill(); break;
                case 6: g.enc.Force(EncounterType.Fans, g.elapsed); break;
                case 7: g.enc.Force(EncounterType.Pigs, g.elapsed); break;
                case 8: g.enc.Force(EncounterType.Blackout, g.elapsed); break;
            }
        }

        // ================= 운전 =================

        void Drive()
        {
            var taxi = g.taxi;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            blinkCool -= dt;

            if (taxi.State == TaxiState.Talking)
            {
                if (frame % 4 == 0) held.Add(Key.Space);          // 카메라가 돌아간 뒤 Space 로 태운다
                return;
            }
            if (g.turning) return;                                // 회전 중에는 차가 알아서 간다

            replanT -= dt;
            if (replanT <= 0f)
            {
                replanT = Interval[skill] * Random.Range(0.8f, 1.25f);
                Plan();
            }
            Steer();

            bool brake = (unavoidable && !NoUnav) || WantBrake(dt);
            if (brake) held.Add(Key.S);

            Blinker();
        }

        float Limit { get { return g.roadHalfWidth - CarHalfWidth; } }

        // 어느 차선(가로 위치)이 가장 안전한지 고른다
        void Plan()
        {
            float v = Mathf.Max(g.speed, 6f);
            float sight = Sight[skill];
            ths.Clear();
            float rhw = g.roadHalfWidth;

            var peds = g.peds;
            for (int i = 0; i < peds.Count; i++)
            {
                var p = peds[i];
                if (p.kind == 0 || p.st == 0 || p.fallT >= 0f || p.caller || p.zombie) continue;           // 좀비는 쳐도 되니 피하지 않는다
                if (p.z < -1f || p.z > sight) continue;
                float tStart = 0f;
                if (p.st == 1) tStart = Mathf.Abs(p.x - p.curbX) / (p.kind == 2 ? Mathf.Max(0.5f, p.walkSpeed) : 2.2f) + p.waitTime;
                else if (p.st == 2) tStart = Mathf.Max(0f, p.pauseLeft);
                float t0 = Mathf.Max(0f, (p.z - 2.4f) / v), t1 = Mathf.Max(t0 + 0.05f, (p.z + 0.6f) / v), tm = 0.5f * (t0 + t1);
                var th = new Th { t0 = t0, tm = tm, t1 = t1, clear = CarHalfWidth + 0.3f + 0.25f, w = 10f };
                th.x0 = PedX(p, t0, tStart, rhw); th.xm = PedX(p, tm, tStart, rhw); th.x1 = PedX(p, t1, tStart, rhw);
                ths.Add(th);
            }

            var tr = g.traffic;
            for (int i = 0; i < tr.Count; i++)
            {
                var o = tr[i];
                int k = o.kind;
                if (k == TrafficManager.Ball || k == TrafficManager.Smoke || k == TrafficManager.Boom || k == TrafficManager.Ambulance) continue;
                if (o.z < -5f || o.z > sight + 15f) continue;
                float closing = Mathf.Max(3f, v - o.speed);
                float t0 = Mathf.Max(0f, (o.z - 5f) / closing), t1 = Mathf.Max(t0 + 0.05f, (o.z + 4f) / closing);
                float w = (k == TrafficManager.Cones || k == TrafficManager.Pig) ? 1.5f : 10f;
                ths.Add(new Th { t0 = t0, tm = 0.5f * (t0 + t1), t1 = t1, x0 = o.x, xm = o.x, x1 = o.x, clear = CarHalfWidth + o.halfW + 0.3f, w = w });
            }

            float ad = tr.AmbulanceDist;
            if (ad >= 0f && ad < 80f)
            {
                float ax = tr.AmbulanceX;
                float t0 = Mathf.Max(0f, (ad - 4f) / 14f), t1 = (ad + 4f) / 14f;
                ths.Add(new Th { t0 = t0, tm = 0.5f * (t0 + t1), t1 = t1, x0 = ax, xm = ax, x1 = ax, clear = 2.6f, w = 8f });
            }

            float desired = 0f;
            if (g.taxi.State == TaxiState.Cruise && g.taxi.Caller != null && g.taxi.Caller.z < 90f)
                desired = Mathf.Clamp(g.taxi.Caller.x * 0.9f, -Limit, Limit);

            float lag = 0.1f, vmax = g.maxLateral * 0.85f, limit = Limit;
            float best = float.MaxValue, bestL = targetX, bestHitT = 99f;
            for (float L = -limit; L <= limit + 0.001f; L += 0.5f)
            {
                float cost = 0.10f * Mathf.Abs(L - desired) + 0.03f * Mathf.Abs(L - g.carX) + (Mathf.Abs(L - targetX) > 0.01f ? 0.04f : 0f);
                float hitT = 99f;
                for (int i = 0; i < ths.Count; i++)
                {
                    var th = ths[i];
                    if (Hits(L, th.t0, th.x0, th.clear, lag, vmax) || Hits(L, th.tm, th.xm, th.clear, lag, vmax) || Hits(L, th.t1, th.x1, th.clear, lag, vmax))
                    {
                        cost += th.w / (1f + 2f * th.t0);          // 먼 위험은 가까워지면서 다시 피할 수 있으니 덜 무겁게
                        hitT = Mathf.Min(hitT, th.t0);
                    }
                }
                if (cost < best) { best = cost; bestL = L; bestHitT = hitT; }
            }
            targetX = bestL;
            if (Dbg)
            {
                var d = new StringBuilder();
                d.Append("plan@").Append(g.elapsed.ToString("F1")).Append(" v=").Append(v.ToString("F0")).Append(" carX=").Append(g.carX.ToString("F1")).Append(" best=").Append(bestL.ToString("F1")).Append("/").Append(best.ToString("F1"));
                for (int i = 0; i < ths.Count; i++) { var th = ths[i]; d.Append(" [t").Append(th.t0.ToString("F1")).Append("-").Append(th.t1.ToString("F1")).Append(" x").Append(th.x0.ToString("F1")).Append(">").Append(th.x1.ToString("F1")).Append(" c").Append(th.clear.ToString("F1")).Append("]"); }
                planRing.Enqueue(d.ToString());
                if (planRing.Count > 14) planRing.Dequeue();
                planDump = string.Join(" ## ", planRing.ToArray());
            }
            hist = (hist + bestL.ToString("F1") + "/" + ths.Count + ",");
            if (hist.Length > 60) hist = hist.Substring(hist.Length - 60);
            unavoidable = best >= 1.5f && bestHitT < 2.2f;            // 어디로 가도 부딪힌다: 브레이크로 시간을 번다
            // 막혀서 오래 서 있으면(바리케이드 등) 포기하고 들이받는다
            if (unavoidable && g.speed < 1.5f) unavoidableT += replanT + Interval[skill]; else if (!unavoidable) unavoidableT = 0f;
            if (unavoidableT > 9f) { unavoidable = false; if (unavoidableT > 12f) unavoidableT = 0f; }
        }

        bool Hits(float L, float t, float x, float clear, float lag, float vmax)
        {
            float reach = vmax * Mathf.Max(0f, t - lag);
            float pos = g.carX + Mathf.Clamp(L - g.carX, -reach, reach);
            return Mathf.Abs(pos - x) < clear;
        }

        static float PedX(PedestrianManager.Ped p, float t, float tStart, float rhw)
        {
            float x = p.st == 3 ? p.x + p.dir * p.walkSpeed * t : p.curbX + p.dir * p.walkSpeed * Mathf.Max(0f, t - tStart);
            float far = rhw + 0.6f;
            if (p.dir * x > far) x = p.dir * far;
            return x;
        }

        // 목표 가로 위치로 A/D 를 눌러 간다 (관성을 감안해 속도를 맞춘다)
        void Steer()
        {
            float diff = targetX - g.carX;
            float vDes = Mathf.Clamp(diff * 5f, -g.maxLateral, g.maxLateral);
            float vStop = Mathf.Sqrt(2f * g.lateralAccel * 0.7f * Mathf.Abs(diff));
            vDes = Mathf.Clamp(vDes, -vStop, vStop);
            float v = g.lateralVel;
            if (vDes > v + 0.8f) held.Add(Key.D);
            else if (vDes < v - 0.8f) held.Add(Key.A);
        }

        bool WantBrake(float dt)
        {
            var taxi = g.taxi;
            float v = g.speed;
            float stopDist = v * v / 40f;                           // 브레이크 감속이 20m/s² 일 때 멈추는 거리
            if (taxi.State == TaxiState.Waiting) return true;
            if (taxi.State == TaxiState.Cruise && taxi.Caller != null)
            {
                float cz = taxi.Caller.z;
                if (cz < -2f) return false;
                if (v < 3f && cz > 7f) return false;                // 너무 일찍 멈췄다: 조금 더 간다
                return cz < 4f + stopDist + v * dt * 2f;
            }
            if (taxi.State == TaxiState.Carrying)
            {
                int f, r; float sd;
                g.DestRel(out f, out r, out sd);
                if (r != 0 || f < 0) return false;
                float radius = Mathf.Max(1f, g.arriveRadius);
                if (sd < -radius) return false;
                if (v < 2f && sd > radius) return false;
                return sd - radius * 0.4f <= stopDist + 1f;
            }
            return false;
        }

        // 목적지가 다음 교차로 쪽이면 깜빡이를 미리 켜 둔다
        void Blinker()
        {
            if (blinkCool > 0f || g.turning) return;
            int want = 0;
            if (g.taxi.State == TaxiState.Carrying)
            {
                int f, r; float sd;
                g.DestRel(out f, out r, out sd);
                if (f < 0 || (f == 0 && r != 0)) want = r < 0 ? -1 : 1;
            }
            if (want == 0 && g.traffic.HasKaiju) want = 1;            // 괴수가 길을 막으면 다음 교차로에서 돈다
            int cur = g.blinker;
            if (cur == want) return;
            held.Add(want == 0 ? (cur < 0 ? Key.Q : Key.E) : (want < 0 ? Key.Q : Key.E));
            blinkCool = 0.5f;
        }

        // 눌린 키 집합이 바뀌었을 때만 가상 키보드에 새 상태를 보낸다
        void Flush()
        {
            if (kb == null || (held.SetEquals(sent))) return;
            sent.Clear();
            foreach (var k in held) sent.Add(k);
            var arr = new Key[held.Count];
            held.CopyTo(arr);
            InputSystem.QueueStateEvent(kb, new KeyboardState(arr));
        }

        // ================= 하루 끝 =================

        void EndDay()
        {
            var taxi = g.taxi;
            int coins = g.upgrades.Coins;
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== Day " + day + "  날=" + g.dayEvents.Type + "  모드=" + (g.shiftMode ? "영업" : "무한") + " ===");
            sb.AppendLine("시간 " + g.elapsed.ToString("F0") + "초  레벨 " + g.difficulty.MaxLevelReached + "  손님 " + taxi.Trips + "명  요금 " + taxi.FareTotal
                          + "  사고 " + g.hits + "건  니어미스 " + g.nearMisses + "  점수 " + g.session.TotalScore + (g.shiftMode ? "  평점 " + g.ShiftStars() + "/3  순수익 " + g.ShiftNet() : ""));
            sb.AppendLine("코인 +" + (coins - dayStartCoins) + " (보유 " + coins + ")  체력회복 " + healCount + "회  종료 목숨 " + g.session.Lives);
            float tot = Mathf.Max(0.01f, stateTime[0] + stateTime[1] + stateTime[2] + stateTime[3]);
            sb.AppendLine("시간 분포: 손님찾기 " + Pct(stateTime[0], tot) + "  대화 " + Pct(stateTime[1], tot) + "  이동 " + Pct(stateTime[2], tot) + "  대기 " + Pct(stateTime[3], tot));
            if (causes.Count > 0) sb.AppendLine("사고 원인: " + Dump(causes));
            foreach (var t in trips) sb.AppendLine(t);
            sb.AppendLine("프레임: " + PerfLine());
            sb.AppendLine("메모리: 시작 " + memStart + " -> 끝 " + MemSnapshot());
            if (popups.Count > 0) sb.AppendLine("팝업 상위: " + Top(popups, 14));
            sb.AppendLine("--- 타임라인(앞 120줄) ---");
            foreach (var t in timeline) sb.AppendLine("  " + t);
            dayLines.Add(sb.ToString());
            sumElapsed.Add(g.elapsed); sumFare.Add(taxi.FareTotal); sumNet.Add(g.shiftMode ? g.ShiftNet() : taxi.FareTotal); sumTrips.Add(taxi.Trips); sumHits.Add(g.hits); sumNear.Add(g.nearMisses);
            sumCoins.Add(coins - dayStartCoins); sumDied.Add(g.session.Lives <= 0);

            if (shop) DoShop();

            WriteReport(false);
            if (day >= days) { Finish(errorTotal > 0 ? 2 : 0); return; }
            StartDay();
        }

        static string Pct(float a, float tot) { return Mathf.RoundToInt(100f * a / tot) + "%"; }

        static string Dump(Dictionary<string, int> d)
        {
            var sb = new StringBuilder();
            foreach (var kv in d) sb.Append(kv.Key).Append(" x").Append(kv.Value).Append("   ");
            return sb.ToString();
        }

        static string Top(Dictionary<string, int> d, int n)
        {
            var list = new List<KeyValuePair<string, int>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < n; i++) sb.Append(list[i].Key).Append(" x").Append(list[i].Value).Append(" | ");
            return sb.ToString();
        }

        string PerfLine()
        {
            if (frameMs.Count < 10) return "표본 부족";
            var ms = new List<float>(frameMs);
            ms.Sort();
            float sum = 0f; foreach (var m in ms) sum += m;
            int over33 = 0; foreach (var m in ms) if (m > 33f) over33++;
            var al = new List<long>(frameAlloc);
            al.Sort();
            long asum = 0; foreach (var a in al) asum += a;
            return "평균 " + (sum / ms.Count).ToString("F2") + "ms  p95 " + ms[(int)(ms.Count * 0.95f)].ToString("F2") + "  p99 " + ms[(int)(ms.Count * 0.99f)].ToString("F2")
                   + "  최대 " + ms[ms.Count - 1].ToString("F1") + "  33ms초과 " + over33 + "/" + ms.Count
                   + "  | 프레임당 할당 평균 " + (asum / al.Count) + "B p95 " + al[(int)(al.Count * 0.95f)] + "B  GC " + (GC.CollectionCount(0) - gcStart) + "회";
        }

        static string MemSnapshot()
        {
            int go = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            int tex = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int mat = Resources.FindObjectsOfTypeAll<Material>().Length;
            int mesh = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            long mono = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            return "GO " + go + " Tex " + tex + " Mat " + mat + " Mesh " + mesh + " Mono " + mono + "MB";
        }

        // ================= 상점 (번 코인 쓰기) =================

        static readonly int[] UpgradePriority = { 3, 2, 0, 5, 1, 4 };           // 요금, 팁, 목숨, 자가 수리, 범퍼, 시간

        void DoShop()
        {
            var up = g.upgrades;
            var pr = g.projects;
            for (int guard = 0; guard < 60; guard++)
            {
                bool bought = false;
                for (int i = 0; i < pr.Count; i++)
                {
                    if (pr.Done(i)) continue;
                    if (pr.Unlocked(i) && up.Coins >= ProjectManager.Defs[i].cost && pr.Buy(i, up))
                    {
                        g.ApplyProjectPerks();
                        buyLog.Add("Day " + day + ": 개조 [" + ProjectManager.Defs[i].name + "] " + ProjectManager.Defs[i].cost);
                        bought = true;
                    }
                    break;                                                 // 앞 단계를 끝내야 다음이 열린다
                }
                if (!bought)
                {
                    foreach (int id in UpgradePriority)
                    {
                        if (up.IsMax(id)) continue;
                        int cost = up.Cost(id);
                        if (up.Coins >= cost && up.TryBuy(id))
                        {
                            buyLog.Add("Day " + day + ": 업그레이드 [" + UpgradeManager.Defs[id].name + "] Lv" + up.Level(id) + " " + cost);
                            bought = true;
                            break;
                        }
                    }
                }
                if (!bought) break;
            }
        }

        // ================= 로그/보고서 =================

        void OnLog(string msg, string stack, LogType type)
        {
            string key = msg.Length > 160 ? msg.Substring(0, 160) : msg;
            int nl = key.IndexOf('\n');
            if (nl > 0) key = key.Substring(0, nl);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errorTotal++;
                Add(errors, key);
                if (!errorStack.ContainsKey(key))
                    errorStack[key] = "day " + day + " t=" + (g != null ? g.elapsed.ToString("F0") : "?") + "s\n" + (stack.Length > 700 ? stack.Substring(0, 700) : stack);
            }
            else if (type == LogType.Warning)
            {
                Add(warns, key);
            }
        }

        void WriteReport(bool final)
        {
            var sb = new StringBuilder(rep.ToString());
            foreach (var d in dayLines) { sb.AppendLine(d); }
            sb.AppendLine("##### 요약 #####");
            if (allCauses.Count > 0) sb.AppendLine("전체 사고 원인: " + Dump(allCauses));
            sb.AppendLine("전체 팝업 상위: " + Top(allPopups, 30));
            sb.AppendLine("구매 기록:");
            foreach (var b in buyLog) sb.AppendLine("  " + b);
            sb.AppendLine(SummaryLine());
            sb.AppendLine("보유 코인 " + g.upgrades.Coins + "  개조 " + g.projects.DoneCount + "/" + g.projects.Count);
            sb.AppendLine("오류/예외 " + errorTotal + "건 (종류 " + errors.Count + ")");
            foreach (var kv in errors) sb.AppendLine("  x" + kv.Value + "  " + kv.Key + "\n      " + errorStack[kv.Key].Replace("\n", "\n      "));
            sb.AppendLine("경고 종류 " + warns.Count + ": " + Top(warns, 12));
            sb.AppendLine("실제 걸린 시간 " + wall.Elapsed.TotalSeconds.ToString("F0") + "초, 프레임 " + frame + (final ? "  (끝)" : "  (진행 중)"));
            try { File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true)); }
            catch (Exception e) { Debug.LogWarning("[SimBot] 보고서 저장 실패: " + e.Message); }
        }

        void Finish(int code)
        {
            if (finished) return;
            finished = true;
            phase = Phase.Done;
            Application.logMessageReceived -= OnLog;
            Time.captureFramerate = 0;
            WriteReport(true);
            Debug.Log("[SimBot] 끝. 보고서: " + outPath);
            BotExitCode = code;
            BotFinished = true;
        }
    }
}
#else
public partial class DriveGame
{
    void BotInit() { }
    void BotUpdate() { }
    void BotOnPopup(string text) { }
}
#endif
