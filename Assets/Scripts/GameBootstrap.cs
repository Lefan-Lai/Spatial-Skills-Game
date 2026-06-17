using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<LittleShapeEngineerGame>() != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("Little Shape Engineer");
        gameObject.AddComponent<LittleShapeEngineerGame>();
    }
}
