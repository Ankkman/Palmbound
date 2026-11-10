
using UnityEngine;

public class PalmBridgeController : MonoBehaviour
{
    [Header("Left Hand Tracking")]
    [SerializeField] private PalmDetector palmDetector;
    [SerializeField] private Transform wrist;
    [SerializeField] private Transform palmJoint;
    [SerializeField] private Transform indexMetacarpal;
    [SerializeField] private Transform littleMetacarpal;

    [Header("Bridge")]
    [SerializeField] private GameObject bridgeVisual;
    [SerializeField] private Transform bridgeCenter;
    [SerializeField] private Renderer bridgeRenderer;

    [Header("Position Tolerance (Meters)")]
    [SerializeField] private float horizontalTolerance = 0.05f;
    [SerializeField] private float heightTolerance = 0.06f;
    [SerializeField] private float depthTolerance = 0.06f;

    [Header("Palm Orientation")]
    [SerializeField] private bool reversePalmNormal = false;

    [SerializeField, Range(0f, 90f)]
    private float maxPalmTilt = 40f;

    [SerializeField, Range(0f, 90f)]
    private float maxFingerAlignmentAngle = 50f;

    [Header("Debug Markers")]
    [SerializeField] private Transform palmDebugMarker;

    [Tooltip("Optional wrist bone of the visible hand mesh.")]
    [SerializeField] private Transform visualWristReference;

    [SerializeField] private Transform visualDebugMarker;

    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logConditions = true;

    public bool IsBridgeReady { get; private set; }
    public Vector3 PalmCenter { get; private set; }
    public Vector3 PalmNormal { get; private set; }

    private bool hasValidPalm;
    private bool correctPosition;
    private bool correctTilt;
    private bool correctAlignment;
    private float nextLogTime;

    private void Awake()
    {
        if (bridgeVisual != null)
            bridgeVisual.SetActive(false);
    }

    // Evaluate after normal Update methods.
    private void LateUpdate()
    {
        if (!HasValidHand())
        {
            hasValidPalm = false;
            SetBridgeReady(false);

            if (palmDebugMarker != null)
                palmDebugMarker.gameObject.SetActive(false);

            if (visualDebugMarker != null)
                visualDebugMarker.gameObject.SetActive(false);

            return;
        }


        Vector3 wristPosition = wrist.position;
        Vector3 indexPosition = indexMetacarpal.position;
        Vector3 littlePosition = littleMetacarpal.position;

        Vector3 knuckleCenter =
            (indexPosition + littlePosition) * 0.5f;

        // Use the actual tracked palm joint.
        PalmCenter = palmJoint.position;

        Vector3 fingerAxis =
            knuckleCenter - wristPosition;

        Vector3 acrossPalm =
            indexPosition - littlePosition;

        Vector3 normal =
            Vector3.Cross(fingerAxis, acrossPalm);

        if (normal.sqrMagnitude < 0.000001f)
        {
            hasValidPalm = false;
            SetBridgeReady(false);
            return;
        }

        PalmNormal = normal.normalized;

        if (reversePalmNormal)
            PalmNormal = -PalmNormal;

        hasValidPalm = true;

        // Tracked palm marker: yellow.
        if (palmDebugMarker != null)
        {
            palmDebugMarker.gameObject.SetActive(true);
            palmDebugMarker.position = PalmCenter;
        }

        // Optional comparison marker:
        // follows a reference from the visible hand mesh.
        if (visualDebugMarker != null)
        {
            bool hasVisual = visualWristReference != null;

            visualDebugMarker.gameObject.SetActive(hasVisual);

            if (hasVisual)
                visualDebugMarker.position =
                    visualWristReference.position;
        }

        // Palm position relative to the gap.
        Vector3 offset =
            bridgeCenter.InverseTransformPoint(PalmCenter);

        correctPosition =
            Mathf.Abs(offset.x) <= horizontalTolerance &&
            Mathf.Abs(offset.y) <= heightTolerance &&
            Mathf.Abs(offset.z) <= depthTolerance;

        float palmTilt =
            Vector3.Angle(PalmNormal, Vector3.up);

        correctTilt = palmTilt <= maxPalmTilt;

        Vector3 projectedFingerAxis =
            Vector3.ProjectOnPlane(
                fingerAxis,
                Vector3.up
            );

        correctAlignment = false;

        if (projectedFingerAxis.sqrMagnitude > 0.000001f)
        {
            float alignment = Mathf.Abs(
                Vector3.Dot(
                    projectedFingerAxis.normalized,
                    bridgeCenter.right.normalized
                )
            );

            correctAlignment =
                alignment >= Mathf.Cos(
                    maxFingerAlignmentAngle *
                    Mathf.Deg2Rad
                );
        }

        bool ready =
            palmDetector.IsPalmOpen &&
            correctPosition &&
            correctTilt &&
            correctAlignment;

        SetBridgeReady(ready);

        if (logConditions && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + 0.5f;

            Debug.Log(
                $"PALMBOUND | " +
                $"Position: {correctPosition} | " +
                $"Tilt: {correctTilt} ({palmTilt:F1}) | " +
                $"Alignment: {correctAlignment} | " +
                $"Offset: {offset.ToString("F3")}"
            );
        }
    }

    private bool HasValidHand()
    {
        return palmDetector != null &&
               palmDetector.IsPalmOpen &&
               wrist != null &&
               palmJoint != null &&
               indexMetacarpal != null &&
               littleMetacarpal != null &&
               bridgeCenter != null;
    }

    private void SetBridgeReady(bool ready)
    {
        if (IsBridgeReady != ready)
        {
            IsBridgeReady = ready;

            Debug.Log(ready
                ? "PALMBOUND: BRIDGE READY"
                : "PALMBOUND: BRIDGE LOST");
        }

        if (bridgeVisual != null)
            bridgeVisual.SetActive(ready);

        if (ready && bridgeRenderer != null)
            bridgeRenderer.material.color = Color.cyan;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || bridgeCenter == null)
            return;

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            bridgeCenter.position,
            new Vector3(
                horizontalTolerance * 2f,
                heightTolerance * 2f,
                depthTolerance * 2f
            )
        );

        if (!Application.isPlaying || !hasValidPalm)
            return;

        Gizmos.color =
            correctPosition ? Color.green : Color.red;

        Gizmos.DrawSphere(PalmCenter, 0.012f);

        Gizmos.color =
            correctTilt ? Color.green : Color.yellow;

        Gizmos.DrawRay(
            PalmCenter,
            PalmNormal * 0.12f
        );

        Gizmos.color = Color.white;

        Gizmos.DrawLine(
            PalmCenter,
            bridgeCenter.position
        );
    }
}
