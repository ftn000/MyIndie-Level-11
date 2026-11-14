using UnityEngine;

// Остывший котёл (рядом GiveFlame). После «отдать пламя»: трубы теплеют, лёд на сливе тает, перечисленные капели замолкают.
[RequireComponent(typeof(GiveFlame))]
public class Boiler : MonoBehaviour
{
    public Ice[] ice;
    public DripSpawner[] drips;
    public Renderer[] pipes;
    public Light glow;
    public bool Lit { get; private set; }

    void Awake() => GetComponent<GiveFlame>().Given += Ignite;

    void Ignite()
    {
        Lit = true;
        foreach (var i in ice) if (i) i.Melt();
        foreach (var d in drips) d.enabled = false;
        foreach (var r in pipes) r.material.color = new Color(0.9f, 0.4f, 0.12f);
        if (glow) glow.enabled = true;
    }
}
