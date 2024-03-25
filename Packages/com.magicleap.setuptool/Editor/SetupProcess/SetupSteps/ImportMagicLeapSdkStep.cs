#region

using System;
using System.IO;
using MagicLeap.SetupTool.Editor.Interfaces;
using MagicLeap.SetupTool.Editor.Utilities;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

#endregion

namespace MagicLeap.SetupTool.Editor.Setup
{
    /// <summary>
    /// Imports the Magic Leap SDK
    /// </summary>
    public class ImportMagicLeapSdkStep : ISetupStep
    {
        //Localization
        private const string IMPORT_MAGIC_LEAP_SDK = "Import the Magic Leap SDK";
        private const string IMPORT_MAGIC_LEAP_SDK_BUTTON = "Import package";
        private const string CHANGE_MAGIC_LEAP_SDK_BUTTON = "Change";
        private const string UPDATE_MAGIC_LEAP_SDK_BUTTON = "Update";
        private const string CONDITION_MET_LABEL = "Done";
        private const string UPDATE_PACKAGE_TOOLTIP = "Current Version: [v{0}]. Update to [{1}]";
        private const string CURRENT_PACKAGE_VERSION_TOOLTIP = "Current Version: v{0}";
        private const string IMPORTING_PACKAGE_PROGRESS_HEADER = "Importing Package";
        private const string DELETING_PACKAGE_PROGRESS_HEADER = "Deleting Package";
        private const string IMPORTING_PACKAGE_PROGRESS_BODY = "Importing: [{0}]";
        private const string CURRENT_VERSION_HINT_FORMAT = "Current Version [{0}]";
        private const string PACKAGE_NOT_INSTALLED_TOOLTIP = "Not installed";
        private const string DELETING_EMBEDDED_PACKAGE_DIALOG_HEADER = "Update Magic Leap SDK package";
        private const string DELETING_EMBEDDED_PACKAGE_DIALOG_BODY = "This will delete your embedded package. This action cannot be undone";
        private const string DELETING_EMBEDDED_PACKAGE_DIALOG_OK = "Continue";
        private const string DELETING_EMBEDDED_PACKAGE_DIALOG_CANCEL = "Cancel";
        private const string SELECT_PACKAGE_DIALOG_HEADER = "Please select Unity Package";
        private const string SELECT_PACKAGE_DIALOG_BODY = "Please select the unity package that you would like to import into your project.";
        private const string SELECT_PACKAGE_DIALOG_OK = "Continue";
        private const string SELECT_PACKAGE_DIALOG_CANCEL = "Cancel";
        private const string SDK_PACKAGE_FILE_BROWSER_TITLE = "Select the Unity MagicLeap SDK package"; //Title text of SDK path browser
        private const string MAGIC_LEAP_PACKAGE_ID = "com.magicleap.unitysdk"; // Used to check if the build platform is installed
        private const string REGISTRY_PACKAGE_OPTION_TITLE = "Add Magic Leap Registry";
        private const string REGISTRY_PACKAGE_OPTION_BODY = "Would you like to install remote version of the Magic Leap SDK via Magic Leap's Registry?";
        private const string REGISTRY_PACKAGE_OPTION_OK = "Use Magic Leap Registry";
        private const string REGISTRY_PACKAGE_OPTION_CANCEL = "Use Local Copy";
        private const string SELECT_SDK_DIALOG_USE_OPENXR_OPTION = "Use OpenXR";
        private const string SELECT_SDK_DIALOG_USE_MLSDK_OPTION = "Use Magic Leap Sdk (deprecated)";
        private const string SELECT_SDK_DIALOG_TITLE = "Use OpenXR SDK";
        private const string SELECT_SDK_DIALOG_BODY = "Would you like to use the OpenXR or Magic Leap Sdk (deprecated)?";
        private const string FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_OK = "Try again";
        private const string FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_CANCEL = "Cancel";
        private const string FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_TITLE = "Failed to import package";
        private const string FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_BODY = "Would you like to try again?";
        private const string FAILED_TO_IMPORT_PACKAGE_ERROR = "Failed to import package: {0}";
        
