using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
  public class Tile
  {
    public Vector2Int pos;
    public Acre acre;
    public int elevation;

    public bool isLeveled;
    
    public bool isCliff;
    public bool isBeachCliff;
    public CliffTile cliffTile;
    public List<Tile> connectedCliffs;
    public Vector2Int cliffDirection;
    public int cliffTextureNumber;

    public List<bool> possibleFloors;
    public int floor;
    public bool isPossibleFloorChecked;

    public bool isMergeCliff;
    public List<Tuple<int, CliffTile>> mergeCliffs;

    public bool isRiver;
    public bool isRiverEdge;
    public Vector2Int riverEdgeDir;
    public Vector2 riverDir;

    public float riverLineFactor;
    public float[] vertexRiverLineFactor;
    public float riverValue;
    public int riverValueCliff;
    public int riverValueLine;
    public float[] vertexRiverValue;
    public bool riverValueLineVisited;

    public bool isSlopeLowEnd;
    public bool isSlope;
    public bool isSlopeCliff;
    public bool isSlopeEdge1;
    public bool isSlopeEdge2;
    public bool isSlopeLower;
    public bool isSlopeHigher;
    public int slopeFactor;

    public bool isWaterfall;
    
    public bool isBeach;
    
    public bool modified;
    
    public Tile(Vector2Int pos, Acre acre, int elevation)
    {
      this.acre = acre;
      this.pos = pos;
      this.elevation = elevation;

      isCliff = false;
      isBeachCliff = false;
      cliffTile = null;
      connectedCliffs = new List<Tile>();
      
      floor = elevation;
      possibleFloors = new List<bool>();
      isPossibleFloorChecked = false;
      
      isMergeCliff = false;
      mergeCliffs = new List<Tuple<int, CliffTile>>();

      riverValueLine = -1;
      vertexRiverValue = new float[4];
      vertexRiverLineFactor = new float[4];

      isBeach = false;
      
      isLeveled = false;

      modified = false;
    }
  }
}
