using UnityEngine;

// Выход метана из воды: пузырь раз в period секунд. Как DripSpawner, ждёт «Play».
public class MethaneVent : MonoBehaviour
{
    public GameObject bubblePrefab;
    public float period = 2f, phase;
    float ph;

    void Start() => ph = phase;

    void Update()
    {
        if (GameManager.I == null || !GameManager.I.Playing) return;
        ph += Time.deltaTime;
        if (ph >= period) { ph -= period; Instantiate(bubblePrefab, transform.position, Quaternion.identity); }
    }
}
