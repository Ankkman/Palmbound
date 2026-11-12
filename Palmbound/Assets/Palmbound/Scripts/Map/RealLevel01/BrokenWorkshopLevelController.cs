using UnityEngine;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PALMBOUND Level 01. Uses the anchors created by BrokenWorkshopGenerator.
/// The animated Robot is a separate world-space root; only this script moves it.
/// Requires ConfigureGeneratedBridge(Transform, GameObject) on PalmBridgeController.
/// </summary>
public class BrokenWorkshopLevelController : MonoBehaviour
{
    private enum Stage
    {
        WalkToGate, WaitForSwitch, WalkToBridge, WaitForBridge,
        CrossBridge, WalkToBoarding, WaitForPalm, BoardPalm,
        CarryRobot, Disembark, WalkToGoal, WaitForTerminal, Complete
    }

    [Header("Scene references (assign once)")]
    [SerializeField] private BrokenWorkshopGenerator workshop;
    [SerializeField] private Transform robot;
    [SerializeField] private Animator robotAnimator;
    [SerializeField] private PalmBridgeController bridgeController;
    [SerializeField] private PalmDetector palmDetector;
    [SerializeField] private Transform palmJoint;
    [Tooltip("Tracked index fingertip used for button interaction; use a real tracked joint, not a synthetic visual marker.")]
    [SerializeField] private Transform indexTip;

    [Header("Movement in world meters")]
    [SerializeField] private float walkSpeed = 0.14f;
    [SerializeField] private float reachDistance = 0.012f;
    [Tooltip("Vertical distance from palm joint to robot gameplay root during carrying.")]
    [SerializeField] private float palmRobotHeight = 0.07f;
    [SerializeField] private float palmFollowSharpness = 12f;

    [Header("Hand interactions")]
    [SerializeField] private float touchRadius = 0.035f;
    [SerializeField] private float boardingRadius = 0.12f;
    [SerializeField] private float landingRadius = 0.12f;
    [SerializeField] private float maxPalmTilt = 45f;
    [SerializeField] private float maxStablePalmSpeed = 0.25f;
    [SerializeField] private float boardingHoldSeconds = 0.35f;
    [SerializeField] private float landingHoldSeconds = 0.45f;
    [SerializeField] private float handGraceSeconds = 0.55f;

    [Header("Gate")]
    [SerializeField] private float gateOpeningHeight = 0.14f;
    [SerializeField] private float gateOpeningSpeed = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logStages = true;
    [SerializeField] private bool keyboardTestInEditor = true;

    private Stage stage;
    private Transform gateButton;
    private Transform terminalCore;
    private Transform boardingApproach;
    private Vector3 gateClosedLocalPosition;
    private Vector3 gateOpenLocalPosition;
    private bool gateUnlocked;
    private float holdTimer;
    private float lostTimer;
    private Vector3 previousPalmPosition;
    private bool hasPreviousPalm;
    private float palmSpeed;
    private bool palmFacingUp;
    private bool handOpen;
    private bool didInitialize;

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");

    private void Start()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        // The generator owns the positions and the subtle bridge artwork.
        // No hand-entered BridgeCenter or PalmBridgeVisual required.
        bridgeController.ConfigureGeneratedBridge(
            workshop.BridgeCenter, workshop.PalmBridgeVisual);

        gateButton = FindNamedChild(workshop.GateSwitch, "Touch_Button");
        terminalCore = FindNamedChild(workshop.EnergyTerminal, "Energy_Core");
        boardingApproach = FindNamedChild(workshop.GeneratedRoot, "BoardingApproachPoint");

        // The original generator does not necessarily contain BoardingApproachPoint.
        // Falling back to CheckpointPoint is safe for this initial level.
        if (boardingApproach == null)
            boardingApproach = workshop.CheckpointPoint;

        if (gateButton == null || terminalCore == null)
        {
            Debug.LogError("PALMBOUND: Generated switch button or energy core was not found.", this);
            enabled = false;
            return;
        }

        gateClosedLocalPosition = workshop.GatePanel.localPosition;
        float localOpening = gateOpeningHeight /
            Mathf.Max(0.0001f, workshop.GatePanel.parent.lossyScale.y);
        gateOpenLocalPosition = gateClosedLocalPosition + Vector3.up * localOpening;
        workshop.GatePanel.localPosition = gateClosedLocalPosition;
        gateUnlocked = false;

