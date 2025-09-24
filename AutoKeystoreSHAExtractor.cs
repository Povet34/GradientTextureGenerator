#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

[System.Serializable]
public class KeystoreConfig
{
    public string name;
    public string keystorePath;
    public string aliasName;
    public string storePassword;
    public string keyPassword;
    public string sha1;
    public string sha256;
    public bool isProcessed;

    public KeystoreConfig(string name, string path, string alias, string storePass, string keyPass)
    {
        this.name = name;
        this.keystorePath = path;
        this.aliasName = alias;
        this.storePassword = storePass;
        this.keyPassword = keyPass;
        this.sha1 = "";
        this.sha256 = "";
        this.isProcessed = false;
    }
}

public class AutoKeystoreSHAExtractor : EditorWindow
{
    // 프로젝트 기본 경로
    private string projectPath;
    private string foundKeytoolPath = "";
    private bool keytoolSearched = false;

    // 키스토어 설정들
    private List<KeystoreConfig> keystoreConfigs;

    private Vector2 scrollPosition;
    private bool showResults = false;
    private bool isProcessing = false;

    [MenuItem("Tools/Auto Keystore SHA Extractor")]
    public static void ShowWindow()
    {
        AutoKeystoreSHAExtractor window = GetWindow<AutoKeystoreSHAExtractor>("Auto SHA Extractor");
        window.minSize = new Vector2(500, 600);
    }


    private void OnEnable()
    {
        InitializeKeystoreConfigs();

        if (!keytoolSearched)
        {
            FindKeytoolPath();
            keytoolSearched = true;
        }
    }

    private void InitializeKeystoreConfigs()
    {
        projectPath = Directory.GetParent(Application.dataPath).FullName;

        UnityEngine.Debug.Log($"Project Path set to: {projectPath}");

        keystoreConfigs = new List<KeystoreConfig>
        {
            new KeystoreConfig("Development",
                Path.Combine(projectPath, "Assets", "Keystores", "Dev.keystore"),
                "chipy12", "chipy12", "chipy12"),

            new KeystoreConfig("Test",
                Path.Combine(projectPath, "Assets", "Keystores", "Test.keystore"),
                "chipy12", "chipy12", "chipy12"),

            new KeystoreConfig("Production",
                Path.Combine(projectPath, "Assets", "Keystores", "Prod.keystore"),
                "chipy12", "chipy12", "chipy12")
        };
    }

