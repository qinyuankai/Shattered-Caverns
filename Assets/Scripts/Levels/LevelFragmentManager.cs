using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChunkFragmentRequirement
{
    public int chunkId;
    [Min(1)] public int requiredFragmentCount = 1;
}

public class LevelFragmentManager : MonoBehaviour
{
    [SerializeField] private List<ChunkFragmentRequirement> chunkRequirements =
        new List<ChunkFragmentRequirement>();

    public IReadOnlyList<ChunkFragmentRequirement> ChunkRequirements => chunkRequirements;
}
