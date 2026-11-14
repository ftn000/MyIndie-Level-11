using UnityEngine;

// Тёплый поток: объём = transform.localScale вокруг transform.position. Держи прыжок, чтобы подниматься.
// Визуал строится здесь один раз для всех потоков: колонна искр и марева на весь объём (материал из Resources/SparkAdd).
public class Updraft : Interactable
{
    ParticleSystem fx;

    void Start()
    {
        var mat = Resources.Load<Material>("SparkAdd");
        var go = new GameObject("UpdraftFx"); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var s = transform.localScale;
        var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true; m.playOnAwake = true;
        m.scalingMode = ParticleSystemScalingMode.Shape; m.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; // форма берёт размер объёма, частицы не раздуваются
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f); m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.4f); m.maxParticles = 400; m.gravityModifier = 0;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .62f, .25f, .55f), new Color(1f, .85f, .5f, .9f));
        var e = ps.emission; e.rateOverTime = Mathf.Clamp(s.x * s.y * 1.6f, 20f, 160f);
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1, 1, 0.5f / Mathf.Max(0.01f, s.z));
        var vo = ps.velocityOverLifetime; vo.enabled = true; vo.space = ParticleSystemSimulationSpace.World; vo.x = new ParticleSystem.MinMaxCurve(0f, 0f); vo.z = new ParticleSystem.MinMaxCurve(0f, 0f); vo.y = new ParticleSystem.MinMaxCurve(3f, 5f); // вверх, мировая ось
        var n = ps.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 0.4f; n.scrollSpeed = 0.6f;
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
        c.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        ps.Play(); fx = ps;
    }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (new Bounds(transform.position, transform.localScale).Contains(p.transform.position)) p.inUpdraft = true;
    }
}
