using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Inspect and style the existing town signs without changing their interaction owners.
public static class FaceliftWorldEditor
{
    private static readonly string[] Paths = {
        "Assets/Prefabs/Main/GameMan.prefab", "Assets/Prefabs/Main/BuffingMan.prefab" };

    public static object Audit()
    {
        return Paths.Select(path => {
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                return new { path, transforms = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t == root.transform || t.GetComponent<TextMesh>() != null || t.GetComponent<Collider>() != null)
                    .Select(t => new { name = t.name, parent = t.parent == null ? null : t.parent.name,
                        position = t.localPosition.ToString(), scale = t.localScale.ToString(),
                        rotation = t.localEulerAngles.ToString(), active = t.gameObject.activeSelf,
                        text = t.GetComponent<TextMesh>() == null ? null : Text(t.GetComponent<TextMesh>()),
                        collider = t.GetComponent<Collider>() == null ? null : t.GetComponent<Collider>().GetType().Name,
                        materials = t.GetComponent<Renderer>() == null ? null : t.GetComponent<Renderer>().sharedMaterials.Select(m => m == null ? null : AssetDatabase.GetAssetPath(m)).ToArray()
                    }).ToArray() };
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }).ToArray();
    }

    private static object Text(TextMesh text) => new { text.text, text.fontSize, text.characterSize,
        text.lineSpacing, alignment = text.alignment.ToString(), anchor = text.anchor.ToString(),
        font = AssetDatabase.GetAssetPath(text.font), color = ColorUtility.ToHtmlStringRGBA(text.color),
        bounds = text.GetComponent<Renderer>().bounds.ToString() };

    public static object ApplyHuntSign()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var root = PrefabUtility.LoadPrefabContents(Paths[0]);
        try {
            var sign = root.transform.Find("Game");
            if (sign == null || sign.GetComponent<TextMesh>() == null || sign.Find("GoGameScene").GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException("Original hunting sign and click collider required.");
            var text = sign.GetComponent<TextMesh>();
            text.text = "HUNTING GROUNDS\nTap to enter";
            text.fontSize = 64;
            text.characterSize = .6f;
            text.lineSpacing = 1.1f;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color32(246, 241, 221, 255);
            var back = sign.Find("GoGameScene");
            back.localScale = new Vector3(60, 20, 1);
            back.GetComponent<MeshRenderer>().sharedMaterial = PanelMaterial();
            PrefabUtility.SaveAsPrefabAsset(root, Paths[0]);
            return new { huntingSignStyled = true, originalClickTargetRetained = true };
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static object ApplyBuffSign()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var root = PrefabUtility.LoadPrefabContents(Paths[1]);
        try {
            var sign = root.transform.Find("Admob");
            if (sign == null || sign.GetComponent<TextMesh>() == null || sign.Find("GoogleAdmob").GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException("Original reward sign and click collider required.");
            var text = sign.GetComponent<TextMesh>();
            text.fontSize = 64; text.characterSize = .5f; text.lineSpacing = 1.1f;
            text.alignment = TextAlignment.Center; text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color32(246, 241, 221, 255);
            var back = sign.Find("GoogleAdmob");
            back.localScale = new Vector3(80, 25, 1);
            back.GetComponent<MeshRenderer>().sharedMaterial = PanelMaterial();
            PrefabUtility.SaveAsPrefabAsset(root, Paths[1]);
            return new { rewardSignStyled = true, rewardMessagesAndClickTargetRetained = true };
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Material PanelMaterial()
    {
        const string path = "Assets/Art/Branding/Facelift-World-Panel.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("Existing URP unlit shader required.");
        material = new Material(shader) { name = "Facelift World Panel" };
        material.SetColor("_BaseColor", new Color32(18, 39, 36, 255));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static object RenderHuntSign() => RenderSign(Paths[0], "Game", "GoGameScene", "hunting-sign-after");
    public static object RenderBuffSign() => RenderSign(Paths[1], "Admob", "GoogleAdmob", "reward-sign-after");
    private static object RenderSign(string path, string signName, string backName, string label)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        var previous = RenderTexture.active;
        try {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            var sign = root.transform.Find(signName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = renderer.transform == sign || renderer.transform.IsChildOf(sign);
            sign.localPosition = Vector3.zero;
            sign.localRotation = Quaternion.identity;
            sign.gameObject.SetActive(true);
            var cameraObject = new GameObject("Sign preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 3.2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(126, 174, 77, 255);
            target = new RenderTexture(900, 600, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(900, 600, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 900, 600), 0, 0); image.Apply();
            string output = Path.GetFullPath(".revival-local/facelift-1029/" + label + ".png");
            File.WriteAllBytes(output, image.EncodeToPNG());
            var textBounds = sign.GetComponent<Renderer>().bounds;
            var backBounds = sign.Find(backName).GetComponent<Renderer>().bounds;
            bool inside = textBounds.min.x >= backBounds.min.x && textBounds.max.x <= backBounds.max.x &&
                textBounds.min.y >= backBounds.min.y && textBounds.max.y <= backBounds.max.y;
            return new { image = output, textBounds = textBounds.ToString(), backBounds = backBounds.ToString(), textInsidePanel = inside, visualOnly = true };
        } finally {
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
