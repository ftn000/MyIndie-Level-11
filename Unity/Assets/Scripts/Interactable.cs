using System.Collections.Generic;
using UnityEngine;

// Всё, что реагирует на огонёк. Без физических слоёв и триггеров: FlamePlayer сам опрашивает список.
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new();
    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);
    public abstract void Tick(FlamePlayer p, Flame f, float dt);

    protected static float Cheb(FlamePlayer p, Vector3 c) =>
        Mathf.Max(Mathf.Abs(p.transform.position.x - c.x), Mathf.Abs(p.transform.position.y - c.y));
}
