using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LegacyBlockLoader
{
    internal class AssetLoaderCoroutine : MonoBehaviour
    {
        private static bool RunningCoroutine = false;
        private static IEnumerator<object> coroutine;

        void Update()
        {
            if (!RunningCoroutine)
            {
                if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.AltGr) || Input.GetKey(KeyCode.LeftControl)) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.B))
                {
                    RunningCoroutine = true;
                    coroutine = DirectoryAssetLoader.ReloadAssets();
                }
            }
            else
            {
                while (coroutine.MoveNext())
                {
                    
                }
                coroutine = null;
                RunningCoroutine = false;
            }
        }
    }
}
