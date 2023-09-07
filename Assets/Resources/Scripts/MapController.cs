using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapController : MonoBehaviour
{
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

    private List<CharacterController> _characters;
    //Пути, найденные алгоритмом Дейкстры
    private Dictionary<Vector3Int, int> _dijkstra;
    private List<Vector3Int> _canAttack;
    private bool canCommand = false;
    private int[] _factionAlive;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        InitiateMap();

        StartTurn();

        //onMouseEnter += Highlite;
        onMouseUp += OnClick;
    }

    // Update is called once per frame
    void Update()
    {
        CheckMouse();
    }

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

    #region Подсветка
    private void Highlite(Vector3Int coord)
    {
        highliteMap.ClearAllTiles();

        highliteMap.SetTile(coord, highliteTile);
        highliteMap.RefreshAllTiles();
    }

    private void HighliteArea(IEnumerable<Vector3Int> coords)
    {
        highliteMap.ClearAllTiles();

        foreach(var coord in coords) {
            highliteMap.SetTile(coord, highliteTile);
        }
    }
    #endregion

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

        character.onMoveEnd += EndTurn;
        character.unit.onDeath += RemoveUnit;
        character.tilePos = pos;
        character.faction = faction;

        _factionAlive[faction]++;
    }

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
        EndTurn();
    }

    private void Victory(int faction)
    {
        //Выводим табличку
        Debug.Log(string.Format("Игрок {0} победил!", faction));
    }

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

    private void UpdateIDs()
    {
        for (int i = 0; i < _characters.Count; i++)
            _characters[i].unit.ID = i;
    }

    private void StartTurn()
    {
        canCommand = true;
        CalculateDijkstra(tilemap.WorldToCell(_characters[turn].transform.position), 0, true);
    }

    private void EndTurn()
    {
        turn = (turn + 1) % _characters.Count;
        StartTurn();
    }

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

        if (init) HighliteArea(_dijkstra.Keys);
    }

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

        _characters[turn].SetWay(way);
    }

    private void InitAttack(CharacterController unit)
    {
        CharacterController self = _characters[turn];

        if (self.unit.isRange) {
            Attack();
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
            self.onMoveEnd += Attack;
        }

        void Attack()
        {
            Debug.Log("attack");

            self.Attack();
            SetDamage(unit.unit, self.unit.GetRandomDamage());
            self.onMoveEnd -= Attack;
        }
    }

    private void SetDamage(Unit unit, int amount)
    {
        Debug.Log(amount);
        unit.DealDamage(amount);
    }
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