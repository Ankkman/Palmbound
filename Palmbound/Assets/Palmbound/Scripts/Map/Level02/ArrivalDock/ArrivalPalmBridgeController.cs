using UnityEngine;

/// <summary>Checks for an open palm at the generated bridge marker and shows a temporary
/// safe route across the gap. Never moves the XR rig or the robot itself.</summary>
[DisallowMultipleComponent]
public sealed class ArrivalPalmBridgeController : MonoBehaviour
{
    public enum PalmUpAxis { Up, Down, Forward, Back, Right, Left }

    [Header("Generated Tower")]
    [SerializeField] private ClockworkTowerGenerator tower;
    [Header("Hand Tracking")]
    [SerializeField] private PalmDetector palmDetector;
    [SerializeField] private Transform trackedPalmJoint;
    [SerializeField] private PalmUpAxis palmNormalAxis = PalmUpAxis.Up;
    [SerializeField] private bool requirePalmFacingUp = false;
    [SerializeField, Range(10f, 90f)] private float maxUpwardTiltDegrees = 70f;
    [SerializeField, Min(0.02f)] private float palmProximityMeters = 0.105f;
    [SerializeField, Min(0f)] private float readyHoldSeconds = 0.20f;
    [Header("Visuals")]
    [SerializeField] private bool showTemporaryLightBridge = true;

    public bool IsBridgeReady { get; private set; }
    public bool IsPalmPresent { get; private set; }
    public bool HasCompleted { get; private set; }

    private Transform bridgeAnchor;
    private Transform bridgeVisual;
    private float stableTime;
    private bool initialized;

    public void Initialize(ClockworkTowerGenerator source)
    {
        if (initialized) return;
        tower = source;
        bridgeAnchor = tower != null ? tower.ArrivalBridgeCenter : null;
        if (bridgeAnchor == null || trackedPalmJoint == null || palmDetector == null)
        {
            Debug.LogError("PALMBOUND Arrival Bridge: Missing tower bridge anchor, palm joint or PalmDetector.", this);
            enabled = false;
            return;
        }
        stableTime = 0f;
        IsBridgeReady = false;
        IsPalmPresent = false;
        HasCompleted = false;
        if (showTemporaryLightBridge) CreateBridgeVisual();
        initialized = true;
    }

    private void Start() { if (!initialized) Initialize(tower); }

    private void Update()
    {
        if (!initialized) return;
        bool nearby = Vector3.Distance(trackedPalmJoint.position, bridgeAnchor.position) <= palmProximityMeters;
        bool orientationOkay = !requirePalmFacingUp ||
            Vector3.Angle(PalmNormal(), Vector3.up) <= maxUpwardTiltDegrees;
        IsPalmPresent = palmDetector.IsPalmOpen && nearby && orientationOkay;
        stableTime = IsPalmPresent ? stableTime + Time.deltaTime : 0f;
        IsBridgeReady = HasCompleted || (IsPalmPresent && stableTime >= readyHoldSeconds);
        if (bridgeVisual != null)
            bridgeVisual.gameObject.SetActive(IsBridgeReady && !HasCompleted);
    }

    public void MarkCompleted()
    {
        HasCompleted = true;
        IsBridgeReady = true;
        if (bridgeVisual != null) bridgeVisual.gameObject.SetActive(false);
    }

    public bool CanContinueCrossing()
    {
        return IsPalmPresent || HasCompleted;
    }

    private Vector3 PalmNormal()
    {
        switch (palmNormalAxis)
        {
            case PalmUpAxis.Down: return -trackedPalmJoint.up;
            case PalmUpAxis.Forward: return trackedPalmJoint.forward;
            case PalmUpAxis.Back: return -trackedPalmJoint.forward;
            case PalmUpAxis.Right: return trackedPalmJoint.right;
            case PalmUpAxis.Left: return -trackedPalmJoint.right;
            default: return trackedPalmJoint.up;
        }
    }

    private void CreateBridgeVisual()
    {
        // Generated waypoints and the gap run along local X at floor 0.130m.
        Transform root = tower.GeneratedRoot;
        if (root == null) return;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "RUNTIME_ArrivalPalmCrossingGuide";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(-0.022f, 0.130f, -0.065f);
        go.transform.localScale = new Vector3(0.16f, 0.004f, 0.052f);
        Renderer r = go.GetComponent<Renderer>();
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        m.color = new Color(0.1f, 0.8f, 0.95f, 0.7f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.02f, 0.35f, 0.5f));
        r.material = m;
        bridgeVisual = go.transform;
        go.SetActive(false);
    }
}
