using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// One-time Edit Mode alignment tool. Put this on the gameplay Robot root.
/// Move a FeetReference child of the animated visual to the bottom of the feet,
/// then run the context menu command. The root will represent the feet.
/// </summary>
public class RobotFeetAligner : MonoBehaviour
{
    [SerializeField] private Transform robotVisual;
    [SerializeField] private Transform feetReference;

    [ContextMenu("Align Visual Feet To Robot Root")]
    private void AlignFeet()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("PALMBOUND: Align feet in Edit Mode, not Play Mode.", this);
            return;
        }
        if (robotVisual == null || feetReference == null ||
            !feetReference.IsChildOf(robotVisual))
        {
            Debug.LogError("PALMBOUND: Assign RobotVisual and a FeetReference under RobotVisual.", this);
            return;
        }

#if UNITY_EDITOR
        Undo.RecordObject(robotVisual, "Align Robot Feet");
#endif
        // Shifts the entire visual without moving the gameplay root or the rig bones.
        Vector3 offset = transform.position - feetReference.position;
        robotVisual.position += offset;
#if UNITY_EDITOR
        EditorUtility.SetDirty(robotVisual);
#endif
        Debug.Log("PALMBOUND: Visual aligned. FeetReference now coincides with Robot root.", this);
    }
}
