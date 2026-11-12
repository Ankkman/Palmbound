using UnityEngine;

/// <summary>
/// PALMBOUND Clockwork Tower - Zone 01 only.
/// Sequence: spawn -> approach gate -> charge button -> open gate -> approach gap ->
/// offer palm -> cross -> lift dock checkpoint. Does not move the Camera Rig.
/// </summary>
[DisallowMultipleComponent]
public sealed class ClockworkArrivalDockLevelController : MonoBehaviour
{
    private enum Stage
    {
        WalkToGate, WaitForGate, WalkToGap, WaitForPalm,
        Crossing, WalkToLift, Finished
    }

    [Header("Required Scene References")]
    [SerializeField] private ClockworkTowerGenerator tower;
    [SerializeField] private Transform robot;
    [SerializeField] private Animator robotAnimator;
    [SerializeField] private ArrivalGateController gate;
    [SerializeField] private ArrivalPalmBridgeController bridge;
    [Header("World-space movement")]
    [SerializeField, Min(0.01f)] private float walkingSpeedMetersPerSecond = 0.12f;
    [SerializeField, Min(0.002f)] private float crossingSpeedMetersPerSecond = 0.065f;
    [SerializeField, Min(0.001f)] private float arriveDistanceMeters = 0.007f;
    [SerializeField] private bool faceTravelDirection = true;
    [SerializeField, Min(0f)] private float rotationDegreesPerSecond = 400f;
    [Header("Guidance")]
    [SerializeField] private bool createInWorldInstruction = true;
    [SerializeField] private bool logStageChanges = true;
    [SerializeField] private string animatorWalkBool = "IsWalking";

    public bool ArrivalDockCompleted { get { return stage == Stage.Finished && initialized; } }

    private Transform gateStop;
    private Transform gapApproach;
    private Transform gapExit;
    private Transform liftEntrance;
    private Stage stage;
    private bool initialized;
    private TextMesh instruction;
    private int walkingHash;

    private void Start()
    {
        if (!Validate())
        {
            enabled = false;
            return;
        }
        walkingHash = Animator.StringToHash(animatorWalkBool);
        // All marker world positions follow ClockworkTowerRoot automatically.
        gateStop = FindByName(tower.GeneratedRoot, "02_ArrivalGateStopPoint");
        gapApproach = FindByName(tower.GeneratedRoot, "03_ArrivalBridgeApproach");
        gapExit = FindByName(tower.GeneratedRoot, "04_ArrivalBridgeExit");
        // This marker is slightly farther right on the same deck; inspect it in Scene view.
        liftEntrance = FindByName(tower.GeneratedRoot, "05_LiftLowerApproach");
        // Do not use LiftBoardingPoint here: that is elevated for the hand, not the robot feet.
        if (gateStop == null || gapApproach == null || gapExit == null || liftEntrance == null)
        {
            Debug.LogError("PALMBOUND Zone01: Missing generated navigation markers. Rebuild tower and inspect names.", this);
            enabled = false;
            return;
        }
        // Initialize interactions explicitly so their state is ready at scene start.
        gate.Initialize(tower);
        bridge.Initialize(tower);
        robot.position = tower.RobotStartPoint.position;
        SetWalk(false);
        initialized = true;
        if (createInWorldInstruction) CreateInstruction();
        ChangeStage(Stage.WalkToGate, "A tiny traveler enters the Clockwork Tower...");
    }

