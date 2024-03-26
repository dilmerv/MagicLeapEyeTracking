using System.Collections.Generic;
using LearnXR.Core;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.MagicLeap;
using UnityEngine.XR.OpenXR.Features.Interactions;
using InputDevice = UnityEngine.XR.InputDevice;
using Logger = LearnXR.Core.Logger;

public class GazeInputManager : Singleton<GazeInputManager>
{
    private InputDevice eyeTrackingDevice;
    public bool EyeTrackingPermissionGranted { get; private set; }
    public Vector3 GazePosition { get; private set; }
    public Quaternion GazeRotation { get; private set; }
    
    void Start()
    {
        MagicLeap.Android.Permissions.RequestPermission(MLPermission.EyeTracking, OnPermissionGranted, 
            OnPermissionDenied, OnPermissionDenied);
    }

    private void Update()
    {
        if (!EyeTrackingPermissionGranted) return;
       
        if (!eyeTrackingDevice.isValid)
        {
            List<InputDevice> inputDeviceList = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, inputDeviceList);
            if (inputDeviceList.Count > 0)
            {
                eyeTrackingDevice = inputDeviceList[0];
            }

            if (!eyeTrackingDevice.isValid)
            {
                Logger.Instance.LogWarning($"Unable to get eye tracking information");
                return;
            }
        }
        
        bool hasData = eyeTrackingDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked);
        hasData &= eyeTrackingDevice.TryGetFeatureValue(EyeTrackingUsages.gazePosition, out Vector3 position);
        hasData &= eyeTrackingDevice.TryGetFeatureValue(EyeTrackingUsages.gazeRotation, out Quaternion rotation);

        if (isTracked && hasData)
        {
            GazePosition = position;
            GazeRotation = rotation;
        }
    }

    private void OnPermissionDenied(string permission)
    {
        Logger.Instance.LogError($"Eye tracking permission denied.");
    }

    private void OnPermissionGranted(string permission)
    {
        EyeTrackingPermissionGranted = true;
    }
}