#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class GradientTextureGenerator : EditorWindow
{
    [Header("텍스처 설정")]
    private int textureWidth = 512;
    private int textureHeight = 512;
    private string textureName = "GradientTexture";

    [Header("그라디언트 설정")]
    private Color startColor = Color.red;
    private Color endColor = Color.blue;
    private float gradientDirection = 0f; // 0-360도
    private float startPosition = 0f; // 0-1
    private float endPosition = 1f; // 0-1

    [Header("고급 설정")]
    private GradientType gradientType = GradientType.Linear;
    private AnimationCurve gradientCurve = AnimationCurve.Linear(0, 0, 1, 1);
    private bool enableAlphaGradient = false;
    private float alphaStart = 1f;
    private float alphaEnd = 1f;

    [Header("미리보기")]
    private Texture2D previewTexture;
    private bool autoUpdate = true;

    private string[] gradientTypeNames = { "선형", "원형", "대각선", "다이아몬드" };

    public enum GradientType
    {
        Linear,     // 선형
        Radial,     // 원형
        Diagonal,   // 대각선
        Diamond     // 다이아몬드
    }

    [MenuItem("Tools/그라디언트 텍스처 생성기")]
    public static void ShowWindow()
    {
        GetWindow<GradientTextureGenerator>("그라디언트 텍스처 생성기");
    }

    void OnEnable()
    {
        CreatePreviewTexture();
        UpdatePreview();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // 제목
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 18;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("그라디언트 텍스처 생성기", titleStyle);

        EditorGUILayout.Space(10);

        // 텍스처 설정
        EditorGUILayout.LabelField("텍스처 설정", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        textureWidth = EditorGUILayout.IntSlider("가로 크기", textureWidth, 64, 2048);
        textureHeight = EditorGUILayout.IntSlider("세로 크기", textureHeight, 64, 2048);
        textureName = EditorGUILayout.TextField("텍스처 이름", textureName);

        EditorGUILayout.Space(5);

        // 그라디언트 설정
        EditorGUILayout.LabelField("그라디언트 설정", EditorStyles.boldLabel);

        gradientType = (GradientType)EditorGUILayout.Popup("그라디언트 타입",
            (int)gradientType, gradientTypeNames);

        startColor = EditorGUILayout.ColorField("시작 색상", startColor);
        endColor = EditorGUILayout.ColorField("끝 색상", endColor);

        if (gradientType == GradientType.Linear || gradientType == GradientType.Diagonal)
        {
            gradientDirection = EditorGUILayout.Slider("방향 (도)", gradientDirection, 0f, 360f);
            EditorGUILayout.LabelField($"방향: {GetDirectionDescription(gradientDirection)}");
        }

        startPosition = EditorGUILayout.Slider("시작 지점", startPosition, 0f, 1f);
        endPosition = EditorGUILayout.Slider("끝 지점", endPosition, 0f, 1f);

        EditorGUILayout.Space(5);

        // 고급 설정
        EditorGUILayout.LabelField("고급 설정", EditorStyles.boldLabel);

        gradientCurve = EditorGUILayout.CurveField("그라디언트 커브", gradientCurve);

        enableAlphaGradient = EditorGUILayout.Toggle("알파 그라디언트 사용", enableAlphaGradient);
        if (enableAlphaGradient)
        {
            alphaStart = EditorGUILayout.Slider("시작 알파", alphaStart, 0f, 1f);
            alphaEnd = EditorGUILayout.Slider("끝 알파", alphaEnd, 0f, 1f);
        }

        EditorGUILayout.Space(5);

        // 자동 업데이트 토글
        autoUpdate = EditorGUILayout.Toggle("자동 미리보기 업데이트", autoUpdate);

        bool changed = EditorGUI.EndChangeCheck();

        // 미리보기 영역
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

        if (previewTexture != null)
        {
            float maxWidth = EditorGUIUtility.currentViewWidth - 40;
            float aspectRatio = (float)textureHeight / textureWidth;
            float previewWidth = Mathf.Min(maxWidth, 256);
            float previewHeight = previewWidth * aspectRatio;

            Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
        }

        EditorGUILayout.Space(10);

        // 버튼들
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("미리보기 업데이트", GUILayout.Height(30)))
        {
            UpdatePreview();
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PNG로 내보내기", GUILayout.Height(30)))
        {
            ExportToPNG();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // 자동 업데이트
        if (changed && autoUpdate)
        {
            UpdatePreview();
        }

        EditorGUILayout.EndVertical();
    }

    void CreatePreviewTexture()
    {
        if (previewTexture != null)
            DestroyImmediate(previewTexture);

        previewTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
    }

    void UpdatePreview()
    {
        if (previewTexture == null ||
            previewTexture.width != textureWidth ||
            previewTexture.height != textureHeight)
        {
            CreatePreviewTexture();
        }

        GenerateGradientTexture(previewTexture);
        previewTexture.Apply();
        Repaint();
    }

    void GenerateGradientTexture(Texture2D texture)
    {
        Color[] pixels = new Color[texture.width * texture.height];

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float normalizedX = (float)x / (texture.width - 1);
                float normalizedY = (float)y / (texture.height - 1);

                float gradientValue = CalculateGradientValue(normalizedX, normalizedY);

                // 시작/끝 지점 적용
                gradientValue = Mathf.Lerp(startPosition, endPosition, gradientValue);
                gradientValue = Mathf.Clamp01(gradientValue);

                // 커브 적용
                gradientValue = gradientCurve.Evaluate(gradientValue);

                // 색상 보간
                Color pixelColor = Color.Lerp(startColor, endColor, gradientValue);

                // 알파 그라디언트 적용
                if (enableAlphaGradient)
                {
                    float alpha = Mathf.Lerp(alphaStart, alphaEnd, gradientValue);
                    pixelColor.a = alpha;
                }

                pixels[y * texture.width + x] = pixelColor;
            }
        }

        texture.SetPixels(pixels);
    }

    float CalculateGradientValue(float x, float y)
    {
        float value = 0f;

        switch (gradientType)
        {
            case GradientType.Linear:
                // 선형 그라디언트
                float angleRad = gradientDirection * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                Vector2 position = new Vector2(x - 0.5f, y - 0.5f);
                value = Vector2.Dot(position, direction) + 0.5f;
                break;

            case GradientType.Radial:
                // 원형 그라디언트 (중심에서 바깥으로)
                float centerX = 0.5f;
                float centerY = 0.5f;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                value = distance * 1.414f; // 대각선 거리로 정규화
                break;

            case GradientType.Diagonal:
                // 대각선 그라디언트
                value = (x + y) * 0.5f;
                break;

            case GradientType.Diamond:
                // 다이아몬드 형태 그라디언트
                float distX = Mathf.Abs(x - 0.5f);
                float distY = Mathf.Abs(y - 0.5f);
                value = (distX + distY);
                break;
        }

        return Mathf.Clamp01(value);
    }

    string GetDirectionDescription(float angle)
    {
        if (angle >= 337.5f || angle < 22.5f) return "→ 오른쪽";
        else if (angle >= 22.5f && angle < 67.5f) return "↗ 우상단";
        else if (angle >= 67.5f && angle < 112.5f) return "↑ 위";
        else if (angle >= 112.5f && angle < 157.5f) return "↖ 좌상단";
        else if (angle >= 157.5f && angle < 202.5f) return "← 왼쪽";
        else if (angle >= 202.5f && angle < 247.5f) return "↙ 좌하단";
        else if (angle >= 247.5f && angle < 292.5f) return "↓ 아래";
        else return "↘ 우하단";
    }

    void ExportToPNG()
    {
        if (string.IsNullOrEmpty(textureName))
        {
            EditorUtility.DisplayDialog("오류", "텍스처 이름을 입력해주세요.", "확인");
            return;
        }

        // 최종 텍스처 생성
        Texture2D finalTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        GenerateGradientTexture(finalTexture);
        finalTexture.Apply();

        // PNG 바이트 변환
        byte[] pngData = finalTexture.EncodeToPNG();

        // 저장 경로 설정
        string defaultPath = "Assets/Textures";
        if (!Directory.Exists(defaultPath))
            Directory.CreateDirectory(defaultPath);

        string fileName = $"{textureName}_{textureWidth}x{textureHeight}.png";
        string fullPath = $"{defaultPath}/{fileName}";

        // 파일 저장
        File.WriteAllBytes(fullPath, pngData);

        // 텍스처 임포트 설정
        AssetDatabase.ImportAsset(fullPath);
        TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = enableAlphaGradient ?
                TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = enableAlphaGradient;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // 정리
        DestroyImmediate(finalTexture);

        EditorUtility.DisplayDialog("완료",
            $"그라디언트 텍스처가 생성되었습니다!\n경로: {fullPath}", "확인");

        // Project 창에서 파일 선택
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
    }

    void OnDisable()
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
        }
    }
}

#endif