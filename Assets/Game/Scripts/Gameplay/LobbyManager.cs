using UnityEngine;

// 로비 매니저: 2D 횡스크롤 로비에서 캐릭터의 걷기, NPC/택시와의 상호작용, 대사 진행을 맡는다.
// 화면에 그리는 건 모르고(DriveGame.Lobby.cs 가 그림), 무엇을 했는지는 이벤트로 알린다:
//  NpcInteract = NPC 에게 말을 걸었다, DialogueFinished = 대사가 모두 끝났다(태그와 함께), BoardRequested = 택시에 타서 화면이 어두워진 뒤 영업 시작.
public class LobbyManager
{
    public const float WorldW = 2200f;     // 로비 전체 폭 (화면 높이 720 기준 단위)
    public const float NpcX = 520f;
    public const float TaxiX = 1880f;
    const float Speed = 280f;
    const float TypeSpeed = 38f;           // 대사가 한 글자씩 나오는 속도 (글자/초)

    public float PlayerX { get; private set; }
    public int Facing { get; private set; }            // 1 = 오른쪽, -1 = 왼쪽
    public float WalkPhase { get; private set; }
    public bool Moving { get; private set; }
    public bool NearNpc { get { return NearNpcIdx >= 0; } }
    public int NearNpcIdx { get; private set; }               // 가장 가까운 npc 번호 (말 걸 수 있는 거리 안에 없으면 -1)
    public float[] NpcXs = { NpcX };                          // 말 걸 수 있는 npc 들의 위치 (0 = 사장). DriveGame 이 정해 준다
    public bool NearTaxi { get; private set; }
    public bool Boarding { get { return boardT >= 0f; } }       // 택시에 타는 중 (암전이 시작되기 전 한 프레임도 포함)

    // 대사
    public bool InDialogue { get; private set; }
    public string Shown { get; private set; }          // 지금까지 나온 글자
    public bool LineDone { get; private set; }
    public int DialogueTag { get; private set; }       // 대사를 시작한 쪽이 붙인 표시 (끝났을 때 돌려준다)
    string[] lines;
    int lineIdx;
    float charT;
    int shownCount;

    /// <summary>택시에 타는 중 어두워지는 정도 (0 ~ 1). 타는 중이 아니면 -1.</summary>
    public float BoardFade { get; private set; }
    float boardT = -1f;

    public event System.Action<int> NpcInteract;
    public event System.Action BoardRequested;
    public event System.Action<int> DialogueFinished;

    public LobbyManager()
    {
        Reset(1000f);
    }

    public void Reset(float startX)
    {
        PlayerX = startX;
        Facing = 1;
        WalkPhase = 0f;
        Moving = false;
        InDialogue = false;
        boardT = -1f;
        BoardFade = -1f;
        Shown = "";
        UpdateNear();
    }

    void UpdateNear()
    {
        NearNpcIdx = -1;
        float best = 85f;                                       // 이 거리 안에서 가장 가까운 사람
        for (int i = 0; i < NpcXs.Length; i++)
        {
            float d = Mathf.Abs(PlayerX - NpcXs[i]);
            if (d < best) { best = d; NearNpcIdx = i; }
        }
        NearTaxi = Mathf.Abs(PlayerX - TaxiX) < 150f;
    }

    /// <summary>대사 시작. tag 는 끝났을 때 DialogueFinished 로 돌아온다.</summary>
    public void StartDialogue(string[] dialogue, int tag)
    {
        lines = Loc.S(dialogue);                          // 지금 언어의 글로 (한국어‖English 묶음에서 고른다)
        Moving = false;
        lineIdx = 0;
        DialogueTag = tag;
        InDialogue = true;
        BeginLine();
    }

    void BeginLine()
    {
        charT = 0f;
        shownCount = 0;
        Shown = "";
        LineDone = lines[lineIdx].Length == 0;
    }

    /// <summary>한 프레임. dt = 실제 시간, move = -1/0/1 걷는 방향, interact = 상호작용 키를 방금 눌렀는가.</summary>
    public void Tick(float dt, float move, bool interact)
    {
        if (boardT >= 0f)
        {
            boardT += dt;
            BoardFade = Mathf.Clamp01(boardT / 0.8f);
            if (boardT >= 1.0f)
            {
                boardT = -1f;
                BoardFade = -1f;
                if (BoardRequested != null) BoardRequested();
            }
            return;
        }

        if (InDialogue)
        {
            string full = lines[lineIdx];
            if (!LineDone)
            {
                charT += dt * TypeSpeed;
                int n = Mathf.Min(full.Length, (int)charT);
                if (n != shownCount)
                {
                    shownCount = n;
                    Shown = full.Substring(0, n);
                }
                if (n >= full.Length) LineDone = true;
            }
            if (interact)
            {
                if (!LineDone)
                {
                    shownCount = full.Length;
                    Shown = full;
                    LineDone = true;
                }
                else if (lineIdx + 1 < lines.Length)
                {
                    lineIdx++;
                    BeginLine();
                }
                else
                {
                    InDialogue = false;
                    Shown = "";
                    if (DialogueFinished != null) DialogueFinished(DialogueTag);
                }
            }
            return;
        }

        Moving = Mathf.Abs(move) > 0.1f;
        if (Moving)
        {
            Facing = move > 0f ? 1 : -1;
            PlayerX = Mathf.Clamp(PlayerX + move * Speed * dt, 80f, WorldW - 80f);
            WalkPhase += dt * 9f;
        }
        UpdateNear();

        if (interact)
        {
            if (NearNpc)
            {
                int ni = NearNpcIdx;
                Facing = PlayerX > NpcXs[ni] ? -1 : 1;
                if (NpcInteract != null) NpcInteract(ni);
            }
            else if (NearTaxi)
            {
                boardT = 0f;
                Moving = false;
            }
        }
    }
}
