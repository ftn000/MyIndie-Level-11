using UnityEngine;

// Лёд тает от большой формы или от рывка. heatOnly: только от котла (Melt).
// thin: тонкий лёд проваливается сразу при касании (не плавится 0.6 с), держит только маленькую форму.
public class Ice : Interactable
{
    public bool heatOnly, thin;
    float melt;

    public void Melt() { Fx.Steam(transform.position, 5); Destroy(gameObject); }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (heatOnly || Cheb(p, transform.position) > p.H + (thin ? 0.55f : 0.8f) || !(thin ? !f.IsSmall : f.IsBig) && !p.Dashing) return;
        melt += dt;
        if (Random.value < 0.3f) Fx.Steam(transform.position, 1);
        if (melt > (thin ? 0f : 0.6f)) { f.Add(-0.03f); Melt(); }
    }
}
