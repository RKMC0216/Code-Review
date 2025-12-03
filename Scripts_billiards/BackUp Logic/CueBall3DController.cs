using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CueBall3DController : MonoBehaviour
{
    public float maxPower = 20f;
    public float dragMultiplier = 5f;
    public LineRenderer aimLine;

    private Rigidbody rb;
    private Vector3 dragStartPos;
    private bool isDragging = false;

    public CueBallTrajectory trajectory; // assign in inspector
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
    }

    void Update()
    {
        if (rb.linearVelocity.magnitude > 0.1f) return; // prevent shooting while moving

        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPos = GetMousePositionOnTable();
            isDragging = true;
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector3 currentDragPos = GetMousePositionOnTable();
            Vector3 direction = dragStartPos - currentDragPos;

            // Draw trajectory while dragging
            if (trajectory != null)
            {
                Vector3 shotDirection = dragStartPos - currentDragPos;
                float power = Mathf.Clamp(shotDirection.magnitude * dragMultiplier, 0, maxPower);
                Vector3 velocity = shotDirection.normalized * power;

                trajectory.DrawTrajectory(transform.position, velocity);
            }

            if (aimLine != null)
            {
                aimLine.enabled = true;
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, transform.position + direction.normalized * 2f);
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Vector3 dragEndPos = GetMousePositionOnTable();
            Vector3 shotDirection = dragStartPos - dragEndPos;
            float power = Mathf.Clamp(shotDirection.magnitude * dragMultiplier, 0, maxPower);

            rb.AddForce(shotDirection.normalized * power, ForceMode.Impulse);

            isDragging = false;

            if (trajectory != null)
                trajectory.HideTrajectory();

            if (aimLine != null)
                aimLine.enabled = false;
        }
    }


    Vector3 GetMousePositionOnTable()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane tablePlane = new Plane(Vector3.up, transform.position); // assuming table is flat on Y

        if (tablePlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return transform.position;
    }
}
