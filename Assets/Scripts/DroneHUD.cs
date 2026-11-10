// DroneHUD.cs
// Live on-screen dashboard for demos and report screenshots.
// Purely visual: reads stats from the agent, never changes them.

using UnityEngine;
using Unity.MLAgents.Policies;

public class DroneHUD : MonoBehaviour
{
    public LethaboDroneAgent agent;
    public string title = "Autonomous Robot Navigation  |  ML-Agents PPO";

    GUIStyle titleStyle, textStyle, boxStyle;
    BehaviorParameters behavior;
    Texture2D bg;

    void Start()
    {
        if (agent != null) behavior = agent.GetComponent<BehaviorParameters>();
        bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
        bg.Apply();
    }

    void OnGUI()
    {
        if (agent == null) return;

        if (titleStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = bg;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            textStyle.normal.textColor = Color.white;
        }

        int done = agent.EpisodesFinished;
        float rate = done > 0 ? 100f * agent.Successes / done : 0f;
        string mode = behavior != null ? behavior.BehaviorType.ToString() : "-";
        string model = (behavior != null && behavior.Model != null) ? behavior.Model.name : "none (training)";

        GUI.Box(new Rect(10, 10, 430, 175), GUIContent.none, boxStyle);
        GUI.Label(new Rect(22, 16, 410, 26), title, titleStyle);
        GUI.Label(new Rect(22, 46, 410, 22), $"Episode:            {done + 1}", textStyle);
        GUI.Label(new Rect(22, 68, 410, 22), $"Targets reached:    {agent.Successes}  ({rate:0.0}%)", textStyle);
        GUI.Label(new Rect(22, 90, 410, 22), $"Obstacle hits:      {agent.ObstacleHits}", textStyle);
        GUI.Label(new Rect(22, 112, 410, 22), $"Episode reward:     {agent.GetCumulativeReward():0.00}", textStyle);
        GUI.Label(new Rect(22, 134, 410, 22), $"Mode:               {mode}", textStyle);
        GUI.Label(new Rect(22, 156, 410, 22), $"Model:              {model}", textStyle);
    }
}
