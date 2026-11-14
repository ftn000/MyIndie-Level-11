using UnityEngine;

// Пузырь метана: всплывает, касание = вспышка: подпитка +0.2 и толчок вверх. Если размер > 0.6, толчок сильнее и вбок (в сторону от центра пузыря).
public class Methane : Interactable
{
    public float rise = 1.2f, maxRise = 7f;
    float startY = float.NaN;

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (float.IsNaN(startY)) startY = transform.position.y;
        transform.position += Vector3.up * rise * dt;
        if (transform.position.y - startY > maxRise) { Destroy(gameObject); return; }
        if (Vector3.Distance(p.transform.position, transform.position) > 0.75f) return;
        bool big = f.size > 0.6f;
        f.Add(0.2f);
        if (big) p.Kick(17f, (p.transform.position.x >= transform.position.x ? 1 : -1) * 7.5f);
        else p.Kick(13f);
        Fx.Spark(transform.position, 20, 5); Destroy(gameObject);
    }
}
