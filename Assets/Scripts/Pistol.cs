using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What she can shoot with. The pistol is hers from the start; the rest are found.</summary>
public enum WeaponKind { Pistol, Rifle, Bow, Shotgun, FlareGun, AutoRifle, Sniper }

/// <summary>
/// Her weapons. Started as the one pistol and kept the name: this component now holds every
/// weapon with its own ammo, and whichever is in hand fires toward the mouse. A hit slows a
/// stalker and, once it has taken enough this chase, sends it off; nothing here kills.
///
/// Rifle: hits for two, loud, a real reload clip. Bow: silent, one arrow at a time, arrows
/// that miss can be picked back up. Shotgun: two shells, short and wide. Flare gun: puts a
/// flare thirty metres out. Auto rifle: hold to fire, a spray. Sniper: three rounds, seventy
/// metres, one hit sends anything running.
///
/// Animation: a shot fires the "Shoot" trigger (upper-body aim pose for the stance, chosen by
/// the "Weapon" float: 0 pistol, 1 rifle, 2 bow) and marks her Armed; the base locomotion
/// blends to the stance on "Weapon" and "Armed". Recoil is done on the bones in LateUpdate,
/// and the pistol's reload is posed there too; the rifles share the rifle reload clip.
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
        [Tooltip("Which animator stance it uses: 0 pistol, 1 rifle, 2 bow.")]
        public int stance = 0;
        [Tooltip("Degrees of scatter on each shot.")]
        public float spread = 0f;
        [Tooltip("Keeps firing while the button is held.")]
        public bool automatic = false;
    }

    private const int Kinds = 7;

    [Header("Weapons")]
    [SerializeField] private WeaponStats pistol = new WeaponStats();
    [SerializeField] private WeaponStats rifle = new WeaponStats { magazine = 5, fireCooldown = 1.1f, reloadSeconds = 2.7f, range = 40f, shotRadius = 0.35f, knockback = 11f, hitWeight = 2, noiseRadius = 48f, volume = 0.6f, pitch = 0.8f, kickScale = 1.6f, stance = 1 };
    [SerializeField] private WeaponStats bow = new WeaponStats { magazine = 1, fireCooldown = 0.9f, reloadSeconds = 1.0f, range = 30f, shotRadius = 0.3f, knockback = 4f, hitWeight = 1, noiseRadius = 0f, volume = 0f, pitch = 1f, kickScale = 0.5f, flash = false, stance = 2 };
    [SerializeField] private WeaponStats shotgun = new WeaponStats { magazine = 2, fireCooldown = 0.8f, reloadSeconds = 2.2f, range = 12f, shotRadius = 1.4f, knockback = 15f, hitWeight = 2, noiseRadius = 60f, volume = 0.8f, pitch = 0.65f, kickScale = 2.2f, stance = 1 };
    [SerializeField] private WeaponStats flareGun = new WeaponStats { magazine = 1, fireCooldown = 0.6f, reloadSeconds = 1.6f, range = 30f, shotRadius = 0f, knockback = 0f, hitWeight = 0, noiseRadius = 14f, volume = 0.3f, pitch = 1.3f, kickScale = 0.8f, stance = 0 };
    [SerializeField] private WeaponStats autoRifle = new WeaponStats { magazine = 30, fireCooldown = 0.1f, reloadSeconds = 2.5f, range = 30f, shotRadius = 0.3f, knockback = 4f, hitWeight = 1, noiseRadius = 45f, volume = 0.4f, pitch = 1.15f, kickScale = 0.6f, stance = 1, spread = 4f, automatic = true };
    [SerializeField] private WeaponStats sniper = new WeaponStats { magazine = 3, fireCooldown = 1.6f, reloadSeconds = 3.2f, range = 70f, shotRadius = 0.25f, knockback = 18f, hitWeight = 3, noiseRadius = 70f, volume = 0.7f, pitch = 0.6f, kickScale = 2.5f, stance = 1 };
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
    private readonly int[] loaded = new int[Kinds];
    // Spare ammo pools: 0 pistol rounds, 1 rifle rounds, 2 arrows, 3 shells, 4 auto rounds, 5 sniper rounds. The flare gun fires the flares she carries.
    private readonly int[] pool = new int[6];
    private readonly bool[] owned = { true, false, false, false, false, false, false };

    private static int Pool(WeaponKind k)
    {
        switch (k)
        {
            case WeaponKind.Pistol: return 0;
            case WeaponKind.Rifle: return 1;
            case WeaponKind.Bow: return 2;
            case WeaponKind.Shotgun: return 3;
            case WeaponKind.AutoRifle: return 4;
            case WeaponKind.Sniper: return 5;
        }
        return -1;
    }

    public int Loaded => LoadedOf(Current);
    public int Reserve => ReserveOf(Current);
    public int LoadedOf(WeaponKind k) => k == WeaponKind.FlareGun ? Mathf.Min(1, Inventory.Count(ItemKind.Flare)) : loaded[(int)k];
    public int ReserveOf(WeaponKind k) { int p = Pool(k); return k == WeaponKind.FlareGun ? Mathf.Max(0, Inventory.Count(ItemKind.Flare) - 1) : p < 0 ? 0 : pool[p]; }
    public bool Owns(WeaponKind k) => owned[(int)k];
    public bool IsReloading { get; private set; }
    private WeaponStats StatsOf(WeaponKind k)
    {
        switch (k)
        {
            case WeaponKind.Rifle: return rifle;
            case WeaponKind.Bow: return bow;
            case WeaponKind.Shotgun: return shotgun;
            case WeaponKind.FlareGun: return flareGun;
            case WeaponKind.AutoRifle: return autoRifle;
            case WeaponKind.Sniper: return sniper;
        }
        return pistol;
    }
    private WeaponStats Stats => StatsOf(Current);
    public bool UsesAmmo(WeaponKind k) => true;

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
    private GameObject pistolModel;
    private readonly GameObject[] models = new GameObject[Kinds];
    private readonly Transform[] muzzles = new Transform[Kinds];

    private void Awake()
    {
        Instance = this;
        movement = GetComponent<PlayerMovement>();
        cam = Camera.main;
        loaded[0] = pistol.magazine; pool[0] = startReserve;

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
        models[0] = pistolModel; muzzles[0] = muzzle;
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
        if (k == WeaponKind.Bow) return;   // the bow is out of the game; the kind stays so nothing shifts
        owned[(int)k] = true;
        if (ammo > 0) AddAmmo(k, ammo);
        Equip(k);
    }

    public void AddAmmo(WeaponKind k, int n)
    {
        int p = Pool(k); if (p < 0) return;
        pool[p] += n;
        if (k == WeaponKind.Bow && loaded[(int)k] == 0 && pool[p] > 0) { loaded[(int)k] = 1; pool[p]--; }   // one nocked straight away
    }

    /// <summary>Rounds earned at home: pistol rounds. Only called when a survivor is through the door.</summary>
    public void AddReserve(int rounds) { AddAmmo(WeaponKind.Pistol, rounds); }

    public bool Equip(WeaponKind k)
    {
        if (!owned[(int)k] || IsReloading) return false;
        if (Current == k) return true;
        Current = k;
        ShowWeapon();
        if (animator != null) animator.SetFloat(WeaponHash, Stats.stance);
        FloatingText.Show(transform.position + Vector3.up * 1.6f, ItemInfo.Name(WeaponItem(k)), 1.2f);
        return true;
    }

    public static ItemKind WeaponItem(WeaponKind k)
    {
        switch (k)
        {
            case WeaponKind.Rifle: return ItemKind.Rifle;
            case WeaponKind.Bow: return ItemKind.Bow;
            case WeaponKind.Shotgun: return ItemKind.Shotgun;
            case WeaponKind.FlareGun: return ItemKind.FlareGun;
            case WeaponKind.AutoRifle: return ItemKind.AutoRifle;
            case WeaponKind.Sniper: return ItemKind.Sniper;
        }
        return ItemKind.Pistol;
    }

    public static WeaponKind KindOf(ItemKind i)
    {
        switch (i)
        {
            case ItemKind.Rifle: return WeaponKind.Rifle;
            case ItemKind.Bow: return WeaponKind.Bow;
            case ItemKind.Shotgun: return WeaponKind.Shotgun;
            case ItemKind.FlareGun: return WeaponKind.FlareGun;
            case ItemKind.AutoRifle: return WeaponKind.AutoRifle;
            case ItemKind.Sniper: return WeaponKind.Sniper;
        }
        return WeaponKind.Pistol;
    }

    public static bool IsWeaponItem(ItemKind i) => i == ItemKind.Pistol || i == ItemKind.Rifle || i == ItemKind.Bow || i == ItemKind.Shotgun || i == ItemKind.FlareGun || i == ItemKind.AutoRifle || i == ItemKind.Sniper;

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

    /// <summary>A model root in the pistol's frame on the gun socket, scaled so one unit is one metre.</summary>
    private GameObject InPistolFrame(string name)
    {
        Vector3 fwd = muzzle != null ? gunSocket.InverseTransformDirection((muzzle.position - pistolModel.transform.position).normalized) : Vector3.forward;
        float k = 1f / Mathf.Max(0.0001f, gunSocket.lossyScale.x);
        var go = new GameObject(name); go.transform.SetParent(gunSocket, false);
        go.transform.localPosition = pistolModel.transform.localPosition;
        go.transform.localRotation = Quaternion.LookRotation(fwd, gunSocket.InverseTransformDirection(Vector3.up));
        go.transform.localScale = Vector3.one * k;
        return go;
    }

    private Transform MuzzleAt(GameObject model, Vector3 local)
    {
        var t = new GameObject("Muzzle").transform; t.SetParent(model.transform, false); t.localPosition = local; return t;
    }

    /// <summary>The found weapons have no models yet: they are built from primitives, in metres. Real models can replace any.</summary>
    private void BuildModels()
    {
        if (models[1] != null || gunSocket == null || pistolModel == null) return;
        var dark = Lit(new Color(0.16f, 0.16f, 0.18f), 0.5f, 0.8f); var black = Lit(new Color(0.1f, 0.1f, 0.12f), 0.4f, 0.6f);
        var wood = Lit(new Color(0.36f, 0.24f, 0.14f), 0.3f, 0f); var darkWood = Lit(new Color(0.3f, 0.2f, 0.12f), 0.3f, 0f);

        // Rifle.
        var rm = InPistolFrame("RifleModel");
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.28f), new Vector3(0.035f, 0.04f, 0.62f), Quaternion.identity, dark);
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.22f), new Vector3(0.045f, 0.05f, 0.34f), Quaternion.identity, wood);
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.16f), new Vector3(0.04f, 0.09f, 0.24f), Quaternion.Euler(-12f, 0f, 0f), wood);
        Part(rm.transform, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0.1f), new Vector3(0.02f, 0.03f, 0.1f), Quaternion.identity, dark);
        models[(int)WeaponKind.Rifle] = rm; muzzles[(int)WeaponKind.Rifle] = MuzzleAt(rm, new Vector3(0f, 0.02f, 0.6f));

        // Shotgun: shorter, two barrels side by side, fat stock.
        var sg = InPistolFrame("ShotgunModel");
        Part(sg.transform, PrimitiveType.Cube, new Vector3(-0.02f, 0.02f, 0.22f), new Vector3(0.035f, 0.035f, 0.5f), Quaternion.identity, dark);
        Part(sg.transform, PrimitiveType.Cube, new Vector3(0.02f, 0.02f, 0.22f), new Vector3(0.035f, 0.035f, 0.5f), Quaternion.identity, dark);
        Part(sg.transform, PrimitiveType.Cube, new Vector3(0f, -0.01f, 0.16f), new Vector3(0.075f, 0.05f, 0.24f), Quaternion.identity, wood);
        Part(sg.transform, PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.15f), new Vector3(0.05f, 0.1f, 0.26f), Quaternion.Euler(-12f, 0f, 0f), wood);
        models[(int)WeaponKind.Shotgun] = sg; muzzles[(int)WeaponKind.Shotgun] = MuzzleAt(sg, new Vector3(0f, 0.02f, 0.48f));

        // Flare gun: a stubby orange pistol.
        var fg = InPistolFrame("FlareGunModel");
        Part(fg.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.08f), new Vector3(0.05f, 0.06f, 0.2f), Quaternion.identity, Lit(new Color(0.9f, 0.4f, 0.12f), 0.4f, 0f));
        Part(fg.transform, PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.03f), new Vector3(0.035f, 0.1f, 0.05f), Quaternion.Euler(15f, 0f, 0f), Lit(new Color(0.25f, 0.12f, 0.06f), 0.3f, 0f));
        models[(int)WeaponKind.FlareGun] = fg; muzzles[(int)WeaponKind.FlareGun] = MuzzleAt(fg, new Vector3(0f, 0.02f, 0.2f));

        // Auto rifle: black, boxy, a curved magazine hanging under it.
        var ar = InPistolFrame("AutoRifleModel");
        Part(ar.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.26f), new Vector3(0.035f, 0.04f, 0.5f), Quaternion.identity, dark);
        Part(ar.transform, PrimitiveType.Cube, new Vector3(0f, 0.01f, -0.02f), new Vector3(0.05f, 0.07f, 0.34f), Quaternion.identity, black);
        Part(ar.transform, PrimitiveType.Cube, new Vector3(0f, -0.09f, 0.02f), new Vector3(0.035f, 0.14f, 0.06f), Quaternion.Euler(15f, 0f, 0f), black);
        Part(ar.transform, PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.24f), new Vector3(0.035f, 0.06f, 0.16f), Quaternion.identity, dark);
        Part(ar.transform, PrimitiveType.Cube, new Vector3(0f, 0.065f, 0f), new Vector3(0.02f, 0.02f, 0.2f), Quaternion.identity, dark);
        models[(int)WeaponKind.AutoRifle] = ar; muzzles[(int)WeaponKind.AutoRifle] = MuzzleAt(ar, new Vector3(0f, 0.02f, 0.52f));

        // Sniper: a long barrel, dark stock, a scope on top.
        var sn = InPistolFrame("SniperModel");
        Part(sn.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.38f), new Vector3(0.03f, 0.03f, 0.84f), Quaternion.identity, dark);
        Part(sn.transform, PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.14f), new Vector3(0.045f, 0.05f, 0.44f), Quaternion.identity, darkWood);
        Part(sn.transform, PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.2f), new Vector3(0.04f, 0.09f, 0.26f), Quaternion.Euler(-12f, 0f, 0f), darkWood);
        Part(sn.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.075f, 0.02f), new Vector3(0.035f, 0.11f, 0.035f), Quaternion.Euler(90f, 0f, 0f), dark);
        models[(int)WeaponKind.Sniper] = sn; muzzles[(int)WeaponKind.Sniper] = MuzzleAt(sn, new Vector3(0f, 0.02f, 0.8f));

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
        models[(int)WeaponKind.Bow] = bm; muzzles[(int)WeaponKind.Bow] = MuzzleAt(bm, new Vector3(0f, 0f, 0.1f));
    }

    private void ShowWeapon()
    {
        BuildModels();
        for (int i = 0; i < Kinds; i++) if (models[i] != null) models[i].SetActive(i == (int)Current);
    }

    private Transform MuzzleNow => muzzles[(int)Current] != null ? muzzles[(int)Current] : muzzle;

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (kb != null && !Intro.Playing && !GameHUD.Paused)
        {
            if (kb.digit1Key.wasPressedThisFrame) Equip(WeaponKind.Pistol);
            if (kb.digit2Key.wasPressedThisFrame) Equip(WeaponKind.Rifle);
            if (kb.digit3Key.wasPressedThisFrame) Equip(WeaponKind.Shotgun);
            if (kb.digit4Key.wasPressedThisFrame) Equip(WeaponKind.FlareGun);
            if (kb.digit5Key.wasPressedThisFrame) Equip(WeaponKind.AutoRifle);
            if (kb.digit6Key.wasPressedThisFrame) Equip(WeaponKind.Sniper);
        }
        bool trigger = mouse != null && (Stats.automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame);
        if (trigger && !Encounter.Active && Time.time >= Encounter.SuppressFireUntil && !GameHUD.MouseOverBar()) TryFire();
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
        if (pistolReload && !magDropped && p >= 0.3f)
        {
            magDropped = true;
            Vector3 from = lHand != null ? lHand.position : transform.position + Vector3.up * 0.8f;
            var dropped = MakeMag();
            dropped.transform.localScale = MagSize * 2.1f;
            dropped.transform.position = from;
            dropped.transform.rotation = Random.rotation;
            dropped.AddComponent<DroppedMag>();
        }
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

        // Reloads posed by hand: the pistol's magazine swap, and the same left-arm pull for the shotgun and flare gun.
        float phase = ReloadPhase;
        ReloadProps(phase);
        bool posedReload = IsReloading && (Current == WeaponKind.Pistol || Current == WeaponKind.Shotgun || Current == WeaponKind.FlareGun);
        float down = posedReload ? ArmDown(phase) : 0f;
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
            if ((Current == WeaponKind.Bow && Reserve > 0) || (Current != WeaponKind.Bow && Current != WeaponKind.FlareGun && Reserve > 0 && w.automatic)) { TryReload(); return; }
            if (Current == WeaponKind.Bow && Reserve > 0) { TryReload(); return; }
            Debug.Log("Click. Empty.");
            nextFireTime = Time.time + 0.2f;
            return;
        }

        if (Current == WeaponKind.FlareGun) { if (!Inventory.Take(ItemKind.Flare)) return; }
        else loaded[(int)Current]--;
        nextFireTime = Time.time + w.fireCooldown;
        kick = 1f;
        bool quiet = Current == WeaponKind.Bow;
        if (quiet)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.Bell != null) AudioManager.Instance.Play(AudioManager.Instance.Bell, transform.position, 0.12f, 20f, 3.2f);   // the string, near enough
        }
        else if (AudioManager.Instance != null && w.volume > 0f) AudioManager.Instance.Play(AudioManager.Instance.Gunshot, transform.position, w.volume, 200f, w.pitch * Random.Range(0.95f, 1.05f));
        if (w.noiseRadius > 0f) Stalker.Noise(transform.position, w.noiseRadius);   // a shot carries: the far ones come. Only the one it hits runs (Stalker.Hit).
        if (w.flash) { flashUntil = Time.time + flashSeconds; SetFlash(true); }

        Transform mz = MuzzleNow;
        Vector3 origin = transform.position + Vector3.up * 0.9f + aimDir * 0.7f;

        if (Current == WeaponKind.FlareGun)
        {
            // A flare, fired: it arcs out to where she aimed and burns there twenty seconds.
            Vector3 to = AimPointClamped(w.range);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(vis.GetComponent<Collider>());
            vis.transform.localScale = new Vector3(0.05f, 0.16f, 0.05f);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", new Color(1f, 0.45f, 0.25f)); vis.GetComponent<Renderer>().sharedMaterial = m;
            Thrown.Launch(vis, mz != null ? mz.position : origin, new Vector3(to.x, 0.05f, to.z), 0.5f, p => Flare.Ignite(p, 20f));
            return;
        }

        // Scatter, if the weapon has any.
        Vector3 dir = w.spread > 0f ? Quaternion.Euler(0f, Random.Range(-w.spread, w.spread), 0f) * aimDir : aimDir;

        RaycastHit[] hits = Physics.SphereCastAll(origin, w.shotRadius, dir, w.range, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Stalker target = null;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.root == transform.root) continue;
            Stalker s = h.collider.GetComponentInParent<Stalker>();
            if (s == null) continue;
            if (Current == WeaponKind.Shotgun) { s.Hit(dir * w.knockback, w.hitWeight); if (h.distance < best) { best = h.distance; target = s; } continue; }   // the spread takes everything in it
            if (h.distance < best) { best = h.distance; target = s; }
        }
        if (target != null && Current != WeaponKind.Shotgun) target.Hit(dir * w.knockback, w.hitWeight);

        // Where it stopped: the stalker, a tree, the snow, or the end of its reach.
        Vector3 stop = origin + dir * w.range; float stopAt = w.range; bool hitSomething = target != null;
        foreach (RaycastHit h in Physics.RaycastAll(origin, dir, w.range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.root == transform.root || h.collider.GetComponentInParent<Stalker>() != null) continue;
            if (h.distance < stopAt) { stopAt = h.distance; stop = h.point; }
        }
        if (target != null && best < stopAt) { stop = origin + dir * best; stopAt = best; }
        Vector3 from = mz != null ? mz.position : origin;
        if (quiet) Arrow.Loose(from, stop, dir, target == null || Random.value < 0.5f);
        else if (Current == WeaponKind.Shotgun)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 d = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-9f, 9f), 0f) * dir;
                Tracer.Spawn(from, origin + d * Mathf.Min(stopAt, w.range) * Random.Range(0.7f, 1f), hitSomething);
            }
        }
        else if (Current == WeaponKind.Sniper) { Tracer.Spawn(from, stop, hitSomething); Tracer.Spawn(from, stop, hitSomething); }   // a brighter line
        else Tracer.Spawn(from, stop, hitSomething);
    }

    private void TryReload()
    {
        if (IsReloading || Current == WeaponKind.FlareGun) return;   // the flare gun takes the next flare by itself
        if (Reserve <= 0 || Loaded >= Stats.magazine) return;
        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        IsReloading = true;
        reloadStart = Time.time; reloadDuration = Stats.reloadSeconds * HomeBonuses.ReloadMultiplier; magDropped = false;
        lastShotTime = Time.time;   // arms up for it, as if she had just fired
        bool rifleClip = Current == WeaponKind.Rifle || Current == WeaponKind.AutoRifle || Current == WeaponKind.Sniper;
        if (animator != null && rifleClip && HasParameter(animator, "Reload")) animator.SetTrigger("Reload");
        yield return new WaitForSeconds(reloadDuration);
        int i = (int)Current, p = Pool(Current);
        if (p >= 0)
        {
            int need = Stats.magazine - loaded[i];
            int take = Mathf.Min(need, pool[p]);
            loaded[i] += take;
            pool[p] -= take;
        }
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
        int i = (int)Current, p = Pool(Current); if (p < 0) return 0;
        int fromReserve = Mathf.Min(rounds, pool[p]);
        pool[p] -= fromReserve;
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

    private Vector3 AimPointClamped(float maxRange)
    {
        Vector3 fallback = transform.position + aimDir * maxRange * 0.6f;
        if (cam == null || Mouse.current == null) return fallback;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!ground.Raycast(ray, out float enter)) return fallback;
        Vector3 d = ray.GetPoint(enter) - transform.position; d.y = 0f;
        if (d.magnitude > maxRange) d = d.normalized * maxRange;
        return transform.position + d;
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
