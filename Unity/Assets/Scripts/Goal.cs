using UnityEngine;

public class Goal : Interactable
{
    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (Vector3.Distance(p.transform.position, transform.position) < 1.9f) GameManager.I.Win();
    }
}
