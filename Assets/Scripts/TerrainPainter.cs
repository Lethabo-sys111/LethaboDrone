// TerrainPainter.cs
// Colours the terrain by height and slope (grass -> dirt -> rock) and repaints
// every time the agent generates new hills. Visual only; disabled in headless training.

using UnityEngine;

public class TerrainPainter : MonoBehaviour
{
    public LethaboDroneAgent agent;
    public Terrain terrain;

    public Color grass = new Color(0.30f, 0.55f, 0.22f);
    public Color dirt  = new Color(0.55f, 0.43f, 0.28f);
    public Color rock  = new Color(0.58f, 0.58f, 0.60f);
    public float maxHillHeight = 3f;     // world units (hill_height 0.3 x terrain height 10)
    public int alphamapResolution = 64;  // low = fast repaint

    TerrainData data;
    TerrainLayer[] originalLayers;

    void Start()
    {
        if (Application.isBatchMode || agent == null || terrain == null) { enabled = false; return; }

        data = terrain.terrainData;
        originalLayers = data.terrainLayers;
        data.alphamapResolution = alphamapResolution;
        data.terrainLayers = new[] { MakeLayer(grass, 0.10f), MakeLayer(dirt, 0.14f), MakeLayer(rock, 0.18f) };

        agent.TerrainRegenerated += Paint;
        Paint();
    }

    void OnDestroy()
    {
        if (agent != null) agent.TerrainRegenerated -= Paint;
        if (data != null) data.terrainLayers = originalLayers; // don't leave runtime layers in the asset
    }

    TerrainLayer MakeLayer(Color c, float noise)
    {
        const int n = 64;
        var tex = new Texture2D(n, n);
        float seed = Random.Range(0f, 100f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = (Mathf.PerlinNoise(seed + x * 0.2f, seed + y * 0.2f) - 0.5f) * 2f * noise;
                Color p = c * (1f + v);
                p.a = 1f;
                tex.SetPixel(x, y, p);
            }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        return new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(2f, 2f), smoothness = 0f, metallic = 0f };
    }

    void Paint()
    {
        int r = data.alphamapResolution;
        var maps = new float[r, r, 3];

        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float nx = (float)x / (r - 1);
                float nz = (float)y / (r - 1);
                float t = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / maxHillHeight);
                float steep = data.GetSteepness(nx, nz) / 90f;

                float g = Mathf.Clamp01(1f - t * 2f);
                float d = Mathf.Clamp01(1f - Mathf.Abs(t - 0.5f) * 2f);
                float k = Mathf.Max(Mathf.Clamp01(t * 2f - 1f), Mathf.Clamp01(steep * 2f));

                float sum = g + d + k + 0.0001f;
                maps[y, x, 0] = g / sum;
                maps[y, x, 1] = d / sum;
                maps[y, x, 2] = k / sum;
            }

        data.SetAlphamaps(0, 0, maps);
    }
}
