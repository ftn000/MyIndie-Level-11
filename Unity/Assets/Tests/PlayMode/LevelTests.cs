using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Секции уровня гоняются ботом без клавиатуры: FlamePlayer.MoveInput + фиксированный dt.
public class LevelTests
{
    const float Dt = 1f / 60f;
    FlamePlayer p;
    GameManager gm;

    [UnitySetUp]
    public IEnumerator Load()
    {
        Time.captureFramerate = 60;
        yield return SceneManager.LoadSceneAsync("Level1");
        yield return null;
        p = UnityEngine.Object.FindFirstObjectByType<FlamePlayer>();
        gm = GameManager.I; gm.state = GameManager.State.Play; p.manual = true;
    }

    void Tp(float x, float y, float size) { p.transform.position = new Vector3(x, y, 0); p.flame.size = size; Physics.SyncTransforms(); }

    // input(frame) -> (dir, held, jumpPressed, dash, down)
    IEnumerator Run(int frames, Func<int, (int dir, bool held, bool jump, bool dash, bool down)> input, Func<bool> until = null)
    {
        for (int i = 0; i < frames; i++)
        {
            var c = input(i);
            p.MoveInput(Dt, c.dir, c.held, c.jump, c.dash, c.down);
            if (p.flame.Dead || (until != null && until())) yield break;
            yield return null;
        }
    }

    [UnityTest] public IEnumerator IceMeltsAndOpensPath()
    {
        Tp(52.5f, 1.6f, 1f);
        yield return Run(400, _ => (1, false, false, false, false), () => p.transform.position.x > 56f);
        Assert.Greater(p.transform.position.x, 56f, "прошёл сквозь растаявший лёд");
    }

    [UnityTest] public IEnumerator VentNeedsWickMode()
    {
        Tp(60.5f, 1.6f, 1f);
        yield return Run(60, _ => (1, false, false, false, false));
        Assert.Less(p.transform.position.x, 62f, "на большом пламени решётка не пускает");
        yield return Run(500, _ => (1, false, false, false, true), () => p.transform.position.x > 63.5f);
        Assert.Greater(p.transform.position.x, 63.5f, "фитилёк прошёл решётку");
    }

    [UnityTest] public IEnumerator RopeBridgeAndGoal()
    {
        Tp(119.5f, 9f, 1f);
        yield return Run(120, _ => (0, false, false, false, false));
        Assert.IsNotNull(GameObject.Find("Plank"), "верёвка сгорела, мост упал");
        Tp(121.5f, 1.6f, 1f);
        yield return Run(600, _ => (1, false, false, false, false), () => gm.state == GameManager.State.Win);
        Assert.AreEqual(GameManager.State.Win, gm.state, "дошёл до камина по мосту");
    }

    [UnityTest] public IEnumerator PitCrossedWithUpdrafts()
    {
        Tp(91.5f, 1.6f, 1f);
        yield return Run(2400, _ =>
        {
            bool climb = p.inUpdraft && p.transform.position.y < (p.transform.position.x > 100.5f ? 13f : 8.9f);
            bool right = !(climb && (p.transform.position.x > 95f && p.transform.position.x < 98f || p.transform.position.x > 102f)) && !(p.transform.position.x > 106.2f && p.transform.position.x < 107.8f && !p.Grounded);
            return (right ? 1 : 0, climb || p.Grounded, false, false, false);
        }, () => p.transform.position.x > 111.5f && p.Grounded);
        Assert.Greater(p.transform.position.x, 111.5f, "пересёк пропасть");
        Assert.IsFalse(p.flame.Dead);
    }

    [UnityTest] public IEnumerator FuelDrainsFasterWhenBig()
    {
        Tp(40.5f, 1.6f, 1f); float big = p.flame.DrainRate;
        p.flame.size = 0.1f; float small = p.flame.DrainRate;
        Assert.Greater(big / small, 2.5f);
        yield return null;
    }

    [UnityTest] public IEnumerator WindParticlesAppearInGust()
    {
        Tp(80.5f, 1.6f, 1f);
        int maxAlive = 0;
        for (int i = 0; i < 480; i++)
        {
            p.MoveInput(Dt, 0, false, false, false, false);
            int alive = 0;
            foreach (var ps in UnityEngine.Object.FindFirstObjectByType<Fan>().GetComponentsInChildren<ParticleSystem>()) alive += ps.particleCount;
            maxAlive = Mathf.Max(maxAlive, alive);
            if (i == 330) // середина порыва: снимок кадра, если задан OGONOK_OUT
            {
                var path = Environment.GetEnvironmentVariable("OGONOK_OUT");
                if (!string.IsNullOrEmpty(path)) Capture(path);
            }
            yield return null;
        }
        Assert.Greater(maxAlive, 20, "частицы ветра летят в порыв");
    }

    static void Capture(string path)
    {
        LogAssert.ignoreFailingMessages = true; // Camera.Render в batchmode шумит ошибкой шейдера редактора
        var cam = Camera.main; var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
    }
}
