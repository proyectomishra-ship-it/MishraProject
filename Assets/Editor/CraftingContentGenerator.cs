#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera de una sola vez, y de forma segura de repetir:
///   1. Materiales, armaduras, accesorios y mejoras de armas (ScriptableObjects).
///   2. Sus recetas de crafteo.
///   3. Restriccion de clase (Mago -> mago, etc.), tambien en las armas base.
///   4. Un pickup (prefab de red) por cada material.
///   5. Tablas de loot por enemigo con los materiales, asignadas a cada enemigo.
///   6. Todo agregado a ItemDatabase, CraftingRecipeDatabase y DefaultNetworkPrefabs.
///
/// USO: menu  RPG > Generar materiales, equipo, recetas y loot
///
/// REQUISITO: instalar antes los scripts actualizados (IEquippable, EquipmentData,
/// WeaponData, PlayerClassData, etc.); si no, la restriccion de clase no compila/aplica.
///
/// NOTAS
/// - Lo que ya existe NO se sobrescribe (se conservan tus ajustes manuales), salvo la
///   restriccion de clase, que se reaplica en cada ejecucion segun las tablas.
/// - Los items nuevos se AGREGAN AL FINAL de las bases. Nunca se reordena: el ID de red
///   de cada item es su indice en la lista.
/// - Para cambiar stats/costos/loot: edita las tablas de abajo y borra el asset viejo
///   antes de regenerar, o edita el asset directamente en el Inspector.
///
/// ACCION: guardar en Assets/Editor/CraftingContentGenerator.cs
/// </summary>
public static class CraftingContentGenerator
{
    private const string ObjectsDir      = "Assets/Objects";
    private const string MaterialsDir    = "Assets/Objects/Materials";
    private const string PickupMatsDir   = "Assets/Objects/Materials/PickupMats";
    private const string EquipmentDir    = "Assets/Objects/Equipment";
    private const string WeaponsDir      = "Assets/Objects/Weapons";
    private const string RecipesDir      = "Assets/Objects/Recipes";
    private const string IconsDir        = "Assets/Art/Icons";
    private const string PickupsDir      = "Assets/Prefabs/Miscelaneos/Materials";
    private const string IngotModelsDir  = "Assets/Prefabs/Miscelaneos/Vertical Slice/";
    private const string LootDir         = "Assets/Scripts/Data/LootTable";
    private const string EnemiesDir      = "Assets/Prefabs/Enemies";
    private const string ItemDbPath      = "Assets/Objects/ItemDatabase.asset";
    private const string RecipeDbPath    = "Assets/Objects/CraftingRecipeDatabase.asset";
    private const string NetPrefabsPath  = "Assets/DefaultNetworkPrefabs.asset";

    // =====================================================================
    //  TABLAS DE DATOS  (valores iniciales: ajustalos al balance del juego)
    // =====================================================================

    private class MatDef
    {
        public string key, name, desc, icon;
        public MatDef(string key, string name, string desc, string icon)
        { this.key = key; this.name = name; this.desc = desc; this.icon = icon; }
    }

    private class Ing
    {
        public string key; public int qty;
        public Ing(string key, int qty) { this.key = key; this.qty = qty; }
    }

    private class Mod
    {
        public StatType stat; public float value;
        public Mod(StatType stat, float value) { this.stat = stat; this.value = value; }
    }

    private class EquipDef
    {
        public string key, name, desc, icon;
        public EquipmentSlot slot;
        public int gold;
        public Mod[] mods;
        public Ing[] ingredients;
        public EquipDef(string key, string name, EquipmentSlot slot, string icon, string desc,
                        int gold, Mod[] mods, Ing[] ingredients)
        {
            this.key = key; this.name = name; this.slot = slot; this.icon = icon;
            this.desc = desc; this.gold = gold; this.mods = mods; this.ingredients = ingredients;
        }
    }

    private class WeaponDef
    {
        public string key, name, desc, basePath;
        public float attack;
        public int gold;
        public Ing[] ingredients;
        public WeaponDef(string key, string name, string basePath, string desc,
                         float attack, int gold, Ing[] ingredients)
        {
            this.key = key; this.name = name; this.basePath = basePath; this.desc = desc;
            this.attack = attack; this.gold = gold; this.ingredients = ingredients;
        }
    }

    private static Mod M(StatType s, float v) => new Mod(s, v);
    private static Ing I(string k, int q) => new Ing(k, q);

