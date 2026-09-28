using PiGame.Lobby;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkPlayerState), typeof(PlayerMove))]
    public class NetworkPlayerCombat : NetworkBehaviour, IGameplayInputBlocker
    {
        [SerializeField] private InputActionReference _aimDirectionAction;
        [SerializeField] private InputActionReference _aimPointerAction;
        [SerializeField] private InputActionReference _aimFireAction;
        [SerializeField] private InputActionReference _cancelAimAction;
        [SerializeField] private InputActionReference _abilityAction;
        [SerializeField] private SpriteRenderer _aimIndicator;
        [SerializeField] private float _aimIndicatorDistance = 1.15f;

        private NetworkPlayerState _playerState;
        private PlayerMove _playerMove;
        private ICharacterCombat _characterCombat;
        private Vector2 _aimDirection = Vector2.right;
        private bool _localAimHeld;
        private bool _localAbilityHeld;
        private bool _waitForAimRelease;
        private bool _waitForAbilityRelease;
        private bool _isGameplayInputBlocked;

        private void Awake()
        {
            _playerState = GetComponent<NetworkPlayerState>();
            _playerMove = GetComponent<PlayerMove>();
            _characterCombat = GetComponent<ICharacterCombat>();
            if (_characterCombat == null || _aimIndicator == null)
            {
                Debug.LogError("Configure ICharacterCombat e o indicador de mira no prefab do jogador.", this);
                enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!enabled)
            {
                return;
            }

            _aimIndicator.gameObject.SetActive(IsOwner);
            _aimIndicator.enabled = false;
            if (IsOwner)
            {
                if (!ConfigureInput())
                {
                    enabled = false;
                    return;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            _isGameplayInputBlocked = false;
            _waitForAimRelease = false;
            _waitForAbilityRelease = false;
            _localAimHeld = false;
            _localAbilityHeld = false;
            if (_aimIndicator != null)
            {
                _aimIndicator.enabled = false;
                _aimIndicator.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (_isGameplayInputBlocked)
            {
                _waitForAimRelease = true;
                _waitForAbilityRelease = true;
                CancelLocalAim();
                _localAbilityHeld = false;
                return;
            }

            if (!_playerState.CanAct)
            {
                _waitForAimRelease = true;
                _waitForAbilityRelease = true;
                CancelLocalAim();
                _localAbilityHeld = false;
                return;
            }

            Vector2 aim = ReadAimDirection();
            if (aim.sqrMagnitude >= 0.04f)
            {
                _aimDirection = aim.normalized;
                RefreshAimIndicator();
            }
            if(_localAimHeld)
            {
                UpdateShootAimRpc(_aimDirection);
            }

            UpdateAbilityInput();

            bool aimPressed = ReadAimPressed();
            if (_waitForAimRelease)
            {
                if (aimPressed)
                {
                    return;
                }

                _waitForAimRelease = false;
            }

            bool cancelPressed = ReadCancelPressed();

            if (cancelPressed && _localAimHeld)
            {
                _waitForAimRelease = true;
                CancelLocalAim();
                return;
            }

            if (aimPressed)
            {
                _localAimHeld = true;
                RefreshAimIndicator();

                FireRpc(_aimDirection);
                return;
            }

            if (!_localAimHeld)
            {
                return;
            }

            _localAimHeld = false;
            RefreshAimIndicator();


            ShootReleasedRpc();
            FireRpc(_aimDirection);
        }

        public void SetGameplayInputBlocked(bool isBlocked)
        {
            if (!IsOwner)
            {
                return;
            }

            _isGameplayInputBlocked = isBlocked;
            if (isBlocked)
            {
                _waitForAimRelease = true;
                _waitForAbilityRelease = true;
                CancelLocalAim();
                _localAbilityHeld = false;
            }
        }

        private void UpdateAbilityInput()
        {
            bool abilityPressed = _abilityAction.action.IsPressed();
            if (_waitForAbilityRelease)
            {
                if (abilityPressed)
                {
                    return;
                }

                _waitForAbilityRelease = false;
            }

            if (abilityPressed == _localAbilityHeld)
            {
                return;
            }

            _localAbilityHeld = abilityPressed;
            Vector2 moveDirection = _playerMove.ReadMovementDirection();
            if (abilityPressed)
            {
                BeginAbilityRpc(_aimDirection, moveDirection);
            }
            else
            {
                EndAbilityRpc(_aimDirection, moveDirection);
            }
        }

        private void CancelLocalAim()
        {
            _localAimHeld = false;
            RefreshAimIndicator();
        }

        [Rpc(SendTo.Server)]
        private void FireRpc(Vector2 direction, RpcParams rpcParams = default)
        {
            if (!_playerState.CanAct || rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _characterCombat.ShootServer(direction);
        }

        [Rpc(SendTo.Server)]
        private void UpdateShootAimRpc(Vector2 direction, RpcParams rpcParams = default)
        {
            if(!_playerState.CanAct || rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _characterCombat.UpdateShootAimServer(direction);
        }

        [Rpc(SendTo.Server)]
        private void ShootReleasedRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _characterCombat.ShootReleaseServer();
        }

        [Rpc(SendTo.Server)]
        private void BeginAbilityRpc(
            Vector2 aimDirection, Vector2 moveDirection, RpcParams rpcParams = default)
        {
            if (_playerState.CanAct && rpcParams.Receive.SenderClientId == OwnerClientId)
            {
                _characterCombat.BeginAbilityServer(aimDirection, moveDirection);
            }
        }

        [Rpc(SendTo.Server)]
        private void EndAbilityRpc(
            Vector2 aimDirection, Vector2 moveDirection, RpcParams rpcParams = default)
        {
            if (_playerState.CanAct && rpcParams.Receive.SenderClientId == OwnerClientId)
            {
                _characterCombat.EndAbilityServer(aimDirection, moveDirection);
            }
        }

        private Vector2 ReadAimDirection()
        {
            if (_playerState.InputDevice == LobbyInputDeviceKind.Gamepad)
            {
                return _aimDirectionAction.action.ReadValue<Vector2>();
            }

            if (Camera.main != null)
            {
                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
                    _aimPointerAction.action.ReadValue<Vector2>());
                return mouseWorld - transform.position;
            }

            return _aimDirection;
        }

        private bool ReadAimPressed()
        {
            return _aimFireAction.action.IsPressed();
        }

        private bool ReadCancelPressed()
        {
            return _cancelAimAction.action.WasPressedThisFrame();
        }

        private bool ConfigureInput()
        {
            InputAction aimDirection = _aimDirectionAction != null ? _aimDirectionAction.action : null;
            InputAction aimPointer = _aimPointerAction != null ? _aimPointerAction.action : null;
            InputAction aimFire = _aimFireAction != null ? _aimFireAction.action : null;
            InputAction cancelAim = _cancelAimAction != null ? _cancelAimAction.action : null;
            InputAction ability = _abilityAction != null ? _abilityAction.action : null;
            if (aimDirection == null || aimPointer == null || aimFire == null
                || cancelAim == null || ability == null
                || aimDirection.actionMap == null
                || aimDirection.actionMap != aimPointer.actionMap
                || aimDirection.actionMap != aimFire.actionMap
                || aimDirection.actionMap != cancelAim.actionMap
                || aimDirection.actionMap != ability.actionMap)
            {
                Debug.LogError("Configure AimDirection, AimPointer, AimFire, CancelAim e Ability do mesmo Action Map no NetworkPlayerCombat.", this);
                return false;
            }

            return true;
        }

        private void RefreshAimIndicator()
        {
            _aimIndicator.enabled = IsOwner && _localAimHeld && _playerState.IsAlive;
            _aimIndicator.color = _playerState.IndicatorColor;
            _aimIndicator.transform.localPosition =
                (Vector3)(_aimDirection * _aimIndicatorDistance);
        }
    }
}
