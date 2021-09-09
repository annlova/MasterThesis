using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
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

    // Start is called before the first frame update
    void Start()
    {
        _world = new World(_mapWidth, _mapHeight, _acreSize, _elevationRangeMin, _elevationRangeMax, _maxCliffEat, _cliffTiles);
        _world.GenerateAcres();
        
        var acres = _world._acres;
        
        for (int acreX = 0; acreX < _mapWidth; acreX++)
        {
            for (int acreY = 0; acreY < _mapHeight; acreY++)
            {

                var acre = acres[acreX, acreY];
                float height = acre._elevation * 2;
                
                for (int tileX = 0; tileX < _acreSize; tileX++)
                {
                    for (int tileY = 0; tileY < _acreSize; tileY++)
                    {
                        var tile = acre._tiles[tileX, tileY];
                        Vector3 position = new Vector3(acreX * _acreSize + tileX, height, (_mapHeight - 1 - acreY) * _acreSize + (_acreSize - 1 - tileY));
                        if (tile._isCliff)
                        {
                            Instantiate(tile._cliffTile._prefab, position + Vector3.down * 2, tile._cliffTile._prefab.transform.rotation, transform);
                        }
                        else
                        {
                            Instantiate(_tileObject, position + Vector3.down * 2 * tile._deelevation, Quaternion.identity, transform);
                        }
                    }
                }
            }
        }

        Combine();
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
            
            i++;
        }
        transform.GetComponent<MeshFilter>().mesh = new Mesh();
        transform.GetComponent<MeshFilter>().mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        transform.GetComponent<MeshFilter>().mesh.CombineMeshes(combine);
        transform.gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
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
    public void GenerateAcres()
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

                // TODO: TEMP CODE REMOVE
                if (x == 0 && y < _height - 1)
                {
                    elevation = 1;
                }else if (x == 1 && y == _height - 2)
                {
                    elevation = 0;
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
        
        // TODO: remove
        // for (int x = 0; x < _width; x++)
        // {
        //     for (int y = 0; y < _height; y++)
        //     {
        //         GenerateAcre(x, y);
        //     }
        // }
        //
        // // TODO: Refactor generation code to have a cleaner solution that does not require a special pass for right hand cliffs
        // for (int x = _width - 1; x >= 0; x--)
        // {
        //     for (int y = _height - 1; y >= 0; y--)
        //     {
        //         SpecialPassMarkRightHandsideCliffs(x, y);
        //     }
        // }
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
                acre._tiles[x, y] = new Tile(Tile.TileType.Flat);
            }
        }
    }

    private int CalculateIslands()
    {
        int islandIndex = 0;
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (_acres[x, y]._islandIndex < 0)
                {
                    ConnectNeighbours(_acres[x, y], islandIndex);
                    islandIndex++;
                }
            }
        }

        return islandIndex - 1;
    }

    private int temp = 0;
    private void ConnectNeighbours(Acre acre, int islandIndex)
    {
        temp++;
        int x = acre._x;
        int y = acre._y;
        acre._islandIndex = islandIndex;
        if (islandIndex == 0)
        {
            Debug.Log(x + ", " + y);
        }

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
            acre._hasNorthWestCliff = true;
        }

        if (n != null && e != null && ne != null &&
            acre._islandIndex == n._islandIndex &&
            acre._islandIndex == e._islandIndex &&
            ne._elevation < acre._elevation)
        {
            acre._hasNorthEastCliff = true;
        }

        if (s != null && e != null && se != null &&
            acre._islandIndex == s._islandIndex &&
            acre._islandIndex == e._islandIndex &&
            se._elevation < acre._elevation)
        {
            acre._hasSouthEastCliff = true;
        }

        if (s != null && w != null && sw != null &&
            acre._islandIndex == s._islandIndex &&
            acre._islandIndex == w._islandIndex &&
            sw._elevation < acre._elevation)
        {
            acre._hasSouthWestCliff = true;
        }
    }

    private void MarkCliffTiles(int highestIslandIndex)
    {
        for (int islandIndex = 0; islandIndex <= highestIslandIndex; islandIndex++)
        {
            var startAcre = FindLeftBottomMostAcre(islandIndex);
            if (startAcre._elevation == 0)
            {
                continue;
            }
            new CliffWalkAgent(_cliffTiles, startAcre, _maxCliffEat).Walk(_acres, _width, _height);
        }

    }

    private Acre FindLeftBottomMostAcre(int islandIndex)
    {
        Acre acre = null;
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (_acres[x, y]._islandIndex == islandIndex)
                {
                    acre = _acres[x, y];
                }
            }

            if (acre != null)
            {
                return acre;
            }
            Debug.Log("x: " + x);
        }

        throw new Exception("This code should not be reached :(.");
    }

}

