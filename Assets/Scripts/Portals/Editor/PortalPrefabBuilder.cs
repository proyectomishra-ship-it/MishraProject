using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera los dos prefabs del sistema, listos para usar:
///
///   Assets/Prefabs/Portals/Portal.prefab
///       Portal
///       ├── (BoxCollider trigger + ScenePortal)
///       ├── Model (reemplazar)      <- acá va el modelo de la entrada/salida
///       └── Arrival                 <- dónde aparece quien LLEGA a este portal
///
///   Assets/Prefabs/Portals/Cave.prefab
///       Cave                        <- LocationZone
///       ├── Visuals                 <- acá va el modelo de la cueva (se enciende/apaga)
///       │   └── Floor (reemplazar)  <- piso de prueba, para poder testear ya mismo
///       ├── EnemyPoints             <- acá van los EnemySpawnPoint de la cueva
///       └── Exit_Portal             <- instancia del prefab Portal (la salida)
///
/// Se ejecuta UNA vez desde el menú: Tools > Portals > Crear prefabs.
/// Los placeholders (cubos) son para ver la estructura y probar; se reemplazan
/// por los modelos reales.
///
/// El NavMesh NO se genera acá: el proyecto usa una única NavMeshSurface global
/// (recoge todos los objetos con colliders), así que las cuevas entran en el
/// siguiente Bake de esa superficie.
/// </summary>
public static class PortalPrefabBuilder
{
    private const string Folder = "Assets/Prefabs/Portals";
    private const string PortalPath = Folder + "/Portal.prefab";
    private const string CavePath = Folder + "/Cave.prefab";

    [MenuItem("Tools/Portals/Crear prefabs (Portal y Cave)")]
    public static void CreatePrefabs()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath) != null
                   || AssetDatabase.LoadAssetAtPath<GameObject>(CavePath) != null;

        if (exists && !EditorUtility.DisplayDialog(
                "Los prefabs ya existen",
                "Portal.prefab y/o Cave.prefab ya existen. Si los regenerás se pierden los cambios " +
                "hechos a mano en ellos y pueden romperse los overrides de las instancias que ya " +
                "pusiste en la escena.\n\n¿Reemplazarlos?",
                "Reemplazar", "Cancelar"))
        {
            return;
        }

        EnsureFolder();
        BuildPortal();
        BuildCave();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var cave = AssetDatabase.LoadAssetAtPath<GameObject>(CavePath);
        Selection.activeObject = cave;
        EditorGUIUtility.PingObject(cave);

        Debug.Log($"[Portales] Prefabs creados en {Folder}. Si Unity pregunta si guardar la escena, " +
                  "no hace falta: la generación usa objetos temporales.");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Portals");
    }

    // =====================================================================
    // PORTAL
    // =====================================================================

    private static void BuildPortal()
    {
        var root = new GameObject("Portal");

        // Trigger: losa de 3 x 3 x 1.5 m con la base en el piso (el pivote del prefab va a nivel del suelo).
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.5f, 0f);
        box.size = new Vector3(3f, 3f, 1.5f);

        var portal = root.AddComponent<ScenePortal>();

        // El trigger no debe afectar al NavMesh global (que recoge todos los colliders).
        var modifier = root.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = true;

        // Placeholder del modelo, sin collider para no bloquear al jugador.
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.name = "Model (reemplazar)";
        Object.DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        model.transform.localScale = new Vector3(3f, 3f, 0.2f);

        // Punto de llegada: 3 m delante del portal, mirando hacia afuera.
        var arrival = new GameObject("Arrival");
        arrival.transform.SetParent(root.transform, false);
        arrival.transform.localPosition = new Vector3(0f, 0.1f, 3f);
        arrival.transform.localRotation = Quaternion.identity;

        var so = new SerializedObject(portal);
        so.FindProperty("arrivalPoint").objectReferenceValue = arrival.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PortalPath);
        Object.DestroyImmediate(root);
    }

    // =====================================================================
    // CAVE
    // =====================================================================

    private static void BuildCave()
    {
        var portalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath);

        var root = new GameObject("Cave");
        var zone = root.AddComponent<LocationZone>();

        // Visuals: lo que se enciende/apaga. Incluye un piso de prueba con collider.
        var visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor (reemplazar)";
        floor.transform.SetParent(visuals.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        floor.transform.localScale = new Vector3(30f, 0.5f, 30f);

        // Contenedor para los EnemySpawnPoint (fuera de Visuals: nunca se apagan).
        var enemyPoints = new GameObject("EnemyPoints");
        enemyPoints.transform.SetParent(root.transform, false);

        // Salida: instancia del prefab Portal, fuera de Visuals (el trigger tiene que estar siempre activo).
        // Su eje Z apunta hacia el interior de la cueva: ahí aparece quien entra.
        var exit = (GameObject)PrefabUtility.InstantiatePrefab(portalAsset, root.transform);
        exit.name = "Exit_Portal";
        exit.transform.localPosition = new Vector3(0f, 0f, -12f);
        exit.transform.localRotation = Quaternion.identity;

        var zso = new SerializedObject(zone);
        var toggled = zso.FindProperty("toggledObjects");
        toggled.arraySize = 1;
        toggled.GetArrayElementAtIndex(0).objectReferenceValue = visuals;
        zso.FindProperty("center").objectReferenceValue = root.transform;
        zso.FindProperty("activationRadius").floatValue = 40f;
        zso.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CavePath);
        Object.DestroyImmediate(root);
    }
}
