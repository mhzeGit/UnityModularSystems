using UnityEngine;

namespace MHZE.WallBreak
{
    /// <summary>
    /// Rides on one fallen piece of a broken wall. Puffs dust and thuds on its first hard landing, stops blocking the player
    /// once it has come to rest (unless the wall asks for solid rubble), and shrinks away when its time is up.
    /// </summary>
    public sealed class WallDebris : MonoBehaviour
    {
        internal BreakableWall owner;
        internal float lifetime = 45f;

        private Rigidbody body;
        private Collider shape;
        private Vector3 baseScale;
        private float age, restTime;
        private bool settled, puffed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            shape = GetComponent<Collider>();
            baseScale = transform.localScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (!settled && body != null && owner != null && !owner.SolidRubble)
            {
                restTime = body.IsSleeping() ? restTime + Time.deltaTime : 0f;
                if (restTime > 0.6f || age > 10f) Settle();
            }
            if (age > lifetime)
            {
                float k = 1f - (age - lifetime);
                if (k <= 0f) { Destroy(gameObject); return; }
                transform.localScale = baseScale * k;
            }
        }

        /// <summary>The piece stays where it lies but can no longer be bumped into.</summary>
        private void Settle()
        {
            settled = true;
            if (body != null) body.isKinematic = true;
            if (shape != null) shape.isTrigger = true;
        }

        private void OnCollisionEnter(Collision c)
        {
            if (puffed || owner == null) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.4f || c.contactCount == 0) return;
            puffed = true;
            ContactPoint p = c.GetContact(0);
            owner.NotePieceImpact(p.point, p.normal, speed);
        }
    }
}
