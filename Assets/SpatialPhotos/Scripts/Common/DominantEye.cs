using System;
using UnityEngine;

namespace SpatialPhotos
{
    public enum Eye { Left, Right }

    /// <summary>
    /// The eye people sight through when framing a photo. Measured by <see cref="DominantEyeCalibration"/> and
    /// saved in PlayerPrefs; photos are taken from its viewpoint.
    /// </summary>
    public static class DominantEye
    {
        private const string PrefsKey = "SpatialPhotos.DominantEye";

        /// <summary>Raised after <see cref="Set"/> or <see cref="Clear"/>.</summary>
        public static event Action Changed;

        public static bool HasValue => PlayerPrefs.HasKey(PrefsKey);

        /// <summary>The saved eye, or the right eye (the more common one) until calibrated.</summary>
        public static Eye Current => PlayerPrefs.GetInt(PrefsKey, (int)Eye.Right) == (int)Eye.Left ? Eye.Left : Eye.Right;

        public static void Set(Eye eye)
        {
            PlayerPrefs.SetInt(PrefsKey, (int)eye);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
