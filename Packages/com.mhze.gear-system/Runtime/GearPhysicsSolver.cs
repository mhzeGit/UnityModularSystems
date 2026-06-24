using UnityEngine;

namespace MHZE.GearSystem
{
    public static class GearPhysicsSolver
    {
        public static void Step(float deltaTime, int maxIterations = 10, float relaxation = 1f)
        {
            var gears = Object.FindObjectsOfType<GearConstraint>();
            if (gears == null || gears.Length == 0) return;

            Step(deltaTime, gears, maxIterations, relaxation);
        }

        public static void Step(float deltaTime, GearConstraint[] gears, int maxIterations = 10, float relaxation = 1f)
        {
            if (gears == null || gears.Length == 0) return;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                foreach (var gear in gears)
                {
                    if (gear == null) continue;
                    var deps = gear.dependencies;
                    if (deps == null) continue;

                    foreach (var dep in deps)
                    {
                        if (dep == null || dep == gear) continue;

                        float nA = gear.toothCount;
                        float nB = dep.toothCount;
                        if (nA < 1f || nB < 1f) continue;

                        float omegaA = gear.angularVelocity;
                        float omegaB = dep.angularVelocity;

                        float C = omegaA * nA + omegaB * nB;
                        if (Mathf.Abs(C) < 1e-10f) continue;

                        float invMassA = gear.isDriver ? 0f : 1f;
                        float invMassB = dep.isDriver ? 0f : 1f;
                        float totalInvMass = invMassA * nA * nA + invMassB * nB * nB;
                        if (totalInvMass < 1e-10f) continue;

                        float lambda = C / totalInvMass * relaxation;

                        if (!gear.isDriver)
                            gear.angularVelocity -= lambda * nA * invMassA;
                        if (!dep.isDriver)
                            dep.angularVelocity -= lambda * nB * invMassB;
                    }
                }
            }

            foreach (var gear in gears)
            {
                if (gear == null) continue;
                gear.currentAngle += gear.angularVelocity * deltaTime;
            }
        }
    }
}
