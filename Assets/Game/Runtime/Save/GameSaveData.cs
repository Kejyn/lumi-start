using System;
using Lumivale.Core.Persistence;

namespace Lumivale.Start.Game
{
    /// <summary>Game-owned example; no gameplay model is part of lumi-core.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceNamespace: "Lumivale.Start.Phase2Demo", sourceAssembly: "Lumivale.Start.Phase2Demo", sourceClassName: "Phase4DemoSaveData")]
    public sealed class GameSaveData
    {
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public int Counter;

        public static SaveSchema<GameSaveData> CreateSchema()
        {
            return new SaveSchema<GameSaveData>("starter-cube", 1, Validate);
        }

        private static void Validate(GameSaveData data)
        {
            if (data == null || data.Counter < 0 || data.Counter > 1000000 ||
                !ValidCoordinate(data.PositionX) || !ValidCoordinate(data.PositionY) ||
                !ValidCoordinate(data.PositionZ))
                throw new ArgumentException("The save contains an invalid cube position or counter.");
        }

        private static bool ValidCoordinate(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 100000f;
        }
    }
}
