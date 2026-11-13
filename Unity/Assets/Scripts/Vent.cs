using UnityEngine;

// Решётка: пропускает только фитилёк (size < 0.3).
public class Vent : Interactable
{
    public Collider col;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (f.size < 0.3f) { col.enabled = false; return; }
        // не запираем огонёк внутри, если он вырос в проёме
        if (!col.enabled && col.bounds.Intersects(new Bounds(p.transform.position, Vector3.one * p.H * 2))) return;
        col.enabled = true;
    }
}
