using NUnit.Framework;
using UnityEngine;

namespace MHZE.WallBreak.Tests
{
    public class BreakableWallTests
    {
        GameObject go;

        [TearDown] public void TearDown() { if (go != null) Object.DestroyImmediate(go); }

        BreakableWall MakeWall(Vector3 scale)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = scale;
            return go.AddComponent<BreakableWall>();
        }

        [Test]
        public void StrikeCountsAndBreaks()
        {
            BreakableWall wall = MakeWall(new Vector3(4f, 3f, 0.3f));
            int broken = 0;
            wall.Broken += p => broken++;
            for (int i = 0; i < wall.HitsToBreak - 1; i++)
                Assert.IsTrue(wall.Strike(go.transform.position, Vector3.back));
            Assert.IsFalse(wall.IsBroken);
            wall.Strike(go.transform.position, Vector3.back);
            Assert.IsTrue(wall.IsBroken);
            Assert.AreEqual(1, broken);
        }

        [Test]
        public void BreakingLeavesARemainderAndTakesTheHoleOut()
        {
            BreakableWall wall = MakeWall(new Vector3(4f, 3f, 0.3f));
            wall.BreakNow(go.transform.position, Vector3.back);
            Transform rem = go.transform.Find(go.name + "_Remainder");
            Assert.IsNotNull(rem, "remainder object");
            Mesh m = rem.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(m.vertexCount, 24, "remainder was cut into more geometry than a plain cube");
            Assert.IsFalse(go.GetComponent<MeshRenderer>().enabled);
            // Nothing of the remainder may cover the middle of the hole at standing height.
            Bounds hole = new Bounds(go.transform.TransformPoint(new Vector3(0f, -0.2f, 0f)), new Vector3(0.6f, 1.2f, 1f));
            var tris = m.triangles; var v = m.vertices;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 c = go.transform.TransformPoint((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                Assert.IsFalse(hole.Contains(c), "a remainder triangle sits inside the hole");
            }
        }

        [Test]
        public void ResetPutsTheWallBack()
        {
            BreakableWall wall = MakeWall(new Vector3(4f, 3f, 0.3f));
            wall.BreakNow(go.transform.position, Vector3.back);
            wall.ResetWall();
            Assert.IsFalse(wall.IsBroken);
            Assert.AreEqual(0, wall.HitsTaken);
            Assert.IsTrue(go.GetComponent<MeshRenderer>().enabled);
            Assert.IsTrue(go.GetComponent<Collider>().enabled);
        }
    }
}
