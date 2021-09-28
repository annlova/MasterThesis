using System;
using System.Collections.Generic;
using TMPro;
using TMPro.EditorUtilities;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private int _mapWidth;
    [SerializeField] private int _mapHeight;
    [SerializeField] private int _elevationRangeMin;
    [SerializeField] private int _elevationRangeMax;

    [SerializeField] private GameObject _tileObject;
    [SerializeField] private int _acreSize;

    [SerializeField] private int _maxCliffEat;
    [SerializeField] private CliffTile[] _cliffTiles;
    
    private World _world;

    private ComputeBuffer buffer;
    
    // Start is called before the first frame update
    void Start()
    {
        _world = new World(_mapWidth, _mapHeight, _acreSize, _elevationRangeMin, _elevationRangeMax, _maxCliffEat, _cliffTiles);
        var highestIslandIndex = _world.GenerateAcres();
        temp = highestIslandIndex;
        
        var acres = _world._acres;

        var colors = new Vector4[_acreSize * _acreSize * _mapWidth * _mapHeight];
        for (int acreX = 0; acreX < _mapWidth; acreX++)
        {
            for (int acreY = 0; acreY < _mapHeight; acreY++)
            {
                var acre = acres[acreX, acreY];
                
                for (int tileX = 0; tileX < _acreSize; tileX++)
                {
                    for (int tileY = 0; tileY < _acreSize; tileY++)
                    {
                        var tile = acre._tiles[tileX, tileY];
                        var color = (float) acre._islandIndex / highestIslandIndex;
                        color /= 2.0f;
                        color += 0.5f;
                        var colorIndex = acreX * _acreSize + tileX + acreY * _acreSize * _acreSize * _mapWidth +
                                         tileY * _acreSize * _mapWidth;
                        colors[colorIndex] = new Vector4(color, 0.3f, 0.3f, 1.0f);
                        // colors[colorIndex] = new Vector4(Random.Range(0.5f, 1.0f),Random.Range(0.5f, 1.0f),Random.Range(0.5f, 1.0f), 1.0f);
                        var position = new Vector3(acreX * _acreSize + tileX + 0.5f, tile._elevation * 2, (_mapHeight - 1 - acreY) * _acreSize + (_acreSize - 1 - tileY) + 0.5f);
                        if (tile._isCliff)
                        {
                            for (int i = tile._floor; i < tile._elevation; i++)
                            {
                                var p = new Vector3(position.x, i * 2, position.z);
                                Instantiate(tile._cliffTile._prefab, p, tile._cliffTile._prefab.transform.rotation, transform);
                            }

                            var floorPosition = new Vector3(position.x, tile._floor * 2, position.z);
                            Instantiate(_tileObject, floorPosition, Quaternion.identity, transform);
                        }
                        else
                        {
                            Instantiate(_tileObject, position, Quaternion.identity, transform);
                        }
                    }
                }
            }
        }

        // colors[_acreSize * _mapWidth - 1] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        // colors[_acreSize * _mapWidth] = new Vector4(0.0f, 1.0f, 1.0f, 1.0f);
        Combine();
        var renderer = GetComponent<Renderer>();
        renderer.sharedMaterial.SetInt("_AcreSize", _acreSize);
        renderer.sharedMaterial.SetInt("_MapWidth", _mapWidth);
        renderer.sharedMaterial.SetInt("_MapHeight", _mapHeight);
        buffer = new ComputeBuffer(colors.Length, sizeof(float) * 4);
        buffer.SetData(colors);
        renderer.sharedMaterial.SetBuffer("_TileValues", buffer);
    }

    public void OnDisable()
    {
        buffer.Release();
    }

    public void Combine()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length - 1];
        
        int i = 1;
        while (i < meshFilters.Length)
        {
            combine[i - 1].mesh = meshFilters[i].mesh;
            combine[i - 1].transform = meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false);
            var collider = meshFilters[i].gameObject.transform.Find("Collider");
            if (collider)
            {
                collider.transform.SetParent(transform);
            }
            
            i++;
        }
        transform.GetComponent<MeshFilter>().mesh = new Mesh();
        transform.GetComponent<MeshFilter>().mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        transform.GetComponent<MeshFilter>().mesh.CombineMeshes(combine);
        transform.gameObject.SetActive(true);
    }

    private int temp = 0;
    // Update is called once per frame
    void Update()
    {
        // if (Input.GetKeyDown(KeyCode.K))
        // {
        //     _world.MarkCliffTilesIteration(temp);
        //     
        //             var acres = _world._acres;
        //
        // for (int acreX = 0; acreX < _mapWidth; acreX++)
        // {
        //     for (int acreY = 0; acreY < _mapHeight; acreY++)
        //     {
        //         var acre = acres[acreX, acreY];
        //         
        //         for (int tileX = 0; tileX < _acreSize; tileX++)
        //         {
        //             for (int tileY = 0; tileY < _acreSize; tileY++)
        //             {
        //                 var tile = acre._tiles[tileX, tileY];
        //                 var position = new Vector3(acreX * _acreSize + tileX, tile._elevation * 2, (_mapHeight - 1 - acreY) * _acreSize + (_acreSize - 1 - tileY));
        //                 if (tile._isCliff)
        //                 {
        //                     for (int i = tile._floor; i < tile._elevation; i++)
        //                     {
        //                         var p = new Vector3(position.x, i * 2, position.z);
        //                         Instantiate(tile._cliffTile._prefab, p, tile._cliffTile._prefab.transform.rotation, transform);
        //                     }
        //
        //                     var floorPosition = new Vector3(position.x, tile._floor * 2, position.z);
        //                     Instantiate(_tileObject, floorPosition, Quaternion.identity, transform);
        //                 }
        //             }
        //         }
        //     }
        // }
        // }
    }
}

