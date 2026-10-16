using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 목적지 안내: 목적지를 앞/옆 칸수로 바꾸고 안내 문장을 만든다
public partial class DriveGame
{
    int destX, destY;                    // 목적지 교차로의 격자 좌표 (AcceptPassenger 에서 정함)

    int hintF = int.MinValue, hintR;     // 안내 글자 캐시 키 (f,r 이 바뀔 때만 문자열을 다시 만든다)

    string hintText = "";

    bool hintNear;

    // 목적지를 "다음 교차로 N 기준" 앞 f칸 / 오른쪽 r칸(왼쪽은 음수)으로 바꾼다. sd 는 목적지 줄(r==0)에서 차→목적지 앞쪽 거리(m).
    // (gx,gy)는 방금 지난 교차로, N = (gx,gy)+진행방향. 회전 중이면 회전이 끝난 뒤의 상태로 계산해 CompleteTurn 때 값이 튀지 않게 한다.
    void NavState(out int bx, out int by, out int h, out float zE)
    {
        float period = Period;
        bx = gx;
        by = gy;
        h = heading;
        zE = nextInterDist;
        if (turning)
        {
            bx += HeadX[h];
            by += HeadY[h];
            h = (h + turnDir + 4) % 4;
            zE = Mathf.Max(0f, turnCenterRoad - (dist + 8f)) + period;   // 회전하는 교차로 중심까지 + 한 칸
        }
    }

    void DestRel(out int f, out int r, out float sd)
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
        string turn = r < 0 ? "좌회전" : "우회전";                  // r==0 이면 돌아가는 길은 우회전
        string side = r < 0 ? "왼쪽 " : "오른쪽 ";
        int m = Mathf.Abs(r);
        if (near) return "목적지 교차로예요 — 브레이크로 정차하세요";
        if (f < 0) return "목적지가 뒤쪽이에요 — 다음 교차로에서 " + turn + "해 돌아가세요";
        if (r == 0) return f == 0 ? "목적지가 바로 앞 교차로예요" : "목적지: 앞 " + (f + 1) + "칸 — 직진";
        if (f == 0) return "목적지: 앞 1칸, " + side + m + "칸 — 다음 교차로에서 " + turn;
        return "목적지: 앞 " + (f + 1) + "칸, " + side + m + "칸 — 교차로 " + f + "개 지난 뒤 다음 교차로에서 " + turn;
    }
}
