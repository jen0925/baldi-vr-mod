using System.Collections.Generic;
using UnityEngine;

// Less for the headset to draw inside the school (full VR only):
// - the school's floor, wall and ceiling pieces are joined into a few big
//   batches per room instead of one draw each (they never move), and
// - characters, items and their shadows behind a wall from the player are not
//   drawn (the game itself still has them; it is only the drawing that stops).
public static class SchoolSpeed
{
    private const float Near = 12f;          // game units: always drawn this close
    private const float Interval = 0.1f;     // seconds between looks at what is hidden
    private const int UILayer = 5;

    // The game's tiles, known by their meshes (Tile_Single_N_Mesh and so on);
    // the game's own TileController is out of reach of this assembly.
    private static readonly HashSet<MeshRenderer> batched = new HashSet<MeshRenderer>();
    private static readonly List<Renderer> watched = new List<Renderer>();
    private static readonly List<GameObject> toBatch = new List<GameObject>();
    private static int wallMask;
    private static float nextLook, nextFind, nextBatch;
    private static bool wasOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject runner = new GameObject("School Speed");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private void LateUpdate()
        {
            bool on = VRRig.InFullVR;
            if (!on)
            {
                if (wasOn) ShowAll();
                wasOn = false;
                return;
            }
            wasOn = true;
            float now = Time.unscaledTime;
            if (now >= nextBatch)
            {
                nextBatch = now + (Time.timeSinceLevelLoad < 10f ? 2f : 10f);
                Batch();
            }
            if (now >= nextFind)
            {
                nextFind = now + (Time.timeSinceLevelLoad < 10f ? 2f : 5f);
                Find();
            }
            if (now >= nextLook)
            {
                nextLook = now + Interval;
                Look();
            }
        }
    }

    // Joins the tiles not joined yet; the game makes new ones when it opens or
    // closes a wall, so this looks again now and then.
    private static void Batch()
    {
        batched.RemoveWhere(tile => tile == null);
        toBatch.Clear();
        GameObject root = null;
        foreach (MeshFilter filter in Object.FindObjectsOfType<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.name.StartsWith("Tile_")) continue;
            MeshRenderer tile = filter.GetComponent<MeshRenderer>();
            if (tile == null || !batched.Add(tile)) continue;
            toBatch.Add(tile.gameObject);
            if (root == null) root = tile.transform.root.gameObject;
            if (wallMask == 0)
            {
                foreach (Collider wall in tile.GetComponentsInChildren<Collider>(true)) wallMask |= 1 << wall.gameObject.layer;
            }
        }
        if (toBatch.Count > 1) StaticBatchingUtility.Combine(toBatch.ToArray(), root);
    }

    // The characters, items and shadows to hide when behind a wall.
    private static void Find()
    {
        ShowAll();
        watched.Clear();
        // Not what is drawn over the view (the jump rope and the like, on the
        // UI layer far from the player): it is never behind a wall.
        foreach (SpriteRenderer sprite in Object.FindObjectsOfType<SpriteRenderer>())
        {
            if (sprite.gameObject.layer != UILayer) watched.Add(sprite);
        }
        GameObject shadows = GameObject.Find("VR Shadow Set");
        if (shadows != null) shadows.GetComponentsInChildren(true, renderersBuffer);
        foreach (MeshRenderer shadow in renderersBuffer) watched.Add(shadow);
        renderersBuffer.Clear();
    }

    private static readonly List<MeshRenderer> renderersBuffer = new List<MeshRenderer>();

    private static void Look()
    {
        if (wallMask == 0) return;
        Vector3 eye = VRRig.HeadPosition;
        for (int i = 0; i < watched.Count; i++)
        {
            Renderer renderer = watched[i];
            if (renderer == null) continue;
            renderer.forceRenderingOff = !Seen(eye, renderer.bounds);
        }
    }

    // Seen if a straight line from the eyes reaches its middle, its top or
    // either side without going through a wall (checked a little short of it,
    // so a shadow lying on a wall or floor does not hide itself).
    private static bool Seen(Vector3 eye, Bounds bounds)
    {
        Vector3 centre = bounds.center;
        Vector3 toward = centre - eye;
        float distance = toward.magnitude;
        if (distance < Near) return true;
        Vector3 across = Vector3.Cross(Vector3.up, toward / distance) * bounds.extents.magnitude;
        return Clear(eye, centre)
            || Clear(eye, centre + Vector3.up * bounds.extents.y)
            || Clear(eye, centre + across)
            || Clear(eye, centre - across);
    }

    private static bool Clear(Vector3 eye, Vector3 point)
    {
        Vector3 toward = point - eye;
        float distance = toward.magnitude - 1f;
        if (distance <= 0f) return true;
        return !Physics.Raycast(eye, toward.normalized, distance, wallMask, QueryTriggerInteraction.Ignore);
    }

    private static void ShowAll()
    {
        foreach (Renderer renderer in watched)
        {
            if (renderer != null) renderer.forceRenderingOff = false;
        }
    }
}