        public static bool HasMagicLeapSdkInPackageManager;
        private static int _busyCounter;
        private static bool _checkingForPackage;
        public static bool Running;
        /// <inheritdoc />
        public Action OnExecuteFinished { get; set; }
        public bool Block => true;
        private static int BusyCounter
        {
            get => _busyCounter;
            set => _busyCounter = Mathf.Clamp(value, 0, 100);
        }
 
        /// <inheritdoc />
        public bool Busy => BusyCounter > 0;
        
        /// <inheritdoc />
        public bool Required => true;
        
        private bool _subscribedToEditorChangeEvent;
        /// <inheritdoc />
        public bool IsComplete => HasMagicLeapSdkInPackageManager;
        private static string _sdkPackageVersion;
        private static bool _packageNotInstalled;
        private static bool _embedded;
        private static bool _isCurrent;
        private static bool _installedFromRegistry;
        private static string _currentVersion;
        private static bool _checkingPackage;
        private static bool _dontTryImportAgain;
        public bool CanExecute => EnableGUI();
        
        private bool _loading
        {
            get
            {
                return AssetDatabase.IsAssetImportWorkerProcess() ||
                       EditorApplication.isUpdating ||
                       EditorApplication.isCompiling || Busy;
            }
        }
        /// <inheritdoc />
        public void Refresh()
        {
            CheckUnityMagicLeapPackage();
            
         

            CheckForMagicLeapSdkPackage(CheckVersion);
           
          
        }
#if USE_MLSDK && UNITY_MAGICLEAP
        [MenuItem("Magic Leap/Upgrade To OpenXR")]
        public static void UpgradeToOpenXR()
        {
            BusyCounter++;
   
            PackageUtility.RemovePackage("com.unity.xr.magicleap", RemovePackageSuccess);
            static void RemovePackageSuccess(bool success)
            {
   
                if (success)
                {
                    Debug.Log("Removed com.unity.xr.magicleap package");
                    DefineSymbolUtility.RemoveDefineSymbol("USE_MLSDK");
                    BusyCounter++;
                    CallWhenNotBusy(() =>
                    {
                        DefineSymbolUtility.AddDefineSymbol("USE_ML_OPENXR");
                        AssetDatabase.SaveAssets();
                        AssetDatabase.RefreshSettings();
                        AssetDatabase.Refresh(ImportAssetOptions.Default);
                        BusyCounter--;
                     
                    });
                }
                else
                {
                    Debug.LogError("Failed to remove package.");
                }
                BusyCounter--;
             
            }
         
        }
#endif
#if USE_ML_OPENXR && !UNITY_MAGICLEAP        
        [MenuItem("Magic Leap/Downgrade To MLSDK")]
        public static void DowngradeToMLSDK()
        {
            BusyCounter++;
   
            PackageUtility.AddPackage("com.unity.xr.magicleap", RemovePackageSuccess);
            static void RemovePackageSuccess(bool success)
            {
   
                if (success)
                {
                    Debug.Log("Added com.unity.xr.magicleap package");
                    BusyCounter++;
                    DefineSymbolUtility.RemoveDefineSymbol("USE_ML_OPENXR");
                    CallWhenNotBusy(() =>
                    {
                        DefineSymbolUtility.AddDefineSymbol("USE_MLSDK");
                        AssetDatabase.SaveAssets();
                        AssetDatabase.RefreshSettings();
                        AssetDatabase.Refresh(ImportAssetOptions.Default);
                        BusyCounter--;
                    });
                
                }
                else
                {
                    Debug.LogError("Failed to remove package.");
                }
                BusyCounter--;
         
            }
         
        }
#endif
    

        void CheckUnityMagicLeapPackage()
        {
            if (_loading || _dontTryImportAgain) return;
              if (!DefineSymbolUtility.ContainsDefineSymbolInAllBuildTargets("USE_MLSDK") && !DefineSymbolUtility.ContainsDefineSymbolInAllBuildTargets("USE_ML_OPENXR"))
              {
                  var useOpenXR = EditorUtility.DisplayDialog(SELECT_SDK_DIALOG_TITLE, SELECT_SDK_DIALOG_BODY, SELECT_SDK_DIALOG_USE_OPENXR_OPTION, SELECT_SDK_DIALOG_USE_MLSDK_OPTION);
                  if (useOpenXR)
                  {
                      DefineSymbolUtility.AddDefineSymbol("USE_ML_OPENXR");
                  }
                  else
                  {
                      DefineSymbolUtility.AddDefineSymbol("USE_MLSDK");

                  }
              }
                                                   
#if USE_MLSDK && !UNITY_MAGICLEAP
            ImportUnityMagicLeapPackage();
#endif
        }

