using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace TerrainGenerator
{
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField]
        private float stepDeltaTime;
        
        [SerializeField]
        private Vector2Int numAcres;

        [SerializeField] 
        private int acreSize;
        
        [SerializeField] 
        private int elevationRangeMin;
        
        [SerializeField] 
        private int elevationRangeMax;

        [SerializeField] 
        private int maxCliffEat;
        
        [SerializeField]
        private int slopeWidth;
        
        [SerializeField]
        private int slopeLength;
        
        [SerializeField]
        private CliffTile[] cliffTiles;
        
        [SerializeField] 
        private GameObject flatTilePrefab;

        [SerializeField]
        private GameObject slopeCliffPrefab;

        [SerializeField]
        private GameObject slopeCliffRoofPrefab;

        [SerializeField]
        private GameObject slopeCliffEndSWPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndHighFloorSWPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndLowFloorSWPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndHighRoofSWPrefab;

        [SerializeField]
        private GameObject slopeCliffSpecialSWPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndSEPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndHighFloorSEPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndLowFloorSEPrefab;
        
        [SerializeField]
        private GameObject slopeCliffEndHighRoofSEPrefab;

        [SerializeField]
        private GameObject slopeCliffSpecialSEPrefab;

        // Private variables outside of terrain generation

        private ComputeBuffer tileValueBuffer;
        private Vector4[] tileValues;

        private GameObject flatsRenderObject;
        private GameObject flatsColliderObject;
        private MeshFilter flatsMeshFilter;
        private Renderer flatsRenderer;
        private GameObject cliffsRenderObject;
        private GameObject cliffsColliderObject;
        private MeshFilter cliffsMeshFilter;
        private Renderer cliffsRenderer;

        // Start is called before the first frame update
        void Start()
        {
            Init();
            while (currentWalkAgent == null || !currentWalkAgent.IsDone() || currentIsland < numIslands)
            {
                var ok = step();
                if (!ok)
                {
                    break;
                }
            }

            InitPossibleFloors();
            LevelTerrain();
            ComputeCliffFloors();
            ComputeSlopes();
            CreateTerrainMesh();
            UpdateShaderVariables();
        }

        private bool done = false;
        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                stepContinuously = !stepContinuously;
            }
            else if (stepContinuously)
            {
                if (Time.time - lastStepTime >= stepDeltaTime)
                {
                    lastStepTime = Time.time;
                    var ok = step();
                    if (!done)
                    {
                        Animate();
                        if (!ok)
                        {
                            DestroyAnimation();
                            LevelTerrain();
                            ComputeCliffFloors();
                            CreateTerrainMesh();
                            done = true;
                        }
                    }
                }
            }
            else if (Input.GetKeyDown(KeyCode.P))
            {
                var ok = step();
                if (!done)
                {
                    Animate();
                    if (!ok)
                    {
                        DestroyAnimation();
                        LevelTerrain();
                        ComputeCliffFloors();
                        CreateTerrainMesh();
                        done = true;
                    }
                }
            }
        }

        /// <summary>
        /// Perform a single step of the terrain creation.
        /// </summary>
        private bool step()
        {
            var unable = true;
            while (unable)
            {
                // Check if all steps have been completed
                if (currentWalkAgent != null && currentWalkAgent.IsDone() && currentIsland == numIslands ||
                    currentWalkAgent == null && currentIsland == numIslands)
                {
                    return false;
                }

                // Check if a new walk agent should be created
                if (currentWalkAgent == null || currentWalkAgent.IsDone())
                {
                    (currentWalkAgent, unable) = CreateWalkAgent(currentIsland);
                    currentIsland++;
                }
                else
                {
                    unable = false;
                }
            }

            // Perform a step of the walk agent
            currentWalkAgent.Step();
            return true;
        }

        private Dictionary<Vector2Int, GameObject> createdInstances = new Dictionary<Vector2Int, GameObject>();
        private void Animate()
        {
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = tiles[x, z];
                    if (tile.modified)
                    {
                        tile.modified = false;
                        var p = new Vector2Int(x, z);
                        if (createdInstances.ContainsKey(p))
                        {
                            Destroy(createdInstances[p]);
                            createdInstances.Remove(p);
                        }
                        else
                        {
                            var pCliff = new Vector3(x + 0.5f, (tile.elevation - 1) * 2, height - 1 - z + 0.5f);
                            var o = Instantiate(tile.cliffTile.prefab, pCliff, tile.cliffTile.prefab.transform.rotation, transform);
                            createdInstances.Add(p, o);
                        }

                        return;
                    }
                }
            }
        }

        private void DestroyAnimation()
        {
            foreach (var ins in createdInstances)
            {
                Destroy(ins.Value);
            }
            createdInstances.Clear();
        }

        private float elevationHeight = 2.0f;
        private void CreateTerrainMesh()
        {
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = tiles[x, z];
                    if (tile.isCliff)
                    {
                        for (int i = tile.floor; i < tile.elevation; i++)
                        {
                            var p = new Vector3(x + 0.5f, i * 2, height - 1 - z + 0.5f);
                            Instantiate(tile.cliffTile.prefab, p, tile.cliffTile.prefab.transform.rotation, transform);
                        }
                        
                        var pRoof = new Vector3(x + 0.5f, tile.elevation * elevationHeight, height - 1 - z + 0.5f);
                        Instantiate(tile.cliffTile.prefabRoof, pRoof, tile.cliffTile.prefabRoof.transform.rotation, transform);

                        if (tile.isMergeCliff)
                        {
                            var mergeList = tile.mergeCliffs;
                            foreach (var item in mergeList)
                            {
                                for (int i = tile.floor; i < item.Item1; i++)
                                {
                                    var p = new Vector3(x + 0.5f, i * elevationHeight, height - 1 - z + 0.5f);
                                    Instantiate(item.Item2.prefab, p, item.Item2.prefab.transform.rotation, transform);
                                }

                                var pSubRoof = new Vector3(x + 0.5f, item.Item1 * elevationHeight, height - 1 - z + 0.5f);
                                Instantiate(item.Item2.prefabRoof, pSubRoof, item.Item2.prefabRoof.transform.rotation, transform);
                            }
                        }
                        
                        var pFloor = new Vector3(x + 0.5f, tile.floor * elevationHeight, height - 1 - z + 0.5f);
                        Instantiate(flatTilePrefab, pFloor, Quaternion.identity, transform);
                    }
                    else if (tile.isSlope)
                    {
                        if (tile.isSlopeCliff && tile.isSlopeHigher)
                        {
                            var p = new Vector3(x + 0.5f, (tile.elevation - 1) * elevationHeight, height - 1 - z + 0.5f);
                            var pRoof = p + new Vector3(0.0f, elevationHeight, 0.0f);
                            float rotation = 0.0f;
                            if (tile.isSlopeEdge1)
                            {
                                rotation = 270.0f;
                            }
                            else
                            {
                                rotation = 90.0f;
                            }

                            if (tile.slopeFactor > 0)
                            {
                                Instantiate(slopeCliffPrefab, p, Quaternion.Euler(0.0f, rotation, 0.0f), transform);
                                Instantiate(slopeCliffRoofPrefab, p + Vector3.up * elevationHeight, Quaternion.Euler(0.0f, rotation, 0.0f), transform);
                            }
                            else
                            {
                                if (tile.isSlopeEdge1)
                                {
                                    Instantiate(slopeCliffEndSWPrefab, p, slopeCliffEndSWPrefab.transform.rotation, transform);
                                    Instantiate(slopeCliffEndHighRoofSWPrefab, p + Vector3.up * elevationHeight,
                                        Quaternion.identity, transform);
                                }
                                else
                                {
                                    Instantiate(slopeCliffEndSEPrefab, p, slopeCliffEndSEPrefab.transform.rotation, transform);
                                    Instantiate(slopeCliffEndHighRoofSEPrefab, p + Vector3.up * elevationHeight,
                                        Quaternion.identity, transform);
                                }
                            }
                        }

                        if (tile.isSlopeLowEnd)
                        {
                            var p = new Vector3(x + 0.5f, (tile.elevation - 1) * elevationHeight, height - 1 - z + 0.5f);
                            CliffTile ct = null;
                            bool specialSW = false;
                            bool specialSE = false;
                            if (tile.isSlopeEdge1)
                            {
                                if (tile.cliffTile.slopeSouthWestConnectionIndex < 0)
                                {
                                    specialSW = true;
                                }
                                else
                                {
                                    ct = cliffTiles[tile.cliffTile.slopeSouthWestConnectionIndex];
                                }
                            }
                            else if (tile.isSlopeEdge2)
                            {
                                if (tile.cliffTile.slopeSouthEastConnectionIndex < 0)
                                {
                                    specialSE = true;
                                }
                                else
                                {
                                    ct = cliffTiles[tile.cliffTile.slopeSouthEastConnectionIndex];
                                }
                            }

                            if (specialSW)
                            {
                                Instantiate(slopeCliffSpecialSWPrefab, p, Quaternion.identity, transform);
                            }
                            else if (specialSE)
                            {
                                Instantiate(slopeCliffSpecialSEPrefab, p, Quaternion.identity, transform);
                            }
                            else if (ct != null)
                            {
                                var rotation = ct.prefab.transform.rotation;
                                Instantiate(ct.prefab, p, rotation, transform);
                                // Instantiate(tile.cliffTile.prefab, p + Vector3.up * elevationHeight, tile.cliffTile.prefab.transform.rotation, transform);
                                Instantiate(ct.prefabRoof, p + Vector3.up * elevationHeight, rotation, transform);
                            }
                        }
                        
                        {
                            var angleRad = (float) (Math.Atan(elevationHeight / slopeLength));
                            var angleDeg = (float) -(angleRad * (180.0 / Math.PI));
                            var scaleFactor = (float) (Math.Sqrt(slopeLength * slopeLength + elevationHeight * elevationHeight) / slopeLength);
                            var yOffset = (float) tile.slopeFactor / slopeLength;
                            var y = tile.elevation - yOffset;
                            y *= elevationHeight;
                            y -= (float) Math.Sin(angleRad) * scaleFactor * 0.5f;
                            var p = new Vector3(x + 0.5f, y, height - 1 - z + 0.5f);
                            var prefab = flatTilePrefab;
                            if (tile.isSlopeCliff)
                            {
                                if (tile.isSlopeEdge1 && tile.slopeFactor == 0)
                                {
                                    prefab = slopeCliffEndHighFloorSWPrefab;
                                }
                                else if (tile.isSlopeEdge2 && tile.slopeFactor == 0)
                                {
                                    prefab = slopeCliffEndHighFloorSEPrefab;
                                }
                                else if (tile.isSlopeEdge1 && tile.isSlopeLowEnd)
                                {
                                    prefab = slopeCliffEndLowFloorSWPrefab;
                                }
                                else if (tile.isSlopeEdge2 && tile.isSlopeLowEnd)
                                {
                                    prefab = slopeCliffEndLowFloorSEPrefab;
                                }
                            }
                            var o = Instantiate(prefab, p, Quaternion.Euler(angleDeg, 0.0f, 0.0f), transform);
                            var oScale = o.transform.localScale;
                            oScale.z *= scaleFactor;
                            o.transform.localScale = oScale;

                            // Set all normals straight up
                            var mesh = o.GetComponent<MeshFilter>().mesh;
                            var normals = mesh.normals;
                            for (int i = 0; i < normals.Length; i++)
                            {
                                normals[i] = Quaternion.Euler(-angleDeg, 0.0f, 0.0f) * Vector3.up;
                            }
                            
                            mesh.normals = normals;

                            if ((tile.isSlopeEdge1 || tile.isSlopeEdge2) && tile.isSlopeLower)
                            {
                                var vertices = mesh.vertices;
                                for (int i = 0; i < vertices.Length; i++)
                                {
                                    if (vertices[i].x < -0.1f && tile.isSlopeEdge1 || vertices[i].x > 0.1f && tile.isSlopeEdge2)
                                    {
                                        var offset = y - (tile.elevation - 1) * 2;//-(elevationHeight - yOffset * elevationHeight - (float) Math.Sin(angleRad));
                                        if (vertices[i].z > 0.0f)
                                        {
                                            offset += (float) (Math.Sin(angleRad) * 0.5 * scaleFactor);
                                        }
                                        else
                                        {
                                            offset -= (float) (Math.Sin(angleRad) * 0.5 * scaleFactor);
                                        }
                                        
                                        vertices[i] -= Quaternion.Inverse(o.transform.rotation) * Vector3.up * offset;//Quaternion.AngleAxis(-angleDeg, Vector3.right) * Vector3.up * offset;
                                    }
                                }

                                mesh.vertices = vertices;
                            }
                        }
                    }
                    else
                    {
                        var p = new Vector3(x + 0.5f, tile.elevation * elevationHeight, height - 1 - z + 0.5f);
                        Instantiate(flatTilePrefab, p, Quaternion.identity, transform);
                    }

                    var islandIndexFactor = (float) tile.acre.islandIndex / numIslands;
                    if (tile.isSlope)
                    {
                        tileValues[x + z * width].x = 0.8f;
                        tileValues[x + z * width].y = 0.5f;
                        tileValues[x + z * width].z = 0.3f;
                    }
                    else if (!tile.isCliff && tile.cliffTile != null)
                    {
                        tileValues[x + z * width].x = 1.0f;
                        tileValues[x + z * width].y = 1.0f;
                        tileValues[x + z * width].z = 1.0f;
                    }
                    else if (tile.isMergeCliff)
                    {
                        tileValues[x + z * width].x = 1.0f;
                    }
                }
            }
            tileValueBuffer.SetData(tileValues);
            Combine();
        }

        private void UpdateShaderVariables()
        {
            flatsRenderer.sharedMaterial.SetInt("_AcreSize", acreSize);
            flatsRenderer.sharedMaterial.SetInt("_MapWidth", numAcres.x);
            flatsRenderer.sharedMaterial.SetInt("_MapHeight", numAcres.y);
            flatsRenderer.sharedMaterial.SetBuffer("_TileValues", tileValueBuffer);
            
            cliffsRenderer.sharedMaterial.SetInt("_AcreSize", acreSize);
            cliffsRenderer.sharedMaterial.SetInt("_MapWidth", numAcres.x);
            cliffsRenderer.sharedMaterial.SetInt("_MapHeight", numAcres.y);
            cliffsRenderer.sharedMaterial.SetBuffer("_TileValues", tileValueBuffer);
        }
        
        // Combine child meshes
        private void Combine()
        {
            // meshFilter.sharedMesh.Clear();
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            var combineFlats = new List<CombineInstance>();
            var combineCliffs = new List<CombineInstance>();

            int i = 0;
            while (i < meshFilters.Length)
            {
                switch (meshFilters[i].tag)
                {
                    case "Flat":
                        var flat = new CombineInstance();
                        flat.mesh = meshFilters[i].mesh;
                        flat.transform = meshFilters[i].transform.localToWorldMatrix;
                        var flatCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (flatCollider)
                        {
                            flatCollider.transform.SetParent(flatsColliderObject.transform);
                        }
                        combineFlats.Add(flat);
                        break;
                    case "Cliff":
                        var cliff = new CombineInstance();
                        cliff.mesh = meshFilters[i].mesh;
                        cliff.transform = meshFilters[i].transform.localToWorldMatrix;
                        var cliffCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (cliffCollider)
                        {
                            cliffCollider.transform.SetParent(cliffsColliderObject.transform);
                        }
                        combineCliffs.Add(cliff);
                        break;
                    default:
                        i++;
                        continue;
                }
                
                meshFilters[i].gameObject.SetActive(false);
                Destroy(meshFilters[i].gameObject);
                i++;
            }
            
            flatsMeshFilter.sharedMesh = new Mesh();
            flatsMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            flatsMeshFilter.sharedMesh.CombineMeshes(combineFlats.ToArray());
            flatsMeshFilter.gameObject.SetActive(true);

            cliffsMeshFilter.sharedMesh = new Mesh();
            cliffsMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            cliffsMeshFilter.sharedMesh.CombineMeshes(combineCliffs.ToArray());
            cliffsMeshFilter.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            tileValueBuffer.Release();
        }
        
        // Private variables for terrain generation

        private Acre[,] acres;
        private int numLayers;
        private int numIslands;
        
        private Tile[,] tiles;
        
        private int width;
        private int height;

        // Step variables
        private bool stepContinuously;
        private float lastStepTime;
        private WalkAgent currentWalkAgent;
        private int currentIsland;
        
        private void Init()
        {
            width = numAcres.x * acreSize;
            height = numAcres.y * acreSize;

            flatsRenderObject = transform.Find("FlatsRender").gameObject;
            flatsColliderObject = transform.Find("FlatsCollider").gameObject;
            flatsMeshFilter = flatsRenderObject.GetComponent<MeshFilter>();
            flatsRenderer = flatsRenderObject.GetComponent<Renderer>();


            cliffsRenderObject = transform.Find("CliffsRender").gameObject;
            cliffsColliderObject = transform.Find("CliffsCollider").gameObject;
            cliffsMeshFilter = cliffsRenderObject.GetComponent<MeshFilter>();
            cliffsRenderer = cliffsRenderObject.GetComponent<Renderer>();
            
            tileValues = new Vector4[width * height];
            for (int i = 0; i < tileValues.Length; i++)
            {
                tileValues[i] = new Vector4(0.1f, 0.1f, 0.1f, 1.0f);
            }
            tileValueBuffer = new ComputeBuffer(tileValues.Length, sizeof(float) * 4);
            
            stepContinuously = false;
            lastStepTime = Time.time;
            currentIsland = 0;
            
            GenAcres();
            ComputeIslands();
            ComputeCliffOrientations();
            InitTiles();
        }
        
        /// <summary>
        /// Generate acre elevations
        /// </summary>
        private void GenAcres()
        {
            acres = new Acre[numAcres.x, numAcres.y];
            numLayers = Random.Range(elevationRangeMin, elevationRangeMax + 1);
            GenTopBotAcres();
            BasicAcresGen();
        }

        /// Set top and bottom acres to highest and lowest elevation
        private void GenTopBotAcres()
        {
            for (int i = 0; i < numAcres.x; i++)
            {
                var top = new Acre(new Vector2Int(i, 0), numLayers - 1);
                acres[i, 0] = top;
                var top2 = new Acre(new Vector2Int(i, 1), numLayers - 1);
                acres[i, 1] = top2;

                var bottom = new Acre(new Vector2Int(i, numAcres.y - 1), 0);
                acres[i, numAcres.y - 1] = bottom;
            }
        }

        /// First basic acre generation algorithm TEMPORARY
        private void BasicAcresGen()
        {
            for (int x = 0; x < numAcres.x; x++)
            {
                for (int y = 2; y < numAcres.y - 1; y++)
                {
                    int previousElevation = acres[x, y - 1].elevation;

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

                    var acre = new Acre(new Vector2Int(x, y), elevation);
                    acres[x, y] = acre;
                }
            }
        }

        /// Compute which acres are apart of the same island, and number of islands
        private void ComputeIslands()
        {
            int islandIndex = 0;
            int w = numAcres.x;
            int h = numAcres.y;
            for (int layer = numLayers - 1; layer >= 0; layer--)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (acres[x, y].islandIndex < 0 &&
                            acres[x, y].elevation == layer)
                        {
                            ConnectAcreNeighbours(x, y, islandIndex, layer);
                            islandIndex++;
                        }
                    }
                }
            }

            numIslands = islandIndex;
        }

        private void ConnectAcreNeighbours(int x, int y, int islandIndex, int elevation)
        {
            if (x < 0 || x >= numAcres.x ||
                y < 0 || y >= numAcres.y)
            {
                return;
            }
            
            var acre = acres[x, y];

            if (acre.islandIndex == islandIndex ||
                acre.elevation != elevation)
            {
                return;
            }
            
            acre.islandIndex = islandIndex;
            ConnectAcreNeighbours(x, y - 1, islandIndex, elevation);
            ConnectAcreNeighbours(x - 1, y, islandIndex, elevation);
            ConnectAcreNeighbours(x + 1, y, islandIndex, elevation);
            ConnectAcreNeighbours(x, y + 1, islandIndex, elevation);
        }

        /// <summary>
        /// Compute acres cliff orientations
        /// </summary>
        private void ComputeCliffOrientations()
        {
            for (int y = 0; y < numAcres.y; y++)
            {
                for (int x = 0; x < numAcres.x; x++)
                {
                    var acre = acres[x, y];
                    int maxW = numAcres.x - 1;
                    int maxH = numAcres.y - 1;
                    
                    var w = x > 0 ? acres[x - 1, y] : null;
                    var n = y > 0 ? acres[x, y - 1] : null;
                    var e = x < maxW ? acres[x + 1, y] : null;
                    var s = y < maxH ? acres[x, y + 1] : null;
                    
                    var nw = x > 0 && y > 0 ? acres[x - 1, y - 1] : null;
                    var ne = x < maxW && y > 0 ? acres[x + 1, y - 1] : null;
                    var se = x < maxW && y < maxH ? acres[x + 1, y + 1] : null;
                    var sw = x > 0 && y < maxH ? acres[x - 1, y + 1] : null;

                    if (w != null && acre.elevation > w.elevation) { acre.hasWestCliff = true; }

                    if (n != null && acre.elevation > n.elevation)
                    {
                        throw new Exception("North acre elevation should not be lower!");
                    }
                    if (e != null && acre.elevation > e.elevation) { acre.hasEastCliff = true; }
                    if (s != null && acre.elevation > s.elevation) { acre.hasSouthCliff = true; }

                    if (n != null && w != null && nw != null &&
                        acre.islandIndex == n.islandIndex &&
                        acre.islandIndex == w.islandIndex &&
                        nw.elevation < acre.elevation)
                    {
                        throw new Exception("Not possible!");
                    }

                    if (n != null && e != null && ne != null &&
                        acre.islandIndex == n.islandIndex &&
                        acre.islandIndex == e.islandIndex &&
                        ne.elevation < acre.elevation)
                    {
                        throw new Exception("Not possible!");
                    }

                    if (s != null && e != null && se != null &&
                        (acre.islandIndex == s.islandIndex &&
                         acre.islandIndex == e.islandIndex &&
                         se.elevation < acre.elevation ||
                         s.elevation == e.elevation &&
                         se.elevation < s.elevation))
                    {
                        acre.hasSouthEastCliff = true;
                    }

                    if (s != null && w != null && sw != null &&
                        (acre.islandIndex == s.islandIndex &&
                        acre.islandIndex <= w.elevation &&
                        sw.elevation < acre.elevation ||
                        s.elevation <= w.elevation &&
                        sw.elevation < s.elevation))
                    {
                        acre.hasSouthWestCliff = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Initialize the tiles
        /// </summary>
        private void InitTiles()
        {
            tiles = new Tile[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var acre = acres[x / acreSize, y / acreSize];
                    tiles[x, y] = new Tile(new Vector2Int(x, y), acre, acre.elevation);
                }
            }
        }

        private (WalkAgent, bool) CreateWalkAgent(int islandIndex)
        {
            Vector2Int forward;
            Vector2Int right;
            Tile startTile;
            
            var startAcre = FindIslandStartAcre(islandIndex);
            if (startAcre.elevation == 0 || startAcre.cliffWalked)
            {
                return (null, true);
            }
            
            if (startAcre.hasWestCliff)
            {
                forward = new Vector2Int(0, 1);
                right = new Vector2Int(-1, 0);

                var startX = startAcre.pos.x * acreSize + Random.Range(0, maxCliffEat);
                var startY = startAcre.pos.y * acreSize;
                startTile = tiles[startX, startY];
                
                // Check if start acre needs to be modified
                var neighbourPos = startAcre.pos - forward;
                if (IsAcre(neighbourPos))
                {
                    var neighbour = GetAcre(neighbourPos);
                    if (neighbour.hasSouthCliff)
                    {
                        // Find correct start tile
                        startTile = FindStartTileWS(neighbour);
                    } 
                    else if (neighbour.hasSouthWestCliff)
                    {
                        neighbourPos = startAcre.pos - forward + right;
                        if (IsAcre(neighbourPos))
                        {
                            neighbour = GetAcre(neighbourPos);
                            if (neighbour.hasEastCliff)
                            {
                                // Find correct start tile
                                (startTile, forward, right) = FindStartTileWSWE(neighbour);
                            }
                        }
                    }
                }
            }
            else if (startAcre.hasSouthCliff)
            {
                forward = new Vector2Int(1, 0);
                right = new Vector2Int(0, 1);

                var startX = startAcre.pos.x * acreSize;
                var startY = startAcre.pos.y * acreSize + (acreSize - 1) - Random.Range(0, maxCliffEat);
                startTile = tiles[startX, startY];
                
                // Check if start acre needs to be modified
                var neighbourPos = startAcre.pos - forward;
                if (IsAcre(neighbourPos))
                {
                    var neighbour = GetAcre(neighbourPos);
                    if (neighbour.hasEastCliff)
                    {
                        // Find correct start tile
                        startTile = FindStartTileSE(neighbour);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException();
            }

            var agent = new WalkAgent(
                tiles, width, height,
                acres, startAcre, acreSize,
                cliffTiles, maxCliffEat,
                startTile.pos, forward, right
            );
            
            return (agent, false);
        }

        /// Find start acre
        /// Left-Topmost if west cliff present,
        /// else Left-Bottommost
        private Acre FindIslandStartAcre(int islandIndex)
        {
            Acre acre = null;
            for (int x = 0; x < numAcres.x; x++)
            {
                for (int y = 0; y < numAcres.y; y++)
                {
                    if (acres[x, y].islandIndex == islandIndex)
                    {
                        acre = acres[x, y];
                        if (acre.hasWestCliff)
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

            throw new InvalidOperationException();
        }

        private Tile FindStartTileWS(Acre acre)
        {
            var acreTilePos = acre.pos * acreSize;
            var startPos = acreTilePos + new Vector2Int(0, acreSize - 1);
            for (int y = startPos.y; y >= acreTilePos.y + acreSize - maxCliffEat; y--)
            {
                for (int x = startPos.x; x < acreTilePos.x + maxCliffEat; x++)
                {
                    var tile = tiles[x, y];
                    if (tile.isCliff)
                    {
                        return tiles[x, y];
                    }
                }
            }

            throw new InvalidOperationException();
        }

        private (Tile, Vector2Int, Vector2Int) FindStartTileWSWE(Acre acre)
        {
            var acreTilePos = acre.pos * acreSize;
            var startPos = acreTilePos + new Vector2Int(acreSize - 1, acreSize - 1);
            for (int x = startPos.x; x >= acreTilePos.x + acreSize - maxCliffEat; x--)
            {
                for (int y = startPos.y; y >= acreTilePos.y + acreSize - maxCliffEat; y--)
                {
                    var tile = tiles[x, y];
                    if (tile.isCliff)
                    {
                        var returnTile = tiles[x, y];
                        if (returnTile.acre != acre)
                        {
                            return (returnTile, new Vector2Int(0, 1), new Vector2Int(-1, 0)); 
                        }
                        else
                        {
                            return (returnTile, new Vector2Int(1, 0), new Vector2Int(0, 1));
                        }
                    }
                }
            }

            throw new InvalidOperationException();
        }
        
        private Tile FindStartTileSE(Acre acre)
        {
            var acreTilePos = acre.pos * acreSize;
            var startPos = acreTilePos + new Vector2Int(acreSize - 1, acreSize - 1);
            for (int x = startPos.x; x >= acreTilePos.x + acreSize - maxCliffEat; x--)
            {
                for (int y = startPos.y; y >= acreTilePos.y + acreSize - maxCliffEat; y--)
                {
                    var tile = tiles[x, y];
                    if (tile.isCliff)
                    {
                        return tiles[x, y];
                    }
                }
            }

            throw new InvalidOperationException();
        }

        private void InitPossibleFloors()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = tiles[x, y];
                    for (int i = 0; i < tile.elevation; i++)
                    {
                        tile.possibleFloors.Add(false);
                    }
                }
            }
        }
        
        private void LevelTerrain()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (tiles[x, y].isLeveled)
                    {
                        continue;
                    }
                    
                    var list = new List<Tile>();
                    var cliffList = new List<Tile>();
                    int lowest = numLayers;

                    var todo = new List<Vector2Int>();
                    todo.Add(new Vector2Int(x, y));

                    for (int i = 0; i < todo.Count; i++)
                    {
                        var p = todo[i];
                        if (p.x < 0 || p.x >= width ||
                            p.y < 0 || p.y >= height)
                        {
                            continue;
                        }
                        
                        var tile = tiles[p.x, p.y];
                        if (tile.elevation < lowest)
                        {
                            lowest = tile.elevation;
                        }
                        
                        if (tile.isLeveled || tile.isCliff)
                        {
                            if (tile.isCliff)
                            {
                             cliffList.Add(tile);
                            }
                            continue;
                        }

                        list.Add(tile);
                        tile.isLeveled = true;
                        
                        var neighbours = new Vector2Int[4];
                        neighbours[0] = new Vector2Int(p.x - 1, p.y);
                        neighbours[1] = new Vector2Int(p.x, p.y - 1);
                        neighbours[2] = new Vector2Int(p.x + 1, p.y);
                        neighbours[3] = new Vector2Int(p.x, p.y + 1);
            
                        foreach (var neighbour in neighbours)
                        {
                            todo.Add(neighbour);
                        }
                    }
                    
                    if (list.Count == 1)
                    {
                        // An only tile, choose lowest surrounding elevation
                        var p = todo[0];
                        var neighbours = new Vector2Int[4];
                        neighbours[0] = new Vector2Int(p.x - 1, p.y);
                        neighbours[1] = new Vector2Int(p.x, p.y - 1);
                        neighbours[2] = new Vector2Int(p.x + 1, p.y);
                        neighbours[3] = new Vector2Int(p.x, p.y + 1);
            
                        foreach (var neighbour in neighbours)
                        {
                            if (neighbour.x < 0 || neighbour.x >= width ||
                                neighbour.y < 0 || neighbour.y >= height)
                            {
                                continue;
                            }
                            var t = tiles[neighbour.x, neighbour.y];
                            if (t.elevation < lowest)
                            {
                                lowest = t.elevation;
                            }
                        }
                    }
        
                    foreach (var tile in list)
                    {
                        tile.elevation = lowest;
                    }

                    foreach (var tile in cliffList)
                    {
                        if (tile.possibleFloors.Count > lowest)
                        {
                            tile.possibleFloors[lowest] = true;
                        }
                    }
                }
            }
        }

        private void ComputeCliffFloors()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = tiles[x, y];
                    if (!tile.isCliff)
                    {
                        continue;
                    }

                    int tileElevation = tile.elevation;
                    if (tile.isMergeCliff)
                    {
                        foreach (var item in tile.mergeCliffs)
                        {
                            if (item.Item1 < tileElevation)
                            {
                                tileElevation = item.Item1;
                            }
                        }
                    }
                    
                    var todo = new List<Vector2Int>();
                    todo.Add(new Vector2Int(x, y));
                    
                    bool floorSet = false;
                    int floorMergeCliffOption = tileElevation;
                    for (int i = 0; i < todo.Count; i++)
                    {
                        var t = tiles[todo[i].x, todo[i].y];
                        if (!t.isCliff || t.isPossibleFloorChecked)
                        {
                            continue;
                        }

                        t.isPossibleFloorChecked = true;
                        
                        if (tile != t && t.isMergeCliff)
                        {
                            int floor = 0;
                            foreach (var item in t.mergeCliffs)
                            {
                                if (item.Item1 < tileElevation && item.Item1 > floor)
                                {
                                    floor = item.Item1;
                                    tileValues[tile.pos.x + tile.pos.y * width].y = 1.0f;
                                }
                            }

                            floorMergeCliffOption = floor;
                            continue;
                        }

                        if (!t.isMergeCliff)
                        {
                            if (t.elevation != tileElevation)
                            {
                                continue;
                            }
                        
                            for (int j = 0; j < t.possibleFloors.Count; j++)
                            {
                                if (t.possibleFloors[j])
                                {
                                    tile.floor = j;
                                    floorSet = true;
                                    tileValues[tile.pos.x + tile.pos.y * width].z = 1.0f;
                                    break;
                                }
                            }

                            if (floorSet)
                            {
                                break;
                            }
                        }

                        foreach (var connection in t.connectedCliffs)
                        {
                            todo.Add(connection.pos);
                        }
                    }

                    if (!floorSet)
                    {
                        tile.floor = floorMergeCliffOption;
                    }
                    
                    foreach (var p in todo)
                    {
                        var t = tiles[p.x, p.y];
                        t.isPossibleFloorChecked = false;
                    }
                    
                    // int lowestFlat = numLayers;
                    // int lowestCliff = numLayers;
                    // foreach (var neighbour in neighbours)
                    // {
                    //     if (neighbour.x < 0 || neighbour.x >= width ||
                    //         neighbour.y < 0 || neighbour.y >= height)
                    //     {
                    //         continue;
                    //     }
                    //
                    //     var t = tiles[neighbour.x, neighbour.y];
                    //     if (t.isCliff && t.elevation == tile.elevation)
                    //     {
                    //         
                    //     }
                    //     
                    //     if (t.isCliff && tile.elevation == t.elevation && t.floor < lowestCliff)
                    //     {
                    //         lowestCliff = t.floor;
                    //     } 
                    //     else if (t.isCliff && t.elevation < lowestCliff)
                    //     {
                    //         lowestCliff = t.elevation;
                    //     }
                    //     else if (!t.isCliff && t.elevation < lowestFlat)
                    //     {
                    //         lowestFlat = t.elevation;
                    //     }
                    // }
                    //
                    // if (lowestFlat < numLayers)
                    // {
                    //     tile.floor = lowestFlat;
                    // }
                    // else if (lowestCliff < numLayers)
                    // {
                    //     tile.floor = lowestCliff;
                    // }
                    // else
                    // {
                    //     tile.floor = tile.elevation - 1;
                    // }
                }
            }
        }

        private void ComputeSlopes()
        {
            // For each layer
            for (int layer = numLayers - 1; layer > 0; layer--)
            {
                // Find islands on same layer that border a layer one elevation down
                var islands = FindSlopeableAcres(layer);
                
                // For each island on same layer that borders a layer one elevation down
                foreach (var island in islands)
                {
                    if (island.Count == 0)
                    {
                        continue;
                    }
                    
                    // TODO: Choose randomly?
                    var connectedIslands = new bool[numIslands];
                    for (int i = 0; i < island.Count; i++)
                    {
                        var connectedIsland = GetNeighbour(island[i], new Vector2Int(0, 1)).islandIndex;
                        if (!connectedIslands[connectedIsland])
                        {
                            var success = AddSlope(island[i]);
                            if (success)
                            {
                                connectedIslands[connectedIsland] = true;
                            }
                        }
                    }
                }
            }
        }

        private List<Acre>[] FindSlopeableAcres(int layer)
        {
            var islands = new List<Acre>[numIslands];
            for (int i = 0; i < islands.Length; i++)
            {
                islands[i] = new List<Acre>();
            }
            
            for (int acreY = 0; acreY < numAcres.y; acreY++)
            {
                for (int acreX = 0; acreX < numAcres.x; acreX++)
                {
                    var acre = acres[acreX, acreY];
                    if (acre.elevation == layer &&
                        IsNeighbour(acre, new Vector2Int(0, 1)) &&
                        GetNeighbour(acre, new Vector2Int(0, 1)).elevation == acre.elevation - 1)
                    {
                        islands[acre.islandIndex].Add(acre);
                    }
                }
            }

            return islands;
        }
        
        private bool AddSlope(Acre acre)
        {
            // Find placement
            Vector2Int placementPos = Vector2Int.zero;
            var validPlacements = new List<Vector2Int>();

            // For each tile in acre
            var p = acre.pos * acreSize;
            for (int y = p.y; y < p.y + acreSize; y++)
            {
                for (int x = p.x; x < p.x + acreSize - slopeWidth; x++)
                {
                    // Check if placement valid
                    bool valid = true;
                    for (int i = 0; i < slopeWidth; i++)
                    {
                        var tileAbove = tiles[x + i, y];
                        valid &= tileAbove.elevation == acre.elevation && !tileAbove.isCliff;
                        var tileBelow = tiles[x + i, y + slopeLength - 1];
                        valid &= tileBelow.elevation == acre.elevation - 1 && !tileBelow.isCliff;
                    }

                    if (valid)
                    {
                        validPlacements.Add(new Vector2Int(x, y));
                    }
                }
            }

            // Find best placement from valid ones
            var targetPlacement = new Vector2Int(p.x + acreSize / 2 - slopeWidth / 2, p.y + acreSize / 2);
            float lowestDist = acreSize;
            foreach (var placement in validPlacements)
            {
                var dist = Vector2Int.Distance(placement, targetPlacement);
                if (dist < lowestDist)
                {
                    lowestDist = dist;
                    placementPos = placement;
                }
            }
            
            // If valid placement found, add slope
            if (validPlacements.Count > 0)
            {
                bool[] high = {true, true};
                for (int y = placementPos.y; y < placementPos.y + slopeLength; y++)
                {
                    for (int x = placementPos.x; x < placementPos.x + slopeWidth; x++)
                    {
                        var tile = tiles[x, y];
                        
                        tile.isSlope = true;
                        tile.slopeFactor = y - placementPos.y;
                        tile.elevation = acre.elevation;

                        if (x - placementPos.x == 0)
                        {
                            tile.isSlopeEdge1 = true;

                            bool lowestCliff = true;
                            if (tile.connectedCliffs.Count >= 1)
                            {
                                lowestCliff = !(tile.connectedCliffs[0].pos == tile.pos + new Vector2Int(0, 1));
                            }

                            if (tile.isCliff && high[0] && lowestCliff)
                            {
                                tile.isCliff = false;
                                tile.isSlopeLowEnd = true;
                                tile.isSlopeCliff = true;
                                tile.isSlopeHigher = false;
                                tile.isSlopeLower = true;
                                high[0] = false;
                            }
                            else
                            {
                                tile.isCliff = false;
                                tile.isSlopeCliff = high[0];
                                tile.isSlopeHigher = high[0];
                                tile.isSlopeLower = !high[0];
                            }
                        }
                        else if (x == placementPos.x + slopeWidth - 1)
                        {
                            tile.isSlopeEdge2 = true;


                            bool lowestCliff = true;
                            if (tile.connectedCliffs.Count >= 2)
                            {
                                lowestCliff = !(tile.connectedCliffs[1].pos == tile.pos + new Vector2Int(0, 1));
                            }
                            
                            if (tile.isCliff && high[1] && lowestCliff)
                            {
                                tile.isCliff = false;
                                tile.isSlopeLowEnd = true;
                                tile.isSlopeCliff = true;
                                tile.isSlopeHigher = false;
                                tile.isSlopeLower = true;
                                high[1] = false;
                            }
                            else
                            {
                                tile.isCliff = false;
                                tile.isSlopeCliff = high[1];
                                tile.isSlopeHigher = high[1];
                                tile.isSlopeLower = !high[1];
                            }
                        }
                        else
                        {
                            tile.isCliff = false;
                        }
                    }
                }
            }

            return validPlacements.Count > 0;
        }
        
        private bool IsAcre(Vector2Int pos)
        {
            return !(pos.x < 0 || pos.x >= numAcres.x ||
                    pos.y < 0 || pos.y >= numAcres.y);
        }
        private Acre GetAcre(Vector2Int pos)
        {
            Assert.IsTrue(IsAcre(pos));
            return acres[pos.x, pos.y];
        }

        private bool IsNeighbour(Acre acre, Vector2Int offset)
        {
            return IsAcre(acre.pos + offset);
        }
        
        private Acre GetNeighbour(Acre acre, Vector2Int offset)
        {
            return GetAcre(acre.pos + offset);
        }
    }
}
