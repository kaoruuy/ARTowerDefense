using UnityEngine;
using UnityEngine.UI;

namespace ARTD
{
    /// <summary>A small, resolution-independent command banner with slanted sides.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlantedPanel : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float slant = Mathf.Min(18f, r.width * .08f);
            var a = new Vector2(r.xMin, r.yMin);
            var b = new Vector2(r.xMin + slant, r.yMax);
            var c = new Vector2(r.xMax, r.yMax);
            var d = new Vector2(r.xMax - slant, r.yMin);
            Quad(mesh, a, b, c, d, color);
            Color border = new Color32(182, 143, 80, 255);
            Edge(mesh, a, b, border); Edge(mesh, b, c, border);
            Edge(mesh, c, d, border); Edge(mesh, d, a, border);
        }

        static void Edge(VertexHelper mesh, Vector2 a, Vector2 b, Color color)
        {
            Vector2 delta = b - a;
            Vector2 inset = new Vector2(delta.y, -delta.x).normalized * 1.5f;
            Quad(mesh, a, b, b + inset, a + inset, color);
        }

        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero); mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
