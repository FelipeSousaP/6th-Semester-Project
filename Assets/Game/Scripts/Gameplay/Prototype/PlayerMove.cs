using PiGame.Input;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private InputActionReference _aimFireAction;
        [SerializeField] private InputActionReference _abilityAction;

        private Rigidbody2D _rigidbody;
        private Collider2D _bodyCollider;
        private NetworkPlayerState _playerState;

        private Animator _playerAnimator;
        private SpriteRenderer _spriteRenderer;
        [Header("Ground")]
        [SerializeField] private float _groundCheckDistance = 0.1f;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private Transform _groundCheck;

        [Header("Wall")]
        [SerializeField] private Transform _wallCheck;
        [SerializeField] private float _wallCheckDistance = 0.15f;
        [SerializeField] private LayerMask _wallLayer;

        [SerializeField] private float _wallStickTime = 0.5f;
        [SerializeField] private float _wallSlideSpeed = 3f;

        [Header("WallJump")]
        [SerializeField] private float _wallJumpHorizontalForce = 10f;
        [SerializeField] private float _wallJumpVerticalForce = 12f;
        [SerializeField] private float _wallJumpControlLockTime = 0.2f;

        private bool _isGrounded;
        private bool _isTouchingWall;
        private bool _isWallSliding;
        private bool _isCrouching;
        private bool _isDroppingFromWall;

        private float _stickTimer;

        private int _wallDirection;
        private float _wallJumpControlTimer ;
        private bool _isGameplayInputBlocked;
        private bool _waitForJumpRelease;
        private InputActionMap _playerActions;

        public bool IsCrouching => _isCrouching;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _bodyCollider = GetComponent<Collider2D>();
            _playerState = GetComponent<NetworkPlayerState>();
            _playerAnimator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            if(!IsOwner)
                return;

            if (!ConfigureInput())
            {
                enabled = false;
                return;
            }

            _playerActions.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if(!IsOwner)
                return;
            _isGameplayInputBlocked = false;
            _waitForJumpRelease = false;
            ResetCrouch();
            _playerActions?.Disable();
        }

        private void Update()
        {
            if(!IsOwner)
                return;
            Debug.Log("sasad");
            if (_isGameplayInputBlocked)
            {
                ResetCrouch();
                _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
                return;
            }

            if(_playerState != null && !_playerState.CanAct)
            {
                ResetCrouch();
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            CheckGround();
            CheckWall();
            UpdateCrouch();
            
            HandleMovement();
            HandleWallSlide();
            HandleJump();

            if (_wallJumpControlTimer  > 0f)
            {
                _wallJumpControlTimer  -= Time.deltaTime;
            }
        }

        public void SetGameplayInputBlocked(bool isBlocked)
        {
            if (!IsOwner)
            {
                return;
            }

            _isGameplayInputBlocked = isBlocked;
            if (isBlocked)
                _waitForJumpRelease = true;

            if (isBlocked && _rigidbody != null)
            {
                ResetCrouch();
                _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
            }
        }

        private void HandleMovement()
        {
            if (_wallJumpControlTimer  > 0f)
                return;
            Vector2 input = _moveAction.action.ReadValue<Vector2>();
            Debug.Log("Salve, estou lendo: " + input)  ;
            if (_isCrouching)
            {
                input.x = 0f;
            }

            _rigidbody.linearVelocity = new Vector2(input.x * _moveSpeed, _rigidbody.linearVelocity.y);
            _playerAnimator.SetFloat("xVelocity",Math.Abs(_rigidbody.linearVelocity.x));
            _playerAnimator.SetFloat("yVelocity",_rigidbody.linearVelocity.y);
            if (input.x > 0.01f)
            {
                _spriteRenderer.flipX = false;
            }
            else if (input.x < -0.01f)
            {
                _spriteRenderer.flipX = true;
            }
        }

        private void HandleJump()
        {
            if (_waitForJumpRelease)
            {
                if (!_jumpAction.action.IsPressed())
                    _waitForJumpRelease = false;
                return;
            }

            if(_isCrouching || !_jumpAction.action.WasPressedThisFrame())
                return;

            if(_isWallSliding)
            {
                float jumpDirection = -_wallDirection;

                _rigidbody.linearVelocity = new Vector2(jumpDirection * _wallJumpHorizontalForce, _wallJumpVerticalForce);
                _playerAnimator.SetBool("isJumping", true);

                _wallJumpControlTimer  = _wallJumpControlLockTime;

                _isWallSliding = false;
                _stickTimer = 0f;

                return;
            }

            if(!_isGrounded)
            {
                return;
            }

            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, _jumpForce);
            _playerAnimator.SetBool("isJumping", true);
            _isGrounded = false;
        }

        private void CheckGround()
        {
            if (_bodyCollider == null || _groundCheck == null)
            {
                _isGrounded = false;
                return;
            }

            Bounds bounds = _bodyCollider.bounds;
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
            }
        }

        private void CheckWall()
        {
            RaycastHit2D rightHit = Physics2D.Raycast(_wallCheck.position, Vector2.right, _wallCheckDistance, _wallLayer);
            RaycastHit2D leftHit = Physics2D.Raycast(_wallCheck.position, Vector2.left, _wallCheckDistance, _wallLayer);

            if(rightHit.collider != null)
            {
                _isTouchingWall = true;
                _wallDirection = 1;
            }
            else if(leftHit.collider != null)
            {
                _isTouchingWall = true;
                _wallDirection = -1;
            }
            else
            {
                _isTouchingWall = false;
                _wallDirection = 0;
            }
        }

        private bool IsPressingAgainstWall()
        {
            Vector2 input = _moveAction.action.ReadValue<Vector2>();

            if(_wallDirection == 1)
            {
                return input.x > 0;
            }

            if(_wallDirection == -1)
            {
                return input.x < 0;
            }

            return false;
        }

        private bool ConfigureInput()
        {
            InputAction move = _moveAction != null ? _moveAction.action : null;
            InputAction jump = _jumpAction != null ? _jumpAction.action : null;
            InputAction crouch = _crouchAction != null ? _crouchAction.action : null;
            InputAction aimFire = _aimFireAction != null ? _aimFireAction.action : null;
            InputAction ability = _abilityAction != null ? _abilityAction.action : null;
            if (move == null || jump == null || crouch == null || aimFire == null || ability == null
                || move.actionMap != jump.actionMap
                || move.actionMap != crouch.actionMap
                || move.actionMap != aimFire.actionMap
                || move.actionMap != ability.actionMap)
            {
                Debug.LogError("Configure Move, Jump, Crouch, AimFire e Ability do mesmo Action Map no PlayerMove.", this);
                return false;
            }
            Debug.Log("é tru familia");
            _playerActions = move.actionMap;
            new InputBindingService(_playerActions.asset).Load();
            return true;
        }

        private void UpdateCrouch()
        {
            bool wantsToCrouch = _crouchAction.action.IsPressed();
            bool isAimingOrUsingAbility = _aimFireAction.action.IsPressed()
                || _abilityAction.action.IsPressed();
            _isCrouching = wantsToCrouch && _isGrounded && !isAimingOrUsingAbility;

            if (wantsToCrouch && !_isGrounded && _isTouchingWall)
            {
                _isDroppingFromWall = true;
                _isWallSliding = false;
                _stickTimer = 0f;
            }
            else if (!wantsToCrouch || _isGrounded)
            {
                _isDroppingFromWall = false;
            }
        }

        private void ResetCrouch()
        {
            _isCrouching = false;
            _isDroppingFromWall = false;
        }

        private void HandleWallSlide()
        {
            if (_isDroppingFromWall)
            {
                _isWallSliding = false;
                _stickTimer = 0f;
                return;
            }

            if(_isGrounded)
            {
                _isWallSliding = false;
                _stickTimer = 0f;
                return;
            }

            if(!_isTouchingWall || !IsPressingAgainstWall())
            {
                _isWallSliding = false;
                _stickTimer = 0f;
                return;
            }

            _isWallSliding = true;

            _stickTimer += Time.deltaTime;

            if(_stickTimer < _wallStickTime)
            {
                _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0f);
            }
            else
            {
               _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, -_wallSlideSpeed); 
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (_bodyCollider == null)
                _bodyCollider = GetComponent<Collider2D>();

            if (_bodyCollider == null)
                return;

            Bounds bounds = _bodyCollider.bounds;
            Vector2 probeOrigin = new Vector2(
                _groundCheck != null ? _groundCheck.position.x : bounds.center.x,
                bounds.min.y + Physics2D.defaultContactOffset);
            Vector2 probeSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x * 0.75f),
                Physics2D.defaultContactOffset * 2f);

            Gizmos.color = _isGrounded ? Color.green : Color.red;

            Vector2 rayDirection = Vector2.down * _groundCheckDistance;
            Gizmos.DrawWireCube(probeOrigin, probeSize);
            Gizmos.DrawLine(probeOrigin, probeOrigin + rayDirection);
        }
    }
}

