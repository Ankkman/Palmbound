using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// PALMBOUND: Editor-time generator for the Level 01 Broken Workshop blockout.
/// Attach to an empty GameObject (scale 1,1,1), then use the component context
/// menu: "Build / Rebuild Broken Workshop".
///
/// Geometry and material assets persist in the saved Unity scene/project.
/// No runtime spawning, gameplay logic, Meta SDK APIs, or hand scripts.
/// </summary>
public class BrokenWorkshopGenerator : MonoBehaviour
{
    [Header("Compact Seated Layout")]
    [SerializeField, Range(0.70f, 1f)] private float levelScale = 0.78f;

    [Header("Generated Scene References - automatic, do not drag")]
    [SerializeField, HideInInspector] private Transform generatedRoot;
    [SerializeField, HideInInspector] private Transform bridgeCenter;
    [SerializeField, HideInInspector] private GameObject palmBridgeVisual;
    [SerializeField, HideInInspector] private Transform robotStartPoint;
    [SerializeField, HideInInspector] private Transform gateStopPoint;
    [SerializeField, HideInInspector] private Transform bridgeApproachPoint;
    [SerializeField, HideInInspector] private Transform checkpointPoint;
    [SerializeField, HideInInspector] private Transform boardingPoint;
    [SerializeField, HideInInspector] private Transform landingPoint;
    [SerializeField, HideInInspector] private Transform landingExitPoint;
    [SerializeField, HideInInspector] private Transform goalPoint;
    [SerializeField, HideInInspector] private Transform gatePanel;
    [SerializeField, HideInInspector] private Transform gateSwitch;
    [SerializeField, HideInInspector] private Transform energyTerminal;

    [SerializeField, HideInInspector]
    private Transform boardingApproachPoint;

    public Transform BoardingApproachPoint =>
        boardingApproachPoint;

    public Transform GeneratedRoot => generatedRoot;
    public Transform BridgeCenter => bridgeCenter;
    public GameObject PalmBridgeVisual => palmBridgeVisual;
    public Transform RobotStartPoint => robotStartPoint;
    public Transform GateStopPoint => gateStopPoint;
    public Transform BridgeApproachPoint => bridgeApproachPoint;
    public Transform CheckpointPoint => checkpointPoint;
    public Transform BoardingPoint => boardingPoint;
    public Transform LandingPoint => landingPoint;
    public Transform LandingExitPoint => landingExitPoint;
    public Transform GoalPoint => goalPoint;
    public Transform GatePanel => gatePanel;
    public Transform GateSwitch => gateSwitch;
    public Transform EnergyTerminal => energyTerminal;

#if UNITY_EDITOR
    private const string GeneratedName = "GENERATED_BrokenWorkshop";
    private const string MaterialFolder = "Assets/Palmbound/Generated/BrokenWorkshop/Materials";

    private Material steel;
    private Material slate;
    private Material panel;
    private Material brightMetal;
    private Material brass;
    private Material dark;
    private Material cyan;
    private Material amber;
    private Material mint;
    private Material glassBlue;

