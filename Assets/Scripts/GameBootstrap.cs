using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<AstroBrickMissionGame>() != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("AstroBrick Mission");
        gameObject.AddComponent<AstroBrickMissionGame>();
    }
}
