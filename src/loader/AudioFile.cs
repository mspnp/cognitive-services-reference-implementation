// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
namespace Fabrikam.Uploader
{
    using System.IO;

    public class AudioFile
    {
        private string _audioFilePath;

        private AudioFile(string filepath)
        {
            _audioFilePath = filepath;
        }

        public string FullPath => Path.GetFullPath(_audioFilePath);

        public string ParentFolderName => new DirectoryInfo(_audioFilePath).Parent.Name;

        public string FileName => Path.GetFileName(_audioFilePath);
        
        public static AudioFile GetAudioFile(string filepath)=>  new AudioFile(filepath);
    }
}