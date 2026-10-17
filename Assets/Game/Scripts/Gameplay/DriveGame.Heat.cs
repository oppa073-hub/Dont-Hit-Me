using System.Collections.Generic;
using UnityEngine;

// 도시 위험도(Heat): Day 번호가 오를수록 0~5단계로 올라가서, 업그레이드로 쉬워진 게임을 다시 팽팽하게 만든다.
// 단계마다: 판을 시작하는 레벨 상승(차/장애물이 일찍 나옴), 동시에 건너는 사람 수 상한 증가, 랜덤 인카운트 간격 단축,
// 사건이 있는 날(괴수/히어로/좀비) 확률 증가. 대신 요금에 위험수당이 붙는다. 값은 인스펙터 Heat 헤더.
public partial class DriveGame
{
    int heat;

    int HeatFor(int day)
    {
        int h = 0;
        for (int i = 0; i < heatDays.Length; i++) if (day >= heatDays[i]) h = i + 1;
        return Mathf.Clamp(h, 0, 5);
    }

    // 판을 시작할 때 맨 먼저
    void UpdateHeat()
    {
        if (dayEvents == null) return;
        heat = HeatFor(dayEvents.Day);
        difficulty.Heat = heat;
        dayEvents.Heat = heat;
        if (enc != null) enc.IntervalMul = Mathf.Max(0.4f, 1f - heatEncounterCut * heat);
    }

    static string HeatStars(int h)
    {
        h = Mathf.Clamp(h, 0, 5);
        return new string('\u2605', h) + new string('\u2606', 5 - h);
    }
}