        robot.position = workshop.RobotStartPoint.position;
        robotAnimator.SetBool(IsWalking, false);
        didInitialize = true;
        ChangeStage(Stage.WalkToGate, "Robot is waking up and heading to the gate.");
    }

    private bool ValidateReferences()
    {
        bool valid = workshop != null && robot != null && robotAnimator != null &&
            bridgeController != null && palmDetector != null && palmJoint != null &&
            workshop.GeneratedRoot != null && workshop.BridgeCenter != null &&
            workshop.PalmBridgeVisual != null && workshop.RobotStartPoint != null &&
            workshop.GateStopPoint != null && workshop.BridgeApproachPoint != null &&
            workshop.CheckpointPoint != null && workshop.BoardingPoint != null &&
            workshop.LandingPoint != null && workshop.LandingExitPoint != null &&
            workshop.GoalPoint != null && workshop.GatePanel != null &&
            workshop.GateSwitch != null && workshop.EnergyTerminal != null;
        if (!valid)
            Debug.LogError("PALMBOUND: Missing robot, hand, or generated workshop reference. Rebuild map if anchor fields are empty.", this);
        if (indexTip == null)
            Debug.LogWarning("PALMBOUND: IndexTip not assigned. Touch switches will not work on Quest; keyboard testing may still work in Editor.", this);
        return valid;
    }

    private void Update()
    {
        if (!didInitialize) return;
        UpdatePalmTracking();
        if (gateUnlocked)
        {
            workshop.GatePanel.localPosition = Vector3.MoveTowards(
                workshop.GatePanel.localPosition,
                gateOpenLocalPosition,
                gateOpeningSpeed * Time.deltaTime /
                Mathf.Max(0.0001f, workshop.GatePanel.parent.lossyScale.y));
        }
    }

    private void LateUpdate()
    {
        if (!didInitialize) return;

        switch (stage)
        {
            case Stage.WalkToGate:
                WalkTo(workshop.GateStopPoint.position, Stage.WaitForSwitch,
                    "Touch the amber workshop switch.");
                break;

            case Stage.WaitForSwitch:
                SetWalking(false);
                if (Touched(gateButton, KeyCode.G))
                {
                    gateUnlocked = true;
                    ChangeStage(Stage.WalkToBridge, "Gate unlocked. Moving to broken walkway.");
                }
                break;

            case Stage.WalkToBridge:
                if (!GateOpenEnough())
                {
                    SetWalking(false);
                    break;
                }
                WalkTo(workshop.BridgeApproachPoint.position, Stage.WaitForBridge,
                    "Offer your palm across the broken walkway.");
                break;

            case Stage.WaitForBridge:
                SetWalking(false);
                if (bridgeController.IsBridgeReady)
                    ChangeStage(Stage.CrossBridge, "Crossing the palm bridge.");
                break;

            case Stage.CrossBridge:
                if (!bridgeController.IsBridgeReady)
                {
                    SetWalking(false);
                    break;
                }
                WalkTo(workshop.CheckpointPoint.position, Stage.WalkToBoarding,
                    "Bridge complete. Walking to the boarding dock.");
                break;

            case Stage.WalkToBoarding:
                WalkTo(boardingApproach.position, Stage.WaitForPalm,
                    "Hold an open, upward palm by the boarding dock.");
                break;

            case Stage.WaitForPalm:
                SetWalking(false);
                if (StablePalmNear(workshop.BoardingPoint.position, boardingRadius))
                    holdTimer += Time.deltaTime;
                else
                    holdTimer = 0f;
                if (holdTimer >= boardingHoldSeconds)
                {
                    holdTimer = 0f;
                    lostTimer = 0f;
                    ChangeStage(Stage.BoardPalm, "Robot boarding your palm.");
                }
                break;

            case Stage.BoardPalm:
                if (!KeepPalmOrRecover()) break;
                SetWalking(true);
                if (MoveRobot(GetPalmCarryPosition()))
                {
                    SetWalking(false);
                    ChangeStage(Stage.CarryRobot, "Carry the robot to the raised reactor deck.");
                }
                break;

            case Stage.CarryRobot:
                if (!KeepPalmOrRecover()) break;
                SetWalking(false);
                robot.position = Vector3.Lerp(robot.position, GetPalmCarryPosition(),
                    1f - Mathf.Exp(-palmFollowSharpness * Time.deltaTime));
                if (StablePalmNear(workshop.LandingPoint.position, landingRadius))
                    holdTimer += Time.deltaTime;
                else
                    holdTimer = 0f;
                if (holdTimer >= landingHoldSeconds)
                {
                    holdTimer = 0f;
                    ChangeStage(Stage.Disembark, "Robot stepping onto the upper deck.");
                }
                break;

            case Stage.Disembark:
                WalkTo(workshop.LandingExitPoint.position, Stage.WalkToGoal,
                    "Heading toward the energy terminal.");
                break;

            case Stage.WalkToGoal:
                WalkTo(workshop.GoalPoint.position, Stage.WaitForTerminal,
                    "Touch the glowing energy core to finish.");
                break;

            case Stage.WaitForTerminal:
                SetWalking(false);
                if (Touched(terminalCore, KeyCode.E))
                {
                    ChangeStage(Stage.Complete, "LEVEL 01 COMPLETE!");
                }
                break;

            case Stage.Complete:
                SetWalking(false);
                break;
        }
    }

    private bool GateOpenEnough()
    {
        return gateUnlocked && Vector3.Distance(
            workshop.GatePanel.localPosition, gateOpenLocalPosition) <= 0.008f;
    }

    private bool MoveRobot(Vector3 destination)
    {
        robot.position = Vector3.MoveTowards(
            robot.position, destination, walkSpeed * Time.deltaTime);
        return Vector3.Distance(robot.position, destination) <= reachDistance;
    }

    private void WalkTo(Vector3 destination, Stage next, string nextMessage)
    {
        SetWalking(true);
        if (!MoveRobot(destination)) return;
        robot.position = destination;
        SetWalking(false);
        ChangeStage(next, nextMessage);
    }

    private void UpdatePalmTracking()
    {
        handOpen = palmDetector.IsPalmOpen;
        palmFacingUp = false;
        if (!handOpen)
        {
            hasPreviousPalm = false;
            return;
        }

        Vector3 p = palmJoint.position;
        palmSpeed = hasPreviousPalm && Time.deltaTime > 0f
            ? Vector3.Distance(previousPalmPosition, p) / Time.deltaTime : 0f;
        previousPalmPosition = p;
        hasPreviousPalm = true;

        // Reuse the corrected, calibrated normal of the working bridge script.
        // The bridge controller runs in LateUpdate, so this can be one frame behind.
        palmFacingUp = Vector3.Angle(bridgeController.PalmNormal, Vector3.up)
            <= maxPalmTilt;
    }

    private bool StablePalmNear(Vector3 target, float radius)
    {
        return handOpen && palmFacingUp && palmSpeed <= maxStablePalmSpeed &&
            Vector3.Distance(palmJoint.position, target) <= radius;
    }

    private bool KeepPalmOrRecover()
    {
        if (handOpen && palmFacingUp)
        {
            lostTimer = 0f;
            return true;
        }
        SetWalking(false);
        lostTimer += Time.deltaTime;
        if (lostTimer >= handGraceSeconds)
        {
            robot.position = boardingApproach.position;
            holdTimer = 0f;
            lostTimer = 0f;
            ChangeStage(Stage.WaitForPalm, "Palm lost. Try boarding again.");
        }
        return false;
    }

    private Vector3 GetPalmCarryPosition()
    {
        return palmJoint.position + Vector3.up * palmRobotHeight;
    }

    private bool Touched(Transform target, KeyCode testKey)
    {
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        if (keyboardTestInEditor && Keyboard.current != null)
        {
            if (testKey == KeyCode.G && Keyboard.current.gKey.wasPressedThisFrame)
                return true;
            if (testKey == KeyCode.E && Keyboard.current.eKey.wasPressedThisFrame)
                return true;
        }
#endif
        return indexTip != null && target != null &&
            Vector3.Distance(indexTip.position, target.position) <= touchRadius;
    }

    private void SetWalking(bool value)
    {
        if (robotAnimator != null)
            robotAnimator.SetBool(IsWalking, value);
    }

    private void ChangeStage(Stage next, string message)
    {
        stage = next;
        if (logStages) Debug.Log("PALMBOUND | " + message, this);
    }

    private static Transform FindNamedChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform result = FindNamedChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
