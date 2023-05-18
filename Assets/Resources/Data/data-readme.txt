## Player data

모든 데이터는 string 형식으로 저장 됨

local data는 첫 값이 기본 값이며 local data이기 때문에 플레이하면서 수정된다.
Resources/Data/Player-local.json으로 local에 저장되어있다.

server data는 가질 수 있는 데이터 형식 or 값을 표기 함
server data의 경우 인앱 결제 상품 등 현금과 관련된 data 혹은 바뀌지 않아야 하는 data로 구성된다.
항상 DataManager에서 관리하며 절대 local에 두지 않는다.

### local data

- Tutorial > "null"
- Gold > "0"
- Level > "1"
- Experience > "0"
- HP > "100"
- Power > "10"
- AttackSpeed > "0.5"
- MoveSpeed > "2.0"
- MaxCount > "1"

### server data
- Sex > "Man" or "Woman"
- NickName > string