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
    private Dictionary<Vector3Int, int> _battleField;
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
    public SpawnInfo[] spawnQueue;

    public int turn { get; private set; } = 0;

    private List<CharacterController> _characters;
    private Dictionary<Vector3Int, (int dist, Vector3Int[] neighbours)> _dijkstra;
    private bool canCommand = false;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        InitiateMap();

        StartTurn();

        //onMouseEnter += Highlite;
        onMouseUp += MoveUnit;
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
        _battleField = new Dictionary<Vector3Int, int>();

        Vector3Int offset = new Vector3Int(mapSize.x / 2, mapSize.y / 2, 0);

        for (int i = 0; i < mapSize.x; i++) {
            for (int j = 0; j < mapSize.y; j++) {
                Vector3Int pos = new Vector3Int(i, j, 0) - offset;

                tilemap.SetTile(pos, normalTile);
                _battleField.Add(pos, 0);
            }
        }

        _characters = new List<CharacterController>();

        foreach (var info in spawnQueue) {
            Unit unit = Spawner.SpawnUnit(info.name, info.count);
            AddUnit(unit, info.position);
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

    private void AddUnit(Unit unit, Vector3Int pos)
    {
        if (_battleField[pos] == 1) {
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
        _battleField[pos] = 1;

        character.onMoveEnd += EndTurn;
        character.unit.onDeath += RemoveUnit;
    }

    private void RemoveUnit(object id)
    {
        _characters.RemoveAt((int)id);
    }

    private Vector3Int FindEmpty()
    {
        //Если место не будет найдено, то возвращается вектор с Z = 1
        Vector3Int pos = Vector3Int.forward;
        
        for (int x = 0; x < mapSize.x; x++)
            for (int y = 0; y < mapSize.y; y++)
                if (_battleField[new Vector3Int(x, y, 0)] == 0) {
                    pos = new Vector3Int(x, y, 0);
                }

        return pos;
    }

    private void UpdateIDs()
    {
        for (int i = 0; i < _characters.Count; i++)
            _characters[i].ID = i;
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
        if (init) _dijkstra = new Dictionary<Vector3Int, (int, Vector3Int[])>();

        if (!_battleField.ContainsKey(pos) || dist > _characters[turn].unit.distance || (_battleField[pos] == 1 && !init))
            return;

        //Параметр сдвига по гексагональной сетке
        int offset = Mathf.Abs(pos.y % 2);

        if (!_dijkstra.ContainsKey(pos)) {
            Vector3Int[] neigh = new Vector3Int[]
            {
                pos - new Vector3Int(1 - offset, -1, 0),
                pos - new Vector3Int(1, 0, 0),
                pos - new Vector3Int(1 - offset, 1, 0),
                pos - new Vector3Int(0 - offset, 1, 0),
                pos - new Vector3Int(0 - offset, -1, 0),
                pos - new Vector3Int(-1, 0, 0)
            };

            _dijkstra.Add(pos, (dist, neigh));
        }
        else if (dist < _dijkstra[pos].dist) {
            _dijkstra[pos] = (dist, _dijkstra[pos].neighbours);
        } else return;

        foreach (var nPos in _dijkstra[pos].neighbours)
            CalculateDijkstra(nPos, dist + 1, false);

        if (init) HighliteArea(_dijkstra.Keys);
    }

    private void MoveUnit(Vector3Int target)
    {
        if (!canCommand || !_battleField.ContainsKey(target) || !_dijkstra.ContainsKey(target)) return;

        canCommand = false;

        Vector3Int oldPos = tilemap.WorldToCell(_characters[turn].transform.position);

        _battleField[oldPos] = 0;
        _battleField[target] = 1;

        List<Vector3> way = new List<Vector3>();

        int minDist = _dijkstra[target].dist;

        //Самой последней точкой является сама цель перемещения
        way.Add(tilemap.CellToWorld(target));

        while (minDist > 0) {
            Vector3Int[] neighbours = _dijkstra[target].neighbours;

            foreach (var pos in neighbours) {
                if (!_dijkstra.ContainsKey(pos)) continue;

                if (_dijkstra[pos].dist < minDist) {
                    minDist = _dijkstra[pos].dist;
                    target = pos;
                }
            }

            way.Insert(0, tilemap.CellToWorld(target));
        }

        _characters[turn].SetWay(way);
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