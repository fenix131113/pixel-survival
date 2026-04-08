using GameAssembly.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameAssembly.MainMenuSystem
{
    public static class MenuButtonFeedbackBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (!scene.IsValid() || scene.name != ScenesData.MENU_SCENE_NAME)
                return;

            InstallFeedback(scene);
        }

        private static void InstallFeedback(Scene scene)
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (!button || button.gameObject.scene != scene)
                    continue;

                if (button.TryGetComponent<MenuButtonFeedback>(out _))
                    continue;

                button.gameObject.AddComponent<MenuButtonFeedback>();
            }
        }
    }
}
