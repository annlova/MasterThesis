using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Schema;
using FMOD;
using Microsoft.Win32.SafeHandles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace TerrainGenerator
{
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] 
        private int seed;
        
        [SerializeField] 
        private bool spawnSlopes;
        
        [SerializeField] 
        private bool spawnRivers;
        
        [SerializeField] 
        private bool spawnDecorations;
        
        [SerializeField] 
        private bool spawnWaterfalls;
        
        [SerializeField]
        private float stepDeltaTime;
        
        [SerializeField]
        private int maxCliffWalkReverts;
        
        [SerializeField]
        private Vector2Int numAcres;

        [SerializeField] 
        public int acreSize;
        
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
        public int gridSizeFactor;
        
        [SerializeField] 
        private GameObject waterfallPrefab;

        [SerializeField]
        private GameObject treeCanopyPrefab;

        [SerializeField] 
        private float canopyRadius;

        [SerializeField]
        private GameObject treeTrunkPrefab;
        
        [SerializeField]
        private float treeTrunkRadius;
        
        [SerializeField]
        private GameObject rockPrefab;

        [SerializeField]
        private GameObject waterFallMistEmitter;

        [SerializeField]
        private GameObject sparkleEmitter;

        [SerializeField] 
        private float dirtPatchSeparation;

        [SerializeField] 
        private float dirtPatchRadiusMin;
        
        [SerializeField] 
        private float dirtPatchRadiusMax;

        [SerializeField] 
        private float connectedPatchDistanceRadiusMultiplier;
        
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
        private GameObject waterfallRenderObject;
        private GameObject waterfallColliderObject;
        private MeshFilter waterfallMeshFilter;
        private Renderer waterfallRenderer;

        public Dictionary<Collider, float[]> beaches;
        
        public Dictionary<Collider, float[]> GetBeaches()
        {
            return beaches;
        }
        
        // Start is called before the first frame update
        void Start()
        {
            var totalTime = Time.realtimeSinceStartupAsDouble;
            if (seed != 0)
            {
                Random.InitState(seed);
            }

            Init();
            while (currentWalkAgent == null || !currentWalkAgent.IsDone() || currentIsland < numIslands)
            {
                var ok = step();
                if (!ok)
                {
                    break;
                }
            }

            var  cliffTime = Time.realtimeSinceStartupAsDouble - totalTime;
            
            InitPossibleFloors();
            LevelTerrain();
            var  floorTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime;
            ComputeCliffFloors();
            var  cliffFloorTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime;
            ComputeRiverMeta();
            if (spawnRivers)
            {
                ComputeRivers();
            }
            var  riverTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime - cliffFloorTime;
            if (spawnSlopes)
            {
                ComputeSlopes();
            }
            var  slopeTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime - cliffFloorTime -
                            riverTime;
            if (spawnDecorations)
            {
                SpawnDecorations();
            }
            var  decoTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime - cliffFloorTime -
                           riverTime - slopeTime;
            if (spawnWaterfalls)
            {
                ComputeWaterfalls();
            }
            var  waterfallTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime - cliffFloorTime -
                                riverTime - slopeTime - decoTime;
            ComputeDirtPatches();
            var dirtTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime - cliffFloorTime -
                                riverTime - slopeTime - decoTime - waterfallTime;
            CreateTerrainMesh();
            var terrainTime = Time.realtimeSinceStartupAsDouble - totalTime - cliffTime - floorTime - cliffFloorTime -
                                riverTime - slopeTime - decoTime - waterfallTime - dirtTime;
            UpdateShaderVariables();
            totalTime = Time.realtimeSinceStartupAsDouble - totalTime;
            Debug.Log(totalTime + "\n" + 
                cliffTime + "\n" +
                floorTime + "\n" +
                cliffFloorTime + "\n" +
                riverTime + "\n" +
                slopeTime + "\n" +
                decoTime + "\n" +
                waterfallTime + "\n" +
                dirtTime + "\n" +
                terrainTime);
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
        private float riverSurfaceOffset = 0.1f;
        private float bottomRiverOffset = 0.7f;
        private void CreateTerrainMesh()
        {
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = tiles[x, z];
                    var riverOffset = tile.isRiver ? bottomRiverOffset : 0.0f;
                    if (tile.isWaterfall || tile.isRiverTransition)
                    {
                        // var p = new Vector3(x + 0.5f, tile.elevation, height - 1 - z + 0.5f);
                        // Instantiate(treeTrunkPrefab, p, Quaternion.identity, transform);
                    }
                    else if (tile.isCliff)
                    {
                        Vector3 p = Vector3.zero;
                        for (int i = tile.floor; i < tile.elevation; i++)
                        {
                            p = new Vector3(x + 0.5f, i * 2 - riverOffset, height - 1 - z + 0.5f);
                            var obj = Instantiate(tile.cliffTile.prefab, p, tile.cliffTile.prefab.transform.rotation, transform);
                            
                            // Set cliff texture number
                            var mesh = obj.GetComponent<MeshFilter>().mesh;
                            var uv2 = new Vector2[mesh.vertices.Length];
                            for (int j = 0; j < uv2.Length; j++)
                            {
                                uv2[j] = new Vector2(tile.cliffTextureNumber, 0.0f);
                            }
                            mesh.uv2 = uv2;
                        }

                        if (tile.isBeachCliff)
                        {
                            p = new Vector3(x + 0.5f, /*-0.3f*/-elevationHeight, height - 1 - z + 0.5f);
                            Instantiate(tile.cliffTile.prefab/*prefabBeach*/, p, tile.cliffTile.prefab/*prefabBeach*/.transform.rotation, transform);
                            var pRoof = new Vector3(x + 0.5f, tile.elevation * elevationHeight - riverOffset, height - 1 - z + 0.5f);
                            var o = Instantiate(tile.cliffTile.prefabRoof, pRoof, tile.cliffTile.prefabRoof.transform.rotation, transform);
                            ComputeTileVertexPatchData(tile, o);
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
                            // setRiverDistAttribute(tile, roofObj);
                            ComputeTileVertexPatchData(tile, roofObj);

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
                                        var pMerge = new Vector3(x + 0.5f, /*-0.3f*/-elevationHeight, height - 1 - z + 0.5f);
                                        Instantiate(item.Item2.prefab/*prefabBeach*/, pMerge, item.Item2.prefab/*prefabBeach*/.transform.rotation, transform);
                                    }
                                    else
                                    {
                                        var pSubRoof = new Vector3(x + 0.5f, item.Item1 * elevationHeight - riverOffset, height - 1 - z + 0.5f);
                                        var o = Instantiate(item.Item2.prefabRoof, pSubRoof, item.Item2.prefabRoof.transform.rotation, transform);
                                        ComputeTileVertexPatchData(tile, o);
                                    }
                                }
                            }

                            if (renderFloor)
                            {
                                var pFloor = new Vector3(x + 0.5f, tile.floor * elevationHeight - riverOffset,
                                    height - 1 - z + 0.5f);
                                var floorObj = Instantiate(flatTilePrefab, pFloor, Quaternion.identity, transform);
                                ComputeTileVertexPatchData(tile, floorObj);
                                // setRiverDistAttribute(tile, floorObj);


                                // TODO
                                if (tile.isRiverEdge)
                                {
                                    var riverEdgePrefab = cliffTiles[9].prefab;
                                    var rot = Quaternion.FromToRotation(Vector3.back,
                                        new Vector3(tile.riverEdgeDir.x, 0.0f, -tile.riverEdgeDir.y));
                                    var o = Instantiate(riverEdgePrefab, p, rot * riverEdgePrefab.transform.rotation,
                                        transform);
                                    ComputeTileVertexPatchData(tile, o);
                                }
                                else if (tile.isRiver)
                                {
                                    floorObj.gameObject.tag = "RiverBottom";
                                    roofObj.gameObject.tag = "RiverBottom";
                                    var o1 = Instantiate(riverTilePrefab,
                                        pFloor + new Vector3(0.0f, riverOffset - riverSurfaceOffset, 0.0f), Quaternion.identity,
                                        transform);
                                    var o2 = Instantiate(riverTilePrefab,
                                        pRoof + new Vector3(0.0f, riverOffset - riverSurfaceOffset, 0.0f), Quaternion.identity,
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
                                var o = Instantiate(slopeCliffRoofPrefab, p + Vector3.up * elevationHeight, Quaternion.Euler(0.0f, rotation, 0.0f), transform);
                                ComputeTileVertexPatchData(tile, o);
                            }
                            else
                            {
                                if (tile.isSlopeEdge1)
                                {
                                    Instantiate(slopeCliffEndSWPrefab, p, slopeCliffEndSWPrefab.transform.rotation, transform);
                                    var o = Instantiate(slopeCliffEndHighRoofSWPrefab, p + Vector3.up * elevationHeight,
                                        Quaternion.identity, transform);
                                    ComputeTileVertexPatchData(tile, o);
                                }
                                else
                                {
                                    Instantiate(slopeCliffEndSEPrefab, p, slopeCliffEndSEPrefab.transform.rotation, transform);
                                    var o = Instantiate(slopeCliffEndHighRoofSEPrefab, p + Vector3.up * elevationHeight,
                                        Quaternion.identity, transform);
                                    ComputeTileVertexPatchData(tile, o);
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
                                var o = Instantiate(ct.prefabRoof, p + Vector3.up * elevationHeight, rotation, transform);
                                ComputeTileVertexPatchData(tile, o);
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
                            ComputeTileVertexPatchData(tile, o);
                            // setRiverDistAttribute(tile, o);
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
                            ComputeTileVertexPatchData(tile, floorObj);
                            // setRiverDistAttribute(tile, floorObj);
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
                                var o = Instantiate(riverTilePrefab, p + new Vector3(0.0f, riverOffset - riverSurfaceOffset, 0.0f), Quaternion.identity, transform);
                                setRiverDirAttribute(tile, o);
                                Instantiate(sparkleEmitter,
                                    p + new Vector3(0.0f, riverOffset - riverSurfaceOffset, 0.0f),
                                    sparkleEmitter.transform.rotation, transform);
                            }
                        }
                    }

                    if (tile.possibleWaterfall)
                    {
                        tileValues[x + z * width].x = 1.0f;
                        tileValues[x + z * width].y = 1.0f;
                        tileValues[x + z * width].z = 1.0f;
                    }
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
                        // tileValues[x + z * width].x = 1.0f;
                        // tileValues[x + z * width].y = 1.0f;
                        // tileValues[x + z * width].z = 1.0f;
                    }
                    else if (tile.isMergeCliff)
                    {
                        tileValues[x + z * width].x = 1.0f;
                    }

                    // var islandFactor = (float) tile.acre.islandIndex / (numIslands - 1) * 0.8f;
                    // tileValues[x + z * width].x = 0.2f + islandFactor;
                    // tileValues[x + z * width].y = 0.2f + islandFactor;
                    // tileValues[x + z * width].z = 0.2f + islandFactor;

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
                    var riverOutlet = acres[x, y].hasRiverSouth && y == numAcres.y - 1;
                    var p = new Vector3(0.0f, elevation, 0.0f);
                    if (x == 0 && y < numAcres.y - 1 && (acres[x, y].elevation == 0 || acres[x, y + 1].elevation == 0))
                    {
                        p.z = height - size - y * size;
                        CreateBeach(p, riverOutlet);
                        var beachHeightMap = CreateBeach(p + Vector3.left * size, false);
                        CreateOcean(p + Vector3.left * size, oceanOffset, beachHeightMap);
                    }
                    else if (y == numAcres.y - 1 && acres[x, y].elevation == 0)
                    {
                        p.x = x * size;
                        p.z = 0.0f;
                        
                        var beachHeightmap = CreateBeach(p, riverOutlet);
                        if (riverOutlet)
                        {
                            CreateOcean(p, oceanOffset, beachHeightmap);
                        }
                        beachHeightmap = CreateBeach(p + Vector3.back * size, riverOutlet);
                        CreateOcean(p + Vector3.back * size, oceanOffset, beachHeightmap);
                    }
                    else if (x == numAcres.x - 1 && y < numAcres.y - 1 && (acres[x, y].elevation == 0 || acres[x, y + 1].elevation == 0))
                    {
                        p.x = width - size;
                        p.z = height - size - y * size;
                        
                        CreateBeach(p, riverOutlet);
                        var beachHeightMap = CreateBeach(p + Vector3.right * size, false);
                        CreateOcean(p + Vector3.right * size, oceanOffset, beachHeightMap);
                    }
                }
            }
            
            var bh = CreateBeach(new Vector3(-size, elevation, -size), false);
            CreateOcean(new Vector3(-size, elevation, -size), oceanOffset, bh);
            
            bh = CreateBeach(new Vector3(-size, elevation, 0), false);
            CreateOcean(new Vector3(-size, elevation, 0), oceanOffset, bh);
            
            bh = CreateBeach(new Vector3(numAcres.x * size, elevation, -size), false);
            CreateOcean(new Vector3(numAcres.x * size, elevation, -size), oceanOffset, bh);
            
            bh = CreateBeach(new Vector3(numAcres.x * size, elevation, 0), false);
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
                float height = beachHeights[(int) (vertices[i].x * gridSizeFactor + 0.3f) + (int) (vertices[i].z * gridSizeFactor + 0.3f) * (acreSize * gridSizeFactor + 1)];
                uv2[i] = new Vector2(height, 0.0f);
            }

            mesh.uv2 = uv2;
        }
        
        private float[] CreateBeach(Vector3 pos, bool riverOutlet)
        {
            var size = acreSize + 1;
            var trueSize = acreSize * gridSizeFactor + 1;
            var beach = Instantiate(beachGridPrefab, pos, beachGridPrefab.transform.rotation, transform);
            var mesh = beach.GetComponent<MeshFilter>().mesh;
            
            var vertices = mesh.vertices;
            var normals = mesh.normals;

            Assert.IsTrue(vertices.Length == trueSize * trueSize);
            var beachHeight = new float[trueSize * trueSize];
            
            for (int i = 0; i < vertices.Length; i++)
            {
                // Calculate vertex position
                var worldPos = vertices[i] + pos;
                var offset = ComputeBeachVertexOffset(worldPos);
                var outletHalfWidth = riverWidth / 1.5f;
                if (worldPos.z < 0.0f)
                {
                    outletHalfWidth += Math.Max((size / 6.0f) - vertices[i].z / 6.0f, 0.0f);
                }
                
                if (vertices[i].x >= size / 2.0f - outletHalfWidth &&
                    vertices[i].x <= size / 2.0f + outletHalfWidth &&
                    riverOutlet)
                {
                    offset += 0.5f * (outletHalfWidth - (float) Math.Abs(vertices[i].x - size / 2.0f));
                }
                vertices[i] = vertices[i] + Vector3.down * offset;

                var index = (int) (vertices[i].x * gridSizeFactor + 0.3f) + (int) (vertices[i].z * gridSizeFactor + 0.3f) * trueSize;
                beachHeight[index] = vertices[i].y;
                if (worldPos.z >= 0.0f && worldPos.x > 0.0f && worldPos.x < width)
                {
                    var t = tiles[(int) worldPos.x, height - 2 - (int) worldPos.z];
                    if (!t.isBeach)
                    {
                        // beachHeight[index] = 0.4f;
                    }
                }
                
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

            // Add beach heights to beaches
            var collider = beach.GetComponentInChildren<BoxCollider>();
            beaches[collider] = beachHeight;

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
            else
            {
                var px = (int) p.x;
                var pz = (int) p.z;
                if (px >= acreSize / 2 - (int) Math.Ceiling((riverWidth - 2) / 2.0f) &&
                    px <= width - acreSize / 2 + (int) Math.Ceiling((riverWidth - 2) / 2.0f) &&
                    pz >= acreSize / 2 - (int) Math.Ceiling((riverWidth - 2) / 2.0f) &&
                    !tiles[px, height - 1 - pz].isBeach)
                {
                    return 1.0f;
                }
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
                    if (prevElevation <= t.elevation)
                    {
                        Instantiate(cliffTileFlat.prefab,  p + Vector3.left * 2.0f + Vector3.up * 4.0f, cliffTileFlat.prefab.transform.rotation, transform);
                        Instantiate(cliffTileFlat.prefab,  p + Vector3.left * 2.0f + Vector3.up * 8.0f, cliffTileFlat.prefab.transform.rotation, transform);
                    }
                }
                Instantiate(cliffTileFlat.prefabRoof, p + Vector3.up * elevationHeight, cliffTileFlat.prefabRoof.transform.rotation, transform);
                Instantiate(flatTilePrefab, p + Vector3.up * elevationHeight + Vector3.left, flatTilePrefab.transform.rotation, transform);
                if (prevElevation <= t.elevation)
                {
                    Instantiate(cliffTileFlat.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.left * 2.0f, cliffTileFlat.prefabRoof.transform.rotation, transform);
                    for (var x = 3; x < acreSize; x++)
                    {
                        Instantiate(flatTilePrefab, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.left * x, flatTilePrefab.transform.rotation, transform);
                    }
                }

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
                    // Instantiate(cliffTileCorner.prefab, p + Vector3.back * 2.0f + Vector3.up * 2.0f, cliffTileCorner.prefab.transform.rotation, transform);
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

                    for (int h = 1; h <= 4; h++)
                    {
                        Instantiate(cliffTileCorner.prefab, p + Vector3.left * 2.0f + Vector3.up * h * 2.0f, cliffTileCorner.prefab.transform.rotation, transform);
                        for (int x = 3; x < (t.elevation < 1 ? acreSize : 2); x++)
                        {
                            Instantiate(cliffTileAway.prefab, p + Vector3.left * x + Vector3.up * h * 2.0f, cliffTileAway.prefab.transform.rotation, transform);
                        }
                    }
                    
                    Instantiate(cliffTileCorner.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back, cliffTileCorner.prefabRoof.transform.rotation, transform);
                    Instantiate(cliffTileCorner.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.left * 2.0f, cliffTileCorner.prefabRoof.transform.rotation, transform);
                    for (int x = 1; x < (t.elevation < 1 ? acreSize : 2); x++)
                    {
                        Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back + Vector3.left * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                        Instantiate(flatTilePrefab, p + Vector3.up * elevationHeight + Vector3.left * x, flatTilePrefab.transform.rotation, transform);
                        if (x > 1)
                        {
                            Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.left * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                        }
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
                    if (!(t.elevation == 0 && prevElevation > t.elevation))
                    {
                        Instantiate(cliffTileFlat.prefab,  p + Vector3.right * 2.0f + Vector3.up * 4.0f, cliffTileFlat.prefab.transform.rotation, transform);
                        Instantiate(cliffTileFlat.prefab,  p + Vector3.right * 2.0f + Vector3.up * 8.0f, cliffTileFlat.prefab.transform.rotation, transform);
                    }
                }
                Instantiate(cliffTileFlat.prefabRoof, p + Vector3.up * elevationHeight, cliffTileFlat.prefabRoof.transform.rotation, transform);
                Instantiate(flatTilePrefab, p + Vector3.up * elevationHeight + Vector3.right, flatTilePrefab.transform.rotation, transform);

                if (!(t.elevation == 0 && prevElevation > t.elevation))
                {
                    Instantiate(cliffTileFlat.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.right * 2.0f, cliffTileFlat.prefabRoof.transform.rotation, transform);
                    for (var x = 3; x < acreSize; x++)
                    {
                        Instantiate(flatTilePrefab, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.right * x, flatTilePrefab.transform.rotation, transform);
                    }
                }

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
                    
                    var offset = t.elevation > 0 ? 1.0f : 0.0f;
                    for (int h = t.elevation < 1 ? 1 : prevElevation + 2; h <= 4; h++)
                    {
                        Instantiate(cliffTileCorner.prefab, p + Vector3.right * 2.0f + Vector3.up * h * 2.0f + Vector3.back * offset, cliffTileCorner.prefab.transform.rotation, transform);
                        for (int x = 3; x < acreSize; x++)
                        {
                            Instantiate(cliffTileAway.prefab, p + Vector3.right * x + Vector3.up * h * 2.0f + Vector3.back * offset, cliffTileAway.prefab.transform.rotation, transform);
                        }
                    }
                    
                    Instantiate(cliffTileCorner.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back, cliffTileCorner.prefabRoof.transform.rotation, transform);
                    Instantiate(cliffTileCorner.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.right * 2.0f + Vector3.back * offset, cliffTileCorner.prefabRoof.transform.rotation, transform);
                    for (int x = 1; x < acreSize; x++)
                    {
                        if (t.elevation >= 1)
                        {
                            if (x < 2)
                            {
                                Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back + Vector3.right * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                            }
                            else if (x > 2)
                            {
                                Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.back + Vector3.right * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                            }
                        }
                        else if (t.elevation < 1)
                        {
                            Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * elevationHeight + Vector3.back * (1.0f + offset) + Vector3.right * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                            Instantiate(flatTilePrefab, p + Vector3.up * elevationHeight + Vector3.right * x, flatTilePrefab.transform.rotation, transform);
                            
                            if (x > 2)
                            {
                                Instantiate(cliffTileAway.prefabRoof, p + Vector3.up * (elevationHeight + 8.0f) + Vector3.right * x, cliffTileAway.prefabRoof.transform.rotation, transform);
                            }
                        }
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

                var waterfall = false;
                if (i >= 0 && i < width)
                {
                    waterfall = tiles[i, 0].acre.hasRiverNorth;
                }

                if (waterfall && i % acreSize == acreSize / 2 - (riverWidth - 2) / 2)
                {
                    var wp = new Vector3(i, t.elevation * 2.0f + 4.0f, height);
                    // CreateWaterfall(wp, wp + Vector3.right * 2.0f, wp + Vector3.down * 2.0f, wp + Vector3.down * 2.0f + Vector3.right * 2.0f,
                    //                 wp + Vector3.forward * 3.0f, wp + Vector3.forward * 3.0f + Vector3.right * 2.0f,
                    //                 wp + Vector3.down * 2.0f, wp + Vector3.down * 2.0f + Vector3.right * 2.0f, 0.0f);
                    Vector3 right = Vector3.right * (riverWidth - 2);
                    Vector3 down = Vector3.down * (4.0f + riverSurfaceOffset);
                    Vector3 forward = Vector3.forward * 3.0f;
                    CreateWaterfall(Vector3.zero, right, down, down + right, 
                                    forward, forward + right,
                                    down, down + right, wp, Vector2Int.up);
                }
                else if (waterfall &&
                        i % acreSize >= acreSize / 2 - (riverWidth - 2) / 2 &&
                        i % acreSize <= acreSize / 2 + (riverWidth - 2) / 2)
                {
                    
                }
                else
                {
                    Instantiate(cliffTileFlat.prefab, p, cliffTileFlat.prefab.transform.rotation, transform);
                    Instantiate(cliffTileFlat.prefab, p + Vector3.up * 2.0f, cliffTileFlat.prefab.transform.rotation, transform);
                    
                    for (int z = 0; z < 3; z++)
                    {
                        Instantiate(flatTilePrefab, p + Vector3.up * 4.0f + Vector3.forward * z, flatTilePrefab.transform.rotation, transform);
                    }
                }

                Instantiate(cliffTileFlat.prefab, p + Vector3.forward * 3.0f + Vector3.up * 4.0f, cliffTileFlat.prefab.transform.rotation, transform);
                Instantiate(cliffTileFlat.prefab, p + Vector3.forward * 3.0f + Vector3.up * 6.0f, cliffTileFlat.prefab.transform.rotation, transform);
                Instantiate(cliffTileFlat.prefab, p + Vector3.forward * 3.0f + Vector3.up * 8.0f, cliffTileFlat.prefab.transform.rotation, transform);
                Instantiate(cliffTileFlat.prefab, p + Vector3.forward * 3.0f + Vector3.up * 10.0f, cliffTileFlat.prefab.transform.rotation, transform);
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
            var combineWaterfall = new List<CombineInstance>();

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
                    case "Waterfall":
                        var waterfall = new CombineInstance();
                        waterfall.mesh = meshFilters[i].mesh;
                        waterfall.transform = meshFilters[i].transform.localToWorldMatrix;
                        var waterfallCollider = meshFilters[i].gameObject.transform.Find("Collider");
                        if (waterfallCollider)
                        {
                            waterfallCollider.transform.SetParent(rocksColliderObject.transform);
                        }
                        combineWaterfall.Add(waterfall);
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

            waterfallMeshFilter.sharedMesh = new Mesh();
            waterfallMeshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            waterfallMeshFilter.sharedMesh.CombineMeshes(combineWaterfall.ToArray());
            waterfallMeshFilter.gameObject.SetActive(true);
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

        private List<Tile> cliffStartTiles;

        private List<Vector2> patchList;
        private List<float> patchRandomList;
        
        private void Init()
        {
            width = numAcres.x * acreSize;
            height = numAcres.y * acreSize;

            flatsRenderObject = transform.Find("FlatsRender").gameObject;
            flatsColliderObject = transform.Find("FlatsCollider").gameObject;
            flatsMeshFilter = flatsRenderObject.GetComponent<MeshFilter>();
            flatsRenderer = flatsRenderObject.GetComponent<Renderer>();
            flatsRenderer.material.SetFloat("_PatchRadiusMin", dirtPatchRadiusMin);
            flatsRenderer.material.SetFloat("_PatchRadiusMax", dirtPatchRadiusMax);


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
            riversRenderer.shadowCastingMode = ShadowCastingMode.Off;
            riversRenderer.receiveShadows = true;
            
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
            
            waterfallRenderObject = transform.Find("WaterfallRender").gameObject;
            waterfallColliderObject = transform.Find("WaterfallCollider").gameObject;
            waterfallMeshFilter = waterfallRenderObject.GetComponent<MeshFilter>();
            waterfallRenderer = waterfallRenderObject.GetComponent<Renderer>();

            beaches = new Dictionary<Collider, float[]>();
            
            tileValues = new Vector4[width * height];
            for (int i = 0; i < tileValues.Length; i++)
            {
                tileValues[i] = new Vector4(0.1f, 0.1f, 0.1f, 1.0f);
            }
            tileValueBuffer = new ComputeBuffer(tileValues.Length, sizeof(float) * 4);
            
            stepContinuously = false;
            lastStepTime = Time.time;
            currentIsland = 0;

            cliffStartTiles = new List<Tile>();
            
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
                return;
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

            cliffStartTiles.Add(startTile);
            var agent = new WalkAgent(
                tiles, width, height,
                acres, startAcre, acreSize,
                cliffTiles, maxEat, minEat,
                maxCliffTextureNumber, riverWidth - 2, //TODO
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
                for (int elevation = acre.elevation; elevation >= 0; elevation--)
                {
                    if (acre.waterfallTiles[elevation].Count == 0)
                    {
                        continue;
                    }

                    var tiles = acre.waterfallTiles[elevation];
                    for (int i = 1; i < tiles.Count - 1; i++)
                    {
                        tiles[i].isCliff = false;
                    }
                }

                for (int elevation = acre.elevation; elevation >= 0; elevation--)
                {
                    // No river, no waterfall
                    if (acre.waterfallTiles[elevation].Count == 0)
                    {
                        continue;
                    }
                    
                    var tiles = acre.waterfallTiles[elevation];
                    
                    // List<Tile> tilesDown = null;
                    // Tile firstDown = null;
                    // Tile lastDown = null;
                    // if (elevation > 0)
                    // {
                    //     if (acre.waterfallTiles[elevation - 1].Count > 0)
                    //     {
                    //         tilesDown = acre.waterfallTiles[elevation - 1];
                    //         var firstDown = tilesDown[0];
                    //         var lastDown = tilesDown[tiles.Count - 1];
                    //     }
                    // }
                    
                    var first = tiles[0];
                    var last = tiles[tiles.Count - 1];
                    var firstLastDiff = last.pos - first.pos;

                    var transPosFirst = Vector3.zero;
                    var transPosLast = Vector3.zero;
                    var posFirstTop = Vector2Int.zero;
                    var posLastTop = Vector2Int.zero;
                    var posFirstBot = Vector2Int.zero;
                    var posLastBot = Vector2Int.zero;
                    int orientation = -1; // 0 = WEST, 1 = SOUTH, 2 = EAST
                    if (acre.waterfallDir.x > 0)
                    {
                        // EAST
                        orientation = 2;
                        if (first.pos.x < last.pos.x)
                        {
                            posFirstTop = new Vector2Int(first.pos.x - 1, first.pos.y + 1);
                            posLastTop = new Vector2Int(first.pos.x - 1, last.pos.y);
                            posFirstBot = new Vector2Int(last.pos.x + 1, first.pos.y + 1);
                            posLastBot = new Vector2Int(last.pos.x + 1, last.pos.y);
                                                        
                            var nextFirst = this.tiles[last.pos.x + 1, first.pos.y];
                            var nextLast = this.tiles[last.pos.x + 1, last.pos.y]; 
                            if (nextFirst.isWaterfall && nextFirst.isCliff ||
                                nextLast.isWaterfall && nextLast.isCliff)
                            {
                                posFirstBot = new Vector2Int(last.pos.x, first.pos.y + 1);
                                posLastBot = new Vector2Int(last.pos.x, last.pos.y);
                            }
                        }
                        else
                        {
                            posFirstTop = new Vector2Int(last.pos.x - 1, first.pos.y + 1);
                            posLastTop = new Vector2Int(last.pos.x - 1, last.pos.y);
                            posFirstBot = new Vector2Int(first.pos.x + 1, first.pos.y + 1);
                            posLastBot = new Vector2Int(first.pos.x + 1, last.pos.y);
                                                        
                            var nextFirst = this.tiles[first.pos.x + 1, first.pos.y];
                            var nextLast = this.tiles[first.pos.x + 1, last.pos.y]; 
                            if (nextFirst.isWaterfall && nextFirst.isCliff ||
                            nextLast.isWaterfall && nextLast.isCliff)
                            {
                                posFirstBot = new Vector2Int(first.pos.x, first.pos.y + 1);
                                posLastBot = new Vector2Int(first.pos.x, last.pos.y);
                            }
                        }
                    }
                    else if (acre.waterfallDir.x < 0)
                    {
                        // WEST
                        orientation = 0;
                        if (first.pos.x > last.pos.x)
                        {
                            posFirstTop = new Vector2Int(first.pos.x + 2, first.pos.y);
                            posLastTop = new Vector2Int(first.pos.x + 2, last.pos.y + 1);
                            posFirstBot = new Vector2Int(last.pos.x, first.pos.y);
                            posLastBot = new Vector2Int(last.pos.x, last.pos.y + 1);
                                                        
                            var nextFirst = this.tiles[last.pos.x - 1, first.pos.y];
                            var nextLast = this.tiles[last.pos.x - 1, last.pos.y]; 
                            if (nextFirst.isWaterfall && nextLast.isCliff ||
                            nextLast.isWaterfall && nextLast.isCliff)
                            {
                                posFirstBot = new Vector2Int(last.pos.x + 1, first.pos.y);
                                posLastBot = new Vector2Int(last.pos.x + 1, last.pos.y + 1);
                            }
                        }
                        else
                        {
                            posFirstTop = new Vector2Int(last.pos.x + 2, first.pos.y);
                            posLastTop = new Vector2Int(last.pos.x + 2, last.pos.y + 1);
                            posFirstBot = new Vector2Int(first.pos.x, first.pos.y);
                            posLastBot = new Vector2Int(first.pos.x, last.pos.y + 1);
                            
                            var nextFirst = this.tiles[first.pos.x - 1, first.pos.y];
                            var nextLast = this.tiles[first.pos.x - 1, last.pos.y]; 
                            if (nextFirst.isWaterfall && nextFirst.isCliff ||
                            nextLast.isWaterfall && nextLast.isCliff)
                            {
                                posFirstBot = new Vector2Int(first.pos.x + 1, first.pos.y);
                                posLastBot = new Vector2Int(first.pos.x + 1, last.pos.y + 1);
                            }
                        }
                    }
                    else
                    {
                        // SOUTH
                        orientation = 1;
                        if (first.pos.y < last.pos.y)
                        {
                            posFirstTop = new Vector2Int(first.pos.x, first.pos.y - 1);
                            posLastTop = new Vector2Int(last.pos.x + 1, first.pos.y - 1);
                            posFirstBot = new Vector2Int(first.pos.x, last.pos.y + 2);
                            posLastBot = new Vector2Int(last.pos.x + 1, last.pos.y + 2);

                            if (first.pos.y < height - 1 &&
                                last.pos.y < height - 1)
                            {
                                var nextFirst = this.tiles[first.pos.x, first.pos.y + 1];
                                var nextLast = this.tiles[last.pos.x, last.pos.y + 1];
                                if ((nextFirst.isWaterfall && nextFirst.isCliff) ||
                                    (nextLast.isWaterfall && nextLast.isCliff) ||
                                    (first.isMergeCliff || last.isMergeCliff))
                                {
                                    posFirstBot = new Vector2Int(first.pos.x, last.pos.y);
                                    posLastBot = new Vector2Int(last.pos.x + 1, last.pos.y);
                                }
                            }
                        }
                        else
                        {
                            posFirstTop = new Vector2Int(first.pos.x, last.pos.y - 1);
                            posLastTop = new Vector2Int(last.pos.x + 1, last.pos.y - 1);
                            posFirstBot = new Vector2Int(first.pos.x, first.pos.y + 2);
                            posLastBot = new Vector2Int(last.pos.x + 1, first.pos.y + 2);

                            if (first.pos.y < height - 1 &&
                                last.pos.y < height - 1)
                            {
                                var nextFirst = this.tiles[first.pos.x, first.pos.y + 1];
                                var nextLast = this.tiles[last.pos.x, last.pos.y + 1];
                                if ((nextFirst.isWaterfall && nextFirst.isCliff) ||
                                    (nextLast.isWaterfall && nextLast.isCliff) ||
                                    (first.isMergeCliff || last.isMergeCliff))
                                {
                                    posFirstBot = new Vector2Int(first.pos.x, first.pos.y);
                                    posLastBot = new Vector2Int(last.pos.x + 1, first.pos.y);
                                }
                            }
                        }
                    }

                    // Find vertices a, b, c, d of waterfall mesh
                    var a = Vector3.zero;
                    var b = Vector3.zero;
                    var c = Vector3.zero;
                    var d = Vector3.zero;
                    var e = Vector3.zero;
                    var f = Vector3.zero;
                    var g = Vector3.zero;
                    var h = Vector3.zero;
                    var p = new Vector3(first.pos.x + 0.5f, (elevation - 1) * elevationHeight - riverSurfaceOffset,
                        height - 1 - first.pos.y + 0.5f);

                    var firstMesh = first.cliffTile.prefab.GetComponent<MeshFilter>().sharedMesh;
                    var firstVerts = firstMesh.vertices;
                    var firstUvs = firstMesh.uv;
                    for (int i = 0; i < firstMesh.vertexCount; i++)
                    {
                        var uv = firstUvs[i];
                        var offsetVector = new Vector3(acre.waterfallDir.x, 0.0f, -acre.waterfallDir.y);
                        var abc = -acre.waterfallDir * firstLastDiff +
                                  acre.waterfallDir * new Vector2((float) Math.Ceiling(a.x) - a.x,
                                      (float) Math.Ceiling(a.y) - a.y) +
                                  acre.waterfallDir;
                        if (uv.x < 0.5f && uv.y > 0.5f)
                        {
                            a = first.cliffTile.prefab.transform.rotation * firstVerts[i];
                            e = new Vector3(posFirstTop.x, p.y + a.y, height - posFirstTop.y) - p;
                        }
                        else if (uv.x < 0.5f && uv.y < 0.5f)
                        {
                            float downOffset = first.elevation - first.floor - 1;
                            if (first.elevation == 0)
                            {
                                downOffset = 1.0f;
                            }

                            c = first.cliffTile.prefab.transform.rotation * firstVerts[i] +
                                Vector3.down * downOffset * elevationHeight;
                            g = new Vector3(posFirstBot.x, p.y + c.y, height - posFirstBot.y) - p;
                        }
                    }

                    var lastMesh = last.cliffTile.prefab.GetComponent<MeshFilter>().sharedMesh;
                    var lastVerts = lastMesh.vertices;
                    var lastUvs = lastMesh.uv;
                    for (int i = 0; i < lastMesh.vertexCount; i++)
                    {
                        var uv = lastUvs[i];
                        var abc = -acre.waterfallDir * firstLastDiff +
                                  acre.waterfallDir * new Vector2((float) Math.Ceiling(a.x) - a.x,
                                      (float) Math.Ceiling(a.y) - a.y) +
                                  acre.waterfallDir;
                        if (uv.x > 0.5f && uv.y > 0.5f)
                        {
                            b = last.cliffTile.prefab.transform.rotation * lastVerts[i];
                            b += new Vector3(firstLastDiff.x, 0.0f, -firstLastDiff.y);
                            f = new Vector3(posLastTop.x, p.y + b.y, height - posLastTop.y) - p;
                        }
                        else if (uv.x > 0.5f && uv.y < 0.5f)
                        {
                            float downOffset = first.elevation - first.floor - 1;
                            if (first.elevation == 0)
                            {
                                downOffset = 1.0f;
                            }

                            d = last.cliffTile.prefab.transform.rotation * lastVerts[i] +
                                Vector3.down * downOffset * elevationHeight;
                            d += new Vector3(firstLastDiff.x, 0.0f, -firstLastDiff.y);
                            h = new Vector3(posLastBot.x, p.y + d.y, height - posLastBot.y) - p;
                        }
                    }

                    CreateWaterfall(a, b, c, d, e, f, g, h, p, acre.waterfallDir);
                    var mistPos = Vector3.Lerp(c, d, 0.5f) + p;
                    if (mistPos.y < -0.3f)
                    {
                        mistPos.y = -1.5f;
                    }

                    var mistRot = orientation == 0 ? -90.0f :
                                      orientation == 2 ? 90.0f :
                                      0.0f;
                    
                    // Instantiate(waterFallMistEmitter, mistPos, Quaternion.Euler(0.0f, 0.0f, mistRot), transform);
                    // Debug.Log("Waterfall! Width = " + waterfallWidth + ". start = " + start + ". end = " + end);
                }
            }
        }

        private void CreateWaterfall(Vector3 a, Vector3 b, Vector3 c, Vector3 d, 
                                    Vector3 e, Vector3 f, Vector3 g, Vector3 h, Vector3 p,
                                    Vector2Int dir)
        {
            var n3d = b - a;
            var n2d = new Vector2(n3d.z, -n3d.x);
            n3d = new Vector3(n2d.x, 0.0f, n2d.y).normalized;
            var angle = (float) (Math.Acos(Vector3.Dot(Vector3.back, n3d)) / (2.0 * Math.PI)) * 360.0f;
            if (n3d.x > 0.0f)
            {
                angle = -angle;
            }
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);
            
            var waterfall = Instantiate(waterfallPrefab, p, Quaternion.identity, transform);
            var waterfallBottom = Instantiate(waterfallPrefab, p - new Vector3(0.0f, bottomRiverOffset - riverSurfaceOffset, 0.0f), Quaternion.identity, transform);
            waterfallBottom.tag = "RiverBottom";
            var mesh = waterfall.GetComponent<MeshFilter>().mesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv2 = new Vector2[mesh.uv.Length];
            var bottomMesh = waterfallBottom.GetComponent<MeshFilter>().mesh;
            var verticesBottom = bottomMesh.vertices;
            var normalsBottom = bottomMesh.normals;
            // Assert.IsTrue(vertices.Length == 8);
            for (var i = 0; i < vertices.Length; i++)
            {
                uv2[i] = dir;
                var v = vertices[i];
                if (Math.Abs(v.z) < 0.5f)
                {
                    if (v.x < 0.0f)
                    {
                        if (v.y > 0.0f)
                        {
                            vertices[i] = a - new Vector3(0.0f, 0.5f - v.y, 0.0f) + rotation * new Vector3(0.0f, 0.0f, v.z);
                            normals[i] = rotation * normals[i];
                            verticesBottom[i] = vertices[i] + rotation * new Vector3(0.0f, 0.0f, bottomRiverOffset);
                            normalsBottom[i] = normals[i];
                        }
                        else
                        {
                            vertices[i] = c + new Vector3(0.0f, 0.5f + v.y, 0.0f) + rotation * new Vector3(0.0f, 0.0f, v.z);;
                            normals[i] = rotation * normals[i];
                            verticesBottom[i] = vertices[i] + rotation * new Vector3(0.0f, 0.0f, bottomRiverOffset);
                            normalsBottom[i] = normals[i];
                        }
                    }
                    else
                    {
                        if (v.y > 0.0f)
                        {
                            vertices[i] = b - new Vector3(0.0f, 0.5f - v.y, 0.0f) + rotation * new Vector3(0.0f, 0.0f, v.z);;
                            normals[i] = rotation * normals[i];
                            verticesBottom[i] = vertices[i] + rotation * new Vector3(0.0f, 0.0f, bottomRiverOffset);
                            normalsBottom[i] = normals[i];
                        }
                        else
                        {
                            vertices[i] = d + new Vector3(0.0f, 0.5f + v.y, 0.0f) + rotation * new Vector3(0.0f, 0.0f, v.z);;
                            normals[i] = rotation * normals[i];
                            verticesBottom[i] = vertices[i] + rotation * new Vector3(0.0f, 0.0f, bottomRiverOffset);
                            normalsBottom[i] = normals[i];
                        }
                    }
                }
                else
                {
                    if (v.x < 0.0f)
                    {
                        if (v.y > 0.0f)
                        {
                            vertices[i] = e;
                            normals[i] = Vector3.up;
                            verticesBottom[i] = vertices[i];
                            normalsBottom[i] = normals[i];
                        }
                        else
                        {
                            vertices[i] = g;
                            normals[i] = Vector3.up;
                            verticesBottom[i] = vertices[i];
                            normalsBottom[i] = normals[i];
                        }
                    }
                    else
                    {
                        if (v.y > 0.0f)
                        {
                            vertices[i] = f;
                            normals[i] = Vector3.up;
                            verticesBottom[i] = vertices[i];
                            normalsBottom[i] = normals[i];
                        }
                        else
                        {
                            vertices[i] = h;
                            normals[i] = Vector3.up;
                            verticesBottom[i] = vertices[i];
                            normalsBottom[i] = normals[i];
                        }
                    }
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv2 = uv2;
            bottomMesh.vertices = verticesBottom;
            bottomMesh.normals = normalsBottom;
            
            var mistPos = Vector3.Lerp(c, d, 0.5f) + p;
            if (mistPos.y < -0.3f)
            {
                mistPos.y = -1.5f;
            }
            Instantiate(waterFallMistEmitter, mistPos, waterFallMistEmitter.transform.rotation * Quaternion.Euler(0.0f, 0.0f, angle), transform);
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
                                if (t.isRiver || t.isWaterfall)
                                {
                                    continue;
                                }
                                
                                var waterfallRow = false;
                                for (var i = 1; i < riverWidth - 1; i++)
                                {
                                    var pp = new Vector2Int(pos.x + x, pos.y + acreSize / 2 - riverWidth / 2 + i);
                                    var ttt = tiles[pp.x, pp.y];
                                    var tt = ttt;
                                    if (x > 0)
                                    {
                                        tt = tiles[pp.x - 1, pp.y];
                                    }

                                    var wft = acre.waterfallTiles[tt.elevation];
                                    if (i > 1 && i < riverWidth - 2 ||
                                        wft.Count > 0 &&
                                        !((tt == wft[0] ||
                                           tt == wft[wft.Count - 1]) ||
                                          (ttt == wft[0] ||
                                           ttt == wft[wft.Count - 1])))
                                    {
                                        continue;
                                    }

                                    if (tt.isWaterfall || ttt.isWaterfall)
                                    {
                                        waterfallRow = true;
                                        break;
                                    }
                                }
                                
                                if (y == 0 && !waterfallRow)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, 1);
                                }
                                else if (y == riverWidth - 1 && !waterfallRow)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, -1);
                                }
                                else if (y > 0 && y < riverWidth - 1)
                                {
                                    if (waterfallRow)
                                    {
                                        t.isRiverTransition = true;
                                        continue;
                                    }
                                    
                                    // t.isRiverTransition = waterfallTransition;
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
                                if (t.isRiver || t.isWaterfall)
                                {
                                    continue;
                                }
                                
                                var waterfallRow = false;
                                for (var i = 1; i < riverWidth - 1; i++)
                                {
                                    var pp = new Vector2Int(pos.x + x, pos.y + acreSize / 2 - riverWidth / 2 + i);
                                    var ttt = tiles[pp.x, pp.y];
                                    var tt = ttt;
                                    if (x < acreSize - 1)
                                    {
                                        tt = tiles[pp.x + 1, pp.y];
                                    }

                                    var wft = acre.waterfallTiles[tt.elevation];
                                    if (i > 1 && i < riverWidth - 2 ||
                                        wft.Count > 0 &&
                                        !((tt == wft[0] ||
                                           tt == wft[wft.Count - 1]) ||
                                          (ttt == wft[0] ||
                                           ttt == wft[wft.Count - 1])))
                                    {
                                        continue;
                                    }

                                    if (tt.isWaterfall || ttt.isWaterfall)
                                    {
                                        waterfallRow = true;
                                        break;
                                    }
                                }
                                
                                if (y == 0 && !waterfallRow)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, 1);
                                }
                                else if (y == riverWidth - 1 && !waterfallRow)
                                {
                                    t.isRiverEdge = true;
                                    t.riverEdgeDir = new Vector2Int(0, -1);
                                }
                                else if (y > 0 && y < riverWidth - 1)
                                {
                                    if (waterfallRow)
                                    {
                                        t.isRiverTransition = true;
                                        continue;
                                    }
                                    
                                    // t.isRiverTransition = waterfallTransition;
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

                                var waterfallRow = false;
                                for (var i = 1; i < riverWidth - 1; i++)
                                {
                                    var pp = new Vector2Int(pos.x + acreSize / 2 - riverWidth / 2 + i, pos.y + y);
                                    var ttt = tiles[pp.x, pp.y];
                                    var tt = ttt;
                                    if (y < acreSize - 1)
                                    {
                                        tt = tiles[pp.x, pp.y + 1];
                                    }

                                    var wft = acre.waterfallTiles[tt.elevation];
                                    if (i > 1 && i < riverWidth - 2 ||
                                        wft.Count > 0 &&
                                        !((tt == wft[0] ||
                                           tt == wft[wft.Count - 1]) ||
                                          (ttt == wft[0] ||
                                           ttt == wft[wft.Count - 1])))
                                    {
                                        continue;
                                    }

                                    if (tt.isWaterfall || ttt.isWaterfall)
                                    {
                                        waterfallRow = true;
                                        break;
                                    }
                                }
                                
                                if (x == 0 && !waterfallRow)
                                {
                                    tiles[p.x, p.y].isRiverEdge = true;
                                    tiles[p.x, p.y].riverEdgeDir = new Vector2Int(1, 0);
                                }
                                else if (x == riverWidth - 1 && !waterfallRow)
                                {
                                    tiles[p.x, p.y].isRiverEdge = true;
                                    tiles[p.x, p.y].riverEdgeDir = new Vector2Int(-1, 0);
                                }
                                else if (x > 0 && x < riverWidth - 1)
                                {
                                    if (waterfallRow)
                                    {
                                        t.isRiverTransition = true;
                                        continue;
                                    }
                                    
                                    tiles[p.x, p.y].isRiver = true;
                                    tiles[p.x, p.y].isRiverEdge = false;
                                    tiles[p.x, p.y].riverDir = Vector2.down;
                                }
                            }
                        }
                    }

                    centerRiverDir = centerRiverDir.normalized;
                    // Fix middle tiles
                    for (int y = 0; y < riverWidth - 2; y++)
                    {
                        for (int x = 0; x < riverWidth - 2; x++)
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
            
            for (int i = 0; i < points.Count; i++)
            {
                var p2 = points[i];
                var tile = tiles[(int) p2.x, height - 1 - (int) p2.y];
                if (tile.isCliff ||
                    tile.isBeach ||
                    tile.isSlope ||
                    tile.isRiver ||
                    tile.isWaterfall ||
                    tile.isRiverEdge ||
                    tile.isRiverTransition ||
                    p2.x < canopyRadius || p2.x > width - canopyRadius ||
                    p2.y > height - canopyRadius)
                {
                    continue;
                }
                var p = new Vector3(points[i].x, tile.elevation * elevationHeight, points[i].y);
                var isTree = Random.Range(0.0f, 1.0f) <= treeProbability;
                if (isTree)
                {
                    var rotation = Random.Range(0.0f, 359.0f);
                    var canopy = Instantiate(treeCanopyPrefab, p, treeCanopyPrefab.transform.rotation * Quaternion.Euler(0.0f, rotation, 0.0f), transform); // TODO
                    var canopyMesh = canopy.GetComponent<MeshFilter>().mesh;
                    var canopyUv2 = new Vector2[canopyMesh.uv.Length];
                    var canopyRng = Random.Range(0.0f, 1.0f);
                    var canopyRng2 = Random.Range(0.0f, 1.0f);
                    for (var k = 0; k < canopyUv2.Length; k++)
                    {
                        canopyUv2[k] = new Vector2(canopyRng, canopyRng2);
                    }

                    canopyMesh.uv2 = canopyUv2;
                    
                    var trunk = Instantiate(treeTrunkPrefab, p, treeTrunkPrefab.transform.rotation * Quaternion.Euler(0.0f, rotation, 0.0f), transform); // TODO
                    var trunkMesh = trunk.GetComponent<MeshFilter>().mesh;
                    var trunkUv2 = new Vector2[trunkMesh.uv.Length];
                    var trunkRng = Random.Range(0.0f, 1.0f);
                    for (var k = 0; k < trunkUv2.Length; k++)
                    {
                        trunkUv2[k] = new Vector2(trunkRng, 0.0f);
                    }

                    trunkMesh.uv2 = trunkUv2;
                }
                else
                {
                    var rotation = Random.Range(0.0f, 359.0f);
                    Instantiate(rockPrefab, p, rockPrefab.transform.rotation * Quaternion.Euler(0.0f, rotation, 0.0f), transform); // TODO
                }
            }
        }

        private void ComputeTileVertexPatchData(Tile tile, GameObject prefab)
        {
            if (patchList.Count < 2)
            {
                return;
            }
            
            var mesh = prefab.GetComponent<MeshFilter>().mesh;
            var vertices = mesh.vertices;
            var uv2 = new Vector2[vertices.Length];
            var uv3 = new Vector2[vertices.Length];
            var uv4 = new Vector2[vertices.Length];

            var pos = new Vector2(tile.pos.x + 0.5f, height - tile.pos.y + 0.5f);
            float distance = width * width * height * height;
            float distance2 = width * width * height * height;
            var closest = new int[2];
            
            for (var k = 0; k < patchList.Count; k++)
            {
                var dist = Vector2.Distance(pos, patchList[k]);
                if (dist < distance)
                {
                    distance2 = distance;
                    closest[1] = closest[0];
                    
                    distance = dist;
                    closest[0] = k;
                }
                else if (dist < distance2)
                {
                    distance2 = dist;
                    closest[1] = k; 
                }
            }

            var rng = Random.Range(0.0f, 1.0f);
            for (var i = 0; i < uv2.Length; i++)
            {
                uv2[i] = patchList[closest[0]];
                uv3[i] = patchList[closest[1]];
                uv4[i] = new Vector2(patchRandomList[closest[0]], patchRandomList[closest[1]]);
            }

            mesh.uv2 = uv2;
            mesh.uv3 = uv3;
            mesh.uv4 = uv4;

            // var vertexPosData = new Vector2[vertices.Length];
            // for (var i = 0; i < vertexPosData.Length; i++)
            // {
            //     vertexPosData[i] = new Vector2(tile.pos.x + 0.5f, height - tile.pos.y + 0.5f);// + new Vector2(v.x + 0.5f, v.z + 0.5f);
            //     vertexPosData[i] += new Vector2(vertices[i].x, vertices[i].z);
            // }
            //     
            // // Calculate smallest distance and angle for each vertex
            // var vertexPatchData1 = new List<Vector2>(vertexPosData.Length);
            // var vertexPatchData2 = new List<Vector2>(vertexPosData.Length);
            // // var vertexPatchData3 = new List<Vector2>(vertexPosData.Length);
            // var distances = new float[vertexPosData.Length];
            // for (var i = 0; i < vertexPosData.Length; i++)
            // {
            //     vertexPatchData1.Add(new Vector2(50.0f, 50.0f));
            //     vertexPatchData2.Add(new Vector2(50.0f, 50.0f));
            //     // vertexPatchData3.Add(new Vector2(50.0f, 50.0f));
            //     distances[i] = width * width * height * height;
            // }
            //
            // for (var i = 0; i < vertexPatchData1.Count; i++)
            // {
            //     var v = vertexPosData[i];
            //     for (var k = 0; k < patchList.Count; k++)
            //     {
            //         var dist = Vector2.Distance(v, patchList[k]);
            //         if (dist < distances[i])
            //         {
            //             distances[i] = dist;
            //             // vertexPatchData3[i] = vertexPatchData2[i];
            //             vertexPatchData2[i] = vertexPatchData1[i];
            //             vertexPatchData1[i] = new Vector2(patchList[k].x, patchList[k].y);
            //         }
            //     }
            //     
            //     uv2[i] = vertexPatchData1[i];
            //     uv3[i] = vertexPatchData2[i];
            //     // uv4[i] = vertexPatchData3[i];
            // }

            // mesh.uv2 = uv2;
            // mesh.uv3 = uv3;
            // mesh.uv4 = uv4;
        }
        private void ComputeDirtPatches()
        {
            patchList = PoissonDisk(new Vector4(0.0f, 0.0f, width, height), dirtPatchSeparation);
            // Remove patches that collide with cliffs
            var removeList = new List<Vector2>();
            // for (var i = 0; i < patchList.Count; i++)
            // {
            //     var keep = KeepPatch(patchList[i]);
            //     if (!keep)
            //     {
            //         removeList.Add(patchList[i]);
            //     }
            // }

            patchRandomList = new List<float>(patchList.Count);
            var patchExtraRandomList = new List<float>();
            
            var patchListCount = patchList.Count;
            for (var i = 0; i < patchListCount; i++)
            {
                var rRng = Random.Range(0.0f, 1.0f);
                var r = dirtPatchRadiusMin * (1.0f - rRng) + dirtPatchRadiusMax * rRng;// < rRng2 ? rRng2 : rRng; 

                var keep = KeepPatch(patchList[i], r);
                if (!keep)
                {
                    removeList.Add(patchList[i]);
                    continue;
                }
                
                patchRandomList.Add(rRng);
                var rng = Random.Range(0.0f, 1.0f);
                var p = patchList[i];
                if (rng > 0.8f)
                {
                    var rngX = Random.Range(0.0f, 1.0f);
                    var rngY = Random.Range(0.0f, 1.0f);
                    p += new Vector2(rngX, rngY).normalized * r * connectedPatchDistanceRadiusMultiplier;
                    var pKeep = KeepPatch(p, r);
                    if (pKeep)
                    {
                        patchList.Add(p);
                        patchExtraRandomList.Add(rRng);
                    }
                }
                if (rng > 0.5f)
                {
                    var rngX = Random.Range(0.0f, 1.0f);
                    var rngY = Random.Range(0.0f, 1.0f);
                    p += new Vector2(rngX, rngY).normalized * r * connectedPatchDistanceRadiusMultiplier;
                    var pKeep = KeepPatch(p, r);
                    if (pKeep)
                    {
                        patchList.Add(p);
                        patchExtraRandomList.Add(rRng);
                    }
                }
            }

            foreach (var patch in removeList)
            {
                patchList.Remove(patch);
            }
            
            for (var i = 0; i < patchExtraRandomList.Count; i++)
            {
                patchRandomList.Add(patchExtraRandomList[i]);
            }
            
            // Debug.Log(patchList.Count);
        }

        private bool KeepPatch(Vector2 pos, float r)
        {
            var x = (int) pos.x;
            var y = (int) (height - 1 - pos.y);
            if (x < 0 || x >= width ||
                y < 0 || y >= height)
            {
                return false;
            }

            var tile = tiles[x, y];
            var p = new Vector2Int(tile.acre.pos.x * acreSize, height - 1 - tile.acre.pos.y * acreSize);

            var i_min = p + new Vector2Int(acreSize / 2 - (riverWidth - 2) / 2, -acreSize);
            var i_max = p + new Vector2Int(acreSize / 2 + (riverWidth - 2) / 2, 0);
            var min = new Vector2(i_min.x, i_min.y);
            var max = new Vector2(i_max.x, i_max.y);
            var col = TestSphereAABB(pos, r, min, max);
            
            i_min = p + new Vector2Int(0, -(acreSize / 2 + (riverWidth - 2) / 2));
            i_max = p + new Vector2Int(acreSize, -(acreSize / 2 - (riverWidth - 2) / 2));
            min = new Vector2(i_min.x, i_min.y);
            max = new Vector2(i_max.x, i_max.y);
            col |= TestSphereAABB(pos, r, min, max);

            if (col && tile.acre.hasRiver)
            {
                return false;
            }
            
            // Debug.Log(i_min + " " + i_max + " " + pos + " ");

            return tile.riverValueCliff > r;
        }
        
        bool TestSphereAABB(Vector2 c, float r, Vector2 min, Vector2 max)
        {
            var sqDist = SqDistPointAABB(c, min, max);
            return sqDist <= r * r;
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
