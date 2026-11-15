#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// P-13: текстуры грейбокса. UV в метрах мира (BoxUV) и то, что текстура не тронула геометрию Level1.
public class TextureTests
{
    // Куб 4×1×2 с центром (10, 5, 0): фронт (−z) покрывает x 8..12, y 4.5..5.5; верх (+y) — x 8..12, z −1..1.
    [Test] public void BoxUVIsInWorldMeters()
    {
        var m = BoxUV.Make(new Vector3(4, 1, 2), new Vector3(10, 5, 0));
        var v = m.vertices; var n = m.normals; var uv = m.uv;
        var front = Enumerable.Range(0, v.Length).Where(i => n[i] == Vector3.back).Select(i => uv[i]).ToArray();
        var top = Enumerable.Range(0, v.Length).Where(i => n[i] == Vector3.up).Select(i => uv[i]).ToArray();
        Assert.AreEqual(4, front.Length); Assert.AreEqual(4, top.Length);
        Assert.AreEqual(8f, front.Min(u => u.x), 1e-4); Assert.AreEqual(12f, front.Max(u => u.x), 1e-4);
        Assert.AreEqual(4.5f, front.Min(u => u.y), 1e-4); Assert.AreEqual(5.5f, front.Max(u => u.y), 1e-4);
        Assert.AreEqual(-1f, top.Min(u => u.y), 1e-4); Assert.AreEqual(1f, top.Max(u => u.y), 1e-4);
    }

    // Все треугольники смотрят наружу: иначе грань пропадёт (отсечение задних граней).
    [Test] public void BoxUVFacesOutward()
    {
        var m = BoxUV.Make(Vector3.one, Vector3.zero); var v = m.vertices; var t = m.triangles;
        Assert.AreEqual(36, t.Length);
        for (int i = 0; i < t.Length; i += 3)
        {
            var c = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3;
            Assert.Greater(Vector3.Dot(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]), c), 0, $"треугольник {i / 3}");
        }
    }

    // В собранной Level1: текстурные кубы получили меш с UV в метрах, коллайдер остался единичным кубом.
    [UnityTest] public IEnumerator Level1TexturedCubesKeepColliders()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level1.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var floors = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Floor").ToArray();
        Assert.Greater(floors.Length, 10, "доски пола дома собраны");
        foreach (var r in floors)
        {
            Assert.IsTrue(r.sharedMaterial.GetTexture("_BaseMap"), r.name);
            Assert.AreNotEqual("Cube", r.GetComponent<MeshFilter>().sharedMesh.name, "меш с UV в метрах, не встроенный куб");
            var bc = r.GetComponent<BoxCollider>();
            Assert.AreEqual(Vector3.one, bc.size); Assert.AreEqual(Vector3.zero, bc.center);
        }
    }
}
#endif