        private void ImportUnityMagicLeapPackage()
        {

            BusyCounter++;
            MagicLeapRegistryPackageImporter.InstallUnityMLPackage(OnAddedPackage);
            void OnAddedPackage(bool success)
            {
                if (!success)
                {
                    Debug.LogErrorFormat(FAILED_TO_IMPORT_PACKAGE_ERROR,"com.unity.xr.magicleap");
                    var tryAgain = EditorUtility.DisplayDialog(FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_TITLE, FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_BODY, FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_OK, FAILED_TO_IMPORT_UNITY_MAGICLEAP_DIALOG_CANCEL);
                    if (tryAgain)
                    {
                        ImportUnityMagicLeapPackage();
                    }
                    else
                    {
                        _dontTryImportAgain = true;
                    }
                }
                else
                {
                    _dontTryImportAgain = true;
                }
                BusyCounter--;
            }
        }

        

        public void CheckVersion()
        {
            PackageUtility.GetPackageInfo(MAGIC_LEAP_PACKAGE_ID, ObtainedPackageInfo);



           void ObtainedPackageInfo(UnityEditor.PackageManager.PackageInfo info)
          {
          
              
              if (info == null)
              {
               
                  _packageNotInstalled = true;
                  _sdkPackageVersion = PACKAGE_NOT_INSTALLED_TOOLTIP;
                  return;
              }

              _currentVersion = info.version;
              _packageNotInstalled = false;

              _embedded = info.source == PackageSource.Embedded;
              _installedFromRegistry= info.source == PackageSource.Registry;
              if (_installedFromRegistry)
              {
                  var versionComparer = new MagicLeapPackageUtility.VersionComparer();
                  var latestVersion = info.versions.latest;
                  var isCurrentVersion = versionComparer.Compare(info.versions.latest,info.version) <=0;
                  _isCurrent = isCurrentVersion;
                  if ((!isCurrentVersion))
                  {
                      _sdkPackageVersion = string.Format(UPDATE_PACKAGE_TOOLTIP, info.version, latestVersion);
                      return;
                  }
              }
              else
              {
                  var latestSDKPath = MagicLeapPackageUtility.GetLatestUnityPackagePath();
                  var directoryInfo = new DirectoryInfo(latestSDKPath).Parent;
 
                  if (directoryInfo != null)
                  {
                      var versionComparer = new MagicLeapPackageUtility.VersionComparer();
                      var isCurrentVersion = versionComparer.Compare(directoryInfo.Name,info.version) <=0;
                      _isCurrent = isCurrentVersion;
                      if ((!isCurrentVersion))
                      {
                          _sdkPackageVersion = string.Format(UPDATE_PACKAGE_TOOLTIP, info.version, directoryInfo.Name);
                          return;
                      }
                  }
              }
              
              _sdkPackageVersion = string.Format(CURRENT_PACKAGE_VERSION_TOOLTIP, info.version);

          }
           
        }
        private static void CallWhenNotBusy(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.update += UpdateEditor;

                void UpdateEditor()
                {
                    if(AssetDatabase.IsAssetImportWorkerProcess() ||
                       EditorApplication.isUpdating ||
                       EditorApplication.isCompiling)
                    {
                        return;
                    }
                    action?.Invoke();
                    EditorApplication.update -= UpdateEditor;
                }
            };
        }

  