public class World
{
    /// <summary>Width in acres.</summary>
    private int _width;
    /// <summary>Height in acres.</summary>
    private int _height;
    /// <summary>Number of tiles along an acre edge.</summary>
    private int _acreSize;
    /// <summary>Acre data.</summary>
    public Acre[,] _acres { get; }

    private int _elevationRangeMin;
    private int _elevationRangeMax;
    private int _maxLayers;

    private int _maxCliffEat;
    private CliffTile[] _cliffTiles;
    
    public World(int width, int height, int acreSize, int elevationRangeMin, int elevationRangeMax, int maxCliffEat, CliffTile[] cliffTiles)
    {
        _width = width;
        _height = height;
        _acreSize = acreSize;
        _acres = new Acre[width, height];

        _elevationRangeMin = elevationRangeMin;
        _elevationRangeMax = elevationRangeMax;

        _maxCliffEat = maxCliffEat;
        _cliffTiles = cliffTiles;
    }
    
    /// <summary>
    /// Generate layout of all acres and data corresponding to an acre "chunk".
    /// </summary>
    public int GenerateAcres()
    {
        // Calculate number of layers
        _maxLayers = Random.Range(_elevationRangeMin, _elevationRangeMax + 1);
        
        // Set top and bottom acres to highest and lowest elevation
        for (int i = 0; i < _width; i++)
        {
            var top = new Acre(i, 0, _maxLayers);
            _acres[i, 0] = top;

            var bottom = new Acre(i, _height - 1, 0);
            _acres[i, _height - 1] = bottom;
        }

        // First basic acre generation algorithm TEMPORARY
        for (int x = 0; x < _width; x++)
        {
            for (int y = 1; y < _height - 1; y++)
            {
                int previousElevation = _acres[x, y - 1]._elevation;

                bool changeElevation = Random.Range(0, 3) > 1;
                var elevation = previousElevation;
                if (changeElevation)
                {
                    elevation--;
                }

                if (elevation < 0)
                {
                    elevation = 0;
                }
                
                // TODO remove
                if (x == 0 && y < 2)
                {
                    elevation = 2;
                }
                else if (x == 0 && y >= 2)
                {
                    elevation = 0;
                }
                else if (x == 1 && y < 3)
                {
                    elevation = 1;
                }

                var acre = new Acre(x, y, elevation);
                _acres[x, y] = acre;
            }
        }

        foreach (var a in _acres)
        {
            InitializeAcreTiles(a);
        }

        // Calculate which "island" an acre belongs to
        var highestIslandIndex = CalculateIslands();
        // Calculate what cliff orientations the acres have
        CalculateAcresCliffOrientations();
        // "Walk" each island perimeter, marking cliff tiles along the way
        MarkCliffTiles(highestIslandIndex);

        LevelTerrain();
        
        CalculateCliffFloors();

        return highestIslandIndex;
    }

    /// <summary>
    /// Initializes Acre tiles array with FLAT tiles.
    /// </summary>
    /// <param name="acre">The acre to work with.</param>
    public void InitializeAcreTiles(Acre acre)
    {
        acre._size = _acreSize;
        acre._tiles = new Tile[acre._size, acre._size];
        for (int x = 0; x < acre._size; x++)
        {
            for (int y = 0; y < acre._size; y++)
            {
                acre._tiles[x, y] = new Tile(acre._elevation);
            }
        }
    }

