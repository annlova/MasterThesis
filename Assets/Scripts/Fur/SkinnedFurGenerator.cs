using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Serialization;

public class SkinnedFurGenerator : MonoBehaviour
{
    private GameObject mesh;

    [SerializeField]
    [Range(0.0f, 0.04f)]
    private float shellDistance;
    private float prevShellDistance;
    
    [SerializeField]
    [Range(1, 128)]
    private int numberOfLayers;
    private int prevNumberOfLayers;
    
    [SerializeField]
    [Range(1, 2000)]
    private float furMultiplier;
    private float prevFurMultiplier;
    
    [SerializeField]
    [Range(-1, 1)]
    private float furDensity;
    private float prevFurDensity;
    
    [SerializeField]
    private Vector2 windDirection;
    private Vector2 prevWindDirection;
    
    [SerializeField]
    private float windForce;
    private float prevWindForce;
    
    [SerializeField]
    private float windFrequency;
    private float prevWindFrequency;
    
    [SerializeField]
    private float windSize;
    private float prevWindSize;
    
    // private variables

    private Light sun;
    private Color prevSunColor;
    
    private Renderer[] renderers;
    private bool shouldUpdateRenderers;
    
    // Start is called before the first frame update
    void Start()
    {
        mesh = transform.Find("Shell").gameObject;
        
        sun = GameObject.Find("Sun").GetComponent<Light>();
        prevSunColor = sun.color;
        
        CreateShells();
        
        mesh.SetActive(false);
        
        renderers = GetComponentsInChildren<Renderer>();
        shouldUpdateRenderers = false;
        
        UpdatePrevVariables();
    }
    

    void CreateShells()
    {
        for (int i = 0; i < numberOfLayers; i++)
        {
            var o = Instantiate(mesh, transform.position, Quaternion.identity, transform);
            o.transform.rotation = transform.rotation;
            var mat = o.GetComponent<Renderer>().material;

            mat.SetFloat("_ShellDistance", shellDistance);
            mat.SetFloat("_Layer", i);
            mat.SetFloat("_MaxLayer", numberOfLayers - 1);
            
            mat.SetFloat("_FurMultiplier", furMultiplier);
            mat.SetFloat("_FurDensity", furDensity);
            
            mat.SetVector("_WindDirection", windDirection);
            mat.SetFloat("_WindForce", windForce);
            mat.SetFloat("_WindFrequency", windFrequency);
            mat.SetFloat("_WindSize", windSize);

            mat.SetVector("_LightColor", sun.color);
            
            mat.renderQueue = 3016 + i;
        }
    }

    void DeleteShells()
    {
        foreach (Transform child in transform)
        {
            GameObject.Destroy(child.gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {

        UpdateRenderers();
        UpdateShells();
        UpdateMaterials();
        UpdatePrevVariables();
    }

    void UpdateRenderers()
    {
        if (shouldUpdateRenderers)
        {
            renderers = GetComponentsInChildren<Renderer>();    
        }
    }

    void UpdateShells()
    {
        if (prevShellDistance != shellDistance ||
            prevNumberOfLayers != numberOfLayers)
        {
            DeleteShells();
            CreateShells();
            shouldUpdateRenderers = true;
        }
    }

    void UpdateMaterials()
    {
        foreach (var renderer in renderers)
        {
            if (prevFurMultiplier != furMultiplier)
            {
                renderer.sharedMaterial.SetFloat("_FurMultiplier", furMultiplier);
            }

            if (prevFurDensity != furDensity)
            {
                renderer.sharedMaterial.SetFloat("_FurDensity", furDensity);
            }

            if (prevSunColor != sun.color)
            {
                renderer.sharedMaterial.SetVector("_LightColor", sun.color);
            }
            
            if (prevWindDirection != windDirection)
            {
                renderer.sharedMaterial.SetVector("_WindDirection", windDirection);
            }
             
            if (prevWindForce != windForce)
            {
                renderer.sharedMaterial.SetFloat("_WindForce", windForce);
            }
             
            if (prevWindFrequency != windFrequency)
            {
                renderer.sharedMaterial.SetFloat("_WindFrequency", windFrequency);
            }
             
            if (prevWindSize != windSize)
            {
                renderer.sharedMaterial.SetFloat("_WindSize", windSize);
            }
        }
    }

    void UpdatePrevVariables()
    {
        prevShellDistance = shellDistance;
        prevNumberOfLayers = numberOfLayers;
        prevFurMultiplier = furMultiplier;
        prevFurDensity = furDensity;
        prevSunColor = sun.color;
        prevWindDirection = windDirection; 
        prevWindForce = windForce; 
        prevWindFrequency = windFrequency; 
        prevWindSize = windSize;
    }
}
