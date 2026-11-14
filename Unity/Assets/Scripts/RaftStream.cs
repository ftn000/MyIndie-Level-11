using UnityEngine;

// Исток канала: плоты (доска, ящик, пенопласт) всплывают у одного края воды и плывут к другому с интервалом interval.
// Канал = вода от xa до xb на высоте top. dir = +1 течение вправо (по ходу игрока), -1 влево. Плотов в полёте несколько:
// шаг speed * interval 4.2 м при ширинах 1.2..2 м. При старте канал уже заполнен (prewarm), пена на воде плывёт по течению.
public class RaftStream : Interactable
{
    public float xa, xb, top, dir = 1, speed = 1.5f, interval = 2.8f, phase;
    public Material raftMat;
    float timer;
    int n;
    static Material[] mats;

    // имя, ширина, высота, цвет
    static readonly (string name, float w, float h, Color c)[] Kinds =
    {
        ("RaftPlank", 2f, 0.3f, new Color(0.36f, 0.30f, 0.24f)), ("RaftCrate", 1.4f, 0.5f, new Color(0.45f, 0.34f, 0.2f)),
        ("RaftFoam", 1.2f, 0.35f, new Color(0.72f, 0.76f, 0.78f)), ("RaftPlank", 1.7f, 0.3f, new Color(0.3f, 0.26f, 0.22f)),
    };

    void Start()
    {
        float step = speed * interval;
        float len = xb - xa;
        for (float d = phase * step; d < len - 2.5f; d += step) Spawn(d);
        timer = (phase * step) / speed;
        BuildFoam();
    }

    // dist: сколько уже проплыл от истока (prewarm)
    void Spawn(float dist)
    {
        var k = Kinds[n++ % Kinds.Length];
        mats ??= new Material[Kinds.Length];
        int ki = (n - 1) % Kinds.Length;
        if (!mats[ki] && raftMat) { mats[ki] = new Material(raftMat) { color = k.c }; }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = k.name; go.transform.SetParent(transform.parent, false);
        go.transform.localScale = new Vector3(k.w, k.h, 3f);
        float x0 = dir > 0 ? xa + k.w / 2 : xb - k.w / 2, end = dir > 0 ? xb - k.w / 2 : xa + k.w / 2;
        go.transform.position = new Vector3(x0 + dir * dist, top - k.h / 2, 0);
        if (mats[ki]) go.GetComponent<Renderer>().sharedMaterial = mats[ki];
        var r = go.AddComponent<Raft>();
        r.Init(dir, speed, end, dist > 0 ? Raft.RiseTime : 0);
    }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        timer += dt;
        if (timer < interval) return;
        timer -= interval;
        Spawn(0);
    }

    // Пена и мусор на воде: частицы плывут от истока к стоку со скоростью течения.
    void BuildFoam()
    {
        var mat = Resources.Load<Material>("SparkAdd");
        var go = new GameObject("Foam"); go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(dir > 0 ? xa : xb, top - 0.03f, 0); go.transform.rotation = Quaternion.Euler(0, dir > 0 ? 90 : -90, 0); // forward = направление течения
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        float life = (xb - xa) / speed;
        var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true; m.playOnAwake = true;
        m.startLifetime = life; m.startSpeed = speed; m.maxParticles = 300; m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.7f, 0.7f, 0.5f), new Color(0.85f, 0.9f, 0.9f, 0.8f));
        var e = ps.emission; e.rateOverTime = 5f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.8f, 0.02f, 0.02f); // поперёк канала по z (1.8 м вокруг плоскости игры)
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(1, .85f), new GradientAlphaKey(0, 1) });
        c.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        ps.Simulate(life, true, true); ps.Play(); // канал уже течёт
    }
}
