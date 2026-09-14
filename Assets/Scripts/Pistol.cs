using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What she can shoot with. The pistol is hers from the start; the others are found.</summary>
public enum WeaponKind { Pistol, Rifle, Bow }

/// <summary>
/// Her weapons. Started as the one pistol and kept the name: this component now holds the
/// pistol, the rifle and the bow, with their own ammo, and whichever is in hand fires toward
/// the mouse. A hit slows a stalker and, once it has taken enough this chase, sends it off;
/// nothing here kills. The rifle hits for two and is loud; the bow is quiet and its arrows
/// can be picked back up.
///
/// Animation: a shot fires the "Shoot" trigger (upper-body aim pose for the weapon in hand,
/// chosen by the "Weapon" float) and marks her Armed; the base locomotion blends to the
/// weapon's stance on "Weapon" and "Armed". Recoil is done on the bones in LateUpdate, and
/// the pistol's reload is posed there too; the rifle has a reload clip.
/// </summary>
public class Pistol : MonoBehaviour
{
    public static Pistol Instance { get; private set; }

    [System.Serializable]
    public class WeaponStats
    {
        public int magazine = 6;
        public float fireCooldown = 0.35f;
        public float reloadSeconds = 2.5f;
        public float range = 22f;
        public float shotRadius = 0.45f;
        public float knockback = 7f;
        [Tooltip("How many hits one round counts for against a stalker.")]
        public int hitWeight = 1;
        [Tooltip("How far the shot is heard. Zero: silent.")]
        public float noiseRadius = 32f;
        public float volume = 0.45f, pitch = 1f;
        public float kickScale = 1f;
        public bool flash = true;
    }

    [Header("Weapons")]
    [SerializeField] private WeaponStats pistol = new WeaponStats();
    [SerializeField] private WeaponStats rifle = new WeaponStats { magazine = 5, fireCooldown = 1.1f, reloadSeconds = 2.7f, range = 40f, shotRadius = 0.35f, knockback = 11f, hitWeight = 2, noiseRadius = 48f, volume = 0.6f, pitch = 0.8f, kickScale = 1.6f };
    [SerializeField] private WeaponStats bow = new WeaponStats { magazine = 1, fireCooldown = 0.9f, reloadSeconds = 1.0f, range = 30f, shotRadius = 0.3f, knockback = 4f, hitWeight = 1, noiseRadius = 0f, volume = 0f, pitch = 1f, kickScale = 0.5f, flash = false };
    [SerializeField] private int startReserve = 6;

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

    // ---- state ----
    public WeaponKind Current { get; private set; } = WeaponKind.Pistol;
    private readonly int[] loaded = new int[3], reserve = new int[3];
    private readonly bool[] owned = { true, false, false };

    public int Loaded => loaded[(int)Current];
    public int Reserve => reserve[(int)Current];
    public int LoadedOf(WeaponKind k) => loaded[(int)k];
    public int ReserveOf(WeaponKind k) => reserve[(int)k];
    public bool Owns(WeaponKind k) => owned[(int)k];
    public bool IsReloading { get; private set; }
    private WeaponStats Stats => Current == WeaponKind.Rifle ? rifle : Current == WeaponKind.Bow ? bow : pistol;

    /// <summary>True for a short window after the last shot; movement uses it to forbid running.</summary>
    public bool IsArmed => Time.time - lastShotTime < armedSeconds;

    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int ArmedHash = Animator.StringToHash("Armed");
    private static readonly int WeaponHash = Animator.StringToHash("Weapon");

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
    private GameObject pistolModel, rifleModel, bowModel;
    private Transform rifleMuzzle, bowMuzzle;

    private void Awake()
    {
        Instance = this;
        movement = GetComponent<PlayerMovement>();
        cam = Camera.main;
        loaded[0] = pistol.magazine; reserve[0] = startReserve;

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
                lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }

        if (gunSocket != null)
        {
            socketBaseRot = gunSocket.localRotation;
            socketBasePos = gunSocket.localPosition;
            if (gunSocket.childCount > 0) pistolModel = gunSocket.GetChild(0).gameObject;
        }
        SetFlash(false);
        ShowWeapon();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- owning and switching ----

    /// <summary>A weapon found out there. It comes with what was beside it.</summary>
    public void Give(WeaponKind k, int ammo)
    {
        owned[(int)k] = true;
        AddAmmo(k, ammo);
        Equip(k);
    }

    public void AddAmmo(WeaponKind k, int n) { reserve[(int)k] += n; if (k == WeaponKind.Bow && loaded[2] == 0 && reserve[2] > 0) { loaded[2] = 1; reserve[2]--; } }

    /// <summary>Rounds earned at home: pistol rounds. Only called when a survivor is through the door.</summary>
    public void AddReserve(int rounds) { AddAmmo(WeaponKind.Pistol, rounds); }

