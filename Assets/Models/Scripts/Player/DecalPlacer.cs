using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecalPlacer : MonoBehaviour
{
    [SerializeField] 
    private GameObject decalPrefab;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hitInfo;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hitInfo))
            {
                SpawnDecal(hitInfo);
            }
        }
    }

    void SpawnDecal(RaycastHit hitInfo)
    {
        var decal = Instantiate(decalPrefab);
        decal.transform.position = hitInfo.point;
        decal.transform.forward = hitInfo.normal * -1f;
    }
}

