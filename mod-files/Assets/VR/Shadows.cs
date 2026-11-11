using System.Collections.Generic;
using UnityEngine;

// In VR every character, item and object standing in the school gets a sharp,
// hard-edged shadow on the floor, which follows it as it moves. A character or
// item casts its own shape (Baldi's shadow is Baldi, the scissors' shadow is
// scissors), laid on the floor and stretched away from the light; solid things
// like desks cover their footprint, and the player has a round one. New ones
// (characters spawning, items dropped) are picked up every couple of seconds;
// shadows are hidden outside VR, so the flat game stays as made.
public static class Shadows
{
    private const float Size = 0.75f;   // of the sprite's width
    private const float Lift = 0.03f;   // above the floor, in game units
    private const float SchoolFloor = 0f; // the school's floor, when nothing below is solid
    // Which way shapes' shadows fall along the floor, and how long they are
    // for their height: a light high up and off to one side.
    private static readonly Vector3 Away = new Vector3(0.6f, 0f, 0.8f);  // straight under a lamp
    private const float Tile = 10f;            // the school's tiles; a ceiling light over each
    private const float CeilingHeight = 10f;
    private const float MinLength = 0.35f;     // of the thing's height
    private const float MaxLength = 1.3f;
    private static MeshRenderer handShadow;
    private static MeshRenderer padShadow;

    private class Cast
    {
        public Renderer source;
        public Transform shadow;
        public float floor;
        public bool shaped;            // casts its own picture's shape
        public MeshRenderer drawn;
        public Sprite sprite;          // the picture last given to the shadow
        public bool placed;
        public Vector3 lastCentre;     // where the thing was when last placed
    }

    private static readonly List<Cast> casts = new List<Cast>();

    // Every cutout, for its thickness (see Thickness).
    private static readonly List<Renderer> cutouts = new List<Renderer>();
    private const float ThinThickness = 0.35f;
    private const float PeopleThickness = 0.2f;
    private static MaterialPropertyBlock block;
    private static readonly HashSet<Renderer> known = new HashSet<Renderer>();
    private static Material material;
    private static Material shapeMaterial;
    private static Mesh quad;
    private static GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject runner = new GameObject("VR Shadows");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private float next;

