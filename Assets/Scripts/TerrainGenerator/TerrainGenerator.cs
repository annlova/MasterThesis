using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using Microsoft.Win32.SafeHandles;
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
        private int maxCliffWalkReverts;
        
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
        private int minCliffEat;
        
        [SerializeField] 
        private int maxBeachCliffEat;
        
        [SerializeField] 
        private int minBeachCliffEat;
        
        [SerializeField] 
        private int maxCliffTextureNumber;
        
        [SerializeField]
        private int numRivers;
        
        [SerializeField]
        private int riverWidth;
        
        [SerializeField]
        private int slopeWidth;
        
        [SerializeField]
        private int slopeLength;
        
        [SerializeField]
        private float decorationSpacing;
        
        [SerializeField]
        [Range(0, 1)]
        private float treeProbability;
        
        [SerializeField]
        private CliffTile[] cliffTiles;
        
        [SerializeField] 
        private GameObject flatTilePrefab;

        [SerializeField]
        private GameObject riverTilePrefab;

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

        [SerializeField]
        private GameObject beachGridPrefab;

        [SerializeField]
        private GameObject oceanGridPrefab;

        [SerializeField]
        private GameObject treeCanopyPrefab;

        [SerializeField]
        private GameObject treeTrunkPrefab;
        
        [SerializeField]
        private GameObject rockPrefab;

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
        private GameObject beachCliffsRenderObject;
        private GameObject beachCliffsColliderObject;
        private MeshFilter beachCliffsMeshFilter;
        private Renderer beachCliffsRenderer;
        private GameObject riversRenderObject;
        private GameObject riversColliderObject;
        private MeshFilter riversMeshFilter;
        private Renderer riversRenderer;
        private GameObject riversBottomRenderObject;
        private MeshFilter riversBottomMeshFilter;
        private Renderer riversBottomRenderer;
        private GameObject beachRenderObject;
        private GameObject beachColliderObject;
        private MeshFilter beachMeshFilter;
        private Renderer beachRenderer;
        private GameObject oceanRenderObject;
        private GameObject oceanColliderObject;
        private MeshFilter oceanMeshFilter;
        private Renderer oceanRenderer;
        private GameObject canopyRenderObject;
        private GameObject canopyColliderObject;
        private MeshFilter canopyMeshFilter;
        private Renderer canopyRenderer;
        private GameObject trunkRenderObject;
        private GameObject trunkColliderObject;
        private MeshFilter trunkMeshFilter;
        private Renderer trunkRenderer;
        private GameObject rocksRenderObject;
        private GameObject rocksColliderObject;
        private MeshFilter rocksMeshFilter;
        private Renderer rocksRenderer;

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
            ComputeRiverMeta();
            ComputeRivers();
            ComputeSlopes();
            SpawnDecorations();
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
                    var riverOffset = tile.isRiver ? 0.7f : 0.0f;
                    if (tile.isCliff)
                    {
                        Vector3 p = Vector3.zero;
                        for (int i = tile.floor; i < tile.elevation; i++)
                        {
                            p = new Vector3(x + 0.5f, i * 2 - riverOffset, height - 1 - z + 0.5f);
                            var obj = Instantiate(tile.cliffTile.prefab, p, tile.cliffTile.prefab.transform.rotation, transform);
                            
                            // Set cliff texture number
                            var mesh = obj.GetComponent<MeshFilter>().mesh;
                            var uv2 = mesh.uv2;
                            for (int j = 0; j < uv2.Length; j++)
                            {
                                uv2[j] = new Vector2(tile.cliffTextureNumber, 0.0f);
                            }
                            mesh.uv2 = uv2;
                        }

                        if (tile.isBeachCliff)
                        {
                            p = new Vector3(x + 0.5f, -0.3f, height - 1 - z + 0.5f);
                            Instantiate(tile.cliffTile.prefabBeach, p, tile.cliffTile.prefabBeach.transform.rotation, transform);
                            var pRoof = new Vector3(x + 0.5f, tile.elevation * elevationHeight - riverOffset, height - 1 - z + 0.5f);
                            Instantiate(tile.cliffTile.prefabRoof, pRoof, tile.cliffTile.prefabRoof.transform.rotation, transform);
                        }
                        else
                        {
                            if (tile.isBeach)
                            {
                                p = new Vector3(x + 0.5f, -elevationHeight - riverOffset, height - 1 - z + 0.5f);
                                Instantiate(tile.cliffTile.prefab, p, tile.cliffTile.prefab.transform.rotation, transform);
                            }
                            
                            var pRoof = new Vector3(x + 0.5f, tile.elevation * elevationHeight - riverOffset, height - 1 - z + 0.5f);
                            var roofObj = Instantiate(tile.cliffTile.prefabRoof, pRoof, tile.cliffTile.prefabRoof.transform.rotation, transform);
                            setRiverDistAttribute(tile, roofObj);

                            var renderFloor = !tile.isBeach;
                            if (tile.isMergeCliff)
                            {
                                var mergeList = tile.mergeCliffs;
                                foreach (var item in mergeList)
                                {
                                    for (int i = tile.floor; i < item.Item1; i++)
                                    {
                                        var pMerge = new Vector3(x + 0.5f, i * elevationHeight - riverOffset, height - 1 - z + 0.5f);
                                        Instantiate(item.Item2.prefab, pMerge, item.Item2.prefab.transform.rotation, transform);
                                    }

                                    if (item.Item1 == 0 && tile.floor == 0)
                                    {
                                        renderFloor = false;
                                        var pMerge = new Vector3(x + 0.5f, -0.3f, height - 1 - z + 0.5f);
                                        Instantiate(item.Item2.prefabBeach, pMerge, item.Item2.prefabBeach.transform.rotation, transform);
                                    }
                                    else
                                    {
                                        var pSubRoof = new Vector3(x + 0.5f, item.Item1 * elevationHeight - riverOffset, height - 1 - z + 0.5f);
                                        Instantiate(item.Item2.prefabRoof, pSubRoof, item.Item2.prefabRoof.transform.rotation, transform);
                                    }
                                }
                            }

                            if (renderFloor)
                            {
                                var pFloor = new Vector3(x + 0.5f, tile.floor * elevationHeight - riverOffset,
                                    height - 1 - z + 0.5f);
                                var floorObj = Instantiate(flatTilePrefab, pFloor, Quaternion.identity, transform);
                                setRiverDistAttribute(tile, floorObj);


                                // TODO
                                if (tile.isRiverEdge)
                                {
                                    var riverEdgePrefab = cliffTiles[9].prefab;
                                    var rot = Quaternion.FromToRotation(Vector3.back,
                                        new Vector3(tile.riverEdgeDir.x, 0.0f, -tile.riverEdgeDir.y));
                                    Instantiate(riverEdgePrefab, p, rot * riverEdgePrefab.transform.rotation,
                                        transform);
                                }
                                else if (tile.isRiver)
                                {
                                    floorObj.gameObject.tag = "RiverBottom";
                                    roofObj.gameObject.tag = "RiverBottom";
                                    var o1 = Instantiate(riverTilePrefab,
                                        pFloor + new Vector3(0.0f, riverOffset - 0.1f, 0.0f), Quaternion.identity,
                                        transform);
                                    var o2 = Instantiate(riverTilePrefab,
                                        pRoof + new Vector3(0.0f, riverOffset - 0.1f, 0.0f), Quaternion.identity,
                                        transform);
                                    setRiverDirAttribute(tile, o1);
                                    setRiverDirAttribute(tile, o2);
                                }
                            }
                        }
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
                            setRiverDistAttribute(tile, o);
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
                        if (!tile.isBeach)
                        {
                            var p = new Vector3(x + 0.5f, tile.elevation * elevationHeight - riverOffset, height - 1 - z + 0.5f);

                            var floorObj = Instantiate(flatTilePrefab, p, Quaternion.identity, transform);
                            setRiverDistAttribute(tile, floorObj);
                            // TODO
                            if (tile.isRiverEdge)
                            {
                                var riverEdgePrefab = cliffTiles[9].prefab;
                                var rot = Quaternion.FromToRotation(Vector3.back, new Vector3(tile.riverEdgeDir.x, 0.0f, -tile.riverEdgeDir.y));
                                Instantiate(riverEdgePrefab, p + Vector3.down * elevationHeight, rot * riverEdgePrefab.transform.rotation, transform);
                            }
                            else if (tile.isRiver)
                            {
                                floorObj.gameObject.tag = "RiverBottom";
                                var o = Instantiate(riverTilePrefab, p + new Vector3(0.0f, riverOffset - 0.1f, 0.0f), Quaternion.identity, transform);
                                setRiverDirAttribute(tile, o);
                            }
                        }
                    }

                    // if (tile.acre.hasRiver)
                    // {
                    //     tileValues[x + z * width].x = 0.6f;
                    //     tileValues[x + z * width].y = 0.6f;
                    //     tileValues[x + z * width].z = 0.9f;
                    // }
                    if (tile.isBeach)
                    {
                        tileValues[x + z * width].x = 0.0f;
                        tileValues[x + z * width].y = 1.0f;
                        tileValues[x + z * width].z = 0.0f;
                    }
                    
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

            CreateWestBorderCliffs();
            CreateEastBorderCliffs();
            CreateNorthBorderCliffs();

            CreateBeaches();
            
            Combine();
        }

        private void CreateBeaches()
        {
            var size = acreSize;
            var elevation = -0.3f; // TODO
            var oceanOffset = 0.5f; // TODO
            for (int y = 0; y < numAcres.y; y++)
            {
                for (int x = 0; x < numAcres.x; x++)
                {
                    var p = new Vector3(0.0f, elevation, 0.0f);
                    if (x == 0 && y < numAcres.y - 1 && (acres[x, y].elevation == 0 || acres[x, y + 1].elevation == 0))
                    {
                        p.z = height - size - y * size;
                        CreateBeach(p);
                        var beachHeightMap = CreateBeach(p + Vector3.left * size);
                        CreateOcean(p + Vector3.left * size, oceanOffset, beachHeightMap);
                    }
                    else if (y == numAcres.y - 1 && acres[x, y].elevation == 0)
                    {
                        p.x = x * size;
                        p.z = 0.0f;
                        
                        CreateBeach(p);
                        var beachHeightMap = CreateBeach(p + Vector3.back * size);
                        CreateOcean(p + Vector3.back * size, oceanOffset, beachHeightMap);
                    }
                    else if (x == numAcres.x - 1 && y < numAcres.y - 1 && (acres[x, y].elevation == 0 || acres[x, y + 1].elevation == 0))
                    {
                        p.x = width - size;
                        p.z = height - size - y * size;
                        
                        CreateBeach(p);
                        var beachHeightMap = CreateBeach(p + Vector3.right * size);
                        CreateOcean(p + Vector3.right * size, oceanOffset, beachHeightMap);
                    }
                }
            }
            
            var bh = CreateBeach(new Vector3(-size, elevation, -size));
            CreateOcean(new Vector3(-size, elevation, -size), oceanOffset, bh);
            
            bh = CreateBeach(new Vector3(-size, elevation, 0));
            CreateOcean(new Vector3(-size, elevation, 0), oceanOffset, bh);
            
            bh = CreateBeach(new Vector3(numAcres.x * size, elevation, -size));
            CreateOcean(new Vector3(numAcres.x * size, elevation, -size), oceanOffset, bh);
            
            bh = CreateBeach(new Vector3(numAcres.x * size, elevation, 0));
            CreateOcean(new Vector3(numAcres.x * size, elevation, 0), oceanOffset, bh);
        }

        private void CreateOcean(Vector3 pos, float offset, float[] beachHeights)
        {
            var ocean = Instantiate(oceanGridPrefab, pos + Vector3.down * offset, oceanGridPrefab.transform.rotation, transform);
            var mesh = ocean.GetComponent<MeshFilter>().mesh;
            var vertices = mesh.vertices;
            var uv2 = new Vector2[vertices.Length];
            for (int i = 0; i < uv2.Length; i++)
            {
                float height = beachHeights[(int) (vertices[i].x + 0.3f) + (int) (vertices[i].z + 0.3f) * (acreSize + 1)];
                uv2[i] = new Vector2(height, 0.0f);
            }

            mesh.uv2 = uv2;
        }
        
        private float[] CreateBeach(Vector3 pos)
        {
            var size = acreSize + 1;
            var beach = Instantiate(beachGridPrefab, pos, beachGridPrefab.transform.rotation, transform);
            var mesh = beach.GetComponent<MeshFilter>().mesh;
            
            var vertices = mesh.vertices;
            var normals = mesh.normals;

            Assert.IsTrue(vertices.Length == size * size);
            var beachHeight = new float[size * size];
            
            for (int i = 0; i < vertices.Length; i++)
            {
                // Calculate vertex position
                var worldPos = vertices[i] + pos;
                var offset = ComputeBeachVertexOffset(worldPos);
                vertices[i] = vertices[i] + Vector3.down * offset;

                var index = (int) (vertices[i].x + 0.3f) + (int) (vertices[i].z + 0.3f) * size;
                beachHeight[index] = vertices[i].y;
                
                // Calculate new normal
                var worldPosN = worldPos + Vector3.forward;
                var offsetN = ComputeBeachVertexOffset(worldPosN);
                worldPosN += Vector3.down * offsetN;
                var worldPosW = worldPos + Vector3.left;
                var offsetW = ComputeBeachVertexOffset(worldPosW);
                worldPosW += Vector3.down * offsetW;
                var worldPosE = worldPos + Vector3.right;
                var offsetE = ComputeBeachVertexOffset(worldPosE);
                worldPosE += Vector3.down * offsetE;
                var worldPosS = worldPos + Vector3.back;
                var offsetS = ComputeBeachVertexOffset(worldPosS);
                worldPosS += Vector3.down * offsetS;

                var vecN = (worldPosN - worldPos);//.normalized;
                var vecW = (worldPosW - worldPos);//.normalized;
                var vecE = (worldPosE - worldPos);//.normalized;
                var vecS = (worldPosS - worldPos);//.normalized;
                
                var crossWN = Vector3.Cross(vecW, vecN);
                var crossNE = Vector3.Cross(vecN, vecE);
                var crossES = Vector3.Cross(vecE, vecS);
                var crossSW = Vector3.Cross(vecS, vecW);
                var invertedNormal = (crossWN + crossNE + crossES + crossSW).normalized;
                normals[i] = new Vector3(invertedNormal.x, invertedNormal.y, invertedNormal.z);
            }

            mesh.vertices = vertices;
            mesh.normals = normals;

            return beachHeight;
        }

        private float ComputeBeachVertexOffset(Vector3 p)
        {
            var min = new Vector2(0.0f, 0.0f);
            var max = new Vector2(width, height);
            var shoreDist = SqDistPointAABB(new Vector2(p.x, p.z), min, max);
            var falloffTerm = shoreDist / ((acreSize + 1) * (acreSize + 1));
            var waveTerm = 0.0f;
            var waveFactor = 0.08f;
            if (p.x < min.x || p.x > max.x)
            {
                waveTerm += ((float) Math.Sin(p.z * 0.5f) + (float) Math.Sin(p.z * 1.0f) + 2.0f) * waveFactor;
                // + (float) Math.Sin(p.z * 14.5f) + 2.0f
            }
            else if (p.z < min.y)
            {
                waveTerm += ((float) Math.Sin(p.x * 0.5f) + (float) Math.Sin(p.x * 0.2f) + 2.0f) * waveFactor;
                //  + (float) Math.Sin(p.x * 24.0f) + 2.0f
            }
            return falloffTerm * 5.0f + waveTerm;
        }
        
        private float SqDistPointAABB(Vector2 p, Vector2 min, Vector2 max)
        {
            float sqDist = 0.0f;
            {
                float vx = p.x;
                if (vx < min.x) { sqDist += (min.x - vx) * (min.x - vx); }
                if (vx > max.x) { sqDist += (vx - max.x) * (vx - max.x); }
            }
            {
                float vy = p.y;
                if (vy < min.y) { sqDist += (min.y - vy) * (min.y - vy); }
                if (vy > max.y) { sqDist += (vy - max.x) * (vy - max.y); }
            }
            return sqDist;
        }
        
        private void CreateWestBorderCliffs()
        {
            // Create west edge
            var prevElevation = elevationRangeMax - 1;
            for (int i = 1; i < numAcres.y * acreSize; i++)
            {
                var t = tiles[0, i];
                var cliffTileFlat = cliffTiles[7];
                var p = new Vector3(-0.5f, 0, height - 1 - (i - 1) + 0.5f);
                for (int h = t.elevation; h < prevElevation + 2; h++)
                {
                    p.y = h * elevationHeight;
                    Instantiate(cliffTileFlat.prefab,  p, cliffTileFlat.prefab.transform.rotation, transform);
                }
                Instantiate(cliffTileFlat.prefabRoof, p + Vector3.up * elevationHeight, cliffTileFlat.prefabRoof.transform.rotation, transform);
                Instantiate(flatTilePrefab, p + Vector3.up * elevationHeight + Vector3.left, flatTilePrefab.transform.rotation, transform);

                if (prevElevation > t.elevation)
                {
                    if (t.elevation == 0)
                    {
                        p.y = t.elevation;
                        Instantiate(cliffTileFlat.prefab,  p + Vector3.down * elevationHeight, cliffTileFlat.prefab.transform.rotation, transform);
                    }

                    var cliffTileCorner = cliffTiles[18];
                    var cliffTileAway = cliffTiles[8];
                    int startH = t.elevation < 1 ? 0 : prevElevation + 1;
                    p.y = startH * elevationHeight - elevationHeight;
                    Instantiate(cliffTileCorner.prefab, p + Vector3.back, cliffTileCorner.prefab.transform.rotation, transform);
                    for (int h = startH; h < prevElevation + 2; h++)
                    {
                        p.y = h * elevationHeight;
                        Instantiate(cliffTileCorner.prefab, p + Vector3.back, cliffTileCorner.prefab.transform.rotation, transform);
                        for (int x = 1; x < (t.elevation < 1 ? acreSize : 2); x++)
                        {
                            Instantiate(cliffTileAway.prefab, p + Vector3.back + Vector3.left * x, cliffTileAway.prefab.transform.rotation, transform);
                            if (h == startH)
                            {
                                Instantiate(cliffTileAway.prefab, p + Vector3.back + Vector3.left * x + Vector3.down * elevationHeight, cliffTileAway.prefab.transform.rotation, transform);
                            }
                        }
                    }
                    Instantiate(cliffTileCorner.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back, cliffTileCorner.prefabRoof.transform.rotation, transform);
                    for (int x = 1; x < (t.elevation < 1 ? acreSize : 2); x++)
                    {
                        Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back + Vector3.left * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                    }
                }
                if (t.elevation < 1)
                {
                    break;//TODO
                }

                prevElevation = t.elevation;
            }
        }
        private void CreateEastBorderCliffs()
        {
            // Create west edge
            var prevElevation = elevationRangeMax - 1;
            for (int i = 1; i < numAcres.y * acreSize; i++)
            {
                var t = tiles[numAcres.x * acreSize - 1, i];
                var cliffTileFlat = cliffTiles[25];
                var p = new Vector3(numAcres.x * acreSize + 0.5f, 0, height - 1 - (i - 1) + 0.5f);
                for (int h = t.elevation; h < prevElevation + 2; h++)
                {
                    p.y = h * elevationHeight;
                    Instantiate(cliffTileFlat.prefab,  p, cliffTileFlat.prefab.transform.rotation, transform);
                }
                Instantiate(cliffTileFlat.prefabRoof, p + Vector3.up * elevationHeight, cliffTileFlat.prefabRoof.transform.rotation, transform);
                Instantiate(flatTilePrefab, p + Vector3.up * elevationHeight + Vector3.right, flatTilePrefab.transform.rotation, transform);

                if (prevElevation > t.elevation)
                {
                    if (t.elevation == 0)
                    {
                        p.y = t.elevation;
                        Instantiate(cliffTileFlat.prefab,  p + Vector3.down * elevationHeight, cliffTileFlat.prefab.transform.rotation, transform);
                    }

                    var cliffTileCorner = cliffTiles[23];
                    var cliffTileAway = cliffTiles[8];
                    int startH = t.elevation < 1 ? 0 : prevElevation + 1;
                    p.y = startH * elevationHeight - elevationHeight;
                    Instantiate(cliffTileCorner.prefab, p + Vector3.back, cliffTileCorner.prefab.transform.rotation, transform);
                    for (int h = startH; h < prevElevation + 2; h++)
                    {
                        p.y = h * elevationHeight;
                        Instantiate(cliffTileCorner.prefab, p + Vector3.back, cliffTileCorner.prefab.transform.rotation, transform);
                        for (int x = 1; x < (t.elevation < 1 ? acreSize : 2); x++)
                        {
                            Instantiate(cliffTileAway.prefab, p + Vector3.back + Vector3.right * x, cliffTileAway.prefab.transform.rotation, transform);
                            if (h == startH)
                            {
                                Instantiate(cliffTileAway.prefab, p + Vector3.back + Vector3.right * x + Vector3.down * elevationHeight, cliffTileAway.prefab.transform.rotation, transform);
                            }
                        }
                    }
                    Instantiate(cliffTileCorner.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back, cliffTileCorner.prefabRoof.transform.rotation, transform);
                    for (int x = 1; x < (t.elevation < 1 ? acreSize : 2); x++)
                    {
                        Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back + Vector3.right * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                    }
                }
                if (t.elevation < 1)
                {
                    break;//TODO
                }

                prevElevation = t.elevation;
            }
        }
        private void CreateNorthBorderCliffs()
        {
            var t = tiles[0, 0];
            var cliffTileFlat = cliffTiles[9];
            for (int i = -2; i < numAcres.x * acreSize + 2; i++)
            {
                var p = new Vector3(i + 0.5f, t.elevation * 2.0f, height - 1 + 1.5f);
                Instantiate(cliffTileFlat.prefab, p, cliffTileFlat.prefab.transform.rotation, transform);
                Instantiate(cliffTileFlat.prefab, p + Vector3.up * 2.0f, cliffTileFlat.prefab.transform.rotation, transform);
                for (int z = 0; z < 3; z++)
                {
                    Instantiate(flatTilePrefab, p + Vector3.up * 4.0f + Vector3.forward * z, flatTilePrefab.transform.rotation, transform);
                }
            }
        }

        private void setRiverDirAttribute(Tile tile, GameObject obj)
        {
            var mesh = obj.GetComponent<MeshFilter>().mesh;
            var uv2 = mesh.uv;
            for (int i = 0; i < uv2.Length; i++)
            {
                uv2[i] = tile.riverDir;
            }

            mesh.uv2 = uv2;
        }

        private void setRiverDistAttribute(Tile tile, GameObject obj)
        {
            var mesh = obj.GetComponent<MeshFilter>().mesh;
            var uv2 = mesh.uv;
            for (int i = 0; i < uv2.Length; i++)
            {
                uv2[i] = new Vector2(tile.vertexRiverValue[i % 4], tile.riverLineFactor);
            }
        
            mesh.uv3 = uv2;
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
            var combineBeachCliffs = new List<CombineInstance>();
            var combineBeach = new List<CombineInstance>();
            var combineRivers = new List<CombineInstance>();
            var combineRiversBottom = new List<CombineInstance>();
            var combineOcean = new List<CombineInstance>();
            var combineCanopies = new List<CombineInstance>();
            var combineTrunks = new List<CombineInstance>();
            var combineRocks = new List<CombineInstance>();

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
                    case "BeachCliff":
                        var beachCliff = new CombineInstance();
                        beachCliff.mesh = meshFilters[i].mesh;
                        beachCliff.transform = meshFilters[i].transform.localToWorldMatrix;
                        var beachCliffCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (beachCliffCollider)
                        {
                            beachCliffCollider.transform.SetParent(beachCliffsColliderObject.transform);
                        }
                        combineBeachCliffs.Add(beachCliff);
                        break;
                    case "Beach":
                        var beach = new CombineInstance();
                        beach.mesh = meshFilters[i].mesh;
                        beach.transform = meshFilters[i].transform.localToWorldMatrix;
                        var beachCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (beachCollider)
                        {
                            beachCollider.transform.SetParent(beachColliderObject.transform);
                        }
                        combineBeach.Add(beach);
                        break;
                    case "RiverSurface":
                        var river = new CombineInstance();
                        river.mesh = meshFilters[i].mesh;
                        river.transform = meshFilters[i].transform.localToWorldMatrix;
                        var riverCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (riverCollider)
                        {
                            riverCollider.transform.SetParent(riversColliderObject.transform);
                        }
                        combineRivers.Add(river);
                        break;
                    case "RiverBottom":
                        var riverBottom = new CombineInstance();
                        riverBottom.mesh = meshFilters[i].mesh;
                        riverBottom.transform = meshFilters[i].transform.localToWorldMatrix;
                        combineRiversBottom.Add(riverBottom);
                        break;
                    case "Ocean":
                        var ocean = new CombineInstance();
                        ocean.mesh = meshFilters[i].mesh;
                        ocean.transform = meshFilters[i].transform.localToWorldMatrix;
                        var oceanCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (oceanCollider)
                        {
                            oceanCollider.transform.SetParent(oceanColliderObject.transform);
                        }
                        combineOcean.Add(ocean);
                        break;
                    case "Canopy":
                        var canopy = new CombineInstance();
                        canopy.mesh = meshFilters[i].mesh;
                        canopy.transform = meshFilters[i].transform.localToWorldMatrix;
                        var canopyCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (canopyCollider)
                        {
                            canopyCollider.transform.SetParent(canopyColliderObject.transform);
                        }
                        combineCanopies.Add(canopy);
                        break;
                    case "Trunk":
                        var trunk = new CombineInstance();
                        trunk.mesh = meshFilters[i].mesh;
                        trunk.transform = meshFilters[i].transform.localToWorldMatrix;
                        var trunkCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (trunkCollider)
                        {
                            trunkCollider.transform.SetParent(trunkColliderObject.transform);
                        }
                        combineTrunks.Add(trunk);
                        break;
                    case "Rock":
                        var rock = new CombineInstance();
                        rock.mesh = meshFilters[i].mesh;
                        rock.transform = meshFilters[i].transform.localToWorldMatrix;
                        var rockCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (rockCollider)
                        {
                            rockCollider.transform.SetParent(rocksColliderObject.transform);
                        }
                        combineRocks.Add(rock);
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

            beachCliffsMeshFilter.sharedMesh = new Mesh();
            beachCliffsMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            beachCliffsMeshFilter.sharedMesh.CombineMeshes(combineBeachCliffs.ToArray());
            beachCliffsMeshFilter.gameObject.SetActive(true);

            beachMeshFilter.sharedMesh = new Mesh();
            beachMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            beachMeshFilter.sharedMesh.CombineMeshes(combineBeach.ToArray());
            beachMeshFilter.gameObject.SetActive(true);

            riversMeshFilter.sharedMesh = new Mesh();
            riversMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            riversMeshFilter.sharedMesh.CombineMeshes(combineRivers.ToArray());
            riversMeshFilter.gameObject.SetActive(true);

            riversBottomMeshFilter.sharedMesh = new Mesh();
            riversBottomMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            riversBottomMeshFilter.sharedMesh.CombineMeshes(combineRiversBottom.ToArray());
            riversBottomMeshFilter.gameObject.SetActive(true);

            oceanMeshFilter.sharedMesh = new Mesh();
            oceanMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            oceanMeshFilter.sharedMesh.CombineMeshes(combineOcean.ToArray());
            oceanMeshFilter.gameObject.SetActive(true);

            canopyMeshFilter.sharedMesh = new Mesh();
            canopyMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            canopyMeshFilter.sharedMesh.CombineMeshes(combineCanopies.ToArray());
            canopyMeshFilter.gameObject.SetActive(true);

            trunkMeshFilter.sharedMesh = new Mesh();
            trunkMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            trunkMeshFilter.sharedMesh.CombineMeshes(combineTrunks.ToArray());
            trunkMeshFilter.gameObject.SetActive(true);

            rocksMeshFilter.sharedMesh = new Mesh();
            rocksMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            rocksMeshFilter.sharedMesh.CombineMeshes(combineRocks.ToArray());
            rocksMeshFilter.gameObject.SetActive(true);
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

            beachCliffsRenderObject = transform.Find("BeachCliffsRender").gameObject;
            beachCliffsColliderObject = transform.Find("BeachCliffsCollider").gameObject;
            beachCliffsMeshFilter = beachCliffsRenderObject.GetComponent<MeshFilter>();
            beachCliffsRenderer = beachCliffsRenderObject.GetComponent<Renderer>();
            
            riversRenderObject = transform.Find("RiversRender").gameObject;
            riversColliderObject = transform.Find("RiversCollider").gameObject;
            riversMeshFilter = riversRenderObject.GetComponent<MeshFilter>();
            riversRenderer = riversRenderObject.GetComponent<Renderer>();
            
            riversBottomRenderObject = transform.Find("RiversBottomRender").gameObject;
            riversBottomMeshFilter = riversBottomRenderObject.GetComponent<MeshFilter>();
            riversBottomRenderer = riversBottomRenderObject.GetComponent<Renderer>();
            
            beachRenderObject = transform.Find("BeachRender").gameObject;
            beachColliderObject = transform.Find("BeachCollider").gameObject;
            beachMeshFilter = beachRenderObject.GetComponent<MeshFilter>();
            beachRenderer = beachRenderObject.GetComponent<Renderer>();
            
            oceanRenderObject = transform.Find("OceanRender").gameObject;
            oceanColliderObject = transform.Find("OceanCollider").gameObject;
            oceanMeshFilter = oceanRenderObject.GetComponent<MeshFilter>();
            oceanRenderer = oceanRenderObject.GetComponent<Renderer>();
            
            canopyRenderObject = transform.Find("CanopyRender").gameObject;
            canopyColliderObject = transform.Find("CanopyCollider").gameObject;
            canopyMeshFilter = canopyRenderObject.GetComponent<MeshFilter>();
            canopyRenderer = canopyRenderObject.GetComponent<Renderer>();
            
            trunkRenderObject = transform.Find("TrunkRender").gameObject;
            trunkColliderObject = transform.Find("TrunkCollider").gameObject;
            trunkMeshFilter = trunkRenderObject.GetComponent<MeshFilter>();
            trunkRenderer = trunkRenderObject.GetComponent<Renderer>();
            
            rocksRenderObject = transform.Find("RocksRender").gameObject;
            rocksColliderObject = transform.Find("RocksCollider").gameObject;
            rocksMeshFilter = rocksRenderObject.GetComponent<MeshFilter>();
            rocksRenderer = rocksRenderObject.GetComponent<Renderer>();
            
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

            for (int i = 0; i < numRivers; i++)
            {
                BasicAcreRiverSelection();
            }
        }

        private void BasicAcreRiverSelection()
        {
            // Select top tile
            var topX = Random.Range(0, numAcres.x);
            var tries = 0;
            while (acres[topX, 0].hasRiver && tries < numAcres.x)
            {
                topX = (topX + 1) % numAcres.x;
                tries++;
            }

            if (tries == numAcres.x)
            {
                throw new NotSupportedException();
            }
            
            // Do acre river walk
            var acre = acres[topX, 0];
            acre.hasRiver = true;
            acre.hasRiverNorth = true;

            while (true) {
                var moveSelection = Random.Range(0, 3);
                bool validSelection = false;
                while (!validSelection)
                {
                    switch (moveSelection)
                    {
                        case 0:
                            if (IsNeighbour(acre, Vector2Int.left))
                            {
                                var left = GetNeighbour(acre, Vector2Int.left);
                                if (left.elevation <= acre.elevation && !left.hasRiverEast)
                                {
                                    acre.hasRiverWest = true;
                                    acre.riverWestFlowsWest = true;
                                    left.hasRiverEast = true;
                                    left.riverEastFlowsEast = false;
                                    acre = left;
                                    validSelection = true;
                                    continue;
                                }
                            }

                            moveSelection = 2;
                            break;
                        case 1:
                            if (IsNeighbour(acre, Vector2Int.right))
                            {
                                var right = GetNeighbour(acre, Vector2Int.right);
                                if (right.elevation <= acre.elevation && !right.hasRiverWest)
                                {
                                    acre.hasRiverEast = true;
                                    acre.riverEastFlowsEast = true;
                                    right.hasRiverWest = true;
                                    right.riverWestFlowsWest = false;
                                    acre = right;
                                    validSelection = true;
                                    continue;
                                }
                            }

                            moveSelection = 2;
                            break;
                        case 2:
                            if (IsNeighbour(acre, Vector2Int.up))
                            {
                                acre.hasRiverSouth = true;
                                var down = GetNeighbour(acre, Vector2Int.up);
                                down.hasRiverNorth = true;
                                acre = down;
                                validSelection = true;
                                continue;
                            }

                            // Done!
                            acre.hasRiverSouth = true;
                            return;
                        default:
                            throw new NotImplementedException();
                    }
                }

                if (!acre.hasRiver)
                {
                    acre.hasRiver = true;
                }
                else
                {
                    return;
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

                    if ((w != null && acre.elevation > w.elevation) ||
                        (acre.elevation == 0 && x == 0))
                    {
                        acre.hasWestCliff = true;
                    }

                    if (n != null && acre.elevation > n.elevation)
                    {
                        throw new Exception("North acre elevation should not be lower!");
                    }

                    if ((e != null && acre.elevation > e.elevation) ||
                        (acre.elevation == 0 && x == numAcres.x - 1))
                    {
                        acre.hasEastCliff = true;
                    }

                    if ((s != null && acre.elevation > s.elevation) ||
                        (acre.elevation == 0 && y == numAcres.y - 1))
                    {
                        acre.hasSouthCliff = true;
                    }

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
            var maxEat = startAcre.elevation > 0 ? maxCliffEat : maxBeachCliffEat;
            var minEat = startAcre.elevation > 0 ? minCliffEat : minBeachCliffEat;
            // if (startAcre.elevation == 0 || startAcre.cliffWalked)
            // {
            //     return (null, true);
            // }
            
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
                        startTile = FindStartTileWS(neighbour, minEat, maxEat);
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
                                (startTile, forward, right) = FindStartTileWSWE(neighbour, minEat, maxEat);
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
                        startTile = FindStartTileSE(neighbour, minEat, maxEat);
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
                cliffTiles, maxEat, minEat,
                maxCliffTextureNumber,
                startTile.pos, forward, right,
                maxCliffWalkReverts
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

        private Tile FindStartTileWS(Acre acre, int min, int max)
        {
            var acreTilePos = acre.pos * acreSize;
            var startPos = acreTilePos + new Vector2Int(min, acreSize - 1);
            for (int y = startPos.y; y >= acreTilePos.y + acreSize - max; y--)
            {
                for (int x = startPos.x; x < acreTilePos.x + max; x++)
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

        private (Tile, Vector2Int, Vector2Int) FindStartTileWSWE(Acre acre, int min, int max)
        {
            var acreTilePos = acre.pos * acreSize;
            var startPos = acreTilePos + new Vector2Int(acreSize - 1, acreSize - 1 - min);
            for (int x = startPos.x; x >= acreTilePos.x + acreSize - max; x--)
            {
                for (int y = startPos.y; y >= acreTilePos.y + acreSize - max; y--)
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
        
        private Tile FindStartTileSE(Acre acre, int min, int max)
        {
            var acreTilePos = acre.pos * acreSize;
            var startPos = acreTilePos + new Vector2Int(acreSize - 1, acreSize - 1 - min);
            for (int x = startPos.x; x >= acreTilePos.x + acreSize - max; x--)
            {
                for (int y = startPos.y; y >= acreTilePos.y + acreSize - max; y--)
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
                    var lowest = numLayers;

                    var todo = new List<Vector2Int>();
                    todo.Add(new Vector2Int(x, y));

                    var mapEdgeReached = false;
                    for (int i = 0; i < todo.Count; i++)
                    {
                        var p = todo[i];
                        if (p.x < 0 || p.x >= width ||
                            p.y < 0 || p.y >= height)
                        {
                            mapEdgeReached = true;
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

                    var isBeach = mapEdgeReached && (lowest <= 0);
                    foreach (var tile in list)
                    {
                        tile.elevation = lowest;
                        tile.isBeach = isBeach;
                    }

                    foreach (var tile in cliffList)
                    {
                        tile.isBeach |= isBeach;
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
                                    tile.isBeach |= t.isBeach;
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
                        valid &= tileAbove.elevation == acre.elevation && !tileAbove.isCliff && !tileAbove.isRiver;
                        var tileBelow = tiles[x + i, y + slopeLength - 1];
                        valid &= tileBelow.elevation == acre.elevation - 1 && !tileBelow.isCliff && !tileBelow.isRiver;
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

        private void ComputeWaterfalls()
        {
            foreach (var acre in acres)
            {
                // No river, no waterfall
                if (!acre.hasRiver)
                {
                    continue;
                }

                // Find first cliff tile
                var startCliff = FindFirstCliffInAcre(acre);
                if (startCliff == null)
                {
                    // No cliffs, no waterfall
                    continue;
                }
                
                // Add waterfall if needed
                if (acre.hasRiverWest && acre.hasWestCliff)
                {
                    // startCliff.
                }
                else if (acre.hasRiverSouth && acre.hasSouthCliff)
                {
                    
                }
                else if (acre.hasRiverEast && acre.hasEastCliff)
                {
                    
                }
            }
        }

        private Tile FindFirstCliffInAcre(Acre acre)
        {
            if (acre.hasWestCliff)
            {
                return FindFirstWestCliffTile(acre);
            }
            else if (acre.hasSouthCliff)
            {
                return FindFirstSouthCliffTile(acre);
            }
            else if (acre.hasEastCliff)
            {
                return FindFirstEastCliffTile(acre);
            }

            return null;
        }
        private Tile FindFirstWestCliffTile(Acre acre)
        {
            Tile startCliff = null;
            Vector2Int acrePos = acre.pos * acreSize;
            for (int x = 0; x < acreSize; x++)
            {
                var tile = tiles[acrePos.x + x, acrePos.y];
                if (tile.isCliff)
                {
                    startCliff = tile;
                    break;
                }
            }

            if (startCliff == null)
            {
                throw new NotSupportedException();
            }

            return startCliff;
        }
        
        private Tile FindFirstSouthCliffTile(Acre acre)
        {
            Tile startCliff = null;
            Vector2Int acrePos = acre.pos * acreSize;
            for (int y = 0; y < acreSize; y++)
            {
                var tile = tiles[acrePos.x, acrePos.y + acreSize - 1 - y];
                if (tile.isCliff)
                {
                    startCliff = tile;
                    break;
                }
            }

            if (startCliff == null)
            {
                throw new NotSupportedException();
            }

            return startCliff;
        }
        
        private Tile FindFirstEastCliffTile(Acre acre)
        {
            Tile startCliff = null;
            Vector2Int acrePos = acre.pos * acreSize;
            for (int x = 0; x < acreSize; x++)
            {
                var tile = tiles[acrePos.x + acreSize - 1 - x, acrePos.y + acreSize - 1];
                if (tile.isCliff)
                {
                    startCliff = tile;
                    break;
                }
            }

            if (startCliff == null)
            {
                throw new NotSupportedException();
            }

            return startCliff;
        }

        // TODO: Create real river walk agent
        private void ComputeRivers()
        {
            foreach (var acre in acres)
            {
                if (acre.hasRiver)
                {
                    var pos = acre.pos * acreSize;
                    var centerRiverDir = new Vector2(0.0f, 0.0f);
                    if (acre.hasRiverNorth)
                    {
                        centerRiverDir += Vector2.down;
                        for (int x = 0; x < riverWidth; x++)
                        {
                            for (int y = 0; y <= acreSize / 2; y++)
                            {
                                var p = new Vector2Int(pos.x + acreSize / 2 - riverWidth / 2 + x, pos.y + y);
                                var t = tiles[p.x, p.y];
                                if (t.isRiver)
                                {
                                    continue;
                                }
                                if (x == 0)
                                {
                                    tiles[p.x, p.y].isRiverEdge = true;
                                    tiles[p.x, p.y].riverEdgeDir = new Vector2Int(1, 0);
                                }
                                else if (x == riverWidth - 1)
                                {
                                    tiles[p.x, p.y].isRiverEdge = true;
                                    tiles[p.x, p.y].riverEdgeDir = new Vector2Int(-1, 0);
                                }
                                else
                                {
                                    tiles[p.x, p.y].isRiver = true;
                                    tiles[p.x, p.y].isRiverEdge = false;
                                    tiles[p.x, p.y].riverDir = Vector2.down;
                                }
                            }
                        }
                    }
                    if (acre.hasRiverWest)
                    {
                        centerRiverDir += acre.riverWestFlowsWest ? Vector2.left : Vector2.right;
                        for (int y = 0; y < riverWidth; y++)
                        {
                            for (int x = 0; x <= acreSize / 2; x++)
                            {
                                var p = new Vector2Int(pos.x + x, pos.y + acreSize / 2 - riverWidth / 2 + y);
                                var t = tiles[p.x, p.y];
                                if (t.isRiver)
                                {
                                    continue;
                                }
                                if (y == 0)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, 1);
                                }
                                else if (y == riverWidth - 1)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, -1);
                                }
                                else
                                {
                                    t.isRiver = true;
                                    t.isRiverEdge = false;
                                    t.riverDir = t.acre.riverWestFlowsWest ? Vector2.left : Vector2.right;
                                }
                            }
                        }
                    }
                    if (acre.hasRiverEast)
                    {
                        centerRiverDir += acre.riverEastFlowsEast ? Vector2.right : Vector2.left;
                        for (int y = 0; y < riverWidth; y++)
                        {
                            for (int x = acreSize - 1; x >= acreSize / 2; x--)
                            {
                                var p = new Vector2Int(pos.x + x, pos.y + acreSize / 2 - riverWidth / 2 + y);
                                var t = tiles[p.x, p.y];
                                if (t.isRiver)
                                {
                                    continue;
                                }
                                if (y == 0)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, 1);
                                }
                                else if (y == riverWidth - 1)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, -1);
                                }
                                else
                                {
                                    t.isRiver = true;
                                    t.isRiverEdge = false;
                                    t.riverDir = t.acre.riverEastFlowsEast ? Vector2.right : Vector2.left;
                                }
                            }
                        }
                    }
                    if (acre.hasRiverSouth)
                    {
                        centerRiverDir += Vector2.down;
                        for (int x = 0; x < riverWidth; x++)
                        {
                            for (int y = acreSize / 2 - 1; y < acreSize; y++)
                            {
                                var p = new Vector2Int(pos.x + acreSize / 2 - riverWidth / 2 + x, pos.y + y);
                                var t = tiles[p.x, p.y];
                                if (t.isRiver)
                                {
                                    continue;
                                }
                                if (x == 0)
                                {
                                    tiles[p.x, p.y].isRiverEdge = true;
                                    tiles[p.x, p.y].riverEdgeDir = new Vector2Int(1, 0);
                                }
                                else if (x == riverWidth - 1)
                                {
                                    tiles[p.x, p.y].isRiverEdge = true;
                                    tiles[p.x, p.y].riverEdgeDir = new Vector2Int(-1, 0);
                                }
                                else
                                {
                                    tiles[p.x, p.y].isRiver = true;
                                    tiles[p.x, p.y].isRiverEdge = false;
                                    tiles[p.x, p.y].riverDir = Vector2.down;
                                }
                            }
                        }
                    }

                    centerRiverDir = centerRiverDir.normalized;
                    // Fix middle tiles
                    for (int y = 0; y < riverWidth / 2; y++)
                    {
                        for (int x = 0; x < riverWidth / 2; x++)
                        {
                            var p = new Vector2Int(pos.x + acreSize / 2 - riverWidth / 2 + 1 + x, pos.y + acreSize / 2 + 1 - riverWidth / 2 + y);
                            var t = tiles[p.x, p.y];
                            t.isRiverEdge = false;
                            t.isRiver = true;
                            t.riverDir = centerRiverDir;
                        }
                    }
                }
            }
        }

        private void ComputeRiverMeta()
        {
            foreach (var acre in acres)
            {
                ComputeAcreRiverMeta(acre);
                WalkRiverCenterLine(acre);
                ComputeAcreCenterLineMeta(acre);
                ComputeRealRiverValue(acre);
            }
            
            SmoothRiverValues();
        }

        private void ComputeRealRiverValue(Acre acre)
        {
            for (int y = 0; y < acreSize; y++)
            {
                for (int x = 0; x < acreSize; x++)
                {
                    var p = acre.pos * acreSize + new Vector2Int(x, y);
                    var tile = tiles[p.x, p.y];
                    tile.riverValue = (float) tile.riverValueLine / (tile.riverValueLine + tile.riverValueCliff);
                }
            }
        }
        
        private void ComputeAcreCenterLineMeta(Acre acre)
        {
            var list = new List<Tile>();
            for (int y = 0; y < acreSize; y++)
            {
                for (int x = 0; x < acreSize; x++)
                {
                    var p = acre.pos * acreSize + new Vector2Int(x, y);
                    var tile = tiles[p.x, p.y];
                    if (tile.riverValueLine == 0)
                    {
                        list.Add(tile);
                    }
                }
            }

            foreach (var tile in list)
            {
                SetRiverLineValues(tile);
            }
        }
        
        private void SetRiverLineValues(Tile tile)
        {
            var todo = new List<Tile>();
            todo.Add(tile);

            for (int i = 0; i < todo.Count; i++)
            {
                tile = todo[i];
                var p = new Vector2Int[8];
                p[0] = new Vector2Int(-1, -1);
                p[1] = new Vector2Int(0, -1);
                p[2] = new Vector2Int(1, -1);
                p[3] = new Vector2Int(-1, 0);
                p[4] = new Vector2Int(1, 0);
                p[5] = new Vector2Int(-1, 1);
                p[6] = new Vector2Int(0, 1);
                p[7] = new Vector2Int(1, 1);
                foreach (var pn in p)
                {
                    if (IsNeighbour(tile, pn))
                    {
                        var t = GetNeighbour(tile, pn);
                        if (t.riverValueLine < 0 || t.riverValueLine > tile.riverValueLine + 1)
                        {
                            t.riverValueLine = tile.riverValueLine + 1;
                            t.riverLineFactor = tile.riverLineFactor;
                            todo.Add(t);
                        }
                    }
                }
            }
        }

        private void WalkRiverCenterLine(Acre acre)
        {
            // TODO: Temp
            var start = new Vector2Int(8, 15);
            var end = new Vector2Int(8, 0);

            var sTile = tiles[acre.pos.x * acreSize + start.x, acre.pos.y * acreSize + start.y];
            var eTile = tiles[acre.pos.x * acreSize + end.x, acre.pos.y * acreSize + end.y];
            start += acre.pos * acreSize;
            end += acre.pos * acreSize;
            var sDone = false;
            var eDone = true;
            while (!(sDone && eDone))
            {
                if (!sDone)
                {
                    var sTemp = SetRiverLine(sTile, start, end);
                    sDone = sTile == sTemp;
                    sTile = sTemp;
                }

                // if (!eDone)
                // {
                //     var eTemp = SetRiverLine(eTile, end, start);
                //     eDone = eTile == eTemp;
                //     eTile = eTemp;
                // }
            }

            for (int y = 0; y < acreSize; y++)
            {
                for (int x = 0; x < acreSize; x++)
                {
                    var p = acre.pos * acreSize + new Vector2Int(x, y);
                    var tile = tiles[p.x, p.y];
                    tile.riverLineFactor /= xc - 1;
                }
            }
            
            xc = 0;
        }

        private int xc = 0;
        private Tile SetRiverLine(Tile tile, Vector2Int start, Vector2Int end)
        {
            tile.riverValueLine = 0;
            tile.riverLineFactor = xc++;
            var p = new Vector2Int[8];
            p[0] = new Vector2Int(-1, -1);
            p[1] = new Vector2Int(0, -1);
            p[2] = new Vector2Int(1, -1);
            p[3] = new Vector2Int(-1, 0);
            p[4] = new Vector2Int(1, 0);
            p[5] = new Vector2Int(-1, 1);
            p[6] = new Vector2Int(0, 1);
            p[7] = new Vector2Int(1, 1);
            var highest = -1;
            float highestDistEnd = acreSize + 1;
            var chosen = tile;
            foreach (var pn in p)
            {
                if (IsNeighbour(tile, pn))
                {
                    
                    var t = GetNeighbour(tile, pn);
                    var distStart1 = Vector2Int.Distance(t.pos, start);
                    var distStart2 = Vector2Int.Distance(t.pos + pn, start);
                    var distEnd1 = Vector2Int.Distance(tile.pos, end);
                    var distEnd2 = Vector2Int.Distance(t.pos, end);
                    if (distEnd1 <= distEnd2)
                    {
                        continue;
                    }
                    
                    if (t.riverValueCliff > highest || (t.riverValueCliff == highest && !t.riverValueLineVisited && distEnd2 < highestDistEnd))
                    {
                        highest = t.riverValueCliff;
                        highestDistEnd = distEnd2;
                        chosen = t;

                    }
                }
            }

            return chosen;
        }
        
        private void SmoothRiverValues()
        {
            var nw = new Vector2Int(-1, -1);
            var n = new Vector2Int(0, -1);
            var ne = new Vector2Int(1, -1);
            var w = new Vector2Int(-1, 0);
            var e = new Vector2Int(1, 0);
            var sw = new Vector2Int(-1, 1);
            var s = new Vector2Int(0, 1);
            var se = new Vector2Int(1, 1);
            foreach (var tile in tiles)
            {
                var l = new List<float>();
                SmoothRiverValuesAddNeighbour(l, tile, w);
                SmoothRiverValuesAddNeighbour(l, tile, nw);
                SmoothRiverValuesAddNeighbour(l, tile, n);
                l.Add(tile.riverValue);
                tile.vertexRiverValue[2] = BilinearInterpolation(l[0], l[1], l[2], l[3]);
                l.Clear();

                SmoothRiverValuesAddNeighbour(l, tile, n);
                SmoothRiverValuesAddNeighbour(l, tile, ne);
                SmoothRiverValuesAddNeighbour(l, tile, e);
                l.Add(tile.riverValue);
                tile.vertexRiverValue[3] = BilinearInterpolation(l[0], l[1], l[2], l[3]);
                l.Clear();

                SmoothRiverValuesAddNeighbour(l, tile, sw);
                SmoothRiverValuesAddNeighbour(l, tile, w);
                l.Add(tile.riverValue);
                SmoothRiverValuesAddNeighbour(l, tile, s);
                tile.vertexRiverValue[1] = BilinearInterpolation(l[0], l[1], l[2], l[3]);
                l.Clear();

                SmoothRiverValuesAddNeighbour(l, tile, s);
                l.Add(tile.riverValue);
                SmoothRiverValuesAddNeighbour(l, tile, e);
                SmoothRiverValuesAddNeighbour(l, tile, se);
                tile.vertexRiverValue[0] = BilinearInterpolation(l[0], l[1], l[2], l[3]);
                l.Clear();
            }
        }

        private float BilinearInterpolation(float a, float b, float c, float d)
        {
            var e = a * 0.5f + b * 0.5f;
            var f = c * 0.5f + d * 0.5f;
            return e * 0.5f + f * 0.5f;
        }

        private void SmoothRiverValuesAddNeighbour(List<float> l, Tile tile, Vector2Int offset)
        {
            if (IsNeighbour(tile, offset))
            {
                l.Add(GetNeighbour(tile, offset).riverValue);
            }
            else
            {
                l.Add(tile.riverValue);
            }
        }

        private void ComputeAcreRiverMeta(Acre acre)
        {
            (var cliffs, var flats) = ComputeRiverMetaFindCliffsAndFlats(acre);
            foreach (var cliff in cliffs)
            {
                cliff.riverValueLine = -1;
                SetRiverCliffValues(cliff);
            }
            foreach (var flat in flats)
            {
                flat.riverValueLine = -1;
                if (flat.riverValueCliff == 0)
                {
                    SetRiverCliffValues(flat);
                }
            }
        }

        private void SetRiverCliffValues(Tile tile)
        {
            var todo = new List<Tile>();
            todo.Add(tile);

            for (int i = 0; i < todo.Count; i++)
            {
                tile = todo[i];
                var p = new Vector2Int[8];
                p[0] = new Vector2Int(-1, -1);
                p[1] = new Vector2Int(0, -1);
                p[2] = new Vector2Int(1, -1);
                p[3] = new Vector2Int(-1, 0);
                p[4] = new Vector2Int(1, 0);
                p[5] = new Vector2Int(-1, 1);
                p[6] = new Vector2Int(0, 1);
                p[7] = new Vector2Int(1, 1);
                foreach (var pn in p)
                {
                    if (IsNeighbour(tile, pn))
                    {
                        var t = GetNeighbour(tile, pn);
                        if (!t.isCliff && t.riverValueCliff > tile.riverValueCliff + 1)
                        {
                            t.riverValueCliff = tile.riverValueCliff + 1;
                            todo.Add(t);
                        }
                    }
                }
            }
        }
        
        private (List<Tile> cliffs, List<Tile> flats) ComputeRiverMetaFindCliffsAndFlats(Acre acre)
        {
            var cliffs = new List<Tile>();
            var flats = new List<Tile>();

            var p = acre.pos * acreSize;
            
            for (int y = 0; y < acreSize; y++)
            {
                for (int x = 0; x < acreSize; x++)
                {
                    var tile = tiles[p.x + x, p.y + y];
                    if (tile.isCliff)
                    {
                        cliffs.Add(tile);
                        tile.riverValueCliff = 0;
                    }
                    else
                    {
                        flats.Add(tile);
                        tile.riverValueCliff = acreSize;
                        if (x == 0 || x == acreSize - 1 ||
                            y == 0 || y == acreSize - 1)
                        {
                            tile.riverValueCliff = 0;
                        }
                    }
                }
            }

            return (cliffs, flats);
        }

        private void SpawnDecorations()
        {
            var points = PoissonDisk(new Vector4(0.0f, 0.0f, width, height), decorationSpacing);
            Debug.Log(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                var p2 = points[i];
                var tile = tiles[(int) p2.x, height - 1 - (int) p2.y];
                if (tile.isCliff || tile.isBeach || tile.isSlope || tile.isRiver)
                {
                    continue;
                }
                var p = new Vector3(points[i].x, tile.elevation * elevationHeight, points[i].y);
                var isTree = Random.Range(0.0f, 1.0f) <= treeProbability;
                if (isTree)
                {
                    Instantiate(treeCanopyPrefab, p, treeCanopyPrefab.transform.rotation, transform); // TODO
                    Instantiate(treeTrunkPrefab, p, treeTrunkPrefab.transform.rotation, transform); // TODO
                }
                else
                {
                    Instantiate(rockPrefab, p, rockPrefab.transform.rotation, transform); // TODO
                }
            }
        }

        /// <summary>
        /// Fast Poisson Disk - Robert Bridson
        /// extents holds min and max per dimension (xy - min, zw - max).
        /// r holds minimum distance between samples.
        /// k holds the limit of samples to choose before rejection.
        /// </summary>
        private List<Vector2> PoissonDisk(Vector4 extents, float r, int k = 30)
        {
            var cellSize = (r / (float) Math.Sqrt(2.0));
            var gridW = (int) Math.Ceiling((extents.z - extents.x) / cellSize);
            var gridH = (int) Math.Ceiling((extents.w - extents.y) / cellSize);
            var grid = new int[gridW * gridH];
            for (var i = 0; i < grid.Length; i++)
            {
                grid[i] = -1;
            }
            
            var points = new List<Vector2>();
            var active = new List<int>();

            var x = Random.Range(0.0f, extents.z - extents.x);
            var y = Random.Range(0.0f, extents.w - extents.y);
            var xi = (int) (x / cellSize);
            var yi = (int) (y / cellSize);
            var sample = new Vector2(x, y);

            points.Add(sample);
            active.Add(0);
            grid[xi + yi * gridW] = 0;

            while (active.Count > 0)
            {
                // Get random sample from active list
                var i = active[Random.Range(0, active.Count)];
                // Sample k annulus points
                var valid = false;
                for (int j = 0; j < k; j++)
                {
                    var p = RandomAnnulusPoint(points[i], r, r * 2.0f);
                    if (p.x < 0.0f || p.x > width || p.y < 0.0f || p.y > height)
                    {
                        continue;
                    }
                    
                    xi = (int) (p.x / cellSize);
                    yi = (int) (p.y / cellSize);
                    var gridIndex = xi + yi * gridW;
                    var neighbours = new int[9];
                    neighbours[0] = gridIndex + gridW - 1;
                    neighbours[1] = gridIndex + gridW;
                    neighbours[2] = gridIndex + gridW + 1;
                    neighbours[3] = gridIndex - 1;
                    neighbours[4] = gridIndex;
                    neighbours[5] = gridIndex + 1;
                    neighbours[6] = gridIndex - gridW - 1;
                    neighbours[7] = gridIndex - gridW;
                    neighbours[8] = gridIndex - gridW + 1;
                    valid = true;
                    foreach (var n in neighbours)
                    {
                        if (n > 0 && n < grid.Length &&
                            grid[n] > -1)
                        {
                            var np = points[grid[n]];
                            if (Vector2.Distance(p, np) < r)
                            {
                                valid = false;
                                break;
                            }
                        }
                    }

                    if (valid)
                    {
                        points.Add(p);
                        active.Add(points.Count - 1);
                        grid[gridIndex] = points.Count - 1;
                        break;
                    }
                }

                if (!valid)
                {
                    active.Remove(i);
                }
            }

            // Offset points to correct bounds
            for (int i = 0; i < points.Count; i++)
            {
                points[i] += new Vector2(extents.x, extents.y);
            }

            return points;
        }

        private Vector2 RandomAnnulusPoint(Vector2 p, float rMin, float rMax)
        {
            var theta = Random.Range(0.0f, 2.0f * (float) Math.PI);
            var A = 2.0f / (rMax * rMax - rMin * rMin);
            var r = (float) Math.Sqrt(2.0f * Random.Range(0.0f, 1.0f) / A + rMin * rMin);
            var x = r * (float) Math.Cos(theta);
            var y = r * (float) Math.Sin(theta);
            return p + new Vector2(x, y);
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

        private bool IsTile(Vector2Int pos)
        {
            return !(pos.x < 0 || pos.x >= numAcres.x * acreSize ||
                     pos.y < 0 || pos.y >= numAcres.y * acreSize);
        }
        
        private Tile GetTile(Vector2Int pos)
        {
            Assert.IsTrue(IsTile(pos));
            return tiles[pos.x, pos.y];
        }
        
        private bool IsNeighbour(Tile tile, Vector2Int offset)
        {
            var p = tile.pos + offset;
            return tile.pos / acreSize == p / acreSize && IsTile(p);
        }
        
        private Tile GetNeighbour(Tile tile, Vector2Int offset)
        {
            return GetTile(tile.pos + offset);
        }
    }
}
