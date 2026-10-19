using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 구간별 걸린 시간 기록 (성능 점검용). 켜져 있을 때(-simbot, -uiaudit, -uishots)만 기록하고, 꺼져 있으면 시간만 돌려주고 끝낸다.
// 쓰는 법: long t = Now(); ...일...; t = Sec("이름", t);   보고서는 SectionReport() (봇 보고서에 붙는다)
public partial class DriveGame
{
    class SecStat { public float total, max; public int calls; public int over5; public long alloc; }

    readonly Dictionary<string, SecStat> secStats = new Dictionary<string, SecStat>();
    bool secOn;

    long allocMark;

    static long Now() { return System.Diagnostics.Stopwatch.GetTimestamp(); }
    static long AllocNow() { return UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); }
    void MarkAlloc() { if (secOn) allocMark = AllocNow(); }

    long Sec(string name, long since)
    {
        long now = Now();
        if (secOn)
        {
            float ms = (now - since) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            SecStat st;
            if (!secStats.TryGetValue(name, out st)) secStats[name] = st = new SecStat();
            st.total += ms; st.calls++;
            long a = AllocNow();
            if (a > allocMark) st.alloc += a - allocMark;      // 줄어들면 쓰레기 수거(GC)가 있었던 것이라 센다 않는다
            allocMark = a;
            if (ms > st.max) st.max = ms;
            if (ms > 5f) st.over5++;
        }
        return now;
    }

    // 평균과 최대가 큰 순서대로 (ms)
    public string SectionReport()
    {
        if (secStats.Count == 0) return "";
        var list = new List<KeyValuePair<string, SecStat>>(secStats);
        list.Sort((a, b) => b.Value.total.CompareTo(a.Value.total));
        var sb = new StringBuilder("구간별 시간(ms): 이름  평균 / 최대 / 5ms 넘은 횟수 / 호출 수 / 호출당 할당(B)\n");
        foreach (var kv in list)
        {
            var s = kv.Value;
            sb.AppendLine("  " + kv.Key.PadRight(14) + (s.total / Mathf.Max(1, s.calls)).ToString("0.00") + " / " + s.max.ToString("0.0") + " / " + s.over5 + " / " + s.calls + " / " + (s.alloc / Mathf.Max(1, s.calls)));
        }
        return sb.ToString();
    }
}
