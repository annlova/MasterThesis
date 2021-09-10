using System;
using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour {

    public GameObject player;                           //Public variable to store a reference to the player game object

    [SerializeField] private Vector3 offset;            //Private variable to store the offset distance between the player and camera

    [SerializeField] private float angle;               //Camera angle
    
    void Start ()
    {
        offset = new Vector3(0, 5.9f, -3.76f);     // Distance from player character
        angle = 51;                                     // Camera angle
        
        transform.Rotate(Vector3.right, angle);
        //Calculate and store the offset value by getting the distance between the player's position and camera's position.
        //offset = transform.position - player.transform.position;
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
