using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Vector3 target = new Vector3(22.5f, 6.0f, 8.0f);
    public float distance = 38.0f;
    public float sensitivity = 4.0f;
    private float yaw = 35.0f;
    private float pitch = 22.0f;

    private void Start()
    {
        Apply();
    }

    private void Update()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -80.0f, 80.0f);
            Apply();
        }
        if (Input.GetMouseButton(2))
        {
            target -= transform.right * Input.GetAxis("Mouse X") * distance * 0.01f;
            target -= transform.up * Input.GetAxis("Mouse Y") * distance * 0.01f;
            Apply();
        }
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.001f)
        {
            distance = Mathf.Clamp(distance - wheel * 10.0f, 5.0f, 150.0f);
            Apply();
        }
    }

    public void Apply()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
        transform.position = target + rotation * new Vector3(0.0f, 0.0f, -distance);
        transform.LookAt(target);
    }
}
