using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 진행 기록 연결부: 목숨/점수/무적/게임오버는 GameSession 이 하고, 여기서는 짧은 이름의 읽기 전용 속성으로 보여 준다.
public partial class DriveGame
{
    GameSession session;

    int lives { get { return session.Lives; } }
    int score { get { return session.Score; } }
    int nearMisses { get { return session.NearMisses; } }
    int hits { get { return session.Hits; } }
    float distScore { get { return session.DistScore; } }
    float invuln { get { return session.Invuln; } }
    float hurtFlash { get { return session.HurtFlash; } }
    bool gameOver { get { return session.GameOver; } }
    int maxLivesNow { get { return session.MaxLives; } }

    void CreateSession()
    {
        session = new GameSession(this);
    }
}
