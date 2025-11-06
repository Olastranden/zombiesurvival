using UnityEngine;
using UnityEngine.AI;

public class HostileAI : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] waypoints;         // Set via inspector
    public float waypointTolerance = 0.5f; // Distance to consider "reached"

    [Header("Detection & Chase")]
    public Transform player;              // Assign Player transform in inspector
    public float detectionRadius = 8f;    // Radius in world units
    public float chaseStopDistance = 1.5f;// How close to get to player

    [Header("Movement & Rotation")]
    public float rotationSpeed = 10f;     // How fast to face player (slerp)
    public float moveSpeedMultiplier = 1f;// Agent speed multiplier if needed

    [Header("Misc")]
    public bool loopPatrol = true;        // If false, patrol back-and-forth

    private NavMeshAgent agent;
    private int currentWaypointIndex = 0;
    private bool chasing = false;
    private float sqrDetectionRadius;
    private Vector3 initialPosition;
    private float initialY;
    private bool patrolForward = true;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) Debug.LogError("NavMeshAgent missing.");
        if (player == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO) player = playerGO.transform;
        }

        sqrDetectionRadius = detectionRadius * detectionRadius;
        initialPosition = transform.position;
        initialY = transform.position.y;

        // We control rotation manually to constrain it to horizontal only.
        if (agent != null) agent.updateRotation = false;
    }

    void Start()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            agent.isStopped = false;
            agent.speed *= moveSpeedMultiplier;
            SetDestinationToCurrentWaypoint();
        }
        else
        {
            // If no waypoints, just stop agent until player in range
            agent.isStopped = true;
        }
    }

    void Update()
    {
        // Keep vertical position locked:
        if (Mathf.Abs(transform.position.y - initialY) > 0.001f)
        {
            Vector3 locked = transform.position;
            locked.y = initialY;
            transform.position = locked;
            // Also ensure agent's nextPosition doesn't drift vertically
            agent.nextPosition = locked;
        }

        if (player == null) return;

        float sqrDistToPlayer = (player.position - transform.position).sqrMagnitude;

        if (!chasing && sqrDistToPlayer <= sqrDetectionRadius)
        {
            // Enter chase
            chasing = true;
            agent.isStopped = false;
            agent.stoppingDistance = chaseStopDistance;
        }
        else if (chasing && sqrDistToPlayer > sqrDetectionRadius)
        {
            // Player left detection radius — return to patrol
            chasing = false;
            agent.stoppingDistance = 0f;
            if (waypoints != null && waypoints.Length > 0)
                SetDestinationToCurrentWaypoint();
            else
                agent.isStopped = true;
        }

        if (chasing)
        {
            // chase behavior
            Vector3 targetPosition = new Vector3(player.position.x, initialY, player.position.z);
            agent.SetDestination(targetPosition);

            // Rotate to face player horizontally
            RotateTowards(targetPosition);
        }
        else
        {
            // patrol behavior if available
            if (waypoints != null && waypoints.Length > 0)
            {
                if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
                {
                    AdvanceWaypoint();
                    SetDestinationToCurrentWaypoint();
                }

                // Face movement direction (optional)
                if (agent.velocity.sqrMagnitude > 0.01f)
                {
                    Vector3 lookTarget = transform.position + new Vector3(agent.velocity.x, 0f, agent.velocity.z);
                    RotateTowards(lookTarget);
                }
            }
        }
    }

    private void RotateTowards(Vector3 target)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0f; // zero out vertical so no tilt
        if (dir.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    private void SetDestinationToCurrentWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        agent.SetDestination(new Vector3(waypoints[currentWaypointIndex].position.x, initialY, waypoints[currentWaypointIndex].position.z));
    }

    private void AdvanceWaypoint()
    {
        if (waypoints.Length == 0) return;

        if (loopPatrol)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            // ping-pong
            if (patrolForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = waypoints.Length - 2 >= 0 ? waypoints.Length - 2 : 0;
                    patrolForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex < 0)
                {
                    currentWaypointIndex = 1 < waypoints.Length ? 1 : 0;
                    patrolForward = true;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}

