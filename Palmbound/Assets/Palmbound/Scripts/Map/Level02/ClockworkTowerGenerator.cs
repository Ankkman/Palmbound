using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// PALMBOUND - Level 02: The Clockwork Tower.
/// A five-zone, editor-generated miniature environment. NO GAMEPLAY is
/// implemented here. Build from the component context menu in Edit Mode.
///
/// Root scale should stay (1,1,1). All generated parts are under one child,
/// so later placement scripts may move ClockworkTowerRoot, NEVER Camera Rig.
/// Platforms have static colliders; decorative parts are collider-free.
/// No Rigidbody, CharacterController, locomotion or camera scripts are added.
/// </summary>
[DisallowMultipleComponent]
public sealed class ClockworkTowerGenerator : MonoBehaviour
{
    [Header("Overall scale (1 unit = 1 meter)")]
    [SerializeField, Range(0.75f, 1.15f)] private float towerScale = 1f;
    [SerializeField] private bool generateWalkableColliders = true;
    [SerializeField] private bool animateDecorativeMechanisms = true;
    [SerializeField] private bool showZoneLabels = true;
    [SerializeField] private bool addAtmosphericLights = true;

    [Header("Generated (read only)")]
    [SerializeField, HideInInspector] private Transform generatedRoot;
    [SerializeField, HideInInspector] private Transform robotStartPoint;
    [SerializeField, HideInInspector] private Transform arrivalGatePanel;
    [SerializeField, HideInInspector] private Transform arrivalGateSwitch;
    [SerializeField, HideInInspector] private Transform arrivalBridgeCenter;
    [SerializeField, HideInInspector] private Transform liftBoardingPoint;
    [SerializeField, HideInInspector] private Transform liftLandingPoint;
    [SerializeField, HideInInspector] private Transform generatorWheel;
    [SerializeField, HideInInspector] private Transform rotatingGearBridge;
    [SerializeField, HideInInspector] private Transform pendulumPivot;
    [SerializeField, HideInInspector] private Transform pendulumBrakeButton;
    [SerializeField, HideInInspector] private Transform heartCore;

    public Transform GeneratedRoot { get { return generatedRoot; } }
    public Transform RobotStartPoint { get { return robotStartPoint; } }
    public Transform ArrivalGatePanel { get { return arrivalGatePanel; } }
    public Transform ArrivalGateSwitch { get { return arrivalGateSwitch; } }
    public Transform ArrivalBridgeCenter { get { return arrivalBridgeCenter; } }
    public Transform LiftBoardingPoint { get { return liftBoardingPoint; } }
    public Transform LiftLandingPoint { get { return liftLandingPoint; } }
    public Transform GeneratorWheel { get { return generatorWheel; } }
    public Transform RotatingGearBridge { get { return rotatingGearBridge; } }
    public Transform PendulumPivot { get { return pendulumPivot; } }
    public Transform PendulumBrakeButton { get { return pendulumBrakeButton; } }
    public Transform HeartCore { get { return heartCore; } }

#if UNITY_EDITOR
    // Geometry coordinates are generator-local (before towerScale).
    // Floor heights are the exact TOP of their main Box Colliders.
    private const float F1 = 0.130f;
    private const float F2 = 0.318f;
    private const float F3 = 0.506f;
    private const float F4 = 0.694f;
    private const float F5 = 0.882f;
    private const float Foot = 0.003f;
    private const string GeneratedName = "GENERATED_ClockworkTower";
    private const string AssetDir = "Assets/PALMBOUND/Generated/ClockworkTower";

    private Material metal, dark, graphite, brass, gold, copper, lightMetal;
    private Material cyan, amber, mint, red, glass, black;
    private readonly Dictionary<int, Mesh> gearMeshes = new Dictionary<int, Mesh>();

