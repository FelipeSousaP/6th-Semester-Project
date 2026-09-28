using UnityEngine;
using Unity.Netcode;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider2D))]
    public class IrrigadorProjectile : NetworkProjectile
    {
        [Header("Projectile Settings")]
        [SerializeField] private Collider2D _collectionTrigger;
        [SerializeField] private Collider2D _tipCollider;
        [SerializeField] private Transform _tip;
        private Collider2D _projectileCollider;
        private Rigidbody2D _rigidbody;
        [SerializeField] private LayerMask _surfaceLayer;
        public LayerMask SurfaceLayer => _surfaceLayer;

        private bool _IsStuck;
        public bool IsStuck => _IsStuck;

        private bool _beingPulled;
        private Vector2 _pullDirection;
        private float _pullSpeed;

        private void Awake()
        {
            _projectileCollider = GetComponent<Collider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();
            UpdateColliders();
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

            _IsStuck = false;

            UpdateColliders();
        }

        protected override void Update()
        {
            if (!IsServer || !IsSpawned || _IsStuck)
                return;

            Vector2 previousTipPosition = _tip.position;

            Vector2 movement = _direction * _speed * Time.deltaTime;

            RaycastHit2D hit = Physics2D.Raycast(
                previousTipPosition,
                movement.normalized,
                movement.magnitude,
                _surfaceLayer
            );

            if (hit.collider != null)
            {
                HandleTipSurfaceContact(hit.collider);
                return;
            }

            transform.position += (Vector3)movement;

            if (Time.time >= _despawnAt)
            {
                NetworkObject.Despawn();
            }
        }

        public void HandleTipSurfaceContact(Collider2D other)
        {
            if(!IsServer || _IsStuck)
                return;

            StickToSurface(other);
        }

        private void StickToSurface(Collider2D surface)
        {
            _IsStuck = true;
            _beingPulled = false;

            transform.rotation = Quaternion.FromToRotation(Vector2.right, _direction);
            Physics2D.SyncTransforms();

            ColliderDistance2D distance = _tipCollider.Distance(surface);

            Vector2 correction = distance.pointB - distance.pointA;

            transform.position += (Vector3)correction;

            UpdateColliders();
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if(!IsServer || !IsSpawned)
                return;
            
            NetworkPlayerState playerState = other.GetComponentInParent<NetworkPlayerState>();

            if(!_IsStuck)
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
        

        public void PullServer(Vector2 direction, float speed)
        {
            if(!IsServer || !IsStuck)
                return;
            
            _beingPulled = true;
            _pullDirection = direction;
            _pullSpeed = speed;

            UpdateColliders();
        }

        private void FixedUpdate()
        {
            if (!IsServer || !_beingPulled)
                return;

            _rigidbody.linearVelocity =
                _pullDirection * _pullSpeed;
        }

        public void StopPullServer()
        {
            _beingPulled = false;
            _rigidbody.linearVelocity = Vector2.zero;

            UpdateColliders();
        }

        private void UpdateColliders()
        {
            bool isFlying = !_IsStuck && !_beingPulled;
            bool IsStuck = _IsStuck && !_beingPulled;
            bool isBeingPulled = _beingPulled;

            if(_projectileCollider != null)
            { 
                _projectileCollider.enabled = isFlying || isBeingPulled;
            }
            if(_tipCollider != null)
            {
                _tipCollider.enabled = isFlying || isBeingPulled;
            }
            if(_collectionTrigger!= null)
            {   
                _collectionTrigger.enabled = IsStuck || isBeingPulled;
            }
        }

    }
}

