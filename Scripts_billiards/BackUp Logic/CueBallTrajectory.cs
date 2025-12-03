using UnityEngine;
using System.Collections.Generic;

public class CueBallTrajectory : MonoBehaviour
{
    public LineRenderer cueLine;
    public LineRenderer objectLine;
    public float totalSimTime = 2f;
    public float stepLength = 0.1f;
    public int maxBounces = 5;
    public float objectBallPushMultiplier = 0.9f;
    public float ballRadius = 0.057f;
    public LayerMask collisionMask;

    public void DrawTrajectory(Vector3 origin, Vector3 velocity)
    {
        cueLine.enabled = true;
        objectLine.enabled = false;

        List<Vector3> cuePoints = new List<Vector3>();
        Vector3 currentPos = origin;
        Vector3 currentVel = velocity;
        float timeRemaining = totalSimTime;
        int bounces = 0;

        cuePoints.Add(currentPos);

        while (timeRemaining > 0 && bounces <= maxBounces)
        {
            Vector3 direction = currentVel.normalized;
            float speed = currentVel.magnitude;

            if (Physics.Raycast(currentPos, direction, out RaycastHit hit, stepLength * speed * Time.fixedDeltaTime, collisionMask))
            {
                currentPos = hit.point;
                cuePoints.Add(currentPos);

                // If hit a ball, stop cue path and draw object trajectory
                if (hit.collider.CompareTag("Ball"))
                {
                    cueLine.positionCount = cuePoints.Count;
                    cueLine.SetPositions(cuePoints.ToArray());

                    Vector3 hitBallCenter = hit.collider.transform.position;
                    Vector3 pushDirection = (hitBallCenter - origin).normalized;
                    Vector3 objectVelocity = pushDirection * currentVel.magnitude * objectBallPushMultiplier;

                    DrawObjectTrajectory(hitBallCenter, objectVelocity);
                    return;
                }

                // Bounce off wall
                currentVel = Vector3.Reflect(currentVel, hit.normal);
                bounces++;
            }
            else
            {
                currentPos += currentVel * stepLength;
                cuePoints.Add(currentPos);
                currentVel *= 0.985f;
                timeRemaining -= stepLength;
            }
        }

        cueLine.positionCount = cuePoints.Count;
        cueLine.SetPositions(cuePoints.ToArray());
    }

    private void DrawObjectTrajectory(Vector3 origin, Vector3 velocity)
    {
        objectLine.enabled = true;

        List<Vector3> points = new List<Vector3>();
        Vector3 currentPos = origin;
        Vector3 currentVel = velocity;
        float timeRemaining = totalSimTime;
        int bounces = 0;

        points.Add(currentPos);

        while (timeRemaining > 0 && bounces <= maxBounces)
        {
            Vector3 direction = currentVel.normalized;
            float speed = currentVel.magnitude;

            if (Physics.Raycast(currentPos, direction, out RaycastHit hit, stepLength * speed * Time.fixedDeltaTime, collisionMask))
            {
                currentPos = hit.point;
                points.Add(currentPos);

                currentVel = Vector3.Reflect(currentVel, hit.normal);
                bounces++;
            }
            else
            {
                currentPos += currentVel * stepLength;
                points.Add(currentPos);
                currentVel *= 0.985f;
                timeRemaining -= stepLength;
            }
        }

        objectLine.positionCount = points.Count;
        objectLine.SetPositions(points.ToArray());
    }

    public void HideTrajectory()
    {
        cueLine.enabled = false;
        objectLine.enabled = false;
    }
}
