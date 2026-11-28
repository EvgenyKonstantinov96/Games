using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>Small bounded spring for the two authored ponytail joints.</summary>
    [DefaultExecutionOrder(20)]
    sealed class ZSRSecondaryMotion : MonoBehaviour
    {
        Transform neck, tie, tail;
        Quaternion tieRest, tailRest;
        Vector3 previous, previousVelocity;
        Vector2 angle, angularVelocity;

        internal static void Attach(Transform root, Transform head, Transform first, Transform second)
        {
            if (head == null || first == null || second == null) return;
            var motion = root.gameObject.AddComponent<ZSRSecondaryMotion>();
            motion.neck = head;
            motion.tie = first;
            motion.tail = second;
            motion.tieRest = first.localRotation;
            motion.tailRest = second.localRotation;
            motion.previous = head.position;
        }

        void LateUpdate()
        {
            if (neck == null) return;
            var game = ZSRGame.Instance;
            if (game != null && !game.IsRunning)
            {
                previous = neck.position;
                previousVelocity = Vector3.zero;
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (dt <= 0f) return;
            Vector3 velocity = (neck.position - previous) / dt;
            Vector3 acceleration = (velocity - previousVelocity) / dt;
            var target = new Vector2(Mathf.Clamp(-acceleration.y * .19f, -7f, 7f),
                Mathf.Clamp(acceleration.x * .17f, -9f, 9f));
            angle = Vector2.SmoothDamp(angle, target, ref angularVelocity, .17f, 110f, dt);
            tie.localRotation = tieRest * Quaternion.Euler(angle.x*.42f, 0f, angle.y*.42f);
            tail.localRotation = tailRest * Quaternion.Euler(angle.x, 0f, angle.y);
            previous = neck.position;
            previousVelocity = velocity;
        }
    }
}
