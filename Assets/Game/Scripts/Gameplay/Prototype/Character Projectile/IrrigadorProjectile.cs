using UnityEngine;
using Unity.Netcode;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider2D))]
    public class IrrigadorProjectile : NetworkProjectile
    {
        [SerializeField] private Collider2D _collectionTrigger;
        [SerializeField] private Collider2D _tipCollider;
        private Collider2D _projectileCollider;

        private bool _isStuck;

        private void Awake()
        {
            _projectileCollider = GetComponent<Collider2D>();
            if (_collectionTrigger != null)
                _collectionTrigger.enabled = false;
        }

        public void InitializeServer(
            ulong shooterClientId,
            Vector2 direction,
            ProjectileDefinition definition)
        {
            if (!IsServer)
                return;

            _shooterClientId = shooterClientId;
            _speed = definition.Speed;
            _damage = definition.Damage;

            _direction = direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector2.right;

            _despawnAt = Time.time + definition.LifetimeSeconds;

            _isStuck = false;

            _projectileCollider.enabled = true;
            _collectionTrigger.enabled = false;
        }

        protected override void Update()
        {
            if (!IsServer || !IsSpawned || _isStuck)
                return;

            if(_isStuck)
                return;

            transform.position +=
                (Vector3)(_direction * _speed * Time.deltaTime);

            if (Time.time >= _despawnAt)
            {
                NetworkObject.Despawn();
            }
        }

        public void HandleTipSurfaceContact(Collider2D other)
        {
            if(!IsServer || _isStuck)
                return;

            StickToSurface(other);
        }

        private void StickToSurface(Collider2D surface)
        {
            _isStuck = true;

            transform.rotation = Quaternion.FromToRotation(Vector2.right, _direction);
            Physics2D.SyncTransforms();

            ColliderDistance2D distance = _tipCollider.Distance(surface);

            Vector2 correction = distance.pointB - distance.pointA;

            transform.position += (Vector3)correction;

            _projectileCollider.enabled = false;
            _tipCollider.enabled = false;
            _collectionTrigger.enabled = true;
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if(!IsServer || !IsSpawned)
                return;
            
            NetworkPlayerState playerState = other.GetComponentInParent<NetworkPlayerState>();

            if(!_isStuck)
            {
                if(playerState == null)
                    return;
                
                base.OnTriggerEnter2D(other);
                return;
            }


            if(playerState == null)
            {
                return;
            }

            if(playerState.OwnerClientId != _shooterClientId)
                return;
            
            IrrigadorCombat combat = playerState.GetComponent<IrrigadorCombat>();

            if(combat == null)
                return;
            
            combat.AddNailServer();
            NetworkObject.Despawn();

        }

    }
}

