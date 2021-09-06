using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Color = UnityEngine.Color;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

[Serializable]
public class Map
{
    [SerializeField]
    private Texture2D mMapSave;
    
    private int mMapWidth = 0;
    private int mMapHeight = 0;
    private int mMapDepth = 0;
    private MapTile[] map;
    
    public void LoadMapFromSave(GameObject prefab, Transform parent)
    {
        mMapWidth = mMapSave.width;
        mMapHeight = mMapWidth;
        mMapDepth = mMapSave.height;
        Assert.IsTrue(mMapWidth == mMapHeight && mMapHeight == mMapDepth);
        
        var mapSize = mMapWidth * mMapHeight * mMapDepth;
        map = new MapTile[mapSize];
        for (var x = 0; x < mMapWidth; x++)
        {
            for (var z = 0; z < mMapDepth; z++)
            {
                Color texelColor = mMapSave.GetPixel(x, z);
                int height = (int) (texelColor.r * 255);
                if (texelColor.a != 0)
                {
                    var tile = new MapTile(TileType.Flat);
                    map[x + z * mMapWidth + height * mMapWidth * mMapDepth] = tile;
                    Vector3 position = new Vector3(x + 0.5f, height + 0.5f, z + 0.5f);
                    tile.Instance = Object.Instantiate(prefab, position, Quaternion.identity, parent);
                }
            }
        }
    }

    public void SetTile(int x, int y, int z, float g, float b, float a, GameObject prefab, Transform parent)
    {
        if (map[x + z * mMapWidth + y * mMapWidth * mMapDepth] == null)
        {
            var tile = new MapTile(TileType.Flat);
            map[x + z * mMapWidth + y * mMapWidth * mMapDepth] = new MapTile(TileType.Flat);
            Vector3 position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
            tile.Instance = Object.Instantiate(prefab, position, Quaternion.identity, parent);
        }
    }
    
    public void SetTile(Vector3Int tile, float g, float b, float a, GameObject prefab, Transform parent)
    {
        SetTile(tile.x, tile.y, tile.z, g, b, a, prefab, parent);
    }

    public void DestroyTile(int x, int y, int z)
    {
        if (map[x + z * mMapWidth + y * mMapWidth * mMapDepth] != null)
        {
            Object.Destroy(map[x + z * mMapWidth + y * mMapWidth * mMapDepth].Instance);
            map[x + z * mMapWidth + y * mMapWidth * mMapDepth] = null;
        }
    }
    
    public void DestroyTile(Vector3Int tile)
    {
        DestroyTile(tile.x, tile.y, tile.z);
    }
    
    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < mMapWidth &&
               y >= 0 && y < mMapHeight &&
               z >= 0 && z < mMapDepth;
    }
    
    public bool IsTile(int x, int y, int z)
    {
        return InBounds(x, y, z) && map[x + z * mMapWidth + y * mMapWidth * mMapDepth] != null;
    }
    
    public bool IsTile(Vector3Int pos)
    {
        return IsTile(pos.x, pos.y, pos.z);
    }
    
    public int GetWidth()
    {
        return mMapWidth;
    }
    
    public int GetHeight()
    {
        return mMapHeight;
    }
    
    public int GetDepth()
    {
        return mMapDepth;
    }

    public (bool, Vector3Int) PickTile(Ray ray, float dist)
    {
        Vector3 p0 = ray.origin;
        Vector3 p1 = ray.origin + ray.direction * dist;
        
        AABB root = new AABB(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(mMapWidth, mMapHeight, mMapDepth));

        List<Vector3Int> intersects = new List<Vector3Int>();
        Test(p0, p1, root, intersects);

        if (intersects.Count == 0)
        {
            return (false, new Vector3Int(-1, -1, -1));
        }
        else
        {
            float shortestDist = dist;
            Vector3Int shortestPos = new Vector3Int((int) shortestDist, (int) shortestDist, (int) shortestDist);
            foreach (var pos in intersects)
            {
                float posDist = Vector3.Distance(ray.origin, pos);
                if (posDist <= shortestDist)
                {
                    shortestDist = posDist;
                    shortestPos = pos;
                }
            }

            return (true, shortestPos);
        }
    }

    private void Test(Vector3 p0, Vector3 p1, AABB aabb, List<Vector3Int> intersects)
    {
        if (aabb.TestSegment(p0, p1))
        {
            if (aabb.CubicSize() < 1.0f + AABB.Epsilon)
            {
                var posFloat = aabb.Min;
                Vector3Int pos = new Vector3Int((int) (posFloat.x + AABB.Epsilon),
                                                (int) (posFloat.y + AABB.Epsilon), 
                                                (int) (posFloat.z + AABB.Epsilon));
                if (IsTile(pos))
                {
                    intersects.Add(pos);
                }
            }
            else
            {
                var list = aabb.Subdivide();
                foreach (var sub in list)
                {
                    Test(p0, p1, sub, intersects);
                }
            }
        }
    }

    public (TileDirection, Vector3) PickSide(Vector3Int tile, Ray ray, float dist)
    {
        AABB aabb = new AABB(tile, tile + new Vector3(1.0f, 1.0f, 1.0f));
        var intersection = aabb.IntersectRay(ray, dist);
        if (intersection.Item1)
        {
            var p = intersection.Item2;
            var dir = p - (tile + new Vector3(0.5f, 0.5f, 0.5f));
            var absX = Math.Abs(dir.x);
            var absY = Math.Abs(dir.y);
            var absZ = Math.Abs(dir.z);
            if (absX > absY && absX > absZ)
            {
                if (dir.x >= 0.0f)
                {
                    return (TileDirection.East, new Vector3(1.0f, 0.0f, 0.0f));
                }
                else
                {
                    return (TileDirection.West, new Vector3(-1.0f, 0.0f, 0.0f));
                }
            } 
            else if (absY > absX && absY > absZ)
            {
                if (dir.y >= 0.0f)
                {
                    return (TileDirection.Up, new Vector3(0.0f, 1.0f, 0.0f));
                }
                else
                {
                    return (TileDirection.Down, new Vector3(0.0f, -1.0f, 0.0f));
                }
            }
            else
            {
                if (dir.z >= 0.0f)
                {
                    return (TileDirection.North, new Vector3(0.0f, 0.0f, 1.0f));
                }
                else
                {
                    return (TileDirection.South, new Vector3(0.0f, 0.0f, -1.0f));
                }
            }
        }
        Assert.IsTrue(false);
        return (TileDirection.Unknown, new Vector3(0.0f, 0.0f, 0.0f));
    }
}
