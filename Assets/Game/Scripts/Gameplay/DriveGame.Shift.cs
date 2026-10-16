using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 하루 영업 모드: shiftSeconds 동안 일하고, 시간이 끝나면 (손님을 태운 중이면 그 손님만 마저 모셔다 드리고) 영업을 마치며 결산한다.
// 결산 = 요금 합계 - 사고 수리비. 목숨을 모두 잃으면 그 자리에서 중단된다. 무한 모드는 shiftMode 를 끄면 된다.
public partial class DriveGame
{
    const string ModeKey = "DontHitMe.ShiftMode";

    bool shiftMode = true;
    float shiftLeft, shiftOvertime;       // 남은 영업 시간 / 시간이 끝난 뒤 마지막 손님을 모시느라 쓴 시간
    bool lastFareWarned;
    int hudShiftSec = -1;
    string hudShiftText = "";

    void ResetShift()
    {
        shiftLeft = shiftSeconds;
        shiftOvertime = 0f;
        lastFareWarned = false;
        taxi.AcceptCallers = true;
    }

    void ToggleMode()
    {
        shiftMode = !shiftMode;
        PlayerPrefs.SetInt(ModeKey, shiftMode ? 1 : 0);
        PlayerPrefs.Save();
        RefreshModeText();
    }

    // 사고 수리비를 뺀 순수익
    int ShiftNet() { return Mathf.Max(0, taxi.FareTotal - hits * accidentFine); }

    // 순수익에 따른 평점 (0~3)
    int ShiftStars()
    {
        int net = ShiftNet();
        return net >= shiftStar3 ? 3 : (net >= shiftStar2 ? 2 : (net >= shiftStar1 ? 1 : 0));
    }

    // 코인으로 바꿀 이번 판의 수입 (영업 모드는 수리비를 뺀 순수익)
    int RunIncome() { return shiftMode ? ShiftNet() : taxi.FareTotal; }

    // 메인 루프에서 매 프레임: 시간이 다 되면 새 손님을 받지 않고, 손님이 없으면 영업 종료
    void UpdateShift(float dt)
    {
        if (!shiftMode) return;
        shiftLeft = Mathf.Max(0f, shiftLeft - dt);
        if (shiftLeft > 0f) return;

        taxi.AcceptCallers = false;
        if (taxi.State == TaxiState.Carrying || taxi.State == TaxiState.Waiting)       // 손님을 태웠거나 은행 앞에서 기다리는 중이면 마저 끝낸다
        {
            if (!lastFareWarned)
            {
                lastFareWarned = true;
                Popup("영업 종료 시간! 마지막 손님을 모셔다 드리세요", new Color(1f, 0.8f, 0.4f));
            }
            shiftOvertime += dt;
            if (shiftOvertime <= shiftOvertimeMax) return;       // 너무 오래 끌면 그냥 마친다
            if (turning) return;                                 // 교차로를 도는 중이면 다 돌고 나서 마친다
            Popup("마지막 손님을 못 모셨어요…", new Color(1f, 0.5f, 0.4f));
        }
        else if (turning) return;                                // 월드가 기울어진 채 멈추지 않게, 회전이 끝난 뒤에 마친다
        session.ForceEnd();
    }

    // HUD 글자 ("영업 2:31"). 초가 바뀔 때만 새로 만든다.
    string ShiftHudText()
    {
        int sec = Mathf.CeilToInt(shiftLeft);
        if (sec != hudShiftSec)
        {
            hudShiftSec = sec;
            hudShiftText = shiftLeft > 0f ? "영업 " + (sec / 60) + ":" + (sec % 60).ToString("00") : "마지막 손님";
        }
        return hudShiftText;
    }
}
