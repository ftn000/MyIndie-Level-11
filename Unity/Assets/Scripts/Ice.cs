using UnityEngine;

// Лёд тает от большого пламени (size >= 0.5) или от рывка.
public class Ice : Interactable
{
    float melt;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (Cheb(p, transform.position) > p.H + 0.8f || !(f.size >= 0.5f || p.Dashing)) return;
        melt += dt;
        if (Random.value < 0.3f) Fx.Steam(transform.position, 1);
        if (melt > 0.6f) { f.Add(-0.03f); Fx.Steam(transform.position, 5); Destroy(gameObject); }
    }
}
