#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// P-12: гостиная в сцене Level1Art (Ogonok → Build Level 1 (Art Preview)). Каждый прыжок маршрута проверяется перебором точки
// отрыва и длины зажатия Пробела в обычной форме, без W и Shift. Каждый перебор стартует с покоя на площадке: детерминированно.
public class Room1Tests
{
    const float Dt = 1f / 60f, F = 81f;
    FlamePlayer p;

    [UnitySetUp]
    public IEnumerator Load()
    {
        Time.captureFramerate = 60;
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level1Art.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        p = Object.FindFirstObjectByType<FlamePlayer>();
        GameManager.I.state = GameManager.State.Play; p.manual = true; GameManager.I.mapRects = null;
    }

    // (x0, x1, верх над полом); мир.x = 1 + клетки
    readonly struct Pl { public readonly string n; public readonly float a, b, top; public Pl(string n, float a, float b, float h) { this.n = n; this.a = a; this.b = b; top = F + h; } }
    static readonly Pl Floor = new("floor", 1.5f, 29f, 0), CrateL = new("crateL", 6.4f, 8.0f, 1.1f), CrateS = new("crateS", 6.9f, 7.7f, 1.5f),
        Back = new("chairBack", 7.8f, 8.8f, 3.2f), Seat = new("seat", 8.8f, 10.9f, 1.4f),
        B1 = new("bc1", 11.6f, 14.0f, 1.7f), B2 = new("bc2", 12.8f, 15.1f, 2.8f), B3 = new("bc3", 11.6f, 14.0f, 4.0f), B4 = new("bc4", 12.8f, 15.1f, 5.1f),
        TopR = new("bcTopR", 14.5f, 15.5f, 6.8f), Clock = new("clock", 15.5f, 17.5f, 6.6f), Dresser = new("dresser", 17.7f, 23.0f, 3.0f),
        Stand = new("nightstand", 23.3f, 25.3f, 2.1f), FloorR = new("floorDoorway", 25.8f, 29f, 0);

    void Place(float x, float top) { p.transform.position = new Vector3(x, top + p.H + 0.02f, 0); p.flame.fuel = 1; p.flame.SetForm(Flame.Form.Normal); Physics.SyncTransforms(); }

    // true, если с какой-то точки площадки src есть прыжок на dst
    bool Hop(Pl src, Pl dst, bool needHole = false)
    {
        for (float x = src.a + 0.2f; x <= src.b - 0.2f; x += 0.2f)
        {
            float dir = (dst.a + dst.b) / 2 > x ? 1 : -1;
            for (int hold = 0; hold <= 36; hold++)
            {
                Place(x, src.top);
                for (int i = 0; i < 4; i++) p.MoveInput(Dt, 0, false, false, false, 0);
                for (int i = 0; i < 160; i++)
                {
                    p.MoveInput(Dt, i < 120 ? (int)dir : 0, i < hold, i == 0 && hold > 0, false, 0);
                    var pos = p.transform.position;
                    if (p.flame.Dead) break;
                    if (p.Grounded && pos.x >= dst.a && pos.x <= dst.b && Mathf.Abs(pos.y - p.H - dst.top) < 0.1f) return true;
                    if (hold > 0 && p.Grounded && i > 8) break; // приземлился не туда, следующая попытка
                }
            }
        }
        return false;
    }

    [UnityTest] public IEnumerator RouteFromFireplaceToDoorwayHasEveryJump()
    {
        foreach (var f in Object.FindObjectsByType<Fan>(FindObjectsSortMode.None)) f.enabled = false; // порыв зависит от времени, прыжки считаем без него
        var route = new[] { (Floor, CrateL), (CrateL, CrateS), (CrateS, Back), (Back, Seat), (Seat, B1), (B1, B2), (B2, B3), (B3, B4), (B4, TopR), (TopR, Clock), (Clock, Dresser), (Dresser, Stand), (Stand, FloorR) };
        foreach (var (a, b) in route) Assert.IsTrue(Hop(a, b), $"прыжок {a.n} -> {b.n} невозможен в обычной форме");
        yield return null;
    }

    [UnityTest] public IEnumerator WindShieldedByClockAndDresserButNotOnTop()
    {
        var fan = GameObject.Find("WindowFanRoom1").GetComponent<Fan>();
        void Gust() => fan.offset = ((3.2f - Time.time) % 4.6f + 4.6f) % 4.6f;
        Gust(); Place(15.0f, F); p.MoveInput(Dt, 0, false, false, false, 0);
        Assert.AreEqual(0f, p.windX, "за часами не дует");
        Gust(); Place(20f, Dresser.top); p.MoveInput(Dt, 0, false, false, false, 0);
        Assert.Less(p.windX, 0f, "на комоде порыв толкает");
        Gust(); Place(12f, F); p.MoveInput(Dt, 0, false, false, false, 0);
        Assert.AreEqual(0f, p.windX, "за шкафом и часами не дует");
        float fuel = p.flame.fuel; Gust(); Place(20f, Dresser.top); yield return null;
        for (int i = 0; i < 30; i++) p.MoveInput(Dt, 0, false, false, false, 0);
        Assert.IsFalse(p.flame.Dead, "первый порыв только толкает");
    }

    [UnityTest] public IEnumerator CandleOnShelfIsCheckpointAndCobwebBlocksShelfPath()
    {
        var candle = Object.FindObjectsByType<Candle>(FindObjectsSortMode.None).Single(c => c.transform.position.x < 27 && c.transform.position.y > F);
        Assert.AreEqual(F + 4.5f, candle.transform.position.y, 0.01f, "свеча стоит на верхней грани полки");
        p.transform.position = candle.transform.position + Vector3.up * 0.5f; Physics.SyncTransforms();
        p.MoveInput(Dt, 0, false, false, false, 0);
        Assert.IsTrue(candle.lit); Assert.Greater(GameManager.I.Checkpoint.y, F + 4f, "чекпоинт на полке");
        var web = Object.FindFirstObjectByType<Cobweb>(); Assert.IsNotNull(web);
        float fuel = 1f; p.flame.fuel = fuel; p.transform.position = web.transform.position + Vector3.left * 1.2f; Physics.SyncTransforms();
        for (int i = 0; i < 120 && web; i++) p.MoveInput(Dt, 1, false, false, false, 0);
        yield return null; // Destroy срабатывает в конце кадра
        Assert.IsFalse(web, "паутина сгорела при касании");
    }

    [UnityTest] public IEnumerator FirstZoneStillStartsInFireplace()
    {
        Assert.AreEqual(3.5f, GameManager.I.zoneStart[0].x, 0.01f);
        Assert.IsTrue(Object.FindObjectsByType<Paper>(FindObjectsSortMode.None).Count(x => x.transform.position.x < 27 && x.transform.position.y > F) == 4, "четыре подпитки в гостиной");
        yield return null;
    }
}
#endif
