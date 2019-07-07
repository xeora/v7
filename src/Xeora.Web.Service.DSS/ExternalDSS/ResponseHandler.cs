﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Xeora.Web.Service.Dss
{
    public class ResponseHandler
    {
        private readonly TcpClient _DssServiceClient;
        private readonly ConcurrentDictionary<long, byte[]> _ResponseResults;

        public ResponseHandler(ref TcpClient dssServiceClient)
        {
            this._DssServiceClient = dssServiceClient;
            this._ResponseResults = new ConcurrentDictionary<long, byte[]>();
        }

        public async void HandleAsync()
        {
            try
            {
                await Task.Run(() => this.Handle());
            }
            catch
            { /* Just Handle Exceptions */ }
        }

        public byte[] WaitForMessage(long requestId)
        {
            do
            {
                if (this._ResponseResults.TryRemove(requestId, out byte[] message))
                    return message;

                Thread.Sleep(1);
            } while (true);
        }

        private void Handle()
        {
            byte[] head = new byte[8];
            int bR = 0;

            Stream responseStream = this._DssServiceClient.GetStream();
            do
            {
                // Read Head
                bR += responseStream.Read(head, bR, head.Length - bR);
                if (bR == 0)
                {
                    // give time to fill buffer
                    Thread.Sleep(1);

                    continue;
                }

                if (bR < 8)
                    continue;

                this.Consume(ref responseStream, head);

                bR = 0;
            } while (true);
        }

        private void Consume(ref Stream responseStream, byte[] contentHead)
        {
            // 8 bytes first 5 bytes are requestId, remain 3 bytes are request length. Request length can be max 15Mb
            long head = BitConverter.ToInt64(contentHead, 0);

            long requestId = head >> 24;
            int contentSize = (int)(head & 0xFFFFFF);

            byte[] buffer = new byte[8192];
            int bR = 0;

            Stream contentStream = null;
            try
            {
                contentStream = new MemoryStream();

                while (contentSize > 0)
                {
                    int readLength = buffer.Length;
                    if (contentSize < readLength)
                        readLength = contentSize;

                    bR = responseStream.Read(buffer, 0, readLength);

                    contentStream.Write(buffer, 0, bR);

                    contentSize -= bR;
                }

                byte[] messageBlock = ((MemoryStream)contentStream).ToArray();
                this._ResponseResults.TryAdd(requestId, messageBlock);
            }
            catch
            {
                this._ResponseResults.TryAdd(requestId, null);
            }
            finally
            {
                if (contentStream != null)
                {
                    contentStream.Close();
                    GC.SuppressFinalize(contentStream);
                }
            }
        }
    }
}
