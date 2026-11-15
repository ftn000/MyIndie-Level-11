using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Скриншот сцены без запуска игры (для проверки света в batchmode, нужен GPU: без -nographics).
// Параметры через переменные окружения: OGONOK_X, OGONOK_Y, OGONOK_SIZE, OGONOK_OUT.
public static class OgonokShot
{
    static float Env(string k, float d) => float.TryParse(Environment.GetEnvironmentVariable(k), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

    public static void Shoot()
    {
        EditorSceneManager.OpenScene(Environment.GetEnvironmentVariable("OGONOK_SCENE") ?? "Assets/Scenes/Level1.unity");
        // P-13: несколько кадров за один запуск: OGONOK_SHOTS="x,y,файл;x,y,файл"
        var list = Environment.GetEnvironmentVariable("OGONOK_SHOTS");
        if (string.IsNullOrEmpty(list)) { ShootOne(); return; }
        foreach (var it in list.Split(';'))
        {
            var a = it.Split(',');
            Environment.SetEnvironmentVariable("OGONOK_X", a[0]); Environment.SetEnvironmentVariable("OGONOK_Y", a[1]); Environment.SetEnvironmentVariable("OGONOK_OUT", a[2]);
            ShootOne();
        }
    }

    static void ShootOne()
    {
        var fp = UnityEngine.Object.FindFirstObjectByType<FlamePlayer>();
        float x = Env("OGONOK_X", 3.5f), y = Env("OGONOK_Y", 1.6f);
        fp.transform.position = new Vector3(x, y, 0);
        fp.flame.size = Env("OGONOK_SIZE", 0.8f); fp.flame.Apply();
        var cam = Camera.main;
        cam.transform.position = new Vector3(x, y + 3.4f, -13); cam.transform.rotation = Quaternion.Euler(11, 0, 0);
        if (Environment.GetEnvironmentVariable("OGONOK_WIND") == "1")
        {
            var fan = UnityEngine.Object.FindFirstObjectByType<Fan>();
            fp.windX = -6.25f; fp.flame.wind = -6.25f; fp.flame.Apply(); fp.flame.Apply();
            fan.SimulateGust(3f);
        }
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply();
        File.WriteAllBytes(Environment.GetEnvironmentVariable("OGONOK_OUT") ?? "shot.png", t.EncodeToPNG());
        Debug.Log("Ogonok: shot saved");
    }
}
