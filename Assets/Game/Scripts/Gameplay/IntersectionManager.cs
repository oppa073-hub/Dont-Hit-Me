using UnityEngine;

// 교차로 매니저: 깜빡이, 교차로 회전(시간 기준 90도), 격자 좌표(gx, gy)와 진행 방향(heading) 추적을 맡는다.
// 차를 직접 움직이지 않는다. 회전이 끝나 도로 좌표를 옮겨야 할 때는 TurnCompleted 이벤트로 새 도로 좌표를 알려 주고,
// 장면 교체 순간은 TurnSwapped 이벤트로, 팝업은 PopupRequested 로 알린다. 설정 값은 DriveGame 의 public 필드를 읽는다.
public class IntersectionManager
{
    readonly DriveGame cfg;

    int lastKNext = int.MinValue;
    float turnT;
    float retargetFadeT = -1f;

    // ---- 상태 (읽기는 누구나, 쓰기는 이 클래스만) ----
    public int Blinker { get; private set; }            // -1 좌, 0 없음, +1 우
    public int Heading { get; private set; }            // 진행 방향 (0 북, 1 동, 2 남, 3 서)
    public int Gx { get; private set; }                 // (Gx, Gy) = 방금 지난 교차로의 격자 좌표
    public int Gy { get; private set; }
    public float NextInterDist { get; private set; }    // 다음 교차로 중심까지 남은 거리(m)
    public bool Turning { get; private set; }           // 교차로 중심에서 제자리 회전 중
    public bool Retargeted { get; private set; }        // 회전 중 새 도로 구간으로 이미 교체했는지
    public int TurnDir { get; private set; }           // -1 좌회전, +1 우회전
    public int TurnInterIdx { get; private set; }      // 회전하는 교차로의 첫 구간 번호
    public float TurnAlpha { get; private set; }       // 회전한 각도 0~90
    public float TurnCenterRoad { get; private set; }  // 회전하는 교차로 중심의 도로 좌표
    public float Fade { get; private set; }            // 장면 교체 때 잠깐 어두워지는 정도 0~1

    // ---- 게임에 알리는 이벤트 ----
    public event System.Action<string, Color> PopupRequested;
    /// <summary>회전 도중 옛 도로를 새 도로 구간으로 교체할 때. 받는 쪽은 도로/건물 구간 캐시를 비운다.</summary>
    public event System.Action TurnSwapped;
    /// <summary>회전이 끝났다. 인자는 새 도로 기준 차의 달린 거리(dist). 받는 쪽은 dist 를 옮기고 차/사람/구간을 정리한다.</summary>
    public event System.Action<float> TurnCompleted;

    public IntersectionManager(DriveGame cfg)
    {
        this.cfg = cfg;
    }

    void Popup(string text, Color c)
    {
        if (PopupRequested != null) PopupRequested(text, c);
    }

    // ---- 도로 구조 ----

    int E { get { return Mathf.Max(cfg.intersectionLen + 1, cfg.intersectionEvery); } }       // 교차로 한 칸이 차지하는 구간 수

    /// <summary>교차로 간격(m).</summary>
    public float Period { get { return E * DriveGame.StripLen; } }

    public bool IsIntersection(int idx)
    {
        int e = E;
        int m = idx % e;
        if (m < 0) m += e;
        return m < cfg.intersectionLen;
    }

    /// <summary>달린 거리 d 에서 앞으로 만날 교차로 번호. 교차로 갱신과 건물 해시가 같은 식을 쓰도록 한 곳에 둔다.</summary>
    public int KNextAt(float d)
    {
        float len = cfg.intersectionLen * DriveGame.StripLen;
        return Mathf.CeilToInt(((d + 8f) - len * 0.5f) / Period);
    }

    /// <summary>
    /// 안내/내비 계산용 기준 상태. (bx, by) = 방금 지난 교차로, h = 진행 방향, zE = 다음 교차로 N 까지 남은 거리.
    /// 회전 중이면 회전이 끝난 뒤의 상태(그 교차로, 새 방향)로 계산해 회전이 끝날 때 값이 튀지 않게 한다.
    /// </summary>
    public void NavState(float dist, out int bx, out int by, out int h, out float zE)
    {
        bx = Gx;
        by = Gy;
        h = Heading;
        zE = NextInterDist;
        if (Turning)
        {
            bx += DriveGame.HeadX[h];
            by += DriveGame.HeadY[h];
            h = (h + TurnDir + 4) % 4;
            zE = Mathf.Max(0f, TurnCenterRoad - (dist + 8f)) + Period;   // 회전하는 교차로 중심까지 + 한 칸
        }
    }

    // ---- 조작 ----

    /// <summary>판을 새로 시작.</summary>
    public void Reset()
    {
        Blinker = 0;
        Heading = 0;
        Gx = 0;
        Gy = 0;
        Turning = false;
        Retargeted = false;
        lastKNext = int.MinValue;
        TurnAlpha = 0f;
        retargetFadeT = -1f;
        Fade = 0f;
    }

    /// <summary>깜빡이 Q(-1)/E(+1). 같은 쪽을 다시 누르면 해제. 회전 중에는 바꿀 수 없다.</summary>
    public void ToggleBlinker(int dir)
    {
        if (Turning) return;
        Blinker = Blinker == dir ? 0 : dir;
    }

