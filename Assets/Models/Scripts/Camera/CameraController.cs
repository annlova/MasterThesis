using UnityEngine;

namespace Camera
{
    public class CameraController : MonoBehaviour {

        public GameObject player;                           //Public variable to store a reference to the player game object

        [SerializeField] private Vector3 offset;            //Private variable to store the offset distance between the player and camera

        [SerializeField] private float angle;               //Camera angle
    
        void Start ()
        {
            camera = GetComponentInParent<UnityEngine.Camera>();
            riverRenderer = GameObject.Find("TerrainGenerator").transform.Find("RiversRender").GetComponent<Renderer>();
            waterfallRenderer = GameObject.Find("TerrainGenerator").transform.Find("WaterfallRender").GetComponent<Renderer>();
            oceanRenderer = GameObject.Find("TerrainGenerator").transform.Find("OceanRender").GetComponent<Renderer>();
            
            offset = new Vector3(0, 7.9f, -5.76f);     // Distance from player character
            angle = 51;                                     // Camera angle
        
            transform.Rotate(Vector3.right, angle);
            //Calculate and store the offset value by getting the distance between the player's position and camera's position.
            //offset = transform.position - player.transform.position;
        }

        private UnityEngine.Camera camera;
        private Renderer riverRenderer;
        private Renderer waterfallRenderer;
        private Renderer oceanRenderer;
        private void OnPreRender()
        {
            var projInv = (camera.projectionMatrix).inverse;
            var viewInv = (camera.worldToCameraMatrix).inverse;
            riverRenderer.sharedMaterial.SetMatrix("_ProjInverse", projInv);
            riverRenderer.sharedMaterial.SetMatrix("_ViewInverse", viewInv);
            waterfallRenderer.sharedMaterial.SetMatrix("_ProjInverse", projInv);
            waterfallRenderer.sharedMaterial.SetMatrix("_ViewInverse", viewInv);
            oceanRenderer.sharedMaterial.SetMatrix("_ProjInverse", projInv);
            oceanRenderer.sharedMaterial.SetMatrix("_ViewInverse", viewInv);
        }
        
        /// <summary>
        /// Switches to FreeFlyCamera when 'V' is pressed
        /// </summary>
        void SwitchToFreeFlyCamera()
        {
            if (Input.GetKey(KeyCode.V))
            {
                GetComponent<FreeFlyCamera>().enabled = true;
                GetComponent<CameraController>().enabled = false;
            }
        }
    
        // LateUpdate is called after Update, once per frame
        void LateUpdate () 
        {
            // Set the position of the camera's transform to be the same as the player's, but offset by the calculated offset distance.
            transform.SetPositionAndRotation(player.transform.position + offset, Quaternion.AngleAxis(angle, Vector3.right));
            SwitchToFreeFlyCamera();
        }
    }
}
