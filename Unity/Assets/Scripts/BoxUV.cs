using UnityEngine;

// P-13: меш куба с UV в метрах мира, чтобы рисунок текстуры был одного размера на кубах любой длины
// и продолжался через соседние кубы. Плотность задаёт материал (tiling = 1 / период в метрах).
// Грань ±z: (x, y), ±y: (x, z), ±x: (z, y). Вершины единичного куба: размер задаёт transform.localScale.
public static class BoxUV
{
    // origin: мировой центр куба (для плотов и прочего движущегося: Vector3.zero, рисунок едет вместе с объектом).
    public static Mesh Make(Vector3 size, Vector3 origin)
    {
        var v = new Vector3[24]; var n = new Vector3[24]; var uv = new Vector2[24]; var t = new int[36];
        int k = 0, ti = 0;
        for (int axis = 0; axis < 3; axis++)
            for (int s = -1; s <= 1; s += 2)
            {
                var nrm = Vector3.zero; nrm[axis] = s;
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                for (int i = 0; i < 4; i++)
                {
                    var p = Vector3.zero; p[axis] = 0.5f * s; p[a1] = (i & 1) == 0 ? -0.5f : 0.5f; p[a2] = (i & 2) == 0 ? -0.5f : 0.5f;
                    var w = origin + Vector3.Scale(p, size);
                    v[k + i] = p; n[k + i] = nrm;
                    uv[k + i] = axis == 2 ? new Vector2(w.x, w.y) : axis == 1 ? new Vector2(w.x, w.z) : new Vector2(w.z, w.y);
                }
                // Unity: лицевая сторона треугольника (a, b, c) смотрит по Cross(b - a, c - a)
                bool flip = Vector3.Dot(Vector3.Cross(v[k + 1] - v[k], v[k + 2] - v[k]), nrm) < 0;
                int[] q = flip ? new[] { 0, 2, 1, 1, 2, 3 } : new[] { 0, 1, 2, 2, 1, 3 };
                foreach (var j in q) t[ti++] = k + j;
                k += 4;
            }
        var m = new Mesh { name = $"Box {size.x:0.##}x{size.y:0.##}x{size.z:0.##}", vertices = v, normals = n, uv = uv, triangles = t };
        m.RecalculateBounds();
        return m;
    }
}
