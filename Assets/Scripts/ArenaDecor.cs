// ArenaDecor.cs
// Presents the 10x10 arena as a raised block of earth ("diorama" look):
//  - a solid skirt wall that follows the hill edges down to the grass, rebuilt every
//    time the hills change, so the terrain never looks like it sinks into the ground
//  - grass, trees and rocks around the block
//  - glowing boundary lines (block base + height ceiling)
// Nothing here has a collider and nothing is inside the arena, so learning is unaffected.
// Disabled in headless training.

using System.Collections.Generic;
using UnityEngine;

public class ArenaDecor : MonoBehaviour
{
    public LethaboDroneAgent agent;     // used to read the terrain and ceiling height
    public int trees = 45;
    public int rocks = 30;
    public float innerRadius = 9f;      // keep decor well outside the arena
    public float outerRadius = 26f;
    public Color boundaryColor = new Color(0f, 0.9f, 1f, 0.8f);
    public int seed = 7;
    public float standHeight = 1.0f;    // how high the arena block stands above the grass
    public float groundSize = 25f;      // plane scale: 25 = 250x250 units, so its edge is never in view

    readonly Vector3 center = new Vector3(5f, 0f, 5f);
    Shader litShader;
    Terrain terrain;
    Mesh skirtMesh;

    void Start()
    {
        if (Application.isBatchMode) { enabled = false; return; }

        litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");
        terrain = agent != null ? agent.terrain : Terrain.activeTerrain;

        Random.State old = Random.state;
        Random.InitState(seed);

        // Grass, lowered so the arena stands on it as a raised block
        var ground = Make(PrimitiveType.Plane, center + Vector3.down * standHeight, new Vector3(groundSize, 1f, groundSize),
                          new Color(0.30f, 0.55f, 0.22f), "Ground");
        ground.transform.SetParent(transform);

        BuildSkirtObject();

        Material bark  = Mat(new Color(0.35f, 0.22f, 0.12f));
        Material stone = Mat(new Color(0.45f, 0.45f, 0.48f));

        for (int i = 0; i < trees; i++)
        {
            Vector3 p = RingPoint() + Vector3.down * standHeight;
            float h = Random.Range(1.5f, 3.5f);
            var trunk = Make(PrimitiveType.Cylinder, p + Vector3.up * h * 0.5f, new Vector3(0.25f, h * 0.5f, 0.25f), bark, "Trunk");
            var crown = Make(PrimitiveType.Sphere, p + Vector3.up * (h + 0.6f), Vector3.one * Random.Range(1.4f, 2.4f),
                             Color.Lerp(new Color(0.12f, 0.38f, 0.12f), new Color(0.30f, 0.55f, 0.15f), Random.value), "Crown");
            trunk.transform.SetParent(transform);
            crown.transform.SetParent(transform);
        }

        for (int i = 0; i < rocks; i++)
        {
            Vector3 p = RingPoint() + Vector3.down * standHeight;
            var rock = Make(PrimitiveType.Sphere, p, new Vector3(Random.Range(0.4f, 1.2f), Random.Range(0.3f, 0.7f), Random.Range(0.4f, 1.2f)), stone, "Rock");
            rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-10f, 10f));
            rock.transform.SetParent(transform);
        }

        // Boundary: square around the base of the block, square at the ceiling, 4 corner posts
        float ceiling = agent != null ? agent.ceiling : 7f;
        float baseY = -standHeight + 0.03f;
        DrawSquare(baseY, "BaseEdge");
        DrawSquare(ceiling, "CeilingEdge");
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = Corner(i);
            Line(new[] { c + Vector3.up * baseY, c + Vector3.up * ceiling }, "CornerPost");
        }

        Random.state = old;

        if (agent != null) agent.TerrainRegenerated += RebuildSkirt;
        RebuildSkirt();
    }

    void OnDestroy()
    {
        if (agent != null) agent.TerrainRegenerated -= RebuildSkirt;
    }

    // ---------- Arena skirt (the earth walls of the block) ----------

    void BuildSkirtObject()
    {
        var go = new GameObject("ArenaSkirt");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        var m = new Material(litShader);
        m.color = Color.white;
        m.mainTexture = StrataTexture();
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Glossiness", 0f);
        mr.sharedMaterial = m;

        skirtMesh = new Mesh { name = "ArenaSkirt" };
        skirtMesh.MarkDynamic();
        mf.sharedMesh = skirtMesh;

        // Draw the terrain at full detail so its edge matches the skirt exactly
        if (terrain != null) terrain.heightmapPixelError = 1f;
    }

    // Walks the 4 edges of the terrain and builds a wall from the hill line down to the grass
    void RebuildSkirt()
    {
        if (terrain == null || skirtMesh == null) return;

        TerrainData td = terrain.terrainData;
        Vector3 o = terrain.transform.position;
        int n = td.heightmapResolution - 1;      // one quad per heightmap sample -> exact match
        float bottom = o.y - standHeight;
        float uvTop = o.y + td.size.y * 0.3f;     // texture spans base -> typical hill top

        var verts = new List<Vector3>(n * 16);
        var uvs = new List<Vector2>(n * 16);
        var tris = new List<int>(n * 24);
        float run = 0f;

        for (int side = 0; side < 4; side++)
        {
            Vector3 a = o + Corner(side);
            Vector3 b = o + Corner((side + 1) % 4);

            for (int s = 0; s < n; s++)
            {
                Vector3 p0 = Vector3.Lerp(a, b, (float)s / n);
                Vector3 p1 = Vector3.Lerp(a, b, (float)(s + 1) / n);
                float h0 = o.y + terrain.SampleHeight(p0);
                float h1 = o.y + terrain.SampleHeight(p1);
                float len = Vector3.Distance(p0, p1);

                int i = verts.Count;
                verts.Add(new Vector3(p0.x, h0, p0.z));      // top 0
                verts.Add(new Vector3(p1.x, h1, p1.z));      // top 1
                verts.Add(new Vector3(p1.x, bottom, p1.z));  // bottom 1
                verts.Add(new Vector3(p0.x, bottom, p0.z));  // bottom 0

                uvs.Add(new Vector2(run, Mathf.InverseLerp(bottom, uvTop, h0)));
                uvs.Add(new Vector2(run + len, Mathf.InverseLerp(bottom, uvTop, h1)));
                uvs.Add(new Vector2(run + len, 0f));
                uvs.Add(new Vector2(run, 0f));
                run += len;

                // Clockwise as seen from outside -> faces point outward
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
        }

        skirtMesh.Clear();
        skirtMesh.SetVertices(verts);
        skirtMesh.SetUVs(0, uvs);
        skirtMesh.SetTriangles(tris, 0);
        skirtMesh.RecalculateNormals();
        skirtMesh.RecalculateBounds();
    }

    // Layered earth: dark rock at the base, brown soil bands above
    Texture2D StrataTexture()
    {
        const int h = 128;
        var tex = new Texture2D(4, h) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color rockC = new Color(0.30f, 0.28f, 0.27f);
        Color soilC = new Color(0.50f, 0.36f, 0.22f);
        Color topC  = new Color(0.42f, 0.30f, 0.18f);

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = Color.Lerp(rockC, soilC, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.35f, t)));
            c = Color.Lerp(c, topC, Mathf.InverseLerp(0.7f, 1f, t));
            float band = 0.9f + 0.1f * Mathf.Sin(t * 45f) + (Mathf.PerlinNoise(t * 20f, 3.1f) - 0.5f) * 0.12f;
            c *= band;
            c.a = 1f;
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // ---------- Helpers ----------

    Vector3 RingPoint()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(innerRadius, outerRadius);
        return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    static Vector3 Corner(int i)
    {
        switch (i) { case 0: return new Vector3(0, 0, 0); case 1: return new Vector3(10, 0, 0);
                     case 2: return new Vector3(10, 0, 10); default: return new Vector3(0, 0, 10); }
    }

    void DrawSquare(float y, string name)
    {
        var pts = new Vector3[5];
        for (int i = 0; i < 5; i++) pts[i] = Corner(i % 4) + Vector3.up * y;
        Line(pts, name);
    }

    void Line(Vector3[] pts, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.widthMultiplier = 0.04f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = boundaryColor;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    Material Mat(Color c)
    {
        var m = new Material(litShader);
        m.color = c;
        return m;
    }

    GameObject Make(PrimitiveType type, Vector3 pos, Vector3 scale, Color c, string name) => Make(type, pos, scale, Mat(c), name);

    GameObject Make(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, string name)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        DestroyImmediate(go.GetComponent<Collider>()); // no physics, no ray hits
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }
}
