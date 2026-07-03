using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleBoxVolume : MonoBehaviour
    {
        [SerializeField] private Vector3 _size = Vector3.one;
        [SerializeField] private float _cellDensity = 3f;

        public Vector3 Size
        {
            get => _size;
            set => _size = value;
        }

        public float CellDensity
        {
            get => _cellDensity;
            set => _cellDensity = Mathf.Max(0.01f, value);
        }

        public float CellSize => 1f / _cellDensity;

        public Vector3 Center => transform.position;
        public Vector3 Min => Center - _size * 0.5f;
        public Vector3 Max => Center + _size * 0.5f;
        public Bounds Bounds => new Bounds(Center, _size);

        public Vector3Int GridDimensions => new Vector3Int(
            Mathf.Max(1, Mathf.FloorToInt(_size.x / CellSize)),
            Mathf.Max(1, Mathf.FloorToInt(_size.y / CellSize)),
            Mathf.Max(1, Mathf.FloorToInt(_size.z / CellSize))
        );

        public int CellCount
        {
            get
            {
                Vector3Int dims = GridDimensions;
                return dims.x * dims.y * dims.z;
            }
        }

        public Vector3Int GetCellIndexAtPoint(Vector3 worldPoint)
        {
            Vector3 local = worldPoint - Min;
            Vector3 cellSize = Vector3.one * CellSize;
            Vector3Int dims = GridDimensions;

            return new Vector3Int(
                Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize.x), 0, dims.x - 1),
                Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize.y), 0, dims.y - 1),
                Mathf.Clamp(Mathf.FloorToInt(local.z / cellSize.z), 0, dims.z - 1)
            );
        }

        public Bounds GetCellBounds(int x, int y, int z)
        {
            Vector3 cellSize = Vector3.one * CellSize;
            Vector3 origin = Min + new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
            return new Bounds(origin + cellSize * 0.5f, cellSize);
        }

        public Bounds GetCellBounds(Vector3Int index)
        {
            return GetCellBounds(index.x, index.y, index.z);
        }

        public Bounds GetCellBoundsAtPoint(Vector3 worldPoint)
        {
            Vector3Int index = GetCellIndexAtPoint(worldPoint);
            return GetCellBounds(index);
        }

        public Vector3 RandomPoint()
        {
            Vector3 half = _size * 0.5f;
            return Center + new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z)
            );
        }

        public Vector3 RandomPointInCell(int x, int y, int z)
        {
            Bounds cell = GetCellBounds(x, y, z);
            return new Vector3(
                Random.Range(cell.min.x, cell.max.x),
                Random.Range(cell.min.y, cell.max.y),
                Random.Range(cell.min.z, cell.max.z)
            );
        }

        public bool ContainsPoint(Vector3 point)
        {
            Vector3 half = _size * 0.5f;
            Vector3 local = point - Center;
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.y) <= half.y
                && Mathf.Abs(local.z) <= half.z;
        }

        public Vector3 ClampPoint(Vector3 point)
        {
            Vector3 half = _size * 0.5f;
            Vector3 local = point - Center;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return Center + local;
        }

        private void OnValidate()
        {
            _cellDensity = Mathf.Clamp(_cellDensity, 0.1f, 20f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireCube(Center, _size);
        }
    }
}
