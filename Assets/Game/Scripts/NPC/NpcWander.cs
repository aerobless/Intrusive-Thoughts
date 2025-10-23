using UnityEngine;
using UnityEngine.AI;

namespace Synty.AnimationBaseLocomotion.Samples
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class NpcAdvancedAnimationController : MonoBehaviour
    {
        [Header("Navigation")]
        public float wanderRadius = 10f;
        public float wanderInterval = 5f;
        public float rotationSpeed = 6f;

        [Header("Animation thresholds")]
        public float walkThreshold = 0.2f;
        public float runThreshold = 1.2f;

        NavMeshAgent agent;
        Animator anim;
        float timer;

        // Animator hashes (from your screenshot)
        static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        static readonly int CurrentGait = Animator.StringToHash("CurrentGait");
        static readonly int IsWalking = Animator.StringToHash("IsWalking");
        static readonly int IsStopped = Animator.StringToHash("IsStopped");
        static readonly int IsStarting = Animator.StringToHash("IsStarting");
        static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        static readonly int FallingDuration = Animator.StringToHash("FallingDuration");
        static readonly int StrafeDirectionX = Animator.StringToHash("StrafeDirectionX");
        static readonly int StrafeDirectionZ = Animator.StringToHash("StrafeDirectionZ");
        static readonly int MovementInputPressed = Animator.StringToHash("MovementInputPressed");
        static readonly int MovementInputHeld = Animator.StringToHash("MovementInputHeld");
        static readonly int MovementInputTapped = Animator.StringToHash("MovementInputTapped");

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = GetComponent<Animator>();
            timer = wanderInterval;

            // Initialize
            anim.SetBool(IsGrounded, true);
            anim.SetBool(IsStopped, true);
            anim.SetBool(IsStarting, false);
            anim.SetFloat(FallingDuration, 0f);
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= wanderInterval)
            {
                agent.SetDestination(RandomNavSphere(transform.position, wanderRadius));
                timer = 0f;
            }

            UpdateAnimation();
        }

        void UpdateAnimation()
        {
            Vector3 velocity = agent.velocity;
            float speed = velocity.magnitude;
            bool isMoving = speed > 0.05f;

            // --- Movement input simulation ---
            anim.SetBool(MovementInputPressed, isMoving);
            anim.SetBool(MovementInputHeld, isMoving);
            anim.SetBool(MovementInputTapped, false);

            // --- Speed and gait ---
            anim.SetFloat(MoveSpeed, speed, 0.15f, Time.deltaTime);

            int gait = 0;
            if (speed > walkThreshold && speed < runThreshold) gait = 1;
            else if (speed >= runThreshold) gait = 2;
            anim.SetInteger(CurrentGait, gait);
            anim.SetBool(IsWalking, gait == 1);

            // --- Locomotion state flags ---
            anim.SetBool(IsStopped, !isMoving);
            anim.SetBool(IsStarting, isMoving && anim.GetBool(IsStopped));

            // --- Directional parameters ---
            Vector3 localVel = transform.InverseTransformDirection(velocity);
            anim.SetFloat(StrafeDirectionX, localVel.x);
            anim.SetFloat(StrafeDirectionZ, localVel.z);

            // --- Grounding ---
            anim.SetBool(IsGrounded, true);
            anim.SetFloat(FallingDuration, 0f);

            // --- Facing direction ---
            if (velocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        static Vector3 RandomNavSphere(Vector3 origin, float distance)
        {
            Vector3 random = Random.insideUnitSphere * distance + origin;
            if (NavMesh.SamplePosition(random, out var hit, distance, NavMesh.AllAreas))
                return hit.position;
            return origin;
        }
    }
}
