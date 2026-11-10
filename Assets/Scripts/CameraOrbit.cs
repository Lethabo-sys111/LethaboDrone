// CameraOrbit.cs
// Slow cinematic orbit around the arena for demo videos. Untick "Orbit" for a fixed view.

using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public bool orbit = true;
    public Vector3 center = new Vector3(5f, 1f, 5f);
    public float radius = 12f;
    public float height = 8f;
    public float degreesPerSecond = 8f;

    float angle = -90f;

    void LateUpdate()
    {
        if (!orbit) return;
        angle += degreesPerSecond * Time.deltaTime;
        float r = angle * Mathf.Deg2Rad;
        transform.position = center + new Vector3(Mathf.Cos(r) * radius, height, Mathf.Sin(r) * radius);
        transform.LookAt(center);
    }
}
