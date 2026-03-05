using UnityEngine;

namespace GameAssembly.LocalizationSystem
{
    public class LocalizationBootstrap : MonoBehaviour
    {
        private static LocalizationBootstrap _instance;

        [SerializeField] private LocalizationDatabase database;
        [SerializeField] private string defaultLanguageCode = "en";
        [SerializeField] private bool dontDestroyOnLoad = true;

        private void Awake()
        {
            if (_instance)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            LocalizationService.Initialize(database, defaultLanguageCode);
        }
    }
}
