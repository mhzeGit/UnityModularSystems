using UnityEngine;
using UnityEngine.AI;

namespace MHZE.UltimateDoorSystem
{
    /// <summary>
    /// Keeps the door's NavMeshObstacle on only while the door is open, so the swung-open panel carves
    /// the NavMesh and agents (NPCs) walk around it instead of through it. While the door is closed
    /// (or locked) the obstacle stays off: it would otherwise seal the doorway agents must walk up to
    /// in order to open it.
    /// </summary>
    [AddComponentMenu("MHZE/Ultimate Door System/Door NavMesh Obstacle")]
    [RequireComponent(typeof(UltimateDoorController))]
    [RequireComponent(typeof(NavMeshObstacle))]
    public class DoorNavMeshObstacle : MonoBehaviour
    {
        private UltimateDoorController door;
        private NavMeshObstacle obstacle;

        private void Awake()
        {
            door = GetComponent<UltimateDoorController>();
            obstacle = GetComponent<NavMeshObstacle>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool open = door != null && door.State == UltimateDoorController.DoorState.Open;
            if (obstacle != null && obstacle.enabled != open)
                obstacle.enabled = open;
        }
    }
}
