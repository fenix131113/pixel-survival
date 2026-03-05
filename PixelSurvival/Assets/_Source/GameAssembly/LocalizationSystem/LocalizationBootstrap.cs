using UnityEngine;

namespace LocalizationSystem
{
    public class LocalizationBootstrap : MonoBehaviour
    {
        [SerializeField] private LocalizationDatabase database;
        [SerializeField] private string defaultLanguageCode = "en";
        [SerializeField] private bool dontDestroyOnLoad = true;

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            LocalizationService.Initialize(database, defaultLanguageCode);
        }
    }
}
