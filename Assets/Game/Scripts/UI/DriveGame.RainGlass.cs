using System.Collections.Generic;
using UnityEngine;

// 비 오는 날의 앞유리: 물방울이 하나둘 맺혀 시야를 가리고, 와이퍼가 일정 시간마다 한 번씩 쓸어 닦는다.
// 빗줄기와 물방울은 "앞유리 영역" 안에서만 그린다 (기둥, 천장, 계기판 위에는 비가 내리지 않게).
// 앞유리 모양은 3D 운전석 부품(A필러, 천장, 대시보드 끝선)의 화면 위치로 매 프레임 계산한다.
public partial class DriveGame
{
    class GlassDrop { public float x, y, d, born, wipeAt; }

    readonly List<GlassDrop> drops = new List<GlassDrop>();
    float dropAcc, wiperTimer = 3f, wiperSweepT = -1f, wiperLastT;
    float gTop, gBot, gL0, gL1, gR0, gR1;          // 앞유리: 위/아래 y, 위쪽의 왼/오른 x(0), 아래쪽의 왼/오른 x(1) (IMGUI 화면 좌표)
    bool glassOk;

    Vector2 GlassPoint(float H, Vector3 local)
    {
        var s = cam.WorldToScreenPoint(viewRoot.TransformPoint(local));
        return new Vector2(s.x, H - s.y);
    }

    // 앞유리 모양을 구한다. 운전석이 안 보이는 장면(옆 시점)이면 glassOk = false
    void ComputeGlass(float W, float H, float u)
    {
        glassOk = false;
        if (!cabinVisible || viewRoot == null || cam == null) return;
        float m = 14f * u;                                          // 기둥 두께만큼 안쪽으로
        var lb = GlassPoint(H, pillarLBase); var lt = GlassPoint(H, pillarLTop);
        var rb = GlassPoint(H, pillarRBase); var rt = GlassPoint(H, pillarRTop);
        gTop = Mathf.Clamp(GlassPoint(H, new Vector3(0f, roofY, 0.6f)).y + 6f * u, 0f, H);
        gBot = Mathf.Clamp(GlassPoint(H, new Vector3(0f, dashTopY, 1.37f)).y, gTop + 20f, H);
        // 기둥은 위로 갈수록 안쪽으로 기울어 있으니, 유리의 위/아래 y 에서의 x 를 기둥 선 위에서 구한다
        float dl = lb.y - lt.y, dr = rb.y - rt.y;
        float aL = Mathf.Abs(dl) > 1f ? (gTop - lt.y) / dl : 0f, bL = Mathf.Abs(dl) > 1f ? (gBot - lt.y) / dl : 1f;
        float aR = Mathf.Abs(dr) > 1f ? (gTop - rt.y) / dr : 0f, bR = Mathf.Abs(dr) > 1f ? (gBot - rt.y) / dr : 1f;
        gL0 = Mathf.LerpUnclamped(lt.x, lb.x, aL) + m; gL1 = Mathf.LerpUnclamped(lt.x, lb.x, bL) + m;
        gR0 = Mathf.LerpUnclamped(rt.x, rb.x, aR) - m; gR1 = Mathf.LerpUnclamped(rt.x, rb.x, bR) - m;
        glassOk = gR0 > gL0 + 50f && gR1 > gL1 + 50f;
    }

    bool InGlass(float x, float y)
    {
        if (!glassOk) return true;
        if (y < gTop || y > gBot) return false;
        float t = (y - gTop) / Mathf.Max(1f, gBot - gTop);
        return x > Mathf.Lerp(gL0, gL1, t) && x < Mathf.Lerp(gR0, gR1, t);
    }

    Vector2 RandomGlassPoint(float W)
    {
        for (int i = 0; i < 12; i++)
        {
            float y = Random.Range(gTop, gBot);
            float t = (y - gTop) / Mathf.Max(1f, gBot - gTop);
            float x = Random.Range(Mathf.Lerp(gL0, gL1, t), Mathf.Lerp(gR0, gR1, t));
            if (InGlass(x, y)) return new Vector2(x, y);
        }
        return new Vector2(W * 0.5f, (gTop + gBot) * 0.5f);
    }

    // 와이퍼가 한 번 쓸고 가는 진행도 (0 접힌 자리 ~ 1 끝까지 올라감 ~ 다시 0)
    float WiperSweep01()
    {
        if (wiperSweepT < 0f) return 0f;
        float p = Mathf.Clamp01(wiperSweepT / Mathf.Max(0.4f, rainWipeSeconds));
        float s = p < 0.5f ? p * 2f : (1f - p) * 2f;
        return s * s * (3f - 2f * s);
    }

