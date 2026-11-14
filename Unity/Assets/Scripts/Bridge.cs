using UnityEngine;

// Мост лежит скрытым, пока не сгорит верёвка-триггер с тем же id.
public class Bridge : MonoBehaviour
{
    public string id;
    void Start() { foreach (Transform c in transform) c.gameObject.SetActive(false); }
    void OnEnable() => Rope.Burned += Drop;
    void OnDisable() => Rope.Burned -= Drop;

    void Drop(string rope)
    {
        if (rope != id) return;
        foreach (Transform c in transform) { c.gameObject.SetActive(true); Fx.Steam(c.position, 3); }
    }
}
