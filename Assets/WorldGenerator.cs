using TMPro;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private int _mapWidth;
    [SerializeField] private int _mapHeight;
    [SerializeField] private int _elevationRangeMin;
    [SerializeField] private int _elevationRangeMax;

    [SerializeField] private GameObject _tileObject;
    [SerializeField] private int _acreSize;

    private World _world;

    // Start is called before the first frame update
    void Start()
    {
        _world = new World(_mapWidth, _mapHeight, _acreSize, _elevationRangeMin, _elevationRangeMax);
        _world.GenerateAcres();
        
        var acres = _world._acres;
        
        for (int acreX = 0; acreX < _mapWidth; acreX++)
        {
            for (int acreY = 0; acreY < _mapHeight; acreY++)
            {
                var acre = acres[acreX, acreY];
                _world.GenerateAcre(acre);
                
                float height = acres[acreX, acreY]._elevation - 1;
                if (height < 0.0f)
                {
                    height = 0.0f;
                }
                
                for (int tileX = 0; tileX < acre._size; tileX++)
                {
                    for (int tileY = 0; tileY < acre._size; tileY++)
                    {
                        var rng = Random.Range(-0.3f, 0.3f);
                        Vector3 position = new Vector3(acreX * _acreSize + tileX, height * 2.0f + rng, (_mapHeight - 1 - acreY) * _acreSize + tileY);
                        Instantiate(_tileObject, position, Quaternion.identity, transform);
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
        Debug.Log(meshFilters.Length);
        
        int i = 1;
        while (i < meshFilters.Length)
        {
            combine[i - 1].mesh = meshFilters[i].sharedMesh;
            combine[i - 1].transform = meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false);

            i++;
        }
        
        var meshFilter = transform.GetComponent<MeshFilter>();
        Debug.Log(meshFilter == meshFilters[0]);
        meshFilter.mesh = new Mesh();
        meshFilter.mesh.CombineMeshes(combine);
        GetComponent<MeshCollider>().sharedMesh = meshFilter.mesh;
        transform.gameObject.SetActive(true);

        transform.localScale = new Vector3();
        transform.rotation = Quaternion.identity;
        transform.position = Vector3.zero;
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

    public World(int width, int height, int acreSize, int elevationRangeMin, int elevationRangeMax)
    {
        _width = width;
        _height = height;
        _acreSize = acreSize;
        _acres = new Acre[width, height];

        _elevationRangeMin = elevationRangeMin;
        _elevationRangeMax = elevationRangeMax;
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
            var top = new Acre();
            top._elevation = _maxLayers;
            _acres[i, 0] = top;

            var bottom = new Acre();
            bottom._elevation = 0;
            _acres[i, _height - 1] = bottom;
        }

        // First basic acre generation algorithm TEMPORARY
        for (int x = 0; x < _width; x++)
        {
            for (int y = 1; y < _height - 1; y++)
            {
                int previousElevation = _acres[x, y - 1]._elevation;

                bool changeElevation = Random.Range(0, 3) > 1;
                int elevationChange = 0;
                if (changeElevation)
                {
                    elevationChange = 1;
                }
                
                if (previousElevation == 1)
                {
                    elevationChange = 0;
                }
                
                var acre = new Acre();
                acre._elevation = previousElevation - elevationChange;
                
                _acres[x, y] = acre;
            }
        }
    }

    /// <summary>
    /// Generate all data for a single acre.
    /// </summary>
    public void GenerateAcre(Acre acre)
    {
        InitializeAcreTiles(acre);
        GenerateCliff();
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

    public void GenerateCliff()
    {
        
    }
}

public class Acre
{
    public int _elevation;
    public bool _river;

    public int _size;
    public Tile[,] _tiles;
}

public class Tile
{
    public TileType _type;

    public Tile(TileType type)
    {
        _type = type;
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
