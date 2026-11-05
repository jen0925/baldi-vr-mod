using System.Collections.Generic;
using UnityEngine;

// In full VR open doors have thickness. Each one is a flat panel in the
// wall drawn twice: the wall with a hole (the game's masked material) and the
// door or window picture over it. Here the picture is layered a few times just
// in front of the wall, the front layer full brightness and the deeper ones
// darker, so the door reads as a solid slab in its frame. The layers follow the
// door's picture as it changes (opening, closing, locking) and are hidden
// outside full VR, so the flat game stays as made.
public static class DoorDepth
{
    // Kept shallow: deeper, the copies of a door's bars and windows showed side
    // by side from an angle, like the door overlapping itself.
    private const int Layers = 4;
    private const float Depth = 0.2f;   // game units in front of the wall

    private class Slab
    {
        public MeshRenderer door;
        public MeshRenderer[] layers;
        public Material picture;   // the door's picture last seen
        public bool open;          // whether that picture is an open door
    }

    private static readonly List<Material> materialBuffer = new List<Material>();

    private static readonly List<Slab> slabs = new List<Slab>();
    private static readonly HashSet<MeshRenderer> known = new HashSet<MeshRenderer>();
    private static MaterialPropertyBlock block;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject runner = new GameObject("VR Door Depth");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private float next;

        private void LateUpdate()
        {
            bool on = VRRig.InFullVR;
            if (on && Time.unscaledTime >= next)
            {
                // Looking through the whole level is slow, so not too often,
                // and a second off from the shadows' look.
                next = Time.unscaledTime + (next == 0f ? 1f : Time.timeSinceLevelLoad < 6f ? 2f : 8f);
                Find();
            }
            Follow(on);
        }
    }

    private static void Find()
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (known.Contains(renderer)) continue;
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length != 2 || materials[0] == null || materials[0].shader == null) continue;
            if (!materials[0].shader.name.Contains("MaskedStandard")) continue;
            known.Add(renderer);
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null) continue;
            Slab slab = new Slab { door = renderer, layers = new MeshRenderer[Layers] };
            for (int k = 0; k < Layers; k++)
            {
                GameObject layer = new GameObject("Door Depth " + k, typeof(MeshFilter), typeof(MeshRenderer));
                layer.layer = renderer.gameObject.layer;
                layer.transform.SetParent(renderer.transform, false);
                // A quad faces down its -z; the layers stack out of the wall that way.
                float front = Depth * (k + 1) / Layers;
                layer.transform.localPosition = new Vector3(0f, 0f, -front / Mathf.Max(renderer.transform.lossyScale.z, 0.0001f));
                layer.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer drawn = layer.GetComponent<MeshRenderer>();
                drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float shade = k == Layers - 1 ? 1f : 0.45f + 0.1f * k;
                block.Clear();
                block.SetVector("_TextureColor", new Vector4(shade, shade, shade, 0f));
                drawn.SetPropertyBlock(block);
                slab.layers[k] = drawn;
            }
            slabs.Add(slab);
        }
    }

    private static void Follow(bool on)
    {
        for (int i = slabs.Count - 1; i >= 0; i--)
        {
            Slab slab = slabs[i];
            if (slab.door == null)
            {
                known.Remove(slab.door);
                slabs.RemoveAt(i);
                continue;
            }
            slab.door.GetSharedMaterials(materialBuffer);
            Material picture = materialBuffer.Count > 1 ? materialBuffer[1] : null;
            // Only open doors get the thickness; a shut door stays flat in its
            // wall. The picture's name is read only when the picture changes.
            if (picture != slab.picture)
            {
                slab.picture = picture;
                slab.open = picture != null && picture.name.Contains("Open");
            }
            bool open = slab.open;
            bool show = on && open && slab.door.enabled && slab.door.gameObject.activeInHierarchy;
            foreach (MeshRenderer layer in slab.layers)
            {
                if (layer == null) continue;
                if (layer.enabled != show) layer.enabled = show;
                if (show && layer.sharedMaterial != picture) layer.sharedMaterial = picture;
            }
        }
    }
}
