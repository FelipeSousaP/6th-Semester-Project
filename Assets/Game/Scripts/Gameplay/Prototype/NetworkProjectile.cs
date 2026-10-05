using Unity.Netcode;
using UnityEngine;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider2D))]
    public class NetworkProjectile : NetworkBehaviour
    {
        private readonly NetworkVariable<Color> _color =
            new NetworkVariable<Color>(Color.white);

        private SpriteRenderer _spriteRenderer;
        protected Vector2 _direction;
        protected ulong _shooterClientId;
        public ulong ShooterClientId => _shooterClientId;
        protected float _despawnAt;
        protected float _speed;
        protected int _damage;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            _color.OnValueChanged += HandleColorChanged;
            ApplyColor(_color.Value);
        }

        public override void OnNetworkDespawn()
        {
            _color.OnValueChanged -= HandleColorChanged;
        }

        public virtual void InitializeServer(
            ulong shooterClientId,
            Vector2 direction,
            Color color,
            ProjectileDefinition definition)
        {
            if (!IsServer)
            {
                return;
            }

            _shooterClientId = shooterClientId;
            _speed = definition.Speed;
            _damage = definition.Damage;
            _direction = direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector2.right;
            _color.Value = color;
            _despawnAt = Time.time + definition.LifetimeSeconds;
        }

        protected virtual void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
            if (Time.time >= _despawnAt)
            {
                NetworkObject.Despawn();
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (other.GetComponentInParent<NetworkProjectile>() != null)
            {
                return;
            }

            NetworkPlayerState playerState = other.GetComponentInParent<NetworkPlayerState>();
            if (playerState != null)
            {
                if (playerState.OwnerClientId == _shooterClientId)
                {
                    return;
                }

                if (playerState.IsInvulnerable)
                {
                    return;
                }

                playerState.ApplyDamageServer(_damage, _shooterClientId);
            }

            NetworkObject.Despawn();
        }

        private void HandleColorChanged(Color previousValue, Color currentValue)
        {
            ApplyColor(currentValue);
        }

        private void ApplyColor(Color color)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
        }
    }
}
