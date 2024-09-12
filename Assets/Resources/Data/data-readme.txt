## Player data

모든 데이터는 string 형식으로 저장 됨

Resources/Data/PlayerData.json 에 Player의 시작 데이터가 저장된다.

모든 데이터는 서버에서 관리하나 로컬에도 데이터를 둔다.

경험치 등의 자주 갱신해줘야 하는 데이터 처리를 위해 데이터 관리 규칙을 정한다.
- 첫 로그인 시 기본 데이터는 Resources/Data/PlayerData.json 을 받아 사용한다.
- 이후 Application.persistentDataPath에 PlayerData.json 을 저장하고 이 데이터를 비교대상으로 한다.
- 로컬에서 데이터를 관리하다 특정 데이터의 변경 사항이 생길 경우 바로 서버에 올리는 것이 아닌 Coroutine으로 지속적으로 특정 시간이 지난 뒤 PlayerData.json에 저장한다.
- 이후 씬간 이동 및 재접속 시 로컬 데이터와 PlayerData.json를 비교하여 최신화 한 후 서버에 올린다.
  - 로컬 데이터는 곧 PlayerData.json이긴 하지만 로컬 데이터가 변동 시 바로 PlayerData.json에 저장되는것이 아니라 위 내용처럼 
    Coroutine으로 지속적으로 특정 시간이 지난 뒤 PlayerData.json에 저장되기 때문에 씬 이동시에는 비교하여 최신화 후 서버에 올린다.
- 로그인 시 서버와 PlayerData.json을 비교하여 최신화 한다.

* 사실 JSON이 중간 역할을 해주는게 좋지 않지만 당장 서버에만 의존하게 되면 비용적 손실이 너무 크며 자주 갱신해줘야 하는 데이터의 경우 
  지속적인 서버와의 통신이 필요한데 여러번 반복해서 보낼 경우 패킷 손실이 나는 경우가 빈번하기에 데이터 관련 로직은 서버코드로 처리하게 하고 싶으나 개인 작업으로 진행하니 이렇게 진행!
  
* 데이터가 누락 될 경우는 플레이어가 사냥 시 로컬에서 데이터가 바뀌었는데 PlayerData.json에 저장을 못하고 종료될 경우가 존재한다.

* 만약 데이터가 추가 된다면 Resources/Data/PlayerData.json에 추가 후 Application.persistentDataPath.PlayerData.json에 데이터를 추가하고 서버에 추가한다.

### player data

- NickName > string
- Sex > "Man" or "Woman"
- Tutorial > "null"
- GoogleAdMob > "null or DateTime"
- Gold > "0"
- Power > "10"
- AttackSpeed > "0.5"
- MoveSpeed > "3.0"
