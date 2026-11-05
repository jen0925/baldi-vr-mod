using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The game layers its cameras with URP camera stacks: a base camera draws the
// school, then the overlay cameras in its stack (billboards, HUD, map, screen
// transitions) draw over it into the same picture. The web build uses Unity's
// built-in pipeline, which has no stacks, so this lays the same stacks out the
// built-in way every frame: each overlay draws into its base camera's target,
// just after it, keeping the colour already there. Overlay cameras outside any
// stack draw nothing under URP, so they are switched off until a stack takes
// them in, and a camera that leaves a stack gets its own settings back.
public static class CameraStacks
{
    private struct Own
    {
        public RenderTexture target;
        public Rect rect;
        public float depth;
        public CameraClearFlags clear;
    }

    private static readonly Dictionary<Camera, Own> stacked = new Dictionary<Camera, Own>();
    private static readonly HashSet<Camera> inStackNow = new HashSet<Camera>();
    private static readonly HashSet<Camera> switchedOff = new HashSet<Camera>();
    private static Camera[] cameraBuffer = new Camera[16];
    private static float nextSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject runner = new GameObject("Camera Stacks");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private void LateUpdate() => Apply();
    }

    // Each camera's URP settings as the game shipped them. The exported project
    // cannot load them (the editor drops URP's camera component), so they are put
    // back on the cameras at run time, found by name: base or overlay, the
    // overlays a base camera stacks on top of it, and whether an overlay clears
    // the depth left by the cameras under it.
    private class Shipped
    {
        public CameraRenderType type;
        public bool clearDepth;
        public string[] stack = new string[0];
    }

    private static readonly Dictionary<string, Shipped> shipped = new Dictionary<string, Shipped>
    {
        { "GlobalCam", new Shipped { type = CameraRenderType.Base, clearDepth = true } },
        { "RenderTextureCam", new Shipped { type = CameraRenderType.Base, clearDepth = true } },
        { "Camera", new Shipped { type = CameraRenderType.Base, clearDepth = true } },
        { "MainCamera", new Shipped { type = CameraRenderType.Base, clearDepth = true, stack = new[] { "BillboardCamera", "UICamera", "OverlayCamera" } } },
        { "BillboardCamera", new Shipped { type = CameraRenderType.Overlay, clearDepth = false } },
        { "UICamera", new Shipped { type = CameraRenderType.Overlay, clearDepth = true } },
        { "OverlayCamera", new Shipped { type = CameraRenderType.Overlay, clearDepth = true } },
        { "MapCamera", new Shipped { type = CameraRenderType.Overlay, clearDepth = false } },
    };

    private static readonly HashSet<Camera> restored = new HashSet<Camera>();

    // The stacks themselves. URP's own list on its camera component only works
    // while URP is the active pipeline, so the stacks are kept here instead.
    private static readonly Dictionary<Camera, List<Camera>> stacks = new Dictionary<Camera, List<Camera>>();

    public static List<Camera> Stack(Camera baseCamera)
    {
        Data(baseCamera);
        if (!stacks.TryGetValue(baseCamera, out List<Camera> stack)) stacks[baseCamera] = stack = new List<Camera>();
        return stack;
    }

    // The camera's URP data, put back as shipped the first time it is asked for.
    public static UniversalAdditionalCameraData Data(Camera camera)
    {
        UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        if (restored.Add(camera) && shipped.TryGetValue(camera.name, out Shipped settings))
        {
            data.renderType = settings.type;
            List<Camera> stack = new List<Camera>();
            foreach (string name in settings.stack)
            {
                foreach (Camera other in camera.transform.root.GetComponentsInChildren<Camera>(true))
                {
                    if (other.name == name && !stack.Contains(other)) stack.Add(other);
                }
            }
            stacks[camera] = stack;
        }
        return data;
    }

    private static bool ClearsDepth(Camera camera) => !shipped.TryGetValue(camera.name, out Shipped settings) || settings.clearDepth;

    public static void Apply()
    {
        restored.RemoveWhere(camera => camera == null);
        List<Camera> gone = null;
        foreach (Camera camera in stacks.Keys) if (camera == null) (gone ??= new List<Camera>()).Add(camera);
        if (gone != null) foreach (Camera camera in gone) stacks.Remove(camera);
        // Searching the whole scene for cameras is slow, so only twice a second.
        bool search = Time.unscaledTime >= nextSearch;
        if (search)
        {
            nextSearch = Time.unscaledTime + 0.5f;
            foreach (Camera camera in Object.FindObjectsOfType<Camera>(true))
            {
                if (!restored.Contains(camera) && shipped.ContainsKey(camera.name)) Data(camera);
            }
        }
        inStackNow.Clear();
        if (Camera.allCamerasCount > cameraBuffer.Length) cameraBuffer = new Camera[Camera.allCamerasCount * 2];
        int count = Camera.GetAllCameras(cameraBuffer);
        for (int i = 0; i < count; i++) Lay(cameraBuffer[i]);

        // Cameras that were stacked last frame but no longer are.
        List<Camera> left = null;
        foreach (KeyValuePair<Camera, Own> pair in stacked)
        {
            if (!inStackNow.Contains(pair.Key)) (left ??= new List<Camera>()).Add(pair.Key);
        }
        if (left != null)
        {
            foreach (Camera camera in left)
            {
                if (camera != null)
                {
                    Own own = stacked[camera];
                    camera.targetTexture = own.target;
                    camera.rect = own.rect;
                    camera.depth = own.depth;
                    camera.clearFlags = own.clear;
                }
                stacked.Remove(camera);
            }
        }

        if (!search) return;
        foreach (Camera camera in Object.FindObjectsOfType<Camera>())
        {
            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            bool orphan = data != null && data.renderType == CameraRenderType.Overlay && !inStackNow.Contains(camera);
            if (orphan && camera.enabled)
            {
                camera.enabled = false;
                switchedOff.Add(camera);
            }
            else if (!orphan && switchedOff.Remove(camera))
            {
                camera.enabled = true;
            }
        }
        switchedOff.RemoveWhere(camera => camera == null);
    }

    private static void Lay(Camera baseCamera)
    {
        UniversalAdditionalCameraData data = baseCamera.GetComponent<UniversalAdditionalCameraData>();
        if (data == null || data.renderType != CameraRenderType.Base) return;
        if (!stacks.TryGetValue(baseCamera, out List<Camera> stack)) return;
        for (int i = 0; i < stack.Count; i++)
        {
            Camera overlay = stack[i];
            if (overlay == null || overlay == baseCamera) continue;
            if (!stacked.ContainsKey(overlay))
            {
                stacked[overlay] = new Own { target = overlay.targetTexture, rect = overlay.rect, depth = overlay.depth, clear = overlay.clearFlags };
            }
            inStackNow.Add(overlay);
            overlay.targetTexture = baseCamera.targetTexture;
            overlay.rect = baseCamera.rect;
            overlay.depth = baseCamera.depth + 0.001f * (i + 1);
            overlay.clearFlags = ClearsDepth(overlay) ? CameraClearFlags.Depth : CameraClearFlags.Nothing;
        }
    }
}
