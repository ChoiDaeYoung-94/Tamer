## Player data

모든 데이터는 string 형식으로 저장 됨

Resources/Data/PlayerData.json 에 Player의 시작 데이터가 저장된다.
이 데이터는 플레이하면서 수정되며 서버 통신시 비교 대상이 된다.

모든 데이터는 서버에서 관리한다.
다만 경험치 등의 자주 갱신해줘야 하는 데이터 처리를 위해 데이터 관리 규칙을 정한다.
- 첫 로그인 시 기본 데이터는 Resources/Data/PlayerData.json 을 받아 사용한다.
- 로컬에서 데이터를 관리하다 특정 데이터의 변경 사항이 생길 경우 바로 서버에 올리는 것이 아닌 특정 시간이 지난 뒤 PlayerData.json에 저장한다.
- 이후 씬간 이동이 있을 때 로컬 데이터와 PlayerData.json를 비교하여 최신화 한 후 서버에 올린다.
- 로그인 시 서버와 PlayerData.json을 비교하여 최신화 한다.

* 사실 JSON이 중간 역할을 해주는게 좋지 않지만 당장 서버에만 의존하게 되면 비용적 손실이 너무 크며 자주 갱신해줘야 하는 데이터의 경우 
  지속적인 서버와의 통신이 필요한데 여러번 반복해서 보낼 경우 패킷 손실이 나는 경우가 빈번하기에 데이터 관련 로직은 서버코드로 처리하게 하고 싶으나 개인 작업으로 진행하니 이렇게 진행!

### player data

- NickName > string
- Sex > "Man" or "Woman"
- Tutorial > "null"
- Gold > "0"
- Level > "1"
- Experience > "0"
- HP > "100"
- Power > "10"
- AttackSpeed > "0.5"
- MoveSpeed > "2.0"
- MaxCount > "1"