    // 그리기 이벤트마다 한 번: 물방울 생기기/흘러내리기, 와이퍼 주기
    void UpdateRainGlass(float W, float H, float u)
    {
        float dt = Mathf.Clamp(Time.unscaledTime - wiperLastT, 0f, 0.1f);
        wiperLastT = Time.unscaledTime;
        if (flow != GameFlow.Playing || gameOver || !glassOk) return;
        float rain = weather.Rain;

        if (rain > 0.15f)
        {
            dropAcc += dt * rainDropsPerSec * rain;
            while (dropAcc >= 1f)
            {
                dropAcc -= 1f;
                if (drops.Count >= rainDropLimit) break;
                var p = RandomGlassPoint(W);
                drops.Add(new GlassDrop { x = p.x, y = p.y, d = Random.Range(0.35f, 1f), born = Time.unscaledTime });
            }
        }

        // 큰 물방울은 천천히 흘러내린다
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            if (d.d > 0.7f) d.y += (d.d - 0.7f) * 60f * u * dt;
        }
        drops.RemoveAll(d => d.y > gBot);

        // 와이퍼: 일정 시간마다 한 번 쓸어서 닦는다. 비가 세면 더 자주. 비가 그치고 물방울이 남았으면 한 번 더
        if (wiperSweepT < 0f)
        {
            if (rain <= 0.02f && drops.Count > 0) wiperTimer = Mathf.Min(wiperTimer, 1.5f);
            if (rain > 0.15f || drops.Count > 0) wiperTimer -= dt;
            if (wiperTimer <= 0f)
            {
                wiperSweepT = 0f;
                sound.PlayWipe();
                for (int i = 0; i < drops.Count; i++) drops[i].wipeAt = Random.Range(0.08f, 0.5f);     // 올라가는 동안 차례로 닦인다
            }
        }
        else
        {
            wiperSweepT += dt;
            float p = wiperSweepT / Mathf.Max(0.4f, rainWipeSeconds);
            drops.RemoveAll(d => d.wipeAt > 0f && d.wipeAt <= p);
            if (p >= 1f)
            {
                wiperSweepT = -1f;
                wiperTimer = Mathf.Lerp(rainWipeInterval * 1.4f, rainWipeInterval * 0.6f, Mathf.Clamp01(rain));
            }
        }
    }

    void DrawRainDrops(float u)
    {
        if (!glassOk || drops.Count == 0) return;
        // 물방울이 많이 맺힐수록 앞유리가 뿌옇게 흐려진다 (유리 모양을 따라 가로 띠로 칠해서 기둥 위로는 안 번진다)
        float fog = 0.42f * Mathf.Pow(Mathf.Clamp01(drops.Count / (float)Mathf.Max(1, rainDropLimit)), 1.3f);
        if (fog > 0.01f)
        {
            const int Rows = 14;
            for (int r = 0; r < Rows; r++)
            {
                float y0 = Mathf.Lerp(gTop, gBot, r / (float)Rows), y1 = Mathf.Lerp(gTop, gBot, (r + 1) / (float)Rows);
                float tm = (r + 0.5f) / Rows;
                float xl = Mathf.Lerp(gL0, gL1, tm), xr = Mathf.Lerp(gR0, gR1, tm);
                Fill(new Rect(xl, y0, xr - xl, y1 - y0 + 1f), new Color(0.78f, 0.84f, 0.9f, fog));
            }
        }
        float maxD = rainDropBig * u;
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            float age = Mathf.Clamp01((Time.unscaledTime - d.born) / 2.5f);              // 처음엔 작았다가 커진다
            float size = Mathf.Max(3f * u, maxD * d.d * Mathf.Lerp(0.4f, 1f, age));
            var c = new Vector2(d.x, d.y);
            Dot(c, size * 1.12f, new Color(0.04f, 0.06f, 0.1f, 0.35f));                      // 가장자리 그림자
            Dot(c, size, new Color(0.72f, 0.82f, 0.94f, 0.62f));                              // 물방울 몸통
            Dot(c + new Vector2(-size * 0.17f, -size * 0.2f), size * 0.38f, new Color(1f, 1f, 1f, 0.7f));      // 반짝임
        }
    }
}
