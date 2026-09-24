using UnityEngine;
using TMPro;

public class DigitalClock : MonoBehaviour
{
    TextMeshProUGUI text;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();

        text.text ="abc";
    }

    // Update is called once per frame
    void Update()
    {
        text.text = System.DateTime.Now.ToString("HH:mm:ss");
    }
}
