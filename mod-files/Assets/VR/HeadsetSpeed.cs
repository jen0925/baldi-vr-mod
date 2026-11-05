using UnityEngine;

// The app on the Quest: as many frames a second as the headset can show (the
// Quest 2 goes up to 120), with the edges of the view drawn a little coarser
// (and more so when it is struggling) so it keeps up.
public static class HeadsetSpeed
{
#if UNITY_ANDROID && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject runner = new GameObject("Headset Speed");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private bool done;

        // The headset only answers once it has started, so this keeps asking.
        private void Update()
        {
            if (done || !Unity.XR.Oculus.Performance.TryGetAvailableDisplayRefreshRates(out float[] rates) || rates.Length == 0) return;
            float best = 0f;
            foreach (float rate in rates) best = Mathf.Max(best, rate);
            Unity.XR.Oculus.Performance.TrySetDisplayRefreshRate(best);
            Unity.XR.Oculus.Utils.SetFoveationLevel(2);
            Unity.XR.Oculus.Utils.EnableDynamicFFR(true);
            Application.targetFrameRate = Mathf.RoundToInt(best);
            done = true;
        }
    }
#endif
}