    public bool Equip(WeaponKind k)
    {
        if (!owned[(int)k] || IsReloading) return false;
        if (Current == k) return true;
        Current = k;
        ShowWeapon();
        if (animator != null) animator.SetFloat(WeaponHash, (int)k);
        FloatingText.Show(transform.position + Vector3.up * 1.6f, ItemInfo.Name(WeaponItem(k)), 1.2f);
        return true;
    }

    public static ItemKind WeaponItem(WeaponKind k) => k == WeaponKind.Rifle ? ItemKind.Rifle : k == WeaponKind.Bow ? ItemKind.Bow : ItemKind.Pistol;

    private static Material Lit(Color c, float smooth = 0.35f, float metal = 0.2f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        return m;
    }

    private static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var g = GameObject.CreatePrimitive(type); Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = rot;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    /// <summary>The rifle and bow have no models yet: they are built from primitives in the pistol's frame, in metres.</summary>
    private void BuildModels()
    {
        if (rifleModel != null || gunSocket == null || pistolModel == null) return;
        // The pistol's barrel axis, in socket space, and a scale that makes one unit one metre.
        Vector3 fwd = muzzle != null ? gunSocket.InverseTransformDirection((muzzle.position - pistolModel.transform.position).normalized) : Vector3.forward;
        float k = 1f / Mathf.Max(0.0001f, gunSocket.lossyScale.x);
        Quaternion along = Quaternion.LookRotation(fwd, gunSocket.InverseTransformDirection(Vector3.up));
        Vector3 basePos = pistolModel.transform.localPosition;

        var rm = new GameObject("RifleModel"); rm.transform.SetParent(gunSocket, false); rm.transform.localPosition = basePos; rm.transform.localRotation = along; rm.transform.localScale = Vector3.one * k;
        var dark = Lit(new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.8f); var wood = Lit(new Color(0.36f, 0.24f, 0.14f), 0.3f, 0f);
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.28f), new Vector3(0.035f, 0.04f, 0.62f), Quaternion.identity, dark);        // barrel and receiver
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.22f), new Vector3(0.045f, 0.05f, 0.34f), Quaternion.identity, wood);      // forestock
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.16f), new Vector3(0.04f, 0.09f, 0.24f), Quaternion.Euler(-12f, 0f, 0f), wood); // stock
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0.1f), new Vector3(0.02f, 0.03f, 0.1f), Quaternion.identity, dark);          // sight
        rifleMuzzle = new GameObject("RifleMuzzle").transform; rifleMuzzle.SetParent(rm.transform, false); rifleMuzzle.localPosition = new Vector3(0f, 0.02f, 0.6f);
        rifleModel = rm;

        // The bow sits in her left hand, a tall arc with a string.
        Transform hand = lHand != null ? lHand : gunSocket;
        var bm = new GameObject("BowModel"); bm.transform.SetParent(hand, false);
        float kb = 1f / Mathf.Max(0.0001f, hand.lossyScale.x);
        bm.transform.localScale = Vector3.one * kb; bm.transform.localRotation = Quaternion.identity;
        var limb = Lit(new Color(0.3f, 0.2f, 0.12f), 0.3f, 0f); var str = Lit(new Color(0.85f, 0.82f, 0.7f), 0.2f, 0f);
        const int segs = 9; float R = 0.62f, half = 65f * Mathf.Deg2Rad;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= segs; i++)
        {
            float a = -half + (half * 2f) * i / segs;
            Vector3 p = new Vector3(0f, Mathf.Sin(a) * R, Mathf.Cos(a) * R - R * 0.75f);
            if (i > 0)
            {
                Vector3 mid = (prev + p) * 0.5f; Vector3 d = p - prev;
                Part(bm.transform, PrimitiveType.Cylinder, mid, new Vector3(0.022f, d.magnitude * 0.5f + 0.004f, 0.022f), Quaternion.FromToRotation(Vector3.up, d.normalized), limb);
            }
            prev = p;
        }
        Vector3 top = new Vector3(0f, Mathf.Sin(half) * R, Mathf.Cos(half) * R - R * 0.75f), bot = new Vector3(0f, -Mathf.Sin(half) * R, Mathf.Cos(half) * R - R * 0.75f);
        Part(bm.transform, PrimitiveType.Cylinder, (top + bot) * 0.5f, new Vector3(0.006f, (top - bot).magnitude * 0.5f, 0.006f), Quaternion.FromToRotation(Vector3.up, (top - bot).normalized), str);
        bowMuzzle = new GameObject("BowMuzzle").transform; bowMuzzle.SetParent(bm.transform, false); bowMuzzle.localPosition = new Vector3(0f, 0f, 0.1f);
        bowModel = bm;
    }

    private void ShowWeapon()
    {
        BuildModels();
        if (pistolModel != null) pistolModel.SetActive(Current == WeaponKind.Pistol);
        if (rifleModel != null) rifleModel.SetActive(Current == WeaponKind.Rifle);
        if (bowModel != null) bowModel.SetActive(Current == WeaponKind.Bow);
    }

    private Transform MuzzleNow => Current == WeaponKind.Rifle && rifleMuzzle != null ? rifleMuzzle : Current == WeaponKind.Bow && bowMuzzle != null ? bowMuzzle : muzzle;

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (kb != null && !Intro.Playing && !GameHUD.Paused)
        {
            if (kb.digit1Key.wasPressedThisFrame) Equip(WeaponKind.Pistol);
            if (kb.digit2Key.wasPressedThisFrame) Equip(WeaponKind.Rifle);
            if (kb.digit3Key.wasPressedThisFrame) Equip(WeaponKind.Bow);
        }
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

    [Header("Pistol reload (no clip: the arm is posed here)")]
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

    /// <summary>The magazine in her hand during the back half of the pistol reload, then the empty one on the snow.</summary>
    private void ReloadProps(float p)
    {
        if (lHand == null && animator != null && animator.isHuman) lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        bool pistolReload = IsReloading && Current == WeaponKind.Pistol;
        // The old one leaves her hand at the bottom of the swing.
        if (pistolReload && !magDropped && p >= 0.3f)
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
        bool holding = pistolReload && p >= 0.4f && p < 0.85f && lHand != null;
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

        // Pistol reload: the left arm drops away from the gun and comes back with the next magazine.
        float phase = ReloadPhase;
        ReloadProps(phase);
        float down = IsReloading && Current == WeaponKind.Pistol ? ArmDown(phase) : 0f;
        if (down > 0f)
        {
            Vector3 axis = transform.right;
            if (lUpperArm != null) lUpperArm.rotation = Quaternion.AngleAxis(reloadArmAngle * down, axis) * lUpperArm.rotation;
            if (lLowerArm != null) lLowerArm.rotation = Quaternion.AngleAxis(reloadForearmAngle * down, axis) * lLowerArm.rotation;
        }

        if (kick > 0f)
        {
            Vector3 axis = transform.right;                       // pitch axis, character space
            float arm = armKickAngle * kick * Stats.kickScale;
            if (chest != null)     chest.rotation     = Quaternion.AngleAxis(-chestKickAngle * kick * Stats.kickScale, axis) * chest.rotation;
            if (rUpperArm != null) rUpperArm.rotation = Quaternion.AngleAxis(-arm, axis) * rUpperArm.rotation;
            if (lUpperArm != null) lUpperArm.rotation = Quaternion.AngleAxis(-arm, axis) * lUpperArm.rotation;
            if (rLowerArm != null) rLowerArm.rotation = Quaternion.AngleAxis(-arm * 0.5f, axis) * rLowerArm.rotation;
            if (lLowerArm != null) lLowerArm.rotation = Quaternion.AngleAxis(-arm * 0.5f, axis) * lLowerArm.rotation;
        }

        if (gunSocket != null)
        {
            gunSocket.localRotation = socketBaseRot * Quaternion.Euler(-kickAngle * kick * Stats.kickScale, 0f, 0f);
            gunSocket.localPosition = socketBasePos + socketBaseRot * (Vector3.back * kickBack * kick * Stats.kickScale);
        }
    }

    private void TryFire()
    {
        if (IsReloading || Time.time < nextFireTime) return;
        var w = Stats;

        aimDir = AimDirection();
        faceUntil = Time.time + faceCursorSeconds;
        transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
        lastShotTime = Time.time;
        if (animator != null) animator.SetTrigger(ShootHash);

        if (Loaded <= 0)
        {
            if (Current == WeaponKind.Bow && Reserve > 0) { TryReload(); return; }   // nock one
            Debug.Log("Click. Empty.");
            nextFireTime = Time.time + 0.2f;
            return;
        }

        loaded[(int)Current]--;
        nextFireTime = Time.time + w.fireCooldown;
        kick = 1f;
        if (Current == WeaponKind.Bow)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.Bell != null) AudioManager.Instance.Play(AudioManager.Instance.Bell, transform.position, 0.12f, 20f, 3.2f);   // the string, near enough
        }
        else if (AudioManager.Instance != null) AudioManager.Instance.Play(AudioManager.Instance.Gunshot, transform.position, w.volume, 200f, w.pitch * Random.Range(0.95f, 1.05f));
        if (w.noiseRadius > 0f) Stalker.Noise(transform.position, w.noiseRadius);   // a shot carries: the far ones come. Only the one it hits runs (Stalker.Hit).
        if (w.flash) { flashUntil = Time.time + flashSeconds; SetFlash(true); }

        Vector3 origin = transform.position + Vector3.up * 0.9f + aimDir * 0.7f;
        RaycastHit[] hits = Physics.SphereCastAll(origin, w.shotRadius, aimDir, w.range, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Stalker target = null;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.root == transform.root) continue;
            Stalker s = h.collider.GetComponentInParent<Stalker>();
            if (s == null) continue;
            if (h.distance < best) { best = h.distance; target = s; }
        }
        if (target != null) target.Hit(aimDir * w.knockback, w.hitWeight);

        // Where it stopped: the stalker, a tree, the snow, or the end of its reach.
        Vector3 stop = origin + aimDir * w.range; float stopAt = w.range; bool hitSomething = target != null;
        foreach (RaycastHit h in Physics.RaycastAll(origin, aimDir, w.range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.root == transform.root || h.collider.GetComponentInParent<Stalker>() != null) continue;
            if (h.distance < stopAt) { stopAt = h.distance; stop = h.point; }
        }
        if (target != null && best < stopAt) { stop = origin + aimDir * best; stopAt = best; }
        Transform mz = MuzzleNow;
        if (Current == WeaponKind.Bow)
        {
            // The arrow flies and sticks; one that misses can be picked back up.
            Arrow.Loose(mz != null ? mz.position : origin, stop, aimDir, target == null || Random.value < 0.5f);
        }
        else Tracer.Spawn(mz != null ? mz.position : origin, stop, hitSomething);
    }

    private void TryReload()
    {
        if (IsReloading || Reserve <= 0 || Loaded >= Stats.magazine) return;
        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        IsReloading = true;
        reloadStart = Time.time; reloadDuration = Stats.reloadSeconds * HomeBonuses.ReloadMultiplier; magDropped = false;
        lastShotTime = Time.time;   // arms up for it, as if she had just fired
        if (animator != null && Current == WeaponKind.Rifle && HasParameter(animator, "Reload")) animator.SetTrigger("Reload");
        yield return new WaitForSeconds(reloadDuration);
        int i = (int)Current;
        int need = Stats.magazine - loaded[i];
        int take = Mathf.Min(need, reserve[i]);
        loaded[i] += take;
        reserve[i] -= take;
        IsReloading = false;
    }

    private static bool HasParameter(Animator a, string name)
    {
        foreach (var p in a.parameters) if (p.name == name) return true;
        return false;
    }

    /// <summary>Rounds dropped in the snow when she is taken. Spare first, then what is loaded. Returns how many went.</summary>
    public int LoseRounds(int rounds)
    {
        int i = (int)Current;
        int fromReserve = Mathf.Min(rounds, reserve[i]);
        reserve[i] -= fromReserve;
        int fromLoaded = Mathf.Min(rounds - fromReserve, loaded[i]);
        loaded[i] -= fromLoaded;
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
}

