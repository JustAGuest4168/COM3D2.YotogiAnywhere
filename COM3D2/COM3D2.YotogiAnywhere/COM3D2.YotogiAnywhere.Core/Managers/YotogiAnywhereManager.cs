using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace COM3D2.YotogiAnywhere.Plugin.Core
{
    public class YotogiAnywhereManager : MonoBehaviour
    {
        public bool Initialized { get; private set; }
        public void Initialize()
        {
            //Copied from examples
            if (this.Initialized)
                return;
            YotogiAnywhereHooks.Initialize();
            this.Initialized = true;
            UnityEngine.Debug.Log("YotogiAnywhere: Manager Initialize");
        }

        public void Awake()
        {
            //Copied from examples
            UnityEngine.Debug.Log("YotogiAnywhere: Manager Awake");
            UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)this);
        }
    }
}
