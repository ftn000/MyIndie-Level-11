using UnityEngine;

// Вентилятор шахты: две лопасти вращаются в горизонтальной плоскости. Сбоку видна проекция на x:
// отрезок от втулки до cos(angle)·radius. Лопасть в зоне игрока гасит. Когда сгорела верёвка-ремень (Rope.Burned с beltId),
// лопасти доворачиваются в положение «ребром» (проход у стен свободен) и стоят.
public class ShaftFan : Interactable
{
    public float radius = 6f, omega = 2.2f, thickness = 0.5f;
    public string beltId = "fanbelt";
    public Transform[] blades; // визуал, по одной на лопасть
    public float angle;
    public bool Stopped { get; private set; }

    protected override void OnEnable() { base.OnEnable(); Rope.Burned += OnBurned; }
    protected override void OnDisable() { base.OnDisable(); Rope.Burned -= OnBurned; }
    void OnBurned(string id) { if (id == beltId) Stopped = true; }

    public bool Hits(Vector3 pos, float r)
    {
        var c = transform.position;
        if (Mathf.Abs(pos.y - c.y) > thickness / 2 + r) return false;
        for (int k = 0; k < 2; k++)
        {
            float tip = Mathf.Cos(angle + k * Mathf.PI) * radius;
            if (pos.x > c.x + Mathf.Min(0, tip) - r && pos.x < c.x + Mathf.Max(0, tip) + r) return true;
        }
        return false;
    }

    public override void Tick(FlamePlayer p, Flame f, float dt)
    {
        if (!Stopped) angle += omega * dt;
        else angle = Mathf.MoveTowards(angle, Mathf.Round((angle - Mathf.PI / 2) / Mathf.PI) * Mathf.PI + Mathf.PI / 2, 3f * dt);
        if (Hits(p.transform.position, p.H)) f.Kill();
    }

    void LateUpdate()
    {
        for (int k = 0; blades != null && k < blades.Length; k++)
        {
            float a = angle + k * Mathf.PI;
            blades[k].position = transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius / 2;
            blades[k].rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            blades[k].localScale = new Vector3(radius, thickness, 0.6f);
        }
    }
}