        private void LateUpdate()
        {
            bool on = VRRig.InVR;
            if (root != null && root.activeSelf != on) root.SetActive(on);
            if (!on) return;
            if (Time.unscaledTime >= next)
            {
                // Looking through the whole level is slow, so not too often.
                // (soon after a level starts, then only now and then)
                next = Time.unscaledTime + (Time.timeSinceLevelLoad < 6f ? 2f : 8f);
                Find();
                Thicknesses();
            }
            Follow();
        }
    }

    private static void Find()
    {
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("VR/HardShadow");
            if (shader == null) return;
            material = new Material(shader);
            Shader shapes = Resources.Load<Shader>("VR/SpriteShadow");
            shapeMaterial = shapes != null ? new Material(shapes) : material;
            GameObject maker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = maker.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(maker);
            root = new GameObject("VR Shadow Set");
            Object.DontDestroyOnLoad(root);
        }
        foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
        {
            if (known.Contains(renderer)) continue;
            Material shared = renderer.sharedMaterial;
            if (shared == null || shared.shader == null) continue;
            string shader = shared.shader.name;
            bool cutout = shader.Contains("SpriteStandard");
            known.Add(renderer);
            if (cutout) cutouts.Add(renderer);
            Bounds bounds = renderer.bounds;
            // Cutouts, and solid objects standing on the floor (desks, tables,
            // machines): not the school's walls, floors and ceilings, not flat
            // things on walls, nothing huge.
            bool standing = shader.Contains("Shader Graphs/Standard") && renderer is MeshRenderer
                && bounds.size.y < 14f && bounds.min.y < 6f && bounds.min.y > -1f
                && Mathf.Min(bounds.size.x, bounds.size.z) > 0.8f && Mathf.Max(bounds.size.x, bounds.size.z) < 25f;
            if (!cutout && !standing) continue;
            // Lockers, and other solid things standing against a wall, need no
            // shadow (theirs only ended up hidden inside them).
            if (IsLocker(renderer.transform) || (standing && AgainstWall(renderer, bounds))) continue;
            float floor = Physics.Raycast(bounds.center, Vector3.down, out RaycastHit hit, bounds.extents.y + 6f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : SchoolFloor;
            if (bounds.min.y - floor > 8f) continue;
            GameObject shadow = new GameObject("Shadow", typeof(MeshFilter), typeof(MeshRenderer));
            shadow.transform.SetParent(root.transform, false);
            shadow.GetComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer drawn = shadow.GetComponent<MeshRenderer>();
            bool shaped = cutout && shapeMaterial != material;
            drawn.sharedMaterial = shaped ? shapeMaterial : material;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
            shadow.layer = 0;
            casts.Add(new Cast { source = renderer, shadow = shadow.transform, floor = floor, shaped = shaped, drawn = drawn });
        }
    }

    private static void Thicknesses()
    {
        if (block == null) block = new MaterialPropertyBlock();
        cutouts.RemoveAll(renderer => renderer == null);
        foreach (Renderer renderer in cutouts)
        {
            Thickness(renderer, out float thickness, out float layers);
            renderer.GetPropertyBlock(block);
            if (!block.isEmpty && block.GetFloat("_VRThick") == thickness && block.GetFloat("_VRLayers") == layers) continue;
            block.SetFloat("_VRThick", thickness);
            block.SetFloat("_VRLayers", layers);
            renderer.SetPropertyBlock(block);
        }
    }

    // How thick each cutout's side is: all the same, about 1.1 inches (the shader).
    private static void Thickness(Renderer renderer, out float thickness, out float layers)
    {
        // Every cutout the same: a solid side about 1.1 inches deep (Jen's son).
        thickness = 1f;
        layers = 6f;
    }

    private static void Follow()
    {
        // No shadow for the player's body: only the pad in the left hand and
        // the right hand cast one, their own shapes on the floor below them.
        HeldShadow(ref handShadow, VRRig.HandShape, "VR/HandCursor");
        HeldShadow(ref padShadow, VRRig.PadShape, "VR/YCTP_Shape");
        for (int i = casts.Count - 1; i >= 0; i--)
        {
            Cast cast = casts[i];
            if (cast.source == null)
            {
                if (cast.shadow != null) Object.Destroy(cast.shadow.gameObject);
                casts.RemoveAt(i);
                continue;
            }
            bool showing = cast.source.enabled && cast.source.gameObject.activeInHierarchy;
            if (cast.shadow.gameObject.activeSelf != showing) cast.shadow.gameObject.SetActive(showing);
            if (!showing) continue;
            Bounds bounds = cast.source.bounds;
            // Most things never move (desks, plants): their shadows are left
            // where they are, and only moving or changing things are redone.
            SpriteRenderer spriteRenderer = cast.source as SpriteRenderer;
            if (cast.placed && bounds.center == cast.lastCentre && (spriteRenderer == null || spriteRenderer.sprite == cast.sprite)) continue;
            cast.placed = true;
            cast.lastCentre = bounds.center;
            if (cast.shaped)
            {
                Shape(cast, bounds);
                continue;
            }
            cast.shadow.position = new Vector3(bounds.center.x, cast.floor + Lift, bounds.center.z);
            cast.shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
            // A solid object's shadow covers its footprint.
            cast.shadow.localScale = new Vector3(bounds.size.x * 0.95f, bounds.size.z * 0.95f, 1f);
        }
        known.RemoveWhere(renderer => renderer == null);
    }

    // The thing's own picture flat on the floor: its foot where the thing
    // stands, the rest stretched away from the light.
    private static void Shape(Cast cast, Bounds bounds)
    {
        SpriteRenderer spriteRenderer = cast.source as SpriteRenderer;
        Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float height = bounds.size.y;
        Vector3 foot = new Vector3(bounds.center.x, cast.floor + Lift, bounds.center.z);
        if (sprite != null)
        {
            Vector3 scale = cast.source.transform.lossyScale;
            float perUnit = sprite.pixelsPerUnit;
            width = sprite.rect.width / perUnit * Mathf.Abs(scale.x);
            height = sprite.rect.height / perUnit * Mathf.Abs(scale.y);
            if (cast.sprite != sprite)
            {
                // Only when the picture changes (Baldi's animations and such).
                cast.sprite = sprite;
                // Where the picture sits on its texture, from the corners the
                // game itself draws the sprite with.
                Vector2[] corners = sprite.uv;
                Vector2 low = new Vector2(1f, 1f), high = Vector2.zero;
                foreach (Vector2 corner in corners)
                {
                    low = Vector2.Min(low, corner);
                    high = Vector2.Max(high, corner);
                }
                if (corners.Length == 0) { low = Vector2.zero; high = Vector2.one; }
                if (block == null) block = new MaterialPropertyBlock();
                block.Clear();
                block.SetTexture("_MainTex", sprite.texture);
                block.SetVector("_MainTex_ST", new Vector4(high.x - low.x, high.y - low.y, low.x, low.y));
                cast.drawn.SetPropertyBlock(block);
            }
        }
        else if (cast.sprite == null && cast.source.sharedMaterial != null && cast.source.sharedMaterial.mainTexture != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetTexture("_MainTex", cast.source.sharedMaterial.mainTexture);
            block.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
            cast.drawn.SetPropertyBlock(block);
            cast.sprite = Placeholder;
        }
        // Cast by the nearest ceiling light (one over the middle of each 10-unit
        // tile): the shadow points away from the lamp and is longer the
        // further the thing stands from it, as under a real ceiling light.
        Vector3 lamp = new Vector3(Mathf.Floor(foot.x / Tile) * Tile + Tile * 0.5f, 0f, Mathf.Floor(foot.z / Tile) * Tile + Tile * 0.5f);
        Vector3 fromLamp = new Vector3(foot.x - lamp.x, 0f, foot.z - lamp.z);
        Vector3 away = fromLamp.magnitude > 0.3f ? fromLamp.normalized : Away.normalized;
        float spread = fromLamp.magnitude / Mathf.Max(CeilingHeight - height * 0.5f, 2f);
        float length = height * Mathf.Clamp(spread, MinLength, MaxLength);
        cast.shadow.position = foot + away * (length * 0.5f);
        cast.shadow.rotation = Quaternion.LookRotation(Vector3.down, away);
        cast.shadow.localScale = new Vector3(width, length, 1f);
    }

    // Marks a shadow whose picture has been given once and never changes.
    private static Sprite placeholder;
    private static Sprite Placeholder => placeholder != null ? placeholder : (placeholder = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero));

    private static bool IsLocker(Transform part)
    {
        for (; part != null; part = part.parent)
        {
            string name = part.name;
            if (name.IndexOf("locker", System.StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("blocker", System.StringComparison.OrdinalIgnoreCase) < 0) return true;
        }
        return false;
    }

    private static readonly Vector3[] Sides = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

    private static bool AgainstWall(Renderer renderer, Bounds bounds)
    {
        Transform own = renderer.transform.root;
        foreach (Vector3 side in Sides)
        {
            float reach = Mathf.Abs(Vector3.Dot(bounds.extents, side)) + 0.6f;
            if (Physics.Raycast(bounds.center, side, out RaycastHit hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != own) return true;
        }
        return false;
    }

    // A held thing's shadow: its picture's outline straight down on the floor
    // below it, the size and turn of the thing as seen from above.
    private static void HeldShadow(ref MeshRenderer shadow, RectTransform held, string picture)
    {
        if (shapeMaterial == null || root == null) return;
        if (shadow == null)
        {
            GameObject made = new GameObject("Held Shadow", typeof(MeshFilter), typeof(MeshRenderer));
            made.transform.SetParent(root.transform, false);
            made.GetComponent<MeshFilter>().sharedMesh = quad;
            shadow = made.GetComponent<MeshRenderer>();
            shadow.sharedMaterial = shapeMaterial;
            shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Texture2D art = Resources.Load<Texture2D>(picture);
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            if (art != null) block.SetTexture("_MainTex", art);
            block.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
            shadow.SetPropertyBlock(block);
        }
        bool showing = held != null && held.gameObject.activeInHierarchy;
        if (shadow.gameObject.activeSelf != showing) shadow.gameObject.SetActive(showing);
        if (!showing) return;
        held.GetWorldCorners(heldCorners);
        // Corners 0..3: bottom left, top left, top right, bottom right.
        float floor = Physics.Raycast(held.position, Vector3.down, out RaycastHit hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : SchoolFloor;
        Vector3 bottomLeft = Flat(heldCorners[0]), topLeft = Flat(heldCorners[1]), bottomRight = Flat(heldCorners[3]);
        Vector3 across = bottomRight - bottomLeft;
        Vector3 up = topLeft - bottomLeft;
        Vector3 centre = Flat((heldCorners[0] + heldCorners[2]) * 0.5f);
        shadow.transform.position = new Vector3(centre.x, floor + Lift, centre.z);
        Vector3 facing = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.forward;
        shadow.transform.rotation = Quaternion.LookRotation(Vector3.down, facing);
        shadow.transform.localScale = new Vector3(Mathf.Max(across.magnitude, 0.05f), Mathf.Max(up.magnitude, 0.05f), 1f);
    }

    private static readonly Vector3[] heldCorners = new Vector3[4];

    private static Vector3 Flat(Vector3 point) => new Vector3(point.x, 0f, point.z);
}
