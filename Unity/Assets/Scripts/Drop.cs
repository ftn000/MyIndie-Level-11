using UnityEngine;

public class Drop : MonoBehaviour
{
    float vy;

    void Update()
    {
        float dt = Time.deltaTime;
        vy += 25f * dt;
        transform.position += Vector3.down * vy * dt;
        var gm = GameManager.I; var p = gm ? gm.player : null;
        if (p && !p.flame.Dead && Mathf.Abs(transform.position.x - p.transform.position.x) < p.H + 0.1f
              && Mathf.Abs(transform.position.y - p.transform.position.y) < p.H + 0.15f)
        {
            p.flame.Add(-0.18f); Fx.Steam(transform.position, 6); Destroy(gameObject);
        }
        else if (transform.position.y < -5f || Physics.CheckSphere(transform.position, 0.1f, ~0, QueryTriggerInteraction.Ignore))
            Destroy(gameObject);
    }
}
