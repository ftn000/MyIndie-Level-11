using UnityEngine;

// Мост лежит скрытым, пока не сгорит верёвка-триггер.
public class Bridge : MonoBehaviour
{
    void Start() { foreach (Transform c in transform) c.gameObject.SetActive(false); }
    void OnEnable() => Rope.Burned += Drop;
    void OnDisable() => Rope.Burned -= Drop;

    void Drop()
    {
        foreach (Transform c in transform) { c.gameObject.SetActive(true); Fx.Steam(c.position, 3); }
    }
}
