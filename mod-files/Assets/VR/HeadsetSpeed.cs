using UnityEngine;

// The app on the Quest: as many frames a second as the headset can show (the
// Quest 2 goes up to 120), with the edges of the view drawn a little coarser
// (and more so when it is struggling) so it keeps up. And sharper than the
// headset's usual (Jen's son): the whole view is drawn at up to 1.5 times its
// usual resolution, eased down a step whenever frames start coming late and
// back up once they keep up again, so the frame rate always comes first; the
// loading screen, which is simple, is drawn at 1.7 times (VRRig.ShowLoadWord).
public static class HeadsetSpeed
{
    public static bool Loading;

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
            frame = 1f / best;
            Apply(Start);
            done = true;
        }

        private const float Start = 1.3f, Sharpest = 1.5f, LoadingSharpness = 1.7f, Step = 0.1f;
        private float frame = 1f / 72f, smoothed, scale = 1f, keptUpFor, nextChange;

        private void LateUpdate()
        {
            if (!done) return;
            smoothed = Mathf.Lerp(smoothed <= 0f ? frame : smoothed, Time.unscaledDeltaTime, 0.05f);
            if (Loading)
            {
                if (scale != LoadingSharpness) Apply(LoadingSharpness);
                return;
            }
            if (scale > Sharpest) Apply(Sharpest);
            if (Time.unscaledTime < nextChange) return;
            if (smoothed > frame * 1.15f && scale > 1f)
            {
                // Frames are coming late: a little less sharp.
                Apply(Mathf.Max(1f, scale - Step));
                keptUpFor = 0f;
                nextChange = Time.unscaledTime + 2f;
            }
            else if (smoothed < frame * 1.03f && scale < Sharpest)
            {
                // Keeping up: after a while, a little sharper again.
                keptUpFor += Time.unscaledDeltaTime;
                if (keptUpFor > 4f)
                {
                    Apply(Mathf.Min(Sharpest, scale + Step));
                    keptUpFor = 0f;
                    nextChange = Time.unscaledTime + 4f;
                }
            }
            else keptUpFor = 0f;
        }

        private void Apply(float sharpness)
        {
            scale = sharpness;
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = sharpness;
        }
    }
#endif
}
