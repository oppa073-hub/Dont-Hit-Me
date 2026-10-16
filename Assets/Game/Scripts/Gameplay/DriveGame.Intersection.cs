using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 교차로 연결부: 깜빡이/회전/격자 추적은 IntersectionManager 가 하고, 여기서는 그 상태를 읽기 전용으로 보여 주고
// 회전이 끝났을 때 차/사람/도로 구간을 정리한다.
public partial class DriveGame
{
    IntersectionManager road;

    float worldMove;                     // 이번 프레임에 월드가 실제로 다가온 거리(m). 회전 중에는 0 에 가깝다

    public static readonly int[] HeadX = { 0, 1, 0, -1 };

    public static readonly int[] HeadY = { 1, 0, -1, 0 };

    static readonly string[] HeadNames = { "북", "동", "남", "서" };

    // 매니저 상태를 짧은 이름으로 읽는다 (읽기 전용: 바꾸려면 매니저의 메서드를 쓴다)
    int blinker { get { return road.Blinker; } }                 // -1 좌, 0 없음, +1 우
    int heading { get { return road.Heading; } }                 // 진행 방향 (0 북, 1 동, 2 남, 3 서)
    int gx { get { return road.Gx; } }                           // (gx,gy) = 방금 지난 교차로
    int gy { get { return road.Gy; } }
    float nextInterDist { get { return road.NextInterDist; } }
    bool turning { get { return road.Turning; } }
    bool retargeted { get { return road.Retargeted; } }
    int turnDir { get { return road.TurnDir; } }
    int turnInterIdx { get { return road.TurnInterIdx; } }
    float turnAlpha { get { return road.TurnAlpha; } }
    float turnCenterRoad { get { return road.TurnCenterRoad; } }
    float fade { get { return road.Fade; } }
    float Period { get { return road.Period; } }                 // 교차로 간격(m)

    bool IsIntersection(int idx) { return road.IsIntersection(idx); }

    int KNextAt(float d) { return road.KNextAt(d); }

    void CreateRoad()
    {
        road = new IntersectionManager(this);
        road.PopupRequested += Popup;
        road.TurnSwapped += OnTurnSwapped;
        road.TurnCompleted += OnTurnCompleted;
    }

    void UpdateIntersection(float dt)
    {
        road.Tick(dt, dist, speed, taxi.State == TaxiState.Talking);
    }

    // 회전 도중 새 도로 구간으로 교체: 도로/건물 구간을 새로 구성한다
    void OnTurnSwapped()
    {
        foreach (var s in strips) s.lastIdx = int.MinValue;
    }

    // 회전이 끝났다: 새 도로 좌표로 옮기고, 차를 가운데로 놓고, 사람들을 새 도로 기준으로 다시 깐다
    void OnTurnCompleted(float newDist)
    {
        dist = newDist;
        carX = lateralVel = 0f;
        foreach (var s in strips) s.lastIdx = int.MinValue;
        session.ExtendInvuln(postTurnInvuln);

        // 사람들은 새 도로 기준으로 다시 깐다 (가까운 곳은 비워서 멀리서부터 자연스럽게 나타나게)
        taxi.CallerLost();
        peds.Respawn(SpawnZ);
        traffic.Clear();
    }
}