    private int CalculateIslands()
    {
        int islandIndex = 0;
        for (int layer = _maxLayers; layer >= 0; layer--)
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (_acres[x, y]._islandIndex < 0 &&
                        _acres[x, y]._elevation == layer)
                    {
                        ConnectNeighbours(_acres[x, y], islandIndex);
                        islandIndex++;
                    }
                }
            }
        }

        return islandIndex - 1;
    }

    private void ConnectNeighbours(Acre acre, int islandIndex)
    {
        int x = acre._x;
        int y = acre._y;
        acre._islandIndex = islandIndex;

        Acre[] neighbours = new Acre[4];
        neighbours[0] = x > 0 ? _acres[x - 1, y] : null;
        neighbours[1] = y > 0 ? _acres[x, y - 1] : null;
        neighbours[2] = x < _width - 1 ? _acres[x + 1, y] : null;
        neighbours[3] = y < _height - 1 ? _acres[x, y + 1] : null;

        foreach (var neighbour in neighbours)
        {
            if (neighbour != null &&
                neighbour._islandIndex != islandIndex &&
                acre._elevation == neighbour._elevation)
            {
                ConnectNeighbours(neighbour, islandIndex);
            }
        }
    }

    private void CalculateAcresCliffOrientations()
    {
        foreach (var acre in _acres)
        {
            CalculateCliffOrientations(acre);
        }
    }

    private void CalculateCliffOrientations(Acre acre)
    {
        var x = acre._x;
        var y = acre._y;

        int maxW = _width - 1;
        int maxH = _height - 1;
        
        var w = x > 0 ? _acres[x - 1, y] : null;
        var n = y > 0 ? _acres[x, y - 1] : null;
        var e = x < maxW ? _acres[x + 1, y] : null;
        var s = y < maxH ? _acres[x, y + 1] : null;
        
        var nw = x > 0 && y > 0 ? _acres[x - 1, y - 1] : null;
        var ne = x < maxW && y > 0 ? _acres[x + 1, y - 1] : null;
        var se = x < maxW && y < maxH ? _acres[x + 1, y + 1] : null;
        var sw = x > 0 && y < maxH ? _acres[x - 1, y + 1] : null;

        if (w != null && acre._elevation > w._elevation) { acre._hasWestCliff = true; }

        if (n != null && acre._elevation > n._elevation)
        {
            throw new Exception("North acre elevation should not be lower!");
        }
        if (e != null && acre._elevation > e._elevation) { acre._hasEastCliff = true; }
        if (s != null && acre._elevation > s._elevation) { acre._hasSouthCliff = true; }

        if (n != null && w != null && nw != null &&
            acre._islandIndex == n._islandIndex &&
            acre._islandIndex == w._islandIndex &&
            nw._elevation < acre._elevation)
        {
            throw new Exception("Not possible!");
        }

        if (n != null && e != null && ne != null &&
            acre._islandIndex == n._islandIndex &&
            acre._islandIndex == e._islandIndex &&
            ne._elevation < acre._elevation)
        {
            throw new Exception("Not possible!");
        }

        if (s != null && e != null && se != null &&
            (acre._islandIndex == s._islandIndex &&
             acre._islandIndex == e._islandIndex &&
             se._elevation < acre._elevation ||
             s._elevation == e._elevation &&
             se._elevation < s._elevation))
        {
            acre._hasSouthEastCliff = true;
        }

        if (s != null && w != null && sw != null &&
            (acre._islandIndex == s._islandIndex &&
            acre._islandIndex <= w._elevation &&
            sw._elevation < acre._elevation ||
            s._elevation <= w._elevation &&
            sw._elevation < s._elevation))
        {
            acre._hasSouthWestCliff = true;
        }
    }
    
    public void MarkCliffTiles(int highestIslandIndex)
    {
        for (int islandIndex = 0; islandIndex <= highestIslandIndex; islandIndex++)
        {
            var startAcre = FindStartAcre(islandIndex);
            if (startAcre._elevation == 0 || startAcre._cliffWalked)
            {
                continue;
            }
            new CliffWalkAgent(_acres, _width, _height, _cliffTiles, startAcre, _maxCliffEat).Walk(_acres, _width, _height);
        }
    }

    private int temp = 0;
    public void MarkCliffTilesIteration(int highestIslandIndex)
    {
        if (temp > highestIslandIndex)
        {
            return;
        }
        var startAcre = FindStartAcre(temp++);
        if (startAcre._elevation == 0 || startAcre._cliffWalked)
        {
            return;
        }
        new CliffWalkAgent(_acres, _width, _height, _cliffTiles, startAcre, _maxCliffEat).Walk(_acres, _width, _height);
    }

    private Acre FindStartAcre(int islandIndex)
    {
        Acre acre = null;
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (_acres[x, y]._islandIndex == islandIndex)
                {
                    acre = _acres[x, y];
                    if (acre._hasWestCliff)
                    {
                        return acre;
                    }
                }
            }

            if (acre != null)
            {
                return acre;
            }
        }

        throw new Exception("This code should not be reached :(.");
    }

    private bool RemoveInsideCliffs()
    {
        bool cliffWasRemoved = false;
        for (int y = 0; y < _acreSize * _height; y++)
        {
            for (int x = 0; x < _acreSize * _width; x++)
            {
                var p = new Vector2Int(x, y);
                var acrePos = new Vector2Int(p.x / _acreSize, p.y / _acreSize);
                var tilePos = new Vector2Int(p.x % _acreSize, p.y % _acreSize);
                var tile = _acres[acrePos.x, acrePos.y]._tiles[tilePos.x, tilePos.y];
                if (tile._isCliff)
                {
                    var neighbours = new Vector2Int[8];
                    neighbours[0] = p.x > 0 && p.y > 0 ? new Vector2Int(p.x - 1, p.y - 1) : new Vector2Int(-1, -1);
                    neighbours[1] = p.x > 0 ? new Vector2Int(p.x - 1, p.y) : new Vector2Int(-1, -1);
                    neighbours[2] = p.x > 0 && p.y < _acreSize * _height - 1 ? new Vector2Int(p.x - 1, p.y + 1) : new Vector2Int(-1, -1);
                    neighbours[3] = p.y > 0 ? new Vector2Int(p.x, p.y - 1) : new Vector2Int(-1, -1);
                    neighbours[4] = p.y < _acreSize * _height - 1 ? new Vector2Int(p.x, p.y + 1) : new Vector2Int(-1, -1);
                    neighbours[5] = p.x < _acreSize * _width - 1 && p.y > 0 ? new Vector2Int(p.x + 1, p.y - 1) : new Vector2Int(-1, -1);
                    neighbours[6] = p.x < _acreSize * _width - 1 ? new Vector2Int(p.x + 1, p.y) : new Vector2Int(-1, -1);
                    neighbours[7] = p.x < _acreSize * _width - 1 && p.y < _acreSize * _height - 1? new Vector2Int(p.x + 1, p.y + 1) : new Vector2Int(-1, -1);

                    int elevation = -1;
                    bool remove = true;
                    foreach (var neighbour in neighbours)
                    {
                        if (neighbour != new Vector2Int(-1, -1))
                        {
                            var np = new Vector2Int(neighbour.x, neighbour.y);
                            var nAcrePos = new Vector2Int(np.x / _acreSize, np.y / _acreSize);
                            var nTilePos = new Vector2Int(np.x % _acreSize, np.y % _acreSize);
                            var nTile = _acres[nAcrePos.x, nAcrePos.y]._tiles[nTilePos.x, nTilePos.y];
                            if (elevation == -1)
                            {
                                elevation = nTile._elevation;
                            }

                            // if (nTile._isCliff)
                            // {
                            //     continue;
                            // }
                            if (nTile._elevation != elevation)
                            {
                                remove = false;
                            }
                        }
                    }

                    if (remove)
                    {
                        tile._isCliff = false;
                        tile._isLeveled = false;
                        tile._elevation = elevation;

                        cliffWasRemoved = true;
                    }
                }
            }
        }

        return cliffWasRemoved;
    }
    
    private void LevelTerrain()
    {
        bool relevelTerrain = false;

        do
        {
            for (int y = 0; y < _acreSize * _height; y++)
            {
                for (int x = 0; x < _acreSize * _width; x++)
                {
                    ComputeConnectedTiles(x, y);
                }
            }
        
            // relevelTerrain = RemoveInsideCliffs();
            // if (relevelTerrain)
            // {
            //     Debug.Log("Hello old friend");
            // }
            // // reset
            // for (int y = 0; y < _acreSize * _height; y++)
            // {
            //     for (int x = 0; x < _acreSize * _width; x++)
            //     {
            //         var p = new Vector2Int(x, y);
            //         var acrePos = new Vector2Int(p.x / _acreSize, p.y / _acreSize);
            //         var tilePos = new Vector2Int(p.x % _acreSize, p.y % _acreSize);
            //         var tile = _acres[acrePos.x, acrePos.y]._tiles[tilePos.x, tilePos.y];
            //         tile._isLeveled = false;
            //     }
            // }
        } while (relevelTerrain);
    }

    private void ComputeConnectedTiles(int x, int y)
    {
        var list = new List<Tile>();
        int lowest = _maxLayers;
        
        var toVisit = new List<Vector2Int>();
        toVisit.Add(new Vector2Int(x, y));

        for (int i = 0; i < toVisit.Count; i++)
        {
            var p = toVisit[i];
            var tile = GetTile(p);
            if (tile == null || tile._isLeveled || tile._isCliff)
            {
                continue;
            }
            
            list.Add(tile);
            if (tile._elevation < lowest)
            {
                lowest = tile._elevation;
            }
            tile._isLeveled = true;
            
            var neighbours = new Vector2Int[4];
            neighbours[0] = new Vector2Int(p.x - 1, p.y);//p.x > 0 ? new Vector2Int(p.x - 1, p.y) : new Vector2Int(-1, -1);
            neighbours[1] = new Vector2Int(p.x, p.y - 1);//p.y > 0 ? new Vector2Int(p.x, p.y - 1) : new Vector2Int(-1, -1);
            neighbours[2] = new Vector2Int(p.x + 1, p.y);//p.x < _acreSize * _width - 1 ? new Vector2Int(p.x + 1, p.y) : new Vector2Int(-1, -1);
            neighbours[3] = new Vector2Int(p.x, p.y + 1);//p.y < _acreSize * _height - 1 ? new Vector2Int(p.x, p.y + 1) : new Vector2Int(-1, -1);
            
            foreach (var neighbour in neighbours)
            {
                toVisit.Add(neighbour);
            }
        }
        
        if (list.Count == 1)
        {
            // An only tile, choose lowest surrounding elevation
            var p = toVisit[0];
            var neighbours = new Vector2Int[4];
            neighbours[0] = new Vector2Int(p.x - 1, p.y);
            neighbours[1] = new Vector2Int(p.x, p.y - 1);
            neighbours[2] = new Vector2Int(p.x + 1, p.y);
            neighbours[3] = new Vector2Int(p.x, p.y + 1);
            
            foreach (var neighbour in neighbours)
            {
                var t = GetTile(neighbour);
                if (t != null && t._elevation < lowest)
                {
                    lowest = t._elevation;
                }
            }
        }
        
        foreach (var tile in list)
        {
            tile._elevation = lowest;
        }
    }

    private Tile GetTile(Vector2Int p)
    {
        var acrePos = new Vector2Int(p.x / _acreSize, p.y / _acreSize);
        var tilePos = new Vector2Int(p.x % _acreSize, p.y % _acreSize);
        if (acrePos.x < 0 || acrePos.x >= _width ||
            acrePos.y < 0 || acrePos.y >= _height ||
            tilePos.x < 0 || tilePos.x >= _acreSize ||
            tilePos.y < 0 || tilePos.y >= _acreSize)
        {
            return null;
        }
        
        return _acres[acrePos.x, acrePos.y]._tiles[tilePos.x, tilePos.y];
    }

    private void CalculateCliffFloors()
    {
        for (int y = 0; y < _acreSize * _height; y++)
        {
            for (int x = 0; x < _acreSize * _width; x++)
            {
                var p = new Vector2Int(x, y);
                var acrePos = new Vector2Int(p.x / _acreSize, p.y / _acreSize);
                var tilePos = new Vector2Int(p.x % _acreSize, p.y % _acreSize);
                var tile = _acres[acrePos.x, acrePos.y]._tiles[tilePos.x, tilePos.y];

                if (!tile._isCliff)
                {
                    continue;
                }

                var neighbours = new Vector2Int[8];
                neighbours[0] = new Vector2Int(p.x - 1, p.y - 1);
                neighbours[1] = new Vector2Int(p.x - 1, p.y);
                neighbours[2] = new Vector2Int(p.x - 1, p.y + 1);
                neighbours[3] = new Vector2Int(p.x, p.y - 1);
                neighbours[4] = new Vector2Int(p.x, p.y + 1);
                neighbours[5] = new Vector2Int(p.x + 1, p.y - 1);
                neighbours[6] = new Vector2Int(p.x + 1, p.y);
                neighbours[7] = new Vector2Int(p.x + 1, p.y + 1);

                int lowest = _maxLayers;
                foreach (var neighbour in neighbours)
                {
                    var t = GetTile(neighbour);
                    if (t != null)
                    {
                        if (t._elevation < lowest)
                        {
                            lowest = t._elevation;
                        }

                        // if (t._isCliff)
                        // {
                        //     if (lowest < t._floor)
                        //     {
                        //         t._floor = lowest;
                        //     }
                        //     else
                        //     {
                        //         lowest = t._floor;
                        //     }
                        // }
                    }
                }

                tile._floor = lowest;
            }
        }
    }
}

