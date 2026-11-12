
using UnityEngine;

public class Level01Controller : MonoBehaviour
{
    private enum LevelState
    {
        WaitingForBridge,
        CrossingBridge,
        WaitingForBoarding,
        Boarding,
        Carrying,
        Disembarking,
        WalkingToGoal,
        Completed
    }

    [Header("Robot")]
    [SerializeField] private Transform robot;
    [SerializeField] private Animator robotAnimator;

    [Header("Existing Interaction")]
    [SerializeField] private PalmBridgeController bridge;
    [SerializeField] private PalmDetector palmDetector;

    [Header("Left Hand Joints")]
    [SerializeField] private Transform palmJoint;
    [SerializeField] private Transform wrist;
    [SerializeField] private Transform indexMetacarpal;
    [SerializeField] private Transform littleMetacarpal;

    [Header("Map")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform checkpointPoint;
    [SerializeField] private Transform boardingPoint;
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Transform landingExitPoint;
    [SerializeField] private Transform goalPoint;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 0.25f;
    [SerializeField] private float robotHeight = 0.07f;
    [SerializeField] private float followSharpness = 15f;

    [Header("Hand Placement")]
    [SerializeField] private bool reversePalmNormal = true;
    [SerializeField] private float maxPalmTilt = 40f;
    [SerializeField] private float boardingRadius = 0.12f;
    [SerializeField] private float landingRadius = 0.12f;
    [SerializeField] private float maxStableHandSpeed = 0.20f;

    [Header("Timing")]
    [SerializeField] private float boardingHoldTime = 0.4f;
    [SerializeField] private float landingHoldTime = 0.5f;
    [SerializeField] private float trackingGraceTime = 0.5f;

    private LevelState state = LevelState.WaitingForBridge;
    private float holdTimer;
    private float lostTimer;
    private Vector3 previousPalmPosition;
    private bool hasPreviousPalm;
    private float palmSpeed;
    private bool palmValid;
    private bool walking;

    private static readonly int IsWalkingHash =
        Animator.StringToHash("IsWalking");

    private void Start()
    {
        if (!ReferencesReady())
        {
            Debug.LogError(
                "PALMBOUND: Missing Level 01 references.");
            enabled = false;
            return;
        }

        robot.position = startPoint.position;
        SetWalking(false);
        Debug.Log("PALMBOUND: LEVEL 01 STARTED");
    }

    private bool ReferencesReady()
    {
        return robot != null &&
               robotAnimator != null &&
               bridge != null &&
               palmDetector != null &&
               palmJoint != null &&
               wrist != null &&
               indexMetacarpal != null &&
               littleMetacarpal != null &&
               startPoint != null &&
               checkpointPoint != null &&
               boardingPoint != null &&
               landingPoint != null &&
               landingExitPoint != null &&
               goalPoint != null;
    }

    private void LateUpdate()
    {
        UpdatePalm();

        switch (state)
        {
            case LevelState.WaitingForBridge:
                if (bridge.IsBridgeReady)
                {
                    SetState(LevelState.CrossingBridge,
                        "Crossing the bridge");
                    SetWalking(true);
                }
                break;

            case LevelState.CrossingBridge:
                // Pause if the player's bridge disappears.
                if (!bridge.IsBridgeReady)
                {
                    SetWalking(false);
                    return;
                }

                SetWalking(true);
                if (MoveTo(checkpointPoint.position))
                {
                    SetWalking(false);
                    SetState(LevelState.WaitingForBoarding,
                        "Checkpoint reached. Offer your palm");
                }
                break;

            case LevelState.WaitingForBoarding:
                if (IsHandReadyNear(
                    boardingPoint, boardingRadius))
                {
                    holdTimer += Time.deltaTime;

                    if (holdTimer >= boardingHoldTime)
                    {
                        holdTimer = 0f;
                        lostTimer = 0f;
                        SetWalking(true);
                        SetState(LevelState.Boarding,
                            "Boarding the palm");
                    }
                }
                else
                    holdTimer = 0f;
                break;

            case LevelState.Boarding:
                if (!CheckPalmContinuity())
                    return;

                if (MoveTo(GetPalmRobotPosition()))
                {
                    SetWalking(false);
                    SetState(LevelState.Carrying,
                        "Carry the robot to the higher platform");
                }
                break;

            case LevelState.Carrying:
                if (!CheckPalmContinuity())
                    return;

                float t = 1f - Mathf.Exp(
                    -followSharpness * Time.deltaTime);

                robot.position = Vector3.Lerp(
                    robot.position,
                    GetPalmRobotPosition(),
                    t);

                if (IsHandReadyNear(
                    landingPoint, landingRadius))
                {
                    holdTimer += Time.deltaTime;

                    if (holdTimer >= landingHoldTime)
                    {
                        holdTimer = 0f;
                        SetWalking(true);
                        SetState(LevelState.Disembarking,
                            "Stepping onto destination");
                    }
                }
                else
                    holdTimer = 0f;
                break;

            case LevelState.Disembarking:
                if (MoveTo(landingExitPoint.position))
                {
                    SetState(LevelState.WalkingToGoal,
                        "Approaching goal");
                }
                break;

            case LevelState.WalkingToGoal:
                if (MoveTo(goalPoint.position))
                {
                    SetWalking(false);
                    SetState(LevelState.Completed,
                        "LEVEL 01 COMPLETE!");
                }
                break;

            case LevelState.Completed:
                break;
        }
    }

    private void UpdatePalm()
    {
        palmValid = false;

        if (!palmDetector.IsPalmOpen)
        {
            hasPreviousPalm = false;
            return;
        }

        Vector3 position = palmJoint.position;

        if (hasPreviousPalm && Time.deltaTime > 0f)
            palmSpeed = Vector3.Distance(
                position, previousPalmPosition
            ) / Time.deltaTime;
        else
            palmSpeed = 0f;

        previousPalmPosition = position;
        hasPreviousPalm = true;

        Vector3 fingerAxis =
            (indexMetacarpal.position +
             littleMetacarpal.position) * 0.5f -
            wrist.position;

        Vector3 acrossPalm =
            indexMetacarpal.position -
            littleMetacarpal.position;

        Vector3 normal =
            Vector3.Cross(fingerAxis, acrossPalm);

        if (normal.sqrMagnitude < 0.000001f)
            return;

        normal.Normalize();

        if (reversePalmNormal)
            normal = -normal;

        float tilt = Vector3.Angle(
            normal, Vector3.up);

        palmValid = tilt <= maxPalmTilt;
    }

    private bool IsHandReadyNear(
        Transform point, float radius)
    {
        return palmValid &&
               palmSpeed <= maxStableHandSpeed &&
               Vector3.Distance(
                   palmJoint.position,
                   point.position) <= radius;
    }

    private bool CheckPalmContinuity()
    {
        if (palmValid)
        {
            lostTimer = 0f;

            if (state == LevelState.Boarding)
                SetWalking(true);

            return true;
        }

        SetWalking(false);
        lostTimer += Time.deltaTime;

        if (lostTimer >= trackingGraceTime)
        {
            robot.position = checkpointPoint.position;
            holdTimer = 0f;
            lostTimer = 0f;
            SetState(LevelState.WaitingForBoarding,
                "Hand lost. Try boarding again");
        }

        return false;
    }

    private Vector3 GetPalmRobotPosition()
    {
        return palmJoint.position +
               Vector3.up * robotHeight;
    }

    private bool MoveTo(Vector3 target)
    {
        robot.position = Vector3.MoveTowards(
            robot.position,
            target,
            walkSpeed * Time.deltaTime);

        return Vector3.Distance(
            robot.position, target) <= 0.01f;
    }

    private void SetWalking(bool value)
    {
        if (walking == value)
            return;

        walking = value;
        robotAnimator.SetBool(IsWalkingHash, value);
    }

    private void SetState(
        LevelState newState, string message)
    {
        state = newState;
        Debug.Log("PALMBOUND: " + message);
    }
}
