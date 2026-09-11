using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Waits in the dark. When the player gets close it follows, slower than the player.
/// The moment it is inside the HomeZone it counts as home and walks to its
/// settle spot, then stops for good.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Survivor : MonoBehaviour
{
    public enum State { Waiting, Following, Settling, Home }

    /// <summary>Every live survivor in the scene.</summary>
    public static readonly List<Survivor> All = new List<Survivor>();

    [SerializeField] private float noticeRadius = 2.5f;
    [SerializeField] private float followDistance = 1.6f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float settleTolerance = 0.25f;

    public State CurrentState { get; private set; } = State.Waiting;

    private CharacterController controller;
    private Transform player;
    private Vector3 settleSpot;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void OnEnable()  { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>The stalker got this one. Unrealized value, gone.</summary>
    public void Taken()
    {
        Debug.Log($"Survivor '{name}' was taken.");
        Destroy(gameObject);
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 move = Vector3.zero;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        switch (CurrentState)
        {
            case State.Waiting:
                if (toPlayer.magnitude <= noticeRadius)
                    CurrentState = State.Following;
                break;

            case State.Following:
                if (toPlayer.magnitude > followDistance)
                    move = toPlayer.normalized;

                if (HomeZone.Instance != null && HomeZone.Instance.Contains(transform.position))
                {
                    CurrentState = State.Settling;
                    settleSpot = Home.Instance != null
                        ? Home.Instance.SurvivorArrived(this)
                        : transform.position;
                }
                break;

            case State.Settling:
                Vector3 toSpot = settleSpot - transform.position;
                toSpot.y = 0f;
                if (toSpot.magnitude > settleTolerance)
                    move = toSpot.normalized;
                else
                    CurrentState = State.Home;
                break;

            case State.Home:
                break;
        }

        Vector3 velocity = move * moveSpeed;
        velocity.y = controller.isGrounded ? -1f : -9.81f;
        controller.Move(velocity * Time.deltaTime);

        if (move.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
    }
}
