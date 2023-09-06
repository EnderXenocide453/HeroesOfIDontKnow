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

    private List<CharacterController> _characters;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        InitiateMap();

        AddUnit(new Knight(), Vector3Int.zero, 1);

        onMouseEnter += Highlite;
    }

    // Update is called once per frame
    void Update()
    {
        CheckMouse();
    }

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
    }

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
            Debug.Log(coordinate);
            onMouseEnter?.Invoke(coordinate);
            onMouseExit?.Invoke(_activeCoord);
        }

        _activeCoord = coordinate;
    }

    private void AddUnit(Unit unit, Vector3Int pos, int count)
    {
        CharacterController character = Instantiate(unitPrefab, tilemap.CellToWorld(pos), Quaternion.identity).GetComponent<CharacterController>();

        character.unit = unit;
        character.count = count;

        for (int i = 0; i < _characters.Count; i++)
            if (_characters[i].unit.initiative < unit.initiative) { 
                _characters.Insert(i, character);
                UpdateIDs();
                return;
            }

        _characters.Add(character);
        UpdateIDs();
    }

    private void UpdateIDs()
    {
        for (int i = 0; i < _characters.Count; i++)
            _characters[i].ID = i;
    }
}
