#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;

public class EnvBuildManager : EditorWindow
{
    public enum Env { PROD = 0, TEST = 1, DEV = 2 }

    // Base package name
    private const string BASE_PACKAGE = "com.overthehand.chipy";

    // 각 환경별 키스토어 기본값
    private const string DEV_KEYSTORE_PATH = "Assets/Keystores/Dev.keystore";
    private const string DEV_KEYSTORE_PASSWORD = "chipy12";
    private const string DEV_KEY_ALIAS = "chipy12";
    private const string DEV_KEY_PASSWORD = "chipy12";

    private const string TEST_KEYSTORE_PATH = "Assets/Keystores/Test.keystore";
    private const string TEST_KEYSTORE_PASSWORD = "chipy12";
    private const string TEST_KEY_ALIAS = "chipy12";
    private const string TEST_KEY_PASSWORD = "chipy12";

    private const string PROD_KEYSTORE_PATH = "Assets/Keystores/Prod.keystore";
    private const string PROD_KEYSTORE_PASSWORD = "chipy12";
    private const string PROD_KEY_ALIAS = "chipy12";
    private const string PROD_KEY_PASSWORD = "chipy12";

    // 빌드 설정
    private static Env selectedEnv = Env.DEV;
    private static string outputDirectory = "Builds/";
    private static string appName = "Chipy_Dev";

    // 사용자 지정 패키지명
    private static bool useCustomPackage = false;
    private static string customPackageName = "";

    // 플랫폼 선택
    private static bool buildAndroid = true;
    private static bool buildiOS = false;

    // UI 스크롤
    private Vector2 scrollPosition;

