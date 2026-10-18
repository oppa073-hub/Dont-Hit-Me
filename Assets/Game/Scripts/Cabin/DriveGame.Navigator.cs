using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 대시보드 위 내비게이션 화면(지도, 목적지, 안내선)
public partial class DriveGame
{
    TextMesh clusterText, navHeadText, navInfoText;

    readonly List<Transform> navGridV = new List<Transform>(), navGridH = new List<Transform>(), navRoute = new List<Transform>();

    Material navRoadMat, navRoadHiMat, navRouteMat;

    int navKmh = -1, navHead = -1, navRem = -2;

    string navDestShown;

    // 내비의 내 차 표시 (위를 향한 화살촉). 양면으로 만들어 감기 방향과 상관없이 보이게 한다
    static Mesh ArrowMesh()
    {
        var m = new Mesh { name = "NavArrow" };
        m.vertices = new[] { new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0f, -0.2f, 0f), new Vector3(-0.5f, -0.5f, 0f) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void NavQuad(List<Transform> pool, int i, Vector3 pos, Vector2 size, Material mat)
    {
        if (i >= pool.Count) pool.Add(CabinPart("NavLine", PrimitiveType.Quad, navScreen, mat));
        var q = pool[i];
        if (!q.gameObject.activeSelf) q.gameObject.SetActive(true);
        q.localPosition = pos;
        q.localScale = new Vector3(size.x, size.y, 1f);
        var rd = q.GetComponent<Renderer>();
        if (rd.sharedMaterial != mat) rd.sharedMaterial = mat;
    }

    static void HideFrom(List<Transform> pool, int n)
    {
        for (int i = n; i < pool.Count; i++)
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
    }

    // 안내선 한 토막 (가로 또는 세로). 화면 밖은 잘라내고, 통째로 밖이면 그리지 않는다
    void NavSeg(ref int n, Vector2 a, Vector2 b, float th, float hw, float hh)
    {
        if (Mathf.Approximately(a.y, b.y)) { if (a.y < -hh || a.y > hh) return; }
        else if (a.x < -hw || a.x > hw) return;
        a.x = Mathf.Clamp(a.x, -hw, hw); b.x = Mathf.Clamp(b.x, -hw, hw);
        a.y = Mathf.Clamp(a.y, -hh, hh); b.y = Mathf.Clamp(b.y, -hh, hh);
        NavQuad(navRoute, n++, new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, -0.002f),
                new Vector2(Mathf.Abs(a.x - b.x) + th, Mathf.Abs(a.y - b.y) + th), navRouteMat);
    }

