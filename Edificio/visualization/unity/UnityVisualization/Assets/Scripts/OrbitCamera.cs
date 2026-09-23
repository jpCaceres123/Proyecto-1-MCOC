using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Vector3 target = new Vector3(22.5f, 6.0f, 8.0f);
    public float distance = 38.0f;
    public float sensitivity = 4.0f;
    private float yaw = 35.0f;
    private float pitch = 22.0f;
    private bool firstPerson;
    private Vector3 savedTarget,eye;
    private float savedDistance,savedYaw,savedPitch;
    public bool FirstPerson => firstPerson;
    public Vector3 PlanForward => new Vector3(transform.forward.x,0,transform.forward.z).normalized;
    public Vector3 PlanRight => new Vector3(transform.right.x,0,transform.right.z).normalized;

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
        if(firstPerson) {
            if(!pointerOnPanel && Input.GetMouseButton(1)) {
                yaw+=Input.GetAxis("Mouse X")*sensitivity;
                pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*sensitivity,-75,75);
                ApplyFirstPerson();
            }
            return;
        }
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
        if(firstPerson){ApplyFirstPerson();return;}
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
        if(firstPerson)return;
        target=center;distance=Mathf.Max(6,extent*1.45f);pitch=58; yaw=25;Apply();
    }
    public void EnterFirstPerson(Vector3 eyePosition)
    {
        if(firstPerson)return;
        savedTarget=target;savedDistance=distance;savedYaw=yaw;savedPitch=pitch;
        firstPerson=true;eye=eyePosition;pitch=18;ApplyFirstPerson();
    }
    public void MoveFirstPerson(Vector3 eyePosition)
    {
        if(!firstPerson)return;eye=eyePosition;ApplyFirstPerson();
    }
    public void ExitFirstPerson()
    {
        if(!firstPerson)return;
        firstPerson=false;target=savedTarget;distance=savedDistance;yaw=savedYaw;pitch=savedPitch;Apply();
    }
    private void ApplyFirstPerson()
    {
        transform.position=eye;
        transform.rotation=Quaternion.Euler(pitch,yaw,0);
    }
}
