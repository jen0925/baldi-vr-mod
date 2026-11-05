using UnityEditor;
using UnityEngine;

// The settings the mod needs that live in the game's own prefabs and the
// Oculus settings, done here so a player who unpacks their own copy of the
// game does not have to click through Unity for them: the desks and chairs
// get the WoodDesk colour, Playtime's jump rope a slower beat, and the
// headset Phase Sync. The installer runs SetUpAndBuild, which then sets up
// and builds the Quest app (ApkBuild).
public static class ModSetup
{
    private static readonly string[] WoodPrefabs =
    {
        "BigDesk", "BigDesk_NoSpawn", "Chairs_Desk_Base", "Chairs_Desk_Perfect", "NullBlocker_SmallDesk",
        "NullProjectile_Desk", "BillboardChair", "Chair_Test", "NullProjectile_Chair", "RoundTable_Chairs_1", "RoundTable_Chairs_2",
    };

    private const float RopeDelay = 1.7f;   // each jump a second slower (the game's is 1)

    [MenuItem("Baldi VR/Set up the mod")]
    public static void Run()
    {
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/WoodDesk.mat");
        foreach (string name in WoodPrefabs)
        {
            string path = FindPrefab(name);
            if (path == null || wood == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name == "WoodSimple")
                    {
                        materials[i] = wood;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        string rope = FindPrefab("Jumprope");
        if (rope != null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(rope);
            // Found by name: the game's scripts are not visible from this
            // editor assembly.
            MonoBehaviour jumprope = null;
            foreach (MonoBehaviour part in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (part != null && part.GetType().Name == "Jumprope") jumprope = part;
            }
            if (jumprope != null)
            {
                SerializedObject settings = new SerializedObject(jumprope);
                SerializedProperty delay = settings.FindProperty("ropeDelay");
                if (delay != null)
                {
                    delay.floatValue = RopeDelay;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, rope);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Baldi VR mod: prefab settings done.");
    }

    // For the installer: the settings above, the Quest set-up, Phase Sync,
    // then the Quest app.
    public static void SetUpAndBuild()
    {
        Run();
        ApkBuild.Setup();
        Unity.XR.Oculus.OculusSettings oculus = AssetDatabase.LoadAssetAtPath<Unity.XR.Oculus.OculusSettings>("Assets/XR/Settings/Oculus Settings.asset");
        if (oculus != null)
        {
            oculus.PhaseSync = true;
            EditorUtility.SetDirty(oculus);
            AssetDatabase.SaveAssets();
        }
        ApkBuild.Build();
    }

    private static string FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        return null;
    }
}
