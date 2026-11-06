using UnityEngine;

//Base de todos los agentes que se mueven con steering (Boids, Cazador y Lobos)
public abstract class SteeringAgent : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField]
    protected float _maxSpeed = 5f;
    [SerializeField]
    protected float _maxForce = 10f;
    [SerializeField, Tooltip("Radio del cuerpo, para no superponerse con otros agentes ni paredes")]
    protected float _radius = 0.5f;
    [SerializeField]
    protected float _maxPredictionTime = 1f;

    protected Vector3 _velocity;
    protected float _speedLimit;
    private Collider _ownCollider;
    private readonly Collider[] _wallHits = new Collider[16];

    //Lo que el agente no puede atravesar al moverse (paredes). Los Boids no lo usan.
    protected virtual LayerMask CollisionMask => default;

    public Vector3 Velocity => _velocity;
    public float MaxSpeed => _maxSpeed;
    public float Radius => _radius;
    public Vector3 Position => transform.position.SetY(0f);

    public float DistanceTo(Vector3 position) => Position.FlatDistance(position);

    #region Steering Behaviors

    protected Vector3 CalculateSeek(Vector3 targetPosition, float speed)
    {
        Vector3 desired = (targetPosition - transform.position).SetY(0f).normalized * speed;
        return CalculateSteering(desired);
    }

    protected Vector3 CalculateFlee(Vector3 targetPosition, float speed)
    {
        Vector3 desired = (targetPosition - transform.position).SetY(0f).normalized * speed;
        return CalculateSteering(-desired);
    }

    //Frena dentro de arriveRadius y se detiene a stopDistance del objetivo (para no superponerse)
    protected Vector3 CalculateArrive(Vector3 targetPosition, float speed, float arriveRadius, float stopDistance = 0f)
    {
        Vector3 dir = (targetPosition - transform.position).SetY(0f);
        float distance = dir.magnitude - stopDistance;

        if (distance <= 0f) return CalculateSteering(Vector3.zero);

        if (distance <= arriveRadius)
        {
            float percentDistance = distance / arriveRadius;
            speed *= percentDistance;
        }

        Vector3 desired = dir.normalized * speed;
        return CalculateSteering(desired);
    }

    protected Vector3 CalculatePursuit(SteeringAgent target, float speed) => CalculateSeek(GetFuturePosition(target, speed), speed);
    protected Vector3 CalculateEvade(SteeringAgent target, float speed) => CalculateFlee(GetFuturePosition(target, speed), speed);

    //FuturePosition = Position + Velocity * Time;
    protected Vector3 GetFuturePosition(SteeringAgent target, float speed)
    {
        float distanceToTarget = DistanceTo(target.Position);
        float predictedTime = distanceToTarget / Mathf.Max(0.1f, speed + target.Velocity.magnitude);
        predictedTime = Mathf.Min(predictedTime, _maxPredictionTime);
        return target.Position + target.Velocity * predictedTime;
    }

    protected Vector3 CalculateSteering(Vector3 desired)
    {
        Vector3 steering = desired - _velocity;
        steering = Vector3.ClampMagnitude(steering, _maxForce);
        return steering * Time.deltaTime;
    }

    #endregion

    protected void ApplyForce(Vector3 force, float weight = 1f) => _velocity += force * weight;

    protected void Move()
    {
        _velocity = Vector3.ClampMagnitude(_velocity.SetY(0f), Mathf.Max(0f, _speedLimit));

        //Si un frame dura mucho (tirones o velocidad x3) muevo en varios pasos chicos
        //para no meterme en una pared, sin perder velocidad
        Vector3 step = _velocity * Time.deltaTime;
        float maxStep = Mathf.Max(0.05f, _radius * 0.8f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(step.magnitude / maxStep), 1, 8);
        LayerMask walls = CollisionMask;

        for (int i = 0; i < steps; i++)
        {
            transform.position += step / steps;
            if (walls.value != 0) PushOutOfWalls(walls, _wallHits);
        }

        if (_velocity.sqrMagnitude > 0.01f)
        {
            transform.forward = Vector3.Slerp(transform.forward, _velocity.normalized, 12f * Time.deltaTime);
        }
    }

    public void Stop()
    {
        _velocity = Vector3.zero;
        _speedLimit = 0f;
    }

    public void LookAt(Vector3 position)
    {
        Vector3 dir = (position - transform.position).SetY(0f);
        if (dir.sqrMagnitude > 0.001f) transform.forward = dir.normalized;
    }

    //Si quedo pegado a una pared (o barrera) me empuja hacia afuera
    protected void PushOutOfWalls(LayerMask mask, Collider[] buffer)
    {
        if (mask.value == 0) return;
        if (_ownCollider == null) _ownCollider = GetComponent<Collider>();

        Vector3 center = Position + Vector3.up * 0.5f;
        int count = Physics.OverlapSphereNonAlloc(center, _radius, buffer, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider wall = buffer[i];
            Vector3 away = (center - wall.ClosestPoint(center)).SetY(0f);
            float distance = away.magnitude;

            if (distance < 0.0001f)
            {
                //El centro quedo adentro del collider: uso la penetracion real
                if (_ownCollider != null && Physics.ComputePenetration(_ownCollider, transform.position, transform.rotation,
                        wall, wall.transform.position, wall.transform.rotation, out Vector3 direction, out float depth))
                {
                    transform.position += direction.SetY(0f) * depth;
                    center = Position + Vector3.up * 0.5f;
                }
                continue;
            }
            if (distance >= _radius) continue;

            Vector3 normal = away / distance;
            transform.position += normal * (_radius - distance);
            center = Position + Vector3.up * 0.5f;

            float into = Vector3.Dot(_velocity, normal);
            if (into < 0f) _velocity -= normal * into;
        }
    }
}
