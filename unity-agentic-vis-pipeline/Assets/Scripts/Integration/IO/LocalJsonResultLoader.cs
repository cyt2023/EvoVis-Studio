using System;
using System.IO;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.IO
{
    public static class LocalJsonResultLoader
    {
        public static BackendResultRoot LoadFromStreamingAssets(string relativePath)
        {
            var fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Result JSON was not found.", fullPath);
            }

            var json = File.ReadAllText(fullPath);
            var result = JsonUtility.FromJson<BackendResultRoot>(json);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize backend result JSON.");
            }

            return result;
        }
    }
}
