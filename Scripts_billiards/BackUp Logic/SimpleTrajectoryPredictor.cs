using UnityEngine;
using System.Collections.Generic;

public class SimpleTrajectoryPredictor : MonoBehaviour
{
    public LineRenderer cueLine;
    public LineRenderer objectLine;
    public float ballRadius = 0.057f;
    public float maxDistance = 10f;
    public LayerMask collisionMask;

    public void ShowTrajectory(Vector3 cueBallPos, Vector3 direction)
    {
        cueLine.enabled = true;
        objectLine.enabled = false;

        direction.Normalize();
        Vector3 rayOrigin = cueBallPos;

        if (Physics.SphereCast(rayOrigin, ballRadius, direction, out RaycastHit hit, maxDistance, collisionMask))
        {
            // Line 1: Cue ball trajectory
            cueLine.positionCount = 2;
            cueLine.SetPosition(0, cueBallPos);
            cueLine.SetPosition(1, hit.point);

            // Hit object ball?
            if (hit.collider.CompareTag("Ball"))
            {
                objectLine.enabled = true;

                Vector3 hitBallCenter = hit.collider.transform.position;

                // Direction from cue ball to center of hit ball
                Vector3 pushDir = (hitBallCenter - cueBallPos).normalized;

                // Start from far side of hit ball
                Vector3 objectBallStart = hitBallCenter + pushDir * ballRadius;

                // Do a basic raycast from that direction to simulate object ball path
                if (Physics.Raycast(objectBallStart, pushDir, out RaycastHit objHit, maxDistance, collisionMask))
                {
                    objectLine.positionCount = 2;
                    objectLine.SetPosition(0, objectBallStart);
                    objectLine.SetPosition(1, objHit.point);
                }
                else
                {
                    objectLine.positionCount = 2;
                    objectLine.SetPosition(0, objectBallStart);
                    objectLine.SetPosition(1, objectBallStart + pushDir * maxDistance);
                }
            }
        }
        else
        {
            // Just draw cue line if no hit
            cueLine.positionCount = 2;
            cueLine.SetPosition(0, cueBallPos);
            cueLine.SetPosition(1, cueBallPos + direction * maxDistance);
        }
    }

    public void Hide()
    {
        cueLine.enabled = false;
        objectLine.enabled = false;
    }
}
