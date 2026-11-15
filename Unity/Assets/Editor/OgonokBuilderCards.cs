using UnityEngine;

// P-17: пасхалки по карточкам джема. Лаборатория торта (кухня), легендарная пицца (винный погреб), тюлень (канализация).
// Всё декор из примитивов за плоскостью игры; единственное, что играет, — пицца (Paper с amount = 1).
public static partial class OgonokBuilder
{
    static GameObject Prim(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n; go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.localScale = scale; go.transform.rotation = Quaternion.Euler(euler);
        go.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void BuildCards(Transform level)
    {
        BuildCakeLab(level, 84f, FloorTop(12)); // между декором кухни (шкаф до x 82.2, рамка с 85.4) и правее ящика 72..73: иначе торт в них
        BuildPizza(level, C(214 + 141, 14 + 12) + new Vector3(0, -0.2f, 0)); // правая половина бочки 140..141 у выхода из погреба
        BuildSeal(level, 60.4f,FloorTop(56 + 12));                           // остров между плотами, за плоскостью игры
    }

    static void BuildCakeLab(Transform level, float x, float fy)
    {
        var root = new GameObject("CakeLab").transform; root.SetParent(level, false);
        var cream = Lit("CakeCream", new Color(1f, 0.93f, 0.8f)); var pink = Lit("CakePink", new Color(1f, 0.55f, 0.7f));
        var cherry = Lit("Cherry", new Color(0.8f, 0.05f, 0.1f)); var glass = Lit("Glass", new Color(0.6f, 0.85f, 1f)); glass.SetFloat("_Smoothness", 0.9f);
        float z = 1.8f;
        Prim(PrimitiveType.Cube, "Table", root, new Vector3(x, fy + 0.95f, z), new Vector3(3.6f, 0.15f, 1.2f), mWood);
        foreach (var dx in new[] { -1.65f, 1.65f }) Prim(PrimitiveType.Cube, "Leg", root, new Vector3(x + dx, fy + 0.47f, z), new Vector3(0.15f, 0.95f, 0.9f), mWood);
        Prim(PrimitiveType.Cylinder, "CakeBase", root, new Vector3(x - 0.9f, fy + 1.2f, z), new Vector3(1.1f, 0.18f, 1.1f), pink);
        Prim(PrimitiveType.Cylinder, "CakeMid", root, new Vector3(x - 0.9f, fy + 1.55f, z), new Vector3(0.85f, 0.17f, 0.85f), cream);
        Prim(PrimitiveType.Cylinder, "CakeTop", root, new Vector3(x - 0.9f, fy + 1.88f, z), new Vector3(0.6f, 0.16f, 0.6f), pink);
        Prim(PrimitiveType.Sphere, "Cherry", root, new Vector3(x - 0.9f, fy + 2.1f, z), Vector3.one * 0.22f, cherry);
        var cd = Prim(PrimitiveType.Cylinder, "CandleWax", root, new Vector3(x - 0.9f, fy + 2.3f, z), new Vector3(0.07f, 0.12f, 0.07f), mWax);
        var fl = Sprite("CandleFlame", root, flameSprite, mSparkAdd, 0.45f); fl.transform.position = new Vector3(x - 0.9f, fy + 2.6f, z);
        var lt = new GameObject("CandleLight").AddComponent<Light>(); lt.transform.SetParent(root, false); lt.transform.position = new Vector3(x - 0.9f, fy + 2.6f, z - 0.3f);
        lt.type = LightType.Point; lt.color = new Color(1, .62f, .28f); lt.range = 5; lt.intensity = 2.5f; lt.shadows = LightShadows.None;
        // микроскоп: основание, стойка, труба под углом, окуляр
        Prim(PrimitiveType.Cube, "ScopeBase", root, new Vector3(x + 0.8f, fy + 1.05f, z), new Vector3(0.5f, 0.07f, 0.35f), mBlade);
        Prim(PrimitiveType.Cylinder, "ScopeArm", root, new Vector3(x + 0.95f, fy + 1.4f, z), new Vector3(0.07f, 0.35f, 0.07f), mBlade);
        Prim(PrimitiveType.Cylinder, "ScopeTube", root, new Vector3(x + 0.75f, fy + 1.6f, z), new Vector3(0.12f, 0.3f, 0.12f), mPipe, new Vector3(0, 0, 25));
        // колбы
        foreach (var dx in new[] { 1.4f, 1.7f }) Prim(PrimitiveType.Cylinder, "Flask", root, new Vector3(x + dx, fy + 1.2f, z), new Vector3(0.14f, 0.12f, 0.14f), glass);
    }

    static void BuildPizza(Transform level, Vector3 p)
    {
        var root = new GameObject("LegendaryPizza"); root.transform.SetParent(level, false); root.transform.position = p;
        var dough = Lit("PizzaDough", new Color(1f, 0.72f, 0.2f)); var pep = Lit("Pepperoni", new Color(0.75f, 0.1f, 0.08f));
        Prim(PrimitiveType.Cube, "Slice", root.transform, p + Vector3.up * 0.1f, new Vector3(0.7f, 0.06f, 0.5f), dough, new Vector3(0, 0, 0)); // плоский клин
        Prim(PrimitiveType.Cube, "Crust", root.transform, p + new Vector3(0.33f, 0.1f, 0), new Vector3(0.1f, 0.1f, 0.5f), dough);
        foreach (var o in new[] { new Vector3(-0.1f, 0.15f, 0.1f), new Vector3(0.1f, 0.15f, -0.1f) }) Prim(PrimitiveType.Sphere, "Pepperoni", root.transform, p + o, new Vector3(0.14f, 0.05f, 0.14f), pep);
        var lt = new GameObject("GoldLight").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.position = p + Vector3.up * 0.8f;
        lt.type = LightType.Point; lt.color = new Color(1f, 0.82f, 0.3f); lt.range = 8; lt.intensity = 4; lt.shadows = LightShadows.None;
        var ps = MakePS("GoldSparks", root.transform, mSparkAdd); ps.transform.position = p;
        var m = ps.main; m.startLifetime = 1.6f; m.startSpeed = 0.5f; m.startSize = 0.12f; m.startColor = new Color(1f, 0.85f, 0.3f); m.gravityModifier = -0.05f;
        var e = ps.emission; e.rateOverTime = 2.5f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.7f, 0.1f, 0.1f);
        Fade(ps, new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.6f, 0.1f));
        var pa = root.AddComponent<Paper>(); pa.amount = 1f; pa.legendary = true;
    }

    static void BuildSeal(Transform level, float x, float fy)
    {
        var root = new GameObject("Seal").transform; root.SetParent(level, false);
        var body = Lit("SealFur", new Color(0.75f, 0.82f, 0.92f)); var dark = Lit("SealDark", new Color(0.05f, 0.05f, 0.07f));
        float z = 1f, y = fy + 0.3f; // перед полками декора (z 1.15..1.85), всё ещё за плоскостью игры
        var lt = new GameObject("SealLight").AddComponent<Light>(); lt.transform.SetParent(root, false); lt.transform.position = new Vector3(x + 0.3f, y + 1f, z - 1.5f);
        lt.type = LightType.Point; lt.color = new Color(0.6f, 0.8f, 1f); lt.range = 5; lt.intensity = 3; lt.shadows = LightShadows.None;
        var bob = new GameObject("SealBody").transform; bob.SetParent(root, false); bob.position = new Vector3(x, y, z); bob.gameObject.AddComponent<Bob>();
        Prim(PrimitiveType.Capsule, "Torso", bob, bob.position, new Vector3(0.55f, 0.55f, 0.55f), body, new Vector3(0, 0, 90));
        Prim(PrimitiveType.Sphere, "Head", bob, bob.position + new Vector3(0.62f, 0.12f, 0), Vector3.one * 0.4f, body);
        foreach (var dz in new[] { -0.1f, 0.1f }) Prim(PrimitiveType.Sphere, "Eye", bob, bob.position + new Vector3(0.82f, 0.2f, dz - 0.1f), Vector3.one * 0.06f, dark);
        Prim(PrimitiveType.Sphere, "Nose", bob, bob.position + new Vector3(0.95f, 0.1f, -0.15f), Vector3.one * 0.09f, dark);
        foreach (var dz in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Sphere, "Flipper", bob, bob.position + new Vector3(0.2f, -0.12f, dz), new Vector3(0.35f, 0.08f, 0.18f), body, new Vector3(0, 0, -25));
        Prim(PrimitiveType.Sphere, "Tail", bob, bob.position + new Vector3(-0.75f, -0.05f, 0), new Vector3(0.3f, 0.1f, 0.22f), body);
    }
}
