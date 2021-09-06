using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private float _maxSpeed;
    
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
    
    // Start is called before the first frame update
    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _direction = new Vector3(0.0f, 0.0f, 1.0f);
    }

    // Update is called once per frame
    void Update()
    {
        Move();
    }

    private void Move()
    {
        var vert = Input.GetAxis("Vertical");
        var hori = Input.GetAxis("Horizontal");
        var input = new Vector2(vert, hori);

        var tiltFactor = Vector2.Distance(new Vector2(0.0f, 0.0f), input);
        if (tiltFactor > 0.0f)
        {
            _speed += _acceleration * tiltFactor * Time.deltaTime;
            if (_speed > _maxSpeed)
            {
                _speed = _maxSpeed;
            }
        }
        else
        {
            _speed = 0.0f;
        }


        var inputDir = new Vector3(input.x, 0.0f, input.y);
        inputDir = inputDir.normalized;
        
        var angle = Vector3.SignedAngle(_direction, inputDir, new Vector3(0.0f, 1.0f, 0.0f));
        var sign = 1;
        if (angle < 0)
        {
            sign = -1;
        }
        var angleToRotate = _rotationSpeed * Time.deltaTime;
        if (angleToRotate > Math.Abs(angle))
        {
            angleToRotate = angle;
        }

        angleToRotate *= sign;
        
        _direction = Quaternion.Euler(0.0f, angleToRotate, 0.0f) * _direction;

        var moveVec = _direction * (_speed * Time.deltaTime);

        var temp = new Vector3(moveVec.x, 0.0f, moveVec.y);
        _controller.Move(temp);
    }
}
