using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<BlockDetectiveGame>() != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("Block Detective 2.0");
        gameObject.AddComponent<BlockDetectiveGame>();
    }
}
