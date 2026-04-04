using UnityEngine;

namespace RTS.Units
{
    [System.Serializable]
    public struct BuildingProgress
    {
        public enum BuildingState
        {
            Building,
            Paused,
            Completed,
            Destroyed
        }

        [field: SerializeField] public float StartTime { get; private set; }

        [field: SerializeField] public float Progress01 { get; private set; }

        [field: SerializeField] public BuildingState State { get; private set; }

        public BuildingProgress(BuildingState state, float startTime, float progress01)
        {
            State = state;
            StartTime = startTime;
            Progress01 = progress01;
        }
    }
}