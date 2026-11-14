using UnityEngine;

// Камера 2.5D: фиксированный лёгкий наклон, плавное следование по XY.
public class CamFollow : MonoBehaviour
{
    public FlamePlayer target;
    public Vector3 offset = new(0, 3.4f, -13f);
    public float minX = 8f, maxX = 128f;
    public static CamFollow I;
    Vector3 estFocus;
    float estT;

    void Awake() => I = this;
    public bool Establishing => estT > 0;
    // Общий план: камера отъезжает к focus на seconds секунд, затем плавно возвращается к игроку.
    public void Establish(Vector3 focus, float seconds) { estFocus = focus; estT = seconds; }

    void LateUpdate()
    {
        if (!target) return;
        var t = target.transform.position;
        var want = new Vector3(Mathf.Clamp(t.x + target.face * 1.5f, minX, maxX), t.y, 0) + offset;
        if (estT > 0) { estT -= Time.deltaTime; want = estFocus + new Vector3(0, 0, -34f); }
        transform.position = Vector3.Lerp(transform.position, want, 1 - Mathf.Exp(-5f * Time.deltaTime));
        transform.rotation = Quaternion.Euler(11, 0, 0);
    }
}
