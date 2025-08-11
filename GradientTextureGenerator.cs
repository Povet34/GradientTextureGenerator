#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class GradientTextureGenerator : EditorWindow
{
    [Header("�ؽ�ó ����")]
    private int textureWidth = 512;
    private int textureHeight = 512;
    private string textureName = "GradientTexture";

    [Header("�׶���Ʈ ����")]
    private Color startColor = Color.red;
    private Color endColor = Color.blue;
    private float gradientDirection = 0f; // 0-360��
    private float startPosition = 0f; // 0-1
    private float endPosition = 1f; // 0-1

    [Header("��� ����")]
    private GradientType gradientType = GradientType.Linear;
    private AnimationCurve gradientCurve = AnimationCurve.Linear(0, 0, 1, 1);
    private bool enableAlphaGradient = false;
    private float alphaStart = 1f;
    private float alphaEnd = 1f;

    [Header("�̸�����")]
    private Texture2D previewTexture;
    private bool autoUpdate = true;

    private string[] gradientTypeNames = { "����", "����", "�밢��", "���̾Ƹ��" };

    public enum GradientType
    {
        Linear,     // ����
        Radial,     // ����
        Diagonal,   // �밢��
        Diamond     // ���̾Ƹ��
    }

    [MenuItem("Tools/�׶���Ʈ �ؽ�ó ������")]
    public static void ShowWindow()
    {
        GetWindow<GradientTextureGenerator>("�׶���Ʈ �ؽ�ó ������");
    }

    void OnEnable()
    {
        CreatePreviewTexture();
        UpdatePreview();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // ����
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 18;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("�׶���Ʈ �ؽ�ó ������", titleStyle);

        EditorGUILayout.Space(10);

        // �ؽ�ó ����
        EditorGUILayout.LabelField("�ؽ�ó ����", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        textureWidth = EditorGUILayout.IntSlider("���� ũ��", textureWidth, 64, 2048);
        textureHeight = EditorGUILayout.IntSlider("���� ũ��", textureHeight, 64, 2048);
        textureName = EditorGUILayout.TextField("�ؽ�ó �̸�", textureName);

        EditorGUILayout.Space(5);

        // �׶���Ʈ ����
        EditorGUILayout.LabelField("�׶���Ʈ ����", EditorStyles.boldLabel);

        gradientType = (GradientType)EditorGUILayout.Popup("�׶���Ʈ Ÿ��",
            (int)gradientType, gradientTypeNames);

        startColor = EditorGUILayout.ColorField("���� ����", startColor);
        endColor = EditorGUILayout.ColorField("�� ����", endColor);

        if (gradientType == GradientType.Linear || gradientType == GradientType.Diagonal)
        {
            gradientDirection = EditorGUILayout.Slider("���� (��)", gradientDirection, 0f, 360f);
            EditorGUILayout.LabelField($"����: {GetDirectionDescription(gradientDirection)}");
        }

        startPosition = EditorGUILayout.Slider("���� ����", startPosition, 0f, 1f);
        endPosition = EditorGUILayout.Slider("�� ����", endPosition, 0f, 1f);

        EditorGUILayout.Space(5);

        // ��� ����
        EditorGUILayout.LabelField("��� ����", EditorStyles.boldLabel);

        gradientCurve = EditorGUILayout.CurveField("�׶���Ʈ Ŀ��", gradientCurve);

        enableAlphaGradient = EditorGUILayout.Toggle("���� �׶���Ʈ ���", enableAlphaGradient);
        if (enableAlphaGradient)
        {
            alphaStart = EditorGUILayout.Slider("���� ����", alphaStart, 0f, 1f);
            alphaEnd = EditorGUILayout.Slider("�� ����", alphaEnd, 0f, 1f);
        }

        EditorGUILayout.Space(5);

        // �ڵ� ������Ʈ ���
        autoUpdate = EditorGUILayout.Toggle("�ڵ� �̸����� ������Ʈ", autoUpdate);

        bool changed = EditorGUI.EndChangeCheck();

        // �̸����� ����
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("�̸�����", EditorStyles.boldLabel);

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

        // ��ư��
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("�̸����� ������Ʈ", GUILayout.Height(30)))
        {
            UpdatePreview();
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PNG�� ��������", GUILayout.Height(30)))
        {
            ExportToPNG();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // �ڵ� ������Ʈ
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

                // ����/�� ���� ����
                gradientValue = Mathf.Lerp(startPosition, endPosition, gradientValue);
                gradientValue = Mathf.Clamp01(gradientValue);

                // Ŀ�� ����
                gradientValue = gradientCurve.Evaluate(gradientValue);

                // ���� ����
                Color pixelColor = Color.Lerp(startColor, endColor, gradientValue);

                // ���� �׶���Ʈ ����
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
                // ���� �׶���Ʈ
                float angleRad = gradientDirection * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                Vector2 position = new Vector2(x - 0.5f, y - 0.5f);
                value = Vector2.Dot(position, direction) + 0.5f;
                break;

            case GradientType.Radial:
                // ���� �׶���Ʈ (�߽ɿ��� �ٱ�����)
                float centerX = 0.5f;
                float centerY = 0.5f;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                value = distance * 1.414f; // �밢�� �Ÿ��� ����ȭ
                break;

            case GradientType.Diagonal:
                // �밢�� �׶���Ʈ
                value = (x + y) * 0.5f;
                break;

            case GradientType.Diamond:
                // ���̾Ƹ�� ���� �׶���Ʈ
                float distX = Mathf.Abs(x - 0.5f);
                float distY = Mathf.Abs(y - 0.5f);
                value = (distX + distY);
                break;
        }

        return Mathf.Clamp01(value);
    }

    string GetDirectionDescription(float angle)
    {
        if (angle >= 337.5f || angle < 22.5f) return "�� ������";
        else if (angle >= 22.5f && angle < 67.5f) return "�� ����";
        else if (angle >= 67.5f && angle < 112.5f) return "�� ��";
        else if (angle >= 112.5f && angle < 157.5f) return "�� �»��";
        else if (angle >= 157.5f && angle < 202.5f) return "�� ����";
        else if (angle >= 202.5f && angle < 247.5f) return "�� ���ϴ�";
        else if (angle >= 247.5f && angle < 292.5f) return "�� �Ʒ�";
        else return "�� ���ϴ�";
    }

    void ExportToPNG()
    {
        if (string.IsNullOrEmpty(textureName))
        {
            EditorUtility.DisplayDialog("����", "�ؽ�ó �̸��� �Է����ּ���.", "Ȯ��");
            return;
        }

        // ���� �ؽ�ó ����
        Texture2D finalTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        GenerateGradientTexture(finalTexture);
        finalTexture.Apply();

        // PNG ����Ʈ ��ȯ
        byte[] pngData = finalTexture.EncodeToPNG();

        // ���� ��� ����
        string defaultPath = "Assets/Textures";
        if (!Directory.Exists(defaultPath))
            Directory.CreateDirectory(defaultPath);

        string fileName = $"{textureName}_{textureWidth}x{textureHeight}.png";
        string fullPath = $"{defaultPath}/{fileName}";

        // ���� ����
        File.WriteAllBytes(fullPath, pngData);

        // �ؽ�ó ����Ʈ ����
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

        // ����
        DestroyImmediate(finalTexture);

        EditorUtility.DisplayDialog("�Ϸ�",
            $"�׶���Ʈ �ؽ�ó�� �����Ǿ����ϴ�!\n���: {fullPath}", "Ȯ��");

        // Project â���� ���� ����
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