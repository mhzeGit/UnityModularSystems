using UnityEngine;

namespace MHZE.AirParticlePhysics
{
    public class AirParticleDebugView
    {
        private const int MaxGridDrawDimension = 50;

        public Color boundsColor = Color.cyan;
        public Color cellGridColor = new Color(0f, 0.7f, 1f, 1f);
        public bool showCellGrid = true;

        public void Draw(AirParticleBoxVolume volume)
        {
            DrawBounds(volume);
            if (showCellGrid)
                DrawGrid(volume);
        }

        public void DrawGizmos(AirParticleBoxVolume volume)
        {
            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(volume.Center, volume.Size);

            if (showCellGrid)
                DrawGizmoGrid(volume);
        }

        private void DrawBounds(AirParticleBoxVolume volume)
        {
            Vector3 min = volume.Min;
            Vector3 max = volume.Max;

            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), boundsColor);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), boundsColor);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), boundsColor);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), boundsColor);

            Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), boundsColor);
            Debug.DrawLine(new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), boundsColor);
            Debug.DrawLine(new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), boundsColor);
            Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), boundsColor);

            Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), boundsColor);
            Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), boundsColor);
            Debug.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), boundsColor);
            Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), boundsColor);
        }

        private void DrawGrid(AirParticleBoxVolume volume)
        {
            Vector3Int dims = volume.GridDimensions;
            if (dims.x > MaxGridDrawDimension || dims.y > MaxGridDrawDimension || dims.z > MaxGridDrawDimension)
                return;
            float cs = volume.CellSize;
            Vector3 o = volume.Min;

            for (int x = 0; x <= dims.x; x++)
            {
                float px = o.x + x * cs;
                for (int y = 0; y <= dims.y; y++)
                {
                    Debug.DrawLine(
                        new Vector3(px, o.y + y * cs, o.z),
                        new Vector3(px, o.y + y * cs, o.z + dims.z * cs),
                        cellGridColor
                    );
                }
            }

            for (int x = 0; x <= dims.x; x++)
            {
                float px = o.x + x * cs;
                for (int z = 0; z <= dims.z; z++)
                {
                    Debug.DrawLine(
                        new Vector3(px, o.y, o.z + z * cs),
                        new Vector3(px, o.y + dims.y * cs, o.z + z * cs),
                        cellGridColor
                    );
                }
            }

            for (int y = 0; y <= dims.y; y++)
            {
                float py = o.y + y * cs;
                for (int z = 0; z <= dims.z; z++)
                {
                    Debug.DrawLine(
                        new Vector3(o.x, py, o.z + z * cs),
                        new Vector3(o.x + dims.x * cs, py, o.z + z * cs),
                        cellGridColor
                    );
                }
            }
        }

        private void DrawGizmoGrid(AirParticleBoxVolume volume)
        {
            Vector3Int dims = volume.GridDimensions;
            if (dims.x > MaxGridDrawDimension || dims.y > MaxGridDrawDimension || dims.z > MaxGridDrawDimension)
                return;
            float cs = volume.CellSize;
            Vector3 o = volume.Min;
            Gizmos.color = cellGridColor;

            for (int x = 0; x <= dims.x; x++)
            {
                float px = o.x + x * cs;
                for (int y = 0; y <= dims.y; y++)
                {
                    Gizmos.DrawLine(
                        new Vector3(px, o.y + y * cs, o.z),
                        new Vector3(px, o.y + y * cs, o.z + dims.z * cs)
                    );
                }
            }

            for (int x = 0; x <= dims.x; x++)
            {
                float px = o.x + x * cs;
                for (int z = 0; z <= dims.z; z++)
                {
                    Gizmos.DrawLine(
                        new Vector3(px, o.y, o.z + z * cs),
                        new Vector3(px, o.y + dims.y * cs, o.z + z * cs)
                    );
                }
            }

            for (int y = 0; y <= dims.y; y++)
            {
                float py = o.y + y * cs;
                for (int z = 0; z <= dims.z; z++)
                {
                    Gizmos.DrawLine(
                        new Vector3(o.x, py, o.z + z * cs),
                        new Vector3(o.x + dims.x * cs, py, o.z + z * cs)
                    );
                }
            }
        }
    }
}
