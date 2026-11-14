using UnityEngine;

// Камера 2.5D: фиксированный лёгкий наклон, плавное следование по XY.
public class CamFollow : MonoBehaviour
{
    public FlamePlayer target;
    public Vector3 offset = new(0, 3.4f, -13f);
    public float minX = 8f, maxX = 128f;

    void LateUpdate()
    {
        if (!target) return;
        var t = target.transform.position;
        var want = new Vector3(Mathf.Clamp(t.x + target.face * 1.5f, minX, maxX), t.y, 0) + offset;
        transform.position = Vector3.Lerp(transform.position, want, 1 - Mathf.Exp(-5f * Time.deltaTime));
        transform.rotation = Quaternion.Euler(11, 0, 0);
    }
}
