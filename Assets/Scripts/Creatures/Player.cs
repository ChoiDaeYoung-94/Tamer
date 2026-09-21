using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;

using Cysharp.Threading.Tasks;

public class Player : Creature
{
    private static Player _instance;
    public static Player Instance { get { return _instance; } }

    [Header("--- Player 데이터 ---")]
    [SerializeField] private GameObject _buffingObject = null;

    [Header("--- Player 공용 데이터 ---")]
    public GameObject PlayerObject;
    public Transform CameraArm;
    public GameObject SimpleSword;
    public GameObject MasterSword;
    public GameObject Simpleshield;
    public GameObject Mastershield;
    public bool IsEquippedSword = false;
    public bool IsBuffing = false;
    public float BuffPower;
    public float BuffAttackSpeed;
    public float BuffMoveSpeed;
    private int _gold;
    public int Gold { get { return _gold; } }
    public int MaxCaptureCapacity = 10;
    public List<string> PlayerMonsterCollection = new List<string>();
    public string EquippedItems = string.Empty;
    public List<string> PlayerEquippedItems = new List<string>();

    // 그 외 접근 불가 데이터
    private List<Monster> _allyMonsters = new List<Monster>();
    private bool _isAllyAvailable = false;
    private GameObject _curTargetMonsterObject;
    private Monster _curTargetMonster;
    private Monster _ableCaptureMonster;
    private Collider _captureTrigger;
    private string _monsterCollection = string.Empty;
    private AD.UpdateManager _updateSource;
    private AD.DataManager _gameplayData;
    private int _captureLifetime = -1;
    private bool GameplaySessionCurrent()
    {
        var data = AD.Managers.DataM;
        return data != null && ReferenceEquals(data, _gameplayData) && data.IsServerDataReady &&
            !data.DeletionInProgress && data.PlayFabId == _inventoryOwner && data.AccountGeneration == _inventoryGeneration;
    }
    private string _inventoryOwner;
    private int _inventoryGeneration = -1;
    private string _targetInventoryOwner;
    private int _targetInventoryGeneration = -1;

    private const string PLAYER_MONSTERS_KEY = "AllyMonsters";
    private const string PLAYER_EQUIPPED_ITEMS_KEY = "playerEquippedItems";
    private const string GOLD_KEY = "Gold";

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        _instance = this;
        DontDestroyOnLoad(transform.parent.gameObject);

