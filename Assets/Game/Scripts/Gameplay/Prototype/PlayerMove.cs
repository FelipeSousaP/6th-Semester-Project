using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMove : NetworkBehaviour, IGameplayInputBlocker
    {
        [SerializeField] private float _moveSpeed = 7f;
        [SerializeField] private float _jumpForce = 12f;

        [SerializeField] private InputActionReference _moveAction;
        [SerializeField] private InputActionReference _jumpAction;
        [SerializeField] private InputActionReference _crouchAction;

        private Rigidbody2D _rigidbody;
        private BoxCollider2D _boxCollider2D;
        private NetworkPlayerState _playerState;

        private Animator _playerAnimator;
        private SpriteRenderer _spriteRenderer;

        [Header("Ground")]
        [SerializeField] private float _groundCheckDistance = 0.1f;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private Transform _groundCheck;

        [Header("Jump")]
        [SerializeField] private float _jumpBufferTime;
        [SerializeField] private float _coyoteTimer;
        private float _jumpBufferCounter;
        private float _coyoteCounter;


        [Header("Wall")]
        [SerializeField] private Transform _wallCheck;
        [SerializeField] private float _wallCheckRadius = 0.15f;
        [SerializeField] private LayerMask _wallLayer;
        [SerializeField] private float _wallStickTime = 0.5f;
        [SerializeField] private float _wallSlideSpeed = 3f;
        [SerializeField] private float _wallCheckXPos;

        [Header("WallJump")]
        [SerializeField] private float _wallJumpHorizontalForce = 10f;
        [SerializeField] private float _wallJumpVerticalForce = 12f;
        [SerializeField] private float _wallJumpControlLockTime = 0.2f;

        [Header("Crouch")]
        [SerializeField] private float _colliderShirnkOffset;
        [SerializeField] private float _colliderShirnkSize;
        private Vector2 _originalColliderSize;
        private Vector2 _originalColliderOffset;

        private bool _isGrounded;
        private bool _isTouchingWall;
        private bool _isWallSliding;
        private bool _isCrounching;

        private float _stickTimer;

        private int _wallDirection;
        private float _wallJumpControlTimer;
        private bool _isGameplayInputBlocked;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _playerState = GetComponent<NetworkPlayerState>();
            _playerAnimator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _boxCollider2D = GetComponent<BoxCollider2D>();

            _originalColliderSize = _boxCollider2D.size;
            _originalColliderOffset = _boxCollider2D.offset;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
                return;

            _moveAction.action.Enable();
            _jumpAction.action.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
                return;

            _isGameplayInputBlocked = false;
            _moveAction.action.Disable();
            _jumpAction.action.Disable();
        }

        private void Update()
        {
            if (!IsOwner)
                return;

            if (_isGameplayInputBlocked)
            {
                _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
                return;
            }

            if (_playerState != null && !_playerState.CanAct)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            if (_wallJumpControlTimer > 0f)
            {
                _wallJumpControlTimer -= Time.deltaTime;
            }

            CheckGround();
            CheckWall();

            HandleMovement();
            HandleWallSlide();
            HandleJump();


            HandleCrouch();           
        }

        public void SetGameplayInputBlocked(bool isBlocked)
        {
            if (!IsOwner)
            {
                return;
            }

            _isGameplayInputBlocked = isBlocked;
            if (isBlocked && _rigidbody != null)
            {
                _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
            }
        }

        private void HandleMovement()
        {
            if (_wallJumpControlTimer > 0f) return;

            Vector2 input = _moveAction.action.ReadValue<Vector2>();
            _rigidbody.linearVelocity = new Vector2(input.x * _moveSpeed, _rigidbody.linearVelocity.y);

            _playerAnimator.SetFloat("xVelocity", Math.Abs(_rigidbody.linearVelocity.x));
            _playerAnimator.SetFloat("yVelocity", _rigidbody.linearVelocity.y);

            _wallDirection = (int)input.x;

            _wallCheck.position = new Vector2(transform.position.x + 0.25f * Mathf.Sign(input.x), transform.position.y);

            _spriteRenderer.flipX = Mathf.Sign(input.x) > 0.01f ? false : true;

        }

        private void HandleCrouch()
        {

            if (_crouchAction.action.WasPressedThisFrame())
            {
                if (!_isGrounded) return;

                _isCrounching = true;
                _boxCollider2D.size = new Vector2(_originalColliderSize.x, _originalColliderSize.y * _colliderShirnkSize);
                _boxCollider2D.offset = new Vector2(_originalColliderOffset.x, _colliderShirnkOffset);
            }

            if (_crouchAction.action.WasReleasedThisFrame())
            {
                if (!_isCrounching) return;

                _isCrounching = false;
                _boxCollider2D.size = _originalColliderSize;
                _boxCollider2D.offset = _originalColliderOffset;
            }

        }

        private void HandleJump()
        {
            if (_jumpAction.action.WasPressedThisFrame())
            {

                _jumpBufferCounter = _jumpBufferTime;

            }


            if (_jumpAction.action.WasReleasedThisFrame())
            {
                _jumpBufferCounter = 0;

                if (_rigidbody.linearVelocity.y > 0f)
                {
                    _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 1);

                }

            }


            if (_jumpBufferCounter > 0f)
            {

                _jumpBufferCounter -= Time.deltaTime;

                if (_isWallSliding)
                {
                    float jumpDirection = -_wallDirection;

                    _rigidbody.linearVelocity = new Vector2(jumpDirection * _wallJumpHorizontalForce, _wallJumpVerticalForce);
                    _playerAnimator.SetBool("isJumping", true);

                    _wallJumpControlTimer = _wallJumpControlLockTime;

                    _stickTimer = 0f;
                    _jumpBufferCounter = 0f;

                    return;
                }

                if (_coyoteCounter > 0f)
                {
                    _coyoteCounter = 0f;
                    _jumpBufferCounter = 0f;

                    _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, _jumpForce);

                    _playerAnimator.SetBool("isJumping", true);
                    _isGrounded = false;
                    return;
                }

            }

        }

        private void CheckGround()
        {
            if (_boxCollider2D == null || _groundCheck == null)
            {
                _isGrounded = false;
                return;
            }

            Bounds bounds = _boxCollider2D.bounds;
            Vector2 probeOrigin = new Vector2(
                _groundCheck.position.x,
                bounds.min.y + Physics2D.defaultContactOffset);
            Vector2 probeSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x * 0.75f),
                Physics2D.defaultContactOffset * 2f);

            RaycastHit2D hit = Physics2D.BoxCast(
                probeOrigin,
                probeSize,
                0f,
                Vector2.down,
                _groundCheckDistance,
                _groundLayer);

            bool isFallingOrStill = _rigidbody.linearVelocity.y <= 0.05f;
            bool foundFloor = hit.collider != null && hit.normal.y >= 0.5f;
            _isGrounded = isFallingOrStill && foundFloor;

            if (_isGrounded)
            {
                _playerAnimator.SetBool("isJumping", false);
                _coyoteCounter = _coyoteTimer;
            }
            else
            {
                if (_coyoteCounter > 0)
                {
                    _coyoteCounter -= Time.deltaTime;
                }
            }
        }

        private void CheckWall()
        {
            if (_isGrounded)
            {
                _isWallSliding = false;
                _stickTimer = _wallStickTime;
                return;
            }

            _isTouchingWall = Physics2D.OverlapCircle(_wallCheck.position, _wallCheckRadius, _groundLayer);

        }

        private bool IsPressingAgainstWall()
        {
            Vector2 input = _moveAction.action.ReadValue<Vector2>();

            if (_wallDirection != 0)
            {

                return (_wallDirection * input.x) > 0;

            }

            return false;
        }

        private void HandleWallSlide()
        {
            if (_wallJumpControlTimer > 0f) return;

            if (_isGrounded)
            {
                _isWallSliding = false;
                _stickTimer = 0f;
                return;
            }

            if (!_isTouchingWall || !IsPressingAgainstWall())
            {
                _isWallSliding = false;
                _stickTimer = 0f;
                return;
            }

            _isWallSliding = true;

            _stickTimer += Time.deltaTime;

            if (_stickTimer < _wallStickTime)
            {
                _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0f);
            }
            else
            {
                _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, -_wallSlideSpeed);
            }
        }
    }
}

