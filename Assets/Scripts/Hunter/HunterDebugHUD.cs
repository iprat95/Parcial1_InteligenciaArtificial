using UnityEngine;

public class HunterDebugHUD : MonoBehaviour
{
    private HunterAgent hunter;

    private void Awake()
    {
        if (hunter == null) hunter = GetComponent<HunterAgent>();
    }

    private void OnGUI()
    {
        if (hunter == null) return;

        GUI.Box(new Rect(10, 10, 340, 110), "NPC Cazador - FSM");
        GUI.Label(new Rect(20, 35, 320, 20), $"Estado actual: {hunter.CurrentStateName}");
        GUI.Label(new Rect(20, 55, 320, 20), $"Objetivo actual: {(hunter.CurrentTarget != null ? hunter.CurrentTarget.name : "-")}");
        GUI.Label(new Rect(20, 75, 320, 20), $"Boids detectados: {hunter.DetectedBoidsCount}");
        GUI.Label(new Rect(20, 95, 320, 20), $"Ultima accion: {hunter.LastAction}");
    }
}
