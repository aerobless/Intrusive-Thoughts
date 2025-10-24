using UnityEngine;
using UnityEngine.AI;

namespace Synty.AnimationBaseLocomotion.Samples
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AgentTextOutput))]
    public class AgentHarness : MonoBehaviour
    {
        [Header("Navigation")]
        public float rotationSpeed = 6f;
        public float arrivalThreshold = 0.3f;

        [Header("Animation thresholds")]
        public float walkThreshold = 0.2f;
        public float runThreshold = 1.2f;

        NavMeshAgent navmeshAgent;
        Animator anim;
        AgentTextOutput textOutput;
        GameObject currentTarget;

        public bool IsIdle
        {
            get
            {
                if (navmeshAgent == null)
                    return true;

                if (currentTarget != null)
                    return false;

                if (navmeshAgent.pathPending)
                    return false;

                if (navmeshAgent.hasPath && navmeshAgent.remainingDistance > Mathf.Max(navmeshAgent.stoppingDistance, arrivalThreshold))
                    return false;

                return navmeshAgent.velocity.sqrMagnitude < 0.01f;
            }
        }

        public AgentTextOutput TextOutput
        {
            get
            {
                EnsureComponents();
                return textOutput;
            }
        }

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

        void EnsureComponents()
        {
            if (navmeshAgent == null)
                navmeshAgent = GetComponent<NavMeshAgent>();
            if (anim == null)
                anim = GetComponent<Animator>();
            if (textOutput == null)
                textOutput = GetComponent<AgentTextOutput>();
        }

        void Awake()
        {
            EnsureComponents();
        }

        void Start()
        {
            EnsureComponents();

            // Initialize
            anim.SetBool(IsGrounded, true);
            anim.SetBool(IsStopped, true);
            anim.SetBool(IsStarting, false);
            anim.SetFloat(FallingDuration, 0f);

            ResetSequence();
        }

        void Update()
        {
            HandleNavigation();
            UpdateAnimation();
        }

        void HandleNavigation()
        {
            EnsureComponents();
            if (navmeshAgent == null)
                return;

            if (currentTarget == null)
                return;

            navmeshAgent.SetDestination(currentTarget.transform.position);

            if (!HasArrivedAtDestination())
                return;

            navmeshAgent.isStopped = true;
            navmeshAgent.ResetPath();

            currentTarget = null;
        }

        bool HasArrivedAtDestination()
        {
            if (navmeshAgent == null)
                return false;

            if (navmeshAgent.pathPending || currentTarget == null)
                return false;

            float threshold = Mathf.Max(navmeshAgent.stoppingDistance, arrivalThreshold);
            if (navmeshAgent.remainingDistance > threshold)
                return false;

            // Ensure the agent has essentially come to a stop
            if (navmeshAgent.hasPath && navmeshAgent.velocity.sqrMagnitude > 0.01f)
                return false;

            return true;
        }

        void UpdateAnimation()
        {
            EnsureComponents();
            if (navmeshAgent == null || anim == null)
                return;

            Vector3 velocity = navmeshAgent.velocity;
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

        void ResetSequence()
        {
            currentTarget = null;
            navmeshAgent.isStopped = true;
            navmeshAgent.ResetPath();
        }

        public void WalkTo(GameObject target)
        {
            if (target == null)
            {
                Debug.LogWarning("AgentHarness: Attempted to walk to a null target.");
                return;
            }

            currentTarget = target;
            navmeshAgent.isStopped = false;
            navmeshAgent.SetDestination(currentTarget.transform.position);
        }
    }
}
