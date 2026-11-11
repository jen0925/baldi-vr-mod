using System.Runtime.InteropServices;
using UnityEngine;

// On a computer the game moves its own hand cursor with the mouse and locks the
// mouse to the game (Cursor.lockState). A web page may only lock the mouse during
// a click or key press, so this tells the page what the game wants each time it
// changes and the page does the locking (Plugins/PointerLock.jslib and the page
// template). In VR the controllers drive the cursor and the mouse is left alone.
public static class PointerLock
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PointerLock_Want(int want);
#else
    private static void PointerLock_Want(int want) { }
#endif

    private static int told = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject runner = new GameObject("Pointer Lock");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private void Update()
        {
            int want = Cursor.lockState == CursorLockMode.Locked && !VRRig.InVR ? 1 : 0;
            if (want == told) return;
            told = want;
            PointerLock_Want(want);
        }
    }
}