    private void OnGUI()
    {
        GUILayout.Label("Auto Keystore SHA Extractor", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 프로젝트 경로 설정
        GUILayout.Label("Project Settings:", EditorStyles.boldLabel);
        projectPath = EditorGUILayout.TextField("Project Path:", projectPath);

        if (GUILayout.Button("Update Keystore Paths"))
        {
            InitializeKeystoreConfigs();
        }

        EditorGUILayout.Space(10);

        // Keytool 상태
        GUILayout.Label("Keytool Status:", EditorStyles.boldLabel);
        if (string.IsNullOrEmpty(foundKeytoolPath))
        {
            EditorGUILayout.HelpBox("❌ Keytool not found", MessageType.Error);
            if (GUILayout.Button("🔍 Search for Keytool Again"))
            {
                FindKeytoolPath();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Keytool found!", MessageType.Info);
            EditorGUILayout.LabelField("Path:", foundKeytoolPath, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(10);

        // 키스토어 상태 표시
        GUILayout.Label("Keystore Status:", EditorStyles.boldLabel);

        foreach (var config in keystoreConfigs)
        {
            EditorGUILayout.BeginHorizontal();

            // 환경 이름
            GUILayout.Label($"{config.name}:", GUILayout.Width(80));

            // 파일 존재 여부
            bool exists = File.Exists(config.keystorePath);
            string statusIcon = exists ? "✅" : "❌";
            string statusText = exists ? "Found" : "Missing";

            GUILayout.Label($"{statusIcon} {statusText}", GUILayout.Width(80));

            // 처리 상태
            if (config.isProcessed && !string.IsNullOrEmpty(config.sha1))
            {
                GUILayout.Label("🔑 SHA Extracted", GUILayout.Width(100));
            }
            else if (exists)
            {
                GUILayout.Label("⏳ Ready", GUILayout.Width(100));
            }
            else
            {
                GUILayout.Label("⚠️ Not Ready", GUILayout.Width(100));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(15);

        // 메인 버튼들
        GUI.enabled = !string.IsNullOrEmpty(foundKeytoolPath) && !isProcessing;

        if (GUILayout.Button("🚀 Extract All SHA Keys", GUILayout.Height(35)))
        {
            ExtractAllSHA();
        }

        EditorGUILayout.Space(10);

        // 개별 추출 버튼들
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Dev Only"))
        {
            ExtractSingleSHA(keystoreConfigs[0]);
        }
        if (GUILayout.Button("Test Only"))
        {
            ExtractSingleSHA(keystoreConfigs[1]);
        }
        if (GUILayout.Button("Prod Only"))
        {
            ExtractSingleSHA(keystoreConfigs[2]);
        }
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        // 진행 상태 표시
        if (isProcessing)
        {
            EditorGUILayout.HelpBox("🔄 Processing... Please wait...", MessageType.Info);
        }

        EditorGUILayout.Space(15);

        // 결과 표시
        if (showResults)
        {
            GUILayout.Label("📋 Extraction Results:", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var config in keystoreConfigs)
            {
                if (config.isProcessed && (!string.IsNullOrEmpty(config.sha1) || !string.IsNullOrEmpty(config.sha256)))
                {
                    EditorGUILayout.Space(10);

                    // 환경별 결과 박스
                    GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox);
                    EditorGUILayout.BeginVertical(boxStyle);

                    GUILayout.Label($"🏷️ {config.name} Environment", EditorStyles.boldLabel);

                    if (!string.IsNullOrEmpty(config.sha1))
                    {
                        EditorGUILayout.LabelField("SHA1:", EditorStyles.boldLabel);
                        EditorGUILayout.SelectableLabel(config.sha1, EditorStyles.textField, GUILayout.Height(20));

                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button($"📋 Copy {config.name} SHA1"))
                        {
                            EditorGUIUtility.systemCopyBuffer = config.sha1;
                            UnityEngine.Debug.Log($"✅ {config.name} SHA1 copied: {config.sha1}");
                            ShowNotification(new GUIContent($"{config.name} SHA1 copied!"));
                        }
                        if (GUILayout.Button("🌐 Firebase Console"))
                        {
                            Application.OpenURL("https://console.firebase.google.com/");
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (!string.IsNullOrEmpty(config.sha256))
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("SHA256:", EditorStyles.boldLabel);
                        EditorGUILayout.SelectableLabel(config.sha256, EditorStyles.textField, GUILayout.Height(20));

                        if (GUILayout.Button($"📋 Copy {config.name} SHA256"))
                        {
                            EditorGUIUtility.systemCopyBuffer = config.sha256;
                            UnityEngine.Debug.Log($"✅ {config.name} SHA256 copied: {config.sha256}");
                            ShowNotification(new GUIContent($"{config.name} SHA256 copied!"));
                        }
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // 전체 결과 출력 버튼
            if (GUILayout.Button("📄 Print All Results to Console"))
            {
                PrintAllResultsToConsole();
            }
        }

        // 하단 유틸리티
        EditorGUILayout.Space(15);
        GUILayout.Label("🔧 Utilities:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📂 Open Keystores Folder"))
        {
            string keystoreFolder = Path.Combine(projectPath, "Assets", "Keystores");
            if (Directory.Exists(keystoreFolder))
            {
                EditorUtility.RevealInFinder(keystoreFolder);
            }
            else
            {
                EditorUtility.DisplayDialog("Folder Not Found", $"Keystores folder not found:\n{keystoreFolder}", "OK");
            }
        }
        if (GUILayout.Button("🔄 Reset Results"))
        {
            ResetResults();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void FindKeytoolPath()
    {
        UnityEngine.Debug.Log("🔍 Searching for keytool...");

        List<string> searchPaths = new List<string>();

        // Unity Hub 경로들 (가장 가능성 높음)
        string unityHubPath = @"C:\Program Files\Unity\Hub\Editor";
        if (Directory.Exists(unityHubPath))
        {
            try
            {
                string[] versions = Directory.GetDirectories(unityHubPath);
                foreach (string version in versions)
                {
                    string keytoolPath = Path.Combine(version, @"Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe");
                    if (File.Exists(keytoolPath))
                    {
                        searchPaths.Add(keytoolPath);
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Error searching Unity Hub path: {e.Message}");
            }
        }

        // Unity 직접 설치 경로들
        searchPaths.Add(@"C:\Program Files\Unity\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe");
        searchPaths.Add(@"C:\Program Files (x86)\Unity\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe");

        // Unity Editor Preferences에서 JDK 경로
        string jdkPath = EditorPrefs.GetString("JdkPath");
        if (!string.IsNullOrEmpty(jdkPath))
        {
            searchPaths.Add(Path.Combine(jdkPath, "bin", "keytool.exe"));
        }

        // 일반적인 Java 설치 경로들
        string[] javaPaths = {
            @"C:\Program Files\Java",
            @"C:\Program Files (x86)\Java",
            @"C:\Program Files\Eclipse Foundation",
            @"C:\Program Files\AdoptOpenJDK",
            @"C:\Program Files\Microsoft\jdk-11.0.16.101-hotspot"
        };

        foreach (string javaPath in javaPaths)
        {
            if (Directory.Exists(javaPath))
            {
                try
                {
                    string[] subDirs = Directory.GetDirectories(javaPath);
                    foreach (string subDir in subDirs)
                    {
                        string keytoolPath = Path.Combine(subDir, "bin", "keytool.exe");
                        if (File.Exists(keytoolPath))
                        {
                            searchPaths.Add(keytoolPath);
                        }
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"Error searching Java path {javaPath}: {e.Message}");
                }
            }
        }

        // 첫 번째로 찾은 경로 사용
        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
            {
                foundKeytoolPath = path;
                UnityEngine.Debug.Log($"✅ Found keytool at: {foundKeytoolPath}");
                EditorUtility.DisplayDialog("Keytool Found! 🎉", $"Found keytool at:\n{foundKeytoolPath}", "Great!");
                return;
            }
        }

        UnityEngine.Debug.LogError("❌ Keytool not found in any common locations");
        EditorUtility.DisplayDialog("Keytool Not Found ❌",
            "Keytool not found!\n\nSolutions:\n" +
            "1. Install Unity Android Build Support\n" +
            "2. Install Java JDK\n" +
            "3. Set JDK path in Edit → Preferences → External Tools\n" +
            "4. Check Unity installation",
            "OK");
    }

    private void ExtractAllSHA()
    {
        isProcessing = true;
        int successCount = 0;

        foreach (var config in keystoreConfigs)
        {
            if (File.Exists(config.keystorePath))
            {
                if (ExtractSHAForConfig(config))
                {
                    successCount++;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"⚠️ {config.name} keystore not found: {config.keystorePath}");
            }
        }

        isProcessing = false;
        showResults = successCount > 0;

        string message = $"✅ Extraction completed!\n\nProcessed: {successCount}/{keystoreConfigs.Count} keystores";
        UnityEngine.Debug.Log(message);
        EditorUtility.DisplayDialog("Extraction Complete! 🎉", message, "Awesome!");
    }

    private void ExtractSingleSHA(KeystoreConfig config)
    {
        isProcessing = true;

        if (File.Exists(config.keystorePath))
        {
            bool success = ExtractSHAForConfig(config);
            showResults = success;

            if (success)
            {
                UnityEngine.Debug.Log($"✅ {config.name} SHA extraction successful!");
                EditorUtility.DisplayDialog("Success! 🎉", $"{config.name} SHA extracted successfully!", "Great!");
            }
        }
        else
        {
            EditorUtility.DisplayDialog("File Not Found ❌", $"{config.name} keystore not found:\n{config.keystorePath}", "OK");
        }

        isProcessing = false;
    }

    private bool ExtractSHAForConfig(KeystoreConfig config)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = foundKeytoolPath;
            startInfo.Arguments = $"-list -v -keystore \"{config.keystorePath}\" -alias {config.aliasName} -storepass {config.storePassword} -keypass {config.keyPassword}";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            UnityEngine.Debug.Log($"🔄 Processing {config.name} keystore...");

            Process process = Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                ExtractSHAFromOutput(config, output);
                config.isProcessed = true;
                return true;
            }
            else
            {
                UnityEngine.Debug.LogError($"❌ {config.name} keytool error: {error}");
                EditorUtility.DisplayDialog("Keytool Error", $"{config.name} extraction failed:\n{error}", "OK");
                return false;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"❌ Exception during {config.name} SHA extraction: {ex.Message}");
            EditorUtility.DisplayDialog("Exception", $"{config.name} extraction failed:\n{ex.Message}", "OK");
            return false;
        }
    }

    private void ExtractSHAFromOutput(KeystoreConfig config, string output)
    {
        // SHA1 추출
        Regex sha1Regex = new Regex(@"SHA1:\s*([A-Fa-f0-9:]+)");
        Match sha1Match = sha1Regex.Match(output);
        if (sha1Match.Success)
        {
            config.sha1 = sha1Match.Groups[1].Value.Trim();
            UnityEngine.Debug.Log($"✅ {config.name} SHA1: {config.sha1}");
        }

        // SHA256 추출
        Regex sha256Regex = new Regex(@"SHA256:\s*([A-Fa-f0-9:]+)");
        Match sha256Match = sha256Regex.Match(output);
        if (sha256Match.Success)
        {
            config.sha256 = sha256Match.Groups[1].Value.Trim();
            UnityEngine.Debug.Log($"✅ {config.name} SHA256: {config.sha256}");
        }
    }

    private void PrintAllResultsToConsole()
    {
        UnityEngine.Debug.Log("=== 📋 ALL KEYSTORE SHA RESULTS ===");

        foreach (var config in keystoreConfigs)
        {
            if (config.isProcessed)
            {
                UnityEngine.Debug.Log($"\n🏷️ {config.name.ToUpper()} ENVIRONMENT:");
                if (!string.IsNullOrEmpty(config.sha1))
                    UnityEngine.Debug.Log($"SHA1: {config.sha1}");
                if (!string.IsNullOrEmpty(config.sha256))
                    UnityEngine.Debug.Log($"SHA256: {config.sha256}");
            }
        }

        UnityEngine.Debug.Log("\n=== END OF RESULTS ===");
    }

    private void ResetResults()
    {
        foreach (var config in keystoreConfigs)
        {
            config.sha1 = "";
            config.sha256 = "";
            config.isProcessed = false;
        }
        showResults = false;
        UnityEngine.Debug.Log("🔄 Results reset!");
    }

    //// 퀵 액세스 메뉴 아이템들
    //[MenuItem("Tools/Quick SHA Extract/🚀 All Keystores")]
    //public static void QuickExtractAll()
    //{
    //    AutoKeystoreSHAExtractor window = GetWindow<AutoKeystoreSHAExtractor>();
    //    if (string.IsNullOrEmpty(window.foundKeytoolPath))
    //    {
    //        window.FindKeytoolPath();
    //    }
    //    if (!string.IsNullOrEmpty(window.foundKeytoolPath))
    //    {
    //        window.ExtractAllSHA();
    //    }
    //}

    //[MenuItem("Tools/Quick SHA Extract/🔧 Dev Only")]
    //public static void QuickExtractDev()
    //{
    //    AutoKeystoreSHAExtractor window = GetWindow<AutoKeystoreSHAExtractor>();
    //    if (string.IsNullOrEmpty(window.foundKeytoolPath))
    //    {
    //        window.FindKeytoolPath();
    //    }
    //    if (!string.IsNullOrEmpty(window.foundKeytoolPath))
    //    {
    //        window.ExtractSingleSHA(window.keystoreConfigs[0]);
    //    }
    //}
}

#endif