    [ContextMenu("Build / Rebuild Broken Workshop")]
    private void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("PALMBOUND: Build the map outside Play Mode.", this);
            return;
        }

        if (transform.lossyScale != Vector3.one)
        {
            Debug.LogWarning("PALMBOUND: Use scale (1,1,1) on the workshop root for predictable meter-sized geometry.", this);
        }

        EnsureMaterials();
        Undo.RecordObject(this, "Update Broken Workshop references");

        Transform existing = transform.Find(GeneratedName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject root = new GameObject(GeneratedName);
        Undo.RegisterCreatedObjectUndo(root, "Generate Broken Workshop");
        root.transform.SetParent(transform, false);
        root.transform.localScale = Vector3.one * levelScale;
        generatedRoot = root.transform;

        Transform architecture = Group(generatedRoot, "01_Architecture");
        Transform foundation = Group(architecture, "Foundation_And_Supports");
        Transform start = Group(architecture, "Start_WakeUpBay");
        Transform middle = Group(architecture, "Middle_EngineDeck");
        Transform upper = Group(architecture, "Upper_ReactorChamber");
        Transform details = Group(architecture, "Pipes_Bolts_And_Details");
        Transform guides = Group(generatedRoot, "02_VisualGuides_Only");
        Transform interactions = Group(generatedRoot, "03_InteractionAnchors_AutoCreated");
        Transform navigation = Group(generatedRoot, "04_RobotWaypoints_AutoCreated");

        BuildFoundation(foundation);

        // Platforms are separate at walking height. The continuous lower base
        // is structural decoration, NOT a walkable bridge across the gaps.
        BuildDeck(start, "Start_Deck", new Vector3(-0.250f, 0f, 0f),
            0.185f, 0.245f, cyan);
        BuildDeck(middle, "Checkpoint_Deck", new Vector3(0.015f, 0f, 0f),
            0.185f, 0.245f, amber);
        BuildDeck(upper, "Reactor_Deck", new Vector3(0.288f, 0.170f, 0f),
            0.190f, 0.245f, mint);

        BuildStartRoom(start, details);
        BuildMiddleRoom(middle, details);
        BuildUpperRoom(upper, details);
        BuildBridgeGuides(guides, interactions);
        BuildElevatorGuides(guides, interactions);
        BuildRobotWaypoints(navigation);

        // Automatic reference to the generated switch and terminal, no dragging.
        gateSwitch = FindDeep(generatedRoot, "GateSwitch");
        gatePanel = FindDeep(generatedRoot, "GatePanel");
        energyTerminal = FindDeep(generatedRoot, "EnergyTerminal");

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PALMBOUND: Broken Workshop generated. Save the scene. Map only; gameplay not connected yet.", this);
    }

    [ContextMenu("Select Generated Workshop")]
    private void SelectGenerated()
    {
        if (generatedRoot != null)
            Selection.activeGameObject = generatedRoot.gameObject;
    }

    private static Transform FindDeep(Transform root, string target)
    {
        if (root.name == target) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, target);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform Marker(Transform parent, string name, Vector3 localPosition)
    {
        Transform t = Group(parent, name);
        t.localPosition = localPosition;
        return t;
    }

    private GameObject Box(Transform parent, string name, Vector3 position,
        Vector3 size, Material material, bool keepCollider = false,
        Vector3 euler = default(Vector3))
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localEulerAngles = euler;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        RemoveColliderUnless(obj, keepCollider);
        return obj;
    }

    private GameObject Ball(Transform parent, string name, Vector3 position,
        Vector3 diameter, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = diameter;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        RemoveColliderUnless(obj, false);
        return obj;
    }

    private GameObject Cylinder(Transform parent, string name, Vector3 position,
        float radius, float height, Material material, Vector3 euler = default(Vector3))
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localEulerAngles = euler;
        // Unity cylinder: default height 2, diameter 1.
        obj.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        obj.GetComponent<Renderer>().sharedMaterial = material;
        RemoveColliderUnless(obj, false);
        return obj;
    }

    private void Rod(Transform parent, string name, Vector3 from, Vector3 to,
        float diameter, Material material)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.000001f) return;
        GameObject obj = Cylinder(parent, name, (from + to) * 0.5f,
            diameter * 0.5f, delta.magnitude, material);
        obj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
    }

    private void RemoveColliderUnless(GameObject obj, bool keepCollider)
    {
        if (keepCollider) return;
        Collider c = obj.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c);
    }

    private void BuildFoundation(Transform parent)
    {
        Box(parent, "Bottom_ObsidianSlab", new Vector3(0.016f, -0.260f, 0f),
            new Vector3(0.825f, 0.034f, 0.350f), dark);
        Box(parent, "Mid_Foundation", new Vector3(0.016f, -0.234f, 0f),
            new Vector3(0.810f, 0.026f, 0.337f), steel);
        Box(parent, "Top_InsetPlate", new Vector3(0.016f, -0.218f, 0f),
            new Vector3(0.780f, 0.008f, 0.307f), slate);
        foreach (float z in new[] { -0.162f, 0.162f })
        {
            Box(parent, "Brass_EdgeInlay", new Vector3(0.016f, -0.218f, z),
                new Vector3(0.805f, 0.004f, 0.004f), brass);
        }
        foreach (float x in new[] { -0.385f, 0.415f })
        {
            Box(parent, "Side_EdgeInlay", new Vector3(x, -0.218f, 0),
                new Vector3(0.004f, 0.004f, 0.326f), brass);
        }

        // Perforated grilles on the front of the base.
        for (int i = 0; i < 18; ++i)
        {
            float x = -0.362f + i * 0.043f;
            Box(parent, "Front_Vent_" + i, new Vector3(x, -0.243f, -0.176f),
                new Vector3(0.024f, 0.006f, 0.0025f), i % 3 == 0 ? cyan : panel);
        }

        // Structural columns create the impression of separate elevated islands.
        foreach (float x in new[] { -0.32f, -0.19f, -0.05f, 0.075f, 0.225f, 0.35f })
        {
            float top = x > 0.15f ? 0.157f : -0.015f;
            float bottom = -0.212f;
            float height = top - bottom;
            foreach (float z in new[] { -0.085f, 0.085f })
            {
                Box(parent, "Support_Pillar", new Vector3(x, (bottom + top) * 0.5f, z),
                    new Vector3(0.020f, height, 0.020f), steel);
                Box(parent, "Pillar_BrassFoot", new Vector3(x, bottom + 0.011f, z),
                    new Vector3(0.029f, 0.018f, 0.029f), brass);
            }
        }
        // Visible mechanical undercarriage, well below the robot path.
        Rod(parent, "Lower_PowerPipe", new Vector3(-0.34f, -0.17f, 0.125f),
            new Vector3(0.38f, -0.17f, 0.125f), 0.009f, brass);
        Rod(parent, "Lower_ReturnPipe", new Vector3(-0.34f, -0.19f, 0.135f),
            new Vector3(0.38f, -0.19f, 0.135f), 0.006f, slate);
    }

    private void BuildDeck(Transform parent, string name, Vector3 center,
        float width, float depth, Material accent)
    {
        Transform g = Group(parent, name);
        Box(g, "Deck_Underframe", center + new Vector3(0, -0.025f, 0),
            new Vector3(width + 0.014f, 0.035f, depth + 0.014f), steel);
        Box(g, "Deck_Top", center,
            new Vector3(width, 0.044f, depth), slate, true);
        Box(g, "Walk_Surface", center + new Vector3(0, 0.023f, -0.012f),
            new Vector3(width - 0.026f, 0.003f, 0.117f), panel);
        // Lateral tracks frame the route without blocking the player's palm.
        foreach (float z in new[] { -0.086f, 0.086f })
        {
            Box(g, "Route_Trim", center + new Vector3(0, 0.026f, z),
                new Vector3(width - 0.014f, 0.003f, 0.003f), brass);
        }
        for (int i = 0; i < 4; ++i)
        {
            float x = center.x - width * 0.35f + i * (width * 0.7f / 3f);
            Box(g, "Deck_Seam_" + i, new Vector3(x, center.y + 0.026f, -0.012f),
                new Vector3(0.002f, 0.0015f, 0.106f), steel);
        }
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
        {
            float x = center.x + sx * (width * 0.5f - 0.015f);
            float z = center.z + sz * (depth * 0.5f - 0.015f);
            Ball(g, "Deck_Bolt", new Vector3(x, center.y + 0.027f, z),
                Vector3.one * 0.006f, brightMetal);
        }
        Box(g, "Front_Status_Line", center + new Vector3(0, 0.003f, -depth * 0.5f - 0.007f),
            new Vector3(width - 0.020f, 0.005f, 0.004f), accent);
        // Ribbed side panels add detail when the player views the diorama obliquely.
        for (int i = 0; i < 4; i++)
        {
            float x = center.x + (i - 1.5f) * width * 0.20f;
            Box(g, "Front_Side_Rib", new Vector3(x, center.y - 0.009f, -depth * 0.5f - 0.004f),
                new Vector3(0.009f, 0.017f, 0.003f), i == 1 ? accent : steel);
        }
    }

    private void BuildStartRoom(Transform parent, Transform details)
    {
        // Robot remains visible from the player's side; no full roof.
        Box(parent, "WakeUpBay_RearWall", new Vector3(-0.257f, 0.103f, 0.115f),
            new Vector3(0.186f, 0.160f, 0.018f), steel);
        Box(parent, "RearWall_Inset", new Vector3(-0.257f, 0.104f, 0.102f),
            new Vector3(0.150f, 0.120f, 0.003f), dark);
        Box(parent, "Wall_CyanBand", new Vector3(-0.257f, 0.149f, 0.098f),
            new Vector3(0.140f, 0.007f, 0.004f), cyan);
        foreach (float x in new[] { -0.339f, -0.175f })
        {
            Box(parent, "Wall_OuterUpright", new Vector3(x, 0.113f, 0.105f),
                new Vector3(0.014f, 0.190f, 0.025f), brass);
            Box(parent, "Wall_UprightInner", new Vector3(x, 0.116f, 0.087f),
                new Vector3(0.007f, 0.154f, 0.007f), brightMetal);
        }
        Box(parent, "Bay_Header", new Vector3(-0.257f, 0.207f, 0.106f),
            new Vector3(0.200f, 0.023f, 0.034f), slate);
        for (int i = 0; i < 3; i++)
        {
            float x = -0.305f + i * 0.046f;
            Box(parent, "Ceiling_Light", new Vector3(x, 0.193f, 0.084f),
                new Vector3(0.018f, 0.004f, 0.005f), cyan);
        }
        // A side-mounted power cartridge.
        Box(parent, "Side_PowerCanister", new Vector3(-0.326f, 0.072f, 0.063f),
            new Vector3(0.032f, 0.085f, 0.040f), dark);
        for (int i = 0; i < 3; i++)
            Box(parent, "Canister_Light", new Vector3(-0.326f, 0.048f + i * 0.021f, 0.041f),
                new Vector3(0.021f, 0.005f, 0.004f), cyan);

        // Closed decorative gate. Its movement/activation is a later step.
        Transform gate = Group(parent, "WorkshopGate");
        foreach (float z in new[] { -0.073f, 0.073f })
        {
            Box(gate, "Gate_Upright", new Vector3(-0.200f, 0.084f, z),
                new Vector3(0.018f, 0.122f, 0.018f), brass);
        }
        Box(gate, "Gate_Lintel", new Vector3(-0.200f, 0.146f, 0),
            new Vector3(0.026f, 0.020f, 0.168f), slate);
        Box(gate, "GatePanel", new Vector3(-0.200f, 0.081f, 0),
            new Vector3(0.009f, 0.100f, 0.128f), dark);
        foreach (float z in new[] { -0.043f, 0f, 0.043f })
            Box(gate, "Gate_LightStrip", new Vector3(-0.194f, 0.075f, z),
                new Vector3(0.003f, 0.071f, 0.004f), amber);

        // Touch-accessible button outside the path, on the near-facing edge.
        Transform switchRoot = Marker(parent, "GateSwitch", new Vector3(-0.278f, 0.042f, -0.154f));
        Box(switchRoot, "Switch_Post", new Vector3(0, -0.018f, 0),
            new Vector3(0.052f, 0.036f, 0.053f), steel);
        Box(switchRoot, "Switch_BrassBezel", Vector3.zero,
            new Vector3(0.062f, 0.012f, 0.061f), brass);
        Cylinder(switchRoot, "Touch_Button", new Vector3(0, 0.012f, 0),
            0.019f, 0.012f, amber);
        Cylinder(switchRoot, "Button_Center", new Vector3(0, 0.019f, 0),
            0.008f, 0.002f, brightMetal);

        Rod(details, "Bay_ExternalPipe", new Vector3(-0.350f, 0.118f, 0.145f),
            new Vector3(-0.148f, 0.118f, 0.145f), 0.010f, brass);
        Rod(details, "Bay_ExternalPipeDrop", new Vector3(-0.148f, 0.118f, 0.145f),
            new Vector3(-0.148f, -0.075f, 0.145f), 0.010f, brass);
    }

    private void BuildMiddleRoom(Transform parent, Transform details)
    {
        // Mechanical wall sits behind the walking lane.
        Box(parent, "Engine_BackPanel", new Vector3(0.012f, 0.110f, 0.122f),
            new Vector3(0.167f, 0.157f, 0.014f), steel);
        Box(parent, "Engine_Inset", new Vector3(0.012f, 0.119f, 0.111f),
            new Vector3(0.137f, 0.125f, 0.006f), dark);
        Box(parent, "Engine_Crown", new Vector3(0.012f, 0.198f, 0.120f),
            new Vector3(0.191f, 0.021f, 0.028f), brass);

        // Two visible cog-like wheels - decorative and non-colliding.
        WheelFace(parent, "Main_Gear", new Vector3(-0.016f, 0.118f, 0.104f), 0.046f, brass);
        WheelFace(parent, "Secondary_Gear", new Vector3(0.055f, 0.088f, 0.103f), 0.027f, brightMetal);
        for (int i = 0; i < 3; i++)
        {
            Box(parent, "Engine_StatusMeter", new Vector3(-0.050f + i * 0.022f, 0.184f, 0.101f),
                new Vector3(0.012f, 0.004f, 0.004f), i == 2 ? amber : cyan);
        }
        // Diagonal external power feed and small exhaust stacks.
        Rod(details, "Engine_PipeHorizontal", new Vector3(-0.056f, 0.052f, 0.145f),
            new Vector3(0.094f, 0.052f, 0.145f), 0.011f, brightMetal);
        foreach (float x in new[] { -0.043f, 0.067f })
        {
            Cylinder(details, "Engine_Exhaust", new Vector3(x, 0.217f, 0.134f),
                0.011f, 0.070f, steel);
            Cylinder(details, "Exhaust_Rim", new Vector3(x, 0.256f, 0.134f),
                0.014f, 0.008f, brass);
        }
        // Boarding dock, open toward the elevator gap.
        Box(parent, "Boarding_DockEdge", new Vector3(0.106f, 0.030f, 0),
            new Vector3(0.013f, 0.005f, 0.094f), amber);
        for (int i = 0; i < 3; i++)
        {
            float z = -0.040f + i * 0.040f;
            Box(parent, "Boarding_DockArrow", new Vector3(0.081f, 0.029f, z),
                new Vector3(0.011f, 0.003f, 0.008f), amber);
        }
    }

    private void BuildUpperRoom(Transform parent, Transform details)
    {
        const float y = 0.170f;
        // Raised goal hall: an architectural backdrop, not a closed box.
        Box(parent, "Reactor_RearWall", new Vector3(0.285f, y + 0.112f, 0.113f),
            new Vector3(0.190f, 0.187f, 0.018f), steel);
        Box(parent, "Reactor_Inset", new Vector3(0.285f, y + 0.116f, 0.100f),
            new Vector3(0.159f, 0.150f, 0.004f), dark);
        foreach (float x in new[] { 0.197f, 0.373f })
        {
            Box(parent, "Chamber_Upright", new Vector3(x, y + 0.120f, 0.106f),
                new Vector3(0.018f, 0.233f, 0.027f), brass);
            Box(parent, "Chamber_Upright_Light", new Vector3(x, y + 0.120f, 0.087f),
                new Vector3(0.004f, 0.145f, 0.004f), mint);
        }
        Box(parent, "Chamber_Lintel", new Vector3(0.285f, y + 0.237f, 0.109f),
            new Vector3(0.216f, 0.026f, 0.034f), slate);
        Box(parent, "Chamber_CrownLight", new Vector3(0.285f, y + 0.226f, 0.087f),
            new Vector3(0.135f, 0.005f, 0.004f), mint);

        // Reactor occupies only the rear-right corner, leaving room for robot.
        Transform reactor = Marker(parent, "EnergyTerminal", new Vector3(0.336f, y + 0.027f, 0.064f));
        Cylinder(reactor, "Reactor_Socket", new Vector3(0, 0.021f, 0),
            0.030f, 0.035f, brass);
        Cylinder(reactor, "Reactor_InnerHousing", new Vector3(0, 0.057f, 0),
            0.024f, 0.048f, dark);
        Ball(reactor, "Energy_Core", new Vector3(0, 0.101f, 0),
            new Vector3(0.042f, 0.061f, 0.042f), mint);
        Cylinder(reactor, "Reactor_HaloLower", new Vector3(0, 0.082f, 0),
            0.035f, 0.006f, cyan);
        Cylinder(reactor, "Reactor_HaloUpper", new Vector3(0, 0.124f, 0),
            0.031f, 0.006f, brass);
        foreach (int side in new[] { -1, 1 })
        {
            Rod(reactor, "Reactor_GuardRod", new Vector3(side * 0.032f, 0.036f, 0),
                new Vector3(side * 0.032f, 0.129f, 0), 0.004f, brightMetal);
        }
        Box(parent, "Landing_RimGuide", new Vector3(0.194f, y + 0.030f, 0),
            new Vector3(0.009f, 0.004f, 0.102f), mint);
        Box(parent, "Goal_PathLine", new Vector3(0.281f, y + 0.026f, -0.040f),
            new Vector3(0.134f, 0.002f, 0.004f), mint);
        // Sensor and greebles in back (well out of the main hand path).
        for (int i = 0; i < 4; i++)
            Box(details, "Reactor_BackRib", new Vector3(0.213f + i * 0.044f, y + 0.096f, 0.142f),
                new Vector3(0.010f, 0.089f, 0.008f), brass);
    }

    private void BuildBridgeGuides(Transform guides, Transform anchors)
    {
        // Gap: first platform right edge ~-0.1575, middle left edge ~-0.0775.
        bridgeCenter = Marker(anchors, "BridgeCenter", new Vector3(-0.1175f, 0.055f, 0));
        Transform hint = Group(guides, "Bridge_AlignmentCues");
        foreach (float x in new[] { -0.156f, -0.080f })
        {
            foreach (float z in new[] { -0.061f, 0.061f })
            {
                Box(hint, "Bridge_EndpointBeaconBase", new Vector3(x, 0.032f, z),
                    new Vector3(0.014f, 0.009f, 0.014f), brass);
                Box(hint, "Bridge_EndpointBeacon", new Vector3(x, 0.042f, z),
                    new Vector3(0.006f, 0.014f, 0.006f), cyan);
            }
        }
        // Floating dots indicate the bridge area without creating a visible floor.
        for (int i = 0; i < 4; i++)
        {
            float x = -0.149f + i * 0.021f;
            Ball(hint, "Bridge_GuideDot_" + i, new Vector3(x, 0.041f, -0.072f),
                Vector3.one * 0.0045f, cyan);
        }
        // The actual bridge projection is built but starts disabled.
        // A future bridge readiness script can toggle it automatically.
        Transform bridge = Group(bridgeCenter, "PalmBridge");
        palmBridgeVisual = bridge.gameObject;
        foreach (float z in new[] { -0.044f, 0f, 0.044f })
            Box(bridge, "Thin_BridgeProjection",
                new Vector3(0, -0.030f, z),
                new Vector3(0.091f, 0.003f, 0.004f), cyan);
        foreach (float x in new[] { -0.030f, 0f, 0.030f })
            Box(bridge, "Bridge_CrossPulse",
                new Vector3(x, -0.029f, 0),
                new Vector3(0.003f, 0.004f, 0.093f), glassBlue);
        palmBridgeVisual.SetActive(false);
    }

    private void BuildElevatorGuides(Transform guides, Transform anchors)
    {
        boardingPoint = Marker(anchors, "BoardingPoint", new Vector3(0.112f, 0.052f, 0f));
        landingPoint = Marker(anchors, "LandingPoint", new Vector3(0.199f, 0.210f, 0f));
        landingExitPoint = Marker(
            anchors,
            "LandingExitPoint",
            new Vector3(0.241f, 0.197f, 0f)
        );

        Transform guide = Group(guides, "Elevator_RendezvousCues");
        // Asymmetric little energy arc, toward the far side of the hand passage.
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float x = Mathf.Lerp(0.125f, 0.194f, t);
            float y = Mathf.Lerp(0.066f, 0.209f, t);
            Ball(guide, "Lift_HeightDot_" + i,
                new Vector3(x, y, 0.094f),
                Vector3.one * (0.004f + i * 0.0004f),
                i % 2 == 0 ? amber : cyan);
        }
        Box(guide, "Boarding_EdgeMarker", new Vector3(0.105f, 0.033f, -0.056f),
            new Vector3(0.010f, 0.004f, 0.033f), amber);
        Box(guide, "Landing_EdgeMarker", new Vector3(0.194f, 0.201f, -0.056f),
            new Vector3(0.010f, 0.004f, 0.033f), mint);
    }


    private void BuildRobotWaypoints(Transform navigation)
    {
        // Robot root now represents its feet.
        // Slightly above the walking surfaces to avoid clipping.
        const float lowerFloorY = 0.027f;
        const float upperFloorY = 0.197f;

        robotStartPoint = Marker(
            navigation, "RobotStartPoint",
            new Vector3(-0.310f, lowerFloorY, -0.012f));

        gateStopPoint = Marker(
            navigation, "GateStopPoint",
            new Vector3(-0.225f, lowerFloorY, -0.012f));

        bridgeApproachPoint = Marker(
            navigation, "BridgeApproachPoint",
            new Vector3(-0.158f, lowerFloorY, -0.012f));

        checkpointPoint = Marker(
            navigation, "CheckpointPoint",
            new Vector3(0.008f, lowerFloorY, -0.012f));

        boardingApproachPoint = Marker(
            navigation, "BoardingApproachPoint",
            new Vector3(0.078f, lowerFloorY, -0.012f));

        goalPoint = Marker(
            navigation, "GoalPoint",
            new Vector3(0.300f, upperFloorY, -0.030f));
    }

    private void WheelFace(Transform parent, string name, Vector3 center,
        float radius, Material color)
    {
        Transform wheel = Marker(parent, name, center);
        // Rotated cylinders show the flat circular face toward the player.
        Cylinder(wheel, "Wheel_Disc", Vector3.zero, radius * 0.79f,
            0.009f, color, new Vector3(90f, 0f, 0f));
        Cylinder(wheel, "Wheel_Hub", new Vector3(0, 0, -0.006f),
            radius * 0.22f, 0.012f, steel, new Vector3(90f, 0f, 0f));
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            float x = Mathf.Cos(angle) * radius * 0.78f;
            float y = Mathf.Sin(angle) * radius * 0.78f;
            Box(wheel, "GearTooth_" + i, new Vector3(x, y, 0),
                new Vector3(radius * 0.30f, radius * 0.20f, 0.012f),
                color, false, new Vector3(0, 0, i * 30f));
        }
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Rod(wheel, "WheelSpoke_" + i, Vector3.zero,
                new Vector3(Mathf.Cos(angle) * radius * 0.58f,
                    Mathf.Sin(angle) * radius * 0.58f, -0.005f),
                0.003f, dark);
        }
    }

    private void EnsureMaterials()
    {
        EnsureFolder("Assets", "Palmbound");
        EnsureFolder("Assets/Palmbound", "Generated");
        EnsureFolder("Assets/Palmbound/Generated", "BrokenWorkshop");
        EnsureFolder("Assets/Palmbound/Generated/BrokenWorkshop", "Materials");

        steel = GetMaterial("01_DeepSteel", new Color(0.11f, 0.17f, 0.24f));
        slate = GetMaterial("02_Slate", new Color(0.20f, 0.29f, 0.36f));
        panel = GetMaterial("03_Panel", new Color(0.31f, 0.41f, 0.47f));
        brightMetal = GetMaterial("04_BrightMetal", new Color(0.66f, 0.73f, 0.76f));
        brass = GetMaterial("05_Brass", new Color(0.75f, 0.49f, 0.23f));
        dark = GetMaterial("06_DarkInset", new Color(0.045f, 0.065f, 0.095f));
        cyan = GetMaterial("07_CyanLight", new Color(0.06f, 0.82f, 0.94f), true);
        amber = GetMaterial("08_AmberLight", new Color(0.99f, 0.52f, 0.14f), true);
        mint = GetMaterial("09_MintGoal", new Color(0.19f, 0.93f, 0.70f), true);
        glassBlue = GetMaterial("10_PaleBlue", new Color(0.35f, 0.74f, 0.94f), true);
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }

    private Material GetMaterial(string name, Color color, bool isEmissive = false)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing; // Preserve user-tuned materials.

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            throw new System.InvalidOperationException(
                "No compatible Lit/Unlit shader found. Verify your Unity render pipeline.");

        Material m = new Material(shader) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", isEmissive ? 0f : 0.45f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.52f);
        if (isEmissive && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 1.7f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
#endif
}
