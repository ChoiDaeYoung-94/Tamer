# About

unity(2022.3.43f1 LTS) 3d game project


### Trailer

https://github.com/user-attachments/assets/243e5193-5e8f-4966-8b98-c724b62767dd


## Getting Started

1. Clone
~~~
git clone https://github.com/ChoiDaeYoung-94/Tamer.git
~~~
2. [Download Assets](https://drive.google.com/file/d/1Uf0BY7eUKvNS3aNMrFp-G1Bj6n61p1Fk/view?usp=sharing)
3. Open Project in Unity


## SDK, Package ...

- [PlayFabEditorExtensions, PlayFabSDK](https://docs.microsoft.com/ko-kr/gaming/playfab/sdks/unity3d/installing-unity3d-sdk)
- [play games plugin](https://github.com/playgameservices/play-games-plugin-for-unity/releases)
- [Google AdMob](https://developers.google.com/admob/android/quick-start?hl=ko)
- [MiniJson ](https://github.com/Unity-Technologies/UnityCsReference/blob/master/External/JsonParsers/MiniJson/MiniJSON.cs)
- [IngameDebugConsole](https://assetstore.unity.com/packages/tools/gui/in-game-debug-console-68068)
- [Safe Area Helper](https://assetstore.unity.com/packages/tools/gui/safe-area-helper-130488)


## Technologies and Techniques

- 몬스터 군집이동 구현
- MiniMap에 [FogOfWar](https://github.com/MicKami/FogOfWar) 적용
- 인앱결제
- google, playfab 로그인 적용
  - playfab 데이터 저장을 위해 사용
- Singleton 개선(Managers.cs) 및 사용하는 Manager코드 개선
- PoolManager.cs를 통해 Object Pooling 적용
- Login 시 체크해야 할 부분 State Pattern
- CICD를 통해 디버깅이 가능한 테스트용 빌드와 실제 스토어에 올라갈 빌드를 분리


## Download

- TODO > GOOGLE PLAY
- [APK](https://drive.google.com/file/d/1wlvrgaqbnP74TGHeWDy8xSW0fiXAfK79/view?usp=sharing)


## Self Feedback
- prefab 잘 사용하여 여러 monster 유지보수 용의하도록
  - 추후 프로젝트 진행 시 겹치는 부분 활용 잘 해야 함
- 장착 아이템 코드 확장 관련 개선 필요
    - FPS 프로젝트에서 각 부위별 무기로 개선해보기
- 데이터 관리 시 로컬, 서버 데이터 잘 구분해둬야 함


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


---


# 개인정보처리방침

**AeDeong**는 [Monster Tamer]를 운영합니다. 이 내용는 귀하가 앱을 사용할 때 개인 데이터의 수집, 사용 및 공개에 대한 정책을 알려주기 위해 만들어졌습니다.

## 1. 수집하는 정보

우리는 다음과 같은 유형의 정보를 수집할 수 있습니다:
- 개인 식별 정보 (이름, 이메일 주소 등)
- 사용 데이터 (앱 사용 패턴, 로그 데이터 등)
- 디바이스 정보 (기기 유형, 운영 체제 등)

## 2. 정보 사용 목적

수집된 정보는 다음과 같은 목적으로 사용됩니다:
- 서비스 제공 및 유지
- 사용자 지원 제공
- 사용 패턴 분석을 통한 서비스 개선
- 법적 의무 준수

## 3. 정보 공개

우리는 다음과 같은 경우에 귀하의 정보를 공개할 수 있습니다:
- 법적 요구가 있을 때
- 서비스 제공을 위한 제3자와의 공유 (예: 분석 서비스 제공업체)

## 4. 보안

우리는 귀하의 개인 정보를 보호하기 위해 상업적으로 허용되는 수단을 사용합니다. 그러나, 인터넷을 통한 전송 방법이나 전자 저장 방법은 100% 안전하지 않다는 점을 유의하시기 바랍니다.

## 5. 개인정보 보호정책의 변경

우리는 개인정보 보호정책을 수시로 업데이트할 수 있습니다. 변경 사항은 이 페이지에 게시됩니다. 변경 사항을 정기적으로 확인하는 것이 좋습니다.

## 6. 문의

이 개인정보 보호정책에 대해 질문이나 제안이 있으시면, doeud1410@gmail.com 으로 연락해 주십시오.

이 정책은 2024년 9월 30일부터 유효합니다.
