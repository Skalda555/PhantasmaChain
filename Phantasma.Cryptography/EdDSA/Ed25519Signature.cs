﻿using System.Collections.Generic;
using System.IO;

namespace Phantasma.Cryptography.EdDSA
{
    public class Ed25519Signature : Signature
    {
        public byte[] Bytes { get; private set; }

        public override SignatureKind Kind => SignatureKind.Ed25519;

        public Ed25519Signature(byte[] bytes)
        {
            this.Bytes = bytes;
        }

        public override bool Verify(byte[] message, IEnumerable<Address> addresses)
        {
            foreach (var address in addresses)
            {
                if (Ed25519.Verify(this.Bytes, message, address.PublicKey))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
