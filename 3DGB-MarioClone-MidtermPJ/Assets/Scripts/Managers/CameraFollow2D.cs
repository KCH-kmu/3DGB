using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(2f, 0.5f, -10f);
    [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;

    [Header("Level Bounds")]
    [SerializeField] private float minX = -5f;
    [SerializeField] private float maxX = 96f;
    [SerializeField] private float minY = 0f;
    [SerializeField] private float maxY = 2.5f;

    private Vector3 velocity;

    public void Configure(
        Transform followTarget,
        float minimumX,
        float maximumX,
        float minimumY,
        float maximumY
    )
    {
        target = followTarget;
        minX = minimumX;
        maxX = maximumX;
        minY = minimumY;
        maxY = maximumY;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        desiredPosition.z = offset.z;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime
        );
    }
}
