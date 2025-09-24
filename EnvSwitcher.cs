// Assets/Editor/FirebaseEnvSwitcher.cs
// 폴더 구조:
// FirebaseConfigs/
//  ├─ DEV/  ├─ google-services.json  └─ GoogleService-Info.plist
//  ├─ TEST/ ├─ google-services.json  └─ GoogleService-Info.plist
//  └─ PROD/ ├─ google-services.json  └─ GoogleService-Info.plist
#if UNITY_EDITOR

using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class EnvSwitcher
{
    // 프로젝트 루트/FirebaseConfigs
    static readonly string ConfigRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FirebaseConfigs");

    // Resources 폴더 (빌드 배포용)
    static readonly string ResourcesDir = Path.Combine(Application.dataPath, "Resources");
    static readonly string ResourcesEnvPath = Path.Combine(ResourcesDir, "chipySystem.json");

    enum Env
    {
        PROD = 0,
        TEST = 1,
        DEV = 2,
    }

    [MenuItem("Tools/Set Env/DEV")]
    public static void SetDev() => SetEnv(Env.DEV);

    [MenuItem("Tools/Set Env/TEST")]
    public static void SetTest() => SetEnv(Env.TEST);

    [MenuItem("Tools/Set Env/PROD")]
    public static void SetProd() => SetEnv(Env.PROD);

    static void SetEnv(Env env)
    {
        try
        {
            // 1) 원본 경로 계산
            string envName = env.ToString();
            string envDir = Path.Combine(ConfigRoot, envName);
            string aSrc = Path.Combine(envDir, "google-services.json");
            string iSrc = Path.Combine(envDir, "GoogleService-Info.plist");

            // 2) 타깃 경로(Assets 루트)
            string aDst = Path.Combine(Application.dataPath, "google-services.json");
            string iDst = Path.Combine(Application.dataPath, "GoogleService-Info.plist");

            // 3) 존재 확인
            if (File.Exists(aSrc) == false)
                throw new FileNotFoundException("Android google-services.json not found", aSrc);

            if (File.Exists(iSrc) == false)
                throw new FileNotFoundException("iOS GoogleService-Info.plist not found", iSrc);

            // 4) 간단 검증
            ValidateAndroid(aSrc);
            ValidateiOS(iSrc);

            // 5) Firebase 설정 파일 복사
            CopyFile(aSrc, aDst);
            CopyFile(iSrc, iDst);

            // 6) ChipySystem 환경 설정 파일 생성 (Resources 폴더에만)
            WriteEnvironmentConfig(env);

            // 7) AssetDatabase 갱신
            AssetDatabase.Refresh();

            // 8) EDM Resolve
            RunEDMResolvers();

            // 9) 완료 알림
            EditorUtility.DisplayDialog("Firebase Env",
                $"환경 적용 완료: {envName}\n" +
                $"Firebase 설정 파일 교체 및 Resolve 완료\n" +
                $"ChipySystem 설정: serverEnv = {(int)env}", "OK");

            Debug.Log($"[FirebaseEnvSwitcher] 환경 변경 완료: {envName} (serverEnv: {(int)env})");

            // 10) 어드레서블 프로필 설정
            SetAddressableProfile(envName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseEnvSwitcher] 실패: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Firebase Env Error", $"실패: {e.Message}", "OK");
        }
    }

    static void WriteEnvironmentConfig(Env env)
    {
        var config = new ChipySystem
        {
            serverEnv = (int)env,
            serverPopupBypass = 0,
        };

        string jsonContent = JsonUtility.ToJson(config, true);

        try
        {
            // Resources 폴더 생성 (없으면)
            Directory.CreateDirectory(ResourcesDir);

            // Resources 폴더에 JSON 파일 저장
            File.WriteAllText(ResourcesEnvPath, jsonContent);
            Debug.Log($"[FirebaseEnvSwitcher] Resources 설정 저장: {ResourcesEnvPath}");

            // Resources용 TextAsset도 생성 (.json 확장자 제거)
            string resourcesTextAssetPath = Path.Combine(ResourcesDir, "chipySystem.txt");
            File.WriteAllText(resourcesTextAssetPath, jsonContent);
            Debug.Log($"[FirebaseEnvSwitcher] Resources TextAsset 저장: {resourcesTextAssetPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseEnvSwitcher] Resources 설정 저장 실패: {e.Message}");
            throw;
        }
    }

    static void CopyFile(string src, string dst)
    {
        // .gitignore 권장: Assets/google-services.json, Assets/GoogleService-Info.plist
        File.Copy(src, dst, overwrite: true);
        Debug.Log($"[FirebaseEnvSwitcher] 파일 복사: {Path.GetFileName(src)} -> {Path.GetFileName(dst)}");
    }

    static void RunEDMResolvers()
    {
        try
        {
            Debug.Log("[FirebaseEnvSwitcher] External Dependency Manager Resolve 시작...");

            // Android Resolver
            if (EditorApplication.ExecuteMenuItem("Assets/External Dependency Manager/Android Resolver/Resolve"))
            {
                Debug.Log("[FirebaseEnvSwitcher] Android Resolver 실행 완료");
            }

            // iOS Resolver
            if (EditorApplication.ExecuteMenuItem("Assets/External Dependency Manager/iOS Resolver/Install Cocoapods"))
            {
                Debug.Log("[FirebaseEnvSwitcher] iOS Cocoapods 설치 완료");
            }

            if (EditorApplication.ExecuteMenuItem("Assets/External Dependency Manager/iOS Resolver/Resolve"))
            {
                Debug.Log("[FirebaseEnvSwitcher] iOS Resolver 실행 완료");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FirebaseEnvSwitcher] EDM Resolve 중 일부 실패: {e.Message}");
        }
    }

    static void ValidateAndroid(string jsonPath)
    {
        string json = File.ReadAllText(jsonPath);

        var m = Regex.Match(json, "\"package_name\"\\s*:\\s*\"([^\"]+)\"");
        if (m.Success == false)
        {
            Debug.LogWarning("[FirebaseEnvSwitcher] package_name을 찾지 못했습니다.");
            return;
        }

        string pkg = m.Groups[1].Value.Trim();
        string current = PlayerSettings.applicationIdentifier;
        if (!string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(pkg) && current != pkg)
        {
            Debug.LogWarning($"[FirebaseEnvSwitcher] Android 패키지명 불일치: PlayerSettings={current}, Firebase={pkg}");
            // 치명적 오류로 처리하지 않고 경고만 출력
        }
    }

    static void ValidateiOS(string plistPath)
    {
        string plist = File.ReadAllText(plistPath);

        var m = Regex.Match(plist, "<key>\\s*BUNDLE_ID\\s*</key>\\s*<string>\\s*([^<]+)\\s*</string>");
        if (m.Success == false)
        {
            Debug.LogWarning("[FirebaseEnvSwitcher] BUNDLE_ID를 찾지 못했습니다.");
            return;
        }

        string bundle = m.Groups[1].Value.Trim();
        string current = PlayerSettings.applicationIdentifier;
        if (!string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(bundle) && current != bundle)
        {
            Debug.LogWarning($"[FirebaseEnvSwitcher] iOS 번들 ID 불일치: PlayerSettings={current}, Firebase={bundle}");
            // 치명적 오류로 처리하지 않고 경고만 출력
        }
    }

    private static void SetAddressableProfile(string profile)
    {
        // AddressableAssetSettings 가져오기
        AddressableAssetSettings _addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
        if (_addressableSettings == null)
        {
            Debug.LogError("AddressableAssetSettings not found!");
            return;
        }

        // Profile 변경
        string _profileId = _addressableSettings.profileSettings.GetProfileId(profile);
        if (string.IsNullOrEmpty(_profileId) == false)
        {
            _addressableSettings.activeProfileId = _profileId;
        }
        else
        {
            Debug.LogError($"Profile '{profile}' not found!");
            return;
        }

        // Player Version Override 변경
        _addressableSettings.OverridePlayerVersion = profile;
        Debug.Log($"Catalog/Player Version Override set to: {profile}");

        //// Addressables 캐시 초기화
        //AddressableAssetSettings.CleanPlayerContent(_addressableSettings.ActivePlayerDataBuilder);

        //foreach (var builder in _addressableSettings.DataBuilders)
        //{
        //    if (builder is IDataBuilder dataBuilder && builder != (object)_addressableSettings.ActivePlayerDataBuilder)
        //    {
        //        AddressableAssetSettings.CleanPlayerContent(dataBuilder);
        //    }
        //}
    }

    #region 추가 유틸리티 메뉴

    [MenuItem("Tools/Firebase Env/Show Current Environment")]
    static void ShowCurrentEnv()
    {
        var currentEnv = GetCurrentEnvironment();
        if (currentEnv.HasValue)
        {
            EditorUtility.DisplayDialog("Current Environment",
                $"현재 환경: {currentEnv.Value}\n" +
                $"ServerEnv: {(int)currentEnv.Value}", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Current Environment",
                "환경 설정 파일을 찾을 수 없습니다.", "OK");
        }
    }

    [MenuItem("Tools/Firebase Env/Clean Environment Files")]
    static void CleanEnvironmentFiles()
    {
        try
        {
            int deletedCount = 0;

            // Resources 폴더 정리
            if (File.Exists(ResourcesEnvPath))
            {
                File.Delete(ResourcesEnvPath);
                deletedCount++;
                Debug.Log("[FirebaseEnvSwitcher] chipySystem.json 삭제");
            }

            string resourcesTextAssetPath = Path.Combine(ResourcesDir, "chipySystem.txt");
            if (File.Exists(resourcesTextAssetPath))
            {
                File.Delete(resourcesTextAssetPath);
                deletedCount++;
                Debug.Log("[FirebaseEnvSwitcher] chipySystem.txt 삭제");
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Clean Environment Files",
                $"{deletedCount}개 환경 설정 파일을 삭제했습니다.", "OK");

            Debug.Log($"[FirebaseEnvSwitcher] {deletedCount}개 환경 설정 파일 삭제 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FirebaseEnvSwitcher] 파일 정리 실패: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"파일 정리 실패: {e.Message}", "OK");
        }
    }

    [MenuItem("Tools/Firebase Env/Validate Current Setup")]
    static void ValidateCurrentSetup()
    {
        var result = new System.Text.StringBuilder();
        result.AppendLine("=== Firebase Environment Validation ===");

        // 현재 환경 확인
        var currentEnv = GetCurrentEnvironment();
        if (currentEnv.HasValue)
        {
            result.AppendLine($"현재 환경: {currentEnv.Value} (serverEnv: {(int)currentEnv.Value})");
        }
        else
        {
            result.AppendLine("현재 환경: 설정되지 않음");
        }

        // 파일 존재 여부 확인
        result.AppendLine($"Resources 설정 파일: {(File.Exists(ResourcesEnvPath) ? "존재" : "없음")}");

        string resourcesTextAssetPath = Path.Combine(ResourcesDir, "chipySystem.txt");
        result.AppendLine($"Resources TextAsset: {(File.Exists(resourcesTextAssetPath) ? "존재" : "없음")}");

        // Firebase 설정 파일 확인
        string googleServices = Path.Combine(Application.dataPath, "google-services.json");
        string googleServiceInfo = Path.Combine(Application.dataPath, "GoogleService-Info.plist");
        result.AppendLine($"google-services.json: {(File.Exists(googleServices) ? "존재" : "없음")}");
        result.AppendLine($"GoogleService-Info.plist: {(File.Exists(googleServiceInfo) ? "존재" : "없음")}");

        Debug.Log($"[FirebaseEnvSwitcher] 검증 결과:\n{result}");
        EditorUtility.DisplayDialog("Validation Result", result.ToString(), "OK");
    }

    static Env? GetCurrentEnvironment()
    {
        try
        {
            // Resources에서 확인
            if (File.Exists(ResourcesEnvPath))
            {
                string json = File.ReadAllText(ResourcesEnvPath);
                var config = JsonUtility.FromJson<ChipySystem>(json);
                if (config != null)
                {
                    return (Env)config.serverEnv;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FirebaseEnvSwitcher] 현재 환경 확인 실패: {e.Message}");
        }

        return null;
    }
    #endregion
}

#endif