    // 내비 화면(진행 방향이 위쪽인 격자 지도, 내 차 화살표, 목적지, L자 안내선, 남은 거리)과 계기판 속도
    void UpdateNav()
    {
        int kmh = Mathf.RoundToInt(speed * 3.6f);
        if (kmh != navKmh)
        {
            navKmh = kmh;
            clusterText.text = kmh + " km/h";
        }

        // 운전을 시작한 직후에는 지도 대신 뉴스
        if (newsActive)
        {
            if (!newsShown) EnterNewsView();
            UpdateNewsView();
            return;
        }
        if (newsShown) ExitNewsView();

        float w = Mathf.Max(0.05f, navSize.x), hgt = Mathf.Max(0.04f, navSize.y);
        float hw = w * 0.5f, hh = hgt * 0.5f;
        NavState(out int bx, out int by, out int h, out float zE);
        float period = Period;
        float t = 1f - zE / period;                                  // 방금 지난 교차로에서 다음 교차로까지 몇 %
        float px = bx + HeadX[h] * t, py = by + HeadY[h] * t;
        int rh = (h + 1) & 3;
        float pf = px * HeadX[h] + py * HeadY[h];                    // 내 위치: 진행 방향 좌표
        float pr = px * HeadX[rh] + py * HeadY[rh];                  // 내 위치: 오른쪽 좌표
        float cs = hgt / (2f * Mathf.Max(1, miniMapRange) + 1f);     // 한 칸 크기(m)
        float carY = -hgt * 0.16f;                                   // 앞쪽이 더 많이 보이게 아래쪽에 둔다
        float lw = cs * 0.14f;

        // 격자 도로 (지금 달리는 도로는 밝게)
        int n = 0;
        int k0 = Mathf.RoundToInt(pr);
        for (int k = Mathf.FloorToInt(pr - hw / cs); k <= Mathf.CeilToInt(pr + hw / cs); k++)
        {
            float x = (k - pr) * cs;
            if (Mathf.Abs(x) > hw) continue;
            NavQuad(navGridV, n++, new Vector3(x, 0f, k == k0 ? -0.0012f : -0.001f), new Vector2(lw, hgt), k == k0 ? navRoadHiMat : navRoadMat);
        }
        HideFrom(navGridV, n);
        n = 0;
        for (int j = Mathf.FloorToInt(pf + (-hh - carY) / cs); j <= Mathf.CeilToInt(pf + (hh - carY) / cs); j++)
        {
            float y = carY + (j - pf) * cs;
            if (y < -hh || y > hh) continue;
            NavQuad(navGridH, n++, new Vector3(0f, y, -0.0011f), new Vector2(w, lw), navRoadMat);
        }
        HideFrom(navGridH, n);

        // 목적지와 L자 안내선 (정수 f,r 기준이라 f==0 경계에서 깜빡이지 않는다)
        n = 0;
        int rem = -1;
        bool carrying = taxi.State == TaxiState.Carrying || taxi.State == TaxiState.Waiting;
        if (carrying && !taxi.AlienTrip)
        {
            DestRel(out int f, out int r, out float sd);
            rem = Mathf.RoundToInt(Mathf.Abs(sd) + Mathf.Abs(r) * period);   // 상단 HUD 와 같은 값
            float nF = zE / period;                                  // 다음 교차로까지 앞쪽 칸 수
            float dx = r * cs, dyv = (nF + f) * cs;
            float gw = lw * 2f;
            var a = new Vector2(0f, carY);
            if (f >= 0)
            {
                var b = new Vector2(0f, carY + dyv);
                NavSeg(ref n, a, b, gw, hw, hh);
                NavSeg(ref n, b, new Vector2(dx, b.y), gw, hw, hh);
            }
            else
            {
                // 목적지가 뒤쪽: 다음 교차로까지 가서 옆으로 돌아 나온다 (r==0 이면 오른쪽으로 한 칸)
                float s = r != 0 ? r : 1f;
                var b = new Vector2(0f, carY + nF * cs);
                var c = new Vector2(s * cs, b.y);
                var d = new Vector2(c.x, carY + dyv);
                NavSeg(ref n, a, b, gw, hw, hh);
                NavSeg(ref n, b, c, gw, hw, hh);
                NavSeg(ref n, c, d, gw, hw, hh);
                NavSeg(ref n, d, new Vector2(dx, d.y), gw, hw, hh);
            }
            float m = cs * 0.35f;
            float barTop = -hh + hgt * 0.18f;                        // 아래 정보 띠 위쪽 (목적지가 띠 속에 숨지 않게)
            navDest.localPosition = new Vector3(Mathf.Clamp(dx, -hw + m, hw - m), Mathf.Clamp(carY + dyv, barTop + m, hh - m), -0.0025f);
            navDest.localRotation = Quaternion.Euler(0f, 0f, 45f);
            navDest.localScale = new Vector3(cs * 0.5f, cs * 0.5f, 1f);
        }
        HideFrom(navRoute, n);
        bool showDest = carrying && !taxi.AlienTrip;                 // 외계인 손님의 목적지는 지도에 안 나온다
        if (navDest.gameObject.activeSelf != showDest) navDest.gameObject.SetActive(showDest);

        // 길가에서 기다리는 손님: 내 앞 몇 m 에 있는지 분홍 마름모로 보여 준다 (안내선은 없음)
        int callerM = -1;
        bool callerRight = false;
        var cp = taxi.Caller;
        bool callerShown = !carrying && !turning && taxi.State == TaxiState.Cruise && cp != null && cp.t != null;
        if (callerShown)
        {
            float cy = carY + cp.z * cs / period;
            callerRight = cp.x >= 0f;
            float mc = cs * 0.3f;
            float barTop2 = -hh + hgt * 0.18f;
            float pulse = 1f + 0.2f * Mathf.Sin(Time.unscaledTime * 8f);
            navCaller.localPosition = new Vector3((callerRight ? 1f : -1f) * lw * 2.5f, Mathf.Clamp(cy, barTop2 + mc, hh - mc), -0.0026f);
            navCaller.localRotation = Quaternion.Euler(0f, 0f, 45f);
            navCaller.localScale = new Vector3(cs * 0.38f * pulse, cs * 0.38f * pulse, 1f);
            callerM = Mathf.Max(0, Mathf.RoundToInt(cp.z / 5f) * 5);
        }
        if (navCaller.gameObject.activeSelf != callerShown) navCaller.gameObject.SetActive(callerShown);

        navCar.localPosition = new Vector3(0f, carY, -0.003f);
        navCar.localScale = new Vector3(cs * 0.7f, cs * 0.8f, 1f);

        // 왼쪽 위: 다음 교차로까지 거리(5m 단위). 깜빡이를 켜면 "좌회전/우회전"으로 바뀜 (방향 글자는 뺐다)
        int interKey = Mathf.RoundToInt(nextInterDist / 5f) * 3 + (blinker + 1);
        if (interKey != navHead)
        {
            navHead = interKey;
            string label = blinker < 0 ? Loc.T("좌회전 ", "Left ") : (blinker > 0 ? Loc.T("우회전 ", "Right ") : Loc.T("교차로 ", "Next "));        // 깜빡이를 켜 두면 어느 쪽으로 돌 예정인지 보인다
            navHeadText.text = label + Mathf.RoundToInt(nextInterDist / 5f) * 5 + "m";
        }
        if (carrying && taxi.AlienTrip) rem = -77;
        int infoKey = carrying ? rem : (callerM >= 0 ? -10 - callerM * 2 - (callerRight ? 1 : 0) : -1);
        if (infoKey != navRem || (carrying && !ReferenceEquals(taxi.DestName, navDestShown)))
        {
            navRem = infoKey;
            navDestShown = taxi.DestName;
            if (carrying) navInfoText.text = taxi.DestName + (taxi.AlienTrip ? "  ???m" : "  " + rem + "m");
            else if (callerM >= 0) navInfoText.text = Loc.T("손님 " + (callerRight ? "오른쪽 " : "왼쪽 ") + callerM + "m 앞", "Pickup " + (callerRight ? "right " : "left ") + callerM + "m ahead");
            else navInfoText.text = Loc.T("손님 찾는 중", "Looking for a fare");
        }
    }
}
