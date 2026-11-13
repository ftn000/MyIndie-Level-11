using UnityEngine;

// Чекпоинт и подзаправка. transform.position = верх пола под свечой.
public class Candle : Interactable
{
    public bool lit;
    public Light lightSrc;
    public GameObject flameVis;
    float baseIntensity;

    void Start() { baseIntensity = lightSrc ? lightSrc.intensity : 0; Apply(); }
    void Apply() { if (lightSrc) lightSrc.enabled = lit; if (flameVis) flameVis.SetActive(lit); }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        var c = transform.position + Vector3.up * 0.3f;
        float d = Vector3.Distance(p.transform.position, c);
        if (!lit && d < 0.95f)
        {
            lit = true; Apply();
            GameManager.I.SetCheckpoint(transform.position + Vector3.up * 0.5f);
            f.Add(0.5f); Fx.Spark(c, 24, 4);
        }
        else if (lit && d < 1.65f) f.Warm();
    }

    void Update()
    {
        if (lit && lightSrc) lightSrc.intensity = baseIntensity * (1 + 0.08f * Mathf.Sin(Time.time * 14 + transform.position.x));
    }
}
