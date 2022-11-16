﻿using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace OpenDreamShared.Network.Messages
{
    public sealed class MsgResource : NetMessage
    {
        public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableUnordered;

        public string ResourcePath;
        public byte[] ResourceData;

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            ResourcePath = buffer.ReadString();
            var dataLen = buffer.ReadVariableInt32();
            ResourceData = buffer.ReadBytes(dataLen);
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(ResourcePath);
            buffer.WriteVariableInt32(ResourceData.Length);
            buffer.Write(ResourceData);
        }
    }
}
