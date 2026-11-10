
using UnityEngine;

public class PalmDetector : MonoBehaviour
{
    [Header("Hand Tracking")]
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRSkeleton leftSkeleton;

    [Header("Detection")]
    [Range(0f, 1f)]
    [SerializeField] private float openThreshold = 0.7f;

    public bool IsPalmOpen { get; private set; }

    private void Update()
    {
        if (leftHand == null || leftSkeleton == null)
        {
            IsPalmOpen = false;
            return;
        }

        if (!leftHand.IsTracked ||
            !leftHand.IsDataValid ||
            !leftSkeleton.IsDataValid)
        {
            IsPalmOpen = false;
            return;
        }

        bool indexOpen =
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index)
            < (1f - openThreshold);

        bool middleOpen =
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle)
            < (1f - openThreshold);

        bool ringOpen =
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring)
            < (1f - openThreshold);

        bool pinkyOpen =
            leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky)
            < (1f - openThreshold);

        bool palmOpen =
            indexOpen && middleOpen && ringOpen && pinkyOpen;

        if (palmOpen != IsPalmOpen)
        {
            IsPalmOpen = palmOpen;

            Debug.Log(IsPalmOpen
                ? "PALMBOUND: Palm OPEN"
                : "PALMBOUND: Palm CLOSED");
        }
    }
}
