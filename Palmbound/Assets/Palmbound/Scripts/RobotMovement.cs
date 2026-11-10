
using UnityEngine;

public class RobotMovement : MonoBehaviour
{
    private enum RobotState
    {
        Approaching,
        WaitingForBridge,
        Crossing,
        Completed
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.12f;
    [SerializeField] private float waitingPointX = -0.25f;
    [SerializeField] private float destinationX = 0.4f;

    [Header("Bridge")]
    [SerializeField] private PalmBridgeController palmBridge;

    private RobotState currentState;
    private Vector3 startingPosition;

    [Header("Tracking Grace Period")]
    [SerializeField] private float bridgeLossGraceTime = 0.35f;

    private float bridgeLossTimer;

    private void Start()
    {
        startingPosition = transform.localPosition;
        currentState = RobotState.Approaching;
    }

    private void Update()
    {
        switch (currentState)
        {
            case RobotState.Approaching:
                MoveTowardsX(waitingPointX);

                if (HasReachedX(waitingPointX))
                {
                    currentState = RobotState.WaitingForBridge;
                    Debug.Log("PALMBOUND: Robot waiting for palm.");
                }
                break;

            case RobotState.WaitingForBridge:
                if (palmBridge != null &&
                    palmBridge.IsBridgeReady)
                {
                    currentState = RobotState.Crossing;
                    Debug.Log("PALMBOUND: Robot crossing!");
                }
                break;

            case RobotState.Crossing:
                if (palmBridge == null || !palmBridge.IsBridgeReady)
                {
                    bridgeLossTimer += Time.deltaTime;

                    if (bridgeLossTimer >= bridgeLossGraceTime)
                    {
                        bridgeLossTimer = 0f;
                        ResetRobot();
                    }

                    return;
                }

                bridgeLossTimer = 0f;

                MoveTowardsX(destinationX);

                if (HasReachedX(destinationX))
                {
                    currentState = RobotState.Completed;
                    Debug.Log("PALMBOUND: Puzzle completed!");
                }
                break;

            case RobotState.Completed:
                // Robot waits on the destination platform.
                break;
        }
    }

    private void MoveTowardsX(float targetX)
    {
        Vector3 current = transform.localPosition;

        Vector3 target = new Vector3(
            targetX,
            current.y,
            current.z
        );

        transform.localPosition = Vector3.MoveTowards(
            current,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    private bool HasReachedX(float targetX)
    {
        return Mathf.Abs(
            transform.localPosition.x - targetX
        ) < 0.001f;
    }

    private void ResetRobot()
    {
        transform.localPosition = startingPosition;
        currentState = RobotState.Approaching;

        Debug.Log(
            "PALMBOUND: Bridge lost! Robot restarting."
        );
    }
}
