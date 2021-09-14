using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HairyTest : MonoBehaviour
{

    [SerializeField]
    private GameObject _mesh;

    [SerializeField]
    private float _shellDistance;

    [SerializeField]
    private int _numberOfLayers;

    [SerializeField]
    private float _gravityWeight;

    [SerializeField]
    private Vector3 _gravityDir;

    // Start is called before the first frame update
    /*
    void Start()
    {
        for (int i = 0; i < _numberOfLayers; i++)
        {
            var obj = Instantiate(_mesh, transform.position, Quaternion.identity, transform);

            var propBlock = new MaterialPropertyBlock();
            var renderer = obj.GetComponent<Renderer>();
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat("_FurLength", _shellDistance * i);
            propBlock.SetFloat("_Layer", _gravityWeight);
            propBlock.SetVector("_VGravity", _gravityDir);

            renderer.SetPropertyBlock(propBlock);
        }
    }
    */
    
    void Start()
    {
        for (int i = 0; i < _numberOfLayers; i++)
        {
            var o = Instantiate(_mesh, transform.position, Quaternion.identity, transform);
            var mat = o.GetComponent<Renderer>().material;

            mat.SetFloat("_FurLength", _shellDistance * i);
            mat.SetFloat("_Layer", _gravityWeight * i);
            //mat.SetVector("_VGravity", _gravityDir);

            mat.renderQueue = 3000 + i;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
