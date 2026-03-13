using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartButton3D : MonoBehaviour
{
    public string gameplayScene = "Thuy Tinh";
    public string menuScene = "Menu";

    void OnMouseDown()
    {
        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        yield return SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Additive);

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
        FindObjectsSortMode.None
    );
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = (i == 0);
        }
        Light[] lights = Object.FindObjectsByType<Light>(
FindObjectsSortMode.None
);

        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional && l != RenderSettings.sun)
            {
                l.shadows = LightShadows.None;
            }
        }

        SceneManager.UnloadSceneAsync(menuScene);
    }
}
