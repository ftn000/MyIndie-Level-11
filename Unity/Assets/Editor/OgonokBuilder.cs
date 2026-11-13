using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Собирает сцену Level1 из того же уровня, что в веб-прототипе (index.html, build()).
// Меню: Ogonok/Build Level 1. Запускается и из batchmode: -executeMethod OgonokBuilder.Build
public static class OgonokBuilder
{
    const int W = 136, H = 17;
    const float Depth = 4f;
    static char[,] g;
    static Material mStone, mWall, mIce, mRope, mWax, mPaper, mWater, mGrate, mWood, mFire, mSparkAdd, mSteam, mDrop;
    static Sprite flameSprite, dotSprite;
    static GameObject dropPrefab;

    [MenuItem("Ogonok/Build Level 1")]
    public static void Build()
    {
        foreach (var d in new[] { "Materials", "Art", "Prefabs", "Scenes" }) Directory.CreateDirectory(Path.Combine(Application.dataPath, d));
        MakeAssets();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.022f, 0.035f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.04f); RenderSettings.fogDensity = 0.012f;

        LayoutGrid();
        var level = new GameObject("Level").transform;
        BuildTiles(level);
        BuildDecor(level);
        var player = BuildPlayer();
        BuildCamera(player);
        BuildVolume();
        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level1.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Level1.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Ogonok: Level1 built");
    }

    // ---------- уровень (порт build() из index.html; ty вниз, как в прототипе) ----------
    static void Box(int a, int b, int c, int d, char ch) { for (int y = b; y <= d; y++) for (int x = a; x <= c; x++) g[y, x] = ch; }

    static void LayoutGrid()
    {
        g = new char[H, W];
        Box(0, 0, W - 1, H - 1, '.');
        Box(0, 0, W - 1, 0, '#'); Box(0, 16, W - 1, 16, '#'); Box(0, 0, 0, 16, '#'); Box(W - 1, 0, W - 1, 16, '#');
        Box(8, 14, 9, 15, '#'); Box(12, 12, 13, 15, '#'); g[11, 12] = 'p'; g[15, 19] = 'p';
        g[1, 31] = 'd'; g[1, 36] = 'd'; g[1, 41] = 'd';
        foreach (var x in new[] { 33, 34, 38, 39, 44, 45, 46 }) g[15, x] = '~';
        g[15, 41] = 'p';
        Box(54, 1, 54, 15, 'I');
        Box(62, 1, 62, 14, '#'); g[15, 62] = 'v';
        Box(74, 14, 75, 15, '#'); Box(79, 14, 80, 15, '#'); Box(84, 14, 85, 15, '#'); g[1, 89] = 'F';
        for (int x = 93; x <= 110; x++) g[16, x] = '.';
        foreach (var x in new[] { 94, 95, 96, 101, 102, 103 }) for (int y = 1; y <= 16; y++) g[y, x] = 'U';
        Box(98, 12, 99, 12, '#'); Box(106, 11, 107, 11, '#'); Box(109, 14, 110, 14, '#');
        Box(115, 1, 115, 15, 'R'); Box(120, 6, 120, 13, 'T');
        for (int x = 122; x <= 127; x++) g[16, x] = 'b';
        g[15, 132] = 'G';
        foreach (var (x, y) in new[] { (3, 15), (26, 15), (50, 15), (58, 15), (66, 15), (91, 15), (113, 15), (117, 15) }) g[y, x] = 'c';
    }

    static Vector3 C(int tx, int ty, float z = 0) => new(tx + 0.5f, H - 1 - ty + 0.5f, z);
    static float FloorTop(int ty) => H - 1 - ty; // верх тайла, лежащего в строке ty

    static GameObject Cube(string n, Transform parent, Vector3 center, Vector3 scale, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n; go.transform.SetParent(parent, false);
        go.transform.position = center; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void BuildTiles(Transform level)
    {
        var dripPhase = new System.Collections.Generic.Dictionary<int, float> { { 31, 0f }, { 36, 0.7f }, { 41, 1.4f } };
        for (int ty = 0; ty < H; ty++)
            for (int tx = 0; tx < W; tx++)
            {
                char c = g[ty, tx];
                if (c == '#')
                {
                    int e = tx; while (e + 1 < W && g[ty, e + 1] == '#') e++;
                    int len = e - tx + 1;
                    Cube("Stone", level, new Vector3(tx + len / 2f, H - 1 - ty + 0.5f, 0), new Vector3(len, 1, Depth), mStone);
                    tx = e; continue;
                }
                var p = C(tx, ty);
                switch (c)
                {
                    case 'I': Cube("Ice", level, p, new Vector3(1, 1, 2), mIce).AddComponent<Ice>(); break;
                    case 'R': Cube("Curtain", level, p, new Vector3(0.8f, 1, 2), mRope).AddComponent<Rope>(); break;
                    case 'T': Cube("Rope", level, p, new Vector3(0.2f, 1, 0.2f), mRope, false).AddComponent<Rope>().trigger = true; break;
                    case 'v':
                        var v = Cube("Vent", level, p, new Vector3(1, 1, Depth), mGrate);
                        v.AddComponent<Vent>().col = v.GetComponent<Collider>(); break;
                    case 'p':
                        var pa = Cube("Paper", level, p + Vector3.down * 0.1f, new Vector3(0.35f, 0.45f, 0.05f), mPaper, false);
                        pa.transform.rotation = Quaternion.Euler(0, 0, -12); pa.AddComponent<Paper>(); break;
                    case '~':
                        var pu = new GameObject("Puddle"); pu.transform.SetParent(level, false); pu.transform.position = new Vector3(tx + 0.5f, FloorTop(ty), 0);
                        pu.AddComponent<Puddle>();
                        Cube("Water", pu.transform, pu.transform.position + Vector3.up * 0.15f, new Vector3(1, 0.3f, 1.6f), mWater, false); break;
                    case 'd':
                        var dr = Cube("Drip", level, p, new Vector3(0.35f, 0.4f, 0.35f), mGrate, false);
                        var sp = dr.AddComponent<DripSpawner>(); sp.dropPrefab = dropPrefab; sp.phase = dripPhase[tx]; break;
                    case 'F': BuildFan(level, p); break;
                    case 'c': BuildCandle(level, tx, ty); break;
                    case 'G': BuildGoal(level, tx, ty); break;
                }
            }
        // тёплые потоки: по одной зоне на группу соседних колонок
        for (int tx = 0; tx < W; tx++)
        {
            if (g[1, tx] != 'U') continue;
            int e = tx; while (e + 1 < W && g[1, e + 1] == 'U') e++;
            BuildUpdraft(level, tx, e - tx + 1); tx = e;
        }
        // мост
        var br = new GameObject("Bridge"); br.transform.SetParent(level, false); br.AddComponent<Bridge>();
        for (int tx = 0; tx < W; tx++) if (g[16, tx] == 'b') Cube("Plank", br.transform, C(tx, 16), new Vector3(1, 1, Depth), mWood);
        Cube("BackWall", level, new Vector3(W / 2f, H / 2f, Depth / 2f + 0.5f), new Vector3(W, H, 1), mWall, false);
    }

    static void BuildCandle(Transform level, int tx, int ty)
    {
        var root = new GameObject("Candle"); root.transform.SetParent(level, false);
        root.transform.position = new Vector3(tx + 0.5f, FloorTop(ty), 0);
        var wax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wax.name = "Wax"; Object.DestroyImmediate(wax.GetComponent<Collider>());
        wax.transform.SetParent(root.transform, false); wax.transform.localPosition = Vector3.up * 0.2f; wax.transform.localScale = new Vector3(0.22f, 0.2f, 0.22f);
        wax.GetComponent<Renderer>().sharedMaterial = mWax;
        var fl = Sprite("Flame", root.transform, flameSprite, mSparkAdd, 0.55f); fl.transform.localPosition = Vector3.up * 0.62f;
        var lt = new GameObject("Light").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.localPosition = Vector3.up * 0.7f;
        lt.type = LightType.Point; lt.color = new Color(1, .62f, .28f); lt.range = 7; lt.intensity = 5; lt.shadows = LightShadows.None;
        var cd = root.AddComponent<Candle>(); cd.lightSrc = lt; cd.flameVis = fl; cd.lit = tx == 3;
    }

    static GameObject Sprite(string n, Transform parent, Sprite s, Material m, float scale)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sharedMaterial = m;
        go.AddComponent<Billboard>();
        return go;
    }

    static void BuildFan(Transform level, Vector3 p)
    {
        var root = new GameObject("Fan"); root.transform.SetParent(level, false); root.transform.position = p;
        Cube("Mount", root.transform, p + Vector3.up * 0.4f, new Vector3(0.3f, 0.3f, 0.3f), mGrate, false);
        var bl = new GameObject("Blades").transform; bl.SetParent(root.transform, false); bl.position = p - Vector3.up * 0.2f;
        Cube("B1", bl, bl.position, new Vector3(1.6f, 0.12f, 0.12f), mGrate, false);
        Cube("B2", bl, bl.position, new Vector3(0.12f, 1.6f, 0.12f), mGrate, false);
        var fan = root.AddComponent<Fan>(); fan.blades = bl; // частицы ветра Fan строит сам в Awake
    }

    static void BuildUpdraft(Transform level, int tx, int len)
    {
        var zone = new GameObject("Updraft"); zone.transform.SetParent(level, false);
        zone.transform.position = new Vector3(tx + len / 2f, 8f, 0); zone.transform.localScale = new Vector3(len, 16, Depth);
        zone.AddComponent<Updraft>();
        var ps = MakePS("Embers", level, mSparkAdd);
        ps.transform.position = new Vector3(tx + len / 2f, 0.3f, 0); ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        var m = ps.main; m.startLifetime = 5f; m.startSpeed = 3.2f; m.startSize = 0.09f; m.startColor = new Color(1, .7f, .3f, .7f);
        var e = ps.emission; e.rateOverTime = 7 * len;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(len, 2f, 0.1f);
        Fade(ps, new Color(1, .75f, .35f), new Color(1, .4f, .1f));
    }

    static void BuildGoal(Transform level, int tx, int ty)
    {
        var root = new GameObject("Fireplace"); root.transform.SetParent(level, false);
        root.transform.position = C(tx, ty);
        root.AddComponent<Goal>();
        float z = 1.4f;
        Cube("Left", root.transform, new Vector3(130.5f, 3.5f, z), new Vector3(1, 4, 1), mFire, false);
        Cube("Right", root.transform, new Vector3(134.5f - 0.0f, 3.5f, z), new Vector3(1, 4, 1), mFire, false);
        Cube("Top", root.transform, new Vector3(132.5f, 5.5f, z), new Vector3(5, 1, 1), mFire, false);
        Cube("Log", root.transform, new Vector3(132.5f, 1.2f, 0.4f), new Vector3(2.2f, 0.3f, 0.5f), mWood, false);
        var big = Sprite("BigFlame", root.transform, flameSprite, mSparkAdd, 3.2f); big.transform.position = new Vector3(132.5f, 2.6f, 0.2f);
        var lt = new GameObject("Light").AddComponent<Light>(); lt.transform.SetParent(root.transform, false); lt.transform.position = new Vector3(132.5f, 2.6f, -1f);
        lt.type = LightType.Point; lt.color = new Color(1, .6f, .25f); lt.range = 18; lt.intensity = 22; lt.shadows = LightShadows.None;
    }

    static void BuildDecor(Transform level)
    {
        // Цветной мебельный декор за плоскостью игры: оживает рядом с огнём.
        var cols = new[] { "#6b3f2a", "#3f6b52", "#7a5a2a", "#4a3f7a", "#7a3a4a" };
        var mats = new Material[cols.Length];
        for (int i = 0; i < cols.Length; i++) { ColorUtility.TryParseHtmlString(cols[i], out var c); mats[i] = Lit("Deco" + i, c); }
        uint s = 7; float R() { s = (uint)((ulong)s * 16807 % 2147483647); return s / 2147483647f; }
        for (float x = 3; x < W - 3; x += 2f + R() * 3.4f)
        {
            float k = R(); var m = mats[Mathf.FloorToInt(R() * mats.Length)];
            if (k < 0.55f) { float h = 2.8f + R() * 2.2f, w = 1.8f + R(); Cube("Shelf", level, new Vector3(x, 1 + h / 2, 1.5f), new Vector3(w, h, 0.7f), m, false); }
            else { Cube("Frame", level, new Vector3(x, 4f + R() * 4f, 2.0f), new Vector3(1f, 1.4f, 0.2f), m, false); }
        }
    }

    // ---------- игрок, камера, пост-обработка ----------
    static FlamePlayer BuildPlayer()
    {
        var root = new GameObject("Player"); root.transform.position = new Vector3(3.5f, 1.6f, 0);
        var f = root.AddComponent<Flame>(); var fp = root.AddComponent<FlamePlayer>(); fp.flame = f;
        var vis = Sprite("FlameSprite", root.transform, flameSprite, mSparkAdd, 1f); f.visual = vis.transform;
        var lt = new GameObject("FlameLight").AddComponent<Light>(); lt.transform.SetParent(root.transform, false);
        lt.type = LightType.Point; lt.color = new Color(1, .6f, .25f); lt.shadows = LightShadows.Soft; lt.shadowStrength = 1f; lt.shadowBias = 0.05f;
        f.flameLight = lt;
        var sp = MakePS("Sparks", root.transform, mSparkAdd);
        var m = sp.main; m.startLifetime = 1.3f; m.startSpeed = 0.5f; m.startSize = 0.07f; m.gravityModifier = 0.12f; m.startColor = new Color(1, .8f, .35f, 1);
        var sh = sp.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        Fade(sp, new Color(1, .85f, .4f), new Color(1, .35f, .05f));
        f.sparks = sp;
        var st = MakePS("Steam", root.transform, mSteam);
        var sm = st.main; sm.startLifetime = 1.1f; sm.startSpeed = 0.4f; sm.startSize = 0.3f; sm.startColor = new Color(.8f, .83f, .9f, .35f);
        var se = st.emission; se.rateOverTime = 0;
        var sz = st.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
        Fade(st, new Color(.8f, .83f, .9f, .35f), new Color(.8f, .83f, .9f, 0));
        f.steam = st;
        return fp;
    }

    static ParticleSystem MakePS(string n, Transform parent, Material mat)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true; m.playOnAwake = true; m.maxParticles = 600;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    static void Fade(ParticleSystem ps, Color a, Color b)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(0, 1) });
        col.color = gr;
    }

    static void BuildCamera(FlamePlayer target)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.025f, 0.04f);
        cam.fieldOfView = 40; cam.nearClipPlane = 0.3f; cam.farClipPlane = 120;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        var cf = go.AddComponent<CamFollow>(); cf.target = target;
        go.transform.position = new Vector3(8, 4.6f, -13); go.transform.rotation = Quaternion.Euler(11, 0, 0);
    }

    static void BuildVolume()
    {
        const string path = "Assets/Settings/OgonokVolume.asset";
        AssetDatabase.DeleteAsset(path);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, path);
        T Add<T>() where T : VolumeComponent { var c = prof.Add<T>(true); AssetDatabase.AddObjectToAsset(c, prof); return c; }
        var bloom = Add<Bloom>(); bloom.threshold.Override(0.8f); bloom.intensity.Override(1.1f); bloom.scatter.Override(0.65f);
        var vig = Add<Vignette>(); vig.intensity.Override(0.4f); vig.smoothness.Override(0.5f);
        var tm = Add<Tonemapping>(); tm.mode.Override(TonemappingMode.Neutral);
        var ca = Add<ColorAdjustments>(); ca.contrast.Override(12f); ca.saturation.Override(8f);
        EditorUtility.SetDirty(prof);
        var go = new GameObject("PostFX"); var v = go.AddComponent<Volume>(); v.isGlobal = true; v.sharedProfile = prof;
    }

    // ---------- плейсхолдерные ассеты ----------
    static Material Lit(string name, Color c)
    {
        var path = $"Assets/Materials/{name}.mat"; AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material Shaded(string name, string shader, Sprite tex, Color tint)
    {
        var path = $"Assets/Materials/{name}.mat"; AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find(shader)); m.mainTexture = tex.texture; m.color = tint;
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Sprite MakeSprite(string name, int n, System.Func<float, float, Color> f)
    {
        var path = $"Assets/Art/{name}.png";
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, f((x + 0.5f) / n * 2 - 1, (y + 0.5f) / n * 2 - 1));
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = n; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void MakeAssets()
    {
        // Плейсхолдер пламени: капля. Заменить на рисованный спрайт от команды (Assets/Art/FlamePlaceholder.png).
        flameSprite = MakeSprite("FlamePlaceholder", 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u * (1 + Mathf.Max(0, v) * 1.6f) + (v < 0 ? v * v * 1.3f : v * v * 0.5f));
            float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
            return new Color(Mathf.Lerp(1, 1, a), Mathf.Lerp(0.42f, 0.97f, a * a), Mathf.Lerp(0.08f, 0.75f, a * a), a);
        });
        dotSprite = MakeSprite("Dot", 64, (u, v) => { float a = Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v)); return new Color(1, 1, 1, a * a); });
        mSparkAdd = Shaded("SparkAdd", "Mobile/Particles/Additive", dotSprite, Color.white);
        AssetDatabase.DeleteAsset("Assets/Resources/SparkAdd.mat"); Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources")); AssetDatabase.CopyAsset("Assets/Materials/SparkAdd.mat", "Assets/Resources/SparkAdd.mat"); // Fan грузит через Resources
        mSteam = Shaded("Steam", "Mobile/Particles/Alpha Blended", dotSprite, Color.white);
        mStone = Lit("Stone", new Color(0.30f, 0.32f, 0.40f));
        mWall = Lit("Wall", new Color(0.09f, 0.1f, 0.15f));
        mIce = Lit("Ice", new Color(0.55f, 0.78f, 0.92f)); mIce.SetFloat("_Smoothness", 0.8f);
        mRope = Lit("Rope", new Color(0.62f, 0.45f, 0.28f));
        mWax = Lit("Wax", new Color(0.92f, 0.89f, 0.8f));
        mPaper = Lit("Paper", new Color(0.85f, 0.81f, 0.68f));
        mWater = Lit("Water", new Color(0.27f, 0.5f, 0.8f)); mWater.SetFloat("_Smoothness", 0.9f);
        mGrate = Lit("Grate", new Color(0.18f, 0.19f, 0.26f));
        mWood = Lit("Wood", new Color(0.54f, 0.4f, 0.26f));
        mFire = Lit("Fireplace", new Color(0.29f, 0.25f, 0.22f));
        mDrop = Lit("Drop", new Color(0.44f, 0.69f, 0.91f));

        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(drop.GetComponent<Collider>());
        drop.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f); drop.GetComponent<Renderer>().sharedMaterial = mDrop;
        drop.AddComponent<Drop>();
        dropPrefab = PrefabUtility.SaveAsPrefabAsset(drop, "Assets/Prefabs/Drop.prefab");
        Object.DestroyImmediate(drop);
    }
}
