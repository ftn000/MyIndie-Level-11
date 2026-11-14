using UnityEngine;

// Капля раз в 2.2 с. Игра ждёт «Play», чтобы тайминг не уходил на титульном экране.
public class DripSpawner : MonoBehaviour
{
    public GameObject dropPrefab;
    public float phase;
    float ph;

    public float TimeToDrop => 2.2f - ph;

    void Start() => ph = phase;

    void Update()
    {
        if (GameManager.I == null || !GameManager.I.Playing) return;
        ph += Time.deltaTime;
        if (ph >= 2.2f) { ph -= 2.2f; Instantiate(dropPrefab, transform.position + Vector3.down * 0.3f, Quaternion.identity); }
    }
}
