using Unity.Netcode;
using UnityEngine;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkPlayerState))]
    public class IrrigadorCombat : NetworkBehaviour, ICharacterCombat
    {
        [SerializeField] private ProjectileDefinition _projectile;
        [SerializeField, Min(0f)] private float _shotCooldownSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _projectileSpawnDistance = 0.85f;
        [SerializeField, Min(1f)] private int _maxNails = 4;
        private NetworkVariable<int> _nails = new NetworkVariable<int>(4);
        public int Nails => _nails.Value;

        private NetworkPlayerState _playerState;
        private float _nextShotTime;

        private void Awake()
        {
            _playerState = GetComponent<NetworkPlayerState>();
            if (_projectile == null || _projectile.Prefab == null)
            {
                Debug.LogError("Configure uma definição com prefab de projétil no BasicCharacterCombat.", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            if(!IsServer)
                return;
            
            _nails.Value = _maxNails;
        }

        public void ShootServer(Vector2 direction)
        {
            if (!IsServer || !_playerState.CanAct || _projectile == null
                || _projectile.Prefab == null || Time.time < _nextShotTime || _nails.Value <= 0)
            {
                return;
            }

            _nextShotTime = Time.time + _shotCooldownSeconds;

            Vector2 shotDirection = direction.sqrMagnitude > 0.01f
                ? direction.normalized
                : Vector2.right;
            Vector3 spawnPosition = transform.position
                + (Vector3)(shotDirection * _projectileSpawnDistance);

            NetworkProjectile projectile = Instantiate(
                _projectile.Prefab, spawnPosition, Quaternion.identity);
            projectile.NetworkObject.Spawn(true);
            projectile.InitializeServer(
                OwnerClientId, shotDirection, _playerState.IndicatorColor, _projectile);
            
            _nails.Value--;
        }

        public void AddNailServer()
        {
            if(!IsServer)
                return;
            
            _nails.Value = Mathf.Min(_nails.Value + 1, _maxNails);
        }

        public void BeginAbilityServer(Vector2 aimDirection, Vector2 moveDirection)
        {
        }

        public void EndAbilityServer(Vector2 aimDirection, Vector2 moveDirection)
        {
        }
    }
}

