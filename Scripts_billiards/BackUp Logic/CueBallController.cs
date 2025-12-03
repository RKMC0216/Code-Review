using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CueBallController : MonoBehaviour
{
    public float maxPower = 15f;
    public float minDragDistance = 0.1f;
    public LineRenderer aimLine;

    private Rigidbody2D rb;
    private Vector2 dragStartPos;
    private bool isDragging = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
    }

    void Update()
    {
        if (rb.linearVelocity.magnitude > 0.05f) return;

        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 dragVector = dragStartPos - currentPos;

            if (aimLine != null)
            {
                aimLine.enabled = true;
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, transform.position + (Vector3)dragVector.normalized * Mathf.Min(dragVector.magnitude, 1.5f));
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Vector2 dragEndPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 force = dragStartPos - dragEndPos;

            if (force.magnitude >= minDragDistance)
            {
                rb.AddForce(force.normalized * Mathf.Min(force.magnitude, 1f) * maxPower, ForceMode2D.Impulse);
            }

            isDragging = false;
            if (aimLine != null) aimLine.enabled = false;
        }
    }
}
