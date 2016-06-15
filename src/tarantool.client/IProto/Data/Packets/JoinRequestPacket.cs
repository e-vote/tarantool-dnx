﻿namespace Tarantool.Client.IProto.Data.Packets
{
    public class JoinRequestPacket
    {
        public JoinRequestPacket(int sync, string serverUuid)
        {
            Sync = sync;
            ServerUuid = serverUuid;
        }

        public int Sync { get; }

        public string ServerUuid { get; }
    }
}