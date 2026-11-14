using UnityEngine;

// Решётка: пропускает только фитилёк (форма маленькая).
public class Vent : Interactable
{
    public Collider col;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (f.IsSmall) { col.enabled = false; return; }
        // не запираем огонёк внутри, если он вырос в проёме
        if (!col.enabled && Overlaps(new Bounds(p.transform.position, Vector3.one * p.H * 2))) return;
        col.enabled = true;
    }

    // Bounds выключенного коллайдера пустые, поэтому считаем по трансформу (решётка = единичный куб).
    public bool Overlaps(Bounds b) => new Bounds(transform.position, transform.lossyScale).Intersects(b);
}
