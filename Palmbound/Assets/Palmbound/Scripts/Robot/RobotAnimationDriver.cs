
using UnityEngine;

public class RobotAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator robotAnimator;
    [SerializeField] private Transform robotRoot;

    [Header("Animation Settings")]
    [SerializeField] private float movementThreshold = 0.015f;

    private Vector3 previousPosition;
    private bool initialized;

    private static readonly int IsWalkingHash =
        Animator.StringToHash("IsWalking");

    private void Start()
    {
        if (robotRoot == null)
            robotRoot = transform;

        previousPosition = robotRoot.position;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized ||
            robotAnimator == null ||
            robotRoot == null ||
            Time.deltaTime <= 0f)
            return;

        Vector3 currentPosition = robotRoot.position;

        float speed =
            Vector3.Distance(
                currentPosition,
                previousPosition
            ) / Time.deltaTime;

        bool isWalking =
            speed > movementThreshold;

        robotAnimator.SetBool(
            IsWalkingHash,
            isWalking
        );

        previousPosition = currentPosition;
    }
}