public class Acre
{
    public int _x { get; }
    public int _y { get; }
    public int _elevation { get; }

    public int _islandIndex;

    public bool _hasWestCliff;
    public bool _hasEastCliff;
    public bool _hasSouthEastCliff;
    public bool _hasSouthCliff;
    public bool _hasSouthWestCliff;

    public bool _cliffWalked;
    
    public int _size;
    public Tile[,] _tiles;

    public Acre(int x, int y, int elevation)
    {
        _x = x;
        _y = y;
        _elevation = elevation;
        
        _hasWestCliff = false;
        _hasEastCliff = false;
        _hasSouthEastCliff = false;
        _hasSouthCliff = false;
        _hasSouthWestCliff = false;

        _cliffWalked = false;

        _islandIndex = -1;
    }

    public bool HasPerpendicularCliff(Vector2Int dir)
    {
        if (dir == Vector2Int.right && _hasWestCliff)
        {
            return true;
        } else if (dir == Vector2Int.down && _hasSouthCliff)
        {
            return true;
        }

        return false;
    }
}

public class Tile
{
    public int _elevation;
    public bool _isCliff;
    public Vector2Int _cliffDirection;
    public CliffTile _cliffTile;
    public int _floor;

    public bool _isLeveled;

    public Tile(int elevation)
    {
        _elevation = elevation;
        _floor = elevation;
    }
    
