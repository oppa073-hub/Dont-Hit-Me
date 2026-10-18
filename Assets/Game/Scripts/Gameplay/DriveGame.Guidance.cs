using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 목적지 안내: 목적지를 앞/옆 칸수로 바꾸고 안내 문장을 만든다
public partial class DriveGame
{
    int hintF = int.MinValue, hintR;     // 안내 글자 캐시 키 (f,r 이 바뀔 때만 문자열을 다시 만든다)

    string hintText = "";

    bool hintNear;

    // 안내 기준 상태 (회전 중이면 회전이 끝난 뒤의 상태). 계산은 IntersectionManager 가 한다.
    void NavState(out int bx, out int by, out int h, out float zE)
    {
        road.NavState(dist, out bx, out by, out h, out zE);
    }

    // 목적지를 "다음 교차로 N 기준" 앞 f칸 / 오른쪽 r칸(왼쪽은 음수)으로 바꾼다. sd 는 목적지 줄(r==0)에서 차→목적지 앞쪽 거리(m).
    void DestRel(out int f, out int r, out float sd)
    {
        DestRel(taxi.DestX, taxi.DestY, out f, out r, out sd);      // 지금 태운 손님의 목적지
    }

    // 주어진 격자 좌표(destX, destY)에 대해 계산한다. TaxiManager 가 도착/지나침 판정에 이 함수를 빌려 쓴다.
    void DestRel(int destX, int destY, out int f, out int r, out float sd)
    {
        NavState(out int bx, out int by, out int h, out float zE);
        float period = Period;
        int dx = destX - (bx + HeadX[h]);
        int dy = destY - (by + HeadY[h]);
        int rh = (h + 1) & 3;                                        // 진행 방향의 오른쪽
        f = dx * HeadX[h] + dy * HeadY[h];
        r = dx * HeadX[rh] + dy * HeadY[rh];
        sd = zE + f * period;
    }

    // near: 목적지 줄 위에서 도착 반경 안(브레이크로 정차하면 도착되는 곳)
    string NavHint(int f, int r, bool near)
    {
        bool ko = Loc.Ko;
        string turn = ko ? (r < 0 ? "좌회전" : "우회전") : (r < 0 ? "left" : "right");      // r==0 이면 돌아가는 길은 우회전
        string side = ko ? (r < 0 ? "왼쪽 " : "오른쪽 ") : (r < 0 ? "left " : "right ");
        int m = Mathf.Abs(r);
        if (near) return ko ? "목적지 교차로예요 — 브레이크로 정차하세요" : "This is your destination — stop with the brake";
        if (f < 0) return ko ? "목적지가 뒤쪽이에요 — 다음 교차로에서 " + turn + "해 돌아가세요" : "Your destination is behind you — turn " + turn + " at the next intersection";
        return "";                                              // "목적지: 앞 몇 칸…" 안내는 화면을 가려서 뺐다 (내비 화면과 도착 직전 안내만 남긴다)
        if (r == 0) return f == 0 ? (ko ? "목적지가 바로 앞 교차로예요" : "Your destination is the next intersection") : (ko ? "목적지: 앞 " + (f + 1) + "칸 — 직진" : "Destination: " + (f + 1) + " blocks ahead — go straight");
        if (f == 0) return ko ? "목적지: 앞 1칸, " + side + m + "칸 — 다음 교차로에서 " + turn : "Destination: 1 block ahead, " + m + " blocks " + side + "— " + turn + " at the next intersection";
        return ko ? "목적지: 앞 " + (f + 1) + "칸, " + side + m + "칸 — 교차로 " + f + "개 지난 뒤 다음 교차로에서 " + turn : "Destination: " + (f + 1) + " blocks ahead, " + m + " blocks " + side + "— " + turn + " after " + f + " more intersection" + (f == 1 ? "" : "s");
    }
}
