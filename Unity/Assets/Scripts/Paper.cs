using UnityEngine;

// Сухая бумага: разовая подпитка +30%.
public class Paper : Interactable
{
    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (Vector3.Distance(p.transform.position, transform.position) > 0.75f) return;
        f.Add(0.3f); Fx.Spark(transform.position, 14, 4); Destroy(gameObject);
    }
}