        Init();
        AD.Managers.EquipmentM.Init();
    }

    /// <summary>
    /// 플레이어 초기화 이후 관련 요소 초기화
    /// </summary>
    private void OnEnable()
    {
        BindUpdates(AD.Managers.Instance != null ? AD.Managers.UpdateM : null);
        JoyStick.Instance.StartInit();
        CameraManage.Instance.StartInit();
        PlayerUICanvas.Instance.StartInit();
    }

    private void OnDisable()
    {
        ClearCaptureTarget();
        BindUpdates(null);
        StopBattle();
    }

    private void OnDestroy()
    {
        BindUpdates(null);
        StopBattle();
        if (_instance == this) _instance = null;
    }

    private void BindUpdates(AD.UpdateManager source)
    {
        if (_updateSource != null) _updateSource.OnUpdateEvent -= TouchEvent;
        _updateSource = source;
        if (_updateSource != null) _updateSource.OnUpdateEvent += TouchEvent;
    }

    private void Update()
    {
        if (_isAllyAvailable && State == CreatureState.Move)
            AllyMove();
    }

    #region Initialization

    /// <summary>
    /// HP는 장비에 맞게 따로 계산
    /// Player의 기본 HP는 100
    /// </summary>
    private void Init()
    {
        InitPrefs();

        JoyStick.Instance.SetSpeed(_moveSpeed);

    }

    public void ReSetPlayer()
    {
        ClearCaptureTarget();
        isDie = false;
        gameObject.layer = allyLayer;
        _capsuleCollider.enabled = true;
        State = CreatureState.Idle;
        Hp = _itemAdditionalHp > _originalHp ? _itemAdditionalHp : _originalHp;
    }

    #endregion

    #region Input Events

    /// <summary>
    /// 화면 클릭 이벤트 처리: 버프, 몬스터 포획 등
    /// </summary>
    private void TouchEvent()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                switch (hit.collider.tag)
                {
                    case "GoogleAdMob":
                        AD.Managers.SoundM.UI_Click();
                        if (BuffingMan.Instance != null)
                            BuffingMan.Instance.RequestAdReward();
                        break;
                    case "GoGameScene":
                        AD.Managers.SoundM.UI_Click();
                        AD.Managers.GameM.SwitchMainOrGameScene();
                        break;
                }
            }
        }
    }

    #endregion

    #region Buff and Heal

    public void SetBuff()
    {
        PlaySFX(AD.Managers.SoundM.SFXBuffClip);
        IsBuffing = true;
        BuffPower = _power * 1.3f;
        BuffAttackSpeed = _attackSpeed * 1.3f;
        BuffMoveSpeed = _moveSpeed * 2f;
        JoyStick.Instance.SetSpeed(BuffMoveSpeed);

        foreach (Monster monster in _allyMonsters)
            monster.SetSpeed();

        _buffingObject.SetActive(true);
    }

    public void EndBuff()
    {
        IsBuffing = false;
        JoyStick.Instance.SetSpeed(_moveSpeed);

        foreach (Monster monster in _allyMonsters)
            monster.SetSpeed();

        _buffingObject.SetActive(false);
    }

    public void Heal()
    {
        PlaySFX(AD.Managers.SoundM.SFXHealClip);
        Hp = _itemAdditionalHp > _originalHp ? _itemAdditionalHp : _originalHp;
        HealEffect();
        PlayerUICanvas.Instance.UpdatePlayerInfo();

        foreach (Monster monster in _allyMonsters)
        {
            monster.Hp = monster.OriginalHP;
            monster.HealEffect();
        }
    }

    #endregion

    #region Combat

    /// <summary>
    /// 플레이어 공격 애니메이션에서 진행
    /// </summary>
    protected override void AttackTarget()
    {
        if (isDie || !GameplaySessionCurrent()) return;
        if (_curTargetMonsterObject != null)
        {
            float power = IsBuffing ? BuffPower : Power;
            _curTargetMonster.GetDamage(power);
        }
    }

    public void HandleAttackCoroutine(bool isGame)
    {
        if (isGame && GameplaySessionCurrent())
        {
            StopBattle();
            StartBattle();
        }
    }

    protected override async UniTask BattleLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_curTargetMonsterObject != null)
            {
                Vector3 direction = _curTargetMonsterObject.transform.position - transform.position;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);
                State = CreatureState.Attack;

                float attackSpeed = IsBuffing ? BuffAttackSpeed : _attackSpeed;
                await UniTask.Delay(TimeSpan.FromSeconds(1f / attackSpeed), cancellationToken: token);
            }
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
        }
    }

    /// <summary>
    /// 추후 몬스터 객체 크기에 따라 distance 비교 차이를 둬야 함
    /// 몬스터 Data에 추가해도 괜찮을 것 같음
    /// </summary>
    protected override async UniTask MonitorTargetDistance(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_curTargetMonsterObject != null)
            {
                float distance = Vector3.Distance(transform.position, _curTargetMonsterObject.transform.position);
                if (distance > _curTargetMonster.FlockingRadius + 0.3f)
                    _curTargetMonsterObject = null;
            }
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
        }
    }

    #endregion

    #region Equipment

    public void ApplyEquipment(string item)
    {
        Dictionary<string, object> itemData = AD.Managers.DataM.ItemData[item] as Dictionary<string, object>;

        _itemAdditionalHp = _hp += float.Parse(itemData["Hp"].ToString());
        _power += float.Parse(itemData["Power"].ToString());
        _attackSpeed += float.Parse(itemData["AttackSpeed"].ToString());
        _moveSpeed += float.Parse(itemData["MoveSpeed"].ToString());
    }

    public void UnequipEquipment(string item)
    {
        Dictionary<string, object> itemData = AD.Managers.DataM.ItemData[item] as Dictionary<string, object>;

        _itemAdditionalHp = _hp -= float.Parse(itemData["Hp"].ToString());
        _power -= float.Parse(itemData["Power"].ToString());
        _attackSpeed -= float.Parse(itemData["AttackSpeed"].ToString());
        _moveSpeed -= float.Parse(itemData["MoveSpeed"].ToString());
    }

    #endregion

    #region Ally Monsters Management

    public void BuyAllyMonster(string name)
    {
        if (!GameplaySessionCurrent()) return;
        Monster monster = AD.Managers.PoolM.PopFromPool(name, AD.Managers.PoolM.RootPlayer).GetComponent<Monster>();
        monster.AllySetting(playerPosition: transform.position, setting: true);
        AddAllyMonster(monster);
    }

    public void MoveSound() => PlaySFX(AD.Managers.SoundM.SFXWalkClip);

    /// <summary>
    /// Monster.cs -> GroupMonsterMove()와 동일
    /// </summary>
    private void AllyMove()
    {
        int row = 2;
        int countInRow = 0;
        int listCount = _allyMonsters.Count;
        Vector3 startRowPosition = transform.position + (-transform.forward * _allyMonsters[0].FlockingRadius);

        for (int i = 0; i < listCount; i++)
        {
            if (countInRow >= row)
            {
                row++;
                countInRow = 0;
                startRowPosition += -transform.forward * _allyMonsters[i].FlockingRadius;
            }

            int plusRow = AD.Utility.GetSortedMonsterCount(row, 0);
            int curRow = listCount + 1 - plusRow;
            int maxCountInRow = curRow > row ? row : curRow;

            Vector3 positionOffset = transform.right * (countInRow - (maxCountInRow - 1) / 2.0f) * _allyMonsters[i].FlockingRadius;
            Vector3 targetPosition = startRowPosition + positionOffset;

            _allyMonsters[i].NavMeshAgent.isStopped = false;
            _allyMonsters[i].NavMeshAgent.SetDestination(targetPosition);
            _allyMonsters[i].State = CreatureState.Move;
            countInRow++;
        }
    }

    public void AllyIdle()
    {
        foreach (Monster monster in _allyMonsters)
            monster.State = CreatureState.Idle;
    }

    public void Capture()
    {
        Monster target = _ableCaptureMonster;
        bool allowed = GameplaySessionCurrent() && target != null && target.IsCurrentSession(_captureLifetime) && !isDie && Hp > 0 && _allyMonsters.Count < MaxCaptureCapacity &&
            target.IsCaptureAvailable &&
            _captureTrigger != null && _captureTrigger.enabled && _captureTrigger.gameObject.activeInHierarchy &&
            AD.Managers.Instance != null && AD.Managers.GameM.IsGame &&
            AD.Managers.SceneM != null && !AD.Managers.SceneM.IsTransitioning;
        // Consume before role changes disable the capture effect and fire trigger callbacks.
        ClearCaptureTarget();
        if (!allowed) return;
        target.AllySetting(playerPosition: transform.position);
        AddAllyMonster(target);
        PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    public void ClearCaptureTarget()
    {
        _ableCaptureMonster = null;
        _captureLifetime = -1;
        _captureTrigger = null;
        if (_instance == this && PlayerUICanvas.Instance != null) PlayerUICanvas.Instance.DisableCapture();
    }

    public void ReleaseCaptureTarget(Monster target)
    {
        if (_ableCaptureMonster == target) ClearCaptureTarget();
    }

    protected override void OnDeath() => ClearCaptureTarget();

    private void ObserveCaptureTrigger(Collider col)
    {
        if (!GameplaySessionCurrent() || isDie || Hp <= 0 || _allyMonsters.Count >= MaxCaptureCapacity || !col.CompareTag("Capture")) return;
        Monster target = col.GetComponentInParent<Monster>();
        if (target == null || !target.IsCaptureAvailable) return;
        // Another overlapping corpse must not replace a valid current selection.
        if (_ableCaptureMonster != null && _ableCaptureMonster.IsCaptureAvailable) return;
        _ableCaptureMonster = target;
        _captureLifetime = target.SessionLifetime;
        _captureTrigger = col;
        PlayerUICanvas.Instance.EnableCapture();
    }

    private void SettingAllyMonster()
    {
        string tempAlly = AD.Managers.DataM.LocalPlayerData[PLAYER_MONSTERS_KEY];
        List<string> monsterNames = tempAlly.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();

        foreach (string monsterName in monsterNames)
        {
            Monster monster = AD.Managers.PoolM.PopFromPool(monsterName, AD.Managers.PoolM.RootPlayer).GetComponent<Monster>();
            monster.AllySetting(playerPosition: transform.position, setting: true);
            _allyMonsters.Add(monster);
        }
    }

    private void AddAllyMonster(Monster monster)
    {
        if (!GameplaySessionCurrent() || monster == null || !monster.IsCurrentSession(monster.SessionLifetime)) return;
        _allyMonsters.Add(monster);
        monster.transform.SetParent(AD.Managers.PoolM.RootPlayer);

        string tempAlly = AD.Managers.DataM.LocalPlayerData[PLAYER_MONSTERS_KEY];
        string monsterType = monster.CreatureType.ToString();

        if (tempAlly.Equals("null"))
        {
            _isAllyAvailable = true;
            tempAlly = monsterType;
        }
        else
        {
            tempAlly += $",{monsterType}";
        }
        AD.Managers.DataM.UpdateLocalData(PLAYER_MONSTERS_KEY, tempAlly);
    }

    public void RemoveAllyMonster(Monster monster)
    {
        if (!GameplaySessionCurrent() || monster == null || !monster.IsCurrentSession(monster.SessionLifetime)
            || !_allyMonsters.Remove(monster)) return;
        string tempAlly = string.Empty;

        if (_allyMonsters.Count <= 0)
        {
            _isAllyAvailable = false;
            tempAlly = "null";
        }
        else
        {
            StringBuilder sb = new StringBuilder();

            foreach (Monster _monster in _allyMonsters)
                sb.Append($",{_monster.CreatureType.ToString()}");

            tempAlly = sb.ToString();
        }
        AD.Managers.DataM.UpdateLocalData(PLAYER_MONSTERS_KEY, tempAlly);
        PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    public void RemoveAllAllyMonster()
    {
        foreach (Monster monster in _allyMonsters)
            monster.GetDamage(1000f);
    }

    public void ActiveControl(bool active)
    {
        if (active)
        {
            AD.Managers.PoolM.RootPlayer.transform.position = Vector3.zero;
            foreach (Monster monster in _allyMonsters)
            {
                monster.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(1f, 3f);
                monster.NavMeshAgent.enabled = true;
            }
        }
        else
        {
            foreach (Monster monster in _allyMonsters)
                monster.NavMeshAgent.enabled = false;
            AD.Managers.PoolM.RootPlayer.transform.position = new Vector3(100f, 100f, 100f);
        }
    }

    #endregion

    #region PlayerPrefs Management

    private void InitPrefs()
    {
        RefreshInventorySession();
    }

    public void ClearInventorySession()
    {
        _gameplayData = null;
        StopBattle();
        foreach (var monster in _allyMonsters.ToArray())
            if (monster != null) monster.RetireSession();
        _allyMonsters.Clear(); _isAllyAvailable = false;
        _gold = 0;
        _curTargetMonsterObject = null; _curTargetMonster = null;
        _targetInventoryOwner = null; _targetInventoryGeneration = -1;
        ClearCaptureTarget();
        foreach (var item in PlayerEquippedItems)
        {
            UnequipEquipment(item);
            if (AD.Managers.EquipmentM.EquipmentMapping.TryGetValue(item, out var model) && model != null) model.SetActive(false);
        }
        PlayerMonsterCollection.Clear(); PlayerEquippedItems.Clear();
        _monsterCollection = EquippedItems = string.Empty;
        _inventoryOwner = null; _inventoryGeneration = -1;
    }

    public void RefreshInventorySession()
    {
        var data = AD.Managers.DataM;
        if (!data.IsServerDataReady) { ClearInventorySession(); return; }
        if (ReferenceEquals(_gameplayData, data) && _inventoryOwner == data.PlayFabId && _inventoryGeneration == data.AccountGeneration) return;
        ClearInventorySession();
        var snapshot = data.ReadInventory();
        _inventoryOwner = snapshot.Owner; _inventoryGeneration = data.AccountGeneration;
        _gameplayData = data;
        _gold = int.Parse(data.LocalPlayerData[GOLD_KEY]);
        if (data.LocalPlayerData[PLAYER_MONSTERS_KEY] != "null")
        {
            SettingAllyMonster();
            _isAllyAvailable = _allyMonsters.Count > 0;
        }
        PlayerMonsterCollection = snapshot.Collection.ToList();
        _monsterCollection = string.Join(",", PlayerMonsterCollection);
        PlayerEquippedItems = snapshot.Equipped.Values.Where(value => value != null).ToList();
        EquippedItems = string.Join(",", PlayerEquippedItems);
        foreach (var item in PlayerEquippedItems)
        {
            ApplyEquipment(item);
            if (AD.Managers.EquipmentM.EquipmentMapping.TryGetValue(item, out var model) && model != null) model.SetActive(true);
        }
        if (isActiveAndEnabled && AD.Managers.GameM.IsGame) StartBattle();
    }

    public void EquipInventoryItem(string item)
    {
        var data = AD.Managers.DataM;
        var current = data.ReadInventory();
        var slots = new Dictionary<string, string>(current.Equipped);
        string slot = AD.Managers.EquipmentM.SegmentedEquipment.First(pair => pair.Value.Contains(item)).Key;
        string previous = slots[slot]; slots[slot] = item;
        var saved = data.WriteInventory(_inventoryOwner, _inventoryGeneration, current.Collection, current.OwnedItems, slots);
        // Slot replacement is one durable write; failed validation/I/O leaves equipment and stats untouched.
        if (previous != null)
        {
            UnequipEquipment(previous);
            AD.Managers.EquipmentM.EquipmentMapping[previous].SetActive(false);
        }
        PlayerEquippedItems = saved.Equipped.Values.Where(value => value != null).ToList();
        EquippedItems = string.Join(",", PlayerEquippedItems);
        ApplyEquipment(item); AD.Managers.EquipmentM.EquipmentMapping[item].SetActive(true);
    }

    public string SavePrefs(List<string> list, string str, string data, string key)
    {
        if (list.Contains(data))
            return str;
        if (key != PLAYER_MONSTERS_KEY) throw new InvalidOperationException("Use atomic inventory equipment replacement.");
        var manager = AD.Managers.DataM;
        var current = manager.ReadInventory();
        var collection = current.Collection.ToList();
        if (!collection.Contains(data)) collection.Add(data);
        var saved = manager.WriteInventory(_inventoryOwner, _inventoryGeneration, collection, current.OwnedItems, current.Equipped);
        list.Clear(); list.AddRange(saved.Collection);
        return string.Join(",", list);
    }

    public string RemovePrefs(List<string> list, string str, string data, string key)
    {
        if (!list.Contains(data))
            return str;

        if (key != PLAYER_MONSTERS_KEY) throw new InvalidOperationException("Use atomic inventory equipment replacement.");
        var manager = AD.Managers.DataM;
        var current = manager.ReadInventory();
        var collection = current.Collection.Where(value => value != data).ToList();
        var saved = manager.WriteInventory(_inventoryOwner, _inventoryGeneration, collection, current.OwnedItems, current.Equipped);
        list.Clear(); list.AddRange(saved.Collection);
        return string.Join(",", list);
    }

    #endregion

    public int GetCurMonsterCount()
    {
        return _allyMonsters.Count;
    }

    /// <summary>
    /// monster가 죽은 뒤 호출
    /// </summary>
    public void NotifyPlayerOfDeath(GameObject target, int gold, int lifetime)
    {
        var monster = target != null ? target.GetComponent<Monster>() : null;
        if (!GameplaySessionCurrent() || monster == null || !monster.TryConsumeReward(lifetime)) return;
        int nextGold = _gold + gold;
        _gameplayData.UpdateLocalData(GOLD_KEY, nextGold.ToString());
        _gold = nextGold;
        RecordDefeatedMonster(target);
        if (PlayerUICanvas.Instance != null) PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    private void RecordDefeatedMonster(GameObject target)
    {
        if (target == null || target != _curTargetMonsterObject || _curTargetMonster == null ||
            _targetInventoryOwner == null || _targetInventoryOwner != _inventoryOwner ||
            _targetInventoryGeneration != _inventoryGeneration) return;
        _monsterCollection = SavePrefs(PlayerMonsterCollection, _monsterCollection,
            _curTargetMonster.CreatureType.ToString(), PLAYER_MONSTERS_KEY);
        _curTargetMonsterObject = null; _curTargetMonster = null;
    }

    /// <summary>
    /// Die ani
    /// </summary>
    private void GameOver()
    {
        AD.Managers.GameM.GameOver();
    }

    public void MinusGold(int gold)
    {
        if (!GameplaySessionCurrent()) return;
        int nextGold = _gold - gold;
        _gameplayData.UpdateLocalData(GOLD_KEY, nextGold.ToString());
        _gold = nextGold;
        AD.Managers.DataM.UpdatePlayerData();
        PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    #region Trigger Events

    private void OnTriggerEnter(Collider col)
    {
        ObserveCaptureTrigger(col);
    }

    private void OnTriggerStay(Collider col)
    {
        if (isDie)
            return;

        ObserveCaptureTrigger(col);
        if (col.CompareTag("Monster") && col.gameObject.layer == enemyLayer)
        {
            if (_curTargetMonsterObject == null)
            {
                _curTargetMonsterObject = col.gameObject;
                _curTargetMonster = _curTargetMonsterObject.GetComponent<Monster>();
                _targetInventoryOwner = _inventoryOwner;
                _targetInventoryGeneration = _inventoryGeneration;
            }
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col == _captureTrigger) ClearCaptureTarget();
    }

    #endregion

    public override void Clear()
    {

    }
}