    // ---- MATERIALES (icon = nombre del PNG en Assets/Art/Icons; vacio = sin icono) ----
    private static readonly MatDef[] Materials =
    {
        new MatDef("CopperIngot", "Lingote de Cobre", "Metal blando y facil de trabajar.", "lingote de cobre"),
        new MatDef("SteelIngot",  "Lingote de Acero", "Aleacion resistente para armas y armaduras.", "lingote de acero"),
        new MatDef("GoldIngot",   "Lingote de Oro",   "Metal precioso para joyas y refuerzos.", "lingote de oro"),
        new MatDef("Ruby",        "Rubi",             "Gema roja que concentra poder.", "rubi"),
        new MatDef("Amethyst",    "Amatista",         "Gema violeta afin a la magia.", "amatista"),
        new MatDef("Amber",       "Ambar",            "Resina fosil de brillo calido.", "ambar"),
        new MatDef("Jade",        "Jade",             "Piedra verde, dura y de gran valor.", "jade"),
        new MatDef("Diamond",     "Diamante",         "La gema mas dura que se conoce.", "diamante"),
        // Sin icono todavia: asignalo luego en el asset.
        new MatDef("TannedLeather", "Cuero Curtido",  "Cuero tratado, base de armaduras ligeras.", ""),
        new MatDef("ArcaneCloth",   "Tela Arcana",    "Tela tejida con hilos encantados.", ""),
        new MatDef("WolfFang",      "Colmillo de Lobo", "Colmillo afilado, sirve de adorno y talisman.", ""),
    };

    // ---- EQUIPO: armaduras y accesorios ----
    private static readonly EquipDef[] Equipment =
    {
        // Set de Hierro (Guerrero)
        new EquipDef("Armor_Iron_Helmet", "Casco de Hierro",  EquipmentSlot.Helmet, "casco de hierro",
            "Casco pesado de acero.", 20,
            new[]{ M(StatType.Defense,3), M(StatType.MaxHealth,10) }, new[]{ I("SteelIngot",3) }),
        new EquipDef("Armor_Iron_Chest",  "Peto de Hierro",   EquipmentSlot.Chest,  "peto de hierro",
            "Peto de acero que protege el torso.", 40,
            new[]{ M(StatType.Defense,6), M(StatType.MaxHealth,25) }, new[]{ I("SteelIngot",6) }),
        new EquipDef("Armor_Iron_Legs",   "Falda de Hierro",  EquipmentSlot.Legs,   "falda de hierro",
            "Falda de placas de acero.", 30,
            new[]{ M(StatType.Defense,4), M(StatType.MaxHealth,15) }, new[]{ I("SteelIngot",4) }),
        new EquipDef("Armor_Iron_Boots",  "Botas de Hierro",  EquipmentSlot.Boots,  "botas de hierro",
            "Botas reforzadas con acero.", 15,
            new[]{ M(StatType.Defense,2), M(StatType.MaxHealth,5) }, new[]{ I("SteelIngot",2) }),
        new EquipDef("Amulet_Iron",       "Collar de Hierro", EquipmentSlot.Amulet, "collar de hierro",
            "Collar de eslabones de acero. Aumenta la vida.", 30,
            new[]{ M(StatType.MaxHealth,30) }, new[]{ I("SteelIngot",2), I("GoldIngot",1) }),
        new EquipDef("Ring_Warrior",      "Anillo de Guerrero", EquipmentSlot.Ring, "anillo de guerrero",
            "Anillo con un rubi. Aumenta el ataque.", 30,
            new[]{ M(StatType.Attack,5), M(StatType.Vitality,3) }, new[]{ I("SteelIngot",1), I("GoldIngot",1), I("Ruby",1) }),

        // Set de Cuero (Cazador)
        new EquipDef("Armor_Leather_Helmet", "Casco de Cuero", EquipmentSlot.Helmet, "casco de cuero",
            "Casco ligero de cuero.", 10,
            new[]{ M(StatType.Defense,2), M(StatType.Agility,2) }, new[]{ I("TannedLeather",3), I("CopperIngot",1) }),
        new EquipDef("Armor_Leather_Chest",  "Peto de Cuero",  EquipmentSlot.Chest,  "peto de cuero",
            "Peto de cuero con remaches de cobre.", 25,
            new[]{ M(StatType.Defense,4), M(StatType.Agility,3) }, new[]{ I("TannedLeather",6), I("CopperIngot",2) }),
        new EquipDef("Armor_Leather_Legs",   "Falda de Cuero", EquipmentSlot.Legs,   "falda de cuero",
            "Falda de cuero flexible.", 20,
            new[]{ M(StatType.Defense,3), M(StatType.Agility,2) }, new[]{ I("TannedLeather",4), I("CopperIngot",1) }),
        new EquipDef("Armor_Leather_Boots",  "Botas de Cuero", EquipmentSlot.Boots,  "botas de cuero",
            "Botas livianas para moverse rapido.", 10,
            new[]{ M(StatType.Defense,2), M(StatType.Agility,2), M(StatType.Speed,1) }, new[]{ I("TannedLeather",3), I("CopperIngot",1) }),
        new EquipDef("Amulet_Wolf",          "Collar de Dientes de Lobo", EquipmentSlot.Amulet, "collar de dientes",
            "Talisman de cazador. Aumenta el ataque.", 20,
            new[]{ M(StatType.Attack,6), M(StatType.Dexterity,3) }, new[]{ I("WolfFang",4), I("TannedLeather",1) }),
        new EquipDef("Ring_Hunter",          "Anillo de Cazador", EquipmentSlot.Ring, "anillo de cazador",
            "Anillo con ambar. Mejora la punteria.", 30,
            new[]{ M(StatType.Dexterity,4), M(StatType.Agility,3) }, new[]{ I("CopperIngot",1), I("GoldIngot",1), I("Amber",1) }),

        // Set de Mago
        new EquipDef("Armor_Mage_Helmet", "Casco de Mago", EquipmentSlot.Helmet, "casco de mago",
            "Capucha encantada.", 20,
            new[]{ M(StatType.Defense,1), M(StatType.MaxMana,10), M(StatType.Intelligence,2) }, new[]{ I("ArcaneCloth",3), I("Amethyst",1) }),
        new EquipDef("Armor_Mage_Chest",  "Peto de Mago",  EquipmentSlot.Chest,  "peto de mago",
            "Tunica con runas bordadas.", 40,
            new[]{ M(StatType.Defense,3), M(StatType.MaxMana,20), M(StatType.Intelligence,4) }, new[]{ I("ArcaneCloth",6), I("Amethyst",2) }),
        new EquipDef("Armor_Mage_Legs",   "Falda de Mago", EquipmentSlot.Legs,   "falda de mago",
            "Falda de tela arcana.", 30,
            new[]{ M(StatType.Defense,2), M(StatType.MaxMana,10), M(StatType.Intelligence,3) }, new[]{ I("ArcaneCloth",4), I("Amethyst",1) }),
        new EquipDef("Armor_Mage_Boots",  "Botas de Mago", EquipmentSlot.Boots,  "botas de mago",
            "Botas ligeras de tela encantada.", 15,
            new[]{ M(StatType.Defense,1), M(StatType.MaxMana,5), M(StatType.Speed,1) }, new[]{ I("ArcaneCloth",3), I("Amethyst",1) }),
        new EquipDef("Amulet_Gems",       "Collar de Gemas", EquipmentSlot.Amulet, "collar de gemas",
            "Collar con varias gemas. Amplifica el mana.", 60,
            new[]{ M(StatType.MaxMana,25), M(StatType.Intelligence,5) }, new[]{ I("GoldIngot",1), I("Ruby",1), I("Amethyst",1), I("Diamond",1) }),
        new EquipDef("Ring_Mage",         "Anillo de Mago", EquipmentSlot.Ring, "anillo de mago",
            "Anillo con una amatista. Aumenta la inteligencia.", 30,
            new[]{ M(StatType.Intelligence,5), M(StatType.MaxMana,10) }, new[]{ I("GoldIngot",1), I("Amethyst",1) }),
    };

