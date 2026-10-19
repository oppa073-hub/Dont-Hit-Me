using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 사운드 연결부: 소리 만들기/재생은 AudioManager 가 하고, 여기서는 게임 사건(손님 등장, 도착)을 효과음에 이어 준다.
// 충돌/니어미스 효과음은 DriveGame.Pedestrians.cs 에서 부른다.
public partial class DriveGame
{
    AudioManager sound;

    void CreateSound()
    {
        sound = new AudioManager(this, gameObject, cam);
        taxi.CallerAppeared += sound.PlayChime;
        StartCoroutine(sound.Preload());                 // 소리 파일을 시작할 때 미리 읽는다 (여러 프레임에 나눠서)
    }
}
