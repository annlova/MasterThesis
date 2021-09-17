using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private GameObject _Object;
    
    [SerializeField]
    private int _Layers;

    void Start()
    {
      for (int i = 0; i < _Layers; i++)
      {
        var o = Instantiate(_Object, transform.position, Quaternion.identity, transform);
        var mat = o.GetComponent<Renderer>().material;
        
        mat.SetFloat("_FurLength", 0.01f * i);
        mat.SetFloat("_Layer", 0.01f * i);
        mat.renderQueue = 3000 + i;
      }
    }
}