    [MenuItem("Build/Env Build Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<EnvBuildManager>("Env Build Manager");
        window.minSize = new Vector2(400, 700);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Environment Build Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 환경 선택
        DrawEnvironmentSelection();
        EditorGUILayout.Space(10);

        // 플랫폼 선택
        DrawPlatformSelection();
        EditorGUILayout.Space(10);

        // === 1단계: 패키지명 설정 ===
        DrawPackageSettings();
        EditorGUILayout.Space(10);

        // === 2단계: Force Resolve ===
        DrawResolveSettings();
        EditorGUILayout.Space(10);

        // === 3단계: 빌드 설정 ===
        DrawBuildSettings();
        EditorGUILayout.Space(20);

        // 유틸리티
        DrawUtilities();
        EditorGUILayout.Space(10);

        EditorGUILayout.EndScrollView();
    }

    private void DrawEnvironmentSelection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("환경 선택", EditorStyles.boldLabel);

        var newEnv = (Env)EditorGUILayout.EnumPopup("Environment", selectedEnv);
        if (newEnv != selectedEnv)
        {
            selectedEnv = newEnv;
            UpdateDefaultSettings();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPlatformSelection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("플랫폼 선택", EditorStyles.boldLabel);

        buildAndroid = EditorGUILayout.Toggle("Android 빌드", buildAndroid);
        buildiOS = EditorGUILayout.Toggle("iOS 빌드", buildiOS);

        if (!buildAndroid && !buildiOS)
        {
            EditorGUILayout.HelpBox("최소 하나의 플랫폼을 선택해주세요.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPackageSettings()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("1단계: 패키지명 설정", EditorStyles.boldLabel);

        useCustomPackage = EditorGUILayout.Toggle("사용자 지정 패키지명 사용", useCustomPackage);

        if (useCustomPackage)
        {
            customPackageName = EditorGUILayout.TextField("패키지명:", customPackageName);
            if (string.IsNullOrEmpty(customPackageName))
            {
                EditorGUILayout.HelpBox("예: com.yourcompany.yourapp", MessageType.Info);
            }
        }

        string currentPackage = GetPackageForEnv(selectedEnv);
        EditorGUILayout.LabelField("적용될 패키지명:", currentPackage);

        if (buildAndroid)
        {
            EditorGUILayout.LabelField("현재 Android 패키지명:", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        }
        if (buildiOS)
        {
            EditorGUILayout.LabelField("현재 iOS 패키지명:", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS));
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button($"{selectedEnv} 패키지명 적용", GUILayout.Height(35)))
        {
            ApplyPackageNameOnly();
        }

        EditorGUILayout.HelpBox("패키지를 변경했을 때, Firebase [please fix your bundle id] 팝업이 노출되는지 확인해야합니다. 해당 팝업이 노출될 경우, Cancel하여, 패키지를 덮어씌우지 않도록 주의해야합니다.", MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    private void DrawResolveSettings()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("2단계: Force Resolve", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("패키지명 변경 후 EDM4U Force Resolve를 실행합니다.", MessageType.Info);

        if (GUILayout.Button($"{selectedEnv} Force Resolve 실행", GUILayout.Height(35)))
        {
            ExecuteForceResolveOnly();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBuildSettings()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("3단계: 빌드 설정", EditorStyles.boldLabel);

        // 출력 디렉토리 설정
        EditorGUILayout.BeginHorizontal();
        outputDirectory = EditorGUILayout.TextField("빌드 출력 경로:", outputDirectory);
        if (GUILayout.Button("📁", GUILayout.Width(30)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("빌드 출력 폴더 선택", outputDirectory, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 프로젝트 루트 기준 상대 경로로 변환
                string projectPath = Directory.GetParent(Application.dataPath).FullName;
                if (selectedPath.StartsWith(projectPath))
                {
                    outputDirectory = Path.GetRelativePath(projectPath, selectedPath).Replace('\\', '/') + "/";
                }
                else
                {
                    outputDirectory = selectedPath.Replace('\\', '/') + "/";
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // 앱 이름 설정
        appName = EditorGUILayout.TextField("앱 이름:", appName);

        EditorGUILayout.Space(5);

        // 빌드 정보 표시
        EditorGUILayout.LabelField("빌드 정보", EditorStyles.boldLabel);

        if (buildAndroid)
        {
            string apkPath = Path.Combine(outputDirectory, $"{appName}.apk");
            string aabPath = Path.Combine(outputDirectory, $"{appName}.aab");
            EditorGUILayout.LabelField("APK 출력:", apkPath);
            EditorGUILayout.LabelField("AAB 출력:", aabPath);

            // 키스토어 정보
            var (ksPath, ksAlias) = GetKeystoreInfo(selectedEnv);
            EditorGUILayout.LabelField("키스토어:", Path.GetFileName(ksPath));
            EditorGUILayout.LabelField("Alias:", ksAlias);
        }

        if (buildiOS)
        {
            string xcodeProjectPath = Path.Combine(outputDirectory, appName);
            EditorGUILayout.LabelField("Xcode 프로젝트 출력:", xcodeProjectPath);
        }

        EditorGUILayout.Space(10);

        // 개별 빌드 버튼
        EditorGUILayout.LabelField("개별 빌드:", EditorStyles.boldLabel);

        if (buildAndroid)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"{selectedEnv} APK 빌드", GUILayout.Height(35)))
            {
                ExecuteBuildOnly("APK");
            }

            if (GUILayout.Button($"{selectedEnv} AAB 빌드", GUILayout.Height(35)))
            {
                ExecuteBuildOnly("AAB");
            }
            EditorGUILayout.EndHorizontal();
        }

        if (buildiOS)
        {
            if (GUILayout.Button($"{selectedEnv} iOS Xcode 빌드", GUILayout.Height(35)))
            {
                ExecuteBuildOnly("iOS");
            }
        }

        EditorGUILayout.Space(10);

        // 원클릭 빌드 (기존 방식)
        EditorGUILayout.LabelField("원클릭 빌드 (전체 과정):", EditorStyles.boldLabel);

        if (buildAndroid)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"🚀 {selectedEnv} 풀 APK 빌드", GUILayout.Height(30)))
            {
                BuildAPK(selectedEnv);
            }

            if (GUILayout.Button($"🚀 {selectedEnv} 풀 AAB 빌드", GUILayout.Height(30)))
            {
                BuildAAB(selectedEnv);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (buildiOS)
        {
            if (GUILayout.Button($"🚀 {selectedEnv} 풀 iOS 빌드", GUILayout.Height(30)))
            {
                BuildiOS(selectedEnv);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawUtilities()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("유틸리티", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (buildAndroid && GUILayout.Button("키스토어 확인"))
        {
            CheckKeystoreInfo(selectedEnv);
        }

        if (GUILayout.Button("빌드 폴더 열기"))
        {
            OpenBuildFolder();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("출력 폴더 열기"))
        {
            OpenOutputFolder();
        }

        if (GUILayout.Button("설정 초기화"))
        {
            ResetSettings();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    #region 1단계: 패키지명 변경

    private void ApplyPackageNameOnly()
    {
        try
        {
            string packageName = GetPackageForEnv(selectedEnv);

            if (string.IsNullOrEmpty(packageName))
            {
                EditorUtility.DisplayDialog("오류", "올바른 패키지명을 입력해주세요.", "OK");
                return;
            }

            // Android 패키지명 적용
            if (buildAndroid)
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, packageName);
            }

            // iOS 패키지명 적용
            if (buildiOS)
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, packageName);
            }

            Debug.Log($"[EnvBuildManager] 1단계 완료 - 패키지명 설정: {packageName}");

            string platforms = "";
            if (buildAndroid) platforms += "Android ";
            if (buildiOS) platforms += "iOS ";

            EditorUtility.DisplayDialog("1단계 완료",
                $"{platforms}패키지명이 '{packageName}'로 설정되었습니다.\n\n다음: 2단계 Force Resolve를 실행하세요.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnvBuildManager] 패키지명 설정 실패: {e.Message}");
            EditorUtility.DisplayDialog("오류", $"패키지명 설정 실패: {e.Message}", "OK");
        }
    }

    #endregion

    #region 2단계: Force Resolve

    private void ExecuteForceResolveOnly()
    {
        try
        {
            Debug.Log("[EnvBuildManager] 2단계 시작 - Force Resolve 실행");

            // EnvSwitcher 환경 전환 (Firebase 설정 등)
            TrySwitchEnv(selectedEnv);

            // EDM4U Force Resolve (Android 및 iOS 공통)
            RunExternalDependencyResolverIfPresent();

            EditorUtility.DisplayDialog("2단계 진행중",
                "Force Resolve가 시작되었습니다.\n\n완료되면 3단계 빌드를 실행하세요.\n(콘솔 로그를 확인하세요)", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnvBuildManager] Force Resolve 실패: {e.Message}");
            EditorUtility.DisplayDialog("오류", $"Force Resolve 실패: {e.Message}", "OK");
        }
    }

    #endregion

    #region 3단계: 빌드만 실행

    private void ExecuteBuildOnly(string buildType)
    {
        try
        {
            Debug.Log($"[EnvBuildManager] 3단계 시작 - {buildType} 빌드 실행");

            if (buildType == "APK" || buildType == "AAB")
            {
                if (!SetKeystoreForEnv(selectedEnv))
                {
                    EditorUtility.DisplayDialog("오류", "키스토어 설정 실패!", "OK");
                    return;
                }

                if (buildType == "APK")
                {
                    ExecuteAPKBuild(selectedEnv);
                }
                else if (buildType == "AAB")
                {
                    ExecuteAABBuild(selectedEnv);
                }
            }
            else if (buildType == "iOS")
            {
                ExecuteiOSBuild(selectedEnv);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EnvBuildManager] {buildType} 빌드 실패: {e.Message}");
            EditorUtility.DisplayDialog("오류", $"{buildType} 빌드 실패: {e.Message}", "OK");
        }
    }

    #endregion

    #region 기존 원클릭 빌드 (호환성)

    [MenuItem("Build/Quick Dev APK Build")]
    public static void QuickDevBuild()
    {
        BuildAPK(Env.DEV);
    }

    public static void BuildAPK(Env env)
    {
        Debug.Log($"=== 원클릭 {env} APK 빌드 시작 (전체 과정) ===");

        EditorPrefs.SetString("EnvBuildManager.PendingBuild", "APK");
        EditorPrefs.SetString("EnvBuildManager.PendingEnv", env.ToString());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorApplication.delayCall += TryContinuePendingBuild;
    }

    public static void BuildAAB(Env env)
    {
        Debug.Log($"=== 원클릭 {env} AAB 빌드 시작 (전체 과정) ===");

        EditorPrefs.SetString("EnvBuildManager.PendingBuild", "AAB");
        EditorPrefs.SetString("EnvBuildManager.PendingEnv", env.ToString());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorApplication.delayCall += TryContinuePendingBuild;
    }

    [MenuItem("Build/Quick Dev iOS Build")]
    public static void QuickDeviOSBuild()
    {
        BuildiOS(Env.DEV);
    }

    public static void BuildiOS(Env env)
    {
        Debug.Log($"=== 원클릭 {env} iOS 빌드 시작 (전체 과정) ===");

        EditorPrefs.SetString("EnvBuildManager.PendingBuild", "iOS");
        EditorPrefs.SetString("EnvBuildManager.PendingEnv", env.ToString());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorApplication.delayCall += TryContinuePendingBuild;
    }

    [InitializeOnLoadMethod]
    private static void TryContinuePendingBuild()
    {
        var pending = EditorPrefs.GetString("EnvBuildManager.PendingBuild", string.Empty);
        var pendingEnv = EditorPrefs.GetString("EnvBuildManager.PendingEnv", string.Empty);
        if (string.IsNullOrEmpty(pending) || string.IsNullOrEmpty(pendingEnv)) return;

        EditorPrefs.DeleteKey("EnvBuildManager.PendingBuild");
        EditorPrefs.DeleteKey("EnvBuildManager.PendingEnv");

        if (!Enum.TryParse<Env>(pendingEnv, out var env))
        {
            Debug.LogWarning($"Unknown pending env: {pendingEnv}");
            return;
        }

        WaitUntilEditorReady(() =>
        {
            // 1단계: 패키지명 적용
            string packageName = GetPackageForEnv(env);
            if (pending == "APK" || pending == "AAB")
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, packageName);
            }
            else if (pending == "iOS")
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, packageName);
            }

            // 2단계: 환경 전환 및 Force Resolve
            TrySwitchEnv(env);
            RunExternalDependencyResolverIfPresent();

            // 3단계: Resolve 완료 후 빌드 실행
            WaitForResolveComplete(() =>
            {
                AssetDatabase.Refresh();

                if (pending == "APK")
                {
                    ExecuteAPKBuild(env);
                }
                else if (pending == "AAB")
                {
                    ExecuteAABBuild(env);
                }
                else if (pending == "iOS")
                {
                    ExecuteiOSBuild(env);
                }
                else
                {
                    Debug.LogWarning($"Unknown pending build type: {pending}");
                }
            });
        });
    }

    #endregion

    #region 공통 유틸리티 메서드

    private void UpdateDefaultSettings()
    {
        switch (selectedEnv)
        {
            case Env.DEV:
                if (outputDirectory == "" || outputDirectory.StartsWith("Builds/") == false)
                    outputDirectory = "Builds/Dev/";
                appName = appName.StartsWith("Chipy_") ? $"Chipy_Dev" : appName;
                break;
            case Env.TEST:
                if (outputDirectory == "" || outputDirectory.StartsWith("Builds/") == false)
                    outputDirectory = "Builds/Test/";
                appName = appName.StartsWith("Chipy_") ? $"Chipy_Test" : appName;
                break;
            case Env.PROD:
                if (outputDirectory == "" || outputDirectory.StartsWith("Builds/") == false)
                    outputDirectory = "Builds/Prod/";
                appName = appName.StartsWith("Chipy_") ? $"Chipy_Prod" : appName;
                break;
        }
    }

    private static string GetPackageForEnv(Env env)
    {
        if (useCustomPackage && !string.IsNullOrEmpty(customPackageName))
        {
            return customPackageName;
        }

        return env switch
        {
            Env.DEV => $"{BASE_PACKAGE}.dev",
            Env.TEST => $"{BASE_PACKAGE}.test",
            _ => BASE_PACKAGE, // PROD
        };
    }

    private static void TrySwitchEnv(Env env)
    {
        try
        {
            switch (env)
            {
                case Env.DEV: EnvSwitcher.SetDev(); break;
                case Env.TEST: EnvSwitcher.SetTest(); break;
                case Env.PROD: EnvSwitcher.SetProd(); break;
            }
            Debug.Log($"✅ EnvSwitcher.Set{env}() called");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"EnvSwitcher.Set{env} call failed or missing. Proceeding anyway. {e.Message}");
        }
    }

    // 실제 APK 빌드 로직
    private static void ExecuteAPKBuild(Env env)
    {
        Debug.Log($"=== Starting {env} APK Build ===");

        string fullBuildPath = Path.Combine(outputDirectory, $"{appName}.apk");
        CreateBuildDirectory(outputDirectory);

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = GetEnabledScenePaths(),
            locationPathName = fullBuildPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"✅ {env} APK Build Successful! \nPath: {fullBuildPath}");
            EditorUtility.DisplayDialog("3단계 완료!",
                $"{env} APK 빌드 성공!\n\nPath: {fullBuildPath}", "OK");
        }
        else
        {
            Debug.LogError($"❌ {env} APK Build Failed!");
            EditorUtility.DisplayDialog("빌드 실패", $"{env} APK 빌드 실패! 콘솔을 확인하세요.", "OK");
        }
    }

