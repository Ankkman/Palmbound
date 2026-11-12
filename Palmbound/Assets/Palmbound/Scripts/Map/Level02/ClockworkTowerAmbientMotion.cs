using UnityEngine;

/// <summary>
/// Decorative, kinematic-only motion for Level 02. Never moves the XR rig,
/// player, or robot. Does not use physics. The game can disable this per puzzle.
/// </summary>
[DisallowMultipleComponent]
public sealed class ClockworkTowerAmbientMotion : MonoBehaviour
{
    public enum MotionType { Spin, Pendulum, Piston, Pulse }

    [SerializeField] private MotionType motion = MotionType.Spin;
    [SerializeField] private Vector3 localAxis = Vector3.forward;
    [SerializeField] private float speed = 20f;
    [SerializeField] private float amplitude = 18f;
    [SerializeField] private float phase = 0f;

    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private float elapsed;

    public void Configure(MotionType type, Vector3 axis, float motionSpeed,
        float motionAmplitude = 18f, float motionPhase = 0f)
    {
        motion = type;
        localAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
        speed = motionSpeed;
        amplitude = motionAmplitude;
        phase = motionPhase;
        CaptureBasePose();
    }

    private void Awake() { CaptureBasePose(); }
    private void OnEnable() { CaptureBasePose(); elapsed = 0f; }

    private void CaptureBasePose()
    {
        originalRotation = transform.localRotation;
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        switch (motion)
        {
            case MotionType.Spin:
                transform.localRotation = originalRotation *
                    Quaternion.AngleAxis(elapsed * speed + phase, localAxis);
                break;
            case MotionType.Pendulum:
                transform.localRotation = originalRotation *
                    Quaternion.AngleAxis(Mathf.Sin(elapsed * speed + phase) * amplitude, localAxis);
                break;
            case MotionType.Piston:
                transform.localPosition = originalPosition +
                    localAxis * (Mathf.Sin(elapsed * speed + phase) * amplitude);
                break;
            case MotionType.Pulse:
                float s = 1f + amplitude * Mathf.Sin(elapsed * speed + phase);
                transform.localScale = originalScale * Mathf.Max(0.05f, s);
                break;
        }
    }
}
