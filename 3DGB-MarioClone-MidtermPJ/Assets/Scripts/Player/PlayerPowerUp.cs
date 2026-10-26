using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPowerUp : MonoBehaviour
{
    [SerializeField, Min(1f)]
    private float poweredScaleMultiplier = 1.2f;

    private Vector3 normalScale;

    public bool IsPoweredUp { get; private set; }
    public event Action<bool> PowerUpChanged;

    private void Awake()
    {
        normalScale = transform.localScale;
    }

    public void Activate()
    {
        if (IsPoweredUp)
        {
            return;
        }

        IsPoweredUp = true;
        ApplyScale(poweredScaleMultiplier);
        PowerUpChanged?.Invoke(true);
    }

    public bool TryConsume()
    {
        if (!IsPoweredUp)
        {
            return false;
        }

        IsPoweredUp = false;
        ApplyScale(1f);
        PowerUpChanged?.Invoke(false);
        return true;
    }

    public void ResetPowerUp()
    {
        bool wasPoweredUp = IsPoweredUp;
        IsPoweredUp = false;
        ApplyScale(1f);

        if (wasPoweredUp)
        {
            PowerUpChanged?.Invoke(false);
        }
    }

    private void ApplyScale(float multiplier)
    {
        transform.localScale = new Vector3(
            normalScale.x * multiplier,
            normalScale.y * multiplier,
            normalScale.z
        );
    }
}
