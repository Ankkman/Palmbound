using System.Collections;
using UnityEngine;

/// <summary>
/// Places the editable workshop parent in front of the XR head, before the
/// level controller's Start() initializes the robot position.
/// Does not move the Camera Rig or its tracking children.
/// </summary>
[DefaultExecutionOrder(-500)]
public class WorkshopPlacementController : MonoBehaviour
{
    [Header("Required scene references")]
    [SerializeField] private Transform headset;
    [SerializeField] private Transform workshopRoot;
    [SerializeField] private Behaviour levelController;

    [Header("Seated placement in world meters")]
    [Min(0.25f)] [SerializeField] private float forwardDistance = 0.65f;
    [SerializeField] private float verticalOffset = -0.30f;
    [SerializeField] private float trackingWarmupSeconds = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool logPlacement = true;

    private void Awake()
    {
        if (levelController != null)
            levelController.enabled = false;
    }

    private IEnumerator Start()
    {
        
        if (headset == null || workshopRoot == null || levelController == null)
        {
            Debug.LogError("PALMBOUND Placement: Assign Headset, Workshop Root, and Level Controller.", this);
            yield break;
        }

        // XR poses may not be available in the very first frame of Play Mode.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, trackingWarmupSeconds));
        yield return new WaitForEndOfFrame();

        PlaceWorkshop();
        // Enabling after placement lets BrokenWorkshopLevelController.Start()
        // initialize the robot at the final generated start waypoint.
        levelController.enabled = true;
    }

    [ContextMenu("Place Workshop Now")]
    public void PlaceWorkshop()
    {
        if (headset == null || workshopRoot == null)
        {
            Debug.LogError("PALMBOUND Placement: Missing Headset or Workshop Root.", this);
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(headset.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        // Workshop local -Z is its open/front side, aimed toward the player.
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        workshopRoot.SetPositionAndRotation(
            headset.position + forward * forwardDistance + Vector3.up * verticalOffset,
            rotation);

        if (logPlacement)
            Debug.Log($"PALMBOUND Placement: Head={headset.position:F3}, " +
                      $"Workshop={workshopRoot.position:F3}, " +
                      $"Forward={forward:F3}", this);
    }
}