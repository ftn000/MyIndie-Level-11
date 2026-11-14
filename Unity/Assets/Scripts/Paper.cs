using System.Collections.Generic;
using UnityEngine;

// Подпитка (бумага, газета, пакет, шишка): разовая +30% топлива. Не уничтожается, а выключается: взятое после последнего
// чекпоинта возвращается при респауне (Restore), взятое до него остаётся взятым (Commit на каждом чекпоинте).
public class Paper : Interactable
{
    static readonly List<Paper> taken = new();

    public static void Commit() => taken.Clear();

    public static void Restore()
    {
        foreach (var p in taken) if (p) p.gameObject.SetActive(true);
        taken.Clear();
    }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (Vector3.Distance(p.transform.position, transform.position) > 0.75f) return;
        f.Add(0.3f); Fx.Spark(transform.position, 14, 4);
        taken.Add(this); gameObject.SetActive(false);
    }
}
