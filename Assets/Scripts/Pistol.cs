using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The one gun. Six rounds, slow reload, ammo only comes from survivors who
/// make it home. Left click fires toward the mouse cursor; the player snaps
/// to face the shot. A hit stuns and knocks back a stalker; it never kills.
///
/// Animation: a click fires the Animator's "Shoot" trigger (upper-body aim
/// pose) and marks her Armed; the base walk blends between the normal walk
/// and the pistol walk on "Armed", which fades out a few seconds after the
/// last shot. The aim layer's weight is driven here (up on a shot, down
/// after) so the layer is fully silent when not shooting. On top of that,
/// a procedural recoil rocks the arms and the gun socket in LateUpdate.
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
    [SerializeField] private Animator animator;
    [SerializeField] private Transform gunSocket;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Light muzzleLight;
    [SerializeField] private GameObject muzzleFlash;

    [Header("Kick")]
    [SerializeField] private float kickAngle = 24f;
    [SerializeField] private float kickBack = 0.07f;
    [SerializeField] private float armKickAngle = 14f;
    [SerializeField] private float chestKickAngle = 4f;
    [SerializeField] private float kickRecover = 10f;
    [SerializeField] private float flashSeconds = 0.06f;
    [SerializeField] private float faceCursorSeconds = 0.45f;

    [Header("Stance")]
    [Tooltip("Seconds after the last shot before she drops back to the normal walk.")]
    [SerializeField] private float armedSeconds = 1f;
    [SerializeField] private float armedBlendSeconds = 0.25f;
    [Tooltip("How long the aim pose (upper-body layer) stays up after a shot.")]
    [SerializeField] private float aimHoldSeconds = 0.7f;
    [SerializeField] private float aimBlendIn = 0.06f;
    [SerializeField] private float aimBlendOut = 0.3f;

    public int Loaded { get; private set; }
    public int Reserve { get; private set; }
    public bool IsReloading { get; private set; }

    /// <summary>True for a short window after the last shot; movement uses it to forbid running.</summary>
    public bool IsArmed => Time.time - lastShotTime < armedSeconds;

    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int ArmedHash = Animator.StringToHash("Armed");

    private PlayerMovement movement;
    private Camera cam;
    private float kick;
    private float nextFireTime;
    private float flashUntil;
    private float faceUntil;
    private float lastShotTime = -999f;
    private float aimWeight;
    private int aimLayer = -1;
    private Vector3 aimDir;
    private Quaternion socketBaseRot;
    private Vector3 socketBasePos;
    private Transform rUpperArm, lUpperArm, rLowerArm, lLowerArm, chest;

    private void Awake()
    {
        Instance = this;
        movement = GetComponent<PlayerMovement>();
        cam = Camera.main;
        Loaded = magazineSize;
        Reserve = startReserve;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            aimLayer = animator.GetLayerIndex("UpperBody");
            if (aimLayer >= 0) animator.SetLayerWeight(aimLayer, 0f);

            if (animator.isHuman)
            {
                rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
        }

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

        if (animator != null)
        {
            // Armed stance fades out a few seconds after the last shot.
            float since = Time.time - lastShotTime;
            float armed = since < armedSeconds ? 1f : 0f;
            animator.SetFloat(ArmedHash, armed, armedBlendSeconds, Time.deltaTime);

            // Aim layer weight: quick in, slower out, silent otherwise.
            if (aimLayer >= 0)
            {
                bool aiming = since < aimHoldSeconds;
                float target = aiming ? 1f : 0f;
                float rate = aiming ? aimBlendIn : aimBlendOut;
                aimWeight = Mathf.MoveTowards(aimWeight, target, Time.deltaTime / Mathf.Max(0.01f, rate));
                animator.SetLayerWeight(aimLayer, aimWeight);
            }
        }
    }

    private void LateUpdate()
    {
        // Recoil, applied after the animator has posed the rig this frame.
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-kickRecover * Time.deltaTime));
        if (kick < 0.001f) kick = 0f;

        if (kick > 0f)
        {
            Vector3 axis = transform.right;                       // pitch axis, character space
            float arm = armKickAngle * kick;
            if (chest != null)     chest.rotation     = Quaternion.AngleAxis(-chestKickAngle * kick, axis) * chest.rotation;
            if (rUpperArm != null) rUpperArm.rotation = Quaternion.AngleAxis(-arm, axis) * rUpperArm.rotation;
            if (lUpperArm != null) lUpperArm.rotation = Quaternion.AngleAxis(-arm, axis) * lUpperArm.rotation;
            if (rLowerArm != null) rLowerArm.rotation = Quaternion.AngleAxis(-arm * 0.5f, axis) * rLowerArm.rotation;
            if (lLowerArm != null) lLowerArm.rotation = Quaternion.AngleAxis(-arm * 0.5f, axis) * lLowerArm.rotation;
        }

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
        lastShotTime = Time.time;
        if (animator != null) animator.SetTrigger(ShootHash);

        if (Loaded <= 0)
        {
            Debug.Log("Click. Empty.");
            nextFireTime = Time.time + 0.2f;
            return;
        }

        Loaded--;
        nextFireTime = Time.time + fireCooldown;
        kick = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.Play(AudioManager.Instance.Gunshot, transform.position, 0.45f, 200f, Random.Range(0.95f, 1.05f));
        Stalker.Noise(transform.position, 32f);   // a shot carries
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
        yield return new WaitForSeconds(reloadSeconds * HomeBonuses.ReloadMultiplier);
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

    /// <summary>Rounds dropped in the snow when she is taken. Spare first, then what is loaded. Returns how many went.</summary>
    public int LoseRounds(int rounds)
    {
        int fromReserve = Mathf.Min(rounds, Reserve);
        Reserve -= fromReserve;
        int fromLoaded = Mathf.Min(rounds - fromReserve, Loaded);
        Loaded -= fromLoaded;
        return fromReserve + fromLoaded;
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
        Intro.SetTextColor(style, Color.white);
        string text = IsReloading ? "RELOADING" : $"{Loaded} / {Reserve}";
        GUI.Label(new Rect(20, Screen.height - 50, 300, 40), text, style);
    }
}
