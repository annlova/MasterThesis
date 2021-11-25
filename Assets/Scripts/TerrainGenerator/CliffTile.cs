using System;
using UnityEngine;

namespace TerrainGenerator
{
  [Serializable]
  public class CliffTile
  {
    public GameObject prefab;
    public GameObject prefabRoof;
    public GameObject prefabBeach;
    public CliffTileRule[] rules;
    public CliffOverlap[] overlaps;
    public bool isEndTile;
    public int slopeSouthWestConnectionIndex;
    public int slopeSouthEastConnectionIndex;
    public int slopeWestNorthConnectionIndex;
    public int slopeWestSouthConnectionIndex;
    public int slopeEastNorthConnectionIndex;
    public int slopeEastSouthConnectionIndex;
  }

  [Serializable]
  public class CliffOverlap
  {
    public bool fromWest;
    public bool fromNorth;
    public bool fromSouth;
  }
  
  [Serializable]
  public class CliffTileRule
  {
    public int index;
    public Vector2Int offset;
    public Vector2Int direction;
    public bool endsInMiddle;
  }
}
