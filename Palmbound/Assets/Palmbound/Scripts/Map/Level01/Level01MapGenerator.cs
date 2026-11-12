
using UnityEngine;

public class Level01MapGenerator : MonoBehaviour
{
    [Header("Optional Materials")]
    [SerializeField] private Material platformMaterial;
    [SerializeField] private Material accentMaterial;

    [Header("Generated References")]
    public Transform startPoint;
    public Transform checkpointPoint;
    public Transform boardingPoint;
    public Transform landingPoint;
    public Transform landingExitPoint;
    public Transform goalPoint;

    [ContextMenu("Generate Level 01")]
    public void GenerateLevel()
    {
        Transform old = transform.Find("GeneratedMap");

        if (old != null)
        {
            Debug.LogWarning(
                "GeneratedMap already exists. " +
                "Delete it manually before regenerating.");
            return;
        }

        GameObject map = new GameObject("GeneratedMap");
        map.transform.SetParent(transform, false);

        // Three platforms with two distinct gaps.
        CreatePlatform(map.transform, "StartPlatform",
            new Vector3(-0.45f, 1f, 0.8f),
            new Vector3(0.30f, 0.10f, 0.35f));

        CreatePlatform(map.transform, "CheckpointPlatform",
            new Vector3(0f, 1f, 0.8f),
            new Vector3(0.30f, 0.10f, 0.35f));

        CreatePlatform(map.transform, "GoalPlatform",
            new Vector3(0.65f, 1.25f, 0.8f),
            new Vector3(0.30f, 0.10f, 0.35f));

        // Robot root positions, not palm positions.
        startPoint = CreatePoint(map.transform,
            "StartPoint", -0.45f, 1.12f, 0.8f);

        checkpointPoint = CreatePoint(map.transform,
            "CheckpointPoint", 0f, 1.12f, 0.8f);

        boardingPoint = CreatePoint(map.transform,
            "BoardingPoint", 0.12f, 1.05f, 0.8f);

        landingPoint = CreatePoint(map.transform,
            "LandingPoint", 0.49f, 1.30f, 0.8f);

        landingExitPoint = CreatePoint(map.transform,
            "LandingExitPoint", 0.57f, 1.37f, 0.8f);

        goalPoint = CreatePoint(map.transform,
            "GoalPoint", 0.68f, 1.37f, 0.8f);

        CreateMarker(map.transform, "GoalMarker",
            new Vector3(0.68f, 1.45f, 0.8f));

        Debug.Log("PALMBOUND: Level 01 map generated.");
    }

    private void CreatePlatform(
        Transform parent,
        string objectName,
        Vector3 position,
        Vector3 scale)
    {
        GameObject platform =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        platform.name = objectName;
        platform.transform.SetParent(parent, false);
        platform.transform.localPosition = position;
        platform.transform.localScale = scale;

        if (platformMaterial != null)
            platform.GetComponent<Renderer>().sharedMaterial =
                platformMaterial;
    }

    private Transform CreatePoint(
        Transform parent,
        string pointName,
        float x, float y, float z)
    {
        GameObject point = new GameObject(pointName);
        point.transform.SetParent(parent, false);
        point.transform.localPosition =
            new Vector3(x, y, z);

        return point.transform;
    }

    private void CreateMarker(
        Transform parent,
        string objectName,
        Vector3 position)
    {
        GameObject marker =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        marker.name = objectName;
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = position;
        marker.transform.localScale =
            Vector3.one * 0.045f;

        Collider markerCollider =
            marker.GetComponent<Collider>();

        if (markerCollider != null)
            DestroyImmediate(markerCollider);

        if (accentMaterial != null)
            marker.GetComponent<Renderer>().sharedMaterial =
                accentMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        if (boardingPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                boardingPoint.position, 0.12f);
        }

        if (landingPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                landingPoint.position, 0.12f);
        }
    }
}