    public enum TileType
    {
        None,
        Flat,
        
        EdgeN,
        EdgeE,
        EdgeS,
        EdgeW,
        
        EdgeMidN,
        EdgeMidE,
        EdgeMidS,
        EdgeMidW,
        
        MidMidNE,
        MidMidSE,
        MidMidSW,
        MidMidNW,
        
        MidMidNEInv,
        MidMidSEInv,
        MidMidSWInv,
        MidMidNWInv,
        
        CornerMidNE,
        CornerMidSE,
        CornerMidSW,
        CornerMidNW,
        
        CornerMidNEInv,
        CornerMidSEInv,
        CornerMidSWInv,
        CornerMidNWInv,
        
        CornerMidMirNE,
        CornerMidMirSE,
        CornerMidMirSW,
        CornerMidMirNW,
        
        CornerMidMirNEInv,
        CornerMidMirSEInv,
        CornerMidMirSWInv,
        CornerMidMirNWInv,
    }
}

[Serializable]
public class CliffTile
{
    public GameObject _prefab;

    public CliffTileRule[] _rules;
}

[Serializable]
public class CliffTileRule
{
    public int _index;
    public Vector2Int _offset;
    public Vector2Int _direction;
}

public class WalkState
{
    public int _tries;
    public int _selectedRule;
    public Vector2Int _pos;

    public WalkState(int tries, int selectedRule, Vector2Int pos)
    {
        _tries = tries;
        _selectedRule = selectedRule;
        _pos = pos;
    }
}

public class CliffWalkAgent
{
    private Acre[,] _acres;
    private int _width;
    private int _height;
    
    private CliffTile[] _cliffTiles;

    private Acre _startAcre;
    private Acre _currentAcre;
    private int _cliffElevation;
    
    private Vector2Int _forward;
    private Vector2Int _right;
    private Vector2Int _pos;
    
    private int _acreSize;
    private int _maxCliffEat;

    private bool _noDirectionChange;

    private Stack<WalkState> _history;
    
    public CliffWalkAgent(Acre[,] acres, int width, int height, CliffTile[] cliffTiles, Acre startAcre, int maxCliffEat)
    {
        _acres = acres;
        _width = width;
        _height = height; 
        _cliffTiles = cliffTiles;
        _startAcre = startAcre;
        _currentAcre = startAcre;
        _cliffElevation = startAcre._elevation;
        _acreSize = startAcre._size;
        _maxCliffEat = maxCliffEat;
        _noDirectionChange = false;
        _history = new Stack<WalkState>();
        CalculateCliffWalkStart(_currentAcre);
    }

