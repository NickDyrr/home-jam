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

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !Encounter.Active && Time.time >= Encounter.SuppressFireUntil && !GameHUD.MouseOverBar()) TryFire();
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
            float since = IsReloading ? 0f : Time.time - lastShotTime;   // the gun stays up through a reload
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

    [Header("Reload (no clip: the arm is posed here)")]
    [Tooltip("How far the left upper arm swings down to drop the magazine and fetch the next.")]
    [SerializeField] private float reloadArmAngle = 60f;
    [SerializeField] private float reloadForearmAngle = 25f;
    private float reloadStart = -10f, reloadDuration = 1f;
    private bool magDropped;
    private Transform lHand;
    private GameObject heldMag;
    private static readonly Color MagColor = new Color(0.32f, 0.33f, 0.35f);
    private static readonly Vector3 MagSize = new Vector3(0.028f, 0.095f, 0.018f);

    /// <summary>0 outside a reload, else how far through it we are.</summary>
    private float ReloadPhase => IsReloading ? Mathf.Clamp01((Time.time - reloadStart) / Mathf.Max(0.01f, reloadDuration)) : 0f;

    /// <summary>
    /// How far the left arm is down at phase p: swings down over the first third, holds while the
    /// old mag falls and the new one is taken, and comes back up by 85 percent.
    /// </summary>
    private static float ArmDown(float p)
    {
        if (p < 0.3f) return Mathf.SmoothStep(0f, 1f, p / 0.3f);
        if (p < 0.55f) return 1f;
        if (p < 0.85f) return Mathf.SmoothStep(1f, 0f, (p - 0.55f) / 0.3f);
        return 0f;
    }

    private static GameObject MakeMag()
    {
        var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m.name = "Magazine";
        Destroy(m.GetComponent<Collider>());
        m.transform.localScale = MagSize;
        var r = m.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", MagColor); mat.SetFloat("_Smoothness", 0.35f); mat.SetFloat("_Metallic", 0.6f);
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return m;
    }

    /// <summary>The magazine in her hand during the back half of the reload, then the empty one on the snow.</summary>
    private void ReloadProps(float p)
    {
        if (lHand == null && animator != null && animator.isHuman) lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        // The old one leaves her hand at the bottom of the swing.
        if (IsReloading && !magDropped && p >= 0.3f)
        {
            magDropped = true;
            Vector3 from = lHand != null ? lHand.position : transform.position + Vector3.up * 0.8f;
            var dropped = MakeMag();
            dropped.transform.localScale = MagSize * 2.1f;   // her rig is scaled up; the one on the snow matches what was in her hand
            dropped.transform.position = from;
            dropped.transform.rotation = Random.rotation;
            dropped.AddComponent<DroppedMag>();
        }
        // The fresh one is in her hand while the arm comes back up.
        bool holding = IsReloading && p >= 0.4f && p < 0.85f && lHand != null;
        if (holding && heldMag == null)
        {
            heldMag = MakeMag();
            heldMag.transform.SetParent(lHand, false);
            heldMag.transform.localPosition = new Vector3(0.03f, -0.02f, 0.02f);
            heldMag.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else if (!holding && heldMag != null) { Destroy(heldMag); heldMag = null; }
    }

    private void LateUpdate()
    {
        // Recoil, applied after the animator has posed the rig this frame.
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-kickRecover * Time.deltaTime));
        if (kick < 0.001f) kick = 0f;

        // Reload: the left arm drops away from the gun and comes back with the next magazine.
        float phase = ReloadPhase;
        ReloadProps(phase);
        float down = IsReloading ? ArmDown(phase) : 0f;
        if (down > 0f)
        {
            Vector3 axis = transform.right;
            if (lUpperArm != null) lUpperArm.rotation = Quaternion.AngleAxis(reloadArmAngle * down, axis) * lUpperArm.rotation;
            if (lLowerArm != null) lLowerArm.rotation = Quaternion.AngleAxis(reloadForearmAngle * down, axis) * lLowerArm.rotation;
        }

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
        Stalker.Noise(transform.position, 32f);   // a shot carries: the far ones come. Only the one it hits runs (Stalker.Hit).
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

        // The round itself: a streak to wherever it stopped (the stalker, a tree, the snow, or the end of its reach).
        Vector3 stop = origin + aimDir * range; float stopAt = range; bool hitSomething = target != null;
        foreach (RaycastHit h in Physics.RaycastAll(origin, aimDir, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.root == transform.root || h.collider.GetComponentInParent<Stalker>() != null) continue;
            if (h.distance < stopAt) { stopAt = h.distance; stop = h.point; }
        }
        if (target != null && best < stopAt) { stop = origin + aimDir * best; stopAt = best; }
        Tracer.Spawn(muzzle != null ? muzzle.position : origin, stop, hitSomething);
    }

    private void TryReload()
    {
        if (IsReloading || Reserve <= 0 || Loaded >= magazineSize) return;
        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        IsReloading = true;
        reloadStart = Time.time; reloadDuration = reloadSeconds * HomeBonuses.ReloadMultiplier; magDropped = false;
        lastShotTime = Time.time;   // arms up for it, as if she had just fired
        if (animator != null && HasParameter(animator, "Reload")) animator.SetTrigger("Reload");   // the clip, once one is in
        yield return new WaitForSeconds(reloadDuration);
        int need = magazineSize - Loaded;
        int take = Mathf.Min(need, Reserve);
        Loaded += take;
        Reserve -= take;
        IsReloading = false;
    }

    private static bool HasParameter(Animator a, string name)
    {
        foreach (var p in a.parameters) if (p.name == name) return true;
        return false;
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