    // ---- ARMAS: mejoras por personaje. Copian TODO del arma base (modelo, icono,
    //      proyectil, costos) y solo cambian nombre, descripcion y Ataque. ----
    private static readonly WeaponDef[] Weapons =
    {
        // Guerrero
        new WeaponDef("Sword_Steel", "Espada de Acero", "Assets/Objects/Sword_Basic.asset",
            "Espada forjada en acero.", 40, 50,
            new[]{ I("SteelIngot",5), I("CopperIngot",2) }),
        new WeaponDef("Sword_Jade", "Espada de Jade", "Assets/Objects/Sword_Basic.asset",
            "Hoja de acero con filo de jade.", 55, 150,
            new[]{ I("SteelIngot",8), I("Jade",3), I("GoldIngot",2) }),
        // Cazador
        new WeaponDef("Bow_Reinforced", "Arco Reforzado", "Assets/Objects/Bow_Basic.asset",
            "Arco con cuerda y refuerzos de cobre.", 40, 50,
            new[]{ I("TannedLeather",4), I("CopperIngot",4) }),
        new WeaponDef("Bow_Amber", "Arco de Ambar", "Assets/Objects/Bow_Basic.asset",
            "Arco con incrustaciones de ambar.", 55, 150,
            new[]{ I("TannedLeather",6), I("Amber",3), I("GoldIngot",2) }),
        // Mago
        new WeaponDef("Staff_Amethyst", "Baston de Amatista", "Assets/Objects/Grimoire_Basic.asset",
            "Baston coronado con una amatista.", 40, 50,
            new[]{ I("ArcaneCloth",4), I("Amethyst",3) }),
        new WeaponDef("Staff_Ruby", "Baston de Rubi", "Assets/Objects/Grimoire_Basic.asset",
            "Baston coronado con un rubi.", 55, 150,
            new[]{ I("ArcaneCloth",6), I("Ruby",3), I("GoldIngot",2) }),
    };