    private void CalculateCliffWalkStart(Acre startAcre)
    {
        if (startAcre._hasWestCliff)
        {
            _forward = new Vector2Int(0, 1);
            _right = new Vector2Int(-1, 0);

            var startX = Random.Range(0, _maxCliffEat);
            var startY = 0;
            
            var neighbour = GetNeighbourAcre(-_forward);
            if (neighbour != null &&
                neighbour._hasSouthCliff)
            {
                bool done = false;
                for (int y = _acreSize - 1; y >= _acreSize - _maxCliffEat; y--)
                {
                    for (int x = 0; x < _maxCliffEat; x++)
                    {
                        var t = neighbour._tiles[x, y];
                        if (t._isCliff)
                        {
                            startX = x;
                            startY = y + 1;
                            if (startY >= _acreSize)
                            {
                                startY = 0;
                            }
                            else
                            {
                                _currentAcre = neighbour;
                                _noDirectionChange = true;
                            }
                            
                            done = true;
                            break;
                        }
                    }

                    if (done)
                    {
                        break;
                    }
                }
            }
            else if (neighbour != null &&
                     neighbour._hasSouthWestCliff)
            {
                neighbour = GetNeighbourAcre(-_forward + _right);
                if (neighbour != null &&
                    neighbour._hasEastCliff)
                {
                    bool done = false;
                    for (int x = _acreSize - 1; x >= _acreSize - _maxCliffEat; x--)
                    {
                        for (int y = _acreSize - 1; y >= _acreSize - _maxCliffEat; y--)
                        {
                            var t = neighbour._tiles[x, y];
                            if (t._isCliff)
                            {
                                startX = x + 1;
                                startY = y;
                                if (startX >= _acreSize)
                                {
                                    startX = 0;
                                    _currentAcre = GetNeighbourAcre(-_forward);
                                    _noDirectionChange = true;
                                }
                                else
                                {
                                    _currentAcre = neighbour;
                                    _noDirectionChange = true;
                                    ChangeDirection(Vector2Int.right);
                                }

                                done = true;
                                break;
                            }
                        }

                        if (done)
                        {
                            break;
                        }
                    }
                }
            }
            
            _pos = new Vector2Int(startX, startY);
        }
        else if (startAcre._hasSouthCliff)
        {
            _forward = new Vector2Int(1, 0);
            _right = new Vector2Int(0, 1);
            
            var startX = 0;
            var startY = _acreSize - 1 - Random.Range(0, _maxCliffEat);

            var neighbour = GetNeighbourAcre(-_forward);
            if (neighbour != null &&
                neighbour._hasEastCliff)
            {
                bool done = false;
                for (int x = _acreSize - 1; x >= _acreSize - _maxCliffEat; x--)
                {
                    for (int y = _acreSize - 1; y >= _acreSize - _maxCliffEat; y--)
                    {
                        var t = neighbour._tiles[x, y];
                        if (t._isCliff)
                        {
                            startX = x + 1;
                            startY = y;
                            if (startX >= _acreSize)
                            {
                                startX = 0;
                            }
                            else
                            {
                                _currentAcre = neighbour;
                                _noDirectionChange = true;
                            }

                            done = true;
                            break;
                        }
                    }

                    if (done)
                    {
                        break;
                    }
                }
            }
            
            _pos = new Vector2Int(startX, startY);
        }
        else
        {
            // This should not happen! Cliff orientations calculation wrong probably.
            Assert.IsTrue(false);
        }
    }

    public void Walk(Acre[,] acres, int w, int h)
    {
        var tiles = _currentAcre._tiles;
        var tile = tiles[_pos.x, _pos.y];
        
        // TODO: Think of something robust
        if (_forward.y > 0)
        {
            tile._cliffTile = _cliffTiles[25];
        } else if (_forward.x > 0)
        {
            tile._cliffTile = _cliffTiles[9];
        }
        tile._isCliff = true;
        tile._elevation = _cliffElevation;
        tile._cliffDirection = _forward;

        TransitionToNextAcre(); // In case we start at an edge
        
        bool isDone = false;
        while (!isDone)
        {
            if (_currentAcre._elevation == _cliffElevation)
            {
                _currentAcre._cliffWalked = true;
            }
            
            tiles = _currentAcre._tiles;
            tile = tiles[_pos.x, _pos.y];

            (var validRules, var numRules, var selectedRule) = SelectRule(tile);

            // Loop through all valid tiles until one is found that ends at a valid position
            CliffTileRule rule;
            var tries = 0;
            Vector2Int posForBoundsCheck;
            Vector2Int nextPos;
            do
            {
                if (tries == numRules)
                {
                    if (_history.Count == 0)
                    {
                        // throw new Exception("No valid tiles in next position :(");
                        Debug.Log("At Error: " + _pos + " | " + _forward);
                        Debug.LogError("No valid tiles in next position :(");
                        return;
                    }

                    var state = _history.Pop();
                    tile = tiles[_pos.x, _pos.y];
                    tile._isCliff = false;
                    _pos = state._pos;
                    tile = tiles[_pos.x, _pos.y];
                    (validRules, numRules, _) = SelectRule(tile);
                    selectedRule = state._selectedRule;
                    tries = state._tries;
                }

                rule = validRules[(selectedRule + tries) % numRules];
                tries++;
                
                // Check if tile will be valid
                nextPos = _pos + rule._offset;
                posForBoundsCheck = nextPos + _right * _maxCliffEat;
            } while (isOutsideAcre(nextPos) || !isOutsideAcre(posForBoundsCheck)) ;

            _history.Push(new WalkState(tries, selectedRule, _pos));
            
            // If ok tile found - commit to the move and do step 1 again
            var oldPos = _pos;
            _pos = nextPos;
            tile = tiles[_pos.x, _pos.y];

            bool cliffCollision = false;
            if (rule._offset.x != 0 && rule._offset.y != 0)
            {
                // moving diagonally check if going thru an existing cliff
                var xTile = tiles[oldPos.x + rule._offset.x, oldPos.y];
                var yTile = tiles[oldPos.x, oldPos.y + rule._offset.y];
                cliffCollision = xTile._isCliff && yTile._isCliff;
            }
            
            if (tile._elevation >= _cliffElevation && (tile._isCliff || cliffCollision) && !_noDirectionChange)
            {
                break;
            }
            
            tile._cliffTile = _cliffTiles[rule._index];
            tile._isCliff = true;
            tile._cliffDirection = _forward;
            tile._elevation = _cliffElevation;

            // Check if should rotate
            if (!_noDirectionChange)
            {
                UpdateDirection();
            }

            isDone = TransitionToNextAcre();
        }
    }

