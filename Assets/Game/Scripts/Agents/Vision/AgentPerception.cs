using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AgentPerception : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 15f;
    [SerializeField] private float fovAngle = 90f;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float scanInterval = 0.5f;
    [SerializeField] private float memoryDuration = 5f;
    
     // number of points per collider to test
    [SerializeField, Range(1, 10)] private int visibilitySamples = 6;

    [Header("Layers")]
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private LayerMask obstacleMask = ~0;

    private readonly Dictionary<Interactable, float> _memory = new();
    private readonly List<Interactable> _visibleInteractables = new();

    public IReadOnlyList<Interactable> VisibleInteractables => _visibleInteractables;
    public IReadOnlyCollection<Interactable> RememberedInteractables => _memory.Keys;

    void Start()
    {
        InvokeRepeating(nameof(FindVisibleTargets), 0f, scanInterval);
    }

    void Update()
    {
        DecayMemory();
    }

    void FindVisibleTargets()
    {
        var colliders = Physics.OverlapSphere(transform.position, visionRange, targetMask);

        var uniqueTargets = new HashSet<Interactable>();
        foreach (var col in colliders)
        {
            var target = col.GetComponent<Interactable>();
            if (target != null)
                uniqueTargets.Add(target);
        }

        _visibleInteractables.Clear();

        var eyePos = transform.position + Vector3.up * eyeHeight;

        foreach (var target in uniqueTargets)
        {
            if (target == null) continue;

            var targetCollider = target.GetComponent<Collider>();
            if (targetCollider == null) continue;

            var targetCenter = targetCollider.bounds.center;
            var dir = (targetCenter - eyePos).normalized;
            var angle = Vector3.Angle(transform.forward, dir);
            if (angle > fovAngle * 0.5f) continue;

            if (IsTargetVisible(eyePos, targetCollider))
            {
                _visibleInteractables.Add(target);
                _memory[target] = memoryDuration;
            }
        }
    }

    bool IsTargetVisible(Vector3 eyePos, Collider targetCollider)
    {
        var bounds = targetCollider.bounds;
        var points = GenerateSamplePoints(bounds, visibilitySamples);

        bool visible = false;
        foreach (var p in points)
        {
            var dir = (p - eyePos).normalized;
            var dist = Vector3.Distance(eyePos, p);

            if (!Physics.Raycast(eyePos, dir, dist, obstacleMask))
            {
                Debug.DrawLine(eyePos, p, Color.green, 0.3f);
                visible = true;
            }
            else
            {
                Debug.DrawLine(eyePos, p, Color.red, 0.3f);
            }
        }

        return visible;
    }

    List<Vector3> GenerateSamplePoints(Bounds b, int samples)
    {
        var points = new List<Vector3>
        {
            b.center,
            // Corners of the bounding box
            b.center + new Vector3(b.extents.x, 0, b.extents.z),
            b.center + new Vector3(-b.extents.x, 0, b.extents.z),
            b.center + new Vector3(b.extents.x, 0, -b.extents.z),
            b.center + new Vector3(-b.extents.x, 0, -b.extents.z),

            // Top and mid-high sample
            b.center + Vector3.up * b.extents.y
        };
        if (samples > 6)
        {
            points.Add(b.center + Vector3.up * (b.extents.y * 0.5f));
        }

        return points;
    }

    void DecayMemory()
    {
        var keys = _memory.Keys.ToList();
        foreach (var key in keys)
        {
            _memory[key] -= Time.deltaTime;
            if (_memory[key] <= 0f)
                _memory.Remove(key);
        }
    }

    void OnDrawGizmosSelected()
    {
        var eyePos = transform.position + Vector3.up * eyeHeight;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(eyePos, visionRange);

        var left = Quaternion.Euler(0, -fovAngle / 2f, 0) * transform.forward;
        var right = Quaternion.Euler(0, fovAngle / 2f, 0) * transform.forward;
        Gizmos.DrawRay(eyePos, left * visionRange);
        Gizmos.DrawRay(eyePos, right * visionRange);

        if (!Application.isPlaying) return;

        foreach (var target in _memory.Keys)
        {
            if (target == null) continue;
            Gizmos.color = _visibleInteractables.Contains(target) ? Color.green : Color.gray;
            Gizmos.DrawLine(eyePos, target.transform.position);
        }
    }
}