    /// <summary>게임오버 화면에 어두운 막이 남지 않게 한다 (회전 장면 교체 중에 끝났을 때).</summary>
    public void ClearFade()
    {
        Fade = 0f;
    }

    // ---- 진행 ----

    /// <summary>
    /// 한 프레임 진행. dist = 차가 달린 거리, speed = 차 속도, talking = 손님과 대화 중(이때는 회전을 시작하지 않는다).
    /// 회전: 깜빡이를 켜고 교차로 중심에 도착하면(turnCenterTolerance 안) 그 자리에서 turnDuration 동안 자동으로 돈다.
    /// </summary>
    public void Tick(float dt, float dist, float speed, bool talking)
    {
        int e = E;
        float period = e * DriveGame.StripLen;
        float len = cfg.intersectionLen * DriveGame.StripLen;
        float roadPos = dist + 8f;                                   // 카메라(z=0)가 놓인 도로 좌표
        int kNext = KNextAt(dist);                                   // 앞으로 만날 교차로 번호
        float zC = kNext * period + len * 0.5f - roadPos;             // 그 교차로 중심까지 남은 거리
        NextInterDist = zC;

        if (!Turning)
        {
            if (kNext != lastKNext)
            {
                // 교차로를 막 지났다
                if (lastKNext != int.MinValue)
                {
                    Gx += DriveGame.HeadX[Heading];
                    Gy += DriveGame.HeadY[Heading];
                    if (Blinker != 0)
                    {
                        Popup("깜빡이를 더 일찍 켜세요", new Color(1f, 0.7f, 0.4f));
                        Blinker = 0;
                    }
                }
                lastKNext = kNext;
            }

            // 프레임이 튀어도 중심을 건너뛰지 않도록, 이번 프레임에 달리는 거리의 2배까지는 시작 범위로 본다
            float startWindow = Mathf.Max(cfg.turnCenterTolerance, speed * dt * 2f);
            if (Blinker != 0 && zC <= startWindow && zC > 0f && !talking)
            {
                Turning = true;
                turnT = 0f;
                Retargeted = false;
                retargetFadeT = -1f;
                TurnDir = Blinker;
                TurnCenterRoad = roadPos + zC;
                TurnInterIdx = kNext * e;                            // 이 교차로의 첫 구간 번호
                TurnAlpha = 0f;
                Blinker = 0;
                Popup(TurnDir < 0 ? "좌회전" : "우회전", new Color(0.6f, 0.9f, 1f));
            }
            return;
        }

        // 회전 진행: 차가 교차로 중심에 도착한 뒤, turnDuration 동안 시간에 맞춰 0→90도 돈다
        turnT += dt;
        float p = Mathf.Clamp01(turnT / Mathf.Max(0.1f, cfg.turnDuration));
        TurnAlpha = 90f * (p * p * (3f - 2f * p));

        // 장면 교체는 짧게 어두워지는 순간에 한다 (옛 도로의 가까운 건물과 새 도로의 건물이 겹치는 부분을 가린다)
        if (!Retargeted && retargetFadeT == -1f && TurnAlpha >= cfg.turnSwapAngle) retargetFadeT = 0f;
        if (retargetFadeT >= 0f)
        {
            float fadeTime = Mathf.Max(0.05f, cfg.swapFadeTime);
            retargetFadeT += Time.unscaledDeltaTime;
            Fade = cfg.swapFadeDarkness * Mathf.Sin(Mathf.PI * Mathf.Clamp01(retargetFadeT / fadeTime));
            if (!Retargeted && retargetFadeT >= fadeTime * 0.5f)
            {
                Retargeted = true;
                if (TurnSwapped != null) TurnSwapped();              // 새 도로 구간으로 다시 구성
            }
            if (retargetFadeT >= fadeTime)
            {
                retargetFadeT = -2f;
                Fade = 0f;
            }
        }

        if (p >= 1f) CompleteTurn();
    }

    // 회전이 끝나는 순간: 새 도로가 앞으로 이어지도록 좌표를 옮기고, 방향을 바꾼다.
    void CompleteTurn()
    {
        int e = E;
        float period = e * DriveGame.StripLen;
        float len = cfg.intersectionLen * DriveGame.StripLen;

        Gx += DriveGame.HeadX[Heading];                              // 이 교차로에 도착
        Gy += DriveGame.HeadY[Heading];
        Heading = (Heading + TurnDir + 4) % 4;

        // 새 도로의 교차로 중심 = RebaseBlocks 번째 교차로 (장면 교체 때 새 도로 구간에 쓴 번호와 같다)
        float rebaseRoad = e * DriveGame.RebaseBlocks * DriveGame.StripLen;
        // 중심 '직후'에 놓는다: 그러면 다음 교차로가 곧바로 kNext 가 되어, (Gx,Gy)는 항상 "방금 지난 교차로"다
        float newDist = rebaseRoad + len * 0.5f + 0.01f - 8f;

        Turning = false;
        Retargeted = false;
        TurnAlpha = 0f;
        retargetFadeT = -1f;
        Fade = 0f;
        lastKNext = Mathf.CeilToInt(((newDist + 8f) - len * 0.5f) / period);
        NextInterDist = period - 0.01f;                              // 다음 프레임에 낡은 값(≈0)을 쓰지 않게 새 좌표 기준으로 맞춘다

        if (TurnCompleted != null) TurnCompleted(newDist);
    }
}
