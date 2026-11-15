using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Звук проверяем по счётчикам запусков GameAudio (не по isPlaying: в batchmode без устройства он ненадёжен).
public class AudioTests
{
    const float Dt = 1f / 60f;
    FlamePlayer p; GameManager gm; GameAudio au;

    [UnitySetUp]
    public IEnumerator Load()
    {
        Time.captureFramerate = 60;
        yield return SceneManager.LoadSceneAsync("Level1");
        yield return null;
        p = Object.FindFirstObjectByType<FlamePlayer>(); au = GameAudio.I;
        gm = GameManager.I; gm.state = GameManager.State.Play; p.manual = true;
        yield return null;
    }

    [TearDown] public void Reset() => p.giveHeld = false;

    void Tp(float x, float y, float fuel, Flame.Form form = Flame.Form.Normal) { p.transform.position = new Vector3(x, y, 0); p.flame.fuel = fuel; p.flame.SetForm(form); Physics.SyncTransforms(); }
    void Frame(int jump = 0, bool dash = false, int form = 0) => p.MoveInput(Dt, 0, jump > 1, jump > 0, dash, form);
    IEnumerator Settle() { for (int i = 0; i < 20; i++) { Frame(); yield return null; } Assert.IsTrue(p.Grounded, "стоит на полу"); }

    [Test] public void SceneHasAudioAndOneListener()
    {
        Assert.IsNotNull(au);
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
        Assert.IsTrue(au.slots.All(s => s != null && s.clips.Length > 0), "у каждого события есть клип");
    }

    [UnityTest] public IEnumerator JumpAndDashSoundOnlyWhenAccepted()
    {
        Tp(5f, 82f, 0.8f); yield return Settle();
        int j = au.Count(Snd.Jump), d = au.Count(Snd.Dash);
        Frame(jump: 2); Assert.AreEqual(j + 1, au.Count(Snd.Jump), "реальный толчок");
        for (int i = 0; i < 12; i++) { Frame(jump: 2); yield return null; } // в воздухе: coyote кончился, буфер без толчка
        Assert.AreEqual(j + 1, au.Count(Snd.Jump), "прыжок в воздухе молчит");
        Frame(dash: true); Assert.AreEqual(d + 1, au.Count(Snd.Dash), "принятый рывок");
        Frame(dash: true); Assert.AreEqual(d + 1, au.Count(Snd.Dash), "рывок на перезарядке молчит");
    }

    [UnityTest] public IEnumerator TransformSoundsOnFormChangeNotOnRespawn()
    {
        Tp(5f, 82f, 0.8f); yield return Settle();
        int big = au.Count(Snd.TransformBig);
        Frame(form: 1); Frame(form: 1); // второй шаг на краю: формы не меняет
        Assert.AreEqual(big + 1, au.Count(Snd.TransformBig));
        int n = au.Count(Snd.TransformNormal);
        p.flame.Kill(); gm.Respawn(); // Revive ставит обычную форму через SetForm: без звука превращения
        Assert.AreEqual(n, au.Count(Snd.TransformNormal));
    }

    [UnityTest] public IEnumerator OneDeathOneSoundSurrenderNone()
    {
        Tp(5f, 82f, 0.8f); yield return Settle();
        int d = au.Count(Snd.Death);
        p.flame.Kill(); p.flame.Kill();
        Assert.AreEqual(d + 1, au.Count(Snd.Death));
        gm.Respawn(); p.flame.Surrender();
        Assert.AreEqual(d + 1, au.Count(Snd.Death), "Surrender не смерть");
    }

    [UnityTest] public IEnumerator RespawnsDoNotStackFireLoops()
    {
        Tp(5f, 82f, 0.8f); yield return Settle();
        int srcs = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length, starts = au.FireStarts;
        Assert.AreEqual(1, starts, "петля огня запущена один раз");
        for (int i = 0; i < 3; i++)
        {
            p.flame.Kill();
            yield return new WaitForSeconds(0.5f); // петля затихла и остановилась
            gm.Respawn(); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
        }
        Assert.AreEqual(srcs, Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length, "источники не копятся");
        Assert.AreEqual(starts + 3, au.FireStarts, "по одному перезапуску на респаун");
        Assert.Greater(au.CrackleVolume, 0.1f);
    }