public class Acre
{
    public int _x { get; }
    public int _y { get; }
    public int _elevation { get; }

    public int _islandIndex;

    public bool _hasWestCliff;
    public bool _hasNorthWestCliff;
    public bool _hasNorthCliff;
    public bool _hasNorthEastCliff;
    public bool _hasEastCliff;
    public bool _hasSouthEastCliff;
    public bool _hasSouthCliff;
    public bool _hasSouthWestCliff;
    
    public bool hasLeftCliff;
    public bool hasBottomCliff;
    public bool hasRightCliff;
    
    public int _size;
    public Tile[,] _tiles;

    public Acre(int x, int y, int elevation)
    {
        _x = x;
        _y = y;
        _elevation = elevation;
        
        _hasWestCliff = false;
        _hasNorthWestCliff = false;
        _hasNorthCliff = false;
        _hasNorthEastCliff = false;
        _hasEastCliff = false;
        _hasSouthEastCliff = false;
        _hasSouthCliff = false;
        _hasSouthWestCliff = false;

        _islandIndex = -1;
    }
}

public class Tile
{
    public TileType _type;
    public bool _isCliff;
    public int _deelevation;
    public CliffTile _cliffTile;

    public Tile(TileType type)
    {
        _type = type;
        if (type == TileType.Flat || type == TileType.None)
        {
            _isCliff = false;
        }
        else
        {
            _isCliff = true;
        }
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

    public enum ConnectPoint
    {
        NorthWest = 0,
        North = 1,
        NorthEast = 2,
        East = 3,
        SouthEast = 4,
        South = 5,
        SouthWest = 6,
        West = 7
    }
}

[Serializable]
public class CliffTileRule
{
    public int _index;
    public Vector2Int _offset;
    public Vector2Int _direction;
}

public class CliffWalkAgent
{
    private CliffTile[] _cliffTiles;
    private CliffTile.ConnectPoint _currentConnectPoint;

    private Acre _currentAcre;
    
    private int _islandIndex;
    private Vector2Int _forward;
    private Vector2Int _right;
    private Vector2Int _pos;
    
    private int _acreSize;
    private int _maxCliffEat;
    public CliffWalkAgent(CliffTile[] cliffTiles, Acre startAcre, int maxCliffEat)
    { 
        _cliffTiles = cliffTiles;
        _currentAcre = startAcre;

        if (startAcre == null)
        {
            Debug.Log("hmm...");
        }
        _islandIndex = startAcre._islandIndex;
        _acreSize = startAcre._size;
        _maxCliffEat = maxCliffEat;
        CalculateCliffWalkStart(startAcre);
    }
    
