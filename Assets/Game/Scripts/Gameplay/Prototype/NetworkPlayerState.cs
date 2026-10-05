using System;
using PiGame.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerState : NetworkBehaviour
    {
        [SerializeField] private int _maximumHealth = 3;

        private readonly NetworkVariable<int> _playerSlot = new NetworkVariable<int>(-1);
        private readonly NetworkVariable<LobbyCharacterId> _characterId =
            new NetworkVariable<LobbyCharacterId>(LobbyCharacterId.None);
        private readonly NetworkVariable<LobbyInputDeviceKind> _inputDevice =
            new NetworkVariable<LobbyInputDeviceKind>(LobbyInputDeviceKind.Unknown);
        private readonly NetworkVariable<Color> _indicatorColor =
            new NetworkVariable<Color>(Color.white);
        private readonly NetworkVariable<int> _currentHealth = new NetworkVariable<int>();
        private readonly NetworkVariable<bool> _isAlive = new NetworkVariable<bool>(true);
        private readonly NetworkVariable<bool> _matchActive = new NetworkVariable<bool>(true);

        private SpriteRenderer[] _spriteRenderers;
        private SpriteRenderer _bodySpriteRenderer;
        private Collider2D[] _colliders;
        private Rigidbody2D _rigidbody;
        private bool _isInvulnerable;

        public event Action StateChanged;
        public event Action<NetworkPlayerState, ulong> Died;

        public int PlayerSlot => _playerSlot.Value;
        public LobbyCharacterId CharacterId => _characterId.Value;
        public LobbyInputDeviceKind InputDevice => _inputDevice.Value;
        public Color IndicatorColor => _indicatorColor.Value;
        public int CurrentHealth => _currentHealth.Value;
        public int MaximumHealth => _maximumHealth;
        public bool IsAlive => _isAlive.Value;
        public bool CanAct => _isAlive.Value && _matchActive.Value;
        public bool IsInvulnerable => _isInvulnerable;

        private void Awake()
        {
            _bodySpriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        public override void OnNetworkSpawn()
        {
            _playerSlot.OnValueChanged += HandleIntChanged;
            _characterId.OnValueChanged += HandleCharacterChanged;
            _inputDevice.OnValueChanged += HandleInputChanged;
            _indicatorColor.OnValueChanged += HandleColorChanged;
            _currentHealth.OnValueChanged += HandleIntChanged;
            _isAlive.OnValueChanged += HandleAliveChanged;
            _matchActive.OnValueChanged += HandleBoolChanged;

            ApplyAlivePresentation(_isAlive.Value);
            ApplyHealthPresentation();
            StateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _playerSlot.OnValueChanged -= HandleIntChanged;
            _characterId.OnValueChanged -= HandleCharacterChanged;
            _inputDevice.OnValueChanged -= HandleInputChanged;
            _indicatorColor.OnValueChanged -= HandleColorChanged;
            _currentHealth.OnValueChanged -= HandleIntChanged;
            _isAlive.OnValueChanged -= HandleAliveChanged;
            _matchActive.OnValueChanged -= HandleBoolChanged;
        }

        public void InitializeServer(LobbyPlayerData playerData, Color indicatorColor)
        {
            if (!IsServer)
            {
                return;
            }

            _playerSlot.Value = playerData.PlayerSlot;
            _characterId.Value = playerData.CharacterId;
            _inputDevice.Value = playerData.InputDevice;
            _indicatorColor.Value = indicatorColor;
            _currentHealth.Value = _maximumHealth;
            _isAlive.Value = true;
            _matchActive.Value = true;
            _isInvulnerable = false;
        }

        public void SetInvulnerableServer(bool isInvulnerable)
        {
            if (IsServer)
            {
                _isInvulnerable = isInvulnerable;
            }
        }

        public void SetMatchActiveServer(bool isActive)
        {
            if (!IsServer)
            {
                return;
            }

            _matchActive.Value = isActive;
            if (!isActive && _rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }

        public void ApplyDamageServer(int amount, ulong attackerClientId)
        {
            if (!IsServer || !CanAct || _isInvulnerable || amount <= 0)
            {
                return;
            }

            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - amount);
            if (_currentHealth.Value > 0)
            {
                return;
            }

            _isAlive.Value = false;
            _matchActive.Value = false;
            Died?.Invoke(this, attackerClientId);
        }

        public void RespawnServer(Vector3 position)
        {
            if (!IsServer)
            {
                return;
            }

            transform.position = position;
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }

            TeleportOwnerRpc(
                position,
                RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));

            _currentHealth.Value = _maximumHealth;
            _isAlive.Value = true;
            _matchActive.Value = true;
            _isInvulnerable = false;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void TeleportOwnerRpc(Vector3 position, RpcParams rpcParams)
        {
            transform.position = position;
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }

        private void HandleAliveChanged(bool previousValue, bool currentValue)
        {
            ApplyAlivePresentation(currentValue);
            StateChanged?.Invoke();
        }

        private void ApplyAlivePresentation(bool isAlive)
        {
            foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
            {
                spriteRenderer.enabled = isAlive;
            }

            foreach (Collider2D playerCollider in _colliders)
            {
                playerCollider.enabled = isAlive;
            }

            if (_rigidbody != null)
            {
                _rigidbody.simulated = isAlive;
            }
        }

        private void HandleIntChanged(int previousValue, int currentValue)
        {
            ApplyHealthPresentation();
            StateChanged?.Invoke();
        }

        private void ApplyHealthPresentation()
        {
            float healthRatio = _maximumHealth > 0
                ? (float)_currentHealth.Value / _maximumHealth
                : 0f;
            Color damageColor = new Color(0.55f, 0.18f, 0.18f, 1f);
            Color playerColor = Color.Lerp(damageColor, Color.white, healthRatio);

            _bodySpriteRenderer.color = playerColor;
        }

        private void HandleCharacterChanged(
            LobbyCharacterId previousValue,
            LobbyCharacterId currentValue)
        {
            StateChanged?.Invoke();
        }

        private void HandleInputChanged(
            LobbyInputDeviceKind previousValue,
            LobbyInputDeviceKind currentValue)
        {
            StateChanged?.Invoke();
        }

        private void HandleColorChanged(Color previousValue, Color currentValue)
        {
            StateChanged?.Invoke();
        }

        private void HandleBoolChanged(bool previousValue, bool currentValue)
        {
            StateChanged?.Invoke();
        }
    }
}
