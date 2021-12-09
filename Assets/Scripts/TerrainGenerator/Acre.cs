
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
  public class Acre
  {
    public int islandIndex;

    public Vector2Int pos;
    public int elevation;
    
    public bool hasWestCliff;
    public bool hasEastCliff;
    public bool hasSouthEastCliff;
    public bool hasSouthCliff;
    public bool hasSouthWestCliff;
    
    public bool cliffWalked;

    public bool hasRiver;
    public bool hasRiverNorth;
    public bool hasRiverWest;
    public bool riverWestFlowsWest;
    public bool hasRiverEast;
    public bool riverEastFlowsEast;
    public bool hasRiverSouth;

    public List<Tile> waterfallTiles;
    public float waterfallTransPosFirst;
    public float waterfallTransPosLast;
    public Vector2Int waterfallDir;

    public Acre(Vector2Int pos, int elevation)
    {
      this.islandIndex = -1;
      
      this.pos = pos;
      this.elevation = elevation;
      
      hasWestCliff = false;
      hasEastCliff = false;
      hasSouthEastCliff = false;
      hasSouthCliff = false;
      hasSouthWestCliff = false;

      cliffWalked = false;

      waterfallTiles = new List<Tile>();
    }
  }
}
