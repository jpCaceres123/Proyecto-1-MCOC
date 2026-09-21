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
        float screenY = Screen.height - Input.mousePosition.y;
        Semana3Visualizer viewer = FindAnyObjectByType<Semana3Visualizer>();
        float modelBottom = viewer != null && viewer.ResultsOpen ? Screen.height - 245.0f : Screen.height - 74.0f;
        bool pointerOnPanel = MovingLoadViewer.Blocks(Input.mousePosition) || Input.mousePosition.x < 264.0f || Input.mousePosition.x > Screen.width - 350.0f ||
            screenY < 74.0f || screenY > modelBottom;
        if (!pointerOnPanel && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -80.0f, 80.0f);
            Apply();
        }
        if (!pointerOnPanel && Input.GetMouseButton(2))
        {
            target -= transform.right * Input.GetAxis("Mouse X") * distance * 0.01f;
            target -= transform.up * Input.GetAxis("Mouse Y") * distance * 0.01f;
            Apply();
        }
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (!pointerOnPanel && Mathf.Abs(wheel) > 0.001f)
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

    public void ResetView()
    {
        target = new Vector3(22.5f, 6.0f, 8.0f);
        distance = 38.0f;
        yaw = 35.0f;
        pitch = 22.0f;
        Apply();
    }

    public void FocusPanel(Vector3 center,float extent)
    {
        target=center;distance=Mathf.Max(9,extent*1.7f);pitch=58; yaw=25;Apply();
    }
}
