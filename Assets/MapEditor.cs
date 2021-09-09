using System;
using UnityEngine;
using UnityEngine.U2D;

public class MapEditor : MonoBehaviour
{

  public Map mMap;
  public GameObject mPrefab;   
  public ColorPrefab[] colorMappings;
    
    // Start is called before the first frame update
    void Start()
    {
      //mMap.LoadMapFromSave(mPrefab, transform);
      //GenerateLevel();
    }

    void GenerateLevel()
    {
      for (int x = 0; x < mMap.GetWidth(); x++)
      {
        for (int z = 0; z < mMap.GetDepth(); z++)
        {
          for (int y = 0; y < mMap.GetHeight(); y++)
          {
            if (mMap.IsTile(x, y, z))
            {
              Vector3 position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
              Instantiate(mPrefab, position, Quaternion.identity, transform);
            }
          }
        }
      }
    }

    // Update is called once per frame
    void Update()
    {
      return;
      if (Cursor.lockState == CursorLockMode.None)
      {
        if (Input.GetMouseButtonDown(0))
        {
          // Calculate which tile is being looked at
          var mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
          var pickedTile = mMap.PickTile(mouseRay, 100.0f);
          if (pickedTile.Item1)
          {
            var side = mMap.PickSide(pickedTile.Item2, mouseRay, 100.0f);
            Vector3 position = pickedTile.Item2 + side.Item2 + new Vector3(0.5f, 0.5f, 0.5f);
            Instantiate(mPrefab, position, Quaternion.identity, transform);
            mMap.SetTile((int) position.x, (int) position.y, (int) position.z, 0, 0, 1, mPrefab, transform);
          }
        } 
        else if (Input.GetMouseButtonDown(1))
        {
          Debug.Log("Hello");
          var mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
          var pickedTile = mMap.PickTile(mouseRay, 100.0f);
          if (pickedTile.Item1)
          {
            mMap.DestroyTile(pickedTile.Item2);
          }
        }
      }
    }
}
