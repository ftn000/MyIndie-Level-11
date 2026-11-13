using System;
using UnityEngine;

// Верёвка/занавеска: от касания горит, огонь бежит по соседним звеньям. trigger=true: сгорев, роняет мост.
public class Rope : Interactable
{
    public bool trigger;
    public static event Action Burned;
    bool burning, spread;
    float t;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (!burning)
        {
            if (Cheb(p, transform.position) < p.H + 0.75f && f.size > 0.12f) Ignite();
            return;
        }
        t += dt;
        if (UnityEngine.Random.value < 0.5f) Fx.Spark(transform.position, 1, 1.5f);
        if (!spread && t > 0.18f)
        {
            spread = true;
            foreach (var o in All)
                if (o is Rope r && !r.burning && (r.transform.position - transform.position).sqrMagnitude < 1.2f) r.Ignite();
        }
        if (t > 0.6f) { if (trigger) Burned?.Invoke(); Destroy(gameObject); }
    }

    void Ignite()
    {
        if (burning) return;
        burning = true;
        var rend = GetComponent<Renderer>(); if (rend) rend.material.color = new Color(1f, 0.45f, 0.1f);
        var l = new GameObject("Fire").AddComponent<Light>();
        l.transform.SetParent(transform, false); l.type = LightType.Point; l.color = new Color(1, .55f, .2f); l.range = 4; l.intensity = 6;
    }
}
