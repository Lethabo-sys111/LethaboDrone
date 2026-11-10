// Spinner.cs
// Spins an object around an axis (drone propellers, target beacon). Visual only.

using UnityEngine;

public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 1440f;

    void Update()
    {
        transform.Rotate(axis * degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
