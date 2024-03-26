using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Logger = LearnXR.Core.Logger;

public class GazePasscodeFeature : MonoBehaviour
{
    [SerializeField] private string secretPasscode;
    
    [SerializeField] private UnityEvent OnPasscodeValid = new();
    
    [SerializeField] private UnityEvent OnPasscodeInValid = new();

    [SerializeField] private LayerMask layersToIncludeWithRay;
    [SerializeField] private TextMeshPro passcodeText;
    [SerializeField] [Range(1.0f, 10.0f)] private float minGazeTimeOverNumbers = 2.0f;

    // private variables
    private MeshRenderer[] codeRenderers;
    private Material fillMaterial;
    private float gazeOverCodeTracker;
    private readonly int fillProgressProperty = Shader.PropertyToID("_FillProgress");
    private const float MIN_FILL_RANGE = -0.6f;
    private const float MAX_FILL_RANGE = 0.6f;
    
    private void Awake()
    {
        passcodeText.text = string.Empty;
        var allButtons = GameObject.FindGameObjectsWithTag($"CodeButton");
        codeRenderers = allButtons
            .Select(n => n.GetComponent<MeshRenderer>())
            .ToArray();
    }
    
    void Update()
    {
        if (!GazeInputManager.Instance.EyeTrackingPermissionGranted) return;
        
        var gazePosition = GazeInputManager.Instance.GazePosition;
        var gazeRotation = GazeInputManager.Instance.GazeRotation;
        
        Ray ray = new Ray(gazePosition, gazeRotation * Vector3.forward);

        // Add a new (Passcode) layer to Unity and the tutorial script
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layersToIncludeWithRay))
        {
            // clear any prior fillings
            ClearAllFillings(hit.transform.gameObject);
            
            var passcodeNumBox = hit.collider.gameObject;
            var passcodeNumBoxRenderer = passcodeNumBox.GetComponent<Renderer>();
            
            var passcodeNumText = passcodeNumBox.GetComponentInChildren<TextMeshPro>();
           
            gazeOverCodeTracker += Time.deltaTime;
            var progress = gazeOverCodeTracker / minGazeTimeOverNumbers;
            var fillProgressValue = ConvertPercentageToRange(progress);
            passcodeNumBoxRenderer.material.SetFloat(fillProgressProperty, fillProgressValue);
            
            if (gazeOverCodeTracker >= minGazeTimeOverNumbers)
            {
                Logger.Instance.LogInfo($"(Gaze) Code Selected: {hit.collider.gameObject.name}");
                
                // reset executed
                if (passcodeNumText.text == "RESET")
                {
                    passcodeText.text = string.Empty;
                    gazeOverCodeTracker = 0;
                    return;
                }
                
                // clear it out if max
                if (passcodeText.text.Length >= secretPasscode.Length)
                    passcodeText.text = string.Empty;
                
                string gazeNumberSelected = passcodeNumText.text;
                passcodeText.text += gazeNumberSelected;

                // check passcode combination
                if (passcodeText.text.Length >= secretPasscode.Length)
                {
                    if (passcodeText.text == secretPasscode)
                    {
                        Logger.Instance.LogInfo("Passcode Is Valid - Executing OnPasscodeValid Unity Event...");
                        OnPasscodeValid?.Invoke();
                    }
                    else
                    {
                        Logger.Instance.LogWarning("Passcode Is Not Valid - Executing OnPasscodeInValid Unity Event...");
                        OnPasscodeInValid?.Invoke();
                    }
                }

                // Zero it out to prevent quickly re-typing
                gazeOverCodeTracker = 0;
                passcodeNumBoxRenderer.material.SetFloat(fillProgressProperty, 0);
            }
        }
        else
        {
            gazeOverCodeTracker = 0;
        }
    }
    
    private float ConvertPercentageToRange(float percentage)
    {
        float rangeSize = MAX_FILL_RANGE - (MIN_FILL_RANGE);
        float convertedValue = (percentage * rangeSize) - MAX_FILL_RANGE;
        return convertedValue;
    }

    private void ClearAllFillings(GameObject gameObjectToExclude = null)
    {
        float zeroPercent = ConvertPercentageToRange(0);
        foreach (var currentRenderer in codeRenderers)
        {
            if(currentRenderer.gameObject != gameObjectToExclude)
                currentRenderer.material.SetFloat(fillProgressProperty, zeroPercent);
        }
    }
}