using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ObjectProfiler : EditorWindow
{
    // ========== UI 상태 ==========
    private bool includeChildren = true;
    private bool autoRefresh = true;
    private Camera selectedCamera;

    private Vector2 scrollPos;
    private bool showMeshInfo = true;
    private bool showMaterialInfo = true;
    private bool showRenderingInfo = true;
    private bool showDetailedList = false;

    // ========== 프로파일링 데이터 ==========
    private class ProfileData
    {
        // 메시 정보
        public int totalVertices;
        public int totalTriangles;
        public int meshCount;
        public int skinnedMeshCount;
        public List<MeshInfo> meshDetails = new List<MeshInfo>();

        // 머티리얼/셰이더 정보
        public int materialCount;
        public int uniqueMaterialCount;
        public int shaderCount;
        public Dictionary<string, int> shaderUsage = new Dictionary<string, int>();
        public Dictionary<Material, int> materialUsage = new Dictionary<Material, int>();
        public Dictionary<Shader, ShaderComplexity> shaderComplexity = new Dictionary<Shader, ShaderComplexity>();

        // 렌더링 정보
        public int rendererCount;
        public int drawCallEstimate;
        public long estimatedMemoryBytes;
    }

    private class ShaderComplexity
    {
        public string shaderName;
        public int passCount;
        public List<PassInfo> passes = new List<PassInfo>();
        public int keywordCount;
        public int textureCount;
        public ComplexityLevel level;
        public string reason;
        public ComputationAnalysis computation = new ComputationAnalysis();
    }

    private class PassInfo
    {
        public string name;
        public string lightMode;
        public bool isForwardAdd;
        public bool isShadowCaster;
    }

    private class ComputationAnalysis
    {
        public int textureSamples;          // 텍스처 샘플링 횟수
        public int mathOperations;          // 수학 연산 복잡도
        public bool hasExpensiveMath;       // 비싼 수학 연산 (sin, cos, pow)
        public bool hasLoops;               // 반복문 추정
        public int branchCount;             // 분기문 개수
        public List<string> expensiveOps = new List<string>(); // 비싼 연산 목록
    }

    private enum ComplexityLevel
    {
        VeryLight,  // 매우 가벼움
        Light,      // 가벼움
        Medium,     // 보통
        Heavy,      // 무거움
        VeryHeavy   // 매우 무거움
    }

    private class MeshInfo
    {
        public string objectName;
        public string meshName;
        public int vertices;
        public int triangles;
        public int subMeshCount;
        public bool isSkinnedMesh;
    }

    private ProfileData currentProfile = new ProfileData();

    // ========== 메뉴 및 초기화 ==========

    [MenuItem("Tools/Object Profiler")]
    static void Init()
    {
        ObjectProfiler window = GetWindow<ObjectProfiler>();
        window.titleContent = new GUIContent("Object Profiler");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    void OnEnable()
    {
        // 기본 카메라 설정
        if (selectedCamera == null)
        {
            selectedCamera = Camera.main;
            if (selectedCamera == null)
                selectedCamera = FindObjectOfType<Camera>();
        }

        ProfileSelectedObjects();
    }

    void OnSelectionChange()
    {
        if (autoRefresh)
        {
            ProfileSelectedObjects();
            Repaint();
        }
    }

    void OnInspectorUpdate()
    {
        if (autoRefresh)
            Repaint();
    }

    // ========== GUI ==========

    void OnGUI()
    {
        DrawHeader();
        DrawOptions();

        if (Selection.gameObjects.Length == 0)
        {
            EditorGUILayout.HelpBox("하이어라키에서 오브젝트를 선택해주세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawMeshInfo();
        DrawMaterialInfo();
        DrawRenderingInfo();

        if (showDetailedList)
            DrawDetailedList();

        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Object Profiler", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"선택된 오브젝트: {Selection.gameObjects.Length}개", EditorStyles.helpBox);
        EditorGUILayout.Space(5);
    }

    void DrawOptions()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        bool newIncludeChildren = EditorGUILayout.Toggle("자식 오브젝트 포함", includeChildren);
        if (newIncludeChildren != includeChildren)
        {
            includeChildren = newIncludeChildren;
            ProfileSelectedObjects();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        autoRefresh = EditorGUILayout.Toggle("자동 갱신", autoRefresh);
        if (GUILayout.Button("수동 갱신", GUILayout.Width(100)))
        {
            ProfileSelectedObjects();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("프로파일 카메라:", GUILayout.Width(100));
        selectedCamera = (Camera)EditorGUILayout.ObjectField(selectedCamera, typeof(Camera), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    void DrawMeshInfo()
    {
        showMeshInfo = EditorGUILayout.Foldout(showMeshInfo, "메시 정보", true, EditorStyles.foldoutHeader);
        if (!showMeshInfo) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        DrawStatRow("총 버텍스 수", FormatNumber(currentProfile.totalVertices), Color.cyan);
        DrawStatRow("총 삼각형 수", FormatNumber(currentProfile.totalTriangles), Color.cyan);
        DrawStatRow("메시 개수", currentProfile.meshCount.ToString());
        DrawStatRow("스킨드 메시 개수", currentProfile.skinnedMeshCount.ToString());

        // 메모리 추정
        float memoryMB = currentProfile.estimatedMemoryBytes / (1024f * 1024f);
        string memoryStr = memoryMB >= 1 ? $"{memoryMB:F2} MB" : $"{currentProfile.estimatedMemoryBytes / 1024f:F2} KB";
        DrawStatRow("예상 메모리 사용량", memoryStr, memoryMB > 10 ? Color.yellow : Color.white);

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    void DrawMaterialInfo()
    {
        showMaterialInfo = EditorGUILayout.Foldout(showMaterialInfo, "머티리얼 & 셰이더 정보", true, EditorStyles.foldoutHeader);
        if (!showMaterialInfo) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        DrawStatRow("총 머티리얼 수", currentProfile.materialCount.ToString());
        DrawStatRow("고유 머티리얼 수", currentProfile.uniqueMaterialCount.ToString());
        DrawStatRow("사용 중인 셰이더 수", currentProfile.shaderCount.ToString());

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("셰이더 사용 현황 (복잡도):", EditorStyles.boldLabel);

        foreach (var kvp in currentProfile.shaderUsage.OrderByDescending(x => x.Value))
        {
            EditorGUILayout.BeginHorizontal();

            // 셰이더 이름
            EditorGUILayout.LabelField($"  {kvp.Key}", GUILayout.Width(200));
            EditorGUILayout.LabelField($"× {kvp.Value}", GUILayout.Width(40));

            // 복잡도 표시
            var complexity = currentProfile.shaderComplexity.Values
                .FirstOrDefault(c => c.shaderName == kvp.Key);

            if (complexity != null)
            {
                Color complexityColor = GetComplexityColor(complexity.level);
                GUIStyle style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = complexityColor;
                style.fontStyle = FontStyle.Bold;

                EditorGUILayout.LabelField($"[{GetComplexityLabel(complexity.level)}]", style, GUILayout.Width(80));

                // 툴팁 아이콘
                if (GUILayout.Button(new GUIContent("?", complexity.reason), GUILayout.Width(20)))
                {
                    EditorUtility.DisplayDialog("셰이더 복잡도 분석",
                        $"셰이더: {complexity.shaderName}\n\n{complexity.reason}", "확인");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    void DrawRenderingInfo()
    {
        showRenderingInfo = EditorGUILayout.Foldout(showRenderingInfo, "렌더링 정보", true, EditorStyles.foldoutHeader);
        if (!showRenderingInfo) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        DrawStatRow("Renderer 개수", currentProfile.rendererCount.ToString());
        DrawStatRow("예상 Draw Call", currentProfile.drawCallEstimate.ToString(),
            currentProfile.drawCallEstimate > 50 ? Color.red :
            currentProfile.drawCallEstimate > 20 ? Color.yellow : Color.green);

        if (selectedCamera != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"카메라: {selectedCamera.name}", EditorStyles.boldLabel);

            // 카메라와의 거리 계산
            if (Selection.activeGameObject != null)
            {
                float distance = Vector3.Distance(selectedCamera.transform.position,
                    Selection.activeGameObject.transform.position);
                DrawStatRow("카메라와의 거리", $"{distance:F2}m");

                // 화면상 크기 추정
                bool isVisible = IsVisibleFromCamera(Selection.activeGameObject, selectedCamera);
                DrawStatRow("카메라에 보이는지", isVisible ? "예" : "아니오",
                    isVisible ? Color.green : Color.gray);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("프로파일 카메라를 선택하면 더 자세한 정보를 볼 수 있습니다.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    void DrawDetailedList()
    {
        showDetailedList = EditorGUILayout.Foldout(showDetailedList, "상세 메시 리스트", true, EditorStyles.foldoutHeader);
        if (!showDetailedList) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        foreach (var mesh in currentProfile.meshDetails.OrderByDescending(m => m.vertices))
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"오브젝트: {mesh.objectName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  메시: {mesh.meshName}");
            EditorGUILayout.LabelField($"  타입: {(mesh.isSkinnedMesh ? "Skinned Mesh" : "Mesh")}");
            EditorGUILayout.LabelField($"  버텍스: {FormatNumber(mesh.vertices)}");
            EditorGUILayout.LabelField($"  삼각형: {FormatNumber(mesh.triangles)}");
            EditorGUILayout.LabelField($"  서브메시: {mesh.subMeshCount}");
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    void DrawStatRow(string label, string value, Color? valueColor = null)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(180));

        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.fontStyle = FontStyle.Bold;
        if (valueColor.HasValue)
            style.normal.textColor = valueColor.Value;

        EditorGUILayout.LabelField(value, style);
        EditorGUILayout.EndHorizontal();
    }

    // ========== 프로파일링 로직 ==========

    void ProfileSelectedObjects()
    {
        currentProfile = new ProfileData();

        if (Selection.gameObjects.Length == 0) return;

        HashSet<Material> uniqueMaterials = new HashSet<Material>();
        HashSet<string> uniqueShaders = new HashSet<string>();

        foreach (GameObject obj in Selection.gameObjects)
        {
            ProfileObject(obj, uniqueMaterials, uniqueShaders);
        }

        // 고유 머티리얼 및 셰이더 카운트
        currentProfile.uniqueMaterialCount = uniqueMaterials.Count;
        currentProfile.shaderCount = uniqueShaders.Count;

        // Draw Call 추정 (간단한 계산)
        currentProfile.drawCallEstimate = CalculateDrawCallEstimate();
    }

    void ProfileObject(GameObject obj, HashSet<Material> uniqueMaterials, HashSet<string> uniqueShaders)
    {
        // Renderer 수집
        Renderer[] renderers = includeChildren ?
            obj.GetComponentsInChildren<Renderer>(true) :
            obj.GetComponents<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            currentProfile.rendererCount++;

            // 메시 정보
            ProfileMesh(renderer);

            // 머티리얼 정보
            Material[] materials = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;

            foreach (Material mat in materials)
            {
                if (mat == null) continue;

                currentProfile.materialCount++;
                uniqueMaterials.Add(mat);

                if (mat.shader != null)
                {
                    string shaderName = mat.shader.name;
                    uniqueShaders.Add(shaderName);

                    if (!currentProfile.shaderUsage.ContainsKey(shaderName))
                        currentProfile.shaderUsage[shaderName] = 0;

                    currentProfile.shaderUsage[shaderName]++;

                    // 셰이더 복잡도 분석
                    if (!currentProfile.shaderComplexity.ContainsKey(mat.shader))
                    {
                        currentProfile.shaderComplexity[mat.shader] = AnalyzeShaderComplexity(mat);
                    }
                }

                if (!currentProfile.materialUsage.ContainsKey(mat))
                    currentProfile.materialUsage[mat] = 0;

                currentProfile.materialUsage[mat]++;
            }
        }
    }

    void ProfileMesh(Renderer renderer)
    {
        Mesh mesh = null;
        bool isSkinnedMesh = false;
        string objectName = renderer.gameObject.name;

        // SkinnedMeshRenderer
        if (renderer is SkinnedMeshRenderer smr)
        {
            mesh = smr.sharedMesh;
            isSkinnedMesh = true;
            currentProfile.skinnedMeshCount++;
        }
        // MeshFilter
        else if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
                mesh = filter.sharedMesh;
        }

        if (mesh == null) return;

        currentProfile.meshCount++;

        int vertices = mesh.vertexCount;
        int triangles = mesh.triangles.Length / 3;

        currentProfile.totalVertices += vertices;
        currentProfile.totalTriangles += triangles;

        // 메모리 추정 (간단한 계산)
        // 버텍스당 약 32-64 bytes (position, normal, uv, tangent 등)
        currentProfile.estimatedMemoryBytes += vertices * 48;
        // 인덱스 버퍼
        currentProfile.estimatedMemoryBytes += mesh.triangles.Length * 2; // 16bit indices

        // 상세 정보 저장
        currentProfile.meshDetails.Add(new MeshInfo
        {
            objectName = objectName,
            meshName = mesh.name,
            vertices = vertices,
            triangles = triangles,
            subMeshCount = mesh.subMeshCount,
            isSkinnedMesh = isSkinnedMesh
        });
    }

    int CalculateDrawCallEstimate()
    {
        // 간단한 Draw Call 추정
        // 실제로는 배치, 인스턴싱, SRP Batcher 등에 따라 달라짐

        int drawCalls = 0;

        // 각 고유 머티리얼은 최소 1개의 Draw Call
        foreach (var kvp in currentProfile.materialUsage)
        {
            // 서브메시가 여러 개면 Draw Call 증가
            drawCalls += kvp.Value;
        }

        return drawCalls;
    }

    bool IsVisibleFromCamera(GameObject obj, Camera cam)
    {
        if (cam == null) return false;

        Renderer[] renderers = includeChildren ?
            obj.GetComponentsInChildren<Renderer>() :
            obj.GetComponents<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                return true;
        }

        return false;
    }

    // ========== 유틸리티 ==========

    ShaderComplexity AnalyzeShaderComplexity(Material mat)
    {
        var complexity = new ShaderComplexity
        {
            shaderName = mat.shader.name
        };

        List<string> reasons = new List<string>();
        int score = 0; // 복잡도 점수

        // 연산 복잡도 분석
        AnalyzeComputations(mat, complexity);

        // 1. Pass 개수 및 상세 정보 확인
        complexity.passCount = mat.passCount;
        ExtractPassInfo(mat, complexity);

        if (complexity.passCount > 3)
        {
            score += complexity.passCount * 10;
            reasons.Add($"• 렌더 패스 {complexity.passCount}개 (많을수록 무거움)");
        }

        // ForwardAdd 패스가 있으면 추가 점수
        if (complexity.passes.Any(p => p.isForwardAdd))
        {
            score += 15;
            reasons.Add("• ForwardAdd 패스 사용 (라이트당 추가 렌더링)");
        }

        // 2. 활성화된 키워드 확인
        string[] keywords = mat.shaderKeywords;
        complexity.keywordCount = keywords.Length;
        if (keywords.Length > 5)
        {
            score += keywords.Length * 5;
            reasons.Add($"• 활성화된 키워드 {keywords.Length}개");
        }

        // 3. 텍스처 개수 확인
        complexity.textureCount = CountTextures(mat);
        complexity.computation.textureSamples = complexity.textureCount;

        if (complexity.textureCount > 4)
        {
            score += complexity.textureCount * 3;
            reasons.Add($"• 텍스처 {complexity.textureCount}개 사용");
        }

        // 4. 알려진 무거운 셰이더 체크
        string shaderLower = mat.shader.name.ToLower();

        if (shaderLower.Contains("poiyomi") || shaderLower.Contains(".poiyomi"))
        {
            score += 50;
            reasons.Add("• Poiyomi 셰이더 (기능이 많아 무거울 수 있음)");
        }
        else if (shaderLower.Contains("liltoon") || shaderLower.Contains("lilium"))
        {
            score += 40;
            reasons.Add("• lilToon 셰이더 (기능이 많음)");
        }
        else if (shaderLower.Contains("mtoon"))
        {
            score += 20;
            reasons.Add("• MToon 셰이더");
        }
        else if (shaderLower.Contains("standard"))
        {
            score += 15;
            reasons.Add("• Standard 셰이더 (비교적 무거움)");
        }
        else if (shaderLower.Contains("unlit"))
        {
            score -= 10;
            reasons.Add("• Unlit 셰이더 (가벼움)");
        }
        else if (shaderLower.Contains("mobile") || shaderLower.Contains("simple"))
        {
            score -= 5;
            reasons.Add("• 모바일/Simple 셰이더 (최적화됨)");
        }

        // 5. 특정 기능 체크
        if (mat.HasProperty("_ParallaxMap") && mat.GetTexture("_ParallaxMap") != null)
        {
            score += 15;
            reasons.Add("• Parallax Mapping 사용 (GPU 부하 높음)");
        }

        if (mat.HasProperty("_DetailAlbedoMap") && mat.GetTexture("_DetailAlbedoMap") != null)
        {
            score += 5;
            reasons.Add("• Detail Map 사용");
        }

        if (mat.IsKeywordEnabled("_NORMALMAP"))
        {
            score += 5;
        }

        if (mat.IsKeywordEnabled("_EMISSION"))
        {
            score += 3;
        }

        // 연산 복잡도 점수 추가
        score += complexity.computation.mathOperations;
        if (complexity.computation.hasExpensiveMath)
            score += 20;
        if (complexity.computation.hasLoops)
            score += 25;

        // 복잡도 레벨 결정
        if (score < 10)
            complexity.level = ComplexityLevel.VeryLight;
        else if (score < 30)
            complexity.level = ComplexityLevel.Light;
        else if (score < 60)
            complexity.level = ComplexityLevel.Medium;
        else if (score < 100)
            complexity.level = ComplexityLevel.Heavy;
        else
            complexity.level = ComplexityLevel.VeryHeavy;

        // 이유 조합 - 연산 분석 추가
        complexity.reason = $"복잡도 점수: {score}\n";

        // 픽셀당 연산 분석
        complexity.reason += "\n[픽셀 셰이더 연산 분석]\n";
        complexity.reason += $"• 텍스처 샘플링: 약 {complexity.computation.textureSamples}회\n";

        if (complexity.computation.mathOperations > 0)
        {
            string mathLevel = complexity.computation.mathOperations < 10 ? "낮음" :
                              complexity.computation.mathOperations < 30 ? "보통" : "높음";
            complexity.reason += $"• 수학 연산 복잡도: {mathLevel} (점수: {complexity.computation.mathOperations})\n";
        }

        if (complexity.computation.expensiveOps.Count > 0)
        {
            complexity.reason += "• 비싼 연산 감지:\n";
            foreach (var op in complexity.computation.expensiveOps)
            {
                complexity.reason += $"  - {op}\n";
            }
        }

        if (complexity.computation.branchCount > 0)
        {
            complexity.reason += $"• 조건 분기: 약 {complexity.computation.branchCount}개 (GPU 성능 저하 가능)\n";
        }

        if (complexity.computation.hasLoops)
        {
            complexity.reason += "• ⚠️ 반복문 사용 (픽셀당 여러 번 계산)\n";
        }

        complexity.reason += $"\n[렌더 패스 - 총 {complexity.passCount}개]\n";

        if (complexity.passes.Count > 0)
        {
            foreach (var pass in complexity.passes)
            {
                string passDesc = $"• {pass.name}";
                if (!string.IsNullOrEmpty(pass.lightMode))
                    passDesc += $" ({pass.lightMode})";
                complexity.reason += passDesc + "\n";
            }
        }

        complexity.reason += "\n[기타 분석]\n";
        if (reasons.Count > 0)
        {
            complexity.reason += string.Join("\n", reasons);
        }

        // 최적화 제안 추가
        complexity.reason += "\n\n[최적화 제안]";
        if (complexity.level >= ComplexityLevel.Heavy)
        {
            complexity.reason += "\n• 더 가벼운 셰이더로 교체 고려";
            if (complexity.computation.textureSamples > 6)
                complexity.reason += "\n• 텍스처 샘플링 횟수 줄이기 (현재 많음)";
            if (complexity.computation.hasExpensiveMath)
                complexity.reason += "\n• sin/cos/pow 같은 비싼 연산 줄이기";
            if (complexity.computation.hasLoops)
                complexity.reason += "\n• 반복문을 룩업 테이블로 대체 고려";
            if (complexity.computation.branchCount > 5)
                complexity.reason += "\n• 조건 분기 줄이기 (shader variants 활용)";
        }
        else if (complexity.level == ComplexityLevel.Medium)
        {
            complexity.reason += "\n• 모바일에서는 주의 필요";
            complexity.reason += "\n• 사용하지 않는 기능 비활성화";
        }
        else
        {
            complexity.reason += "\n• 현재 복잡도는 적절합니다";
        }

        return complexity;
    }

    void AnalyzeComputations(Material mat, ShaderComplexity complexity)
    {
        var comp = complexity.computation;

        // 1. 텍스처 샘플링 추정
        comp.textureSamples = CountTextures(mat);

        // Matcap, Sphere mapping 등 추가 샘플링
        if (mat.HasProperty("_MatCap") && mat.GetTexture("_MatCap") != null)
        {
            comp.textureSamples++;
            comp.expensiveOps.Add("MatCap 샘플링 (뷰 방향 계산 필요)");
            comp.mathOperations += 5;
        }

        // 2. Normal Map 연산
        if (mat.IsKeywordEnabled("_NORMALMAP") || (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null))
        {
            comp.mathOperations += 5;
            comp.expensiveOps.Add("Normal Map 변환 (tangent space → world space)");
        }

        // 3. Parallax Mapping (매우 비쌈)
        if (mat.HasProperty("_ParallaxMap") && mat.GetTexture("_ParallaxMap") != null)
        {
            comp.textureSamples += 4; // 평균 4번 추가 샘플링
            comp.hasLoops = true;
            comp.mathOperations += 15;
            comp.expensiveOps.Add("Parallax Occlusion Mapping (반복 샘플링 + 레이마칭)");
        }

        // 4. Rim Lighting
        if (mat.IsKeywordEnabled("_RIM") || mat.HasProperty("_RimLightColor"))
        {
            comp.mathOperations += 3;
            comp.expensiveOps.Add("Rim Lighting (dot product + pow)");
            comp.hasExpensiveMath = true;
        }

        // 5. Specular/Reflection
        if (mat.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF") == false)
        {
            comp.mathOperations += 5;
            comp.hasExpensiveMath = true;
            comp.expensiveOps.Add("Specular 계산 (pow 연산)");
        }

        if (mat.HasProperty("_Cube") || mat.HasProperty("_ReflectionTex"))
        {
            comp.textureSamples++;
            comp.mathOperations += 3;
            comp.expensiveOps.Add("Reflection/Cubemap 샘플링");
        }

        // 6. Emission
        if (mat.IsKeywordEnabled("_EMISSION"))
        {
            comp.mathOperations += 2;

            // Animated emission
            if (mat.HasProperty("_EmissionScrollSpeed") || mat.HasProperty("_EmissionPulse"))
            {
                comp.mathOperations += 3;
                comp.hasExpensiveMath = true;
                comp.expensiveOps.Add("Animated Emission (sin/cos 사용 가능)");
            }
        }

        // 7. Outline (추가 패스)
        if (mat.HasProperty("_OutlineWidth") || mat.shader.name.Contains("Outline"))
        {
            comp.mathOperations += 5;
            comp.expensiveOps.Add("Outline Pass (추가 지오메트리 렌더링)");
        }

        // 8. Dissolve/Clipping
        if (mat.HasProperty("_ClippingMask") || mat.HasProperty("_DissolveTex"))
        {
            comp.textureSamples++;
            comp.branchCount++;
            comp.expensiveOps.Add("Dissolve/Clipping (텍스처 + discard)");
        }

        // 9. Fresnel 효과
        if (mat.HasProperty("_FresnelColor") || mat.shader.name.ToLower().Contains("fresnel"))
        {
            comp.mathOperations += 3;
            comp.hasExpensiveMath = true;
            comp.expensiveOps.Add("Fresnel 효과 (pow 연산)");
        }

        // 10. Detail Map
        if (mat.HasProperty("_DetailAlbedoMap") && mat.GetTexture("_DetailAlbedoMap") != null)
        {
            comp.textureSamples += 2;
            comp.mathOperations += 3;
            comp.expensiveOps.Add("Detail Map 블렌딩");
        }

        // 11. Vertex Animation (키워드로 추정)
        if (mat.IsKeywordEnabled("_VERTEX_ANIM") || mat.HasProperty("_VertexOffset"))
        {
            comp.mathOperations += 5;
            comp.hasExpensiveMath = true;
            comp.expensiveOps.Add("버텍스 애니메이션 (sin/cos 사용 가능)");
        }

        // 12. Transparency 블렌딩
        var renderType = mat.GetTag("RenderType", false, "");
        if (renderType.Contains("Transparent") || mat.GetFloat("_Mode") >= 2)
        {
            comp.branchCount++;
            comp.mathOperations += 2;
        }

        // 13. Multiple Light 처리
        if (complexity.passes.Any(p => p.isForwardAdd))
        {
            comp.expensiveOps.Add("⚠️ ForwardAdd: 라이트 1개당 모든 픽셀 재계산");
            comp.mathOperations += 10;
        }

        // 14. Shadow 계산
        if (mat.IsKeywordEnabled("SHADOWS_SCREEN") || mat.IsKeywordEnabled("_RECEIVE_SHADOWS"))
        {
            comp.textureSamples++;
            comp.mathOperations += 3;
            comp.expensiveOps.Add("그림자 샘플링 + PCF 필터링");
        }

        // 15. Poiyomi 특수 기능들
        if (mat.shader.name.Contains("Poiyomi"))
        {
            // Poiyomi는 기능이 엄청 많음
            if (mat.HasProperty("_AudioLink"))
            {
                comp.mathOperations += 10;
                comp.expensiveOps.Add("AudioLink 통합 (실시간 오디오 반응)");
            }

            if (mat.HasProperty("_EnableDistortion"))
            {
                comp.mathOperations += 8;
                comp.expensiveOps.Add("화면 왜곡 효과");
            }
        }

        // 총 연산 복잡도 정규화
        if (comp.mathOperations > 50)
            comp.mathOperations = 50; // 최대값 제한
    }

    void ExtractPassInfo(Material mat, ShaderComplexity complexity)
    {
        Shader shader = mat.shader;

        try
        {
            // SerializedObject를 통해 패스 정보 추출
            SerializedObject serializedShader = new SerializedObject(shader);
            SerializedProperty parsedForm = serializedShader.FindProperty("m_ParsedForm");

            if (parsedForm != null)
            {
                SerializedProperty subShaders = parsedForm.FindPropertyRelative("m_SubShaders");
                if (subShaders != null && subShaders.arraySize > 0)
                {
                    // 첫 번째 SubShader 사용
                    SerializedProperty subShader = subShaders.GetArrayElementAtIndex(0);
                    SerializedProperty passes = subShader.FindPropertyRelative("m_Passes");

                    if (passes != null)
                    {
                        for (int i = 0; i < passes.arraySize && i < complexity.passCount; i++)
                        {
                            SerializedProperty pass = passes.GetArrayElementAtIndex(i);

                            var passInfo = new PassInfo();

                            // 패스 이름
                            SerializedProperty nameIndices = pass.FindPropertyRelative("m_NameIndices");
                            if (nameIndices != null && nameIndices.arraySize > 0)
                            {
                                int nameIndex = nameIndices.GetArrayElementAtIndex(0).intValue;
                                passInfo.name = $"Pass {i}";
                            }
                            else
                            {
                                passInfo.name = $"Pass {i}";
                            }

                            // LightMode 태그 찾기
                            SerializedProperty tags = pass.FindPropertyRelative("m_Tags");
                            if (tags != null && tags.isArray)
                            {
                                for (int j = 0; j < tags.arraySize; j++)
                                {
                                    SerializedProperty tag = tags.GetArrayElementAtIndex(j);
                                    SerializedProperty tagName = tag.FindPropertyRelative("first");
                                    SerializedProperty tagValue = tag.FindPropertyRelative("second");

                                    if (tagName != null && tagName.stringValue == "LightMode")
                                    {
                                        passInfo.lightMode = tagValue?.stringValue ?? "";
                                        passInfo.isForwardAdd = passInfo.lightMode.Contains("ForwardAdd");
                                        passInfo.isShadowCaster = passInfo.lightMode.Contains("ShadowCaster");
                                        break;
                                    }
                                }
                            }

                            complexity.passes.Add(passInfo);
                        }
                    }
                }
            }
        }
        catch
        {
            // Reflection 실패 시 기본 정보만 사용
            for (int i = 0; i < complexity.passCount; i++)
            {
                complexity.passes.Add(new PassInfo
                {
                    name = $"Pass {i}",
                    lightMode = ""
                });
            }
        }

        // 패스 이름이 없으면 기본 이름 추가
        if (complexity.passes.Count == 0)
        {
            for (int i = 0; i < complexity.passCount; i++)
            {
                complexity.passes.Add(new PassInfo
                {
                    name = $"Pass {i}",
                    lightMode = ""
                });
            }
        }
    }

    int CountTextures(Material mat)
    {
        int count = 0;

        // 일반적인 텍스처 프로퍼티들 체크
        string[] commonTexProps = new string[]
        {
            "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap",
            "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_ParallaxMap",
            "_SpecGlossMap", "_DetailMask"
        };

        foreach (string prop in commonTexProps)
        {
            if (mat.HasProperty(prop) && mat.GetTexture(prop) != null)
                count++;
        }

        return count;
    }

    Color GetComplexityColor(ComplexityLevel level)
    {
        return level switch
        {
            ComplexityLevel.VeryLight => new Color(0.5f, 1f, 0.5f), // 밝은 초록
            ComplexityLevel.Light => Color.green,
            ComplexityLevel.Medium => Color.yellow,
            ComplexityLevel.Heavy => new Color(1f, 0.5f, 0f), // 주황
            ComplexityLevel.VeryHeavy => Color.red,
            _ => Color.white
        };
    }

    string GetComplexityLabel(ComplexityLevel level)
    {
        return level switch
        {
            ComplexityLevel.VeryLight => "매우 가벼움",
            ComplexityLevel.Light => "가벼움",
            ComplexityLevel.Medium => "보통",
            ComplexityLevel.Heavy => "무거움",
            ComplexityLevel.VeryHeavy => "매우 무거움",
            _ => "알 수 없음"
        };
    }

    string FormatNumber(int number)
    {
        if (number >= 1000000)
            return $"{number / 1000000.0:F2}M";
        if (number >= 1000)
            return $"{number / 1000.0:F1}K";
        return number.ToString();
    }
}