using UnityEngine;

public class PlatformMovement : MonoBehaviour
{
    Transform me;

    public float movementRange = 2;
    float initialX = 0f;
    public float step = 0.02f;

    void Start()
    {
        me = GetComponent<Transform>();
        initialX = me.position.x;
    }

    private void FixedUpdate()
    {
        var position = me.position;

        // 현재 X 위치에 step을 더해 이동
        position.x += step;
        me.position = position;

        // 시작 위치를 기준으로 이동 범위를 벗어나면 방향 전환
        if (me.position.x >= initialX + movementRange ||
            me.position.x <= initialX - movementRange)
        {
            step = -step;
        }
    }
}
