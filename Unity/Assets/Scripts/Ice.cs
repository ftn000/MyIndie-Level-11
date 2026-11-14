using UnityEngine;

// Лёд тает от большого пламени (size >= 0.5) или от рывка. heatOnly: только от котла (Melt).
public class Ice : Interactable
{
    public bool heatOnly;
    float melt;

    public void Melt() { Fx.Steam(transform.position, 5); Destroy(gameObject); }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (heatOnly || Cheb(p, transform.position) > p.H + 0.8f || !(f.size >= 0.5f || p.Dashing)) return;
        melt += dt;
        if (Random.value < 0.3f) Fx.Steam(transform.position, 1);
        if (melt > 0.6f) { f.Add(-0.03f); Melt(); }
    }
}
