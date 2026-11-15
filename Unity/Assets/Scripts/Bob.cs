using UnityEngine;

// P-17: лёгкое покачивание декора (тюлень).
public class Bob : MonoBehaviour
{
    float y0;
    void Start() => y0 = transform.position.y;
    void Update() => transform.position = new Vector3(transform.position.x, y0 + Mathf.Sin(Time.time * 1.6f) * 0.03f, transform.position.z);
}
