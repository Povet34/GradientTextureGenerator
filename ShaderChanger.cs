using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ShaderChanger : EditorWindow
{
    // ========== 상수 ==========
    private static readonly string[] RENDERING_MODE_PROPERTIES = new string[]
    {
        "_Mode", "_RenderingMode", "_RenderingType",
        "_RenderMode", "_RenderType", "_BlendMode", "_Preset"
    };

    // ========== UI 상태 ==========
    private Shader targetShader;
    private bool includeChildren = true;

    private Vector2 currentShaderScrollPos;
    private Vector2 targetShaderScrollPos;
    private Vector2 mappingScrollPos;

    private bool showCurrentShaderInfo = true;
    private bool showTargetShaderInfo = true;
    private bool showMappingSettings = true;

    // ========== 데이터 ==========
    private class MaterialInfo
    {
        public Material material;
        public string objectName;
        public string shaderName;
        public string renderModeName;
        public int renderModeValue;
    }

    private List<MaterialInfo> currentMaterials = new List<MaterialInfo>();
    private List<string> targetShaderModes = new List<string>();
    private Dictionary<string, int> renderModeMapping = new Dictionary<string, int>();

    // ========== 메뉴 및 초기화 ==========

    [MenuItem("Tools/Shader Changer")]
    static void Init()
    {
        ShaderChanger window = GetWindow<ShaderChanger>();
        window.titleContent = new GUIContent("Shader Changer");
        window.minSize = new Vector2(450, 600);
        window.Show();
    }

    [MenuItem("GameObject/Change Shader (Quick)", false, 0)]
    static void QuickChangeShader() => Init();

    void OnSelectionChange()
    {
        AnalyzeCurrentMaterials();
        Repaint();
    }

    // ========== GUI ==========

    void OnGUI()
    {
        DrawHeader();
        DrawShaderSelection();
        DrawObjectInfo();
        DrawButtons();
    }

    void DrawHeader()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("선택된 오브젝트의 셰이더 변경", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
    }

    void DrawShaderSelection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("변경할 셰이더:", GUILayout.Width(100));

        Shader newShader = (Shader)EditorGUILayout.ObjectField(targetShader, typeof(Shader), false);
        if (newShader != targetShader)
        {
            targetShader = newShader;
            if (targetShader != null) AnalyzeTargetShader();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        bool newIncludeChildren = EditorGUILayout.Toggle("자식 오브젝트 포함", includeChildren);
        if (newIncludeChildren != includeChildren)
        {
            includeChildren = newIncludeChildren;
            AnalyzeCurrentMaterials();
        }

        EditorGUILayout.Space(10);
    }

    void DrawObjectInfo()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            EditorGUILayout.HelpBox("하이어라키에서 오브젝트를 선택해주세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"선택된 오브젝트: {selectedObjects.Length}개", EditorStyles.helpBox);

        DrawCurrentShaderInfo();

        if (targetShader != null)
        {
            EditorGUILayout.Space(10);
            DrawTargetShaderInfo();

            EditorGUILayout.Space(10);
            DrawRenderModeMapping();
        }
    }

    void DrawCurrentShaderInfo()
    {
        showCurrentShaderInfo = EditorGUILayout.Foldout(showCurrentShaderInfo,
            $"현재 셰이더 정보 ({currentMaterials.Count}개 머티리얼)", true);

        if (!showCurrentShaderInfo || currentMaterials.Count == 0) return;

        EditorGUI.indentLevel++;
        currentShaderScrollPos = EditorGUILayout.BeginScrollView(currentShaderScrollPos, GUILayout.Height(150));

        foreach (var info in currentMaterials)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"오브젝트: {info.objectName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  머티리얼: {info.material.name}");
            EditorGUILayout.LabelField($"  셰이더: {info.shaderName}");

            GUIStyle cyanStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.cyan } };
            EditorGUILayout.LabelField($"  렌더링 모드: {info.renderModeName}", cyanStyle);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        EditorGUILayout.EndScrollView();
        EditorGUI.indentLevel--;
    }

    void DrawTargetShaderInfo()
    {
        showTargetShaderInfo = EditorGUILayout.Foldout(showTargetShaderInfo,
            $"타겟 셰이더 정보: {targetShader.name}", true);

        if (!showTargetShaderInfo) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("사용 가능한 렌더링 모드:", EditorStyles.boldLabel);

        if (targetShaderModes.Count > 0)
        {
            GUIStyle greenStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } };
            foreach (var mode in targetShaderModes)
            {
                EditorGUILayout.LabelField($"  • {mode}", greenStyle);
            }
        }
        else
        {
            EditorGUILayout.LabelField("  (렌더링 모드 정보 없음)");
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    void DrawRenderModeMapping()
    {
        var uniqueModes = currentMaterials.Select(m => m.renderModeName).Distinct().ToList();
        if (uniqueModes.Count == 0 || targetShaderModes.Count == 0) return;

        showMappingSettings = EditorGUILayout.Foldout(showMappingSettings, "렌더링 모드 매핑 설정", true);

        if (!showMappingSettings) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.HelpBox("현재 렌더링 모드를 타겟 셰이더의 어떤 모드로 변경할지 선택하세요.", MessageType.Info);
        EditorGUILayout.Space(5);

        mappingScrollPos = EditorGUILayout.BeginScrollView(mappingScrollPos, GUILayout.Height(120));

        foreach (var currentMode in uniqueModes)
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField(currentMode, GUILayout.Width(150));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));

            if (!renderModeMapping.ContainsKey(currentMode))
                renderModeMapping[currentMode] = 0;

            int selectedIndex = Mathf.Clamp(renderModeMapping[currentMode], 0, targetShaderModes.Count - 1);
            renderModeMapping[currentMode] = EditorGUILayout.Popup(selectedIndex, targetShaderModes.ToArray());

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUI.indentLevel--;
    }

    void DrawButtons()
    {
        EditorGUILayout.Space(10);

        GUI.enabled = targetShader != null && Selection.gameObjects.Length > 0;
        if (GUILayout.Button("셰이더 및 렌더링 모드 변경하기", GUILayout.Height(35)))
        {
            ChangeShaders();
        }
        GUI.enabled = true;

        EditorGUILayout.Space(5);
        if (GUILayout.Button("현재 상태 콘솔에 출력", GUILayout.Height(25)))
        {
            PrintCurrentShaders();
        }
    }

    // ========== 분석 로직 ==========

    void AnalyzeCurrentMaterials()
    {
        currentMaterials.Clear();

        foreach (GameObject obj in Selection.gameObjects)
        {
            Renderer[] renderers = includeChildren ?
                obj.GetComponentsInChildren<Renderer>(true) :
                obj.GetComponents<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;

                Material[] materials = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;

                foreach (Material mat in materials)
                {
                    if (mat == null || mat.shader == null) continue;

                    var info = new MaterialInfo
                    {
                        material = mat,
                        objectName = renderer.gameObject.name,
                        shaderName = mat.shader.name
                    };

                    GetRenderingMode(mat, out info.renderModeName, out info.renderModeValue);
                    currentMaterials.Add(info);
                }
            }
        }
    }

    void AnalyzeTargetShader()
    {
        targetShaderModes.Clear();
        if (targetShader == null) return;

        // Material로 프로퍼티 확인
        Material tempMat = new Material(targetShader);
        bool foundRenderingMode = false;

        // 렌더링 모드 프로퍼티 찾기
        foreach (string propName in RENDERING_MODE_PROPERTIES)
        {
            if (tempMat.HasProperty(propName))
            {
                foundRenderingMode = true;
                // 기본 렌더링 모드 추가
                AddStandardRenderModes();
                break;
            }
        }

        // 렌더링 모드를 못 찾았으면 Blend 기반 모드 추가
        if (!foundRenderingMode)
            AddDefaultRenderModes();

        DestroyImmediate(tempMat);
        InitializeMapping();
    }

    void AddStandardRenderModes()
    {
        targetShaderModes.AddRange(new[]
        {
            "Opaque", "Cutout", "Fade", "Transparent",
            "TransparentZWrite", "Additive", "Multiply",
            "Clip", "TransClipping" // Poiyomi 등의 모드 추가
        });
    }

    void AddDefaultRenderModes()
    {
        Material tempMat = new Material(targetShader);

        if (tempMat.HasProperty("_SrcBlend") && tempMat.HasProperty("_DstBlend"))
        {
            targetShaderModes.AddRange(new[] { "Opaque", "Transparent", "TransparentZWrite", "Additive", "Multiply" });
        }
        else
        {
            string renderType = tempMat.GetTag("RenderType", false, "Opaque");
            targetShaderModes.Add(string.IsNullOrEmpty(renderType) ? "Opaque" : renderType);
        }

        DestroyImmediate(tempMat);
    }

    void InitializeMapping()
    {
        var uniqueModes = currentMaterials.Select(m => m.renderModeName).Distinct();

        foreach (var mode in uniqueModes)
        {
            if (renderModeMapping.ContainsKey(mode)) continue;

            // 같은 이름 찾기
            int sameNameIndex = targetShaderModes.FindIndex(m => m == mode);
            renderModeMapping[mode] = sameNameIndex >= 0 ? sameNameIndex : 0;
        }
    }

    // ========== 렌더링 모드 처리 ==========

    void GetRenderingMode(Material mat, out string modeName, out int modeValue)
    {
        modeName = "Unknown";
        modeValue = -1;

        // 렌더링 모드 프로퍼티 찾기
        foreach (string propName in RENDERING_MODE_PROPERTIES)
        {
            if (!mat.HasProperty(propName)) continue;

            modeValue = (int)mat.GetFloat(propName);

            // 값을 기본 이름으로 변환 (Enum 이름을 직접 못 가져오므로)
            modeName = GetDefaultModeName(modeValue);
            return;
        }

        // 프로퍼티가 없으면 Blend 설정으로 추론
        InferRenderingModeFromBlend(mat, out modeName, out modeValue);
    }

    string GetDefaultModeName(int value)
    {
        return value switch
        {
            0 => "Opaque",
            1 => "Cutout",
            2 => "Fade",
            3 => "Transparent",
            4 => "TransparentZWrite",
            5 => "Additive",
            6 => "Multiply",
            7 => "TransClipping",
            8 => "Clip",
            9 => "TransClipping",
            _ => $"Mode {value}"
        };
    }

    void InferRenderingModeFromBlend(Material mat, out string modeName, out int modeValue)
    {
        modeName = mat.GetTag("RenderType", false, "Opaque");
        modeValue = 0;

        if (!mat.HasProperty("_SrcBlend") || !mat.HasProperty("_DstBlend")) return;

        int src = (int)mat.GetFloat("_SrcBlend");
        int dst = (int)mat.GetFloat("_DstBlend");

        if (src == 1 && dst == 0)
        {
            modeName = "Opaque";
            modeValue = 0;
        }
        else if (src == 5 && dst == 10) // SrcAlpha, OneMinusSrcAlpha
        {
            bool zwrite = mat.HasProperty("_ZWrite") && mat.GetFloat("_ZWrite") == 1;
            modeName = zwrite ? "TransparentZWrite" : "Transparent";
            modeValue = zwrite ? 4 : 3;
        }
        else if (src == 1 && dst == 1)
        {
            modeName = "Additive";
            modeValue = 5;
        }
        else if (src == 2 && dst == 0) // DstColor
        {
            modeName = "Multiply";
            modeValue = 6;
        }
    }

    // ========== 셰이더 변경 ==========

    void ChangeShaders()
    {
        if (targetShader == null || currentMaterials.Count == 0)
        {
            EditorUtility.DisplayDialog("오류", "셰이더를 선택하고 오브젝트를 선택해주세요.", "확인");
            return;
        }

        List<string> log = new List<string>();

        foreach (var info in currentMaterials)
        {
            if (!Application.isPlaying)
                Undo.RecordObject(info.material, "Change Shader and Rendering Mode");

            string oldMode = info.renderModeName;
            info.material.shader = targetShader;

            if (renderModeMapping.TryGetValue(oldMode, out int targetIndex) &&
                targetIndex >= 0 && targetIndex < targetShaderModes.Count)
            {
                string targetMode = targetShaderModes[targetIndex];
                ApplyRenderingMode(info.material, targetMode, targetIndex);

                log.Add($"[{info.objectName}] {info.material.name}");
                log.Add($"  {oldMode} → {targetMode}");
            }

            if (!Application.isPlaying)
                EditorUtility.SetDirty(info.material);
        }

        ShowResult(log);
        AnalyzeCurrentMaterials();
    }

    void ApplyRenderingMode(Material mat, string modeName, int modeIndex)
    {
        // 렌더링 모드 프로퍼티 찾아서 설정
        foreach (string propName in RENDERING_MODE_PROPERTIES)
        {
            if (!mat.HasProperty(propName)) continue;

            // 모드 이름을 인덱스로 변환
            int valueToSet = ConvertModeNameToValue(modeName, modeIndex);
            mat.SetFloat(propName, valueToSet);

            Debug.Log($"[ShaderChanger] {propName} = {modeName} ({valueToSet})");
            break;
        }

        // Blend 설정 적용
        ApplyBlendSettings(mat, modeName);
    }

    int ConvertModeNameToValue(string modeName, int fallbackIndex)
    {
        // 일반적인 모드 이름을 값으로 변환
        return modeName switch
        {
            "Opaque" => 0,
            "Cutout" => 1,
            "Fade" => 2,
            "Transparent" => 3,
            "TransparentZWrite" => 4,
            "Additive" => 5,
            "Multiply" => 6,
            "TransClipping" => 9,
            "Clip" => 8,
            _ => fallbackIndex // 알 수 없으면 드롭다운 인덱스 사용
        };
    }

    void ApplyBlendSettings(Material mat, string modeName)
    {
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // 이름 패턴 매칭
        if (modeName.Contains("Opaque"))
            SetBlendMode(mat, 1, 0, 1, -1);
        else if (modeName.Contains("Cutout"))
            SetBlendMode(mat, 1, 0, 1, 2450, "_ALPHATEST_ON");
        else if (modeName.Contains("Fade"))
            SetBlendMode(mat, 5, 10, 0, 3000, "_ALPHABLEND_ON");
        else if (modeName.Contains("ZWrite"))
            SetBlendMode(mat, 5, 10, 1, 3000);
        else if (modeName.Contains("Trans") || modeName.Contains("Transparent"))
            SetBlendMode(mat, 1, 10, 0, 3000, "_ALPHAPREMULTIPLY_ON");
        else if (modeName.Contains("Additive"))
            SetBlendMode(mat, 1, 1, 0, 3000);
        else if (modeName.Contains("Multiply"))
            SetBlendMode(mat, 2, 0, 0, 3000);
    }

    void SetBlendMode(Material mat, int src, int dst, int zwrite, int queue, string keyword = null)
    {
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", src);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", dst);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", zwrite);
        mat.renderQueue = queue;

        if (!string.IsNullOrEmpty(keyword))
            mat.EnableKeyword(keyword);
    }

    // ========== 유틸리티 ==========

    void ShowResult(List<string> log)
    {
        string message = $"총 {currentMaterials.Count}개의 머티리얼을 변경했습니다.\n\n";
        message += log.Count <= 20 ? string.Join("\n", log) : string.Join("\n", log.Take(20)) + $"\n... 외 {log.Count - 20}개";

        Debug.Log($"[ShaderChanger] 변경 완료:\n{string.Join("\n", log)}");
        EditorUtility.DisplayDialog("완료", message, "확인");
    }

    void PrintCurrentShaders()
    {
        if (currentMaterials.Count == 0)
        {
            Debug.Log("[ShaderChanger] 선택된 오브젝트가 없습니다.");
            return;
        }

        Debug.Log("========== 현재 셰이더 및 렌더링 모드 정보 ==========");

        foreach (var info in currentMaterials)
        {
            Debug.Log($"[{info.objectName}] {info.material.name}");
            Debug.Log($"  Shader: {info.shaderName}");
            Debug.Log($"  Rendering Mode: {info.renderModeName} (값: {info.renderModeValue})");

            PrintMaterialProperties(info.material);
            Debug.Log("---");
        }

        Debug.Log("=================================================");
    }

    void PrintMaterialProperties(Material mat)
    {
        foreach (string propName in RENDERING_MODE_PROPERTIES)
        {
            if (mat.HasProperty(propName))
            {
                Debug.Log($"  렌더링 모드 프로퍼티: {propName} = {mat.GetFloat(propName)}");
                break;
            }
        }

        if (mat.HasProperty("_SrcBlend")) Debug.Log($"  SrcBlend: {mat.GetFloat("_SrcBlend")}");
        if (mat.HasProperty("_DstBlend")) Debug.Log($"  DstBlend: {mat.GetFloat("_DstBlend")}");
        if (mat.HasProperty("_ZWrite")) Debug.Log($"  ZWrite: {mat.GetFloat("_ZWrite")}");
        Debug.Log($"  RenderQueue: {mat.renderQueue}");
    }
}