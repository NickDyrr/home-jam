using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The one gun. Six rounds, slow reload, ammo only comes from survivors who
/// make it home. Left click fires toward the mouse cursor; the player snaps
/// to face the shot. A hit stuns and knocks back a stalker; it never kills.
/// Recoil kick and muzzle flash are procedural on the gun socket.
/// </summary>
public class Pistol : MonoBehaviour
{
    public static Pistol Instance { get; private set; }

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int startReserve = 6;
    [SerializeField] private float reloadSeconds = 2.5f;
    [SerializeField] private float fireCooldown = 0.35f;

    [Header("Shot")]
    [SerializeField] private float range = 22f;
    [SerializeField] private float shotRadius = 0.45f;
    [SerializeField] private float knockback = 7f;

    [Header("Rig")]
    [SerializeField] private Transform gunSocket;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Light muzzleLight;
    [SerializeField] private GameObject muzzleFlash;

    [Header("Kick")]
    [SerializeField] private float kickAngle = 24f;
    [SerializeField] private float kickBack = 0.07f;
    [SerializeField] private float kickRecover = 10f;
    [SerializeField] private float flashSeconds = 0.06f;
    [SerializeField] private float faceCursorSeconds = 0.45f;

    public int Loaded { get; private set; }
    public int Reserve { get; private set; }
    public bool IsReloading { get; private set; }

    private PlayerMovement movement;
    private Camera cam;
    private float kick;
    private float nextFireTime;
    private float flashUntil;
    private float faceUntil;
    private Vector3 aimDir;
    private Quaternion socketBaseRot;
    private Vector3 socketBasePos;

    private void Awake()
    {
        Instance = this;
        movement = GetComponent<PlayerMovement>();
        cam = Camera.main;
        Loaded = magazineSize;
        Reserve = startReserve;
        if (gunSocket != null)
        {
            socketBaseRot = gunSocket.localRotation;
            socketBasePos = gunSocket.localPosition;
        }
        SetFlash(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryFire();
        if (kb != null && kb.rKey.wasPressedThisFrame) TryReload();

        // Face the cursor for a moment after a shot so the pose reads.
        bool facing = Time.time < faceUntil;
        if (movement != null) movement.FacingLocked = facing;
        if (facing && aimDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);

        if (Time.time >= flashUntil) SetFlash(false);
    }

    private void LateUpdate()
    {
        // Recoil kick applied after the animator has posed the hand.
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-kickRecover * Time.deltaTime));
        if (gunSocket != null)
        {
            gunSocket.localRotation = socketBaseRot * Quaternion.Euler(-kickAngle * kick, 0f, 0f);
            gunSocket.localPosition = socketBasePos + socketBaseRot * (Vector3.back * kickBack * kick);
        }
    }

    private void TryFire()
    {
        if (IsReloading || Time.time < nextFireTime) return;

        aimDir = AimDirection();
        faceUntil = Time.time + faceCursorSeconds;
        transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);

        if (Loaded <= 0)
        {
            Debug.Log("Click. Empty.");
            nextFireTime = Time.time + 0.2f;
            return;
        }

        Loaded--;
        nextFireTime = Time.time + fireCooldown;
        kick = 1f;
        flashUntil = Time.time + flashSeconds;
        SetFlash(true);

        Vector3 origin = transform.position + Vector3.up * 0.9f + aimDir * 0.7f;
        RaycastHit[] hits = Physics.SphereCastAll(origin, shotRadius, aimDir, range, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Stalker target = null;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.root == transform.root) continue;
            Stalker s = h.collider.GetComponentInParent<Stalker>();
            if (s == null) continue;
            if (h.distance < best) { best = h.distance; target = s; }
        }
        if (target != null) target.Hit(aimDir * knockback);
    }

    private void TryReload()
    {
        if (IsReloading || Reserve <= 0 || Loaded >= magazineSize) return;
        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        IsReloading = true;
        yield return new WaitForSeconds(reloadSeconds);
        int need = magazineSize - Loaded;
        int take = Mathf.Min(need, Reserve);
        Loaded += take;
        Reserve -= take;
        IsReloading = false;
    }

    /// <summary>Rounds earned at home. Only called when a survivor is through the door.</summary>
    public void AddReserve(int rounds)
    {
        Reserve += rounds;
    }

    private Vector3 AimDirection()
    {
        Vector3 fallback = transform.forward;
        if (cam == null || Mouse.current == null) return fallback;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!ground.Raycast(ray, out float enter)) return fallback;

        Vector3 dir = ray.GetPoint(enter) - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.01f ? dir.normalized : fallback;
    }

    private void SetFlash(bool on)
    {
        if (muzzleLight != null) muzzleLight.enabled = on;
        if (muzzleFlash != null) muzzleFlash.SetActive(on);
    }

    // Placeholder HUD until the real one exists.
    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        string text = IsReloading ? "RELOADING" : $"{Loaded} / {Reserve}";
        GUI.Label(new Rect(20, Screen.height - 50, 300, 40), text, style);
    }
}
