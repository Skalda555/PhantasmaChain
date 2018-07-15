﻿using Phantasma.Core;
using System;
using System.IO;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal class RaftReplicateMessage : Message
    {
        public readonly Block block;

        public RaftReplicateMessage(byte[] pubKey, Block block) : base(Opcode.RAFT_Replicate, pubKey)
        {
            Throw.IfNull(block, nameof(block));
            this.block = block;
        }

        internal static RaftReplicateMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var block = Block.Unserialize(reader);
            return new RaftReplicateMessage(pubKey, block);
        }
    }
}