using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextStrengthSlider : MonoBehaviour
{

    public TMPro.TextMeshProUGUI m_text;
    public UnityEngine.UI.Slider m_slider;


    // Start is called before the first frame update
    void Start()
    {
        UpdateValue();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateValue()
    {
        m_text.text = "Text strength: " + m_slider.value.ToString("0.0#");
        GameLogic.Get().SetTextStrength(m_slider.value);
    }
}
