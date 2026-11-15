using System;
using UnityEngine;

// Верёвка/занавеска: от касания горит, огонь бежит по соседним звеньям. trigger=true: сгорев, роняет мост.
public class Rope : Interactable
{
    public bool trigger;
    public string id; // trigger-верёвка шлёт Burned(id): мост и люк слушают свой id
    public static event Action<string> Burned;
    bool burning, spread;
    float t;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (!burning)
        {
            if (Cheb(p, transform.position) < p.H + 0.75f && f.fuel > 0.12f) { Ignite(); GameAudio.Play(Snd.RopeBurn, transform.position); } // звук только от звена, которое зажёг игрок, не от соседей
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
        if (t > 0.6f) { if (trigger) Burned?.Invoke(id); Destroy(gameObject); }
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
