using UnityEngine;

[DefaultExecutionOrder(100)] // после IgniVisual: он дописывает roll
public class Billboard : MonoBehaviour
{
    public float roll; // наклон в плоскости экрана, градусы (пламя гнётся от ветра)
    Camera cam;
    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, roll);
    }
}
