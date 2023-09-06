using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapController : MonoBehaviour
{
    //Карта хексов
    public Tilemap tilemap;
    public Tile normalTile;
    //Карта подсветки хексов
    public Tilemap highliteMap;
    public Tile highliteTile;

    //Параметры карты
    public Vector2Int mapSize;

    //События взаимодействия с тайлами
    public delegate void TileHandler(Vector3Int coord);
    public event TileHandler onMouseEnter;
    public event TileHandler onMouseExit;
    public event TileHandler onMouseDown;
    public event TileHandler onMouseUp;

    //Выбранный тайл
    private Vector3Int _activeCoord = Vector3Int.zero;
    //Поле боя
    /// <summary>
    /// 0-пустая клетка
    /// 1-препятствие
    /// </summary>
    private Dictionary<Vector3Int, int> _battleField;

    // Start is called before the first frame update
    void Start()
    {
        InitiateMap();

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
}