/// <summary>An arrow in flight for a moment, then stuck where it stopped. Lying on the snow it is a pickup again.</summary>
public class Arrow : MonoBehaviour
{
    private Vector3 from, to; private float t; private bool recoverable;
    private static Material shaft, fletch;

    public static void Loose(Vector3 from, Vector3 to, Vector3 dir, bool recoverable)
    {
        if (shaft == null) { shaft = new Material(Shader.Find("Universal Render Pipeline/Lit")); shaft.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.25f)); }
        if (fletch == null) { fletch = new Material(Shader.Find("Universal Render Pipeline/Lit")); fletch.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.82f)); }
        var go = new GameObject("Arrow");
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false); body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.018f, 0.36f, 0.018f); body.GetComponent<Renderer>().sharedMaterial = shaft;
        var f = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(f.GetComponent<Collider>());
        f.transform.SetParent(go.transform, false); f.transform.localPosition = new Vector3(0f, 0f, -0.3f); f.transform.localScale = new Vector3(0.05f, 0.004f, 0.08f); f.GetComponent<Renderer>().sharedMaterial = fletch;
        go.transform.position = from; go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var a = go.AddComponent<Arrow>(); a.from = from; a.to = to; a.recoverable = recoverable;
    }

    private void Update()
    {
        t += Time.deltaTime / 0.12f;
        if (t < 1f) { transform.position = Vector3.Lerp(from, to, t); return; }
        transform.position = to;
        // Stuck. On the snow it can be taken back; in something else it is spent.
        if (recoverable)
        {
            var p = Pickup.Spawn(ItemKind.Arrow, new Vector3(to.x, 0f, to.z), 1);
            transform.SetParent(p.transform, true);
            transform.position = new Vector3(to.x, 0.06f, to.z);
            transform.rotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);
        }
        else Destroy(gameObject, 40f);
        enabled = false;
    }
}
