using System.Collections;
using UnityEngine;

/// <summary>
/// A body should hit the snow and stop. The flying-back clip keeps drifting after the
/// landing, so once the death state is most of the way through, the animator is frozen on
/// that frame and the body lies where it fell.
/// </summary>
public static class DeathPose
{
    /// <summary>How far through the death clip the body is down. Frozen there.</summary>
    public const float LandedAt = 0.47f;   // first flat contact after the clip's roll; the slide onto the side that follows is cut (was 0.62)

    public static IEnumerator HoldWhenDown(Animator animator, string stateName)
    {
        if (animator == null) yield break;
        float giveUp = Time.time + 6f;
        // Wait for the transition into the state, then for the landing.
        while (Time.time < giveUp)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateName) && info.normalizedTime >= LandedAt) break;
            yield return null;
        }
        if (animator != null) animator.speed = 0f;
    }
}
