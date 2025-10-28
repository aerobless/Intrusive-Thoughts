using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LocationDescription : MonoBehaviour
{
    [SerializeField, TextArea(5, 20)]
    public string description;

#if UNITY_EDITOR
    [SerializeField, Min(0.1f)]
    private float gizmoScale = 1f;

    [SerializeField, Min(0f)]
    private float gizmoHoverHeight = 1.2f;

    [SerializeField]
    private Color gizmoColor = new Color(0.87f, 0.18f, 0.36f, 0.9f);

    private void OnDrawGizmos()
    {
        // Keep the drawing simple: sphere for the head, cone for the point, and a soft ground disc.
        const float baseHeadDiameter = 0.45f;
        const float baseConeHeight = 0.6f;
        const float baseShadowRadius = 0.35f;

        float headDiameter = baseHeadDiameter * gizmoScale;
        float coneHeight = baseConeHeight * gizmoScale;
        float shadowRadius = baseShadowRadius * gizmoScale;

        Vector3 pinTip = transform.position + Vector3.up * gizmoHoverHeight;
        Vector3 coneBase = pinTip + Vector3.up * coneHeight;
        Vector3 headCenter = coneBase + Vector3.up * (headDiameter * 0.5f);
        Vector3 shadowCenter = transform.position + Vector3.up * 0.01f;

        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        Color previousColor = Handles.color;
        Handles.color = gizmoColor;

        Handles.ConeHandleCap(
            controlID: 0,
            position: coneBase,
            rotation: Quaternion.LookRotation(Vector3.down),
            size: coneHeight,
            eventType: EventType.Repaint);

        Handles.SphereHandleCap(
            controlID: 0,
            position: headCenter,
            rotation: Quaternion.identity,
            size: headDiameter,
            eventType: EventType.Repaint);

        Handles.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.35f);
        Handles.DrawSolidDisc(shadowCenter, Vector3.up, shadowRadius);

        Handles.color = previousColor;
        Handles.zTest = previousZTest;
    }
#endif
}
