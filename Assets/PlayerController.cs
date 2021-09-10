using System;
using System.Numerics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private float _maxSpeedWalk;

    [SerializeField]
    private float _maxSpeedRun;
    
    [SerializeField]
    private float _acceleration;

    [SerializeField]
    private float _rotationSpeed;

    // The current speed of the character
    private float _speed;
    
    // The current direction of the character
    private Vector3 _direction;

    // The character controller
    private CharacterController _controller;

    private Animator _animator;

    private FreeFlyCamera _Camera;
    
    // Start is called before the first frame update
    void Start()
    {
        _Camera = GameObject.Find("Main Camera").GetComponent<FreeFlyCamera>();
        
        _controller = GetComponent<CharacterController>();
        _direction = new Vector3(0.0f, 0.0f, 1.0f);

        _animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!FreeFlying())
        {
            Move();   
        }

        _animator.SetFloat("Speed", _speed);
    }

    private bool FreeFlying()
    {
        if (_Camera.enabled)
        {
            return true;   
        }
        return false;
    }

    private void Move()
    {
        // Get direction and tilt from gamepad
        var (dir, tilt) = GetMoveInput();
        
        // Calculate player speed based on tilt
        CalculateSpeed(tilt);

        // Get angle rotated from current player position
        var angleRotated = CalculateRotation(dir);
        
        // Calculate this frame's translation vector, i.e. how far to move character
        var translationVector = _direction * (_speed * Time.deltaTime);
        
        
        // Move character
        _controller.Move(translationVector);
        
        // Rotate character
        transform.Rotate(Vector3.up, angleRotated);
    }

    /// <summary>
    /// Gets input from vertical and horizontal axis.
    /// </summary>
    /// <returns>Input axis' direction and tilt value.</returns>
    private (Vector3, float) GetMoveInput()
    {
        var vert = Input.GetAxis("Vertical");
        var hori = Input.GetAxis("Horizontal");
        var input = new Vector2(hori, vert);
        
        var tiltFactor = Vector2.Distance(new Vector2(0.0f, 0.0f), input);
        
        var inputDir = new Vector3(input.x, 0.0f, input.y);
        inputDir = inputDir.normalized;

        return (inputDir, tiltFactor);
    }

    /// <summary>
    /// Calculates and updates the current speed.
    /// </summary>
    /// <param name="tilt">The input tilt factor</param>
    private void CalculateSpeed(float tilt)
    {
        
        switch (tilt > 0.0f)
        {

            // Check if running button is down as well as joystick is tilted
            case true when Input.GetButton("Fire3"):
                
            {
                _speed += _acceleration * tilt * Time.deltaTime;
                if (_speed > _maxSpeedRun * tilt)
                {
                    _speed = _maxSpeedRun * tilt;
                }

                break;
            }
            
            // Check if joystick is tilted
            case true:
            {
                _speed += _acceleration * tilt * Time.deltaTime;
                if (_speed > _maxSpeedWalk * tilt)
                {
                    _speed = _maxSpeedWalk * tilt;
                }

                break;
            }
            default:
                _speed = 0.0f;
                break;
        }
    }

    /// <summary>
    /// Calculate and update our direction
    /// </summary>
    /// <param name="inputDir">The input direction</param>
    /// <returns>The angle rotated</returns>
    private float CalculateRotation(Vector3 inputDir)
    {
        // Get the angle between the character direction and input direction
        var angle = Vector3.SignedAngle(_direction, inputDir, Vector3.up);
        var sign = 1;
        if (angle < 0)
        {
            sign = -1;
        }
        
        // Calculate amount to rotate this frame according to rotation speed set
        var angleToRotate = _rotationSpeed * Time.deltaTime;
        if (angleToRotate > Math.Abs(angle))
        {
            angleToRotate = angle;
        }

        // Make sure we rotate the correct direction
        angleToRotate *= sign;

        // update direction
        _direction = Quaternion.Euler(0.0f, angleToRotate, 0.0f) * _direction;

        return angleToRotate;
    }
}
