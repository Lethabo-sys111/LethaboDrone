// LethaboDroneAgent.cs
// Autonomous Robot Navigation - hover-robot version.
// A hovering robot starts in a defined start area, must reach a target in the
// opposite area, and avoid static obstacles (pillars) and the terrain.
// The terrain hills are regenerated every episode (dynamic terrain).
//
// Vector observations: 15 | Ray Perception Sensor: nearby obstacles
// Continuous actions: 3  -> [0] left/right, [1] up/down (altitude), [2] forward/backward

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class LethaboDroneAgent : Agent
{
    [Header("Scene references")]
    public Terrain terrain;           // the 10x10x10 terrain
    public Transform target;         // the target destination (no collider needed)
    public Transform[] obstacles;    // static pillars, tagged "Obstacle"

    [Header("Robot")]
    public float thrust = 8f;        // max force per axis (gravity is cancelled, so 0 = hover)
    public float successDistance = 0.7f;
    public float ceiling = 7f;       // max allowed height, so the robot must go AROUND pillars

    [Header("Terrain generation")]
    public float noiseScale = 0.12f;          // smaller = wider hills
    public float defaultHillHeight = 0.3f;    // fraction of terrain height (0.3 = 3 units)

    [Header("Rewards & punishments")]
    public float reachTargetReward = 1f;      // large positive
    public float progressScale = 0.1f;        // small +/- for moving closer / further
    public float obstaclePenalty = -1f;       // large negative
    public float crashPenalty = -0.5f;        // hitting terrain / leaving the area
    public float stepPenalty = -0.001f;       // encourages efficiency

    [Header("Experiment toggles")]
    public bool useGroundSensors = true;      // Hypothesis 1 toggle

    // Read-only stats for the on-screen HUD (do not affect learning)
    public int EpisodesFinished { get; private set; }
    public int Successes { get; private set; }
    public int ObstacleHits { get; private set; }

    // Fired after new hills are generated (used by TerrainPainter, visual only)
    public event System.Action TerrainRegenerated;

    Rigidbody rb;
    TrailRenderer trail;
    TerrainData data;
    int res;
    float prevDistance;
    const float Size = 10f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        TryGetComponent(out trail);
        data = terrain.terrainData;
        res = data.heightmapResolution;
    }

    public override void OnEpisodeBegin()
    {
        GenerateTerrain();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;

        // Defined start area in one corner, target in the opposite corner.
        // Which pair of corners is picked at random so the robot can't memorise one route.
        bool flipX = Random.value < 0.5f;
        bool flipZ = Random.value < 0.5f;
        transform.position = PointInArea(flipX ? 7f : 1f, flipZ ? 7f : 1f, 1.5f);
        target.position    = PointInArea(flipX ? 1f : 7f, flipZ ? 1f : 7f, 1.0f);

        PlaceObstacles();

        if (trail != null) trail.Clear(); // no streak across the reset teleport

        prevDistance = Vector3.Distance(transform.position, target.position);
    }

    // Dynamic terrain: new Perlin-noise hills every episode
    void GenerateTerrain()
    {
        float amp = Academy.Instance.EnvironmentParameters
                           .GetWithDefault("hill_height", defaultHillHeight);
        float ox = Random.Range(0f, 1000f);
        float oz = Random.Range(0f, 1000f);

        float[,] heights = new float[res, res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.PerlinNoise(ox + x * noiseScale, oz + z * noiseScale) * amp;

        data.SetHeights(0, 0, heights);
        TerrainRegenerated?.Invoke();
    }

    // Random point inside a 2x2 area starting at (minX, minZ), sitting on the terrain + offset
    Vector3 PointInArea(float minX, float minZ, float offset)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 p = origin + new Vector3(Random.Range(minX, minX + 2f), 0f, Random.Range(minZ, minZ + 2f));
        p.y = origin.y + terrain.SampleHeight(p) + offset;
        return p;
    }

    // Obstacles stay still during an episode, but are re-seated on the new hills each episode
    void PlaceObstacles()
    {
        Vector3 origin = terrain.transform.position;
        foreach (Transform obs in obstacles)
        {
            Vector3 p = obs.position;
            for (int tries = 0; tries < 30; tries++)
            {
                p = origin + new Vector3(Random.Range(2f, 8f), 0f, Random.Range(2f, 8f));
                if (FlatDistance(p, transform.position) > 1.5f && FlatDistance(p, target.position) > 1.5f)
                    break;
            }
            p.y = origin.y + terrain.SampleHeight(p);
            obs.position = p;
        }
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    float GroundClearance(Vector3 worldPos)
    {
        float ground = terrain.transform.position.y + terrain.SampleHeight(worldPos);
        return (transform.position.y - ground) / Size;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 origin = terrain.transform.position;

        sensor.AddObservation((transform.position - origin) / Size); // 3  robot position (Vector3)
        sensor.AddObservation((target.position - origin) / Size);    // 3  target position (Vector3)
        sensor.AddObservation(rb.linearVelocity / 10f);              // 3  robot velocity
        sensor.AddObservation(Vector3.Distance(transform.position, target.position) / Size); // 1
        sensor.AddObservation(GroundClearance(transform.position));  // 1  height above ground

        // 4 ground sensors: terrain clearance 1 unit away in each direction
        if (useGroundSensors)
        {
            sensor.AddObservation(GroundClearance(transform.position + Vector3.forward));
            sensor.AddObservation(GroundClearance(transform.position + Vector3.back));
            sensor.AddObservation(GroundClearance(transform.position + Vector3.left));
            sensor.AddObservation(GroundClearance(transform.position + Vector3.right));
        }
        else
        {
            for (int i = 0; i < 4; i++) sensor.AddObservation(0f); // keep size at 15
        }
        // Total vector observations = 15
        // Nearby obstacles come from the Ray Perception Sensor 3D component (added in the Inspector)
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.ContinuousActions;
        // a[0] = left(-)/right(+), a[1] = down(-)/up(+), a[2] = backward(-)/forward(+)
        rb.AddForce(-Physics.gravity * rb.mass); // cancel gravity: action 0 = hover
        rb.AddForce(new Vector3(a[0], a[1], a[2]) * thrust);

        AddReward(stepPenalty);

        // Small reward for moving closer, small punishment for moving away
        float dist = Vector3.Distance(transform.position, target.position);
        AddReward((prevDistance - dist) * progressScale);
        prevDistance = dist;

        // Reached the target: large reward
        if (dist < successDistance)
        {
            AddReward(reachTargetReward);
            Successes++;
            EpisodesFinished++;
            Academy.Instance.StatsRecorder.Add("Nav/SuccessRate", 1f);
            Academy.Instance.StatsRecorder.Add("Nav/ObstacleHits", 0f);
            EndEpisode();
            return;
        }

        // Left the area or flew above the ceiling
        Vector3 local = transform.position - terrain.transform.position;
        if (local.x < 0f || local.x > Size || local.z < 0f || local.z > Size || local.y > ceiling)
            Fail(crashPenalty, false);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
            Fail(obstaclePenalty, true);      // large punishment
        else if (collision.gameObject == terrain.gameObject)
            Fail(crashPenalty, false);        // crashed into a hill
    }

    void Fail(float penalty, bool hitObstacle)
    {
        AddReward(penalty);
        EpisodesFinished++;
        if (hitObstacle) ObstacleHits++;
        Academy.Instance.StatsRecorder.Add("Nav/SuccessRate", 0f);
        Academy.Instance.StatsRecorder.Add("Nav/ObstacleHits", hitObstacle ? 1f : 0f);
        EndEpisode();
    }

    // Manual test: W/S forward/back, A/D left/right, Space up, LeftShift down
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        a[0] = Input.GetAxis("Horizontal");
        a[2] = Input.GetAxis("Vertical");
        a[1] = Input.GetKey(KeyCode.Space) ? 1f : (Input.GetKey(KeyCode.LeftShift) ? -1f : 0f);
    }
}
