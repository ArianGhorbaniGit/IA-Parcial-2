using System.Text;
using UnityEngine;

//Resumen de la bandada para el HUD (solo lee el estado de cada Boid)
public class FlockSummary : MonoBehaviour, IHudInfo
{
    [SerializeField]
    private Boid[] _boids;

    public int HudOrder => 10;

    public void Setup(Boid[] boids) => _boids = boids;

    public void AppendHudInfo(StringBuilder sb)
    {
        int flocking = 0, evade = 0, arrive = 0, eating = 0, dead = 0, collected = 0;
        foreach (Boid boid in _boids)
        {
            if (boid == null) continue;
            switch (boid.CurrentState)
            {
                case Boid.BoidStates.Flocking: flocking++; break;
                case Boid.BoidStates.Evade: evade++; break;
                case Boid.BoidStates.Arrive: arrive++; break;
                case Boid.BoidStates.Eating: eating++; break;
                case Boid.BoidStates.Dead: dead++; break;
                case Boid.BoidStates.Collected: collected++; break;
            }
        }

        sb.AppendLine($"<b>BOIDS ({_boids.Length})</b>");
        sb.AppendLine($"{Tag(Boid.BoidStates.Flocking)}Flocking: {flocking}</color>   {Tag(Boid.BoidStates.Evade)}Evade: {evade}</color>");
        sb.AppendLine($"{Tag(Boid.BoidStates.Arrive)}Arrive: {arrive}</color>   {Tag(Boid.BoidStates.Eating)}Comiendo: {eating}</color>");
        sb.AppendLine($"<color=#FF7A7A>Eliminados: {dead}</color>   Recolectados (reapareciendo): {collected}");
    }

    private static string Tag(Boid.BoidStates state) => "<color=#" + FeedbackResources.ToHex(Boid.GetStateColor(state)) + ">";
}
