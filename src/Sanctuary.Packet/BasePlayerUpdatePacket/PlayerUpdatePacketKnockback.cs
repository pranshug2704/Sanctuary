using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Launches an actor along a ballistic arc to a destination while playing a knockback animation.
/// Client: PlayerUpdatePacketKnockback (35.4), read layout i16 i16 u64 u32 f32x4 f32x4 f32.
/// </summary>
/// <remarks>
/// From the client handler: <see cref="Destination"/> is where the actor lands (the arc starts
/// at its current position), <see cref="FacingDirection"/> is normalized and used as the
/// actor's heading, and <see cref="LaunchStrength"/> is multiplied by 8.5 and added to the
/// arc's vertical velocity. AnimationId &lt;= 0 uses the client default (0x57A) followed by
/// its get-up animation (0x57B).
/// When the target is the receiving player, the client stays in a knocked-back movement
/// state (looping fall, walking in place) until the server sends a
/// ClientUpdatePacketUpdateLocation (Teleport = false) at the landing position once the arc
/// is over (~1.5 s for a 5-15 unit knock). Confirmed in game.
/// </remarks>
public class PlayerUpdatePacketKnockback : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public ulong Guid;

    public int AnimationId;

    public Vector4 Destination;
    public Vector4 FacingDirection;

    public float LaunchStrength;

    public PlayerUpdatePacketKnockback() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Destination);
        writer.Write(FacingDirection);
        writer.Write(LaunchStrength);

        return writer.Buffer;
    }
}
