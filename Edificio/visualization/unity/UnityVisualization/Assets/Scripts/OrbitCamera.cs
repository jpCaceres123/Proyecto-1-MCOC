using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Vector3 target = new Vector3(22.5f, 6.0f, 8.0f);
    public float distance = 38.0f;
    public float sensitivity = 4.0f;
    private float yaw = 35.0f;
    private float pitch = 22.0f;
    private bool touchOnPanel;

    private void Start()
    {
        if (FindAnyObjectByType<MobileViewerNavigation>() == null) gameObject.AddComponent<MobileViewerNavigation>();
        if(MobileViewerUI.Enabled) ResetView();else Apply();
    }

    public void ResetView()
    {
        target=new Vector3(22.5f,6f,8f);distance=MobileViewerUI.Enabled && Screen.height>Screen.width?75f:38f;
        yaw=35f;pitch=22f;Apply();
    }

    private void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch first = Input.GetTouch(0);
            if (first.phase == TouchPhase.Began)
                touchOnPanel = MobileViewerUI.Blocks(first.position);
            if (touchOnPanel) return;
            if (Input.touchCount == 1 && first.phase == TouchPhase.Moved)
            {
                yaw += first.deltaPosition.x * 180f / Screen.width;
                pitch = Mathf.Clamp(pitch - first.deltaPosition.y * 180f / Screen.height, -80f, 80f);
            }
            if (Input.touchCount == 2)
            {
                Touch second = Input.GetTouch(1);
                if (MobileViewerUI.Blocks(second.position)) return;
                float before = Vector2.Distance(first.position-first.deltaPosition, second.position-second.deltaPosition);
                float current = Vector2.Distance(first.position, second.position);
                if (current > 1 && before > 1) distance = Mathf.Clamp(distance * before/current, 5f, 150f);
                Vector2 pan = (first.deltaPosition+second.deltaPosition)*.5f;
                target -= (transform.right*pan.x+transform.up*pan.y)*distance/Screen.height;
            }
            Apply();
            return;
        }
        bool pointerOnPanel = MobileViewerUI.Blocks(Input.mousePosition);
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
}
