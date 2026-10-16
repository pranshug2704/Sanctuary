using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// Reward block embedded in quest and objective data: a <see cref="RewardBundleBase"/> plus one int.
/// </summary>
public sealed class QuestRewardData : ISerializableType
{
    public RewardBundleBase Bundle { get; } = new();

    public int Unknown;

    public void Serialize(PacketWriter writer)
    {
        Bundle.Serialize(writer);
        writer.Write(Unknown);
    }
}
