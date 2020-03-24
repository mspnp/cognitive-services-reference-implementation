// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using Microsoft.CognitiveServices.Speech.Audio;
using System.IO;

namespace Fabrikam.Cognitive
{
    class AudioStreamReader : PullAudioInputStreamCallback
    {
        private BinaryReader _reader;

        public AudioStreamReader(Stream stream) => _reader = new BinaryReader(stream);

        public override int Read(byte[] dataBuffer, uint size) => _reader.Read(dataBuffer, 0, (int)size);

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _reader.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        private bool _disposed = false;
    }
}