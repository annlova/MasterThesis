
using UnityEngine;

public enum TileType
{
  Flat,
  NW,
  N,
  NE,
  E,
  SE,
  S,
  SW,
  W
}

public class MapTile
{
  public TileType Type { get; set; }
  public GameObject Instance { get; set; }

  public MapTile(TileType type)
  {
    Type = type;
  }
}
