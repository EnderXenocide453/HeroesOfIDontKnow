using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class MapController : MonoBehaviour
{
    //Кнопка особой способности
    public EventTrigger abilityBtn;
    //Бриф битвы
    public TMPro.TMP_Text logField;

    #region Карта
    //Карта хексов
    public Tilemap tilemap;
    public Tile normalTile;
    //Карта подсветки хексов
    public Tilemap highliteMap;
    public Tile highliteTile;

    //Параметры карты
    public Vector2Int mapSize;

    //Поле боя
    /// <summary>
    /// 0-пустая клетка
    /// 1-препятствие
    /// </summary>
    private Dictionary<Vector3Int, (bool isObstacle, List<Vector3Int> neighbours)> _battleField;
    //Выбранный тайл
    private Vector3Int _activeCoord = Vector3Int.zero;
    #endregion

    #region События
    //События взаимодействия с тайлами
    public delegate void TileHandler(Vector3Int coord);
    public event TileHandler onMouseEnter;
    public event TileHandler onMouseExit;
    public event TileHandler onMouseDown;
    public event TileHandler onMouseUp;
    #endregion

    #region Войска
    public GameObject unitPrefab;
    //начальные войска первого игрока
    public SpawnInfo[] firstSpawnQueue;
    //начальные войска второго игрока
    public SpawnInfo[] secondSpawnQueue;

    public int turn { get; private set; } = 0;

    private int _turnRepeats = 0;
    private List<CharacterController> _characters;
    //Пути, найденные алгоритмом Дейкстры
    private Dictionary<Vector3Int, int> _dijkstra;
    private List<Vector3Int> _canAttack;
    private bool canCommand = false;
    private int[] _factionAlive;

    private bool _isAbilityActive;
    private Vector3Int[] _abilityArea;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        InitiateMap();

        StartTurn();

        onMouseEnter += Highlite;
        onMouseUp += OnClick;
    }

    // Update is called once per frame
    void Update()
    {
        CheckMouse();
    }

    #region Манипуляции с картой
    //Инициация карты и спавн персонажей
    private void InitiateMap()
    {
        tilemap.ClearAllTiles();
        _battleField = new Dictionary<Vector3Int, (bool, List<Vector3Int>)>();

        Vector3Int offset = new Vector3Int(mapSize.x / 2, mapSize.y / 2, 0);

        for (int j = 0; j < mapSize.y; j++) {
            //Параметр сдвига по гексагональной сетке
            int hexOffset = (j + 1) % 2;

            for (int i = 0; i < mapSize.x; i++) {
                Vector3Int pos = new Vector3Int(i, j, 0) - offset;

                tilemap.SetTile(pos, normalTile);

                List<Vector3Int> neigh = new List<Vector3Int>()
                {
                    pos - new Vector3Int(1 - hexOffset, -1, 0),
                    pos - new Vector3Int(0 - hexOffset, -1, 0),
                    pos - new Vector3Int(1 - hexOffset, 1, 0),
                    pos - new Vector3Int(0 - hexOffset, 1, 0),
                    pos - new Vector3Int(1, 0, 0),
                    pos - new Vector3Int(-1, 0, 0)
                };

                _battleField.Add(pos, (false, neigh));
            }
        }

        _characters = new List<CharacterController>();

        _factionAlive = new int[] { 0, 0 };

        foreach (var info in firstSpawnQueue) {
            Unit unit = Spawner.SpawnUnit(info.name, info.count);
            AddUnit(unit, info.position, 0);
        }
        foreach (var info in secondSpawnQueue) {
            Unit unit = Spawner.SpawnUnit(info.name, info.count);
            AddUnit(unit, info.position, 1);
        }
    }

    //Простой поиск свободной клетки
    private Vector3Int FindEmpty()
    {
        //Если место не будет найдено, то возвращается вектор с Z = 1
        Vector3Int pos = Vector3Int.forward;
        
        for (int x = 0; x < mapSize.x; x++)
            for (int y = 0; y < mapSize.y; y++)
                if (!_battleField[new Vector3Int(x, y, 0)].isObstacle) {
                    pos = new Vector3Int(x, y, 0);
                }

        return pos;
    }

    //Алгоритм Дейкстры
    private void CalculateDijkstra(Vector3Int pos, int dist, bool init)
    {
        if (init) {
            _dijkstra = new Dictionary<Vector3Int, int>();
            _canAttack = new List<Vector3Int>();
        }

        //Добавляем ячейку в возможные для атаки
        if (!_canAttack.Contains(pos))
            _canAttack.Add(pos);

        if (!_battleField.ContainsKey(pos) || dist > _characters[turn].unit.distance || (_battleField[pos].isObstacle && !init))
            return;

        //Параметр сдвига по гексагональной сетке
        int offset = Mathf.Abs(pos.y % 2);

        if (!_dijkstra.ContainsKey(pos)) {
            _dijkstra.Add(pos, dist);
        }
        else if (dist < _dijkstra[pos]) {
            _dijkstra[pos] = dist;
        } else return;

        foreach (var nPos in _battleField[pos].neighbours)
            CalculateDijkstra(nPos, dist + 1, false);

        if (init) HighliteArea(_dijkstra.Keys, Vector3Int.zero);
    }
    #endregion

    #region Подсветка
    private void Highlite(Vector3Int pos)
    {
        if (!_isAbilityActive) return;

        HighliteArea(_abilityArea, pos);
    }

    private void HighliteArea(IEnumerable<Vector3Int> area, Vector3Int origin)
    {
        highliteMap.ClearAllTiles();

        foreach(var pos in area) {
            int x = Mathf.Abs((origin.y) % 2) & Mathf.Abs(pos.y % 2);
            highliteMap.SetTile(pos + origin + Vector3Int.right * x, highliteTile);
        }
    }
    #endregion

    //Проверка состояния мыши
    private void CheckMouse()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int coordinate = tilemap.WorldToCell(mouseWorldPos);
        coordinate.z = 0;

        if (Input.GetMouseButtonDown(0)) {
            onMouseDown?.Invoke(coordinate);
        } else if (Input.GetMouseButtonUp(0)) {
            onMouseUp?.Invoke(coordinate);
        } 
        if (coordinate != _activeCoord) {
            onMouseEnter?.Invoke(coordinate);
            onMouseExit?.Invoke(_activeCoord);
        }

        _activeCoord = coordinate;
    }

    //Выбор действий при нажатии мыши
    private void OnClick(Vector3Int target)
    {
        if (!canCommand) return;

        if (_dijkstra.ContainsKey(target)) 
            MoveUnit(target);
        else if (_characters[turn].unit.isRange || _canAttack.Contains(target))
            foreach (var character in _characters)
                if (character.tilePos == target) {
                    if (character.faction == _characters[turn].faction) return;

                    InitAttack(character);
                }

    }

    //Добавление юнита
    private void AddUnit(Unit unit, Vector3Int pos, int faction)
    {
        if (_battleField[pos].isObstacle) {
            pos = FindEmpty();
            if (pos.z == 1) return;
        }
        
        CharacterController character = Instantiate(unitPrefab, tilemap.CellToWorld(pos), Quaternion.identity).GetComponent<CharacterController>();

        character.unit = unit;

        for (int i = 0; i < _characters.Count; i++)
            if (_characters[i].unit.initiative < unit.initiative) { 
                _characters.Insert(i, character);
                UpdateIDs();
                return;
            }

        _characters.Add(character);
        UpdateIDs();
        _battleField[pos] = (true, _battleField[pos].neighbours);

        character.unit.onDeath += RemoveUnit;
        character.tilePos = pos;
        character.faction = faction;

        character.unit.onDamageDone += (object obj) => {
            (int dmg, int death) = ((int, int))obj;

            Log(string.Format("{0} получает {1} урона. {2} {0} погибли", character.unit.name, dmg, death)); 
        };

        _factionAlive[faction]++;
    }

    //Удаление юнита
    private void RemoveUnit(object id)
    {
        Debug.Log("Removed");

        CharacterController character = _characters[(int)id];

        _battleField[character.tilePos] = (false, _battleField[character.tilePos].neighbours);
        _factionAlive[character.faction]--;
        if (_factionAlive[character.faction] == 0) Victory(1 - character.faction + 1);

        _characters[(int)id].Death();
        _characters.RemoveAt((int)id);
        UpdateIDs();

        turn--;
        InstantEndTurn();
    }

    private void UpdateIDs()
    {
        for (int i = 0; i < _characters.Count; i++)
            _characters[i].unit.ID = i;
    }

    private void StartTurn(bool reapeat = false)
    {
        //Если есть абилка, активируем кнопку
        if (_characters[turn].unit.abilityData.type != AbilityType.None) {
            abilityBtn.gameObject.SetActive(true);
        } else {
            abilityBtn.gameObject.SetActive(false);
        }

        canCommand = true;
        CalculateDijkstra(tilemap.WorldToCell(_characters[turn].transform.position), 0, true);

        if (!reapeat) _turnRepeats = 0;
        else {
            Log(string.Format("{0} ходит снова!", _characters[turn].unit.name));
            _turnRepeats++;
        }
    }

    private void EndTurn()
    {
        _characters[turn].onMoveEnd -= EndTurn;

        Log(_characters[turn].unit.name);

        bool repeat = !_characters[turn].unit.EndTurn(_turnRepeats);

        if (!repeat) turn = (turn + 1) % _characters.Count;
        StartTurn(repeat);
    }

    private void InstantEndTurn()
    {
        turn = (turn + 1) % _characters.Count;
        StartTurn(false);
    }

    private void MoveUnit(Vector3Int target)
    {
        if (!_battleField.ContainsKey(target)) return;

        canCommand = false;

        Vector3Int oldPos = tilemap.WorldToCell(_characters[turn].transform.position);

        _battleField[oldPos] = (false, _battleField[oldPos].neighbours);
        _battleField[target] = (true, _battleField[target].neighbours);
        _characters[turn].tilePos = target;

        List<Vector3> way = new List<Vector3>();

        int minDist = _dijkstra[target];

        //Самой последней точкой является сама цель перемещения
        way.Add(tilemap.CellToWorld(target));

        while (minDist > 0) {
            List<Vector3Int> neighbours = _battleField[target].neighbours;

            foreach (var pos in neighbours) {
                if (!_dijkstra.ContainsKey(pos)) continue;

                if (_dijkstra[pos] < minDist) {
                    minDist = _dijkstra[pos];
                    target = pos;
                }
            }

            way.Insert(0, tilemap.CellToWorld(target));
        }

        _characters[turn].onMoveEnd += EndTurn;

        _characters[turn].SetWay(way);
    }

    private void InitAttack(CharacterController unit)
    {
        CharacterController curUnit = _characters[turn];

        if (curUnit.unit.isRange && !EnemyNear()) {
            RangeAttack();
            EndTurn();
        } else {
            int minDist = int.MaxValue;
            Vector3Int target = Vector3Int.zero;

            //Поиск ближайшей к врагу точки
            foreach (var pos in _battleField[unit.tilePos].neighbours) {
                if (_dijkstra.ContainsKey(pos) && minDist > _dijkstra[pos]) {
                    minDist = _dijkstra[pos];
                    target = pos;
                }
            }

            //Если таковых нет, выходим
            if (target == Vector3Int.zero) return;

            MoveUnit(target);
            curUnit.onMoveEnd += MeleeAttack;
        }

        void MeleeAttack()
        {
            //Ближняя атака

            curUnit.Attack();
            SetDamage(unit.unit, curUnit.unit.GetRandomDamage());
            curUnit.onMoveEnd -= MeleeAttack;
        }

        void RangeAttack()
        {
            //Дальняя атака

            curUnit.Attack();
            SetDamage(unit.unit, curUnit.unit.GetRandomDamage(true));
        }

        bool EnemyNear()
        {
            foreach (var item in _characters)
                if (item.faction != curUnit.faction && _battleField[curUnit.tilePos].neighbours.Contains(item.tilePos))
                    return true;

            return false;
        }
    }

    private void SetDamage(Unit unit, int amount)
    {
        unit.DealDamage(amount);
    }

    //Регистрация победы
    private void Victory(int faction)
    {
        //Выводим табличку
        Debug.Log(string.Format("Игрок {0} победил!", faction));
    }

    private void Log(string msg)
    {
        logField.text = msg + "\n" + logField.text;
    }

    #region Методы для кнопок
    public void SkipTurn()
    {
        //Особые действия при пропуске хода

        InstantEndTurn();
    }

    public void Restart()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public void Exit()
    {
        Application.Quit();
    }

    public void UseAttackAbility()
    {
        _isAbilityActive = !_isAbilityActive;

        if (_isAbilityActive)
            _abilityArea = _characters[turn].unit.abilityData.area;
        else {
            HighliteArea(_dijkstra.Keys, Vector3Int.zero);
        }
    }
    #endregion
}

public static class Spawner
{
    public static Unit SpawnUnit(UnitName name, int count)
    {
        Unit unit = null;

        switch (name) {
            case UnitName.Knight:
                unit = new Knight(count);
                break;
            case UnitName.Archer:
                unit = new Archer(count);
                break;
        }

        return unit;
    }
}

[System.Serializable]
public class SpawnInfo
{
    public UnitName name;
    public int count = 1;
    public Vector3Int position;
}

public enum AbilityType
{
    None,
    Attack,
    Heal
}