    // 실제 AAB 빌드 로직
    private static void ExecuteAABBuild(Env env)
    {
        Debug.Log($"=== Starting {env} AAB Build ===");

        EditorUserBuildSettings.buildAppBundle = true;

        string fullBuildPath = Path.Combine(outputDirectory, $"{appName}.aab");
        CreateBuildDirectory(outputDirectory);

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = GetEnabledScenePaths(),
            locationPathName = fullBuildPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);

        EditorUserBuildSettings.buildAppBundle = false;

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"✅ {env} AAB Build Successful! \nPath: {fullBuildPath}");
            EditorUtility.DisplayDialog("3단계 완료!",
                $"{env} AAB 빌드 성공!\n\nPath: {fullBuildPath}", "OK");
        }
        else
        {
            Debug.LogError($"❌ {env} AAB Build Failed!");
            EditorUtility.DisplayDialog("빌드 실패", $"{env} AAB 빌드 실패! 콘솔을 확인하세요.", "OK");
        }
    }

    // 실제 iOS 빌드 로직 (Xcode 프로젝트 생성)
    private static void ExecuteiOSBuild(Env env)
    {
        Debug.Log($"=== Starting {env} iOS Xcode Build ===");

        string xcodeProjectPath = Path.Combine(outputDirectory, appName);
        CreateBuildDirectory(outputDirectory);

        // iOS 기본 설정
        PlayerSettings.iOS.buildNumber = DateTime.Now.ToString("yyMMddHHmm");

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = GetEnabledScenePaths(),
            locationPathName = xcodeProjectPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"✅ {env} iOS Xcode Project Build Successful! \nPath: {xcodeProjectPath}");
            EditorUtility.DisplayDialog("3단계 완료!",
                $"{env} iOS Xcode 프로젝트 빌드 성공!\n\nPath: {xcodeProjectPath}\n\nXcode에서 프로젝트를 열어 최종 빌드를 진행하세요.", "OK");

            // Xcode 프로젝트 폴더 열기
            if (EditorUtility.DisplayDialog("Xcode 프로젝트 열기", "생성된 Xcode 프로젝트 폴더를 열겠습니까?", "예", "아니오"))
            {
                EditorUtility.RevealInFinder(xcodeProjectPath);
            }
        }
        else
        {
            Debug.LogError($"❌ {env} iOS Build Failed!");
            EditorUtility.DisplayDialog("빌드 실패", $"{env} iOS 빌드 실패! 콘솔을 확인하세요.", "OK");
        }
    }

    private static bool SetKeystoreForEnv(Env env)
    {
        var (path, pass, alias, aliasPass) = GetKeystoreConfig(env);

        if (!File.Exists(path))
        {
            Debug.LogError($"{env} keystore not found at: {path}");
            return false;
        }

        PlayerSettings.Android.keystoreName = path;
        PlayerSettings.Android.keystorePass = pass;
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = aliasPass;

        Debug.Log($"✅ {env} keystore settings applied successfully!");
        return true;
    }

    private static (string path, string alias) GetKeystoreInfo(Env env)
    {
        var (p, _, a, _) = GetKeystoreConfig(env);
        return (p, a);
    }

    private static (string path, string pass, string alias, string aliasPass) GetKeystoreConfig(Env env)
    {
        switch (env)
        {
            case Env.DEV:
                return (DEV_KEYSTORE_PATH, DEV_KEYSTORE_PASSWORD, DEV_KEY_ALIAS, DEV_KEY_PASSWORD);
            case Env.TEST:
                return (TEST_KEYSTORE_PATH, TEST_KEYSTORE_PASSWORD, TEST_KEY_ALIAS, TEST_KEY_PASSWORD);
            case Env.PROD:
            default:
                return (PROD_KEYSTORE_PATH, PROD_KEYSTORE_PASSWORD, PROD_KEY_ALIAS, PROD_KEY_PASSWORD);
        }
    }

    private static void CheckKeystoreInfo(Env env)
    {
        var (path, alias) = GetKeystoreInfo(env);
        if (File.Exists(path))
        {
            Debug.Log($"✅ {env} keystore found at: {path}");
            EditorUtility.DisplayDialog("키스토어 확인",
                $"{env} keystore 발견!\n\nPath: {path}\nAlias: {alias}", "OK");
        }
        else
        {
            Debug.LogError($"❌ {env} keystore not found at: {path}");
            EditorUtility.DisplayDialog("키스토어 없음",
                $"{env} keystore 없음!\n\nPath: {path}", "OK");
        }
    }

    private static string[] GetEnabledScenePaths()
    {
        var scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }
        return scenes;
    }

    private static void CreateBuildDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Debug.Log($"Created build directory: {path}");
        }
    }

    private static void OpenBuildFolder()
    {
        if (Directory.Exists(outputDirectory))
        {
            EditorUtility.RevealInFinder(outputDirectory);
        }
        else
        {
            EditorUtility.DisplayDialog("폴더 없음", $"빌드 폴더가 없습니다:\n{outputDirectory}", "OK");
        }
    }

    private static void OpenOutputFolder()
    {
        CreateBuildDirectory(outputDirectory);
        EditorUtility.RevealInFinder(outputDirectory);
    }

    private void ResetSettings()
    {
        if (EditorUtility.DisplayDialog("설정 초기화", "모든 설정을 초기화하시겠습니까?", "Yes", "No"))
        {
            selectedEnv = Env.DEV;
            outputDirectory = "Builds/";
            appName = "Chipy_Dev";
            useCustomPackage = false;
            customPackageName = "";
            buildAndroid = true;
            buildiOS = false;
            UpdateDefaultSettings();

            Debug.Log("[EnvBuildManager] 설정이 초기화되었습니다.");
        }
    }

    private static void WaitForResolveComplete(Action onComplete)
    {
        int maxWaitTime = 100;
        int waitCount = 0;

        void CheckResolveStatus()
        {
            if (waitCount >= maxWaitTime)
            {
                Debug.LogWarning("[EnvBuildManager] Force Resolve 대기 시간 초과. 빌드를 진행합니다.");
                onComplete?.Invoke();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                waitCount++;
                Debug.Log($"[EnvBuildManager] Force Resolve 진행 중... ({waitCount}/{maxWaitTime}초)");
                EditorApplication.delayCall += CheckResolveStatus;
                return;
            }

            Debug.Log("[EnvBuildManager] ✅ Force Resolve 완료! 빌드를 시작합니다.");
            AssetDatabase.Refresh();
            EditorApplication.delayCall += () => onComplete?.Invoke();
        }

        EditorApplication.delayCall += CheckResolveStatus;
    }

    private static void WaitUntilEditorReady(Action onReady)
    {
        void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Poll;
                return;
            }
            onReady?.Invoke();
        }

        EditorApplication.delayCall += Poll;
    }

    private static void RunExternalDependencyResolverIfPresent()
    {
        try
        {
            // Android Resolver
            var androidResolverType =
                Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver") ??
                Type.GetType("GooglePlayServices.PlayServicesResolver, ExternalDependencyManager");

            if (androidResolverType != null)
            {
                var menuResolve = androidResolverType.GetMethod("MenuResolve", BindingFlags.Public | BindingFlags.Static);
                if (menuResolve != null)
                {
                    Debug.Log("Running Android PlayServicesResolver.MenuResolve() (Force Resolve) ...");
                    menuResolve.Invoke(null, null);
                }
                else
                {
                    var resolve = androidResolverType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (resolve != null)
                    {
                        Debug.Log("Running Android PlayServicesResolver.Resolve() ...");
                        resolve.Invoke(null, null);
                    }
                }
            }

            // iOS Resolver
            var iOSResolverType =
                Type.GetType("Google.IOSResolver, Google.IOSResolver") ??
                Type.GetType("GooglePlayServices.IOSResolver, Google.IOSResolver");

            if (iOSResolverType != null)
            {
                var iOSMenuResolve = iOSResolverType.GetMethod("MenuResolve", BindingFlags.Public | BindingFlags.Static);
                if (iOSMenuResolve != null)
                {
                    Debug.Log("Running iOS IOSResolver.MenuResolve() (Force Resolve) ...");
                    iOSMenuResolve.Invoke(null, null);
                }
            }

            if (androidResolverType == null && iOSResolverType == null)
            {
                Debug.Log("EDM4U Resolvers not found. Skipping resolve.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to run External Dependency Resolver: {e.Message}");
        }
    }

    #endregion
}

#endif