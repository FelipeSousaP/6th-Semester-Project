using UnityEngine;

namespace PiGame.Gameplay
{
    public class IrrigadorTipDetector : MonoBehaviour
    {
        private LayerMask _surfaceLayer;
        
        private IrrigadorProjectile _projectile;

        private void Awake()
        {
            _projectile = GetComponentInParent<IrrigadorProjectile>();
            if(_projectile != null)
            {
               _surfaceLayer = _projectile.SurfaceLayer; 
            }
            
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(!IsSurface(other))
                return;

            _projectile.HandleTipSurfaceContact(other);
        }
        private bool IsSurface(Collider2D other)
        {
            return(_surfaceLayer.value & (1 << other.gameObject.layer)) != 0;
        }
    }

}