        private bool EnableGUI()
        {
            var correctBuildTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            return correctBuildTarget;
        }
        /// <inheritdoc />
        public bool Draw()
        {
            GUI.enabled = EnableGUI();


            if (_packageNotInstalled)
            {
                if (CustomGuiContent.CustomButtons.DrawConditionButton(new GUIContent(IMPORT_MAGIC_LEAP_SDK),
                                                                       HasMagicLeapSdkInPackageManager, new GUIContent(CONDITION_MET_LABEL, _sdkPackageVersion),
                                                                       new GUIContent(IMPORT_MAGIC_LEAP_SDK_BUTTON, _sdkPackageVersion), Styles.FixButtonStyle, _installedFromRegistry))
                {

                    Execute();
                    return true;
                }
            }
            else
            {
             
                if (_installedFromRegistry)
                {
                    if (CustomGuiContent.CustomButtons.DrawConditionButton(new GUIContent(IMPORT_MAGIC_LEAP_SDK),
                            _isCurrent, new GUIContent(CONDITION_MET_LABEL, _sdkPackageVersion),
                            new GUIContent(UPDATE_MAGIC_LEAP_SDK_BUTTON, _sdkPackageVersion), Styles.FixButtonStyle, true,null, Color.green,Color.green))
                    {
                
                        DeleteAndExecute();
                        return true;
                    }
                }
                else
                {
                    if (CustomGuiContent.CustomButtons.DrawButton(new GUIContent(IMPORT_MAGIC_LEAP_SDK), new GUIContent(CHANGE_MAGIC_LEAP_SDK_BUTTON, string.Format(CURRENT_VERSION_HINT_FORMAT, _currentVersion)), Styles.FixButtonStyle))
                    {

                        DeleteAndExecute();
                        return true;
                    }
                }
            }

            return false;
        }

        private string GetPackageDirectory()
        {
            string directoryToUse = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME");


            var sdkRoot = MagicLeapPackageUtility.GetUnityPackageDirectory();
            if (!string.IsNullOrEmpty(sdkRoot))
            {
                directoryToUse = sdkRoot;
            }

            if (File.Exists(MagicLeapPackageUtility.DefaultUnityPackagePath))
            {
                var directoryInfo = new DirectoryInfo(MagicLeapPackageUtility.DefaultUnityPackagePath).Parent;
                if (directoryInfo != null)
                {
                    directoryToUse = directoryInfo.FullName;
                }
            }
            else
            {
                var latestUnityPackageFolder = MagicLeapPackageUtility.GetLatestUnityPackagePath();

                if (File.Exists(latestUnityPackageFolder))
                {
                    var directoryInfo = new DirectoryInfo(latestUnityPackageFolder).Parent;
                    if (directoryInfo != null)
                    {
                        directoryToUse = directoryInfo.FullName;
                    }
             
                }
            }
            var path = EditorUtility.OpenFilePanel(SDK_PACKAGE_FILE_BROWSER_TITLE, directoryToUse, "tgz");
            return path;
        }

        private string GetPackageInProject()
        {
            var packageZip = Path.GetFullPath(Application.dataPath + "/../Packages/com.magicleap.unitysdk.tgz");
            var packageFolder = Path.GetFullPath(Application.dataPath + "/../Packages/com.magicleap.unitysdk");
            if (File.Exists(packageZip))
            {
                return packageZip;
            }

            if (Directory.Exists(packageFolder))
            {
                return packageFolder;
            }

            return null;
        }
        public void DeleteAndExecute()
        {

           var packagePath = GetPackageDirectory();
           if (string.IsNullOrWhiteSpace(packagePath))
           {
               return;
           }

           if (_installedFromRegistry)
           {
               Running = true;
               BusyCounter++;


               EditorUtility.DisplayProgressBar(DELETING_PACKAGE_PROGRESS_HEADER, DELETING_PACKAGE_PROGRESS_HEADER, .3f);

             
         
                PackageUtility.RemovePackage(MAGIC_LEAP_PACKAGE_ID, OnRemovedPackage);


               void OnRemovedPackage(bool success)
               {
                   EditorUtility.ClearProgressBar();
                   Refresh();
                   OnPackageDelete(success);
               
               }
           }
           else if (_embedded)
           {
               Running = true;
               BusyCounter++;
               var deletePackage = EditorUtility.DisplayDialog(DELETING_EMBEDDED_PACKAGE_DIALOG_HEADER, DELETING_EMBEDDED_PACKAGE_DIALOG_BODY, DELETING_EMBEDDED_PACKAGE_DIALOG_OK, DELETING_EMBEDDED_PACKAGE_DIALOG_CANCEL);
               if (!deletePackage)
               {
                   EditorUtility.ClearProgressBar();
                   Running = false;
                   BusyCounter--;
                   return;
               }

              
               EditorUtility.DisplayProgressBar(DELETING_PACKAGE_PROGRESS_HEADER, DELETING_PACKAGE_PROGRESS_HEADER, .3f);

             
               var pathToPackagesFolder = GetPackageInProject();
               if (!string.IsNullOrWhiteSpace(pathToPackagesFolder))
               {
                   FileUtil.DeleteFileOrDirectory(pathToPackagesFolder);
                   AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                   Client.Resolve();
               }

               EditorApplication.update += OnEditorUpdate;



               void OnEditorUpdate()
               {
                   var busy = AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isUpdating;
                   if (busy)
                   {
                       return;
                   }

                   EditorUtility.ClearProgressBar();
                   EditorApplication.update -= OnEditorUpdate;
                   Refresh();
                   OnPackageDelete(true);
                   Running = false;
                   BusyCounter--;
               }
           }
           else
           {
               var pathToPackageTarball = GetPackageInProject();

               if (!string.IsNullOrWhiteSpace(pathToPackageTarball))
               {
                   FileUtil.DeleteFileOrDirectory(pathToPackageTarball);
               }
             
                
               Running = true;
               BusyCounter++;
               EditorUtility.DisplayProgressBar(DELETING_PACKAGE_PROGRESS_HEADER, DELETING_PACKAGE_PROGRESS_HEADER, .3f);
               PackageUtility.RemovePackage(MAGIC_LEAP_PACKAGE_ID, OnPackageDelete);
           }
        }

