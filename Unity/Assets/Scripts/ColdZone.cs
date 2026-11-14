using UnityEngine;

// Объём холода (лес): пока центр огонька внутри, расход пламени умножается на mult. Формулу Flame не меняет.
public class ColdZone : Interactable
{
    public float mult = 1.5f;
    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (new Bounds(transform.position, transform.localScale).Contains(p.transform.position)) f.Cold(mult);
    }
}
