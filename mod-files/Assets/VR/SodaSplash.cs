using UnityEngine;

// BSODA in VR: the spray looks exactly as the game made it until it hits a
// wall; there its own picture, the same size, is left flat on the wall at the
// wall's angle in a lighter blue, fading away after a while (Jen's son's
// request). Near a corner it bends round it, folded flat onto the next wall
// (inside or outside corner), so none of it sticks out into the air.
// ITM_BSODA calls this.
public class SodaSplash : MonoBehaviour
{
    private const float Size = 4f;        // game units across
    private const float Stay = 12f;       // seconds before it starts to fade
    private const float FadeTime = 4f;

    private static Material splashMaterial;
    private static Shader seeThrough;

    private MeshRenderer drawn;
    private MaterialPropertyBlock block;
    private float age;

    // A splash on the surface at this point, facing out along its normal.
    public static void Make(Vector3 point, Vector3 normal, SpriteRenderer spray)
    {
        if (splashMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("VR/SodaSplash");
            if (shader == null) return;
            splashMaterial = new Material(shader);
        }
        Vector2 size = new Vector2(Size, Size);
        if (spray != null && spray.sprite != null)
        {
            Vector3 scale = spray.transform.lossyScale;
            size = new Vector2(spray.sprite.rect.width / spray.sprite.pixelsPerUnit * Mathf.Abs(scale.x), spray.sprite.rect.height / spray.sprite.pixelsPerUnit * Mathf.Abs(scale.y));
        }
        GameObject splash = new GameObject("BSODA Splash", typeof(MeshFilter), typeof(MeshRenderer));
        splash.GetComponent<MeshFilter>().sharedMesh = Wrapped(point, normal, size);
        MeshRenderer renderer = splash.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = splashMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SodaSplash fade = splash.AddComponent<SodaSplash>();
        fade.drawn = renderer;
        fade.block = new MaterialPropertyBlock();
        fade.block.SetFloat("_Fade", 1f);
        // The spray's own picture: its texture and where the sprite sits on it.
        if (spray != null && spray.sprite != null)
        {
            Vector2 low = Vector2.one, high = Vector2.zero;
            foreach (Vector2 corner in spray.sprite.uv)
            {
                low = Vector2.Min(low, corner);
                high = Vector2.Max(high, corner);
            }
            fade.block.SetTexture("_MainTex", spray.sprite.texture);
            fade.block.SetVector("_MainTex_ST", new Vector4(high.x - low.x, high.y - low.y, low.x, low.y));
        }
        renderer.SetPropertyBlock(fade.block);
    }

    private const float Step = 0.05f;      // game units walked along the wall at a time
    private const float Lift = 0.03f;      // kept off the wall
    private const int Columns = 48;

    // The splash as a strip laid over the walls: from where it hit, it is
    // walked sideways along the surface both ways, turning with each corner.
    private static Mesh Wrapped(Vector3 point, Vector3 normal, Vector2 size)
    {
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
        if (up.sqrMagnitude < 0.0001f) up = Vector3.forward;
        up.Normalize();
        // The same left-to-right as the flat picture had (a quad faces -z).
        Vector3 right = Quaternion.LookRotation(-normal, up) * Vector3.right;
        int layers = Physics.DefaultRaycastLayers;
        Vector3[] at = new Vector3[Columns + 1];
        Vector3[] outward = new Vector3[Columns + 1];
        int middle = Columns / 2;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 position = point, face = normal, along = right * side;
            float walked = 0f;
            at[middle] = point;
            outward[middle] = normal;
            for (int column = 1; column <= middle; column++)
            {
                float goal = size.x * column / Columns;
                while (walked < goal - 0.0001f)
                {
                    float step = Mathf.Min(Step, goal - walked);
                    Vector3 lifted = position + face * Lift;
                    if (Physics.Raycast(lifted, along, out RaycastHit ahead, step + Lift, layers, QueryTriggerInteraction.Ignore))
                    {
                        // An inside corner: up the next wall, away from this one.
                        walked += Mathf.Max(0f, ahead.distance - Lift);
                        position = ahead.point;
                        along = face;
                        face = ahead.normal;
                        continue;
                    }
                    Vector3 next = position + along * step;
                    if (Physics.Raycast(next + face * Lift, -face, out RaycastHit under, Lift * 4f, layers, QueryTriggerInteraction.Ignore))
                    {
                        position = under.point;
                        face = under.normal;
                    }
                    else if (Physics.Raycast(next - face * Lift, -along, out RaycastHit round, step + Lift, layers, QueryTriggerInteraction.Ignore))
                    {
                        // An outside corner: round it, back along the next wall.
                        position = round.point;
                        Vector3 turned = -face;
                        face = round.normal;
                        along = turned;
                    }
                    else
                    {
                        position = next;
                    }
                    walked += step;
                }
                int index = middle + side * column;
                at[index] = position;
                outward[index] = face;
            }
        }
        Vector3[] vertices = new Vector3[(Columns + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[Columns * 6];
        for (int column = 0; column <= Columns; column++)
        {
            Vector3 onWall = at[column] + outward[column] * Lift;
            float u = (float)column / Columns;
            vertices[column * 2] = onWall - up * size.y * 0.5f;
            vertices[column * 2 + 1] = onWall + up * size.y * 0.5f;
            uvs[column * 2] = new Vector2(u, 0f);
            uvs[column * 2 + 1] = new Vector2(u, 1f);
            if (column == Columns) continue;
            int v = column * 2, t = column * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // The spray's own pictures made see-through and light blue.
    public static void SeeThrough(GameObject spray)
    {
        if (seeThrough == null) seeThrough = Resources.Load<Shader>("VR/SeeThroughSprite");
        if (seeThrough == null) return;
        foreach (SpriteRenderer sprite in spray.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Material material = new Material(seeThrough);
            if (sprite.sharedMaterial != null && sprite.sharedMaterial.HasProperty("_Billboard"))
            {
                material.SetFloat("_Billboard", sprite.sharedMaterial.GetFloat("_Billboard"));
            }
            sprite.material = material;
        }
    }

    // Each splash has its own bent strip; it goes with it.
    private void OnDestroy()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age < Stay) return;
        float left = 1f - (age - Stay) / FadeTime;
        if (left <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        block.SetFloat("_Fade", left);
        drawn.SetPropertyBlock(block);
    }
}
