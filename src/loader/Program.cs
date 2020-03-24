// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
namespace Fabrikam.Uploader
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Azure.Storage.Blobs;

    class Program
    {
        public const int SimulatedDelayBetweenUploads = 100;
        
        private static CancellationTokenSource cts;

        private const string BlobInputContainerName = "audiofiles";

        // Maximum number of messages that may be processed by the block concurrently.
        private const int MaxDegreeOfParallelism = 5;

        // The maximum number of messages. The default is -1, which indicates an unlimited
        // number of messages.        
        private const int ObjectPoolBoundedCapacity = 100000;

        private static async Task UploadAudioFilesAsync<T>(ICollection<string> pathList, Func<string, T> factory,
            ObjectPool<BlobServiceClient> pool, int randomSeed, AsyncConsole console, int simulatedDelay, int waittime) where T : AudioFile
        {

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (waittime > 0)
            {
                TimeSpan span = TimeSpan.FromMilliseconds(waittime);
                await Task.Delay(span);
            }

            Random random = new Random(randomSeed);

            // buffer block that holds the messages . consumer will fetch records from this block asynchronously.
            BufferBlock<T> buffer = new BufferBlock<T>(new DataflowBlockOptions()
            {
                BoundedCapacity = 100000
            });

            // consumer that sends the data to blob storage asynchronoulsy.
            var consumer = new ActionBlock<T>(
                (t) =>
                {
                    using (var client = pool.GetObject())
                    {
                        var containerClient = client.Value.GetBlobContainerClient(BlobInputContainerName);
                        return containerClient.UploadBlobAsync(Path.Combine(t.ParentFolderName, t.FileName),
                                                               File.OpenRead(t.FullPath),
                                                               cts.Token).ContinueWith(
                            async task =>
                            {
                                cts.Cancel();
                                await console.WriteLine(task.Exception.InnerException.Message);
                                await console.WriteLine($"Container client failed for {t}");
                            }, TaskContinuationOptions.OnlyOnFaulted
                        );

                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = ObjectPoolBoundedCapacity,
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                }
            );

            // link the buffer to consumer .
            buffer.LinkTo(consumer, new DataflowLinkOptions()
            {
                PropagateCompletion = true
            });

            long files = 0;

            var taskList = new List<Task>();

            var uploadTask = Task.Factory.StartNew(
                async () =>
                {
                    // generate telemetry records and send them to buffer block
                    foreach (var path in pathList)
                    {
                        if (cts.IsCancellationRequested) break;
                        await buffer.SendAsync<T>(factory(path)).ConfigureAwait(false);

                        if (++files % simulatedDelay == 0)
                        {
                            // random delay every 10 files
                            await Task.Delay(random.Next(100, 1000)).ConfigureAwait(false);
                            await console.WriteLine($"Uploaded {files} audiofiles").ConfigureAwait(false);
                          
                        }
                    }

                    buffer.Complete();
                    await Task.WhenAll(buffer.Completion, consumer.Completion).ConfigureAwait(false);
                    await console.WriteLine($"Uploaded total {files} audiofiles").ConfigureAwait(false);
                }
            ).Unwrap().ContinueWith(
                async task =>
                {
                    cts.Cancel();
                    await console.WriteLine($"failed to upload audiofiles").ConfigureAwait(false);
                    await console.WriteLine(task.Exception.InnerException.Message).ConfigureAwait(false);
                }, TaskContinuationOptions.OnlyOnFaulted
            );

            // await on consumer completion. Incase if sending is failed at any moment ,
            // exception is thrown and caught . This is used to signal the cancel the reading operation and abort all activity further

            try
            {
                await Task.WhenAll(consumer.Completion, uploadTask);
            }
            catch (Exception ex)
            {
                cts.Cancel();
                await console.WriteLine(ex.Message).ConfigureAwait(false);
                await console.WriteLine($"failed to upload audiofiles").ConfigureAwait(false);
                throw;
            }

        }

        private static (string BlobStorageConnectionString,
            int MillisecondsToRun, ICollection<string> AudioFiles) ParseArguments()
        {
            var blobStorageConnectionString = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING");
            var numberOfMillisecondsToRun = (int.TryParse(Environment.GetEnvironmentVariable("SECONDS_TO_RUN"), out int outputSecondToRun) ? outputSecondToRun : 0) * 1000;
            var audioFilesFolderPath = Environment.GetEnvironmentVariable("AUDIO_FILES_FOLDER_PATH");

            if (string.IsNullOrWhiteSpace(blobStorageConnectionString))
            {
                throw new ArgumentException("blobStorageConnectionString must be provided");
            }

            if (string.IsNullOrWhiteSpace(audioFilesFolderPath))
            {
                throw new ArgumentException("fareConnectionString must be provided");
            }

            if (!Directory.Exists(audioFilesFolderPath))
            {
                throw new ArgumentException("audioFilesFolderPath does not exists");
            }

            var audioDataFiles = Directory.EnumerateFiles(
                audioFilesFolderPath,
                "*.wav",
                SearchOption.AllDirectories).ToArray();

            return (blobStorageConnectionString, numberOfMillisecondsToRun, audioDataFiles);
        }

        // blocking collection that helps to print to console the messages on progress on the generation/send to blob container
        private class AsyncConsole
        {
            private BlockingCollection<string> _blockingCollection = new BlockingCollection<string>();
            private CancellationToken _cancellationToken;
            private Task _writerTask;

            public AsyncConsole(CancellationToken cancellationToken = default(CancellationToken))
            {
                _cancellationToken = cancellationToken;
                _writerTask = Task.Factory.StartNew((state) =>
                {
                    var token = (CancellationToken)state;
                    string msg;
                    while (!token.IsCancellationRequested)
                    {
                        if (_blockingCollection.TryTake(out msg, 500))
                        {
                            Console.WriteLine(msg);
                        }
                    }

                    while (_blockingCollection.TryTake(out msg, 100))
                    {
                        Console.WriteLine(msg);
                    }
                }, _cancellationToken, TaskCreationOptions.LongRunning);
            }

            public Task WriteLine(string toWrite)
            {
                _blockingCollection.Add(toWrite);
                return Task.FromResult(0);
            }

            public Task WriterTask
            {
                get { return _writerTask; }
            }
        }

        //  start of the file upload task
        public static async Task<int> Main(string[] args)
        {
            try
            {
                // ActionBlock sends the audio files into the buffer and the buffer sends the audio files
                // to Azure destination such as storage container or event hub
                var (BlobStorageConnectionString, MillisecondsToRun, AudioFiles) = ParseArguments();

                cts = MillisecondsToRun == 0 ? new CancellationTokenSource() : new CancellationTokenSource(MillisecondsToRun);

                Console.CancelKeyPress += (s, e) =>
                {
                    Console.WriteLine("Cancelling data upload");
                    cts.Cancel();
                    e.Cancel = true;
                };

                AsyncConsole console = new AsyncConsole(cts.Token);

                var blobServiceClient = new  BlobServiceClient(BlobStorageConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(BlobInputContainerName);
                await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);

                var blobServiceClientPool = new ObjectPool<BlobServiceClient>(() => new BlobServiceClient(BlobStorageConnectionString), 10);

                var tasks = new List<Task>();

                tasks.Add(UploadAudioFilesAsync<AudioFile>(AudioFiles, AudioFile.GetAudioFile, blobServiceClientPool, 100, console, SimulatedDelayBetweenUploads, 1000));
                tasks.Add(console.WriterTask);

                await Task.WhenAll(tasks.ToArray());
                Console.WriteLine("Audio data upload complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Audio data upload failed");
                return 1;
            }

            return 0;
        }
    }
}