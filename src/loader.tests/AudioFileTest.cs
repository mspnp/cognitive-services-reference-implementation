// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using System;
using Xunit;
using Fabrikam.Uploader;

namespace Fabrikam.Uploader.Tests
{
    public class AudioFileTest
    {
        [Fact]
        public void AudioFile_IsParentFolderName()
        {
            var someFilePath = @"C:\Folder1\Folder2\Folder3\SomeAudioFile.wav";
            var expectedFolderName = "Folder3";
            var audioFile = AudioFile.GetAudioFile(someFilePath);
            Assert.Equal(expectedFolderName, audioFile.ParentFolderName);
        }
    }
}