        void OnPackageDelete(bool success)
        {
            Running = false;
            BusyCounter--;
            EditorUtility.ClearProgressBar();

            BusyCounter++;
            var useRegistry = EditorUtility.DisplayDialog(REGISTRY_PACKAGE_OPTION_TITLE, REGISTRY_PACKAGE_OPTION_BODY,
                                                          REGISTRY_PACKAGE_OPTION_OK, REGISTRY_PACKAGE_OPTION_CANCEL);

            if (useRegistry)
            {
                AddRegistryAndImport();
            }
            else
            {
                AddCopyPastePackageRefresh();
            }

            BusyCounter--;
  
        }
        
        

        /// <inheritdoc />
        public void Execute()
        {
            if (IsComplete || Busy) return;




            BusyCounter++;
            var useRegistry = EditorUtility.DisplayDialog(REGISTRY_PACKAGE_OPTION_TITLE, REGISTRY_PACKAGE_OPTION_BODY,
                                                          REGISTRY_PACKAGE_OPTION_OK, REGISTRY_PACKAGE_OPTION_CANCEL);
                                                          
            if (useRegistry)
            {
                AddRegistryAndImport();
            }
            else
            {
                var selectPackage = EditorUtility.DisplayDialog(SELECT_PACKAGE_DIALOG_HEADER, SELECT_PACKAGE_DIALOG_BODY, SELECT_PACKAGE_DIALOG_OK, SELECT_PACKAGE_DIALOG_CANCEL);
                if (!selectPackage)
                {
                    BusyCounter = 0;
                    Running = false;
                    return;
                }
                AddCopyPastePackageRefresh();

            }

            BusyCounter--;
        
        }

