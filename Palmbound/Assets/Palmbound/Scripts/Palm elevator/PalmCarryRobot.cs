
using UnityEngine;

public class PalmCarryRobot : MonoBehaviour
{
    private enum CarryState
    {
        Waiting,
        Boarding,
        Carrying,
        Disembarking,
        Completed
    }

    [Header("Left Hand")]
    [SerializeField] private PalmDetector palmDetector;
    [SerializeField] private Transform palmJoint;
    [SerializeField] private Transform wrist;
    [SerializeField] private Transform indexMetacarpal;
    [SerializeField] private Transform littleMetacarpal;

    [Header("Puzzle Points")]
    [SerializeField] private Transform boardingPoint;
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Transform landingExitPoint;

    [Header("Palm Orientation")]
    [SerializeField] private bool reversePalmNormal;
    [SerializeField, Range(0f, 90f)]
    private float maxPalmTilt = 40f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.25f;
    [SerializeField] private float robotHeight = 0.07f;
    [SerializeField] private float followSharpness = 15f;

    [Header("Detection")]
    [SerializeField] private float boardingRadius = 0.12f;
    [SerializeField] private float landingRadius = 0.12f;
    [SerializeField] private float maxStableHandSpeed = 0.20f;

    [Header("Timing")]
    [SerializeField] private float boardingHoldTime = 0.4f;
    [SerializeField] private float landingHoldTime = 0.5f;
    [SerializeField] private float trackingGraceTime = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;
    [SerializeField] private bool logPalmAngle = false;

    private CarryState state = CarryState.Waiting;
    private Vector3 startPosition;

    private float boardingTimer;
    private float landingTimer;
    private float trackingLostTimer;
    private float nextLogTime;

    private Vector3 previousPalmPosition;
    private bool hasPreviousPalmPosition;
    private float palmSpeed;

    private bool palmValid;
    private float palmTilt;

    private void Start()
    {
        startPosition = transform.position;
        Log("Waiting for stable, upward-facing palm.");
    }

    private void LateUpdate()
    {
        UpdatePalmStatus();

        if (boardingPoint == null ||
            landingPoint == null ||
            landingExitPoint == null)
            return;

        switch (state)
        {
            case CarryState.Waiting:
                UpdateWaiting();
                break;

            case CarryState.Boarding:
                UpdateBoarding();
                break;

            case CarryState.Carrying:
                UpdateCarrying();
                break;

            case CarryState.Disembarking:
                UpdateDisembarking();
                break;

            case CarryState.Completed:
                break;
        }
    }

    private void UpdatePalmStatus()
    {
        palmValid = false;

        if (palmDetector == null ||
            !palmDetector.IsPalmOpen ||
            palmJoint == null ||
            wrist == null ||
            indexMetacarpal == null ||
            littleMetacarpal == null)
        {
            hasPreviousPalmPosition = false;
            return;
        }

        Vector3 palmPosition = palmJoint.position;

        if (hasPreviousPalmPosition &&
            Time.deltaTime > 0f)
        {
            palmSpeed = Vector3.Distance(
                palmPosition,
                previousPalmPosition
            ) / Time.deltaTime;
        }
        else
        {
            palmSpeed = 0f;
        }

        previousPalmPosition = palmPosition;
        hasPreviousPalmPosition = true;

        Vector3 knuckleCenter =
            (indexMetacarpal.position +
             littleMetacarpal.position) * 0.5f;

        Vector3 fingerAxis =
            knuckleCenter - wrist.position;

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

        palmTilt = Vector3.Angle(
            normal,
            Vector3.up
        );

        palmValid = palmTilt <= maxPalmTilt;

        if (logPalmAngle && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + 1f;

            Debug.Log(
                $"PALMBOUND Elevator | " +
                $"Palm tilt: {palmTilt:F1}° | " +
                $"Valid: {palmValid} | " +
                $"Hand speed: {palmSpeed:F2} m/s"
            );
        }
    }

    private bool HandIsStable()
    {
        return palmValid &&
               palmSpeed <= maxStableHandSpeed;
    }

    private void UpdateWaiting()
    {
        if (!HandIsStable())
        {
            boardingTimer = 0f;
            return;
        }

        bool nearBoarding =
            Vector3.Distance(
                palmJoint.position,
                boardingPoint.position
            ) <= boardingRadius;

        if (!nearBoarding)
        {
            boardingTimer = 0f;
            return;
        }

        boardingTimer += Time.deltaTime;

        if (boardingTimer >= boardingHoldTime)
        {
            boardingTimer = 0f;
            trackingLostTimer = 0f;
            state = CarryState.Boarding;

            Log("Boarding palm.");
        }
    }

    private void UpdateBoarding()
    {
        if (!CheckPalmContinuity())
            return;

        Vector3 target =
            palmJoint.position +
            Vector3.up * robotHeight;

        MoveRobot(target);

        if (Vector3.Distance(
            transform.position, target) <= 0.015f)
        {
            state = CarryState.Carrying;
            Log("Robot is now on your palm.");
        }
    }

    private void UpdateCarrying()
    {
        if (!CheckPalmContinuity())
            return;

        Vector3 target =
            palmJoint.position +
            Vector3.up * robotHeight;

        // Frame-rate-independent smoothing.
        float followAmount =
            1f - Mathf.Exp(
                -followSharpness * Time.deltaTime
            );

        transform.position = Vector3.Lerp(
            transform.position,
            target,
            followAmount
        );

        bool nearLanding =
            Vector3.Distance(
                palmJoint.position,
                landingPoint.position
            ) <= landingRadius;

        if (nearLanding && HandIsStable())
        {
            landingTimer += Time.deltaTime;
        }
        else
        {
            landingTimer = 0f;
        }

        if (landingTimer >= landingHoldTime)
        {
            landingTimer = 0f;
            state = CarryState.Disembarking;

            Log("Landing confirmed. Disembarking.");
        }
    }

    private void UpdateDisembarking()
    {
        // Once the landing is confirmed, the robot
        // finishes walking onto the destination.
        MoveRobot(landingExitPoint.position);

        if (Vector3.Distance(
            transform.position,
            landingExitPoint.position) <= 0.005f)
        {
            transform.position =
                landingExitPoint.position;

            state = CarryState.Completed;

            Log("Palm Elevator puzzle completed!");
        }
    }

    private bool CheckPalmContinuity()
    {
        if (palmValid)
        {
            trackingLostTimer = 0f;
            return true;
        }

        trackingLostTimer += Time.deltaTime;

        if (trackingLostTimer >= trackingGraceTime)
            ResetRobot();

        return false;
    }

    private void MoveRobot(Vector3 destination)
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime
        );
    }

    private void ResetRobot()
    {
        transform.position = startPosition;

        state = CarryState.Waiting;

        boardingTimer = 0f;
        landingTimer = 0f;
        trackingLostTimer = 0f;

        Log("Palm lost. Robot returned to start.");
    }

    private void Log(string message)
    {
        if (logStateChanges)
            Debug.Log("PALMBOUND: " + message);
    }

    private void OnDrawGizmosSelected()
    {
        if (boardingPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                boardingPoint.position,
                boardingRadius
            );
        }

        if (landingPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                landingPoint.position,
                landingRadius
            );
        }
    }
}
