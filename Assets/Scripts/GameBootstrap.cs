using UnityEngine;
using ShapeonautRescue;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<ShapeonautRescueGame>() != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("Little Shape Engineer - Shapeonaut Rescue V2");
        gameObject.AddComponent<ShapeonautRescueGame>();
    }
}