        private void AddRegistryAndImport()
        {
            EditorUtility.DisplayProgressBar(IMPORTING_PACKAGE_PROGRESS_HEADER, string.Format(IMPORTING_PACKAGE_PROGRESS_BODY, MAGIC_LEAP_PACKAGE_ID), .4f);
            BusyCounter++;
            var startImportTime = EditorApplication.timeSinceStartup;
            Running = true;
// #if !USE_MLSDK
            MagicLeapRegistryPackageImporter.AddRegistryAndImport(OnRegistryAndSdkAdded);


            void OnRegistryAndSdkAdded(bool success)
            {
            
              
                if (!success)
                {
                    EditorUtility.ClearProgressBar();
                   Debug.LogError("Failed to import com.magicleap.unitysdk.");
                   BusyCounter--;
                   OnExecuteFinished?.Invoke();
                }
                else
                {
                    EditorApplication.update += EditorUpdate;
                    void EditorUpdate()
                    {
                        var loading = AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling || EditorApplication.isUpdating;
                        if (loading && (EditorApplication.timeSinceStartup - startImportTime) < 10)
                            return;
                        BusyCounter--;
                        EditorUtility.ClearProgressBar();
                        EditorApplication.update -= EditorUpdate;
                        OnExecuteFinished?.Invoke();
                    }
                }
            }
// #else
//             MagicLeapRegistryPackageImporter.InstallUnityMLPackage(OnAddedPackage);
//            
//             void OnAddedPackage(bool success)
//             {
//           
//                 if (success)
//                 {
//                     
//                     EditorApplication.delayCall+=(() =>
//                     {
//                         MagicLeapRegistryPackageImporter.AddRegistryAndImport(OnRegistryAndSdkAdded);
//                     });
//              
//                 }
//                 else
//                 {
//                     Debug.LogError("Failed to import: com.unity.xr.magicleap");
//                     EditorUtility.ClearProgressBar();
//                     BusyCounter--;
//                     OnExecuteFinished?.Invoke();
//                 }
//             }
//             void OnRegistryAndSdkAdded(bool success)
//             {
//                 EditorUtility.ClearProgressBar();
//                 if (success)
//                 {
//        
//                     EditorApplication.update += EditorUpdate;
//
//
//
//                     void EditorUpdate()
//                     {
//                         var loading = AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling || EditorApplication.isUpdating;
//                         if (loading && (EditorApplication.timeSinceStartup - startImportTime) < 10)
//                             return;
//                         BusyCounter--;
//                         EditorApplication.update -= EditorUpdate;
//                         OnExecuteFinished?.Invoke();
//                     }
//                 }
//                 else
//                 {
//                     BusyCounter--;
//                     OnExecuteFinished?.Invoke();
//                     Debug.LogError("Failed to import: com.magicleap.unitysdk.");
//                 }
//
//
//             }
// #endif
        }

        /// <summary>
        /// Updates the variables based on if the Magic Leap SDK are installed
        /// </summary>
        private  void CheckForMagicLeapSdkPackage(Action onFinished = null)
        {
      
            if (!_checkingForPackage)
            {
                _checkingForPackage = true;
              //  BusyCounter++;
                PackageUtility.HasPackageInstalled(MAGIC_LEAP_PACKAGE_ID, OnCheckForMagicLeapPackageInPackageManager, true, true);



                void OnCheckForMagicLeapPackageInPackageManager(bool success, bool hasPackage)
                {
                    Running = false;
                    _checkingForPackage = false;
                    BusyCounter--;
                    HasMagicLeapSdkInPackageManager = hasPackage;
                    onFinished?.Invoke();
           
                }
            }

        }


        private void AddCopyPastePackageRefresh(string packagePath = null)
        {

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                packagePath = GetPackageDirectory();
            }
       
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                ApplyAllRunner.Stop();
                return;
            }
      
            Running = true;
            BusyCounter++;
            EditorUtility.DisplayProgressBar(IMPORTING_PACKAGE_PROGRESS_HEADER, string.Format(IMPORTING_PACKAGE_PROGRESS_BODY, packagePath), .3f);
            var packageName = Path.GetFileName(packagePath);
            var pathToPackagesFolder = Path.GetFullPath(Application.dataPath+ "/../Packages/"+ packageName);
            if(!File.Exists(pathToPackagesFolder))
            {
                 FileUtil.CopyFileOrDirectory(packagePath, pathToPackagesFolder);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Refresh();
            Running = false;
            BusyCounter--;
            AddPackageManagerAndRefresh(packageName);
        }
        /// <summary>
        /// Adds the Magic Leap SDK and refreshes setup variables
        /// </summary>
        private void AddPackageManagerAndRefresh(string packageName)
        {
   
            EditorUtility.DisplayProgressBar(IMPORTING_PACKAGE_PROGRESS_HEADER, string.Format(IMPORTING_PACKAGE_PROGRESS_BODY, packageName), .4f);
            BusyCounter++;
            var startImportTime = EditorApplication.timeSinceStartup;
            PackageUtility.AddPackage("file:"+packageName, OnAddedPackage);
            Running = true;
            
            void OnAddedPackage(bool success)
            {
                EditorUtility.ClearProgressBar();
                if (success)
                {
                    EditorApplication.update += EditorUpdate;
                    void EditorUpdate()
                    {
                        var loading = AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling || EditorApplication.isUpdating;
                        if (loading && (EditorApplication.timeSinceStartup - startImportTime) < 10)
                            return;
                        BusyCounter--;
                        EditorApplication.update -= EditorUpdate;
                    }
                }
                else
                {
                    BusyCounter--;
                }

                OnExecuteFinished?.Invoke();
           
            }
        }
    }
}