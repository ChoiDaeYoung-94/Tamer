# About

unity 3d game project


### Trailer

TODO


## Getting Started

1. Clone
2. [Download Assets](https://drive.google.com/file/d/1U0U3VVj5v4aMPq1CzgJaeC-nNKrx9yES/view?usp=share_link)
3. Open Project in Unity


## Requirements

- Unity2020.3.25f1 LTS


## SDK, Package, Library ...

- [PlayFabEditorExtensions, PlayFabSDK](https://docs.microsoft.com/ko-kr/gaming/playfab/sdks/unity3d/installing-unity3d-sdk)
- [play games plugin](https://github.com/playgameservices/play-games-plugin-for-unity/releases)
- [Google AdMob](https://developers.google.com/admob/android/quick-start?hl=ko)
- [MiniJson ](https://github.com/Unity-Technologies/UnityCsReference/blob/master/External/JsonParsers/MiniJson/MiniJSON.cs)
- [IngameDebugConsole](https://assetstore.unity.com/packages/tools/gui/in-game-debug-console-68068)
- [Safe Area Helper](https://assetstore.unity.com/packages/tools/gui/safe-area-helper-130488)


## Technologies and Techniques
- MiniMap에 [FogOfWar](https://github.com/MicKami/FogOfWar) 적용
- 몬스터 군집이동 구현
- CICD를 통해 디버깅이 가능한 테스트용 빌드와 실제 스토어에 올라갈 빌드를 분리
- Singleton 기법 개선(Managers.cs) 및 사용하는 Manager코드 개선
- Login 시 체크해야 할 부분을 State Pattern으로 구현
- PoolManager.cs를 통해 Object Pooling 적용
- google, playfab 로그인 적용
  - playfab 데이터 저장을 위해 사용


## Download

- TODO > GOOGLE PLAY
- [APK](https://drive.google.com/file/d/1bV78lKlD4uujYy79wkdofEt0yIv41Dr2/view?usp=drive_link)
- Clone the repository locally:
~~~
git clone https://github.com/ChoiDaeYoung-94/Tamer.git
~~~


## Build

| platform  | output   |
| --------- | -------- |
| AOS       | apk, aab |
| iOS       |   TODO   |

build 추출물은 Project root/Build/AOS, Project root/Build/iOS 에 위치한다.


### Unity Scenario

시작 전 게임 프로젝트의 root 경로에 Build 폴더를 만든 뒤 진행한다.

- apk
  - Unity Menu - Build - AOS - APK
- aab
  - Unity Menu - Build - AOS - AAB


### CLI Scenario

https://github.com/ChoiDaeYoung-94/unity-cicd 레포의 build.py를 사용하여 빌드한다.

build.py를 통해 build 시 aab, apk 모두 빌드된다.

terminal > python build.py > 매개변수 입력 > build


### Github Actions Scenario

main branch에 push 할 경우 Github Action이 작동하고 BuildPC에서 빌드를 진행한다.

마지막 commit message에 ci skip 이 포함되어 있을 경우 Github Actions을 skip 한다.

빌드 추출물(aab)은 Appcenter에 upload 되며 Appcenter에서 다운로드 시 apk로 다운로드 하기때문에 apk는 추출하지 않는다.

정상적으로 upload 되었다면 Appcenter에 등록되어 있는 group 사용자에게 알림(e-mail)을 보낸다.
