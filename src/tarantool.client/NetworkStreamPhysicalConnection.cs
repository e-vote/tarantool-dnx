﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

using Tarantool.Client.Model;
using Tarantool.Client.Utils;

namespace Tarantool.Client
{
    internal class NetworkStreamPhysicalConnection : INetworkStreamPhysicalConnection
    {
        private Stream _stream;

        private Socket _socket;

        private bool _disposed;

        public void Dispose()
        {
            _disposed = true;
            _stream?.Dispose();
        }

        public void Connect(ConnectionOptions options)
        {
            options.LogWriter?.WriteLine("Starting socket connection...");
            _socket = new Socket(options.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(options.EndPoint);
            _stream = new NetworkStream(_socket, true);
            options.LogWriter?.WriteLine("Socket connection established.");
        }
        
        public void Write(byte[] buffer, int offset, int count)
        {
            CheckConnectionStatus();
            _stream.Write(buffer, offset, count);
        }

        public async Task Flush()
        {
            CheckConnectionStatus();

            await _stream.FlushAsync();
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            CheckConnectionStatus();
            return await _stream.ReadAsync(buffer, offset, count);
        }
        
        private void CheckConnectionStatus()
        {
            if (_stream == null)
            {
                throw ExceptionHelper.NotConnected();
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkStreamPhysicalConnection));
            }
        }
    }
}