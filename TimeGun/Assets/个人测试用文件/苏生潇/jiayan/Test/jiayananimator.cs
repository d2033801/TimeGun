using UnityEngine;

public class CharacterIK : MonoBehaviour
{
    public Animator animator;

    [Header("手部目标")]
    public Transform rightHandTarget;
    public Transform leftHandTarget;

    [Header("脚部目标（Optional）")]
    public Transform rightFootTarget;
    public Transform leftFootTarget;

    [Range(0, 1)] public float handIKWeight = 1f;
    [Range(0, 1)] public float footIKWeight = 1f;

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // ---- 手部 IK ----
        if (rightHandTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
        }
        if (leftHandTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handIKWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handIKWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
        }

        // ---- 脚部 IK ----
        if (rightFootTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, footIKWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, footIKWeight);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootTarget.rotation);
        }
        if (leftFootTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footIKWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, footIKWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootTarget.rotation);
        }
    }
}
