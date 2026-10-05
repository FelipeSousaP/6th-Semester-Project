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
        [SerializeField] private LayerMask _surfaceLayer;
        public LayerMask SurfaceLayer => _surfaceLayer;

        [Header("Safety")]
        [SerializeField] private float _despawnBelowY = -20f;

        [Header("CharacterHit Settings")]
        [SerializeField] private float _fallGravity = 20f;
        [SerializeField] private float _fallStartOffset = 0.1f;
        private bool _falling;
        private Vector2 _fallVelocity;

        private bool _IsStuck;
        public bool IsStuck => _IsStuck;

        private Collider2D _projectileCollider;
        private readonly Collider2D[] _collectionHits = new Collider2D[8];

        public bool _abilityPull;
        private bool _beingPulled;

        private ulong _lastHitClientId;

        private Vector2 _pullDirection;
        private float _pullSpeed;

        private Collider2D _stuckSurface;

        private bool _hasHitPlayer;

        private void Awake()
        {
            _projectileCollider = GetComponent<Collider2D>();
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

            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;

            _despawnAt = Time.time + definition.LifetimeSeconds;

            _IsStuck = false;
            _hasHitPlayer = false;
            _lastHitClientId = ulong.MaxValue;

            UpdateColliders();
        }

        protected override void Update()
        {
            if (!IsServer || !IsSpawned || (_IsStuck && !_beingPulled && !_falling))
                return;
            
            if (TryDespawnOutOfBounds())
                return;

            Vector2 direction;
            float speed;

            if (_falling)
            {
                _fallVelocity.y -= _fallGravity * Time.deltaTime;

                direction = _fallVelocity.normalized;
                speed = _fallVelocity.magnitude;
            }
            else
            {
                direction = _beingPulled ? _pullDirection : _direction;

                speed = _beingPulled ? _pullSpeed : _speed;

            }

            Vector2 previousTipPosition = _tip.position;
            Vector2 movement = direction * speed * Time.deltaTime;

            if (_beingPulled && TryCollectOwner())
                return;

            RaycastHit2D hit = Physics2D.Raycast(
                previousTipPosition,
                movement.normalized,
                movement.magnitude,
                _surfaceLayer
            );

            if (hit.collider != null)
            {
                if (!_beingPulled || hit.collider != _stuckSurface)
                {
                    HandleTipSurfaceContact(hit.collider);
                    return;
                }
            }

            transform.position += (Vector3)movement;

            if (!_beingPulled && Time.time >= _despawnAt)
            {
                NetworkObject.Despawn();
            }
        }

        public void HandleTipSurfaceContact(Collider2D other)
        {
            if (!IsServer || _IsStuck)
                return;

            StickToSurface(other);
        }

        private void StickToSurface(Collider2D surface)
        {
            if(!_falling)
            {
                Vector2 stickDirection = _beingPulled ? _pullDirection : _direction;

                transform.rotation = Quaternion.FromToRotation(Vector2.right,stickDirection);
            }


            _IsStuck = true;
            _falling = false;
            _beingPulled = false;
            _abilityPull = false;
            _stuckSurface = surface;

            ColliderDistance2D distance = _tipCollider.Distance(surface);

            Vector2 correction = distance.pointB - distance.pointA;

            transform.position += (Vector3)correction;

            UpdateColliders();
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsServer || !IsSpawned || _falling)
                return;

            NetworkPlayerState playerState =
                other.GetComponentInParent<NetworkPlayerState>();

            if (playerState == null)
                return;

            if (!_IsStuck || _beingPulled)
            {
                TryDamagePlayer(playerState);
                return;
            }

            if (playerState.OwnerClientId != _shooterClientId)
                return;

            IrrigadorCombat combat =
                playerState.GetComponent<IrrigadorCombat>();

            if (combat == null)
                return;

            combat.AddNailServer();
            NetworkObject.Despawn();
        }
        public void PullServer(Vector2 direction, float speed, bool abilityPull = false)
        {
            if (!IsServer || !IsStuck)
                return;

            _beingPulled = true;
            _abilityPull = abilityPull;

            _hasHitPlayer = false;
            _lastHitClientId = ulong.MaxValue;

            _pullDirection = direction.normalized;
            _pullSpeed = speed;

            UpdateColliders();
        }
        public void StopPullServer()
        {
            if (_abilityPull)
                return;

            _beingPulled = false;

            UpdateColliders();
        }

        private void UpdateColliders()
        {
            bool isFlying = !_IsStuck && !_beingPulled && !_falling;
            bool IsStuck = _IsStuck && !_beingPulled;
            bool isBeingPulled = _beingPulled;

            if (_projectileCollider != null)
            {
                _projectileCollider.enabled = isFlying || isBeingPulled;
            }
            if (_tipCollider != null)
            {
                _tipCollider.enabled = isFlying || isBeingPulled;
            }
            if (_collectionTrigger != null)
            {
                _collectionTrigger.enabled = IsStuck || isBeingPulled;
            }
        }

        private bool TryCollectOwner()
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = true
            };

            int hitCount = _collectionTrigger.Overlap(
                filter,
                _collectionHits
            );

            for (int i = 0; i < hitCount; i++)
            {
                NetworkPlayerState playerState =
                    _collectionHits[i].GetComponentInParent<NetworkPlayerState>();

                if (playerState == null ||
                    playerState.OwnerClientId != _shooterClientId)
                {
                    continue;
                }

                IrrigadorCombat combat =
                    playerState.GetComponent<IrrigadorCombat>();

                if (combat == null)
                    continue;

                combat.AddNailServer();
                NetworkObject.Despawn();
                return true;
            }

            return false;
        }

        private void StartFalling()
        {
            _falling = true;
            _beingPulled = false;
            _abilityPull = false;

            transform.rotation = Quaternion.Euler(0f,0f,-90f);
            transform.position += Vector3.up * _fallStartOffset;

            _fallVelocity = Vector2.down * 2f;

            UpdateColliders();
        }

        private void TryDamagePlayer(NetworkPlayerState playerState)
        {
            if (!IsServer || !IsSpawned || playerState == null)
                return;

            if (_falling)
                return;

            if (playerState.OwnerClientId == _shooterClientId)
                return;

            if (_hasHitPlayer &&
                _lastHitClientId == playerState.OwnerClientId)
            {
                return;
            }

            _hasHitPlayer = true;
            _lastHitClientId = playerState.OwnerClientId;
            

            StartFalling();
            

            playerState.ApplyDamageServer(
                _damage,
                _shooterClientId
            );
        }

        private bool TryDespawnOutOfBounds()
        {
            Vector3 position = transform.position;

            if (position.y <= _despawnBelowY)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
                        _shooterClientId,
                        out NetworkClient client))
                {
                    NetworkPlayerState playerState =
                        client.PlayerObject.GetComponent<NetworkPlayerState>();

                    if (playerState != null)
                    {
                        IrrigadorCombat combat =
                            playerState.GetComponent<IrrigadorCombat>();

                        if (combat != null)
                            combat.AddNailServer();
                    }
                }

                NetworkObject.Despawn();
                return true;
            }

            return false;
        } 
    }
}

