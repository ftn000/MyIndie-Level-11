using UnityEngine;

// Вентилятор / окно дует влево циклом 4.6 с: лёгкий ветерок (предупреждение), потом порыв.
// Окно: curtain вздувается за секунду до порыва (телеграф). burn = 0: порыв только толкает.
// Укрытие: любой коллайдер между игроком и источником. Частицы ветра гаснут о те же коллайдеры, поэтому «тень» от камня видна.
// Частицы строятся в коде при старте (материал из Resources/SparkAdd), поэтому старую сцену пересобирать не нужно.
public class Fan : Interactable
{
    public Transform blades, curtain;
    public float burn = 0.25f;   // расход пламени в секунду под порывом
    public float range = 22f;    // на сколько метров влево достаёт порыв
    public float offset;         // сдвиг фазы цикла, с (тесты и разнесение окон)
    ParticleSystem[] wind;
    float[] gustRate;
    Vector3 curtainScale, curtainPos;

    const float Period = 4.6f, WarnAt = 1.9f, GustAt = 2.9f, GustEnd = 4.5f;
    public float Phase => (Time.time + offset) % Period;
    public bool Gusting => Phase > GustAt && Phase < GustEnd;
    public float TimeToGust => Gusting ? 0 : (GustAt - Phase + Period) % Period;

    void Awake()
    {
        Build();
        if (curtain) { curtainScale = curtain.localScale; curtainPos = curtain.localPosition; }
    }

    public void Build()
    {
        if (wind != null) return;
        var mat = Resources.Load<Material>("SparkAdd");
        if (!mat) Debug.LogWarning("Fan: нет Resources/SparkAdd.mat, ветер не будет виден");
        gustRate = new[] { 22f, 90f };
        wind = new[]
        {
            Make("WindMass", mat, 1.3f, 2.6f, new Color(.55f, .72f, 1f, .09f), new Color(.8f, .9f, 1f, .16f), 5f, gustRate[0]),   // широкие мягкие массы воздуха
            Make("WindStreaks", mat, 0.07f, 0.14f, new Color(.8f, .9f, 1f, .45f), new Color(1f, 1f, 1f, .8f), 14f, gustRate[1]), // яркие штрихи поверх
        };
    }

    ParticleSystem Make(string name, Material mat, float sizeMin, float sizeMax, Color c1, Color c2, float lengthScale, float rate)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(transform.position.x, transform.position.y, 0); go.transform.rotation = Quaternion.Euler(0, -90, 0); // forward = -X
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true; m.playOnAwake = true;
        m.startLifetime = 2f; m.startSpeed = 14f; m.maxParticles = 400;
        m.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        m.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
        var e = ps.emission; e.rateOverTime = 0;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3f, 7f, 0.1f); // y 1..8, глубина ±1.5
        var n = ps.noise; n.enabled = true; n.strength = 1.4f; n.frequency = 0.3f; n.scrollSpeed = 0.5f; n.damping = true; n.separateAxes = false;
        var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
        col.dampen = 1f; col.bounce = 0f; col.lifetimeLoss = 1f; col.radiusScale = 0.5f; col.quality = ParticleSystemCollisionQuality.High; col.maxCollisionShapes = 256;
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(1, .65f), new GradientAlphaKey(0, 1) });
        c.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = lengthScale; r.cameraVelocityScale = 0;
        ps.Play();
        return ps;
    }

    // Для скриншотов в редакторе: имитирует уже идущий порыв.
    public void SimulateGust(float seconds)
    {
        Build();
        for (int i = 0; i < wind.Length; i++) { var e = wind[i].emission; e.rateOverTime = gustRate[i]; wind[i].Simulate(seconds, true, true); wind[i].Pause(); }
    }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        float ph = Phase;
        bool gust = Gusting, warn = ph > WarnAt && ph <= GustAt;
        if (blades) blades.Rotate(0, 0, (gust ? 1200 : warn ? 500 : 60) * dt);
        if (curtain)
        {
            float b = gust ? 1 : warn ? Mathf.InverseLerp(WarnAt, GustAt, ph) : 0;
            b *= 1 + 0.12f * Mathf.Sin(Time.time * 11);
            curtain.localScale = new Vector3(curtainScale.x + 0.9f * b, curtainScale.y, curtainScale.z);
            curtain.localPosition = curtainPos + Vector3.left * 0.45f * b;
        }
        for (int i = 0; i < wind.Length; i++)
        {
            var e = wind[i].emission; e.rateOverTime = gustRate[i] * (gust ? 1f : warn ? 0.16f : 0f);
            var m = wind[i].main; m.startSpeed = gust ? 14f : 6f;
        }
        if (!gust) return;
        float fx = transform.position.x; var pp = p.transform.position;
        if (pp.x > fx || pp.x < fx - range) return;
        var o = pp + Vector3.right * p.H;
        float dist = fx - o.x;
        if (dist > 0 && !Physics.Raycast(o, Vector3.right, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            p.windX = -6.25f; if (burn > 0) f.Burn(burn, dt);
        }
    }
}
