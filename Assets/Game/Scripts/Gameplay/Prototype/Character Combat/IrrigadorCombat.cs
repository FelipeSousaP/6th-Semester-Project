using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PiGame.Gameplay
{
    [RequireComponent(typeof(NetworkPlayerState))]
    public class IrrigadorCombat : NetworkBehaviour, ICharacterCombat
    {
        [Header("Combat Settings")]
        [SerializeField] private ProjectileDefinition _projectile;
        [SerializeField, Min(0f)] private float _shotCooldownSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _projectileSpawnDistance = 0.85f;
        [SerializeField, Min(1f)] private int _maxNails = 4;
        [SerializeField] private AmmoIndicatorsView _nailIndicators;
        private NetworkVariable<int> _nails = new NetworkVariable<int>(4);
        public int Nails => _nails.Value;
        
        [Header("Magnet Settings")]
        [SerializeField, Min(0f)] private float _magnetRadius = 8f;

        [SerializeField, Range(0f, 1f)] private float _magnetAimThreshold = 0.5f;

        [SerializeField, Min(0f)] private float _magnetSpeed = 20f;

        [SerializeField, Min(0f)] private float _magnetStopDistance = 0.25f;
        private bool _magnetBlockedUntilRelease;

        [Header("Ability Settings")]
        [SerializeField, Min(0f)] private float _abilityRadius = 8f;
        [SerializeField, Range(0f, 1f)] private float _abilityAimThreshold = 0.5f;
        [SerializeField, Min(0f)] private float _abilityPullSpeed = 25f;

        private readonly List<IrrigadorProjectile> _abilityTargets = new();
        private bool _abilityActive;

        private IrrigadorProjectile _magnetTarget;
        private bool _magnetActive;

        private NetworkPlayerState _playerState;
        private Rigidbody2D _rigidbody;
        private float _nextShotTime;

        private void Awake()
        {
            _playerState = GetComponent<NetworkPlayerState>();
            _rigidbody = GetComponent<Rigidbody2D>();
            if (_projectile == null || _projectile.Prefab == null)
            {
                Debug.LogError("Configure uma definição com prefab de projétil no BasicCharacterCombat.", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            _nails.OnValueChanged += HandleNailsChanged;
            _playerState.StateChanged += RefreshNailIndicators;
            RefreshNailIndicators();

            if(!IsServer)
                return;
            
            _nails.Value = _maxNails;
        }

        public override void OnNetworkDespawn()
        {
            _nails.OnValueChanged -= HandleNailsChanged;
            _playerState.StateChanged -= RefreshNailIndicators;
            _nailIndicators.SetCount(0, false);
        }

        private void HandleNailsChanged(int previousValue, int currentValue)
        {
            RefreshNailIndicators();
        }

        private void RefreshNailIndicators()
        {
            _nailIndicators.SetCount(_nails.Value, IsOwner && _playerState.CanAct);
        }

        public void ShootServer(Vector2 direction)
        {
            if (!IsServer || !_playerState.CanAct)
                return;

            if(_nails.Value <= 0)
                return;

            if(_projectile == null || _projectile.Prefab == null || Time.time < _nextShotTime)
                return;
            
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

            if(_nails.Value <= 0)
            {
                _magnetBlockedUntilRelease = true;
                StopMagnetServer();
            }
        }

        public void AddNailServer()
        {
            if(!IsServer)
                return;
            
            _nails.Value = Mathf.Min(_nails.Value + 1, _maxNails);
        }

        private IrrigadorProjectile FindMagnetTarget(Vector2 aimDirection)
        {
            IrrigadorProjectile bestTarget = null;
            float bestAlignment = _magnetAimThreshold;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _magnetRadius);

            foreach(Collider2D hit in hits)
            {
                IrrigadorProjectile nail = hit.GetComponentInParent<IrrigadorProjectile>();

                if(nail == null)
                    continue;
                
                if(!nail.IsStuck)
                    continue;
                
                if(nail.ShooterClientId != OwnerClientId)
                    continue;
                
                Vector2 toNail = ((Vector2)nail.transform.position - (Vector2)transform.position).normalized;

                float alignment = Vector2.Dot(aimDirection.normalized, toNail);

                if(alignment < bestAlignment)
                    continue;
                
                bestAlignment = alignment;
                bestTarget = nail;
            }

            return bestTarget;
        }
        
        private void TryActiveMagnetServer(Vector2 aimDirection)
        {
            IrrigadorProjectile target = FindMagnetTarget(aimDirection);

            if(target == null)
            {
                StopMagnetServer();
                return;
            }

            _magnetTarget = target;
            _magnetActive = true;
        }
        private void StopMagnetServer()
        {
            _magnetTarget = null;
            _magnetActive = false;
        }

        public void ShootReleaseServer()
        {
           if(!IsServer)
            return;

            _magnetBlockedUntilRelease = false;
            StopMagnetServer(); 
        }

        public void UpdateShootAimServer(Vector2 direction)
        {
            if(!IsServer)
                return;

            if(_nails.Value > 0 || _magnetBlockedUntilRelease)
            {
                if (_magnetActive)
                    StopMagnetServer();
                return;
            }

            TryActiveMagnetServer(direction);
        }
        private void FixedUpdate()
        {
            if(!IsServer)
                return;
            
            UpdateMagnetServer();
            UpdateAbilityServer();
        }

        private void UpdateMagnetServer()
        {
            if (!_magnetActive || _magnetTarget == null)
                return;

            Vector2 toTarget =
                (Vector2)_magnetTarget.transform.position - _rigidbody.position;

            float distance = toTarget.magnitude;

            if (distance <= _magnetStopDistance)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                StopMagnetServer();
                return;
            }

            _rigidbody.linearVelocity =
                toTarget.normalized * _magnetSpeed;
        }

        private List<IrrigadorProjectile> FindAbilityTargets(Vector2 aimDirection)
        {
            List<IrrigadorProjectile> targets = new();

            Collider2D[] hits = Physics2D.OverlapCircleAll (transform.position, _abilityRadius);

            Vector2 normalizedAim = aimDirection.normalized;

            foreach(Collider2D hit in hits)
            {
                IrrigadorProjectile nail = hit.GetComponentInParent<IrrigadorProjectile>();

                if(nail == null)
                    continue;

                if(!nail.IsStuck)
                    continue;
                if(nail.ShooterClientId != OwnerClientId)
                    continue;
                    
                Vector2 toNail = ((Vector2)nail.transform.position - (Vector2)transform.position).normalized;

                float alignment = Vector2.Dot(normalizedAim,toNail);

                if(alignment < _abilityAimThreshold)
                    continue;
                
                targets.Add(nail);
            }

            return targets;
        }

        public void BeginAbilityServer(Vector2 aimDirection, Vector2 moveDirection)
        {
            if(!IsServer || !_playerState.CanAct)
                return;
            
            if(_nails.Value > _maxNails - 2)
                return;
            
            _abilityTargets.Clear();

            List<IrrigadorProjectile> targets = FindAbilityTargets(aimDirection);

            if(targets.Count == 0)
                return;

            _abilityTargets.AddRange(targets);
            _abilityActive = true;
        }

        private void UpdateAbilityServer()
        {
            if(!_abilityActive)
                return;
            
            for(int i = _abilityTargets.Count - 1; i >=0; i--)
            {
                IrrigadorProjectile nail = _abilityTargets[i];

                if(nail == null || !nail.IsSpawned || !nail.IsStuck)
                {
                    _abilityTargets.RemoveAt(i);
                    continue;
                }

                Vector2 toPlayer = (Vector2)transform.position - (Vector2)nail.transform.position;
                
                if(toPlayer.sqrMagnitude <= 0.01f)
                    continue;
                
                nail.PullServer(toPlayer.normalized, _abilityPullSpeed,true);
            }

            if(_abilityTargets.Count == 0)
            {
                _abilityActive = false;
            }
        }

        public void EndAbilityServer(Vector2 aimDirection, Vector2 moveDirection)
        {
            if(!IsServer)
                return;

            foreach(IrrigadorProjectile nail in _abilityTargets)
            {
                if(nail != null)
                {
                    nail.StopPullServer();
                }
            }

            _abilityTargets.Clear();
            _abilityActive = false;
        }


    }
}

