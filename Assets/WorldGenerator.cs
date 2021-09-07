using TMPro;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private int _mapWidth;
    [SerializeField] private int _mapHeight;
    [SerializeField] private int _elevationRangeMin;
    [SerializeField] private int _elevationRangeMax;

    [SerializeField] private GameObject _tileObject;
    [SerializeField] private float _tileObjectSize;
    
    private World _world;
    
    // Start is called before the first frame update
    void Start()
    {
        _world = new World(_mapWidth, _mapHeight, _elevationRangeMin, _elevationRangeMax);
        _world.GenerateAcres();
        
        var acres = _world._acres;
        
        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                float height = acres[x, y]._elevation - 1;
                if (height < 0.0f)
                {
                    height = 0.0f;
                }
                Vector3 position = new Vector3(x * _tileObjectSize + _tileObjectSize / 2.0f, height * 2.0f, (_mapHeight - 1 - y) * _tileObjectSize + _tileObjectSize / 2.0f);
                Instantiate(_tileObject, position, Quaternion.identity, transform);
            }
        }
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
    /// <summary>Acre data.</summary>
    public Acre[,] _acres { get; }

    private int _elevationRangeMin;
    private int _elevationRangeMax;
    private int _maxLayers;

    public World(int width, int height, int elevationRangeMin, int elevationRangeMax)
    {
        _width = width;
        _height = height;
        _acres = new Acre[width, height];

        _elevationRangeMin = elevationRangeMin;
        _elevationRangeMax = elevationRangeMax;
    }
    
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
}

public class Acre
{
    public int _elevation;
    public bool _river;
}