    [UnityTest] public IEnumerator HissFollowsFuelNotSize()
    {
        Tp(5f, 82f, 0.9f, Flame.Form.Small); yield return Settle();
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(0f, au.HissVolume, "маленькая форма при полном топливе не умирает");
        int s = au.FireStarts;
        for (int i = 0; i < 90; i++) { p.flame.fuel = i % 2 == 0 ? 0.24f : 0.26f; Frame(); yield return null; } // дрожь у порога
        Assert.LessOrEqual(au.FireStarts, s + 1, "петля шипения не перезапускается у порога");
        p.flame.fuel = 0.05f; yield return new WaitForSeconds(0.7f);
        Assert.Greater(au.HissVolume, 0.2f);
    }

    [UnityTest] public IEnumerator CandleSoundsOnce()
    {
        var c = Object.FindObjectsByType<Candle>(FindObjectsSortMode.None).Where(x => !x.lit && x.transform.position.y > 80f).OrderBy(x => x.transform.position.x).First();
        int n = au.Count(Snd.Candle);
        Tp(c.transform.position.x, c.transform.position.y + 0.5f, 0.5f);
        for (int i = 0; i < 10; i++) { Frame(); yield return null; }
        Assert.IsTrue(c.lit);
        Assert.AreEqual(n + 1, au.Count(Snd.Candle));
    }

    [UnityTest] public IEnumerator BackgroundFollowsZone()
    {
        gm.JumpToZone(4); yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(4, au.Zone); Assert.AreEqual("shaft_air_loop", au.AmbientClip.name); Assert.Greater(au.AmbientVolume, 0.3f);
        gm.JumpToZone(5); yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(5, au.Zone); Assert.AreEqual("forest_winter_loop", au.AmbientClip.name);
        gm.JumpToZone(2); yield return new WaitForSeconds(1.5f);
        Assert.AreEqual("house_cold_loop", au.AmbientClip.name); Assert.AreEqual(au.ambVolume * 0.5f, au.AmbientVolume, 0.02f, "подвал тише дома");
        Assert.AreEqual(1, Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.isPlaying && s.loop && s.name.StartsWith("Amb")), "фон один");
    }

    [UnityTest] public IEnumerator FinaleIsSilentThenIgnitesOnce()
    {
        gm.JumpToZone(6); yield return null;
        var b = Object.FindFirstObjectByType<Bonfire>(); var gf = b.GetComponent<GiveFlame>();
        Tp(b.transform.position.x - 1.5f, b.transform.position.y + 0.8f, 0.8f); yield return Settle();
        p.giveHeld = true;
        for (int i = 0; i < 200 && !gf.Done; i++) { Frame(); yield return null; }
        Assert.IsTrue(gf.Done);
        yield return new WaitForSeconds(2.0f); // t ~ 2.0: тишина между FadeEnd и IgniteAt
        Assert.AreEqual(0f, au.AmbientVolume, 0.001f); Assert.AreEqual(0f, au.CrackleVolume, 0.001f);
        Assert.AreEqual(0, au.Count(Snd.BonfireIgnite), "до IgniteAt костёр молчит");
        yield return new WaitForSeconds(1.0f);
        Assert.AreEqual(1, au.Count(Snd.BonfireIgnite));
        yield return new WaitForSeconds(1.9f); // t ~ 4.9: Игни вернулся
        Assert.AreEqual(1, au.Count(Snd.BonfireIgnite));
        Assert.Greater(au.CrackleVolume, 0.05f, "треск вернулся");
        Assert.AreEqual(0f, au.AmbientVolume, 0.001f, "фон не возвращается");
    }
}
