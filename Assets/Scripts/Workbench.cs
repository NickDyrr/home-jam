using UnityEngine;

/// <summary>
/// The bench outside where the house gets upgraded. Stand near it and press
/// the interact key to go up a house level. Upgrades are free for now so the
/// levels can be tested; the cost hook is the one place to add later.
/// Shows a floating prompt only while the player is in range.
/// </summary>
public class Workbench : MonoBehaviour
{
    [SerializeField] private float interactRadius = 2.4f;
    [SerializeField] private TextMesh prompt;
    [SerializeField] private string upgradeText = "E   Upgrade home";
    [SerializeField] private string maxedText = "Home fully upgraded";

    private Transform player;
    private Transform cam;

    private void Awake()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (Camera.main != null) cam = Camera.main.transform;
        if (prompt == null) prompt = GetComponentInChildren<TextMesh>(true);
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        bool near = d.sqrMagnitude <= interactRadius * interactRadius;
        bool canUpgrade = HouseView.Instance != null && HouseView.Instance.CanUpgrade;

        if (prompt != null)
        {
            prompt.gameObject.SetActive(near);
            if (near)
            {
                prompt.text = canUpgrade ? upgradeText : maxedText;
                if (cam != null) prompt.transform.rotation = cam.rotation;
            }
        }

        if (near && canUpgrade && InteractPressed())
        {
            if (Home.Instance != null) Home.Instance.Upgrade();
            else HouseView.Instance.Upgrade();
        }
    }

    private static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
