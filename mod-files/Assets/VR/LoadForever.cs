#if BALDI_LOAD_FOREVER
using UnityEngine;
using UnityEngine.SceneManagement;

// The loading-screen study app (Jen's son wanted to study it): built with
// BALDI_LOAD_FOREVER (ApkBuild.BuildLoadForever) as its own app beside the
// game. Once the main menu is up, the game's loading screen is shown and kept
// on, in front of everything, so it never goes away.
public static class LoadForever
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject keeper = new GameObject("Load Forever");
        Object.DontDestroyOnLoad(keeper);
        keeper.AddComponent<Keeper>();
    }

    private class Keeper : MonoBehaviour
    {
        private GameObject screen;

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu") return;
            if (screen == null)
            {
                foreach (GameObject found in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (found.name == "LoadingScreen" && found.scene.IsValid())
                    {
                        screen = found;
                        break;
                    }
                }
                if (screen == null) return;
            }
            for (Transform part = screen.transform; part != null; part = part.parent)
            {
                if (!part.gameObject.activeSelf) part.gameObject.SetActive(true);
            }
            screen.transform.SetAsLastSibling();
        }
    }
}
#endif
