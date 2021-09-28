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

    [SerializeField]
    private float _thickness;
    private float _prevThickness;
    
    [SerializeField]
    private float _falloff;
    private float _prevFalloff;

    [SerializeField]
    private float _hairAmount;
    private float _prevHairAmount;

    [SerializeField]
    private float _colorVariation;
    private float _prevColorVariation;
    
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
        if (_prevThickness != _thickness ||
            _prevFalloff != _falloff ||
            _prevHairAmount != _hairAmount ||
            _prevColorVariation != _colorVariation)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (_prevThickness != _thickness)
                {
                    r.sharedMaterial.SetFloat("_Thickness", _thickness);
                }
                if (_prevFalloff != _falloff) {
                    r.sharedMaterial.SetFloat("_Falloff", _falloff);
                }
                if (_prevHairAmount != _hairAmount) {
                    r.sharedMaterial.SetFloat("_HairAmount", _hairAmount);
                }
                if (_prevColorVariation != _colorVariation) {
                    r.sharedMaterial.SetFloat("_ColorVariation", _colorVariation);
                }
            }   
        }

        _prevThickness = _thickness;
        _prevFalloff = _falloff;
        _prevHairAmount = _hairAmount;
        _prevColorVariation = _colorVariation;
    }
}
