using UnityEngine;

/// <summary>
/// The table outside where the house gets built out. Stand near it and press E.
/// It only works once enough survivors are home for the next level; otherwise
/// it says how many more are needed. Shows a floating prompt while in range.
/// </summary>
public class Workbench : MonoBehaviour
{
    [SerializeField] private float interactRadius = 2.4f;
    [SerializeField] private TextMesh prompt;

    private Transform player;
    private Transform cam;
    private float noteUntil;

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
        if (player == null || Home.Instance == null) return;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        bool near = d.sqrMagnitude <= interactRadius * interactRadius;
        int need = Home.Instance.NextUpgradeNeeded;
        int home = Home.Instance.SurvivorsHome;
        bool enough = Home.Instance.CanBuildNext;

        if (prompt != null)
        {
            prompt.gameObject.SetActive(near);
            if (near)
            {
                if (Time.time < noteUntil) prompt.text = $"Not enough survivors to build.\n{need - home} more.";
                else if (need < 0) prompt.text = "Nothing more to build";
                else if (enough) prompt.text = "E   Build out the house";
                else prompt.text = $"Build out the house\n{home} / {need} survivors home";
                if (cam != null) prompt.transform.rotation = cam.rotation;
            }
        }

        if (near && need >= 0 && InteractPressed())
        {
            if (enough) Home.Instance.Upgrade();
            else noteUntil = Time.time + 2.5f;
        }
    }

    private static bool InteractPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
    }
}
