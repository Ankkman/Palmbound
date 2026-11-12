using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Hold a tracked fingertip near the console to charge and open the generated Arrival gate.
/// No physics, collisions, or XR rig motion are involved.</summary>
[DisallowMultipleComponent]
public sealed class ArrivalGateController : MonoBehaviour
{
    [Header("Generated Tower")]
    [SerializeField] private ClockworkTowerGenerator tower;
    [Header("Hand Tracking")]
    [SerializeField] private Transform trackedIndexTip;
    [SerializeField, Min(0.005f)] private float touchRadius = 0.055f;
    [SerializeField, Min(0.1f)] private float secondsToCharge = 1.6f;
    [SerializeField] private bool allowKeyboardGInEditor = true;
    [Header("Gate Animation")]
    [SerializeField, Min(0.01f)] private float openHeightMeters = 0.15f;
    [SerializeField, Min(0.01f)] private float openSpeedMetersPerSecond = 0.17f;
    [Header("Feedback")]
    [SerializeField] private bool createChargeIndicator = true;

    public bool IsCharged { get; private set; }
    public bool IsFullyOpen { get; private set; }
    public float Charge01 { get { return Mathf.Clamp01(chargeSeconds / Mathf.Max(0.01f, secondsToCharge)); } }
    public bool IsBeingHeld { get; private set; }

    private float chargeSeconds;
    private Transform gate;
    private Transform switchAnchor;
    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private Transform fillBar;
    private Renderer switchRenderer;
    private Material runtimeSwitchMaterial;
    private Color baseColor;
    private bool initialized;

    public void Initialize(ClockworkTowerGenerator source)
    {
        if (initialized) return;
        tower = source;
        if (tower == null || tower.ArrivalGatePanel == null || tower.ArrivalGateSwitch == null)
        {
            Debug.LogError("PALMBOUND Arrival Gate: Tower has not been built or generated anchors are missing.", this);
            enabled = false;
            return;
        }
        gate = tower.ArrivalGatePanel;
        switchAnchor = tower.ArrivalGateSwitch;
        closedLocalPosition = gate.localPosition;
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(gate.parent.lossyScale.y));
        openLocalPosition = closedLocalPosition + Vector3.up * (openHeightMeters / parentScaleY);
        gate.localPosition = closedLocalPosition;
        IsCharged = false;
        IsFullyOpen = false;
        IsBeingHeld = false;
        chargeSeconds = 0f;
        if (createChargeIndicator) CreateChargeBar();
        Transform visibleButton = FindByName(tower.GeneratedRoot, "PressAndHoldButton");
        if (visibleButton != null)
        {
            switchRenderer = visibleButton.GetComponent<Renderer>();
            if (switchRenderer != null)
            {
                runtimeSwitchMaterial = new Material(switchRenderer.sharedMaterial);
                switchRenderer.material = runtimeSwitchMaterial;
                baseColor = runtimeSwitchMaterial.color;
            }
        }
        initialized = true;
    }

    private void Start() { if (!initialized) Initialize(tower); }

    private void Update()
    {
        if (!initialized || gate == null || switchAnchor == null) return;
        bool touching = trackedIndexTip != null &&
            Vector3.Distance(trackedIndexTip.position, switchAnchor.position) <= touchRadius;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        if (allowKeyboardGInEditor && Keyboard.current != null && Keyboard.current.gKey.isPressed)
            touching = true;
#endif
        IsBeingHeld = !IsCharged && touching;
        if (!IsCharged)
        {
            chargeSeconds = Mathf.Clamp(chargeSeconds + (touching ? Time.deltaTime : -Time.deltaTime * 0.45f), 0f, secondsToCharge);
            if (chargeSeconds >= secondsToCharge)
            {
                IsCharged = true;
                Debug.Log("PALMBOUND: Arrival gate charged. Opening.", this);
            }
        }
        if (IsCharged)
        {
            float localStep = openSpeedMetersPerSecond * Time.deltaTime /
                Mathf.Max(0.0001f, Mathf.Abs(gate.parent.lossyScale.y));
            gate.localPosition = Vector3.MoveTowards(gate.localPosition, openLocalPosition, localStep);
            IsFullyOpen = Vector3.Distance(gate.localPosition, openLocalPosition) < 0.0005f;
        }
        if (fillBar != null)
        {
            fillBar.localScale = new Vector3(Mathf.Max(0.001f, 0.06f * Charge01), 0.008f, 0.006f);
            fillBar.localPosition = new Vector3(-0.03f + 0.03f * Charge01, 0.025f, -0.012f);
        }
        if (runtimeSwitchMaterial != null)
            runtimeSwitchMaterial.color = Color.Lerp(baseColor, new Color(0.15f, 1f, 0.8f), Charge01);
    }

    private void CreateChargeBar()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "RUNTIME_GateChargeBar";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(switchAnchor, false);
        go.transform.localPosition = new Vector3(0f, 0.025f, -0.012f);
        go.transform.localScale = new Vector3(0.02f, 0.008f, 0.006f);
        Renderer r = go.GetComponent<Renderer>();
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = new Color(0.12f, 0.95f, 0.85f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0f, 0.45f, 0.5f));
        r.material = material;
        fillBar = go.transform;
        // The parent is an unscaled marker; use a visually wide, small bar.
        fillBar.localScale = new Vector3(0.02f, 0.008f, 0.006f);
    }

    private static Transform FindByName(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (runtimeSwitchMaterial != null) Destroy(runtimeSwitchMaterial);
    }
}
