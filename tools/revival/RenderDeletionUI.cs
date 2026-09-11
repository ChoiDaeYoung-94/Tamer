using System;
using System.IO;
using AD;
using AD.Privacy;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class RenderDeletionUI
{
    public static string Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(1080, 1920, 24);
        var texture = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        var prior = RenderTexture.active;
        var output = Path.GetFullPath(".revival-local/deletion-ui");
        Directory.CreateDirectory(output);
        try
        {
            var cameraObject = new GameObject("Deletion verification camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.targetTexture = target;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var canvasObject = new GameObject("Deletion verification canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 10;
            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventSystem, scene);
            var manager = canvasObject.AddComponent<PopupManager>();
            manager.ReleaseException();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/Manager.prefab");
            var property = new SerializedObject(prefab.GetComponent<PopupManager>()).FindProperty("_popupSetting");
            var settings = UnityEngine.Object.Instantiate((GameObject)property.objectReferenceValue, canvasObject.transform, false);
            settings.SetActive(true);
            DeletionSettingsEntry.Ensure(settings, manager);
            Action<string> capture = name =>
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            };
            capture("settings");
            settings.transform.Find("AccountPrivacyEntry").GetComponent<Button>().onClick.Invoke();
            var panel = settings.transform.Find("AccountPrivacy");
            var presenter = panel.GetComponent<DeletionPresenter>();
            presenter.SendMessage("OnEnable");
            presenter.Render(); capture("unavailable-live");
            var session = new DeletionSession(new object(), "synthetic-render", "synthetic-session");
            var gateway = new SyntheticDeletionGateway();
            var flow = new DeletionFlow(gateway, () => session);
            presenter.ConfigureSynthetic(flow);
            flow.RequestAsync().GetAwaiter().GetResult();
            presenter.Render(); capture("confirmation-live");
            return output;
        }
        finally
        {
            RenderTexture.active = prior;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

