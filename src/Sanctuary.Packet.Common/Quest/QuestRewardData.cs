using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// Reward block embedded in quest and objective data (client reader FUN_008e7930).
/// </summary>
/// <remarks>
/// Wire layout measured with the emulator harness: the <see cref="RewardBundleBase"/> fields
/// (including its entry list) followed by one extra int the client stores at +0x58.
/// </remarks>
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
