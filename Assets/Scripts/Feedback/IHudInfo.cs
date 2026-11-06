using System.Text;

//Un componente que quiere mostrar información en el HUD implementa esta interfaz.
//El HUD solo LEE esta información; no le da órdenes a ningún agente.
public interface IHudInfo
{
    //Orden de aparición en el panel (menor = más arriba).
    int HudOrder { get; }

    void AppendHudInfo(StringBuilder sb);
}
