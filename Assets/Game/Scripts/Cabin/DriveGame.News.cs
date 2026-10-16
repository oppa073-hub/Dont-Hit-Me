using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 내비 화면의 아침 뉴스: 운전을 시작하면 지도 대신 앵커가 오늘이 어떤 날인지 알려 주고, 끝나면 지도로 돌아온다.
// 손님을 태우거나 N 키를 누르면 바로 지도로 돌아온다. 원고는 StoryCatalog.News.
public partial class DriveGame
{
    Transform newsRoot, newsStudio, newsHeader, newsTicker, newsHead, newsHair, newsEyeL, newsEyeR, newsBody, newsTie;
    TextMesh newsBadgeText, newsBodyText, newsTickerText;

    bool newsActive, newsShown;          // newsActive = 뉴스 시간, newsShown = 화면이 지금 뉴스 모습인지
    float newsT;
    string[] newsLines = new string[0];
    int newsShownLine = -1, newsShownChars = -1;

    const float NewsLineSec = 3.8f;

    void BuildNews()
    {
        newsRoot = new GameObject("News").transform;
        newsRoot.SetParent(navScreen, false);
        newsStudio = CabinPart("NewsStudio", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.08f, 0.16f, 0.38f)));
        newsHeader = CabinPart("NewsHeader", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.78f, 0.1f, 0.1f)));
        newsTicker = CabinPart("NewsTicker", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.08f, 0.08f, 0.12f)));
        newsBody = CabinPart("NewsSuit", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.1f, 0.1f, 0.16f)));
        newsTie = CabinPart("NewsTie", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.85f, 0.15f, 0.15f)));
        newsHead = CabinPart("NewsHead", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.96f, 0.8f, 0.66f)));
        newsHair = CabinPart("NewsHair", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.12f, 0.08f, 0.06f)));
        newsEyeL = CabinPart("NewsEyeL", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.05f, 0.05f, 0.05f)));
        newsEyeR = CabinPart("NewsEyeR", PrimitiveType.Quad, newsRoot, Unlit(new Color(0.05f, 0.05f, 0.05f)));

        newsBadgeText = CabinText("NewsBadge", newsRoot, 0.0105f, TextAnchor.MiddleLeft, Color.white);
        newsBadgeText.alignment = TextAlignment.Left;
        newsBadgeText.text = "시티뉴스  LIVE";
        newsBodyText = CabinText("NewsBody", newsRoot, 0.0115f, TextAnchor.UpperLeft, Color.white);
        newsBodyText.alignment = TextAlignment.Left;
        newsTickerText = CabinText("NewsTickerText", newsRoot, 0.0085f, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
        newsTickerText.text = "N 키: 뉴스 건너뛰기";
        newsRoot.gameObject.SetActive(false);
    }

    // 운전을 시작할 때: 오늘의 뉴스를 방송한다
    void StartNews()
    {
        newsLines = StoryCatalog.News(dayEvents.Type, dayEvents.Day, shiftMode, Mathf.RoundToInt(shiftSeconds));
        newsT = 0f;
        newsActive = true;
        newsShownLine = newsShownChars = -1;
        sound.PlayArrive();                  // 손님 호출음(차임)과 다른 소리
    }

    void EndNews()
    {
        newsActive = false;
    }

    // 메인 루프에서 매 프레임: 시간이 지나면 뉴스를 끝낸다
    void UpdateNews(float dt, Keyboard kb)
    {
        if (!newsActive) return;
        if (kb != null && kb.nKey.wasPressedThisFrame) { EndNews(); return; }
        newsT += dt;
        if (newsT >= newsLines.Length * NewsLineSec + 0.8f) EndNews();
    }

    // 내비 화면을 뉴스 모습으로: 지도 조각들을 숨기고 스튜디오를 켠다
    void EnterNewsView()
    {
        newsShown = true;
        newsRoot.gameObject.SetActive(true);
        HideFrom(navGridV, 0);
        HideFrom(navGridH, 0);
        HideFrom(navRoute, 0);
        navDest.gameObject.SetActive(false);
        navCaller.gameObject.SetActive(false);
        navCar.gameObject.SetActive(false);
        navBar.gameObject.SetActive(false);
        navHeadText.gameObject.SetActive(false);
        navInfoText.gameObject.SetActive(false);
    }

    // 뉴스가 끝나 지도로 돌아온다
    void ExitNewsView()
    {
        newsShown = false;
        newsRoot.gameObject.SetActive(false);
        navCar.gameObject.SetActive(true);
        navBar.gameObject.SetActive(true);
        navHeadText.gameObject.SetActive(true);
        navInfoText.gameObject.SetActive(true);
        navHead = -1;
        navRem = -3;                         // 아래 정보 글자를 다시 만들게 한다
    }

    // 매 프레임 뉴스 화면 배치와 글자
    void UpdateNewsView()
    {
        float w = Mathf.Max(0.05f, navSize.x), h = Mathf.Max(0.04f, navSize.y);
        newsStudio.localPosition = new Vector3(0f, 0f, -0.0035f);
        newsStudio.localScale = new Vector3(w, h, 1f);
        float hy = h * 0.5f - h * 0.07f;
        newsHeader.localPosition = new Vector3(0f, hy, -0.004f);
        newsHeader.localScale = new Vector3(w, h * 0.14f, 1f);
        newsBadgeText.transform.localPosition = new Vector3(-w * 0.5f + 0.006f, hy, -0.0045f);
        float ty = -h * 0.5f + h * 0.07f;
        newsTicker.localPosition = new Vector3(0f, ty, -0.004f);
        newsTicker.localScale = new Vector3(w, h * 0.14f, 1f);
        newsTickerText.transform.localPosition = new Vector3(0f, ty, -0.0045f);

        // 앵커: 말하는 동안 살짝 들썩인다
        float cx = -w * 0.31f;
        float talk = Mathf.Sin(Time.unscaledTime * 12f) * h * 0.006f;
        float headY = h * 0.04f + talk;
        newsBody.localPosition = new Vector3(cx, headY - h * 0.27f, -0.0045f);
        newsBody.localScale = new Vector3(h * 0.46f, h * 0.24f, 1f);
        newsTie.localPosition = new Vector3(cx, headY - h * 0.21f, -0.0048f);
        newsTie.localScale = new Vector3(h * 0.04f, h * 0.12f, 1f);
        newsHead.localPosition = new Vector3(cx, headY, -0.005f);
        newsHead.localScale = new Vector3(h * 0.20f, h * 0.24f, 1f);
        newsHair.localPosition = new Vector3(cx, headY + h * 0.115f, -0.0052f);
        newsHair.localScale = new Vector3(h * 0.22f, h * 0.07f, 1f);
        newsEyeL.localPosition = new Vector3(cx - h * 0.045f, headY + h * 0.02f, -0.0054f);
        newsEyeL.localScale = new Vector3(h * 0.025f, h * 0.03f, 1f);
        newsEyeR.localPosition = new Vector3(cx + h * 0.045f, headY + h * 0.02f, -0.0054f);
        newsEyeR.localScale = new Vector3(h * 0.025f, h * 0.03f, 1f);

        // 원고: 한 줄씩 한 글자씩 나타난다
        newsBodyText.transform.localPosition = new Vector3(-w * 0.10f, h * 0.5f - h * 0.17f, -0.005f);
        int line = Mathf.Min(newsLines.Length - 1, Mathf.FloorToInt(newsT / NewsLineSec));
        if (line < 0) return;
        string full = StoryCatalog.Wrap(newsLines[line]);
        int chars = Mathf.Clamp(Mathf.FloorToInt((newsT - line * NewsLineSec) * 24f), 0, full.Length);
        if (line != newsShownLine || chars != newsShownChars)
        {
            newsShownLine = line;
            newsShownChars = chars;
            newsBodyText.text = full.Substring(0, chars);
        }
    }
}
