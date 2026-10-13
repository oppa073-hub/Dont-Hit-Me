## 한 줄 소개
운전석 1인칭 시점의 PC 아케이드 게임. 둥글게 휜 도시 도로에서 무단횡단자를 조향으로 피한다.
혼자 한 달 안에 소규모로 완성하는 것이 목표. 지금은 프로토타입 단계.

## 작업 규칙
- 코드를 짜기 전에 계획부터 설명하고 확인받을 것.
- 프로토타입이라 구조보다 속도 우선. 버릴 코드로 취급. 한 번에 하나의 질문만 검증.
- 튜닝 변수는 public 필드로 노출해서 인스펙터에서 바로 조절 가능하게 할 것.
- 확인하지 않은 것을 확인했다고 말하지 말 것. Unity 에디터를 직접 실행할 수 없으므로 컴파일/플레이 결과는 사용자가 알려 줌.
- 삭제, 푸시, 저장소 생성 같은 되돌리기 어렵거나 외부로 나가는 작업은 실행 전에 확인받을 것.
- 기능 작업이 끝나면 qa-tester로 확인한다.
- 외부 에셋은 전부 Assets/_External/ 안에 둔다(.gitignore로 제외됨, 업로드 금지).

## 게임 규칙
- 차는 제자리, 월드가 다가온다. 도로는 앞쪽으로 갈수록 아래로 휜다(y = -curvature * z^2, 동물의 숲 방식).
- 조작: A/D 또는 ←/→ 조향, S/↓ 브레이크. 반응형(음악 리듬 없음).
- 무단횡단자는 2D 스프라이트 빌보드. 충돌하면 목숨 -1(3개), 아슬아슬하게 피하면 니어미스 보너스.
- 차 내부: 3D 핸들 + 3D 팔(arms.fbx)을 코드 IK로 핸들에 올림, 손가락은 접선축으로 굽혀 쥠. 왼팔은 항상 보여야 함.
- 도시: 상자 건물 몸통에 타일 스프라이트를 붙임. 건물 스프라이트는 월드에 고정(사람처럼 카메라를 쳐다보지 않음).

## 구조
- Assets/Prototype3/ProtoDrive.cs : 게임 전체 로직(한 파일)
- Assets/Prototype3/Editor/ProtoDriveSceneCreator.cs : 메뉴 Prototype → Create Drive Scene
- Assets/Prototype3/DriveScene.unity : 에셋 참조가 연결된 씬
- 에셋: GandalfHardcore City Tiles, arms.fbx, arm_albedo_*.png

## 기술 환경
- Unity 6 (6000.3.5f2), URP, 새 Input System. Active Input Handling은 Both 또는 Input System.
- PC 전용. 해상도는 16:9 기준.

## 알려진 한계 / 확인 필요
- 3D 팔 IK 자세와 주먹 모양은 화면에서 검증되지 않음(Hand Angle, Wrist To Hand, Shoulder Center, Arm Length로 조정).
- 건물 스프라이트 수가 많아 성능 확인 필요.

## 에셋 라이선스
- GandalfHardcore City Tiles: 상업 사용과 수정 허용, 재판매·재배포 금지. 저장소는 Private 유지.

## Git
- 원격: (저장소 주소를 여기에 적기)
- 저장소는 Private. 푸시 전 확인받을 것.