    // ---- RESTRICCION DE CLASE: nombre del asset PlayerClassData -> items (por prefijo de clave) ----
    private static readonly Dictionary<string, string[]> ClassKeyPrefixes = new Dictionary<string, string[]>
    {
        { "WarriorData", new[]{ "Armor_Iron_", "Amulet_Iron", "Ring_Warrior", "Sword_" } },
        { "HunterData",  new[]{ "Armor_Leather_", "Amulet_Wolf", "Ring_Hunter", "Bow_" } },
        { "MageData",    new[]{ "Armor_Mage_", "Amulet_Gems", "Ring_Mage", "Staff_" } },
    };

    // Armas base que ya existen: tambien quedan restringidas a su clase.
    private static readonly string[][] BaseWeaponClass =
    {
        new[]{ "Assets/Objects/Sword_Basic.asset",    "WarriorData" },
        new[]{ "Assets/Objects/Bow_Basic.asset",      "HunterData"  },
        new[]{ "Assets/Objects/Grimoire_Basic.asset", "MageData"    },
    };

    // Nombre que ve el jugador (solo se escribe si el campo esta vacio).
    private static readonly string[][] ClassDisplayNames =
    {
        new[]{ "WarriorData", "Guerrero" },
        new[]{ "HunterData",  "Cazador"  },
        new[]{ "MageData",    "Mago"     },
    };

    // ---- PICKUPS: como se ve cada material tirado en el suelo ----
    // Lingotes: usan el modelo real. El resto: forma simple de color (reemplazala por tu arte).
    private class Visual
    {
        public string model; public PrimitiveType shape; public Color color; public float scale;
        public Visual(string model) { this.model = model; scale = 1f; }
        public Visual(PrimitiveType shape, Color color, float scale)
        { this.shape = shape; this.color = color; this.scale = scale; }
    }

    private static readonly Dictionary<string, Visual> Visuals = new Dictionary<string, Visual>
    {
        { "CopperIngot",   new Visual(IngotModelsDir + "Lingote de cobre.prefab") },
        { "SteelIngot",    new Visual(IngotModelsDir + "Lingote de acero.prefab") },
        { "GoldIngot",     new Visual(IngotModelsDir + "Lingote de oro.prefab") },
        { "Ruby",          new Visual(PrimitiveType.Sphere,  new Color(0.85f, 0.10f, 0.15f), 0.30f) },
        { "Amethyst",      new Visual(PrimitiveType.Capsule, new Color(0.60f, 0.30f, 0.85f), 0.25f) },
        { "Amber",         new Visual(PrimitiveType.Sphere,  new Color(1.00f, 0.60f, 0.10f), 0.30f) },
        { "Jade",          new Visual(PrimitiveType.Cube,    new Color(0.20f, 0.70f, 0.40f), 0.30f) },
        { "Diamond",       new Visual(PrimitiveType.Capsule, new Color(0.70f, 0.95f, 1.00f), 0.25f) },
        { "TannedLeather", new Visual(PrimitiveType.Cube,    new Color(0.45f, 0.28f, 0.15f), 0.35f) },
        { "ArcaneCloth",   new Visual(PrimitiveType.Cube,    new Color(0.35f, 0.35f, 0.80f), 0.35f) },
        { "WolfFang",      new Visual(PrimitiveType.Capsule, new Color(0.95f, 0.92f, 0.80f), 0.20f) },
    };

    // ---- LOOT: tablas por enemigo (copian la tabla actual y agregan materiales) ----
    private class DropDef
    {
        public string key; public float weight; public int min, max;
        public DropDef(string key, float weight, int min, int max)
        { this.key = key; this.weight = weight; this.min = min; this.max = max; }
    }

    private class EnemyLoot
    {
        public string enemy, sourceTable;
        public float dropChance;     // < 0 = conservar el de la tabla original
        public int minDrops, maxDrops; // < 0 = conservar
        public DropDef[] extra;
        public EnemyLoot(string enemy, string sourceTable, float dropChance, int minDrops, int maxDrops, DropDef[] extra)
        {
            this.enemy = enemy; this.sourceTable = sourceTable; this.dropChance = dropChance;
            this.minDrops = minDrops; this.maxDrops = maxDrops; this.extra = extra;
        }
    }

    private static DropDef D(string k, float w, int min, int max) => new DropDef(k, w, min, max);

