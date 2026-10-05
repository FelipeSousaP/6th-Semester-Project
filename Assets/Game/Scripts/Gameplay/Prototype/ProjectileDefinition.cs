using UnityEngine;

namespace PiGame.Gameplay
{
    [CreateAssetMenu(fileName = "data_projectile_", menuName = "Pi Game/Combat/Projectile")]
    public class ProjectileDefinition : ScriptableObject
    {
        [SerializeField] private NetworkProjectile _prefab;
        [SerializeField, Min(0)] private int _damage = 1;
        [SerializeField, Min(0f)] private float _speed = 16f;
        [SerializeField, Min(0.01f)] private float _lifetimeSeconds = 3f;

        public NetworkProjectile Prefab => _prefab;
        public int Damage => _damage;
        public float Speed => _speed;
        public float LifetimeSeconds => _lifetimeSeconds;
    }
}
