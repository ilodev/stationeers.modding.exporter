using UnityEngine;

namespace stationeers.modding.exporter
{
    public class EditorScriptableSingleton<T> where T : ScriptableObject
    {
        private static T _instance;

        public EditorScriptableSingleton(T instance = null)
        {
            if (instance != null)
                _instance = instance;

            if (_instance == null)
                _instance = GetInstance();
        }
        //Note: Unity versions 5.6 and earlier fail to load ScriptableObject assets for Types that are defined in an editor assembly 
        //and derive from a Type defined in a non-editor assembly.

        public T instance
        {
            get
            {
                if (_instance == null)
                    GetInstance();

                return _instance;
            }
        }

        private T GetInstance()
        {
            if (_instance == null)
            {
                _instance = Resources.Load<T>(typeof(T).Name);
            }

            // If _instance is still null it means the resource file doesn't exist.
            if (_instance == null)
            {
                _instance = ScriptableObject.CreateInstance<T>();
                Debug.Log($"Creating asset resource. {_instance}");
                AssetUtility.CreateAsset(_instance);
            }
            return _instance;
        }
    }
}