    [ContextMenu("PALMBOUND / Build or Rebuild Clockwork Tower")]
    private void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("PALMBOUND: Build tower in Edit Mode, not Play Mode.", this);
            return;
        }

        if (Vector3.Distance(transform.localScale, Vector3.one) > 0.001f)
            Debug.LogWarning("PALMBOUND: Keep ClockworkTowerRoot scale at (1,1,1). Use the Tower Scale field instead.", this);

        EnsureAssets();
        Undo.RecordObject(this, "Update tower generated references");
        Transform old = transform.Find(GeneratedName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject(GeneratedName);
        Undo.RegisterCreatedObjectUndo(root, "Generate Clockwork Tower");
        root.transform.SetParent(transform, false);
        root.transform.localScale = Vector3.one * towerScale;
        generatedRoot = root.transform;

        Transform structure = Group(generatedRoot, "00_TowerSkeleton");
        Transform dock = Group(generatedRoot, "01_ArrivalDock");
        Transform lift = Group(generatedRoot, "02_LiftShaft");
        Transform gearworks = Group(generatedRoot, "03_Gearworks");
        Transform vault = Group(generatedRoot, "04_PendulumVault");
        Transform heart = Group(generatedRoot, "05_HeartChamber");
        Transform rearMachinery = Group(generatedRoot, "06_BackgroundMachinery_Animated");
        Transform anchors = Group(generatedRoot, "07_InteractionAnchors_NoVisuals");
        Transform navigation = Group(generatedRoot, "08_RobotWaypoints_NoVisuals");
        Transform atmos = Group(generatedRoot, "09_Lights_And_DirectionSigns");

        BuildTowerSkeleton(structure);
        BuildArrivalDock(dock, anchors, navigation);
        BuildLiftShaft(lift, anchors, navigation);
        BuildGearworks(gearworks, anchors, navigation);
        BuildPendulumVault(vault, anchors, navigation);
        BuildHeartChamber(heart, anchors, navigation);
        BuildBackgroundMachinery(rearMachinery);
        BuildSignageAndLights(atmos);

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PALMBOUND Level 02: CLOCKWORK TOWER environment generated. Save the scene. " +
                  "No gameplay or XR camera scripts were installed.", this);
    }

    [ContextMenu("PALMBOUND / Select Generated Tower")]
    private void SelectGenerated()
    {
        if (generatedRoot != null) Selection.activeGameObject = generatedRoot.gameObject;
    }

    private static Transform Group(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform Marker(Transform parent, string name, Vector3 localPos)
    {
        Transform marker = Group(parent, name);
        marker.localPosition = localPos;
        return marker;
    }

    private GameObject Box(Transform parent, string name, Vector3 p, Vector3 size,
        Material mat, bool collider = false, Vector3 euler = default(Vector3))
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = p;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!collider || !generateWalkableColliders)
            DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private GameObject Cylinder(Transform parent, string name, Vector3 p,
        float radius, float height, Material mat, Vector3 euler = default(Vector3))
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = p;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private GameObject Sphere(Transform parent, string name, Vector3 p,
        float diameter, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = p;
        go.transform.localScale = Vector3.one * diameter;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private void Rod(Transform p, string name, Vector3 a, Vector3 b,
        float diameter, Material material)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.0000001f) return;
        GameObject rod = Cylinder(p, name, (a + b) * 0.5f,
            diameter * 0.5f, d.magnitude, material);
        rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
    }

    private void CircleRim(Transform parent, string name, Vector3 center,
        float radius, float thickness, Material mat, int steps = 24, float start = 0f, float end = 360f)
    {
        Transform group = Group(parent, name);
        for (int i = 0; i < steps; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(start, end, (float)i / steps);
            float b = Mathf.Deg2Rad * Mathf.Lerp(start, end, (float)(i + 1) / steps);
            Vector3 p0 = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            Vector3 p1 = center + new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, 0f);
            Rod(group, "RimSegment_" + i, p0, p1, thickness, mat);
        }
    }

    private void RouteDeck(Transform parent, string name, Vector3 c,
        float width, float depth, Material accent, bool rails = true)
    {
        Transform d = Group(parent, name);
        // c.y is exactly the collider TOP, never its center.
        Box(d, "WalkableCollider_TopAtFloorY", new Vector3(c.x, c.y - 0.012f, c.z),
            new Vector3(width, 0.024f, depth), graphite, true);
        Box(d, "Underframe", new Vector3(c.x, c.y - 0.033f, c.z),
            new Vector3(width + 0.012f, 0.020f, depth + 0.012f), metal);
        Box(d, "SurfaceInset", new Vector3(c.x, c.y + 0.0008f, c.z - 0.012f),
            new Vector3(width - 0.016f, 0.0016f, depth - 0.032f), dark);

        foreach (int sign in new int[] { -1, 1 })
        {
            float zz = c.z + sign * (depth * 0.5f - 0.009f);
            Box(d, "BrassRouteEdge", new Vector3(c.x, c.y + 0.002f, zz),
                new Vector3(width - 0.008f, 0.003f, 0.003f), accent);
        }
        int tileCount = Mathf.Max(2, Mathf.RoundToInt(width / 0.035f));
        for (int i = 1; i < tileCount; i++)
        {
            float xx = c.x - width * 0.5f + width * i / tileCount;
            Box(d, "SteelTileSeam", new Vector3(xx, c.y + 0.002f, c.z - 0.011f),
                new Vector3(0.0012f, 0.0012f, depth - 0.041f), metal);
        }
        for (int signX = -1; signX <= 1; signX += 2)
        for (int signZ = -1; signZ <= 1; signZ += 2)
        {
            float xx = c.x + signX * (width * 0.5f - 0.012f);
            float zz = c.z + signZ * (depth * 0.5f - 0.012f);
            Sphere(d, "DeckCornerRivet", new Vector3(xx, c.y + 0.004f, zz), 0.006f, lightMetal);
        }
        // Rear guards only; the FRONT is intentionally clear for hand interaction.
        if (rails)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = c.x + (i - 1) * (width * 0.40f);
                Box(d, "RearRailPost", new Vector3(x, c.y + 0.025f, c.z + depth * 0.5f - 0.005f),
                    new Vector3(0.006f, 0.05f, 0.006f), brass);
            }
            Rod(d, "RearRailTop",
                new Vector3(c.x - width * .47f, c.y + .05f, c.z + depth * .5f - .005f),
                new Vector3(c.x + width * .47f, c.y + .05f, c.z + depth * .5f - .005f),
                .005f, brass);
        }
    }

    private Transform Pad(Transform parent, string name, Vector3 p, float radius,
        Material accent)
    {
        Transform root = Marker(parent, name, p);
        Cylinder(root, "DockRing_Base", new Vector3(0, 0.0015f, 0), radius, 0.003f, metal);
        Cylinder(root, "DockRing_Inner", new Vector3(0, 0.004f, 0), radius * .77f, .002f, dark);
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.PI * 2f * i / 8f;
            Vector3 tick = new Vector3(Mathf.Cos(a) * radius * .83f, .006f, Mathf.Sin(a) * radius * .83f);
            Box(root, "DockGuidanceTick_" + i, tick, new Vector3(.004f, .002f, .006f), accent,
                false, new Vector3(0, i * 45f, 0));
        }
        Cylinder(root, "DockCore", new Vector3(0, .006f, 0), radius * .32f, .003f, accent);
        return root;
    }

    private Transform Gear(Transform parent, string name, Vector3 p,
        float radius, int teeth, Material body, float spinSpeed = 0f)
    {
        Transform pivot = Marker(parent, name, p);
        GameObject shape = new GameObject("ToothedGearMesh");
        shape.transform.SetParent(pivot, false);
        shape.transform.localScale = Vector3.one * radius;
        MeshFilter mf = shape.AddComponent<MeshFilter>();
        mf.sharedMesh = GetGearMesh(teeth);
        MeshRenderer mr = shape.AddComponent<MeshRenderer>();
        mr.sharedMaterial = body;

        // Gear face is in XY, visible from the front (negative Z).
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI * 2f * i / 6f;
            Vector3 inner = new Vector3(Mathf.Cos(a) * radius * .14f,
                Mathf.Sin(a) * radius * .14f, -.010f);
            Vector3 outer = new Vector3(Mathf.Cos(a) * radius * .67f,
                Mathf.Sin(a) * radius * .67f, -.010f);
            Rod(pivot, "Spoke_" + i, inner, outer, radius * .11f, lightMetal);
        }
        Cylinder(pivot, "GearHub", new Vector3(0, 0, -.015f), radius * .22f,
            .024f, metal, new Vector3(90, 0, 0));
        Cylinder(pivot, "GearHubGlow", new Vector3(0, 0, -.029f), radius * .09f,
            .003f, cyan, new Vector3(90, 0, 0));
        if (animateDecorativeMechanisms && Mathf.Abs(spinSpeed) > 0.001f)
            pivot.gameObject.AddComponent<ClockworkTowerAmbientMotion>().Configure(
                ClockworkTowerAmbientMotion.MotionType.Spin, Vector3.forward, spinSpeed);
        return pivot;
    }

    private Mesh GetGearMesh(int teeth)
    {
        Mesh cached;
        if (gearMeshes.TryGetValue(teeth, out cached) && cached != null) return cached;
        string path = AssetDir + "/Meshes/Gear_" + teeth + "T.asset";
        Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset == null)
        {
            const int samplesPerTooth = 6;
            int n = teeth * samplesPerTooth;
            var v = new Vector3[n * 2 + 2];
            var tris = new List<int>(n * 12);
            for (int i = 0; i < n; i++)
            {
                int section = i % samplesPerTooth;
                float angle = Mathf.PI * 2f * i / n;
                float r = (section == 2 || section == 3) ? 1f : .83f;
                float x = Mathf.Cos(angle) * r;
                float y = Mathf.Sin(angle) * r;
                v[i] = new Vector3(x, y, -.065f);
                v[i + n] = new Vector3(x, y, .065f);
            }
            v[2 * n] = new Vector3(0, 0, -.065f);
            v[2 * n + 1] = new Vector3(0, 0, .065f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                // Front is viewed from negative Z, so reverse XY winding.
                tris.Add(2 * n); tris.Add(j); tris.Add(i);
                tris.Add(2 * n + 1); tris.Add(i + n); tris.Add(j + n);
                tris.Add(i); tris.Add(j); tris.Add(i + n);
                tris.Add(j); tris.Add(j + n); tris.Add(i + n);
            }
            asset = new Mesh();
            asset.name = "ClockworkGear_" + teeth + "Teeth";
            asset.vertices = v;
            asset.triangles = tris.ToArray();
            asset.RecalculateNormals();
            asset.RecalculateBounds();
            AssetDatabase.CreateAsset(asset, path);
        }
        gearMeshes[teeth] = asset;
        return asset;
    }

    private void BuildTowerSkeleton(Transform p)
    {
        // Architectural base, not a walkable floor and no player collider.
        Box(p, "BasaltBase_01", new Vector3(0, .007f, 0), new Vector3(.76f, .028f, .53f), black);
        Box(p, "BasaltBase_02", new Vector3(0, .030f, 0), new Vector3(.73f, .020f, .50f), metal);
        Box(p, "BrassBaseTop", new Vector3(0, .044f, 0), new Vector3(.71f, .007f, .48f), brass);
        Box(p, "FoundationInset", new Vector3(0, .049f, 0), new Vector3(.68f, .004f, .45f), dark);

        for (int sideX = -1; sideX <= 1; sideX += 2)
        for (int sideZ = -1; sideZ <= 1; sideZ += 2)
        {
            float x = sideX * .302f;
            float z = sideZ * .210f;
            Box(p, "PrimaryTowerColumn", new Vector3(x, .516f, z),
                new Vector3(.026f, .92f, .026f), metal);
            Box(p, "ColumnBrassLine", new Vector3(x - sideX * .010f, .516f, z - sideZ * .011f),
                new Vector3(.004f, .90f, .004f), brass);
            Cylinder(p, "ColumnFoot", new Vector3(x, .069f, z), .022f, .037f, brass);
            Cylinder(p, "ColumnCapital", new Vector3(x, .978f, z), .024f, .035f, brass);
        }
        // Open lattice on REAR and sides; FRONT remains open to the player.
        foreach (float y in new float[] { .124f, .314f, .502f, .690f, .878f, .989f })
        {
            Box(p, "RearHorizontalBeam", new Vector3(0, y, .216f),
                new Vector3(.63f, .014f, .017f), brass);
            Box(p, "FrontUpperTrim", new Vector3(0, y + .044f, -.218f),
                new Vector3(.63f, .010f, .012f), metal);
            foreach (int sign in new int[] { -1, 1 })
                Box(p, "SideTie", new Vector3(sign * .302f, y, 0),
                    new Vector3(.017f, .012f, .43f), metal);
        }
        for (int i = 0; i < 4; i++)
        {
            float y0 = .13f + i * .188f;
            float y1 = y0 + .188f;
            Rod(p, "RearDiagonalTrussA", new Vector3(-.29f, y0, .217f),
                new Vector3(.29f, y1, .217f), .008f, metal);
            Rod(p, "RearDiagonalTrussB", new Vector3(.29f, y0, .217f),
                new Vector3(-.29f, y1, .217f), .008f, metal);
        }
        // Iconic tapering roof and spire.
        Rod(p, "RoofSlopeLeft", new Vector3(-.325f, .995f, 0),
            new Vector3(0f, 1.085f, 0), .020f, brass);
        Rod(p, "RoofSlopeRight", new Vector3(.325f, .995f, 0),
            new Vector3(0f, 1.085f, 0), .020f, brass);
        Cylinder(p, "SpireStem", new Vector3(0, 1.117f, 0), .012f, .080f, lightMetal);
        Sphere(p, "SpireBeacon", new Vector3(0, 1.165f, 0), .035f, cyan);
        CircleRim(p, "TowerCrownOuter", new Vector3(0, .961f, -.229f), .107f, .008f, brass, 30);
        CircleRim(p, "TowerCrownInner", new Vector3(0, .961f, -.236f), .081f, .005f, gold, 30);
    }

    private void BuildArrivalDock(Transform p, Transform anchors, Transform nav)
    {
        RouteDeck(p, "01_Entry_Deck", new Vector3(-.225f, F1, -.057f), .250f, .195f, cyan);
        RouteDeck(p, "02_BridgeExit_Deck", new Vector3(.164f, F1, -.057f), .225f, .195f, amber);
        // The open gap between the two decks is intentional.
        Box(p, "BridgeVoidWarningNear", new Vector3(-.095f, F1 - .008f, -.151f),
            new Vector3(.016f, .014f, .009f), amber);
        Box(p, "BridgeVoidWarningFar", new Vector3(.035f, F1 - .008f, -.151f),
            new Vector3(.016f, .014f, .009f), amber);
        for (int i = 0; i < 5; i++)
        {
            float x = -.080f + i * .026f;
            Box(p, "BrokenTrackTeeth_" + i, new Vector3(x, F1 - .028f, .106f),
                new Vector3(.009f, .010f + (i % 2) * .010f, .016f), metal);
        }

        // A substantial tunnel opening and an independent gate panel.
        Transform gate = Group(p, "ArrivalLockGate");
        Box(gate, "GateFrameLeft", new Vector3(-.167f, F1 + .068f, -.142f),
            new Vector3(.016f, .143f, .018f), brass);
        Box(gate, "GateFrameRight", new Vector3(-.167f, F1 + .068f, .015f),
            new Vector3(.016f, .143f, .018f), brass);
        Box(gate, "GateHeader", new Vector3(-.167f, F1 + .142f, -.063f),
            new Vector3(.022f, .020f, .180f), graphite);
        arrivalGatePanel = Marker(gate, "GatePanel_AnimateUpLater",
            new Vector3(-.167f, F1 + .063f, -.065f));
        Box(arrivalGatePanel, "SlidingLockShutter", Vector3.zero,
            new Vector3(.010f, .115f, .122f), dark);
        for (int i = -1; i <= 1; i++)
            Box(arrivalGatePanel, "GateLuminousBar", new Vector3(-.006f, 0, i * .032f),
                new Vector3(.004f, .078f, .006f), amber);

        Transform console = Marker(p, "ArrivalGateConsole", new Vector3(-.278f, F1, -.165f));
        Box(console, "ConsoleFoot", new Vector3(0, .026f, 0),
            new Vector3(.058f, .052f, .043f), metal);
        Box(console, "ConsoleAngledFace", new Vector3(0, .057f, -.009f),
            new Vector3(.062f, .016f, .048f), brass, false, new Vector3(-18, 0, 0));
        Sphere(console, "PressAndHoldButton", new Vector3(0, .071f, -.015f), .024f, amber);
        arrivalGateSwitch = Marker(anchors, "Arrival_GateHoldButton",
            new Vector3(-.278f, F1 + .071f, -.180f));

        // Narrative signs and tiny safety stripes.
        for (int i = 0; i < 7; i++)
            Box(p, "DockHazardStripe_" + i,
                new Vector3(-.340f + i * .021f, F1 + .003f, -.151f),
                new Vector3(.009f, .002f, .006f), i % 2 == 0 ? amber : black);
        Gear(p, "EntranceEmblemGear", new Vector3(-.248f, F1 + .094f, .135f),
            .045f, 10, copper);

        robotStartPoint = Marker(nav, "01_RobotStartPoint",
            new Vector3(-.300f, F1 + Foot, -.065f));
        Marker(nav, "02_ArrivalGateStopPoint", new Vector3(-.198f, F1 + Foot, -.065f));
        Marker(nav, "03_ArrivalBridgeApproach", new Vector3(-.115f, F1 + Foot, -.065f));
        Marker(nav, "04_ArrivalBridgeExit", new Vector3(.073f, F1 + Foot, -.065f));
        arrivalBridgeCenter = Marker(anchors, "Bridge_PalmAlignmentCenter",
            new Vector3(-.022f, F1 + .023f, -.065f));
        // A faint cyan guide is decoration, NOT a physical bridge.
        Box(p, "BridgeSuggestedApproachGlow", new Vector3(-.101f, F1 + .005f, -.065f),
            new Vector3(.026f, .002f, .055f), cyan);
        Box(p, "BridgeSuggestedExitGlow", new Vector3(.057f, F1 + .005f, -.065f),
            new Vector3(.026f, .002f, .055f), cyan);
    }

    private void BuildLiftShaft(Transform p, Transform anchors, Transform nav)
    {
        // Lift shaft and upper deck on the RIGHT of the tower.
        float shaftX = .209f;
        // The lower dock uses the Arrival BridgeExit deck at the same height;
        // do not create an overlapping second floor collider there.
        RouteDeck(p, "02_UpperLiftDock", new Vector3(.178f, F2, -.065f), .210f, .192f, mint);
        RouteDeck(p, "03_WinchControlDeck", new Vector3(-.161f, F2, -.062f), .248f, .190f, amber);

        for (int s = -1; s <= 1; s += 2)
        {
            float z = s * .111f;
            Box(p, "LiftVerticalGuide", new Vector3(shaftX, .226f, z),
                new Vector3(.011f, .290f, .012f), lightMetal);
            for (int i = 0; i < 5; i++)
                Box(p, "LiftRailNotch", new Vector3(shaftX + .012f, .105f + i * .057f, z),
                    new Vector3(.008f, .008f, .018f), brass);
        }
        Rod(p, "LiftCableA", new Vector3(.155f, .405f, .124f),
            new Vector3(.155f, F1 + .02f, .124f), .006f, lightMetal);
        Rod(p, "LiftCableB", new Vector3(.255f, .405f, .124f),
            new Vector3(.255f, F1 + .02f, .124f), .006f, lightMetal);
        Cylinder(p, "TopCablePulley", new Vector3(.205f, .402f, .131f), .031f,
            .014f, brass, new Vector3(90, 0, 0));
        Gear(p, "SmallWinchGear", new Vector3(.207f, .405f, .105f),
            .037f, 10, lightMetal, 22f);

        Pad(p, "PalmPickupDockVisual", new Vector3(.189f, F1, -.073f), .041f, cyan);
        Pad(p, "PalmLandingDockVisual", new Vector3(.189f, F2, -.073f), .044f, mint);
        liftBoardingPoint = Marker(anchors, "Lift_PalmBoardingCenter",
            new Vector3(.189f, F1 + .035f, -.073f));
        liftLandingPoint = Marker(anchors, "Lift_PalmLandingCenter",
            new Vector3(.189f, F2 + .038f, -.073f));
        Marker(nav, "05_LiftLowerApproach", new Vector3(.153f, F1 + Foot, -.072f));
        Marker(nav, "06_LiftUpperExit", new Vector3(.175f, F2 + Foot, -.072f));
        Marker(nav, "07_WinchDeckApproach", new Vector3(-.124f, F2 + Foot, -.065f));

        // Generator wheel: big, readable, near the front edge. Gameplay will
        // connect hand rotation to THIS pivot later (not an ambient spinner).
        generatorWheel = Gear(p, "GeneratorWheel_PlayerRotateLater",
            new Vector3(-.164f, F2 + .071f, -.159f), .058f, 12, brass);
        CircleRim(generatorWheel, "GeneratorWheelOuterGrip", Vector3.zero,
            .067f, .008f, gold, 20);
        Sphere(generatorWheel, "GeneratorGripKnob", new Vector3(.065f, 0, -.011f), .021f, amber);
        Box(p, "WinchMachineHousing", new Vector3(-.174f, F2 + .083f, .092f),
            new Vector3(.112f, .120f, .049f), metal);
        for (int i = 0; i < 4; i++)
            Box(p, "WinchPowerMeterSegment", new Vector3(-.205f + i * .021f, F2 + .118f, .061f),
                new Vector3(.013f, .030f, .004f), i == 0 ? mint : dark);
        Marker(anchors, "Winch_PinchRotateHandle", generatorWheel.localPosition +
            new Vector3(.065f, 0, -.011f));

        // Visually separate stage-to-stage transfer. The elevator platform is
        // intentionally a non-moving PLACEHOLDER until its puzzle is added.
        Transform cargoLift = Marker(p, "ServiceLift_Zone2ToZone3_FuturePuzzle",
            new Vector3(-.228f, F2, .075f));
        Box(cargoLift, "ServiceLiftPlatform", new Vector3(0, -.013f, 0),
            new Vector3(.086f, .026f, .087f), graphite, true);
        Cylinder(cargoLift, "ServiceLiftIndicator", new Vector3(0, .003f, 0),
            .018f, .003f, amber);
        Rod(p, "ServiceLiftRail", new Vector3(-.264f, F2, .120f),
            new Vector3(-.264f, F3 + .025f, .120f), .007f, brass);
        Marker(anchors, "ServiceLift_UpperTarget", new Vector3(-.228f, F3 + .030f, .075f));
    }

    private void BuildGearworks(Transform p, Transform anchors, Transform nav)
    {
        RouteDeck(p, "01_LeftGearEntry", new Vector3(-.206f, F3, -.060f), .205f, .191f, amber);
        RouteDeck(p, "02_RightGearExit", new Vector3(.205f, F3, -.060f), .194f, .190f, cyan);

        // The big rotating HUB occupies the gap. It is a future pinch puzzle,
        // therefore it DOES NOT ambient-spin and is not a normal walkable deck.
        rotatingGearBridge = Marker(p, "CentralRotatingBridge_PuzzlePivot",
            new Vector3(0, F3 - .018f, -.066f));
        Cylinder(rotatingGearBridge, "BridgeHub", Vector3.zero, .073f, .021f, brass);
        Cylinder(rotatingGearBridge, "BridgeHubDarkTop", new Vector3(0, .012f, 0),
            .061f, .005f, metal);
        // A visible span, with future logic required to permit robot crossing.
        Box(rotatingGearBridge, "RotatingNarrowWalkway",
            new Vector3(0, .0125f, 0), new Vector3(.214f, .011f, .070f), graphite);
        Box(rotatingGearBridge, "SpanCenterStripe",
            new Vector3(0, .019f, 0), new Vector3(.197f, .002f, .009f), mint);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * .5f;
            Box(rotatingGearBridge, "BridgeHubIndex_" + i,
                new Vector3(Mathf.Sin(a) * .068f, .017f, Mathf.Cos(a) * .068f),
                new Vector3(.010f, .002f, .008f), amber);
        }

        // Two independent clutch gears that can be tied to puzzle logic later.
        Transform clutchLeft = Gear(p, "LeftClutchGear_NoAmbientMotion",
            new Vector3(-.153f, F3 + .115f, .132f), .073f, 14, copper);
        Transform clutchRight = Gear(p, "RightClutchGear_NoAmbientMotion",
            new Vector3(.154f, F3 + .114f, .133f), .076f, 14, brass);
        Sphere(clutchLeft, "ClutchIndicator", new Vector3(0, .075f, -.018f), .014f, amber);
        Sphere(clutchRight, "ClutchIndicator", new Vector3(0, .076f, -.018f), .014f, cyan);

        Transform clutchLever = Marker(p, "GearLockingLever_PuzzleTarget",
            new Vector3(.202f, F3 + .032f, -.171f));
        Box(clutchLever, "LeverHousing", Vector3.zero,
            new Vector3(.048f, .030f, .034f), metal);
        Rod(clutchLever, "LeverHandle", new Vector3(0, .015f, 0),
            new Vector3(-.014f, .070f, -.006f), .010f, brass);
        Sphere(clutchLever, "LeverKnob", new Vector3(-.014f, .070f, -.006f), .024f, red);
        Marker(anchors, "Gearworks_RotateBridgeGrip", new Vector3(.060f, F3 + .030f, -.066f));
        Marker(anchors, "Gearworks_LockLeverTip", new Vector3(.188f, F3 + .102f, -.177f));
        Marker(nav, "08_GearworksEntry", new Vector3(-.196f, F3 + Foot, -.067f));
        Marker(nav, "09_GearworksBridgeWait", new Vector3(-.117f, F3 + Foot, -.067f));
        Marker(nav, "10_GearworksBridgeExit", new Vector3(.118f, F3 + Foot, -.067f));
        Marker(nav, "11_GearworksRightExit", new Vector3(.214f, F3 + Foot, -.067f));

        // Exposed piston at rear - set dressing only; no player/robot collision.
        Transform piston = Marker(p, "RearMechanicalPiston_Visual",
            new Vector3(0, F3 + .09f, .167f));
        Box(piston, "PistonFrame", new Vector3(0, 0, .016f),
            new Vector3(.090f, .053f, .026f), dark);
        Transform shaft = Marker(piston, "MovingPistonRod", new Vector3(0, 0, -.015f));
        Box(shaft, "PistonFace", Vector3.zero, new Vector3(.077f, .046f, .015f), lightMetal);
        if (animateDecorativeMechanisms)
            shaft.gameObject.AddComponent<ClockworkTowerAmbientMotion>().Configure(
                ClockworkTowerAmbientMotion.MotionType.Piston, Vector3.forward,
                2.2f, .013f);
    }

    private void BuildPendulumVault(Transform p, Transform anchors, Transform nav)
    {
        RouteDeck(p, "01_PendulumWaitDeck", new Vector3(.185f, F4, -.065f),
            .207f, .187f, amber);
        RouteDeck(p, "02_PendulumEscapeDeck", new Vector3(-.215f, F4, -.065f),
            .187f, .187f, mint);
        // A narrow fixed walkway with NO collider, intentionally reserved
        // for the future timing/brake challenge.
        Box(p, "SuspendedDangerWalkway_VisualOnly",
            new Vector3(-.006f, F4 - .013f, -.065f),
            new Vector3(.246f, .024f, .058f), graphite);
        for (int i = 0; i < 8; i++)
            Box(p, "WalkwayHazardStripe_" + i,
                new Vector3(-.113f + i * .030f, F4 + .001f, -.065f),
                new Vector3(.009f, .002f, .056f), i % 2 == 0 ? amber : dark);

        // Pendulum swings behind the robot path so its decorative motion cannot
        // physically knock over the robot or be mistaken for player collision.
        pendulumPivot = Marker(p, "GiantPendulum_Pivot",
            new Vector3(0, F4 + .222f, .136f));
        Rod(pendulumPivot, "PendulumArm", Vector3.zero,
            new Vector3(0, -.219f, 0), .012f, lightMetal);
        Sphere(pendulumPivot, "PendulumMass", new Vector3(0, -.228f, 0), .094f, brass);
        Sphere(pendulumPivot, "PendulumEmissiveCenter", new Vector3(0, -.228f, -.043f),
            .032f, amber);
        Cylinder(p, "PendulumBearing", new Vector3(0, F4 + .222f, .118f),
            .025f, .026f, metal, new Vector3(90, 0, 0));
        if (animateDecorativeMechanisms)
            pendulumPivot.gameObject.AddComponent<ClockworkTowerAmbientMotion>().Configure(
                ClockworkTowerAmbientMotion.MotionType.Pendulum,
                Vector3.forward, 1.8f, 20f);

        Transform brake = Marker(p, "PendulumBrakeMachine",
            new Vector3(.239f, F4, -.173f));
        Box(brake, "BrakeHousing", new Vector3(0, .034f, 0),
            new Vector3(.068f, .066f, .037f), metal);
        Cylinder(brake, "BrakeRosette", new Vector3(0, .073f, -.015f),
            .023f, .010f, brass, new Vector3(90, 0, 0));
        Sphere(brake, "StopPendulumButton", new Vector3(0, .073f, -.025f),
            .026f, red);
        pendulumBrakeButton = Marker(anchors, "Pendulum_BrakeButton",
            new Vector3(.239f, F4 + .073f, -.198f));
        Marker(anchors, "Pendulum_RescuePalmCenter", new Vector3(-.028f, F4 + .035f, -.068f));
        Marker(nav, "12_PendulumWait", new Vector3(.184f, F4 + Foot, -.068f));
        Marker(nav, "13_PendulumMidpoint", new Vector3(.000f, F4 + Foot, -.068f));
        Marker(nav, "14_PendulumEscape", new Vector3(-.207f, F4 + Foot, -.068f));

        // Service lift hint between gearworks and vault; no scripted movement.
        for (int side = -1; side <= 1; side += 2)
            Rod(p, "Zone3To4ServiceLiftRails", new Vector3(.232f, F3, side * .104f),
                new Vector3(.232f, F4, side * .104f), .008f, copper);
        Marker(anchors, "Zone3To4_ServiceLiftUpperTarget", new Vector3(.196f, F4 + .024f, -.065f));
    }

    private void BuildHeartChamber(Transform p, Transform anchors, Transform nav)
    {
        RouteDeck(p, "01_HeartArrivalLeft", new Vector3(-.171f, F5, -.070f),
            .222f, .195f, cyan);
        RouteDeck(p, "02_HeartCoreDeck", new Vector3(.147f, F5, -.070f),
            .255f, .195f, mint);
        // Decorative connector between arrival and core; no floor collider.
        Box(p, "TriPowerLink_VisualOnly", new Vector3(-.015f, F5 - .006f, -.065f),
            new Vector3(.098f, .012f, .038f), metal);
        Box(p, "TriPowerLink_EnergyTrace", new Vector3(-.015f, F5 + .002f, -.065f),
            new Vector3(.096f, .003f, .010f), cyan);

        Transform machine = Marker(p, "ClockworkHeartAssembly",
            new Vector3(.134f, F5 + .098f, .117f));
        CircleRim(machine, "HeartRing_Outer", Vector3.zero, .083f, .008f, brass, 28);
        CircleRim(machine, "HeartRing_Inner", new Vector3(0, 0, -.009f),
            .060f, .006f, gold, 26);
        Gear(machine, "HeartRing_BackGear", new Vector3(0, 0, .024f),
            .072f, 16, lightMetal, 10f);
        heartCore = Marker(machine, "HeartCore_TouchLater",
            new Vector3(0, 0, -.055f));
        Sphere(heartCore, "GlowingHeartCrystal", Vector3.zero, .075f, cyan);
        Sphere(heartCore, "CrystalInnerSpark", new Vector3(0, 0, -.028f), .026f, mint);
        if (animateDecorativeMechanisms)
            heartCore.gameObject.AddComponent<ClockworkTowerAmbientMotion>().Configure(
                ClockworkTowerAmbientMotion.MotionType.Pulse,
                Vector3.up, 2.4f, .065f);

        for (int i = 0; i < 3; i++)
        {
            float x = -.214f + i * .083f;
            Transform channel = Marker(p, "PowerChannel_" + (i + 1),
                new Vector3(x, F5 + .026f, -.174f));
            Box(channel, "ChannelBase", Vector3.zero,
                new Vector3(.059f, .035f, .035f), metal);
            Sphere(channel, "ChannelActivationOrb", new Vector3(0, .029f, -.003f),
                .030f, i == 0 ? cyan : (i == 1 ? amber : mint));
            Marker(anchors, "Heart_Channel_" + (i + 1) + "_Touch",
                new Vector3(x, F5 + .055f, -.177f));
        }
        for (int i = 0; i < 3; i++)
        {
            float x = -.214f + i * .083f;
            Rod(p, "ChannelEnergyConduit_" + i,
                new Vector3(x, F5 + .031f, -.125f),
                new Vector3(.134f, F5 + .090f, .062f), .004f,
                i == 0 ? cyan : i == 1 ? amber : mint);
        }
        Marker(nav, "15_HeartArrival", new Vector3(-.185f, F5 + Foot, -.065f));
        Marker(nav, "16_HeartFinalApproach", new Vector3(.115f, F5 + Foot, -.070f));
        Marker(anchors, "Heart_FinalPalmChargeCenter",
            new Vector3(.166f, F5 + .035f, -.070f));
        Marker(anchors, "Heart_CrystalTouchPoint",
            new Vector3(.134f, F5 + .098f, .062f));

        // Final reveal: a clock dial behind the core.
        CircleRim(p, "ClockFaceOuter", new Vector3(-.154f, F5 + .110f, .166f),
            .078f, .007f, brass, 28);
        CircleRim(p, "ClockFaceInner", new Vector3(-.154f, F5 + .110f, .158f),
            .061f, .004f, lightMetal, 28);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Vector3 pt = new Vector3(-.154f + Mathf.Sin(a) * .069f,
                F5 + .110f + Mathf.Cos(a) * .069f, .151f);
            Box(p, "ClockHourMarker_" + i, pt, new Vector3(.004f, .012f, .004f), gold,
                false, new Vector3(0, 0, -i * 30f));
        }
        Rod(p, "ClockHand_Long", new Vector3(-.154f, F5 + .110f, .148f),
            new Vector3(-.126f, F5 + .148f, .148f), .006f, amber);
        Rod(p, "ClockHand_Short", new Vector3(-.154f, F5 + .110f, .145f),
            new Vector3(-.184f, F5 + .128f, .145f), .009f, gold);
        // Transfer from zone4 to top floor will be a future lift interaction.
        Marker(anchors, "Zone4To5_UpperTarget", new Vector3(-.194f, F5 + .032f, -.065f));
    }

    private void BuildBackgroundMachinery(Transform p)
    {
        // Visible gears occupy the back of the tower to avoid fighting the
        // robot, palms, and all important front-facing interaction consoles.
        Gear(p, "BackGear_LowA", new Vector3(-.211f, .194f, .177f), .068f, 12, copper, 16f);
        Gear(p, "BackGear_LowB", new Vector3(-.095f, .228f, .171f), .052f, 10, brass, -21f);
        Gear(p, "BackGear_LowC", new Vector3(.027f, .218f, .181f), .063f, 14, metal, 15f);
        Gear(p, "BackGear_MidA", new Vector3(.029f, .382f, .180f), .071f, 16, copper, 12f);
        Gear(p, "BackGear_MidB", new Vector3(-.089f, .401f, .181f), .057f, 12, gold, -17f);
        Gear(p, "BackGear_MidC", new Vector3(.175f, .380f, .185f), .051f, 10, lightMetal, 24f);
        Gear(p, "BackGear_HighA", new Vector3(-.068f, .572f, .192f), .059f, 12, brass, 17f);
        Gear(p, "BackGear_HighB", new Vector3(.080f, .578f, .191f), .072f, 16, copper, -14f);
        Gear(p, "BackGear_TopA", new Vector3(-.190f, .779f, .192f), .073f, 14, metal, 16f);
        Gear(p, "BackGear_TopB", new Vector3(.140f, .767f, .194f), .077f, 18, brass, -11f);
        // Copper pipes connect all five tiers.
        foreach (float x in new float[] { -.275f, -.251f, .252f, .274f })
        {
            Rod(p, "MainSteamConduit", new Vector3(x, .065f, .181f),
                new Vector3(x, .914f, .181f), .008f, copper);
            for (int i = 0; i < 5; i++)
            {
                float y = .120f + i * .188f;
                Cylinder(p, "ConduitRing", new Vector3(x, y, .181f),
                    .012f, .011f, brass);
            }
        }
        foreach (float y in new float[] { .208f, .402f, .592f, .785f, .936f })
        {
            Rod(p, "HorizontalCoolantPipe", new Vector3(-.249f, y, .182f),
                new Vector3(.252f, y, .182f), .007f, metal);
            for (int j = 0; j < 7; j++)
                Box(p, "PipeValve", new Vector3(-.223f + j * .071f, y, .175f),
                    new Vector3(.004f, .015f, .004f), j % 2 == 0 ? cyan : brass);
        }
        // Tiny vent panels and status gauges on the front skirt.
        for (int i = 0; i < 16; i++)
            Box(p, "FrontBaseCoolingVent_" + i,
                new Vector3(-.336f + i * .045f, .032f, -.258f),
                new Vector3(.023f, .006f, .003f), i % 4 == 0 ? cyan : lightMetal);
    }

    private void BuildSignageAndLights(Transform p)
    {
        float[] floors = { F1, F2, F3, F4, F5 };
        string[] labels = {
            "01  ARRIVAL DOCK", "02  LIFT SHAFT", "03  GEARWORKS",
            "04  PENDULUM VAULT", "05  HEART CHAMBER"
        };
        for (int i = 0; i < floors.Length; i++)
        {
            float y = floors[i] + .097f;
            Box(p, "ZoneHeaderSteel_" + i, new Vector3(-.190f, y, -.224f),
                new Vector3(.206f, .027f, .006f), metal);
            Box(p, "ZoneHeaderGlow_" + i, new Vector3(-.190f, y - .011f, -.230f),
                new Vector3(.195f, .004f, .002f), i % 2 == 0 ? cyan : amber);
            if (showZoneLabels)
            {
                GameObject label = new GameObject("ZoneText_" + labels[i]);
                label.transform.SetParent(p, false);
                label.transform.localPosition = new Vector3(-.279f, y - .007f, -.233f);
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = labels[i];
                text.fontSize = 46;
                text.characterSize = .0115f;
                text.color = new Color(.84f, .95f, .96f);
                text.anchor = TextAnchor.MiddleLeft;
                text.alignment = TextAlignment.Left;
                // The sign is placed on the open (-Z) face of the miniature.
            }
            if (addAtmosphericLights)
            {
                GameObject go = new GameObject("ZoneAccentLight_" + i);
                go.transform.SetParent(p, false);
                go.transform.localPosition = new Vector3(i % 2 == 0 ? -.12f : .14f,
                    floors[i] + .072f, -.091f);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = .31f;
                l.intensity = .45f;
                l.color = i % 2 == 0 ? new Color(.16f, .91f, 1f) : new Color(1f, .58f, .20f);
                l.shadows = LightShadows.None;
            }
        }
        // A simple glowing route progress spine on the near right edge.
        Rod(p, "PowerProgressSpine", new Vector3(.324f, .080f, -.224f),
            new Vector3(.324f, .988f, -.224f), .007f, dark);
        for (int i = 0; i < floors.Length; i++)
        {
            Sphere(p, "TierProgressLight_" + i,
                new Vector3(.324f, floors[i] + .047f, -.228f),
                .018f, i == 0 ? cyan : amber);
        }
    }

    private void EnsureAssets()
    {
        EnsureFolder("Assets", "PALMBOUND");
        EnsureFolder("Assets/PALMBOUND", "Generated");
        EnsureFolder("Assets/PALMBOUND/Generated", "ClockworkTower");
        EnsureFolder(AssetDir, "Materials");
        EnsureFolder(AssetDir, "Meshes");
        gearMeshes.Clear();

        metal = Mat("01_BluedSteel", new Color(.16f, .24f, .32f), .18f);
        dark = Mat("02_MidnightPanel", new Color(.045f, .071f, .105f), .05f);
        graphite = Mat("03_GraphiteDeck", new Color(.105f, .148f, .180f), .08f);
        brass = Mat("04_AntiqueBrass", new Color(.72f, .49f, .21f), .25f);
        gold = Mat("05_BurnishedGold", new Color(.95f, .72f, .34f), .20f);
        copper = Mat("06_OxidizedCopper", new Color(.47f, .29f, .23f), .13f);
        lightMetal = Mat("07_SilverHardware", new Color(.56f, .68f, .72f), .18f);
        cyan = Mat("08_CyanPower", new Color(.13f, .77f, .92f), 1.25f);
        amber = Mat("09_AmberWarning", new Color(1f, .52f, .13f), 1.05f);
        mint = Mat("10_MintSuccess", new Color(.24f, .96f, .67f), 1.15f);
        red = Mat("11_RedDanger", new Color(.96f, .16f, .12f), 1.0f);
        glass = Mat("12_DarkGlass", new Color(.13f, .31f, .39f), .45f);
        black = Mat("13_BlackPlinth", new Color(.025f, .037f, .052f), .05f);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, child);
    }

    private static Material Mat(string name, Color color, float emission)
    {
        string path = AssetDir + "/Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("PALMBOUND: No compatible shader found. Check your render pipeline.");
                return null;
            }
            m = new Material(shader);
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", emission > .8f ? .52f : .30f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", emission > .8f ? .12f : .58f);
        if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", color * Mathf.Max(0f, emission));
            if (emission > 0.7f) m.EnableKeyword("_EMISSION");
            else m.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private void OnDrawGizmosSelected()
    {
        if (generatedRoot == null) return;
        Gizmos.color = new Color(0f, 1f, .9f, .9f);
        foreach (Transform marker in generatedRoot.GetComponentsInChildren<Transform>(true))
        {
            if (marker.parent == null) continue;
            if (marker.parent.name == "08_RobotWaypoints_NoVisuals")
                Gizmos.DrawWireSphere(marker.position, .009f * towerScale);
            else if (marker.parent.name == "07_InteractionAnchors_NoVisuals")
            {
                Gizmos.color = new Color(1f, .68f, .12f, .85f);
                Gizmos.DrawWireCube(marker.position, Vector3.one * .013f * towerScale);
                Gizmos.color = new Color(0f, 1f, .9f, .9f);
            }
        }
    }
#endif // UNITY_EDITOR
}
