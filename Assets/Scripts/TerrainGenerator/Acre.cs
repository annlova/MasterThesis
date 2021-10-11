
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
    }
  }
}
