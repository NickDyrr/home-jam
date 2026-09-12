using UnityEngine;

/// <summary>
/// Trigger volume that moves the player to another floor. Put one at the top
/// of a staircase with its target on the landing above, and one in the
/// stairwell above with its target at the foot of the stairs. The
/// CharacterController is disabled for the move so it does not fight the
/// teleport. A short cooldown stops the two links bouncing the player back
/// and forth.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class StairLink : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float cooldown = 0.6f;

    private static float lastMoveTime = -10f;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryMove(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryMove(other);
    }

    private void TryMove(Collider other)
    {
        if (target == null || !other.CompareTag("Player")) return;
        if (Time.time - lastMoveTime < cooldown) return;

        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        other.transform.position = target.position;
        if (cc != null) cc.enabled = true;
        lastMoveTime = Time.time;
    }
}
