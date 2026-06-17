using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<BlockDetectiveDeluxeGame>() != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("Block Detective Deluxe");
        gameObject.AddComponent<BlockDetectiveDeluxeGame>();
    }
}
