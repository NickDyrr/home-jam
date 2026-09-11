using UnityEngine;

/// <summary>
/// Fixed isometric camera. Angle is set once from pitch/yaw and never rotates.
/// Position smoothly follows the target at a fixed offset.
/// Orthographic size eases out a little when the player is outside the home.
/// </summary>
[RequireComponent(typeof(Camera))]
public class IsoCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float pitch = 35f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float distance = 20f;
    [SerializeField] private float followSpeed = 8f;

    [Header("Home nudge")]
    [SerializeField] private float insideSize = 7f;
    [SerializeField] private float outsideSize = 9f;
    [SerializeField] private float zoomSpeed = 3f;

    private Camera cam;
    private Quaternion fixedRotation;
    private Vector3 offset;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        fixedRotation = Quaternion.Euler(pitch, yaw, 0f);
        offset = fixedRotation * Vector3.back * distance;
        transform.rotation = fixedRotation;
        if (target != null) transform.position = target.position + offset;
        cam.orthographicSize = insideSize;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset, t);
        transform.rotation = fixedRotation;

        bool home = HomeZone.Instance == null || HomeZone.Instance.PlayerIsHome;
        float wanted = home ? insideSize : outsideSize;
        float tz = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, wanted, tz);
    }
}