    private bool TransitionToNextAcre()
    {
        if (isOutsideAcre(_pos + _forward))
        {
            var currentAcrePos = new Vector2Int(_currentAcre._x, _currentAcre._y);
            var nextAcrePos = currentAcrePos + _forward;
            List<Acre> possibleAcres = new List<Acre>();
            if (!isOutsideMap(nextAcrePos) && _acres[nextAcrePos.x, nextAcrePos.y]._elevation >= _cliffElevation)
            {
                var forwardAcre = _acres[nextAcrePos.x, nextAcrePos.y];
                possibleAcres.Add(forwardAcre);
                if (forwardAcre._hasSouthWestCliff || forwardAcre._hasSouthEastCliff)
                {
                    nextAcrePos = currentAcrePos + _forward + _right;
                    if (!isOutsideMap(nextAcrePos) && _acres[nextAcrePos.x, nextAcrePos.y]._elevation >= _cliffElevation)
                    {
                        possibleAcres.Add(_acres[nextAcrePos.x, nextAcrePos.y]);
                    }
                }
            }
            
            if (possibleAcres.Count == 0)
            {
                return true;
            }
            
            var tile = _currentAcre._tiles[_pos.x, _pos.y];
            var (validRules, numRules, selectedRule) = SelectRule(tile);
            var rule = validRules[selectedRule];
            var tries = 0;
            var nextPos = _pos;
            bool validRuleFound = false;
            do
            {
                rule = validRules[(selectedRule + tries) % numRules];
                tries++;
                if (tries > numRules)
                {
                    Debug.Log("Unable to find tile for acre transition! :(");
                    return true;
                }

                nextPos = _pos + rule._offset;
                var acreVector = new Vector2Int(0, 0);
                if (nextPos.x > _acreSize - 1)
                {
                    acreVector += Vector2Int.right;
                }

                if (nextPos.y > _acreSize - 1)
                {
                    acreVector += new Vector2Int(0, 1);
                }
                else if (nextPos.y < 0)
                {
                    acreVector += new Vector2Int(0, -1);
                }
                
                nextPos.x = Mod(nextPos.x, _acreSize);
                nextPos.y = Mod(nextPos.y, _acreSize);
                
                nextAcrePos = currentAcrePos + acreVector;
                if (nextAcrePos == currentAcrePos)
                {
                    break;
                }
                foreach (var acre in possibleAcres)
                {
                    if (acre._x == nextAcrePos.x &&
                        acre._y == nextAcrePos.y)
                    {
                        var nextDirection = CalculateDirectionAfterTransition(acre, _currentAcre);
                        var right = new Vector2Int(-nextDirection.y, nextDirection.x);
                        var posForBoundsCheck = nextPos + right * _maxCliffEat;
                        if (isOutsideAcre(posForBoundsCheck))
                        {
                            validRuleFound = true;
                            break;
                        }
                    }
                }
            } while (!validRuleFound);
            
            _noDirectionChange = false;

            bool cliffCollision = false;
            if (rule._offset.x != 0 && rule._offset.y != 0)
            {
                // moving diagonally check if going thru an existing cliff
                var xTile = GetNeighbourTile(new Vector2Int(rule._offset.x, 0));
                var yTile = GetNeighbourTile(new Vector2Int(0, rule._offset.y));

                if (xTile != null && yTile != null)
                {
                    cliffCollision = xTile._isCliff && yTile._isCliff;
                }
            }

            var oldAcre = _currentAcre;
            _currentAcre = _acres[nextAcrePos.x, nextAcrePos.y];
            var tiles = _currentAcre._tiles;

            _pos = nextPos;
            
            tile = tiles[_pos.x, _pos.y];

            if (tile._isCliff || cliffCollision)
            {
                return true;
            }
            
            tile._cliffTile = _cliffTiles[rule._index];
            tile._isCliff = true;
            tile._cliffDirection = _forward;
            tile._elevation = _cliffElevation;

            ChangeDirection(CalculateDirectionAfterTransition(_currentAcre, oldAcre));

            TransitionToNextAcre();
        }
        
        return false;
    }

    private Vector2Int CalculateDirectionAfterTransition(Acre newAcre, Acre oldAcre)
    {
        var dx = newAcre._x - oldAcre._x;
        var dy = newAcre._y - oldAcre._y;
        
        if (_forward == new Vector2Int(0, 1))
        {
            return new Vector2Int(_forward.x, _forward.y);
        } 
        else if (_forward == Vector2Int.right)
        {
            if (newAcre._hasSouthWestCliff || (newAcre._hasWestCliff && dx != 0 && dy != 0))
            {
                return new Vector2Int(0, 1);
            } 
            else if (newAcre._hasSouthCliff)
            {
                return new Vector2Int(_forward.x, _forward.y);
            }
        } 
        else if (_forward == new Vector2Int(0, -1))
        {
            if (newAcre._hasSouthEastCliff || (newAcre._hasSouthCliff && dx != 0 && dy != 0))
            {
                return new Vector2Int(1, 0);
            } 
            else if (newAcre._hasEastCliff)
            {
                return new Vector2Int(_forward.x, _forward.y);
            }
        }

        if (newAcre == _currentAcre)
        {
            return new Vector2Int(_forward.x, _forward.y);;
        }
        throw new Exception("not possible");
    }
    