    private static readonly EnemyLoot[] Loot =
    {
        new EnemyLoot("Goblin",  LootDir + "/GoblinLootTable.asset", -1f, -1, -1,
            new[]{ D("TannedLeather",3,1,2), D("CopperIngot",2,1,1) }),
        new EnemyLoot("Slime",   LootDir + "/SlimeLootTable.asset",  -1f, -1, -1,
            new[]{ D("ArcaneCloth",3,1,2), D("Amber",2,1,1) }),
        new EnemyLoot("Orc",     LootDir + "/GoblinLootTable.asset", -1f, -1, 3,
            new[]{ D("SteelIngot",3,1,2), D("TannedLeather",2,1,2), D("WolfFang",3,1,2) }),
        new EnemyLoot("Acolyte", LootDir + "/GoblinLootTable.asset", -1f, -1, 3,
            new[]{ D("ArcaneCloth",3,1,2), D("Amethyst",2,1,1), D("Ruby",1,1,1) }),
        new EnemyLoot("DemiGod", LootDir + "/GoblinLootTable.asset", 1f, 2, 4,
            new[]{ D("GoldIngot",3,1,2), D("Diamond",1,1,1), D("Jade",2,1,1), D("Ruby",2,1,1) }),
    };

    // =====================================================================
    //  GENERACION
    // =====================================================================

    private static Dictionary<string, Sprite> iconIndex;

