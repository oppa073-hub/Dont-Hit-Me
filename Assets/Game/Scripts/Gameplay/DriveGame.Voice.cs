using UnityEngine;

// 말소리 연결부: 손님의 말(인사, 수다, 사고 반응, 도착 인사)이 새로 나타나면 그 글자들을 웅얼거린다 (AudioManager.Speak).
// 뉴스 앵커는 한 글자씩 나타날 때마다 작게 (DriveGame.News.cs).
public partial class DriveGame
{
    string lastPaxLine = "", lastThanks = "";
    float lastReactUntil, lastThanksUntil;

    // 손님 목소리: 남자는 낮은 3가지, 여자는 높은 3가지(이름으로 정함), 외계인은 가장 높은 소리
    int PaxVoice()
    {
        if (taxi.Pax != null && taxi.Pax.quirk == Quirk.Alien) return 6;
        int h = string.IsNullOrEmpty(taxi.PaxName) ? 0 : Mathf.Abs(taxi.PaxName.GetHashCode()) % 3;
        return (taxi.PaxMale ? 0 : 3) + h;
    }

    void UpdateVoice()
    {
        if (gameOver || flow != GameFlow.Playing) return;
        if (taxi.State == TaxiState.Talking && taxi.PaxLine != lastPaxLine)
        {
            lastPaxLine = taxi.PaxLine;
            sound.Speak(taxi.PaxLine, PaxVoice(), voicePaxVolume);
        }
        else if (taxi.State != TaxiState.Talking) lastPaxLine = "";
        if (taxi.ReactUntil != lastReactUntil)
        {
            lastReactUntil = taxi.ReactUntil;
            if (!string.IsNullOrEmpty(taxi.ReactText) && taxi.ReactUntil > Time.unscaledTime)
                sound.Speak(taxi.ReactText, PaxVoice(), voicePaxVolume);
        }
        if (taxi.ThanksUntil != lastThanksUntil)
        {
            lastThanksUntil = taxi.ThanksUntil;
            string t = taxi.ThanksText;
            if (!string.IsNullOrEmpty(t) && taxi.ThanksUntil > Time.unscaledTime)
            {
                int c = t.IndexOf(':');
                sound.Speak(c >= 0 ? t.Substring(c + 1) : t, PaxVoice(), voicePaxVolume);
            }
        }
    }
}
