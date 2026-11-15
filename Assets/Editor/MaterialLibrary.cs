using System.IO;
using Corrida;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera as texturas procedurais (PNG) e cria os materiais do jogo em Assets/Resources/Materiais,
/// combinando as texturas fotográficas CC0 (ambientCG) com normal maps e oclusão.
/// Menu: Corrida > Recriar materiais.
/// </summary>
[InitializeOnLoad]
public static class MaterialLibrary
{
    const string TexDir = "Assets/Textures";
    const string GenDir = "Assets/Textures/Gerado";
    const string MatDir = "Assets/Resources/Materiais";

    static MaterialLibrary()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(MatDir + "/Asfalto.mat")) BuildAll();
        };
    }

    [MenuItem("Corrida/Recriar materiais")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(GenDir);
        Directory.CreateDirectory(MatDir);

        Png("Zebra", Tex.Zebra());
        Png("Quadriculado", Tex.Checker());
        Png("Paineis", Tex.Paineis());
        Png("Pneus", Tex.Pneus());
        Png("Torcida", Tex.Torcida());
        Png("Garagens", Tex.Garagens());
        Png("Predio", Tex.Predio());
        Png("Carbono", Tex.Carbono());
        Png("Folhas", Tex.Folhas());
        Png("Folhas_Normal", Tex.FolhasNormal());
        Png("Agua_Normal", Tex.AguaNormal());
        AssetDatabase.Refresh();

        // texturas fotográficas (UV em metros: tiling = 1 / tamanho do ladrilho)
        Mat("Asfalto", Photo("Asphalt031"), new Color(0.48f, 0.48f, 0.5f), Tile(5f), 0.22f, 0f, 0.8f);
        Mat("Grama", Photo("Grass004"), new Color(0.85f, 0.95f, 0.8f), Tile(3.5f), 0.08f);
        Mat("Terreno", Photo("Ground037"), new Color(0.36f, 0.5f, 0.28f), Tile(9f), 0.05f);
        Mat("Concreto", Photo("Concrete034"), new Color(0.95f, 0.95f, 0.95f), Tile(3f), 0.12f);
        Mat("Casca", Photo("Bark014"), new Color(0.9f, 0.85f, 0.8f), new Vector2(2f, 2f), 0.05f);
        Mat("Metal", Photo("CorrugatedSteel009"), new Color(0.85f, 0.87f, 0.9f), Tile(2.5f), 0.45f, 0.6f);
        Mat("Borracha", Photo("Rubber004"), new Color(0.28f, 0.28f, 0.28f), new Vector2(1f, 1f), 0.25f);

        // texturas geradas
        Mat("Zebra", Gen("Zebra"), Color.white, new Vector2(1f, 0.25f), 0.3f);
        Mat("Quadriculado", Gen("Quadriculado"), Color.white, Vector2.one, 0.3f);
        Mat("Paineis", Gen("Paineis"), Color.white, new Vector2(1f, 1f / 48f), 0.35f);
        Mat("Pneus", Gen("Pneus"), Color.white, new Vector2(1f, 0.25f), 0.2f);
        Mat("Torcida", Gen("Torcida"), Color.white, new Vector2(1f / 8f, 1f), 0.05f);
        Mat("Garagens", Gen("Garagens"), Color.white, new Vector2(1f / 32f, 1f / 5f), 0.25f);
        Mat("Predio", Gen("Predio"), Color.white, new Vector2(1f / 4f, 1f / 3f), 0.3f);
        Mat("Carbono", Gen("Carbono"), Color.white, new Vector2(4f, 4f), 0.85f, 0.2f);
        Mat("Folhas", new Maps { color = Gen("Folhas").color, normal = Gen("Folhas_Normal").color }, Color.white, new Vector2(3f, 3f), 0.1f, 0f, 1.2f);
        Mat("Agua", new Maps { normal = Gen("Agua_Normal").color }, new Color(0.1f, 0.27f, 0.38f), new Vector2(10f, 10f), 0.95f, 0.1f, 0.6f);

        // cores lisas
        Mat("PinturaCarro", new Maps(), Color.red, Vector2.one, 0.85f, 0.35f);
        Mat("Vidro", new Maps(), new Color(0.05f, 0.07f, 0.1f), Vector2.one, 0.95f, 0.3f);
        Mat("Cromado", new Maps(), new Color(0.8f, 0.8f, 0.82f), Vector2.one, 0.9f, 1f);
        Mat("Linha", new Maps(), new Color(0.95f, 0.95f, 0.93f), Vector2.one, 0.3f);
        Mat("Tinta", new Maps(), Color.white, Vector2.one, 0.4f);
        Mat("Farol", new Maps(), new Color(1f, 0.97f, 0.85f), Vector2.one, 0.9f, 0f, 1f, new Color(1.2f, 1.1f, 0.9f));
        Mat("LanternaOff", new Maps(), new Color(0.35f, 0.02f, 0.02f), Vector2.one, 0.8f);
        Mat("LanternaOn", new Maps(), new Color(0.9f, 0.05f, 0.03f), Vector2.one, 0.8f, 0f, 1f, new Color(2.5f, 0.1f, 0.05f));

        Sky();
        var textShader = Shader.Find("Corrida/Texto3D");
        if (textShader != null) EditorUtility.SetDirty(GetOrCreate("Texto", textShader));
        AssetDatabase.SaveAssets();
        Debug.Log("[Corrida] Materiais criados em " + MatDir);
    }

    struct Maps { public Texture2D color, normal, ao, metal; }

    static Vector2 Tile(float meters) => new Vector2(1f / meters, 1f / meters);

    static Maps Photo(string id)
    {
        string b = $"{TexDir}/{id}/{id}_";
        return new Maps
        {
            color = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "Color.jpg"),
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "NormalGL.jpg"),
            ao = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "AmbientOcclusion.jpg"),
        };
    }

    static Maps Gen(string name) => new Maps { color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GenDir}/{name}.png") };

    static void Png(string name, Texture2D t)
    {
        string path = $"{GenDir}/{name}.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    static Material GetOrCreate(string name, Shader shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else m.shader = shader;
        return m;
    }

    static void Mat(string name, Maps maps, Color color, Vector2 tiling, float smooth, float metal = 0f, float bump = 1f, Color? emission = null)
    {
        var m = GetOrCreate(name, Shader.Find("Standard"));
        m.color = color;
        m.SetTexture("_MainTex", maps.color);
        m.mainTextureScale = tiling;
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        if (maps.normal != null)
        {
            m.SetTexture("_BumpMap", maps.normal);
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }
        m.SetTexture("_OcclusionMap", maps.ao);
        m.SetFloat("_OcclusionStrength", maps.ao != null ? 0.8f : 1f);
        if (emission.HasValue)
        {
            m.SetColor("_EmissionColor", emission.Value);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
    }

    static void Sky()
    {
        var sh = Shader.Find("Skybox/Procedural");
        if (sh == null) return;
        var m = GetOrCreate("Ceu", sh);
        m.SetFloat("_SunSize", 0.035f);
        m.SetFloat("_SunSizeConvergence", 6f);
        m.SetFloat("_AtmosphereThickness", 0.85f);
        m.SetColor("_SkyTint", new Color(0.42f, 0.58f, 0.85f));
        m.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.4f));
        m.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(m);
    }
}

/// <summary>Configura a importação: normal maps e filtragem das texturas do jogo.</summary>
public class CorridaTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Textures/")) return;
        var ti = (TextureImporter)assetImporter;
        if (assetPath.Contains("_Normal")) ti.textureType = TextureImporterType.NormalMap;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.anisoLevel = 8;
        ti.mipmapEnabled = true;
        ti.filterMode = assetPath.Contains("Quadriculado") ? FilterMode.Point : FilterMode.Trilinear;
        if (assetPath.Contains("_AmbientOcclusion")) ti.sRGBTexture = false;
    }
}
