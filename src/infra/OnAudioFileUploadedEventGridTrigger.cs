using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using Microsoft.Azure.EventGrid.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json.Linq;

namespace Fabrikam.Cognitive
{
    public static class OnAudioFileUploadedEventGridTrigger
    {
        private const string BlobCreatedEvent = "Microsoft.Storage.BlobCreated";

        private static readonly string CognitiveServiceApiKey = Environment.GetEnvironmentVariable("CognitiveServiceApiKey",
                EnvironmentVariableTarget.Process);

        private static readonly string CognitiveServiceApiRegion = Environment.GetEnvironmentVariable("CognitiveServiceApiRegion",
                EnvironmentVariableTarget.Process);

        [FunctionName("OnAudioFileUploadedEventGridTrigger")]
        public static async Task Run(
            [EventGridTrigger] EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read, Connection = "AudioBlobConnectionString")] Stream audioBlob, // assumes large blob size so using Stream not String
            Binder binder,
            ILogger logger)
        {
            var config = SpeechConfig.FromSubscription(CognitiveServiceApiKey, CognitiveServiceApiRegion);

            // Get the output file name
            var textFilename = "default.txt";
            if (eventGridEvent.Data is JObject eventGridData)
            {
                if (eventGridEvent.EventType == BlobCreatedEvent)
                {
                    var storageBlobCreatedEventData = eventGridData.ToObject<StorageBlobCreatedEventData>();
                    if (!string.IsNullOrWhiteSpace(storageBlobCreatedEventData.Url))
                    {
                        textFilename = $"{System.IO.Path.GetFileNameWithoutExtension(storageBlobCreatedEventData.Url)}.txt";
                    }
                }
            }

            // Imperative binding to allow for a different storage account and container for transcribed data
            var attributes = new Attribute[]{
                new BlobAttribute(blobPath: $"transcribedfiles/{textFilename}", FileAccess.Write),
                new StorageAccountAttribute("TextBlobConnectionString")
            };

            var textBlob = await binder.BindAsync<Stream>(attributes).ConfigureAwait(false);

            await ConvertAudioToTextAsync(audioBlob, textBlob, config).ConfigureAwait(false);
        }

        private static async Task ConvertAudioToTextAsync(Stream audioBlob, Stream textBlob, SpeechConfig config)
        {
            var completionSource = new TaskCompletionSource<int>();
            using (var audioInput = AudioConfig.FromStreamInput(new AudioStreamReader(audioBlob)))
            {
                using (var recognizer = new SpeechRecognizer(config, audioInput))
                {
                    var streamWriter = new StreamWriter(textBlob);

                    recognizer.Recognized += (s, e) => streamWriter.Write(e.Result);

                    recognizer.SessionStopped += (s, e) =>
                    {
                        streamWriter.Flush();
                        streamWriter.Dispose();
                        completionSource.TrySetResult(0);
                    };

                    await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                    await Task.WhenAny(new[] { completionSource.Task });

                    await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
