using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<SpatialSkillsGame>() != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("Spatial Skills Game");
        gameObject.AddComponent<SpatialSkillsGame>();
    }
}