    private void CalculateCliffWalkStart(Acre startAcre)
    {
        if (startAcre._hasWestCliff)
        {
            var startX = Random.Range(0, _maxCliffEat);
            var startY = 0;
            _pos = new Vector2Int(startX, startY);
            
            _forward = new Vector2Int(0, 1);
            _right = new Vector2Int(-1, 0);

            var rng = Random.Range(0, 3);
            _currentConnectPoint = rng switch
            {
                0 => CliffTile.ConnectPoint.NorthWest,
                1 => CliffTile.ConnectPoint.North,
                2 => CliffTile.ConnectPoint.NorthEast,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        else if (startAcre._hasSouthCliff)
        {
            var startX = 0;
            var startY = _acreSize - 1 - Random.Range(0, _maxCliffEat);
            _pos = new Vector2Int(startX, startY);
            
            _forward = new Vector2Int(1, 0);
            _right = new Vector2Int(0, 1);
            
            var rng = Random.Range(0, 3);
            _currentConnectPoint = rng switch
            {
                0 => CliffTile.ConnectPoint.NorthWest,
                1 => CliffTile.ConnectPoint.West,
                2 => CliffTile.ConnectPoint.SouthWest,
                _ => throw new ArgumentOutOfRangeException()
            };
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
            tile._cliffTile = _cliffTiles[0];
        } else if (_forward.x > 0)
        {
            tile._cliffTile = _cliffTiles[1];
        }
        tile._isCliff = true;

        bool isDone = false;
        while (!isDone)
        {
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
            tile._cliffTile = _cliffTiles[rule._index];
            tile._isCliff = true;

            var p = _pos + _right;
            while (!isOutsideAcre(p))
            {
                tiles[p.x, p.y]._deelevation++;
                p += _right;
            }
            
            // Check if should rotate
            UpdateDirection();

            isDone = TransitionToNextAcre(acres, w, h);
        }
    }

    private bool TransitionToNextAcre(Acre[,] acres, int w, int h)
    {
        if (isOutsideAcre(_pos + _forward))
        {
            var nextAcrePos = new Vector2Int(_currentAcre._x, _currentAcre._y) + _forward;
            if (nextAcrePos.x >= w || nextAcrePos.y >= h || acres[nextAcrePos.x, nextAcrePos.y]._islandIndex != _currentAcre._islandIndex)
            {
                return true;
            }
            else
            {
                var tile = _currentAcre._tiles[_pos.x, _pos.y];
                (var validRules, var numRules, var selectedRule) = SelectRule(tile);
                
                _currentAcre = acres[nextAcrePos.x, nextAcrePos.y];
                var tiles = _currentAcre._tiles;
                
                _pos += _forward;
                _pos.x = Mod(_pos.x, _acreSize);
                _pos.y = Mod(_pos.y, _acreSize);
                
                tile = tiles[_pos.x, _pos.y];
                tile._isCliff = true;
                tile._cliffTile = _cliffTiles[validRules[selectedRule]._index];
                    
                // TODO: Set direction
                if (_forward == new Vector2Int(0, 1))
                {
                    // Keep same direction
                } 
                else if (_forward == Vector2Int.right)
                {
                    if (_currentAcre._hasSouthWestCliff)
                    {
                        ChangeDirection(new Vector2Int(0, 1));
                    } else if (_currentAcre._hasSouthCliff)
                    {
                        // Keep same direction
                    }
                } 
                else if (_forward == new Vector2Int(0, -1))
                {
                    if (_currentAcre._hasSouthEastCliff)
                    {
                        ChangeDirection(Vector2Int.right);
                    } else if (_currentAcre._hasEastCliff)
                    {
                        // Keep same direction
                    }
                }
                
                TransitionToNextAcre(acres, w, h);
            }
        }
        
        return false;
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
            if (_pos.y == _acreSize - 2)
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
            if (_pos.x == _acreSize - 2)
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

    private bool isOutsideAcre(Vector2Int pos)
    {
        return pos.x < 0 ||
               pos.x >= _acreSize ||
               pos.y >= _acreSize ||
               pos.y < 0;
    }

    private bool NotOpposite(Vector2Int a, Vector2Int b)
    {
        return (a.x * b.x + a.y * b.y) >= 0;
    }

    private int Mod(int x, int m)
    {
        return (x % m + m) % m;
    }
}