    [MenuItem("RPG/Generar materiales, equipo, recetas y loot")]
    public static void Generate()
    {
        BuildIconIndex();

        EnsureFolder(ObjectsDir, "Materials");
        EnsureFolder(ObjectsDir, "Equipment");
        EnsureFolder(ObjectsDir, "Weapons");
        EnsureFolder(ObjectsDir, "Recipes");
        EnsureFolder(MaterialsDir, "PickupMats");
        EnsureFolder("Assets/Prefabs/Miscelaneos", "Materials");

        var items        = new Dictionary<string, ItemData>();
        var newItems     = new List<ItemData>();
        var newRecipes   = new List<CraftingRecipeData>();
        var missingIcons = new List<string>();
        int created = 0, kept = 0;

        // ---- Materiales ----
        foreach (var m in Materials)
        {
            string path = $"{MaterialsDir}/Material_{m.key}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) { kept++; }
            else
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, path);
                var icon = FindIcon(m.icon, m.name, missingIcons);
                SetCommon(item, m.name, ItemType.Material, icon, m.desc);
                created++;
            }
            items[m.key] = item;
            newItems.Add(item);
        }

        // ---- Equipo ----
        foreach (var e in Equipment)
        {
            string path = $"{EquipmentDir}/{e.key}.asset";
            var item = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
            if (item != null) { kept++; }
            else
            {
                item = ScriptableObject.CreateInstance<EquipmentData>();
                AssetDatabase.CreateAsset(item, path);
                var icon = FindIcon(e.icon, e.name, missingIcons);
                SetCommon(item, e.name, ItemType.Equipment, icon, e.desc);

                var so = new SerializedObject(item);
                so.FindProperty("slot").enumValueIndex = (int)e.slot;
                var mods = so.FindProperty("modifiers");
                mods.arraySize = e.mods.Length;
                for (int i = 0; i < e.mods.Length; i++)
                {
                    var el = mods.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("stat").enumValueIndex = (int)e.mods[i].stat;
                    el.FindPropertyRelative("value").floatValue = e.mods[i].value;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                created++;
            }
            items[e.key] = item;
            newItems.Add(item);
        }

        // ---- Armas (mejoras) ----
        foreach (var w in Weapons)
        {
            string path = $"{WeaponsDir}/{w.key}.asset";
            var item = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (item != null) { kept++; }
            else
            {
                var baseWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>(w.basePath);
                if (baseWeapon == null)
                {
                    Debug.LogError($"[CraftingContentGenerator] No se encontro el arma base '{w.basePath}'. Se omite '{w.name}'.");
                    continue;
                }

                item = Object.Instantiate(baseWeapon);
                item.name = w.key;
                AssetDatabase.CreateAsset(item, path);

                var so = new SerializedObject(item);
                so.FindProperty("itemName").stringValue = w.name;
                so.FindProperty("description").stringValue = w.desc;
                SetModifier(so.FindProperty("modifiers"), StatType.Attack, w.attack);
                so.ApplyModifiedPropertiesWithoutUndo();
                created++;
            }
            items[w.key] = item;
            newItems.Add(item);
        }

        // ---- Recetas ----
        foreach (var e in Equipment)
            AddRecipe(e.key, e.ingredients, e.gold, items, newRecipes, ref created, ref kept);
        foreach (var w in Weapons)
            if (items.ContainsKey(w.key))
                AddRecipe(w.key, w.ingredients, w.gold, items, newRecipes, ref created, ref kept);

        // ---- Bases de datos (solo se agrega al final, nunca se reordena) ----
        int addedItems   = AppendToDatabase(ItemDbPath,   "allItems",   newItems);
        int addedRecipes = AppendToDatabase(RecipeDbPath, "allRecipes", newRecipes);

        AssetDatabase.SaveAssets();

        // ---- Restriccion de clase ----
        int restricted = ApplyClassRestrictions(items);

        // ---- Pickups de materiales + registro de red ----
        var pickups = new Dictionary<string, GameObject>();
        int pickupsCreated = 0;
        foreach (var m in Materials)
        {
            if (!items.TryGetValue(m.key, out var item)) continue;
            var prefab = GetOrCreatePickup(m.key, item, ref pickupsCreated);
            if (prefab != null) pickups[m.key] = prefab;
        }
        int netAdded = RegisterNetworkPrefabs(pickups.Values);

        // ---- Tablas de loot por enemigo ----
        int lootTables = 0, enemiesUpdated = 0;
        foreach (var l in Loot)
            BuildLootTable(l, pickups, ref lootTables, ref enemiesUpdated);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CraftingContentGenerator] Listo.\n" +
                  $"  Assets creados: {created} (ya existian: {kept}).\n" +
                  $"  Agregados a ItemDatabase: {addedItems}. A CraftingRecipeDatabase: {addedRecipes}.\n" +
                  $"  Items con restriccion de clase aplicada: {restricted}.\n" +
                  $"  Pickups nuevos: {pickupsCreated}. Registrados en red: {netAdded}.\n" +
                  $"  Tablas de loot nuevas: {lootTables}. Enemigos actualizados: {enemiesUpdated}.");

        if (missingIcons.Count > 0)
            Debug.LogWarning("[CraftingContentGenerator] Items SIN icono (asignalo en el Inspector): " +
                             string.Join(", ", missingIcons));

        VerifyNetworkHashes(pickups);
    }

    // =====================================================================
    //  RESTRICCION DE CLASE
    // =====================================================================

    private static int ApplyClassRestrictions(Dictionary<string, ItemData> items)
    {
        var classes = new Dictionary<string, PlayerClassData>();
        foreach (var kv in ClassKeyPrefixes)
        {
            var cls = FindClass(kv.Key);
            if (cls == null)
            {
                Debug.LogError($"[CraftingContentGenerator] No se encontro el asset PlayerClassData '{kv.Key}'. " +
                               "Se omite la restriccion de esa clase.");
                continue;
            }
            classes[kv.Key] = cls;
        }

        // Nombres visibles (solo si estan vacios).
        foreach (var pair in ClassDisplayNames)
        {
            if (!classes.TryGetValue(pair[0], out var cls)) continue;
            var so = new SerializedObject(cls);
            var p = so.FindProperty("displayName");
            if (p != null && string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = pair[1];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cls);
            }
        }

        int count = 0;

        // Items nuevos, segun el prefijo de su clave.
        foreach (var kv in items)
        {
            foreach (var cp in ClassKeyPrefixes)
            {
                if (!classes.TryGetValue(cp.Key, out var cls)) continue;
                if (!StartsWithAny(kv.Key, cp.Value)) continue;
                if (SetAllowedClass(kv.Value, cls)) count++;
                else return count; // faltan los scripts actualizados: ya se aviso
            }
        }

        // Armas base que ya existian.
        foreach (var bw in BaseWeaponClass)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(bw[0]);
            if (weapon == null || !classes.TryGetValue(bw[1], out var cls)) continue;
            if (SetAllowedClass(weapon, cls)) count++;
        }

        return count;
    }

    private static bool StartsWithAny(string key, string[] prefixes)
    {
        foreach (var p in prefixes)
            if (key.StartsWith(p)) return true;
        return false;
    }

    private static PlayerClassData FindClass(string assetName)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:PlayerClassData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == assetName)
                return AssetDatabase.LoadAssetAtPath<PlayerClassData>(path);
        }
        return null;
    }

    private static bool SetAllowedClass(UnityEngine.Object asset, PlayerClassData cls)
    {
        var so = new SerializedObject(asset);
        var prop = so.FindProperty("allowedClasses");
        if (prop == null)
        {
            Debug.LogError("[CraftingContentGenerator] El campo 'allowedClasses' no existe. " +
                           "Instala primero los scripts actualizados (EquipmentData / WeaponData).");
            return false;
        }

        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = cls;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return true;
    }

    // =====================================================================
    //  PICKUPS Y RED
    // =====================================================================

    private static GameObject GetOrCreatePickup(string key, ItemData item, ref int createdCount)
    {
        string path = $"{PickupsDir}/Pickup_{key}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        if (!Visuals.TryGetValue(key, out var visual))
        {
            Debug.LogWarning($"[CraftingContentGenerator] Sin visual definido para '{key}'. Se omite su pickup.");
            return null;
        }

        var root = new GameObject($"Pickup_{key}");
        try
        {
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.8f;

            root.AddComponent<NetworkObject>();

            var pickup = root.AddComponent<ItemPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("itemData").objectReferenceValue = item;
            so.FindProperty("quantity").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject model = string.IsNullOrEmpty(visual.model)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(visual.model);

            GameObject child;
            if (model != null)
            {
                child = (GameObject)PrefabUtility.InstantiatePrefab(model);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                child.transform.localScale = child.transform.localScale * visual.scale;
            }
            else
            {
                child = GameObject.CreatePrimitive(visual.shape);
                var childCol = child.GetComponent<Collider>();
                if (childCol != null) Object.DestroyImmediate(childCol);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                child.transform.localScale = Vector3.one * visual.scale;
                child.GetComponent<MeshRenderer>().sharedMaterial = GetPickupMaterial(key, visual.color);
            }
            child.name = "Model";

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        createdCount++;
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Material GetPickupMaterial(string key, Color color)
    {
        string path = $"{PickupMatsDir}/Mat_Pickup_{key}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>Registra los pickups en DefaultNetworkPrefabs (sin esto el servidor no puede spawnearlos).</summary>
    private static int RegisterNetworkPrefabs(IEnumerable<GameObject> prefabs)
    {
        var list = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NetPrefabsPath);
        if (list == null)
        {
            Debug.LogError($"[CraftingContentGenerator] No se encontro '{NetPrefabsPath}'.");
            return 0;
        }

        var so = new SerializedObject(list);
        var arr = so.FindProperty("List");
        var present = new HashSet<UnityEngine.Object>();
        for (int i = 0; i < arr.arraySize; i++)
            present.Add(arr.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue);

        int added = 0;
        foreach (var prefab in prefabs)
        {
            if (prefab == null || present.Contains(prefab)) continue;

            arr.InsertArrayElementAtIndex(arr.arraySize);
            var el = arr.GetArrayElementAtIndex(arr.arraySize - 1);
            el.FindPropertyRelative("Override").enumValueIndex = 0;
            el.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            el.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
            el.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
            present.Add(prefab);
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(list);
        return added;
    }

    private static void VerifyNetworkHashes(Dictionary<string, GameObject> pickups)
    {
        var bad = new List<string>();
        foreach (var kv in pickups)
        {
            var no = kv.Value != null ? kv.Value.GetComponent<NetworkObject>() : null;
            if (no == null || no.GlobalObjectIdHash == 0)
                bad.Add(kv.Key);
        }

        if (bad.Count > 0)
            Debug.LogWarning("[CraftingContentGenerator] Estos pickups todavia no tienen hash de red: " +
                             string.Join(", ", bad) + ". Abri cada prefab en " + PickupsDir +
                             " y guardalo (Ctrl+S) para que Netcode lo genere.");
    }

    // =====================================================================
    //  LOOT
    // =====================================================================

    private static void BuildLootTable(EnemyLoot l, Dictionary<string, GameObject> pickups,
        ref int tablesCreated, ref int enemiesUpdated)
    {
        string tablePath = $"{LootDir}/Loot_{l.enemy}.asset";
        var source = AssetDatabase.LoadAssetAtPath<LootTableData>(l.sourceTable);
        var table  = AssetDatabase.LoadAssetAtPath<LootTableData>(tablePath);

        if (table == null)
        {
            if (source == null)
            {
                Debug.LogError($"[CraftingContentGenerator] No se encontro la tabla base '{l.sourceTable}'. Se omite {l.enemy}.");
                return;
            }

            table = Object.Instantiate(source);
            table.name = $"Loot_{l.enemy}";
            AssetDatabase.CreateAsset(table, tablePath);

            var so = new SerializedObject(table);
            var entries = so.FindProperty("entries");
            foreach (var d in l.extra)
            {
                if (!pickups.TryGetValue(d.key, out var prefab)) continue;
                entries.InsertArrayElementAtIndex(entries.arraySize);
                var el = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                el.FindPropertyRelative("pickupPrefab").objectReferenceValue = prefab;
                el.FindPropertyRelative("weight").floatValue = d.weight;
                el.FindPropertyRelative("minQty").intValue = d.min;
                el.FindPropertyRelative("maxQty").intValue = d.max;
            }

            if (l.dropChance >= 0f) so.FindProperty("dropChance").floatValue = l.dropChance;
            if (l.minDrops >= 0)    so.FindProperty("minDrops").intValue = l.minDrops;
            if (l.maxDrops >= 0)    so.FindProperty("maxDrops").intValue = l.maxDrops;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            tablesCreated++;
        }

        // Asignar al enemigo, salvo que ya tenga una tabla propia distinta de la original.
        string prefabPath = $"{EnemiesDir}/{l.enemy}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"[CraftingContentGenerator] No se encontro el prefab '{prefabPath}'.");
            return;
        }

        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var drop = contents.GetComponentInChildren<DropController>(true);
            if (drop == null)
            {
                Debug.LogWarning($"[CraftingContentGenerator] {l.enemy} no tiene DropController.");
                return;
            }

            var so = new SerializedObject(drop);
            var prop = so.FindProperty("lootTable");
            var current = prop.objectReferenceValue;

            if (current == null || current == source)
            {
                prop.objectReferenceValue = table;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                enemiesUpdated++;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // =====================================================================
    //  AYUDAS
    // =====================================================================

    private static void AddRecipe(string outputKey, Ing[] ingredients, int gold,
        Dictionary<string, ItemData> items, List<CraftingRecipeData> newRecipes,
        ref int created, ref int kept)
    {
        string path = $"{RecipesDir}/Recipe_{outputKey}.asset";
        var recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>(path);
        if (recipe != null) { kept++; newRecipes.Add(recipe); return; }

        recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        AssetDatabase.CreateAsset(recipe, path);

        var so = new SerializedObject(recipe);
        so.FindProperty("outputItem").objectReferenceValue = items[outputKey];
        so.FindProperty("outputQuantity").intValue = 1;
        so.FindProperty("goldCost").intValue = gold;

        var list = so.FindProperty("ingredients");
        list.arraySize = ingredients.Length;
        for (int i = 0; i < ingredients.Length; i++)
        {
            if (!items.TryGetValue(ingredients[i].key, out var mat))
            {
                Debug.LogError($"[CraftingContentGenerator] Ingrediente desconocido '{ingredients[i].key}' en '{outputKey}'.");
                continue;
            }
            var el = list.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("item").objectReferenceValue = mat;
            el.FindPropertyRelative("quantity").intValue = ingredients[i].qty;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        created++;
        newRecipes.Add(recipe);
    }

    /// <summary>Campos comunes de ItemData. Los equipables se dejan apilables
    /// igual que las armas existentes, para no romper la logica del inventario.</summary>
    private static void SetCommon(ItemData item, string name, ItemType type, Sprite icon, string desc)
    {
        var so = new SerializedObject(item);
        so.FindProperty("itemName").stringValue = name;
        so.FindProperty("itemType").enumValueIndex = (int)type;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("description").stringValue = desc;
        so.FindProperty("stackable").boolValue = true;
        so.FindProperty("maxStack").intValue = 99;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetModifier(SerializedProperty mods, StatType stat, float value)
    {
        for (int i = 0; i < mods.arraySize; i++)
        {
            var el = mods.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("stat").enumValueIndex == (int)stat)
            {
                el.FindPropertyRelative("value").floatValue = value;
                return;
            }
        }

        mods.InsertArrayElementAtIndex(mods.arraySize);
        var added = mods.GetArrayElementAtIndex(mods.arraySize - 1);
        added.FindPropertyRelative("stat").enumValueIndex = (int)stat;
        added.FindPropertyRelative("value").floatValue = value;
    }

    /// <summary>Agrega al final de la lista los objetos que todavia no estan.</summary>
    private static int AppendToDatabase<T>(string dbPath, string listField, List<T> toAdd)
        where T : UnityEngine.Object
    {
        var db = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dbPath);
        if (db == null)
        {
            Debug.LogError($"[CraftingContentGenerator] No se encontro '{dbPath}'.");
            return 0;
        }

        var so = new SerializedObject(db);
        var list = so.FindProperty(listField);
        var present = new HashSet<UnityEngine.Object>();
        for (int i = 0; i < list.arraySize; i++)
            present.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (var obj in toAdd)
        {
            if (obj == null || present.Contains(obj)) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = obj;
            present.Add(obj);
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);
        return added;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }

    // ---- Iconos: busca por nombre de archivo, ignorando mayusculas y espacios sobrantes ----
    private static string Norm(string s) =>
        Regex.Replace((s ?? "").Trim().ToLowerInvariant(), @"\s+", " ");

    private static void BuildIconIndex()
    {
        iconIndex = new Dictionary<string, Sprite>();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { IconsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            iconIndex[Norm(Path.GetFileNameWithoutExtension(path))] = sprite;
        }
    }

    private static Sprite FindIcon(string iconKey, string itemName, List<string> missing)
    {
        if (!string.IsNullOrEmpty(iconKey) && iconIndex.TryGetValue(Norm(iconKey), out var sprite))
            return sprite;
        missing.Add(itemName);
        return null;
    }
}
#endif