    private (List<CliffTileRule>, int, int) SelectRule(Tile tile)
    {
        var rules = tile._cliffTile._rules;
        List<CliffTileRule> validRules = new List<CliffTileRule>();
        foreach (var r in rules)
        {
            if (r._direction == _forward)
            {
                validRules.Add(r);
            }
        }
        var numRules = validRules.Count;
            
        // If no valid tiles - bad tileset
        if (numRules == 0)
        {
            throw new Exception("No valid tiles in this position :(");
        }
            
        return (validRules, numRules, Random.Range(0, numRules));
    }
    
    private void UpdateDirection()
    {
        if (_forward == new Vector2Int(0, 1) && _currentAcre._hasSouthCliff)
        {
            if (_pos.y > _acreSize - 2)
            {
                Debug.Log("Hrmm");
            }
            if (_pos.y >= _acreSize - 2)
            {
                ChangeDirection(Vector2Int.right);
            }
            else if (_pos.y >= _acreSize - _maxCliffEat)
            {
                if (Random.Range(0, _maxCliffEat) == 0)
                {
                    ChangeDirection(Vector2Int.right);
                }
            }
        } 
        else if (_forward == Vector2Int.right && _currentAcre._hasEastCliff)
        {
            if (_pos.x > _acreSize - 2)
            {
                Debug.Log("Yarr!");
            }
            if (_pos.x >= _acreSize - 2)
            {
                ChangeDirection(new Vector2Int(0, -1));
            } 
            else if (_pos.x >= _acreSize - _maxCliffEat)
            {
                if (Random.Range(0, _maxCliffEat) == 0)
                {
                    ChangeDirection(new Vector2Int(0, -1));
                }
            }
        }
    }
    
    private void ChangeDirection(Vector2Int dir)
    {
        _forward = dir;
        _right = new Vector2Int(-_forward.y, _forward.x);
    }

    public void WalkToEdge()
    {
        var tiles = _currentAcre._tiles;
        var tile = tiles[_pos.x, _pos.y];
        
        // TODO: Think of something robust
        if (_forward.y > 0)
        {
            tile._cliffTile = _cliffTiles[0];
        } else if (_forward.x > 0)
        {
            tile._cliffTile = _cliffTiles[1];
        }
        tile._isCliff = true;
        tile._elevation = _cliffElevation;
        tile._cliffDirection = _forward;

        bool isDone = isOutsideAcre(_pos + _forward);
        while (!isDone)
        {
            tile = tiles[_pos.x, _pos.y];

            (var validRules, var numRules, var selectedRule) = SelectRule(tile);

            // Loop through all valid tiles until one is found that ends at a valid position
            CliffTileRule rule;
            var tries = 0;
            Vector2Int posForBoundsCheck;
            Vector2Int nextPos;
            do
            {
                if (tries == numRules)
                {
                    throw new Exception("No valid tiles in next position :(");
                }

                rule = validRules[(selectedRule + tries) % numRules];
                tries++;
                
                // Check if tile will be valid
                nextPos = _pos + rule._offset;
                posForBoundsCheck = nextPos + _right * _maxCliffEat;
            } while (isOutsideAcre(nextPos) || !isOutsideAcre(posForBoundsCheck)) ;

            // If ok tile found - commit to the move and do step 1 again
            _pos = nextPos;
            tile = tiles[_pos.x, _pos.y];
            
            if (tile._isCliff)
            {
                break;
            }
            
            tile._cliffTile = _cliffTiles[rule._index];
            tile._isCliff = true;
            tile._cliffDirection = _forward;
            tile._elevation = _cliffElevation;

            isDone = isOutsideAcre(_pos + _forward);
        }
    }

    private bool isOutsideAcre(Vector2Int pos)
    {
        return pos.x < 0 ||
               pos.x >= _acreSize ||
               pos.y >= _acreSize ||
               pos.y < 0;
    }

    private bool isOutsideMap(Vector2Int acrePos)
    {
        return acrePos.x < 0 ||
               acrePos.x >= _width ||
               acrePos.y >= _height ||
               acrePos.y < 0;
    }
    private bool NotOpposite(Vector2Int a, Vector2Int b)
    {
        return (a.x * b.x + a.y * b.y) >= 0;
    }

    private int Mod(int x, int m)
    {
        return (x % m + m) % m;
    }

    private Acre GetNeighbourAcre(Vector2Int dir)
    {
        var p = new Vector2Int(_currentAcre._x, _currentAcre._y);
        p += dir;
        if (p.x >= 0 && p.x < _width &&
            p.y >= 0 && p.y < _height)
        {
            return _acres[p.x, p.y];
        }

        return null;
    }

    private Tile GetNeighbourTile(Vector2Int vec)
    {
        var acrePos = new Vector2Int(_currentAcre._x * _acreSize, _currentAcre._y * _acreSize);
        var tilePos = _pos;
        var mapPos = acrePos + tilePos + vec;

        acrePos = new Vector2Int(mapPos.x / _acreSize, mapPos.y / _acreSize);
        if (isOutsideMap(acrePos))
        {
            return null;
        }
        tilePos = new Vector2Int(Mod(mapPos.x, _acreSize), Mod(mapPos.y, _acreSize));
        return _acres[acrePos.x, acrePos.y]._tiles[tilePos.x, tilePos.y];
    }
}
