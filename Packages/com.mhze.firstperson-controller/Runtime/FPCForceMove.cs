// Thin wrapper around FPCMovement's force-move feature. Provides a clean public API to start moving toward a position at a given speed, stop, and be notified on arrival via a callback. Tracks active state for external consumers to query.

using System;
using UnityEngine;

namespace MHZE.FirstPersonController
{
    /// <summary>
    /// Smooth force-move towards a target position.
    /// Uses FPCMovement's ForceMoveTowards internally.
    /// </summary>
    public class FPCForceMove
    {
        private readonly FPCMovement movement;
        private readonly FPCSettings settings;

        public bool IsActive { get; private set; }

        private Vector3? targetPosition;
        private float moveSpeedOverride;
        private Action arrivedCallback;

        public FPCForceMove(FPCMovement movementModule, FPCSettings settings)
        {
            this.movement = movementModule;
            this.settings = settings;
        }

        public void MoveTo(Vector3 position, float? speed = null, Action onArrived = null)
        {
            targetPosition = position;
            moveSpeedOverride = speed ?? settings.moveSpeed;
            arrivedCallback = onArrived;
            IsActive = true;

            movement.ForceMoveTowards(position, moveSpeedOverride, OnForceMoveArrived);
        }

        public void Stop()
        {
            IsActive = false;
            targetPosition = null;
            arrivedCallback = null;
            movement.StopForceMove();
        }

        public void Update(float deltaTime)
        {
            // FPCMovement handles the actual movement; this wrapper
            // exists for state tracking and the clean public API.
            // Could add easing/spring logic here in the future.
        }

        private void OnForceMoveArrived()
        {
            IsActive = false;
            targetPosition = null;

            var cb = arrivedCallback;
            arrivedCallback = null;
            cb?.Invoke();
        }
    }
}