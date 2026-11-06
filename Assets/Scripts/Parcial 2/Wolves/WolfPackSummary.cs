using System.Text;
using UnityEngine;

//Resumen de los lobos para el HUD: estado, posicion conocida del Cazador y uso de A*
public class WolfPackSummary : MonoBehaviour, IHudInfo
{
    [SerializeField]
    private Wolf[] _wolves;

    public int HudOrder => 5;

    public void Setup(Wolf[] wolves) => _wolves = wolves;

    public void AppendHudInfo(StringBuilder sb)
    {
        sb.AppendLine($"<b>LOBOS ({_wolves.Length})</b>");
        foreach (Wolf wolf in _wolves)
        {
            if (wolf == null || !wolf.FSM.InValidState) continue;

            string stateHex = FeedbackResources.ToHex(Wolf.GetStateColor(wolf.FSM.CurrentState));
            string wolfHex = FeedbackResources.ToHex(wolf.WolfColor);
            string memory = wolf.HasLastKnownPosition
                ? $"pos. conocida ({wolf.LastKnownPosition.x:0}, {wolf.LastKnownPosition.z:0})"
                : "sin pista";
            string path = wolf.PathFollower.HasPath ? $" | A*: {wolf.PathFollower.NodeCount} nodos" : "";
            string seeing = wolf.CanSeeHunter ? " <color=#FF5A4A><b>¡LO VE!</b></color>" : "";

            sb.AppendLine($"<color=#{wolfHex}>{wolf.name}</color>: <color=#{stateHex}><b>{wolf.FSM.CurrentStateName}</b></color>{seeing}");
            sb.AppendLine($"   {memory}{path}");
        }
    }
}