    private bool Validate()
    {
        if (tower == null || tower.GeneratedRoot == null || tower.RobotStartPoint == null ||
            robot == null || robotAnimator == null || gate == null || bridge == null)
        {
            Debug.LogError("PALMBOUND Zone01: Assign Tower, Robot root, Robot Animator, Arrival Gate and Arrival Bridge.", this);
            return false;
        }
        // Do not accidentally control any object that contains a camera.
        foreach (Camera c in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c != null && (robot == c.transform || c.transform.IsChildOf(robot)))
            {
                Debug.LogError("PALMBOUND Zone01: ROBOT reference includes the XR camera. Assign the standalone robot root!", this);
                return false;
            }
        }
        return true;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        switch (stage)
        {
            case Stage.WalkToGate:
                if (WalkTo(gateStop.position, walkingSpeedMetersPerSecond))
                    ChangeStage(Stage.WaitForGate, "Hold the amber console button to open the gate.");
                break;
            case Stage.WaitForGate:
                SetWalk(false);
                if (instruction != null && !gate.IsCharged)
                    instruction.text = "HOLD THE AMBER BUTTON  " + Mathf.RoundToInt(gate.Charge01 * 100f) + "%";
                if (gate.IsFullyOpen)
                    ChangeStage(Stage.WalkToGap, "Gate open! Follow the little robot to the broken track.");
                break;
            case Stage.WalkToGap:
                if (WalkTo(gapApproach.position, walkingSpeedMetersPerSecond))
                    ChangeStage(Stage.WaitForPalm, "Offer an OPEN PALM across the glowing bridge markers.");
                break;
            case Stage.WaitForPalm:
                SetWalk(false);
                if (bridge.IsBridgeReady)
                    ChangeStage(Stage.Crossing, "Keep your palm by the gap while the robot crosses!");
                break;
            case Stage.Crossing:
                if (!bridge.CanContinueCrossing())
                {
                    SetWalk(false);
                    if (instruction != null) instruction.text = "KEEP YOUR PALM IN PLACE TO CONTINUE";
                    break;
                }
                if (WalkTo(gapExit.position, crossingSpeedMetersPerSecond))
                {
                    bridge.MarkCompleted();
                    ChangeStage(Stage.WalkToLift, "Rescue complete! The lift shaft awaits.");
                }
                break;
            case Stage.WalkToLift:
                if (WalkTo(liftEntrance.position, walkingSpeedMetersPerSecond))
                    ChangeStage(Stage.Finished, "ZONE 01 COMPLETE! Next: Lift Shaft.");
                break;
            case Stage.Finished:
                SetWalk(false);
                break;
        }
    }

    private bool WalkTo(Vector3 destination, float speed)
    {
        Vector3 delta = destination - robot.position;
        if (delta.magnitude <= arriveDistanceMeters)
        {
            robot.position = destination;
            SetWalk(false);
            return true;
        }
        SetWalk(true);
        if (faceTravelDirection)
        {
            Vector3 planar = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (planar.sqrMagnitude > 0.00001f)
            {
                Quaternion look = Quaternion.LookRotation(planar.normalized, Vector3.up);
                robot.rotation = Quaternion.RotateTowards(robot.rotation, look,
                    rotationDegreesPerSecond * Time.deltaTime);
            }
        }
        robot.position = Vector3.MoveTowards(robot.position, destination, speed * Time.deltaTime);
        return false;
    }

    private void SetWalk(bool walk)
    {
        if (robotAnimator != null) robotAnimator.SetBool(walkingHash, walk);
    }

    private void ChangeStage(Stage next, string msg)
    {
        stage = next;
        if (instruction != null) instruction.text = msg.ToUpperInvariant();
        if (logStageChanges) Debug.Log("PALMBOUND ZONE 01 | " + msg, this);
    }

    private void CreateInstruction()
    {
        // Unlit text, parented to the tower, in front of the Arrival Dock.
        GameObject go = new GameObject("RUNTIME_ArrivalDockInstruction");
        go.transform.SetParent(tower.GeneratedRoot, false);
        go.transform.localPosition = new Vector3(-0.05f, 0.26f, -0.265f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.0017f;
        instruction = go.AddComponent<TextMesh>();
        instruction.fontSize = 30;
        instruction.characterSize = 0.6f;
        instruction.anchor = TextAnchor.MiddleCenter;
        instruction.alignment = TextAlignment.Center;
        instruction.color = new Color(0.40f, 0.98f, 1f);
        instruction.text = "PALMBOUND";
    }

    private static Transform FindByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;
        foreach (Transform c in root)
        {
            Transform match = FindByName(c, targetName);
            if (match != null) return match;
        }
        return null;
    }
}
