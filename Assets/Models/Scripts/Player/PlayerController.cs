using System;
using System.Collections.Generic;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _maxSpeedWalk;

        [SerializeField] private float _maxSpeedRun;

        [SerializeField] private float _acceleration;

        [SerializeField] private float _rotationSpeed;

        [SerializeField] private float terminalVelocity;

        [SerializeField] private GameObject footstepDecal;

        [SerializeField] private int maxFootsteps;
        
        // The current speed of the character
        private float _speed;

        // The current direction of the character
        private Vector3 _direction;
        private Vector3 _gravitySpeed;

        // The character controller
        private CharacterController _controller;

        private Animator _animator;

        private FreeFlyCamera _Camera;

        // Collision varibles
        private Dictionary<Collider, float[]> _beachList;
        private bool _inBeachTrigger;
        private List<Tuple<Collider, float[]>> _beachHeights;
        private int acreSize;
        private int gridSizeFactor;
        
        // Footstep animation
        private const float distancePerFootstep = 0.5f;
        private float distanceSinceLastFootstep = 0.0f;
        private bool leftStepNext;

        private List<GameObject> decalPool;
        private int nextDecalIndex;
        
        // Start is called before the first frame update
        void Start()
        {
            _Camera = GameObject.Find("Main Camera").GetComponent<FreeFlyCamera>();

            _controller = GetComponent<CharacterController>();
            _direction = new Vector3(0.0f, 0.0f, 1.0f);
            _gravitySpeed = new Vector3(0.0f, 0.0f, 0.0f);

            _animator = GetComponent<Animator>();

            _inBeachTrigger = false;
            _beachHeights = new List<Tuple<Collider, float[]>>();
            var tgen = GameObject.Find("TerrainGenerator").GetComponent<TerrainGenerator.TerrainGenerator>();
            _beachList = tgen.GetBeaches();
            acreSize = tgen.acreSize;
            gridSizeFactor = tgen.gridSizeFactor;

            var decalHolder = GameObject.Find("DecalHolder");
            var decalMaterial = decalHolder.GetComponent<Renderer>().sharedMaterial;
            decalPool = new List<GameObject>(maxFootsteps);
            for (int i = 0; i < maxFootsteps; i++)
            {
                var decal = Instantiate(footstepDecal, Vector3.zero + Vector3.down, Quaternion.identity, decalHolder.transform);
                decal.GetComponent<Renderer>().material = decalMaterial;
                decalPool.Add(decal);
            }

            nextDecalIndex = 0;
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

        void SpawnFootstepDecal(bool left, Vector3 normal)
        {
            Vector3 offset;
            if (left)
            {
                offset = -transform.right;
            }
            else
            {
                offset = transform.right;
            }
            offset *= 0.24f;
            var p = transform.position + offset + normal * 0.05f;// + Vector3.up;
            var decal = decalPool[nextDecalIndex];
            nextDecalIndex = (nextDecalIndex + 1) % maxFootsteps;
            decal.transform.position = p;
            decal.transform.localScale = Vector3.one * 0.15f;
            decal.transform.up = normal;
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
            distanceSinceLastFootstep += translationVector.magnitude;

            // Apply gravity
            _gravitySpeed += Physics.gravity * Time.deltaTime;
            if (_gravitySpeed.magnitude > terminalVelocity)
            {
                _gravitySpeed = _gravitySpeed.normalized * terminalVelocity;
            }

            // handle beach collision
            (var collidesBeach, var beachGravitySpeed, var beachNormal) = HandleBeachCollision(transform.position + _gravitySpeed * Time.deltaTime);
            CollisionFlags flag;
            if (collidesBeach)
            { 
                flag = _controller.Move(beachGravitySpeed);
                
                if (distanceSinceLastFootstep >= distancePerFootstep)
                {
                    distanceSinceLastFootstep = 0.0f;
                    SpawnFootstepDecal(leftStepNext, beachNormal);
                    leftStepNext = !leftStepNext;
                }
            }
            else
            {
                flag = _controller.Move(_gravitySpeed * Time.deltaTime);
            }

            // Handle other collision
            if (flag != CollisionFlags.None || collidesBeach/* || _controller.transform.position.y <= -0.5f*/)
            {
                if (_controller.transform.position.y < -0.5f)
                {
                    // _controller.enabled = false;
                    // var pos = transform.position;
                    // transform.position = new Vector3(pos.x, -0.5f, pos.z);
                    // _controller.enabled = true;
                }

                _gravitySpeed.Set(0.0f, 0.0f, 0.0f);
            }

            // Rotate character
            transform.Rotate(Vector3.up, angleRotated);
            
            // if (distanceSinceLastFootstep >= distancePerFootstep)
            // {
            //     distanceSinceLastFootstep = 0.0f;
            //     leftStepNext = !leftStepNext;
            // }
        }

        private (bool, Vector3, Vector3) HandleBeachCollision(Vector3 pos)
        {
            if (_inBeachTrigger)
            {
                foreach (var beach in _beachHeights)
                {
                    if (beach.Item1.bounds.Contains(transform.position))
                    {
                        return CollidesWithBeach(pos, beach.Item2, beach.Item1.transform.position.y);
                    }
                }
            }

            return (false, Vector3.zero, Vector3.zero);
        }

        private (bool, Vector3, Vector3) CollidesWithBeach(Vector3 pos, float[] heights, float offset)
        {
            var posInBounds = new Vector2(Mod(pos.x, acreSize), Mod(pos.z, acreSize));
            var y = pos.y;
            var w = acreSize * gridSizeFactor + 1;
            var lowX = (int) (posInBounds.x * gridSizeFactor);
            var lowY = (int) (posInBounds.y * gridSizeFactor);
            var highX = (int) (posInBounds.x * gridSizeFactor + 1);
            var highY = (int) (posInBounds.y * gridSizeFactor + 1);

            // Determine triangle we are in lower or upper triangle
            var lower = (1.0f - (posInBounds.y * gridSizeFactor - lowY)) > (posInBounds.x * gridSizeFactor - lowX);
            
            // Find quad corners where collision check should happen
            // a - low left, b - low right, c - high left, d - high right

            var a = heights[lowX + lowY * w] + offset;
            var av = new Vector3(lowX, a, lowY);
            var b = heights[highX + lowY * w] + offset;
            var bv = new Vector3(highX, b, lowY);
            var c = heights[lowX + highY * w] + offset;
            var cv = new Vector3(lowX, c, highY);
            var d = heights[highX + highY * w] + offset;
            var dv = new Vector3(highX, d, highY);

            var h = 0.0f;
            if (lower)
            {
                h = ComputeYPointOnTriangle(
                    new Vector3(lowX, a, lowY),
                    bv,
                    cv,
                    posInBounds * gridSizeFactor);
            }
            else
            {
                h = ComputeYPointOnTriangle(
                    cv,
                    bv,
                    new Vector3(highX, d, highY),
                    posInBounds * gridSizeFactor);
            }

            if (y < h)
            {
                // Compute normal
                Vector3 normal;
                if (lower)
                {
                    var v1 = bv - av;
                    var v2 = cv - av;
                    normal = Vector3.Cross(v2, v1).normalized;
                }
                else
                {
                    var v1 = cv - dv;
                    var v2 = bv - dv;
                    normal = Vector3.Cross(v2, v1).normalized;
                }

                return (true, (transform.position.y - h) * Vector3.down, normal);
            }

            return (false, Vector3.zero, Vector3.zero);
        }

        private float ComputeYPointOnTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 p)
        {
            return a.y +
                   ((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) /
                   ((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) *
                   (p.y - a.z) -
                   ((b.z - a.z) * (c.y - a.y) - (c.z - a.z) * (b.y - a.y)) /
                   ((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) *
                   (p.x - a.x);
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
            else
            {
                // Make sure we rotate the correct direction
                angleToRotate *= sign;
            }

            // update direction
            _direction = Quaternion.Euler(0.0f, angleToRotate, 0.0f) * _direction;

            return angleToRotate;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_beachList.ContainsKey(other))
            {
                // Debug.Log("Adding!");
                _inBeachTrigger = true;
                _beachHeights.Add(new Tuple<Collider, float[]>(other, _beachList[other]));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_inBeachTrigger)
            {
                if (_beachList.ContainsKey(other))
                {
                    foreach (var heights in _beachHeights)
                    {
                        if (heights.Item1 == other)
                        {
                            _beachHeights.Remove(new Tuple<Collider, float[]>(other, _beachList[other]));
                            break;
                        }
                    }

                    _inBeachTrigger = _beachList.Count > 0;
                }
            }
        }
        
        /// <summary>
        /// Modulus that always returns positive remainder.
        /// </summary>
        private static float Mod(float x, float m) {
            return (x%m + m)%m;
        }
    }
}
